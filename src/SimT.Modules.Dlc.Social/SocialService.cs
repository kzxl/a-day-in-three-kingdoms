using SimT.Contracts;
using SimT.Contracts.Data;
using SimT.Contracts.Ledger;
using SimT.Contracts.Logging;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Social;

internal sealed class SocialService : ISocialNetwork
{
    private readonly IDbSession _db;
    private readonly IGameClock _clock;
    private readonly IWallet _wallet;
    private readonly IBag _bag;
    private readonly INpcRegistry _registry;
    private readonly IWorldMap _map;
    private readonly IPlayerState _player;
    private readonly IPlayerAttributes _attributes;
    private readonly IItemCatalog _items;
    private readonly ILog _log;

    public SocialService(
        IDbSession db,
        IGameClock clock,
        IWallet wallet,
        IBag bag,
        INpcRegistry registry,
        IWorldMap map,
        IPlayerState player,
        IPlayerAttributes attributes,
        IItemCatalog items,
        ILog log)
    {
        _db = db;
        _clock = clock;
        _wallet = wallet;
        _bag = bag;
        _registry = registry;
        _map = map;
        _player = player;
        _attributes = attributes;
        _items = items;
        _log = log;
    }

    public RelationshipDef GetRelationship(long npcId)
    {
        var rel = _db.QueryOne(
            "SELECT npc_id, affinity, trust, impression, gift_count, last_interaction_date " +
            "FROM kz_relationship WHERE npc_id = @id",
            record => new RelationshipDef(
                record.GetInt64(0),
                record.GetInt32(1),
                record.GetInt32(2),
                record.GetString(3),
                record.GetInt32(4),
                GameDateCodec.Parse(record.GetString(5))),
            DbArgs.One("@id", npcId));

        return rel ?? new RelationshipDef(npcId, 0, 50, "Sơ giao", 0, _clock.Today);
    }

    public IReadOnlyList<RelationshipDef> AllRelationships()
    {
        return _db.Query(
            "SELECT npc_id, affinity, trust, impression, gift_count, last_interaction_date " +
            "FROM kz_relationship ORDER BY affinity DESC",
            record => new RelationshipDef(
                record.GetInt64(0),
                record.GetInt32(1),
                record.GetInt32(2),
                record.GetString(3),
                record.GetInt32(4),
                GameDateCodec.Parse(record.GetString(5))));
    }

    public bool HasMet(long npcId)
    {
        return _db.ScalarLong(
            "SELECT COUNT(*) FROM kz_relationship WHERE npc_id = @id",
            DbArgs.One("@id", npcId)) > 0;
    }

    public IReadOnlyList<MemoryEntry> GetMemories(long npcId, int limit = 20)
    {
        return _db.Query(
            "SELECT id, npc_id, date, topic, sentiment, affinity_delta, summary_vi " +
            "FROM kz_memory WHERE npc_id = @id ORDER BY id DESC LIMIT @limit",
            record => new MemoryEntry(
                record.GetInt64(0),
                record.GetInt64(1),
                GameDateCodec.Parse(record.GetString(2)),
                record.GetString(3),
                record.GetString(4),
                record.GetInt32(5),
                record.GetString(6)),
            new DbArgs().Add("@id", npcId).Add("@limit", Math.Clamp(limit, 1, 100)));
    }

    public IReadOnlyList<RetainerDef> GetRetainers()
    {
        return _db.Query(
            "SELECT id, npc_id, role_assigned, monthly_stipend, loyalty, recruited_date " +
            "FROM kz_retainer ORDER BY id ASC",
            record => new RetainerDef(
                record.GetInt64(0),
                record.GetInt64(1),
                record.GetString(2),
                record.GetInt32(3),
                record.GetInt32(4),
                GameDateCodec.Parse(record.GetString(5))));
    }

    public RetainerDef? GetRetainer(long npcId)
    {
        return _db.QueryOne(
            "SELECT id, npc_id, role_assigned, monthly_stipend, loyalty, recruited_date " +
            "FROM kz_retainer WHERE npc_id = @id",
            record => new RetainerDef(
                record.GetInt64(0),
                record.GetInt64(1),
                record.GetString(2),
                record.GetInt32(3),
                record.GetInt32(4),
                GameDateCodec.Parse(record.GetString(5))),
            DbArgs.One("@id", npcId));
    }

    public NpcRank GetNpcRank(long npcId)
    {
        var npc = _registry.Find(npcId);
        return npc is null ? NpcRank.C : EvaluateNpcRank(npc);
    }

    public static NpcRank EvaluateNpcRank(NpcDef npc)
    {
        if (npc.Role == "merchant" || npc.Role == "tavern" ||
            npc.Code.StartsWith("thuong_nhan") || npc.Code.StartsWith("chu_quan"))
        {
            return NpcRank.C;
        }

        var a = npc.Attributes;
        var sum = a.War + a.Ldr + a.Int + a.Pol + a.Dip + a.Com;
        var maxStat = Math.Max(a.War, Math.Max(a.Ldr, Math.Max(a.Int, Math.Max(a.Pol, Math.Max(a.Dip, a.Com)))));
        var hasTier5Skill = npc.Skills.Any(s => s.Tier >= 5);
        var hasTier4Skill = npc.Skills.Any(s => s.Tier == 4);

        if (hasTier5Skill || sum >= 480 || maxStat >= 94)
        {
            return NpcRank.S;
        }

        if (hasTier4Skill || sum >= 430 || maxStat >= 84)
        {
            return NpcRank.A;
        }

        if (npc.Skills.Any(s => s.Tier == 3) || sum >= 370 || maxStat >= 75)
        {
            return NpcRank.B;
        }

        return NpcRank.C;
    }

    public static bool IsWarlord(NpcDef npc)
    {
        var code = npc.Code;
        var role = npc.Role;
        if (code is "npc_0001" or "npc_0004" or "npc_0014" or "npc_0016" or "npc_0018" or "npc_0019" or "npc_0020" or "npc_0021" or "npc_0022" or "npc_0023")
        {
            return true;
        }

        return role.Contains("Hoàng Đế") || role.Contains("Minh Chủ") || role.Contains("Thái Sư") ||
               role.Contains("Thứ Sử") || role.Contains("Châu Mục") || role.Contains("Thái Bình Đạo Giáo Tổ");
    }

    public RecruitRequirementDef GetRecruitRequirement(long npcId)
    {
        var npc = _registry.Find(npcId);
        if (npc is null)
        {
            return new RecruitRequirementDef(NpcRank.C, 35, 50, 250, 40, false, false, "Không tìm thấy nhân sĩ");
        }

        var rank = EvaluateNpcRank(npc);
        var isWarlord = IsWarlord(npc);
        var (reqAffinity, reqFame, signingFee, minStipend) = GetRankRequirements(rank);

        var rel = GetRelationship(npcId);
        var playerFame = _attributes.Social.Fame;
        var hasEnoughAffinity = rel.Affinity >= reqAffinity;
        var hasEnoughFame = playerFame >= reqFame;
        var hasEnoughCash = _wallet.Liquid >= signingFee;

        var canRecruit = !isWarlord && hasEnoughAffinity && hasEnoughFame && hasEnoughCash && GetRetainer(npcId) is null;

        var statusSummary = isWarlord
            ? "Chư hầu cát cứ, không nhận chiêu mộ"
            : GetRetainer(npcId) is not null
                ? "Đã là gia thần trướng hạ"
                : !hasEnoughAffinity
                    ? $"Cần Hảo cảm {reqAffinity}/100 (hiện có: {rel.Affinity})"
                    : !hasEnoughFame
                        ? $"Cần Uy danh {reqFame:N0} (hiện có: {playerFame:N0})"
                        : !hasEnoughCash
                            ? $"Cần Sính lễ {signingFee:N0} Quan (hiện có: {_wallet.Liquid:N0})"
                            : "Đủ điều kiện chiêu mộ";

        return new RecruitRequirementDef(
            rank,
            reqAffinity,
            reqFame,
            signingFee,
            minStipend,
            isWarlord,
            canRecruit,
            statusSummary);
    }

    public static (int ReqAffinity, int ReqFame, int SigningFee, int MinStipend) GetRankRequirements(NpcRank rank) => rank switch
    {
        NpcRank.S => (80, 1200, 1500, 120),
        NpcRank.A => (60, 600, 800, 70),
        NpcRank.B => (45, 250, 450, 50),
        NpcRank.C => (35, 50, 250, 40),
        _ => (35, 50, 250, 40)
    };

    public string ChatWithNpc(long npcId)
    {
        var npc = _registry.Find(npcId)
                  ?? throw new SimException("social.npc_not_found", $"Không tìm thấy nhân sĩ mã {npcId}.");

        if (npc.CitadelId != _player.CurrentCityId && GetRetainer(npcId) is null)
        {
            var cit = _map.Find(npc.CitadelId);
            var citName = cit?.NameVi ?? "châu quận khác";
            return $"{npc.NameVi} hiện đang ngụ cư tại {citName}, không có mặt tại cùng thành trì với bạn để đàm đạo.";
        }

        if (_attributes.Vitals.CurrentStamina < 5 || _attributes.Vitals.Fatigue >= 95)
        {
            return $"Thân thể mệt mỏi, tinh thần uể oải ({_attributes.Vitals.Fatigue}/100 mệt). Hãy nghỉ ngơi hoặc chuyển ngày để hồi phục thể lực trước khi đàm đạo.";
        }

        var today = _clock.Today;
        var formattedDate = GameDateCodec.Format(today);

        // Player applies fatigue for mental engagement
        _attributes.ApplyFatigue(5);

        var rank = EvaluateNpcRank(npc);
        var pAttr = _attributes.Attributes;
        var nAttr = npc.Attributes;
        var currentRel = GetRelationship(npcId);
        var playerFame = _attributes.Social.Fame;

        // Aloofness check for top tiers:
        if (rank == NpcRank.S && playerFame < 300)
        {
            var brushGain = 1;
            var brushAffinity = Math.Clamp(currentRel.Affinity + brushGain, -100, 100);
            var brushTrust = Math.Clamp(currentRel.Trust + 1, 0, 100);
            var brushImp = CalculateImpression(brushAffinity);

            _db.Transaction(() =>
            {
                _db.Execute(
                    "INSERT INTO kz_relationship (npc_id, affinity, trust, impression, gift_count, last_interaction_date) " +
                    "VALUES (@npcId, @aff, @trust, @imp, @gifts, @date) " +
                    "ON CONFLICT(npc_id) DO UPDATE SET " +
                    "affinity = @aff, trust = @trust, impression = @imp, last_interaction_date = @date",
                    new DbArgs()
                        .Add("@npcId", npcId)
                        .Add("@aff", brushAffinity)
                        .Add("@trust", brushTrust)
                        .Add("@imp", brushImp)
                        .Add("@gifts", currentRel.GiftCount)
                        .Add("@date", formattedDate));

                _db.Execute(
                    "INSERT INTO kz_memory (npc_id, date, topic, sentiment, affinity_delta, summary_vi) " +
                    "VALUES (@npcId, @date, 'chat', 'neutral', @delta, @summary)",
                    new DbArgs()
                        .Add("@npcId", npcId)
                        .Add("@date", formattedDate)
                        .Add("@delta", brushGain)
                        .Add("@summary", $"Diện kiến {npc.NameVi} nhưng chưa đủ danh vọng kết giao, đối phương khách sáo cáo từ (+1 Hảo cảm)."));
            });

            _attributes.AddExp("DIP", 10);
            _attributes.AddExp("PRC", 5);
            return $"{npc.NameVi} khẽ vuốt râu mỉm cười khách sáo: 'Tại hạ quân vụ bề bộn, thứ lỗi chưa tiện cùng các hạ đàm luận sâu.' (Uy danh < 300 đối với hào kiệt phẩm S: +1 Hảo cảm). Mối quan hệ: {brushAffinity}/100 ({brushImp}).";
        }

        if (rank == NpcRank.A && playerFame < 150)
        {
            var brushGain = 1;
            var brushAffinity = Math.Clamp(currentRel.Affinity + brushGain, -100, 100);
            var brushTrust = Math.Clamp(currentRel.Trust + 1, 0, 100);
            var brushImp = CalculateImpression(brushAffinity);

            _db.Transaction(() =>
            {
                _db.Execute(
                    "INSERT INTO kz_relationship (npc_id, affinity, trust, impression, gift_count, last_interaction_date) " +
                    "VALUES (@npcId, @aff, @trust, @imp, @gifts, @date) " +
                    "ON CONFLICT(npc_id) DO UPDATE SET " +
                    "affinity = @aff, trust = @trust, impression = @imp, last_interaction_date = @date",
                    new DbArgs()
                        .Add("@npcId", npcId)
                        .Add("@aff", brushAffinity)
                        .Add("@trust", brushTrust)
                        .Add("@imp", brushImp)
                        .Add("@gifts", currentRel.GiftCount)
                        .Add("@date", formattedDate));

                _db.Execute(
                    "INSERT INTO kz_memory (npc_id, date, topic, sentiment, affinity_delta, summary_vi) " +
                    "VALUES (@npcId, @date, 'chat', 'neutral', @delta, @summary)",
                    new DbArgs()
                        .Add("@npcId", npcId)
                        .Add("@date", formattedDate)
                        .Add("@delta", brushGain)
                        .Add("@summary", $"Gặp gỡ {npc.NameVi}, hàn huyên xã giao (+1 Hảo cảm)."));
            });

            _attributes.AddExp("DIP", 10);
            _attributes.AddExp("PRC", 5);
            return $"Đàm đạo cùng {npc.NameVi}: cuộc trò chuyện lịch thiệp nhưng đối phương còn giữ khoảng cách (+1 Hảo cảm). Mối quan hệ: {brushAffinity}/100 ({brushImp}).";
        }

        // Base gain scaled by rank and diminishing returns based on current affinity
        int baseGain;
        if (rank == NpcRank.C)
        {
            baseGain = currentRel.Affinity < 40 ? 4 : currentRel.Affinity < 80 ? 3 : 1;
        }
        else if (rank == NpcRank.B)
        {
            baseGain = currentRel.Affinity < 45 ? 4 : currentRel.Affinity < 80 ? 3 : 1;
        }
        else if (rank == NpcRank.A)
        {
            baseGain = currentRel.Affinity < 60 ? 3 : currentRel.Affinity < 80 ? 2 : 1;
        }
        else // Rank S
        {
            baseGain = currentRel.Affinity < 60 ? 2 : 1;
        }

        var gain = baseGain;

        // Player diplomacy bonus
        if (pAttr.Dip >= 85 && currentRel.Affinity < 80) gain += 1;
        else if (pAttr.Dip >= 70 && currentRel.Affinity < 50) gain += 1;

        var debateNotes = new List<string>();

        // Skill synergies
        if (npc.Skills.Any(s => s.Code == "nhan_duc") && currentRel.Affinity < 70) gain += 1;
        if (_attributes.IsSkillEquipped("nguyet_dan_giam_dinh"))
        {
            gain += 2;
            debateNotes.Add("tuyệt kỹ [Nguyệt Đán Giám Định] thấu hiểu tâm can (+2 Hảo cảm, +15 Độ thuần thục)");
        }

        // Scholarly debate / strategy resonance
        if (nAttr.Int >= 75)
        {
            if (pAttr.Int >= nAttr.Int - 10)
            {
                if (currentRel.Affinity < 80) gain += 1;
                _attributes.AddExp("INT", 20);
                debateNotes.Add("luận đàm sách lược tâm đầu ý hợp (+20 INT EXP)");
            }
            else
            {
                _attributes.AddExp("INT", 10);
            }
        }

        // Martial camaraderie
        if (nAttr.War >= 80)
        {
            if (pAttr.War >= 75)
            {
                if (currentRel.Affinity < 80) gain += 1;
                _attributes.AddExp("WAR", 20);
                debateNotes.Add("đàm đạo thương pháp sảng khoái (+20 WAR EXP)");
            }
            else
            {
                _attributes.AddExp("WAR", 10);
            }
        }

        // Mercantile synergy
        if (nAttr.Com >= 75 && pAttr.Com >= 70)
        {
            if (currentRel.Affinity < 80) gain += 1;
            _attributes.AddExp("COM", 20);
            debateNotes.Add("chia sẻ nhãn quan buôn bán (+20 COM EXP)");
        }

        // Hard cap gain per interaction by Rank
        var maxGain = rank switch
        {
            NpcRank.S => currentRel.Affinity >= 80 ? 1 : 3,
            NpcRank.A => currentRel.Affinity >= 80 ? 2 : 4,
            NpcRank.B => currentRel.Affinity >= 80 ? 2 : 4,
            _ => currentRel.Affinity >= 80 ? 3 : 5
        };
        gain = Math.Clamp(gain, 1, maxGain);

        var newAffinity = Math.Clamp(currentRel.Affinity + gain, -100, 100);
        var newTrust = Math.Clamp(currentRel.Trust + 2, 0, 100);
        var newImpression = CalculateImpression(newAffinity);

        _db.Transaction(() =>
        {
            _db.Execute(
                "INSERT INTO kz_relationship (npc_id, affinity, trust, impression, gift_count, last_interaction_date) " +
                "VALUES (@npcId, @aff, @trust, @imp, @gifts, @date) " +
                "ON CONFLICT(npc_id) DO UPDATE SET " +
                "affinity = @aff, trust = @trust, impression = @imp, last_interaction_date = @date",
                new DbArgs()
                    .Add("@npcId", npcId)
                    .Add("@aff", newAffinity)
                    .Add("@trust", newTrust)
                    .Add("@imp", newImpression)
                    .Add("@gifts", currentRel.GiftCount)
                    .Add("@date", formattedDate));

            var debateText = debateNotes.Count > 0 ? $", {string.Join(", ", debateNotes)}" : "";
            _db.Execute(
                "INSERT INTO kz_memory (npc_id, date, topic, sentiment, affinity_delta, summary_vi) " +
                "VALUES (@npcId, @date, 'chat', 'positive', @delta, @summary)",
                new DbArgs()
                    .Add("@npcId", npcId)
                    .Add("@date", formattedDate)
                    .Add("@delta", gain)
                    .Add("@summary", $"Đàm đạo cùng {npc.NameVi} tại trà quán ({rank}){debateText} (+{gain} Hảo cảm)."));
        });

        _attributes.AddExp("DIP", 15);
        _attributes.AddExp("PRC", 5);

        if (_attributes.IsSkillEquipped("nguyet_dan_giam_dinh"))
        {
            _attributes.TrainSkill("nguyet_dan_giam_dinh", 15);
        }
        if (_attributes.IsSkillEquipped("ba_lac_than_nhan"))
        {
            _attributes.TrainSkill("ba_lac_than_nhan", 10);
        }
        if (_attributes.IsSkillEquipped("vong_khi_thien_co"))
        {
            _attributes.TrainSkill("vong_khi_thien_co", 10);
        }

        var skillHint = "";
        var unlearnedSkill = npc.Skills.FirstOrDefault(s => !_attributes.HasSkill(s.Code));
        var reqSkillAff = unlearnedSkill?.Tier >= 5 ? 75 : unlearnedSkill?.Tier == 4 ? 55 : 35;
        if (newAffinity >= reqSkillAff && unlearnedSkill is not null)
        {
            skillHint = $" {npc.NameVi} sẵn sàng truyền dạy đặc kỹ [{unlearnedSkill.NameVi}]!";
        }

        var extraInfo = debateNotes.Count > 0 ? $" ({string.Join("; ", debateNotes)})" : "";
        return $"Đàm đạo cùng {npc.NameVi} (Phẩm {rank}): +{gain} Hảo cảm{extraInfo}. Quan hệ hiện tại: {newAffinity}/100 ({newImpression}).{skillHint}";
    }

    public string GiftNpc(long npcId, long itemId)
    {
        var npc = _registry.Find(npcId)
                  ?? throw new SimException("social.npc_not_found", $"Không tìm thấy nhân sĩ mã {npcId}.");

        if (npc.CitadelId != _player.CurrentCityId && GetRetainer(npcId) is null)
        {
            var cit = _map.Find(npc.CitadelId);
            var citName = cit?.NameVi ?? "châu quận khác";
            throw new SimException("social.not_in_same_city", $"{npc.NameVi} hiện đang ở {citName}, không có mặt tại đây để nhận lễ vật.");
        }

        var item = _items.Find(itemId)
                   ?? throw new SimException("social.item_not_found", $"Vật phẩm mã {itemId} không tồn tại.");

        var rank = EvaluateNpcRank(npc);

        // High-rank NPCs reject mundane/cheap bulk commodities
        if (rank == NpcRank.S && item.BasePrice < 35 && (item.Category == "grain" || item.Category == "material" || item.Category == "food"))
        {
            throw new SimException("social.gift_rejected",
                $"{npc.NameVi} khẽ xua tay từ chối: 'Tại hạ chí tại bốn phương, lễ mọn {item.NameVi} này xin các hạ mang về dùng hoặc chẩn cấp dân nghèo.'");
        }

        if (rank == NpcRank.A && item.BasePrice < 20 && (item.Category == "grain" || item.Category == "material"))
        {
            throw new SimException("social.gift_rejected",
                $"{npc.NameVi} mỉm cười từ chối: 'Vật phẩm {item.NameVi} tầm thường này không tiện nhận, xin các hạ giữ lại.'");
        }

        var currentCityId = _player.CurrentCityId;
        if (_bag.Quantity(currentCityId, itemId) <= 0)
        {
            throw new SimException("social.item_shortage", $"Trong hành trang tại thành không có {item.NameVi}.");
        }

        if (!_bag.Remove(currentCityId, itemId, 1))
        {
            throw new SimException("social.gift_failed", $"Không thể lấy {item.NameVi} khỏi túi.");
        }

        var today = _clock.Today;
        var formattedDate = GameDateCodec.Format(today);

        // Preference calculation by character archetype
        var nAttr = npc.Attributes;
        var preferenceMultiplier = 1.0;
        var isFavorite = false;

        // Martial archetype: weapons/materials (sat, dong) & wine (ruou)
        if ((nAttr.War >= 75 || npc.Role.Contains("Tướng") || npc.Role.Contains("Hổ")) &&
            (item.Code == "sat" || item.Code == "dong" || item.Code == "ruou"))
        {
            preferenceMultiplier = 1.5;
            isFavorite = true;
        }
        // Scholar archetype: tea, agarwood, herbs
        else if ((nAttr.Int >= 75 || nAttr.Pol >= 75 || npc.Role.Contains("Sĩ") || npc.Role.Contains("Thừa Tướng")) &&
                 (item.Code == "tra" || item.Code == "tram_huong" || item.Code == "sam" || item.Code == "thuoc"))
        {
            preferenceMultiplier = 1.5;
            isFavorite = true;
        }
        // Merchant archetype: silks, gems, agarwood, tea
        else if ((nAttr.Com >= 70 || npc.Role == "merchant") &&
                 (item.Code == "lua" || item.Code == "gam" || item.Code == "tram_huong" || item.Code == "tra" || item.Code == "dong"))
        {
            preferenceMultiplier = 1.5;
            isFavorite = true;
        }
        else if (item.Category == "luxury")
        {
            preferenceMultiplier = 1.3;
        }

        // Base price value factor
        var currentRel = GetRelationship(npcId);
        int gain;

        if (rank == NpcRank.C)
        {
            // Rank C: baseline 6 to 18 (e.g. tea 35 -> 4 * 1.5 = 6)
            var baseFactor = Math.Clamp((int)(item.BasePrice / 15), 4, 15);
            gain = (int)Math.Round(baseFactor * preferenceMultiplier);
            if (currentRel.Affinity >= 80) gain = Math.Max(2, (int)(gain * 0.5));
            else if (currentRel.Affinity >= 50) gain = Math.Max(3, (int)(gain * 0.75));
        }
        else if (rank == NpcRank.B)
        {
            // Rank B: 4 to 12
            var baseFactor = Math.Clamp((int)(item.BasePrice / 20), 3, 12);
            gain = (int)Math.Round(baseFactor * preferenceMultiplier * 0.85);
            if (currentRel.Affinity >= 80) gain = Math.Max(1, (int)(gain * 0.4));
            else if (currentRel.Affinity >= 50) gain = Math.Max(2, (int)(gain * 0.65));
        }
        else if (rank == NpcRank.A)
        {
            // Rank A: 3 to 8
            var baseFactor = Math.Clamp((int)(item.BasePrice / 25), 2, 10);
            gain = (int)Math.Round(baseFactor * preferenceMultiplier * 0.65);
            if (currentRel.Affinity >= 80) gain = Math.Max(1, (int)(gain * 0.35));
            else if (currentRel.Affinity >= 50) gain = Math.Max(2, (int)(gain * 0.55));
        }
        else // Rank S
        {
            // Rank S: 2 to 6
            var baseFactor = Math.Clamp((int)(item.BasePrice / 30), 2, 8);
            gain = (int)Math.Round(baseFactor * preferenceMultiplier * 0.45);
            if (currentRel.Affinity >= 80) gain = Math.Max(1, (int)(gain * 0.3));
            else if (currentRel.Affinity >= 50) gain = Math.Max(1, (int)(gain * 0.5));
        }

        gain = Math.Max(1, gain);

        var newAffinity = Math.Clamp(currentRel.Affinity + gain, -100, 100);
        var newTrust = Math.Clamp(currentRel.Trust + 4, 0, 100);
        var newGifts = currentRel.GiftCount + 1;
        var newImpression = CalculateImpression(newAffinity);

        var favText = isFavorite ? " rất tâm đắc món quà hợp ý này" : "";
        _db.Transaction(() =>
        {
            _db.Execute(
                "INSERT INTO kz_relationship (npc_id, affinity, trust, impression, gift_count, last_interaction_date) " +
                "VALUES (@npcId, @aff, @trust, @imp, @gifts, @date) " +
                "ON CONFLICT(npc_id) DO UPDATE SET " +
                "affinity = @aff, trust = @trust, impression = @imp, gift_count = @gifts, last_interaction_date = @date",
                new DbArgs()
                    .Add("@npcId", npcId)
                    .Add("@aff", newAffinity)
                    .Add("@trust", newTrust)
                    .Add("@imp", newImpression)
                    .Add("@gifts", newGifts)
                    .Add("@date", formattedDate));

            _db.Execute(
                "INSERT INTO kz_memory (npc_id, date, topic, sentiment, affinity_delta, summary_vi) " +
                "VALUES (@npcId, @date, 'gift', 'positive', @delta, @summary)",
                new DbArgs()
                    .Add("@npcId", npcId)
                    .Add("@date", formattedDate)
                    .Add("@delta", gain)
                    .Add("@summary", $"Tặng phẩm {item.NameVi} cho {npc.NameVi} ({rank}){favText} (+{gain} Hảo cảm)."));
        });

        _attributes.AddExp("DIP", 20);

        return $"Đã biếu tặng {item.NameVi} cho {npc.NameVi} (Phẩm {rank}){favText}: +{gain} Hảo cảm. Mối quan hệ: {newAffinity}/100 ({newImpression}).";
    }

    public string RecruitRetainer(long npcId, string role, int stipend)
    {
        var npc = _registry.Find(npcId)
                  ?? throw new SimException("social.npc_not_found", $"Không tìm thấy nhân sĩ mã {npcId}.");

        if (npc.CitadelId != _player.CurrentCityId)
        {
            var cit = _map.Find(npc.CitadelId);
            var citName = cit?.NameVi ?? "châu quận khác";
            throw new SimException("social.not_in_same_city", $"{npc.NameVi} hiện đang ở {citName}, cần diện kiến trực tiếp tại cùng một thành trì để chiêu mộ.");
        }

        var existing = GetRetainer(npcId);
        if (existing is not null)
        {
            throw new SimException("social.already_retained", $"{npc.NameVi} đã là gia thần của bạn.");
        }

        if (IsWarlord(npc))
        {
            throw new SimException("social.warlord_unrecruitable",
                $"{npc.NameVi} là một phương kiêu hùng chư hầu nắm giữ vạn quân xã tắc, quyết không thể quy phục làm gia thần dưới trướng người khác!");
        }

        var rank = EvaluateNpcRank(npc);
        var (reqAffinity, reqFame, recruitmentFee, minStipend) = GetRankRequirements(rank);
        if (_attributes.IsSkillEquipped("ba_lac_than_nhan"))
        {
            reqAffinity = Math.Max(20, reqAffinity - 15);
            reqFame = (int)(reqFame * 0.85);
        }
        var rel = GetRelationship(npcId);

        if (rel.Affinity < reqAffinity)
        {
            throw new SimException("social.affinity_insufficient",
                $"{npc.NameVi} chưa đủ tin tưởng bạn (Hảo cảm: {rel.Affinity}/100, cần tối thiểu {reqAffinity} đối với nhân sĩ phẩm {rank}). Hãy trò chuyện và tặng quà thêm.");
        }

        var playerFame = _attributes.Social.Fame;
        if (playerFame < reqFame)
        {
            throw new SimException("social.fame_insufficient",
                $"Uy danh của bạn chưa đủ để chiêu mộ nhân sĩ phẩm {rank} như {npc.NameVi} (Uy danh hiện có: {playerFame:N0}, cần tối thiểu {reqFame:N0}). Hãy lập thêm chiến công và thanh thế tại các châu quận.");
        }

        if (_wallet.Liquid < recruitmentFee)
        {
            throw new SimException("social.cash_shortage",
                $"Không đủ ngân lượng chiêu hiền đãi sĩ (cần {recruitmentFee:N0} Quan sính lễ kết giao nhân sĩ phẩm {rank}, hiện có {_wallet.Liquid:N0} Quan).");
        }

        var validRoles = new[] { "bodyguard", "accountant", "advisor", "manager" };
        var assignedRole = validRoles.Contains(role) ? role : AutoAssignRole(npc.Attributes);
        var finalStipend = Math.Max(minStipend, stipend);

        // Persuasion check
        var pAttr = _attributes.Attributes;
        var pSocial = _attributes.Social;
        var baseScore = rank switch
        {
            NpcRank.S => 45,
            NpcRank.A => 60,
            NpcRank.B => 75,
            _ => 100 // Rank C is guaranteed when prerequisites are met
        };

        var dipBonus = (pAttr.Dip - 50) / 2;
        var virBonus = (pSocial.Virtue - 50) / 3;
        var affBonus = (rel.Affinity - reqAffinity) * 2;
        var stiBonus = Math.Min(15, (finalStipend - minStipend) / 5);
        var baLacBonus = _attributes.IsSkillEquipped("ba_lac_than_nhan") ? 15 : 0;
        var totalPersuasionScore = baseScore + dipBonus + virBonus + affBonus + stiBonus + baLacBonus;

        if (totalPersuasionScore < 50)
        {
            // Persuasion failed: polite decline, small affinity dip (-2)
            var todayDate = _clock.Today;
            var fDate = GameDateCodec.Format(todayDate);
            _db.Transaction(() =>
            {
                _db.Execute(
                    "UPDATE kz_relationship SET affinity = MAX(-100, affinity - 2) WHERE npc_id = @id",
                    DbArgs.One("@id", npcId));

                _db.Execute(
                    "INSERT INTO kz_memory (npc_id, date, topic, sentiment, affinity_delta, summary_vi) " +
                    "VALUES (@npcId, @date, 'recruit_declined', 'neutral', -2, @summary)",
                    new DbArgs()
                        .Add("@npcId", npcId)
                        .Add("@date", fDate)
                        .Add("@summary", $"{npc.NameVi} khéo léo từ chối lời mời gia nhập vì chí hướng chưa đồng điệu (-2 Hảo cảm)."));
            });

            return $"{npc.NameVi} khẽ lắc đầu từ tốn: 'Chí hướng của tại hạ còn gửi nơi bốn bể, duyên hội ngộ cùng các hạ chưa đủ chín muồi. Xin đa tạ thịnh tình!' (Chiêu mộ chưa thành công, hảo cảm -2; hãy nâng cao Khẩu Tài, Đức Độ hoặc đãi ngộ bổng lộc hậu hĩnh hơn).";
        }

        var today = _clock.Today;
        var formattedDate = GameDateCodec.Format(today);
        var initialLoyalty = Math.Clamp(65 + (rel.Affinity / 5) + (pSocial.Virtue / 10), 60, 100);

        _db.Transaction(() =>
        {
            _wallet.Charge(recruitmentFee, "social.recruit", $"Sính lễ chiêu mộ gia thần phẩm {rank}: {npc.NameVi}", $"recruit:{npcId}:{formattedDate}");

            _db.Execute(
                "INSERT INTO kz_retainer (npc_id, role_assigned, monthly_stipend, loyalty, recruited_date) " +
                "VALUES (@npcId, @role, @stipend, @loyalty, @date)",
                new DbArgs()
                    .Add("@npcId", npcId)
                    .Add("@role", assignedRole)
                    .Add("@stipend", finalStipend)
                    .Add("@loyalty", initialLoyalty)
                    .Add("@date", formattedDate));

            _db.Execute(
                "INSERT INTO kz_memory (npc_id, date, topic, sentiment, affinity_delta, summary_vi) " +
                "VALUES (@npcId, @date, 'recruit', 'positive', 15, @summary)",
                new DbArgs()
                    .Add("@npcId", npcId)
                    .Add("@date", formattedDate)
                    .Add("@summary", $"{npc.NameVi} ({rank}) cảm kích tri ngộ sính lễ {recruitmentFee} Quan, chính thức bái nhập môn hạ làm {RoleTitleVi(assignedRole)}."));
        });

        _attributes.AddExp("DIP", 25);
        _attributes.GainFame(15, $"Chiêu mộ hiền tài phẩm {rank}: {npc.NameVi}");

        if (_attributes.IsSkillEquipped("ba_lac_than_nhan"))
        {
            _attributes.TrainSkill("ba_lac_than_nhan", 25);
        }

        if (assignedRole is "bodyguard" or "advisor")
        {
            _registry.UpdateCitadel(npcId, _player.CurrentCityId);
        }

        _log.Info($"Recruited NPC {npc.NameVi} ({rank}) as {assignedRole} with stipend {finalStipend} and fee {recruitmentFee}.");
        return $"Đã chiêu mộ thành công {npc.NameVi} (Phẩm {rank}) làm {RoleTitleVi(assignedRole)}! (Sính lễ: {recruitmentFee:N0} Quan, Bổng lộc: {finalStipend} Quan/tháng, Lòng trung thành: {initialLoyalty}/100).";
    }

    public string DismissRetainer(long npcId)
    {
        var ret = GetRetainer(npcId)
                  ?? throw new SimException("social.not_a_retainer", "Nhân sĩ này không phải gia thần của bạn.");

        var npc = _registry.Find(npcId);
        var name = npc?.NameVi ?? $"Mã {npcId}";

        var today = _clock.Today;
        var formattedDate = GameDateCodec.Format(today);

        _db.Transaction(() =>
        {
            _db.Execute("DELETE FROM kz_retainer WHERE npc_id = @id", DbArgs.One("@id", npcId));

            _db.Execute(
                "INSERT INTO kz_memory (npc_id, date, topic, sentiment, affinity_delta, summary_vi) " +
                "VALUES (@npcId, @date, 'dismiss', 'negative', -10, @summary)",
                new DbArgs()
                    .Add("@npcId", npcId)
                    .Add("@date", formattedDate)
                    .Add("@summary", $"{name} rời khỏi trướng hạ, đường ai nấy đi."));
        });

        return $"Đã cho {name} xuất ngũ, giải trừ chức vụ gia thần.";
    }

    public string LearnSkillFromNpc(long npcId, string skillCode)
    {
        var npc = _registry.Find(npcId)
                  ?? throw new SimException("social.npc_not_found", $"Không tìm thấy nhân sĩ mã {npcId}.");

        if (npc.CitadelId != _player.CurrentCityId && GetRetainer(npcId) is null)
        {
            var cit = _map.Find(npc.CitadelId);
            var citName = cit?.NameVi ?? "châu quận khác";
            return $"{npc.NameVi} hiện đang ở {citName}, không thể trực tiếp truyền dạy đặc kỹ.";
        }

        var skill = npc.Skills.FirstOrDefault(s => s.Code.Equals(skillCode, StringComparison.OrdinalIgnoreCase))
                    ?? throw new SimException("social.skill_not_found", $"Nhân sĩ {npc.NameVi} không sở hữu tuyệt kỹ '{skillCode}'.");

        if (_attributes.HasSkill(skill.Code))
        {
            return $"Bạn đã lĩnh hội tuyệt kỹ [{skill.NameVi}] từ trước, hãy tiếp tục tu luyện!";
        }

        var reqAffinity = skill.Tier switch
        {
            >= 5 => 75,
            4 => 55,
            _ => 35
        };

        var rel = GetRelationship(npcId);
        if (rel.Affinity < reqAffinity)
        {
            return $"{npc.NameVi} khẽ lắc đầu từ tốn: 'Mối quan hệ đôi bên chưa đủ thâm sâu (Hảo cảm hiện tại: {rel.Affinity}/100, cần ≥ {reqAffinity} đối với tuyệt kỹ bậc {skill.Tier}). Tuyệt kỹ bản môn chưa thể tùy tiện truyền thụ.'";
        }

        var fameCheck = CheckSkillFameRequirement(skill);
        if (!fameCheck.Met)
        {
            return $"{npc.NameVi} trầm ngâm vuốt râu: 'Tuyệt kỹ bậc {skill.Tier} [{skill.NameVi}] danh bất hư truyền, chỉ truyền thụ cho bậc kỳ tài đã vang danh thiên hạ (Yêu cầu Danh Vọng ≥ {fameCheck.Required}, hiện tại: {fameCheck.Current}).'";
        }

        var statCheck = CheckSkillStatRequirement(skill);
        if (!statCheck.Met)
        {
            return $"{npc.NameVi} mỉm cười lắc đầu: 'Căn cơ tu vi của bạn chưa đủ để ngộ tuyệt kỹ [{skill.NameVi}] (Yêu cầu chỉ số {statCheck.StatName} ≥ {statCheck.Required}, hiện tại: {statCheck.Current}). Hãy rèn luyện thêm!'";
        }

        var tuition = GetSkillTuitionFee(skill);
        if (_wallet.Balance < tuition)
        {
            return $"Lễ bái sư thỉnh giáo tuyệt kỹ [{skill.NameVi}] cần dâng lễ {tuition} Quan kính sư (Ngân quỹ hiện tại: {_wallet.Balance:N0} Quan).";
        }

        if (_attributes.Vitals.CurrentStamina < 15 || _attributes.Vitals.Fatigue >= 90)
        {
            return $"Thân thể mệt mỏi, khó lòng ngộ đạo ({_attributes.Vitals.Fatigue}/100 mệt). Hãy nghỉ ngơi trước khi thỉnh giáo tuyệt kỹ.";
        }

        _wallet.Charge(tuition, "social.tuition", $"Bái sư thỉnh giáo tuyệt kỹ [{skill.NameVi}] từ {npc.NameVi}");
        _attributes.ApplyFatigue(15);
        _clock.Advance(1);

        var today = _clock.Today;
        var formattedDate = GameDateCodec.Format(today);

        _attributes.LearnSkill(skill.Code);

        var newAffinity = Math.Clamp(rel.Affinity + 5, -100, 100);
        var newTrust = Math.Clamp(rel.Trust + 5, 0, 100);
        var newImp = CalculateImpression(newAffinity);

        _db.Transaction(() =>
        {
            _db.Execute(
                "INSERT INTO kz_relationship (npc_id, affinity, trust, impression, gift_count, last_interaction_date) " +
                "VALUES (@npcId, @aff, @trust, @imp, @gifts, @date) " +
                "ON CONFLICT(npc_id) DO UPDATE SET " +
                "affinity = @aff, trust = @trust, impression = @imp, last_interaction_date = @date",
                new DbArgs()
                    .Add("@npcId", npcId)
                    .Add("@aff", newAffinity)
                    .Add("@trust", newTrust)
                    .Add("@imp", newImp)
                    .Add("@gifts", rel.GiftCount)
                    .Add("@date", formattedDate));

            _db.Execute(
                "INSERT INTO kz_memory (npc_id, date, topic, sentiment, affinity_delta, summary_vi) " +
                "VALUES (@npcId, @date, 'learn', 'positive', 5, @summary)",
                new DbArgs()
                    .Add("@npcId", npcId)
                    .Add("@date", formattedDate)
                    .Add("@summary", $"Thỉnh giáo học đạo: Được {npc.NameVi} chỉ điểm, ngộ ra tuyệt kỹ [{skill.NameVi}] ({skill.EffectSummary})."));
        });

        var expAttr = skill.Category switch
        {
            "martial" => "WAR",
            "strategist" => "INT",
            "commerce" => "COM",
            "governance" => "POL",
            _ => "WIL"
        };
        _attributes.AddExp(expAttr, 30);

        _log.Info($"Player learned skill {skill.Code} from NPC {npc.NameVi}.");
        return $"[Thụ Nghiệp Thành Công] Dưới sự chỉ điểm tận tình của {npc.NameVi}, bạn đã giác ngộ và lĩnh hội thành công đặc kỹ [{skill.NameVi}] ({skill.EffectSummary})! (-{tuition} Quan, +5 Hảo cảm, +30 EXP {expAttr}). Tuyệt kỹ đã tự động kích hoạt vào ô trang bị.";
    }

    public string TrainSkillWithNpc(long npcId, string skillCode)
    {
        var npc = _registry.Find(npcId)
                  ?? throw new SimException("social.npc_not_found", $"Không tìm thấy nhân sĩ mã {npcId}.");

        if (npc.CitadelId != _player.CurrentCityId && GetRetainer(npcId) is null)
        {
            var cit = _map.Find(npc.CitadelId);
            var citName = cit?.NameVi ?? "châu quận khác";
            return $"{npc.NameVi} hiện đang ở {citName}, không thể cùng luận đạo tu luyện.";
        }

        var skill = npc.Skills.FirstOrDefault(s => s.Code.Equals(skillCode, StringComparison.OrdinalIgnoreCase))
                    ?? throw new SimException("social.skill_not_found", $"Nhân sĩ {npc.NameVi} không sở hữu tuyệt kỹ '{skillCode}'.");

        if (!_attributes.HasSkill(skill.Code))
        {
            return $"Bạn chưa lĩnh hội tuyệt kỹ [{skill.NameVi}], hãy bái sư thỉnh giáo trước khi tu luyện!";
        }

        var progress = _attributes.GetSkillProgress(skill.Code);
        if (progress != null && progress.Level >= 5)
        {
            return $"Tuyệt kỹ [{skill.NameVi}] của bạn đã đạt cảnh giới Tông Sư (Cực Hạn), không thể thăng tiến thêm cùng sư phụ.";
        }

        if (_wallet.Balance < 30)
        {
            return $"Chi phí trà đàm và thỉnh giáo tu luyện cần 30 Quan kính trà (Hiện có: {_wallet.Balance:N0} Quan).";
        }

        if (_attributes.Vitals.CurrentStamina < 10 || _attributes.Vitals.Fatigue >= 90)
        {
            return $"Thân thể mệt mỏi ({_attributes.Vitals.Fatigue}/100 mệt), không thể dốc sức tu luyện. Hãy dưỡng sức!";
        }

        _wallet.Charge(30, "social.train", $"Trà đàm tu luyện tuyệt kỹ [{skill.NameVi}] cùng {npc.NameVi}");
        _attributes.ApplyFatigue(10);
        _clock.Advance(1);

        var leveledUp = _attributes.TrainSkill(skill.Code, 60);

        var updated = _attributes.GetSkillProgress(skill.Code);
        var title = updated != null ? GetSkillLevelTitle(updated.Level) : "";

        var today = _clock.Today;
        var formattedDate = GameDateCodec.Format(today);

        _db.Transaction(() =>
        {
            _db.Execute(
                "INSERT INTO kz_memory (npc_id, date, topic, sentiment, affinity_delta, summary_vi) " +
                "VALUES (@npcId, @date, 'train', 'positive', 2, @summary)",
                new DbArgs()
                    .Add("@npcId", npcId)
                    .Add("@date", formattedDate)
                    .Add("@summary", $"Luận đạo tu luyện: Cùng {npc.NameVi} nghiên cứu tuyệt kỹ [{skill.NameVi}], lĩnh hội thêm thuần thục."));
        });

        if (leveledUp)
        {
            return $"[Đột Phá Cảnh Giới ⭐] Qua sự chỉ điểm sâu sắc của {npc.NameVi}, tuyệt kỹ [{skill.NameVi}] của bạn đã đột phá lên Cấp {updated?.Level} ({title})! (+60 Thuần Thục, -10 Thể Lực, -30 Quan).";
        }

        return $"[Tu Luyện Tinh Tiến 🥋] Cùng {npc.NameVi} chuyên tâm rèn giũa, tuyệt kỹ [{skill.NameVi}] tích lũy thêm 60 điểm thuần thục ({updated?.Proficiency}/{updated?.MaxProficiency})! (-10 Thể Lực, -30 Quan).";
    }

    private static string GetSkillLevelTitle(int level) => level switch
    {
        1 => "Nhập Môn",
        2 => "Tiểu Thành",
        3 => "Đại Thành",
        4 => "Tinh Thông",
        5 => "Tông Sư",
        _ => "Nhập Môn"
    };

    private (bool Met, string StatName, int Required, int Current) CheckSkillStatRequirement(NpcSkillDef skill)
    {
        var req = skill.Tier switch
        {
            >= 5 => 75,
            4 => 60,
            _ => 40
        };

        var attrs = _attributes.Attributes;
        return skill.Category switch
        {
            "martial" => (attrs.War >= req, "Võ Lực (WAR)", req, attrs.War),
            "strategist" => (attrs.Int >= req, "Trí Tuệ (INT)", req, attrs.Int),
            "commerce" => (attrs.Com >= req, "Thương Mại (COM)", req, attrs.Com),
            "governance" => (Math.Max(attrs.Pol, attrs.Dip) >= req, "Chính Trị/Ngoại Giao (POL/DIP)", req, Math.Max(attrs.Pol, attrs.Dip)),
            _ => (Math.Max(attrs.Wil, attrs.Prc) >= req, "Ý Chí/Cảm Tri (WIL/PRC)", req, Math.Max(attrs.Wil, attrs.Prc))
        };
    }

    private (bool Met, int Required, int Current) CheckSkillFameRequirement(NpcSkillDef skill)
    {
        var req = skill.Tier switch
        {
            >= 5 => 800,
            4 => 300,
            _ => 0
        };
        var current = _attributes.Social.Fame;
        return (current >= req, req, current);
    }

    private static int GetSkillTuitionFee(NpcSkillDef skill) => skill.Tier switch
    {
        >= 5 => 300,
        4 => 100,
        _ => 30
    };

    public void OnMonthClosed(GameDate date)
    {
        ProcessRetainerStipends(date);
        ProcessMonthlyLetters(date);
    }

    private void ProcessRetainerStipends(GameDate date)
    {
        var retainers = GetRetainers();
        if (retainers.Count == 0)
        {
            return;
        }

        var formattedDate = GameDateCodec.Format(date);

        _db.Transaction(() =>
        {
            foreach (var ret in retainers)
            {
                var npc = _registry.Find(ret.NpcId);
                var name = npc?.NameVi ?? $"Gia thần #{ret.NpcId}";

                // Charge monthly stipend with idempotency key
                var key = $"stipend:{ret.Id}:{formattedDate}";
                try
                {
                    _wallet.Charge(ret.MonthlyStipend, "social.stipend", $"Bổng lộc gia thần {name}", key);
                    _db.Execute(
                        "UPDATE kz_retainer SET loyalty = MIN(100, loyalty + 1) WHERE id = @id",
                        DbArgs.One("@id", ret.Id));
                }
                catch (SimException)
                {
                    // If short on cash, loyalty drops!
                    _db.Execute(
                        "UPDATE kz_retainer SET loyalty = MAX(0, loyalty - 15) WHERE id = @id",
                        DbArgs.One("@id", ret.Id));
                    _log.Warn($"Failed to pay stipend for retainer {name} on {formattedDate}; loyalty reduced.");
                }
            }
        });
    }

    public void ProcessMonthlyLetters(GameDate date)
    {
        var hasLetterTable = _db.ScalarLong("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'kz_letter'") > 0;
        if (!hasLetterTable)
        {
            return;
        }

        var formattedDate = GameDateCodec.Format(date);
        var tick = date.DayIndex;

        // Candidate NPCs: affinity >= 50, sworn brothers, or retainers
        var relationships = _db.Query(
            "SELECT npc_id, affinity, impression FROM kz_relationship WHERE affinity >= 50",
            r => (NpcId: r.GetInt64(0), Affinity: r.GetInt32(1), Impression: r.GetString(2)));

        var candidateMap = new Dictionary<long, (int Affinity, string Impression)>();
        foreach (var r in relationships)
        {
            candidateMap[r.NpcId] = (r.Affinity, r.Impression);
        }

        foreach (var sw in GetSwornBrothers())
        {
            if (!candidateMap.ContainsKey(sw.NpcId))
            {
                var rel = GetRelationship(sw.NpcId);
                candidateMap[sw.NpcId] = (Math.Max(rel.Affinity, 80), "Huynh đệ kim lan");
            }
        }

        foreach (var ret in GetRetainers())
        {
            if (!candidateMap.ContainsKey(ret.NpcId))
            {
                var rel = GetRelationship(ret.NpcId);
                candidateMap[ret.NpcId] = (Math.Max(rel.Affinity, 50), "Gia thần tâm phúc");
            }
        }

        if (candidateMap.Count == 0)
        {
            return;
        }

        var swornIds = GetSwornBrothers().Select(s => s.NpcId).ToHashSet();
        var retainerIds = GetRetainers().Select(r => r.NpcId).ToHashSet();

        _db.Transaction(() =>
        {
            foreach (var (npcId, (affinity, impression)) in candidateMap)
            {
                // Max 1 letter per NPC per month
                var alreadySent = _db.ScalarLong(
                    "SELECT COUNT(*) FROM kz_letter WHERE sender_npc_id = @npcId AND sent_date = @date",
                    new DbArgs().Add("@npcId", npcId).Add("@date", formattedDate)) > 0;
                if (alreadySent)
                {
                    continue;
                }

                var isSworn = swornIds.Contains(npcId);
                var isRetainer = retainerIds.Contains(npcId);

                // Chance calculation: sworn brothers stay in close touch, close friends write regularly
                var chance = isSworn ? 70 : (affinity >= 85 ? 50 : (affinity >= 70 ? 35 : 20));
                var rng = new SimpleRng((uint)(tick * 433 ^ (int)npcId));

                if (rng.Next(100) >= chance)
                {
                    continue;
                }

                var npc = _registry.Find(npcId);
                if (npc is null)
                {
                    continue;
                }

                var cit = _map.Find(npc.CitadelId);
                var citName = cit?.NameVi ?? "Phương xa";

                var (title, content, giftItemCode, giftCopper) = GenerateLetterContent(npc, citName, isSworn, isRetainer, affinity, rng);

                long? gItemId = null;
                string? gItemCode = null;
                string? gItemName = null;
                int gItemQty = 0;

                if (!string.IsNullOrEmpty(giftItemCode))
                {
                    var item = _items.FindByCode(giftItemCode);
                    if (item is not null)
                    {
                        gItemId = item.Id;
                        gItemCode = item.Code;
                        gItemName = item.NameVi;
                        gItemQty = 1;
                    }
                }

                _db.Execute(
                    "INSERT INTO kz_letter (sent_date, sender_npc_id, sender_name_vi, sender_citadel_id, title_vi, content_vi, " +
                    "gift_item_id, gift_item_code, gift_item_name_vi, gift_item_quantity, gift_copper, is_read, is_claimed, created_tick) " +
                    "VALUES (@date, @npcId, @name, @citId, @title, @content, @gId, @gCode, @gName, @gQty, @copper, 0, 0, @tick)",
                    new DbArgs()
                        .Add("@date", formattedDate)
                        .Add("@npcId", npc.Id)
                        .Add("@name", npc.NameVi)
                        .Add("@citId", npc.CitadelId)
                        .Add("@title", title)
                        .Add("@content", content)
                        .Add("@gId", gItemId)
                        .Add("@gCode", gItemCode)
                        .Add("@gName", gItemName)
                        .Add("@gQty", gItemQty)
                        .Add("@copper", giftCopper)
                        .Add("@tick", tick));

                // Bonus affinity (+2) for correspondence
                _db.Execute(
                    "UPDATE kz_relationship SET affinity = MIN(100, affinity + 2), last_interaction_date = @date WHERE npc_id = @id",
                    new DbArgs().Add("@date", formattedDate).Add("@id", npc.Id));

                _db.Execute(
                    "INSERT INTO kz_memory (npc_id, date, topic, sentiment, affinity_delta, summary_vi) " +
                    "VALUES (@npcId, @date, 'letter', 'positive', 2, @summary)",
                    new DbArgs()
                        .Add("@npcId", npc.Id)
                        .Add("@date", formattedDate)
                        .Add("@summary", $"{npc.NameVi} gửi phong thư vấn an từ {citName}."));
            }
        });
    }

    private static (string Title, string Content, string? GiftItemCode, int GiftCopper) GenerateLetterContent(
        NpcDef npc,
        string citName,
        bool isSworn,
        bool isRetainer,
        int affinity,
        SimpleRng rng)
    {
        var a = npc.Attributes;

        if (isSworn)
        {
            var roll = rng.Next(2);
            if (roll == 0)
            {
                return (
                    "🌸 Tâm Thư Kim Lan: Vấn An Hiền Đệ / Huynh",
                    $"Bái biệt đã lâu, nơi chiến địa gió lạnh thấu xương, ta vẫn luôn nhớ tới chén rượu ăn thề dưới bóng đào viên thuở trước. " +
                    $"Dạo này lưu lại {citName}, quân mã tề chỉnh, việc lớn từng bước tiến hành. " +
                    $"Ta đặc biệt nhờ trạm dịch gửi tặng chút ngân lượng và rượu thơm hảo hạng, mong hiền đệ bảo trọng thân thể, sớm ngày tái ngộ cùng nâng chén!",
                    "ruou",
                    150);
            }

            return (
                "🤝 Đào Viên Huyết Thệ: Phong Vân Thiên Hạ",
                $"Thời cuộc thiên hạ biến ảo khôn lường, chư hầu khắp nơi rục rịch binh mã. " +
                $"Ta tọa trấn tại {citName}, trong lòng vẫn luôn khắc cốt ghi tâm chí lớn của huynh đệ chúng ta. " +
                $"Nay gửi chút quân tư tiếp tế, chúc hiền huynh vạn sự hanh thông, danh vang bốn bể!",
                null,
                200);
        }

        if (isRetainer)
        {
            var roll = rng.Next(2);
            if (roll == 0)
            {
                return (
                    "🛡️ Bẩm Báo Gia Thần: Bổn Phận & Vấn An Minh Chủ",
                    $"Thuộc hạ phụng mệnh minh chủ, ngày đêm không dám lơ là chức trách tại {citName}. " +
                    $"Gần đây võ nghệ tinh tiến, việc tuần phòng và thu chi đều thuận lợi, xin kính báo để minh chủ an lòng. " +
                    $"Thuộc hạ gom góp được chút ngân lượng dâng lên minh chủ làm lộ phí hành tẩu!",
                    null,
                    80);
            }

            return (
                "📜 Thư Bẩm Báo: Tình Hình Trị An & Dược Liệu Tiếp Tế",
                $"Thuộc hạ tại {citName} quan sát thấy khách buôn qua lại tấp nập, thương lộ bình yên. " +
                $"Thuộc hạ may mắn thu thập được vị thuốc bổ dưỡng khí huyết, xin sai người hỏa tốc chuyển gửi dâng lên minh chủ bồi bổ thân thể!",
                "thuoc",
                0);
        }

        // Archetype based on attributes
        if (a.War >= 75)
        {
            var roll = rng.Next(2);
            if (roll == 0)
            {
                return (
                    "⚔️ Luận Kiếm Chi Thư: Hào Khí Ngút Trời",
                    $"Từ dạo cùng các hạ đàm đạo võ học, ta vẫn luôn khắc ghi nhã ý. " +
                    $"Gần đây tại {citName} ta thường mài kiếm dưới trăng, ngẫm lại lời bàn năm xưa của các hạ thật thấu đáo. " +
                    $"Nay gửi tặng vò rượu nếp ủ kỹ, mong ngày tương phùng cùng cạn chén luận anh hùng!",
                    "ruou",
                    100);
            }

            return (
                "🏹 Phong Thư Quân Trại: Tiễu Phỉ An Dân",
                $"Ta vừa dẫn binh tuần phòng tiễu trừ thảo khấu quanh vùng {citName}, thu được một số dược liệu và quân nhu phòng thân. " +
                $"Nhớ tới bằng hữu bôn ba giang hồ gió bụi, xin gửi tặng để hộ thân khi hữu sự.",
                "thuoc",
                0);
        }

        if (a.Com >= 70)
        {
            var roll = rng.Next(2);
            if (roll == 0)
            {
                return (
                    "💰 Phi Thư Thương Gia: Hồi Báo Lợi Nhuận Buôn Bán",
                    $"Thương đoàn của ta vừa hoàn tất chuyến buôn đường dài về {citName}, buôn may bán đắt, thu lời không nhỏ. " +
                    $"Nhớ đến ân tình và sự tương trợ của các hạ thuở trước, ta trích phần hoa hồng này xin gửi biếu để tỏ chút lòng thành!",
                    null,
                    180);
            }

            return (
                "📦 Tơ Lụa & Danh Trà: Quà Tặng Tri Kỷ Thương Mại",
                $"Tại chợ phiên {citName} vừa nhập về một lô tơ lụa và trà mạn cực phẩm. " +
                $"Ta lập tức chọn lấy phẩm vị tinh xảo nhất gửi ngựa trạm chuyển giao đến tay các hạ, kính chúc buôn may bán đắt, tài lộc dồi dào!",
                "tra",
                50);
        }

        if (a.Int >= 75)
        {
            var roll = rng.Next(2);
            if (roll == 0)
            {
                return (
                    "📜 Thư Đàm Đạo Thời Cuộc: Luận Bàn Đại Thế Chư Hầu",
                    $"Phương bắc phong vân khởi sắc, thiên hạ biến loạn khó lường. " +
                    $"Ta tọa quan thiên văn và thời cuộc tại {citName}, thường nhớ tới những lời đàm luận sáng suốt của các hạ. " +
                    $"Nay gửi phong thư này kèm chút danh trà để các hạ thưởng thức lúc rảnh rỗi nghiền ngẫm việc lớn.",
                    "tra",
                    50);
            }

            return (
                "🌿 Trầm Tư Tu Dưỡng: Dược Liệu Dưỡng Sinh",
                $"Hành tẩu thiên hạ vất vả nhọc nhằn, cần phải bảo trọng khí huyết và tinh thần. " +
                $"Ta tình cờ có được vị danh dược quý ngàn năm, đặc biệt sai người mang tới chúc các hạ thân thể tráng kiện, tâm trí minh mẫn.",
                "sam",
                0);
        }

        return (
            "✉️ Thư Vấn An Bằng Hữu Phương Xa",
            $"Biệt ly mấy độ trăng tròn, ngụ tại {citName} vẫn luôn hướng về cố nhân. " +
            $"Gửi chút lộ phí phong bao, chúc các hạ hành trình bình an, sớm ngày hội ngộ đàm đạo bên bếp lửa hồng.",
            null,
            80);
    }

    public IReadOnlyList<LetterDef> GetLetters(bool unreadOnly = false, int limit = 30)
    {
        var hasTable = _db.ScalarLong("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'kz_letter'") > 0;
        if (!hasTable)
        {
            return Array.Empty<LetterDef>();
        }

        var sql = "SELECT id, sent_date, sender_npc_id, sender_name_vi, sender_citadel_id, title_vi, content_vi, " +
                  "gift_item_id, gift_item_code, gift_item_name_vi, gift_item_quantity, gift_copper, is_read, is_claimed " +
                  "FROM kz_letter ";

        if (unreadOnly)
        {
            sql += "WHERE is_read = 0 ";
        }

        sql += "ORDER BY id DESC LIMIT " + Math.Clamp(limit, 1, 100);

        return _db.Query(sql, r => new LetterDef(
            r.GetInt64(0),
            GameDateCodec.Parse(r.GetString(1)),
            r.GetInt64(2),
            r.GetString(3),
            r.GetInt64(4),
            r.GetString(5),
            r.GetString(6),
            r.IsDbNull(7) ? (long?)null : r.GetInt64(7),
            r.GetStringOrNull(8),
            r.GetStringOrNull(9),
            r.GetInt32(10),
            r.GetInt32(11),
            r.GetInt32(12) != 0,
            r.GetInt32(13) != 0));
    }

    public LetterDef? GetLetter(long letterId)
    {
        var hasTable = _db.ScalarLong("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'kz_letter'") > 0;
        if (!hasTable)
        {
            return null;
        }

        return _db.QueryOne(
            "SELECT id, sent_date, sender_npc_id, sender_name_vi, sender_citadel_id, title_vi, content_vi, " +
            "gift_item_id, gift_item_code, gift_item_name_vi, gift_item_quantity, gift_copper, is_read, is_claimed " +
            "FROM kz_letter WHERE id = @id",
            r => new LetterDef(
                r.GetInt64(0),
                GameDateCodec.Parse(r.GetString(1)),
                r.GetInt64(2),
                r.GetString(3),
                r.GetInt64(4),
                r.GetString(5),
                r.GetString(6),
                r.IsDbNull(7) ? (long?)null : r.GetInt64(7),
                r.GetStringOrNull(8),
                r.GetStringOrNull(9),
                r.GetInt32(10),
                r.GetInt32(11),
                r.GetInt32(12) != 0,
                r.GetInt32(13) != 0),
            DbArgs.One("@id", letterId));
    }

    public string ReadLetter(long letterId)
    {
        var letter = GetLetter(letterId);
        if (letter is null)
        {
            return "Không tìm thấy phong thư này.";
        }

        _db.Execute("UPDATE kz_letter SET is_read = 1 WHERE id = @id", DbArgs.One("@id", letterId));

        var giftText = letter.GiftCopper > 0 && letter.GiftItemQuantity > 0
            ? $"{letter.GiftCopper} Quan tiền & {letter.GiftItemQuantity} {letter.GiftItemNameVi}"
            : letter.GiftCopper > 0
                ? $"{letter.GiftCopper} Quan tiền"
                : letter.GiftItemQuantity > 0
                    ? $"{letter.GiftItemQuantity} {letter.GiftItemNameVi}"
                    : "Không có lễ vật đính kèm";

        var claimStatus = (letter.GiftCopper > 0 || letter.GiftItemQuantity > 0)
            ? (letter.IsClaimed ? "[Đã nhận quà]" : "[Chưa nhận quà - Nhấn nút 'Nhận Quà 🎁']")
            : "";

        var cit = _map.Find(letter.SenderCitadelId);
        var citName = cit?.NameVi ?? "Phương xa";

        return $"══════════════════════════════════════════════════\n" +
               $"📜 {letter.TitleVi}\n" +
               $"Người gửi: {letter.SenderNameVi} (Đang ở {citName})\n" +
               $"Ngày gửi: {GameDateCodec.Format(letter.SentDate)}\n" +
               $"──────────────────────────────────────────────────\n" +
               $"{letter.ContentVi}\n" +
               $"──────────────────────────────────────────────────\n" +
               $"🎁 Lễ vật: {giftText} {claimStatus}\n" +
               $"══════════════════════════════════════════════════";
    }

    public string ClaimLetterGift(long letterId)
    {
        var letter = GetLetter(letterId);
        if (letter is null)
        {
            return "Không tìm thấy phong thư này.";
        }

        if (letter.IsClaimed)
        {
            return "Lễ vật đính kèm trong thư này đã được nhận trước đó rồi.";
        }

        if (letter.GiftCopper <= 0 && letter.GiftItemQuantity <= 0)
        {
            return "Bức thư này không có lễ vật đính kèm.";
        }

        var details = new List<string>();

        if (letter.GiftCopper > 0)
        {
            _wallet.Pay(letter.GiftCopper, "social.letter_gift", $"Lễ vật đính kèm từ thư của {letter.SenderNameVi}");
            details.Add($"+{letter.GiftCopper} Quan tiền");
        }

        if (letter.GiftItemId.HasValue && letter.GiftItemQuantity > 0)
        {
            _bag.Add(_player.CurrentCityId, letter.GiftItemId.Value, letter.GiftItemQuantity);
            details.Add($"+{letter.GiftItemQuantity} {letter.GiftItemNameVi}");
        }

        _db.Execute("UPDATE kz_letter SET is_claimed = 1, is_read = 1 WHERE id = @id", DbArgs.One("@id", letterId));

        var currentCit = _map.Find(_player.CurrentCityId);
        return $"🎁 Đã nhận lễ vật từ {letter.SenderNameVi}: {string.Join(", ", details)} (đã chuyển vào hành trang tại {currentCit?.NameVi ?? "thành này"}).";
    }

    public int UnreadLetterCount()
    {
        var hasTable = _db.ScalarLong("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'kz_letter'") > 0;
        return hasTable ? (int)_db.ScalarLong("SELECT COUNT(*) FROM kz_letter WHERE is_read = 0") : 0;
    }

    private static string CalculateImpression(int affinity) => affinity switch
    {
        >= 90 => "Sinh tử chi giao",
        >= 70 => "Thâm giao tri kỷ",
        >= 40 => "Hảo hữu thân thiết",
        >= 15 => "Quen biết tương đắc",
        >= -10 => "Sơ giao bằng hữu",
        >= -40 => "Lạnh nhạt xa cách",
        _ => "Tử thù đối địch"
    };

    public IReadOnlyList<SwornBrotherDef> GetSwornBrothers() =>
        _db.Query(
            "SELECT npc_id, oath_date, bond_title, assist_count FROM kz_sworn_brother ORDER BY rowid",
            row => new SwornBrotherDef(
                row.GetInt64(0),
                GameDateCodec.Parse(row.GetString(1)),
                row.GetString(2),
                row.GetInt32(3)));

    public string SwearBrotherhood(long npcId)
    {
        var npc = _registry.Find(npcId)
                  ?? throw new SimException("social.npc_not_found", $"Không tìm thấy nhân sĩ mã {npcId}.");

        if (npc.CitadelId != _player.CurrentCityId)
        {
            return $"{npc.NameVi} hiện đang ở thành khác, không thể cùng cắt máu ăn thề tại đây.";
        }

        var existingSworn = GetSwornBrothers();
        if (existingSworn.Any(s => s.NpcId == npcId))
        {
            return $"{npc.NameVi} đã là nghĩa huynh đệ kim lan của bạn rồi!";
        }

        if (existingSworn.Count >= 2)
        {
            return "Bạn đã có đủ 2 nghĩa huynh đệ kim lan (theo điển cố Đào Viên Tam Kết Nghĩa), không thể kết bái thêm.";
        }

        var rel = GetRelationship(npcId);
        if (rel.Affinity < 80 || rel.Trust < 60)
        {
            return $"{npc.NameVi} chưa đủ tin cậy (cần Hảo Cảm ≥ 80 và Tín Nhiệm ≥ 60, hiện tại {rel.Affinity}/100 hảo cảm, {rel.Trust}/100 tín nhiệm) để cùng thề ước sinh tử.";
        }

        const int oathCost = 100;
        if (_wallet.Liquid < oathCost)
        {
            return $"Cần {oathCost} Quan sắm sửa lễ vật trâu rượu tế cáo trời đất, hiện chỉ có {_wallet.Liquid} Quan.";
        }

        var today = _clock.Today;
        var formattedDate = GameDateCodec.Format(today);
        var bondTitle = existingSworn.Count == 0 ? "Nghĩa Huynh" : "Nghĩa Đệ";

        _db.Transaction(() =>
        {
            _wallet.Charge(oathCost, JournalCodes.CaravanFreight, $"Lễ vật tế cáo trời đất kết nghĩa kim lan cùng {npc.NameVi}");
            _clock.Advance(1);

            _db.Execute(
                """
                INSERT INTO kz_sworn_brother (npc_id, oath_date, bond_title, assist_count)
                VALUES (@npcId, @oathDate, @bondTitle, 0)
                """,
                new DbArgs()
                    .Add("@npcId", npcId)
                    .Add("@oathDate", formattedDate)
                    .Add("@bondTitle", bondTitle));

            // Lock affinity & trust to 100, impression to Sworn Brother
            _db.Execute(
                """
                INSERT INTO kz_relationship (npc_id, affinity, trust, impression, gift_count, last_interaction_date)
                VALUES (@npcId, 100, 100, 'Kết Nghĩa Huynh Đệ', 0, @date)
                ON CONFLICT(npc_id) DO UPDATE SET
                    affinity = 100,
                    trust = 100,
                    impression = 'Kết Nghĩa Huynh Đệ',
                    last_interaction_date = excluded.last_interaction_date
                """,
                new DbArgs().Add("@npcId", npcId).Add("@date", formattedDate));

            _db.Execute(
                """
                INSERT INTO kz_memory (npc_id, date, topic, sentiment, affinity_delta, summary_vi)
                VALUES (@npcId, @date, 'oath', 'positive', 20, @summary)
                """,
                new DbArgs()
                    .Add("@npcId", npcId)
                    .Add("@date", formattedDate)
                    .Add("@summary", $"Cùng {npc.NameVi} cắt máu ăn thề, kết nghĩa huynh đệ kim lan tại Đào Viên, nguyện đồng tâm hiệp lực, hoạn nạn có nhau, sinh tử không rời!"));
        });

        _registry.UpdateCitadel(npcId, _player.CurrentCityId);

        return $"Trời đất chứng giám! Bạn cùng {npc.NameVi} đã chính thức kết nghĩa huynh đệ kim lan ({bondTitle})! Tình nghĩa keo sơn, hoạn nạn có nhau.";
    }

    public string HostBanquet()
    {
        const int banquetCost = 150;
        if (_wallet.Liquid < banquetCost)
        {
            return $"Cần {banquetCost} Quan để mở yến tiệc chiêu đãi quần hiền, hiện chỉ có {_wallet.Liquid} Quan.";
        }

        var cityId = _player.CurrentCityId;
        var localNpcs = _registry.FindByCitadel(cityId)
            .Take(6)
            .ToList();

        if (localNpcs.Count == 0)
        {
            return "Không có danh sĩ nào tại thành này để cùng nâng chén thưởng nguyệt.";
        }

        var today = _clock.Today;
        var formattedDate = GameDateCodec.Format(today);

        _db.Transaction(() =>
        {
            _wallet.Charge(banquetCost, JournalCodes.CaravanFreight, "Mở đại yến tiệc kết giao hào kiệt");
            _clock.Advance(1);

            foreach (var npc in localNpcs)
            {
                var rel = GetRelationship(npc.Id);
                var newAffinity = Math.Min(100, rel.Affinity + 12);
                var newTrust = Math.Min(100, rel.Trust + 5);
                var impression = CalculateImpression(newAffinity);

                _db.Execute(
                    """
                    INSERT INTO kz_relationship (npc_id, affinity, trust, impression, gift_count, last_interaction_date)
                    VALUES (@npcId, @aff, @trust, @imp, 0, @date)
                    ON CONFLICT(npc_id) DO UPDATE SET
                        affinity = excluded.affinity,
                        trust = excluded.trust,
                        impression = excluded.impression,
                        last_interaction_date = excluded.last_interaction_date
                    """,
                    new DbArgs()
                        .Add("@npcId", npc.Id)
                        .Add("@aff", newAffinity)
                        .Add("@trust", newTrust)
                        .Add("@imp", impression)
                        .Add("@date", formattedDate));

                _db.Execute(
                    """
                    INSERT INTO kz_memory (npc_id, date, topic, sentiment, affinity_delta, summary_vi)
                    VALUES (@npcId, @date, 'banquet', 'positive', 12, @summary)
                    """,
                    new DbArgs()
                        .Add("@npcId", npc.Id)
                        .Add("@date", formattedDate)
                        .Add("@summary", $"Dự đại yến do bạn chủ trì, cùng nâng chén thưởng nguyệt, đàm luận thế sự thiên hạ."));
            }

            var summary = $"Đại yến quy tụ {localNpcs.Count} danh sĩ ({string.Join(", ", localNpcs.Select(n => n.NameVi))}). Hảo cảm đồng loạt gia tăng!";
            _db.Execute(
                """
                INSERT INTO kz_banquet (citadel_id, date, cost_copper, attendee_count, summary_vi)
                VALUES (@city, @date, @cost, @count, @summary)
                """,
                new DbArgs()
                    .Add("@city", cityId)
                    .Add("@date", formattedDate)
                    .Add("@cost", banquetCost)
                    .Add("@count", localNpcs.Count)
                    .Add("@summary", summary));
        });

        return $"Đại yến thành công rực rỡ! {localNpcs.Count} danh sĩ ({string.Join(", ", localNpcs.Select(n => n.NameVi))}) tề tựu nâng chén, hảo cảm đồng loạt tăng +12 và tín nhiệm +5!";
    }

    private static string AutoAssignRole(CharacterAttributes a)
    {
        if (a.War >= 75) return "bodyguard";
        if (a.Com >= 70) return "manager";
        if (a.Int >= 75) return "advisor";
        return "accountant";
    }

    public static string RoleTitleVi(string role) => role switch
    {
        "bodyguard" => "Hộ Vệ Tướng Quân (Bảo vệ an nguy)",
        "manager" => "Chưởng Quầy Sạp Hàng (Kinh doanh thương mại)",
        "advisor" => "Mưu Thần Quân Sư (Hiến kế mưu lược)",
        "accountant" => "Kế Toán Sổ Sách (Quản lý kho quỹ)",
        _ => "Gia Thần"
    };

    private sealed class SimpleRng
    {
        private uint _state;

        public SimpleRng(uint seed)
        {
            _state = seed == 0 ? 0x12345678 : seed;
        }

        public int Next(int max)
        {
            if (max <= 1) return 0;
            // Xorshift32
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            return (int)(_state % (uint)max);
        }
    }
}
