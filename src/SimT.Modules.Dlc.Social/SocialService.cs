using SimT.Contracts;
using SimT.Contracts.Data;
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

    public string ChatWithNpc(long npcId)
    {
        var npc = _registry.Find(npcId)
                  ?? throw new SimException("social.npc_not_found", $"Không tìm thấy nhân sĩ mã {npcId}.");

        if (_attributes.Vitals.CurrentStamina < 5 || _attributes.Vitals.Fatigue >= 95)
        {
            return $"Thân thể mệt mỏi, tinh thần uể oải ({_attributes.Vitals.Fatigue}/100 mệt). Hãy nghỉ ngơi hoặc chuyển ngày để hồi phục thể lực trước khi đàm đạo.";
        }

        var today = _clock.Today;
        var formattedDate = GameDateCodec.Format(today);

        // Player applies fatigue for mental engagement
        _attributes.ApplyFatigue(5);

        // Calculate affinity gain based on diplomatic acumen and compatibility
        var pAttr = _attributes.Attributes;
        var nAttr = npc.Attributes;

        var gain = 4;
        if (pAttr.Dip >= 70) gain += 2;
        if (pAttr.Dip >= 85) gain += 3;

        // Synergy bonuses
        if (nAttr.War >= 80 && pAttr.War >= 75) gain += 2; // Martial camaraderie
        if (nAttr.Com >= 75 && pAttr.Com >= 70) gain += 2; // Mercantile synergy
        if (nAttr.Int >= 80 && pAttr.Int >= 75) gain += 2; // Scholarly resonance

        var currentRel = GetRelationship(npcId);
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

            _db.Execute(
                "INSERT INTO kz_memory (npc_id, date, topic, sentiment, affinity_delta, summary_vi) " +
                "VALUES (@npcId, @date, 'chat', 'positive', @delta, @summary)",
                new DbArgs()
                    .Add("@npcId", npcId)
                    .Add("@date", formattedDate)
                    .Add("@delta", gain)
                    .Add("@summary", $"Đàm đạo nhân sinh cùng {npc.NameVi} tại trà lâu, chuyện trò tương đắc (+{gain} Hảo cảm)."));
        });

        // Award relevant attribute EXP to player
        if (nAttr.War >= 80) _attributes.AddExp("WAR", 15);
        else if (nAttr.Int >= 80) _attributes.AddExp("INT", 15);
        else if (nAttr.Com >= 75) _attributes.AddExp("COM", 15);
        else _attributes.AddExp("DIP", 15);

        return $"Đàm đạo cùng {npc.NameVi}: +{gain} Hảo cảm. Quan hệ hiện tại: {newAffinity}/100 ({newImpression}).";
    }

    public string GiftNpc(long npcId, long itemId)
    {
        var npc = _registry.Find(npcId)
                  ?? throw new SimException("social.npc_not_found", $"Không tìm thấy nhân sĩ mã {npcId}.");

        var item = _items.Find(itemId)
                   ?? throw new SimException("social.item_not_found", $"Vật phẩm mã {itemId} không tồn tại.");

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

        // Gift value bonus
        var gain = Math.Clamp((int)(item.BasePrice / 20), 6, 35);

        // Favorite item synergy: luxury / tea / silk / weapon
        if (item.Category == "luxury" || item.Code.Contains("tra") || item.Code.Contains("lua") || item.Code.Contains("sam"))
        {
            gain = (int)(gain * 1.4);
        }

        var currentRel = GetRelationship(npcId);
        var newAffinity = Math.Clamp(currentRel.Affinity + gain, -100, 100);
        var newTrust = Math.Clamp(currentRel.Trust + 4, 0, 100);
        var newGifts = currentRel.GiftCount + 1;
        var newImpression = CalculateImpression(newAffinity);

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
                    .Add("@summary", $"Tặng phẩm {item.NameVi} cho {npc.NameVi}, nhận được cảm kích sâu sắc (+{gain} Hảo cảm)."));
        });

        _attributes.AddExp("DIP", 20);

        return $"Đã biếu tặng {item.NameVi} cho {npc.NameVi}: +{gain} Hảo cảm. Mối quan hệ: {newAffinity}/100 ({newImpression}).";
    }

    public string RecruitRetainer(long npcId, string role, int stipend)
    {
        var npc = _registry.Find(npcId)
                  ?? throw new SimException("social.npc_not_found", $"Không tìm thấy nhân sĩ mã {npcId}.");

        var rel = GetRelationship(npcId);
        if (rel.Affinity < 35)
        {
            throw new SimException("social.affinity_insufficient",
                $"{npc.NameVi} chưa đủ tin tưởng bạn (Hảo cảm: {rel.Affinity}/100, cần tối thiểu 35). Hãy trò chuyện và tặng quà thêm.");
        }

        var existing = GetRetainer(npcId);
        if (existing is not null)
        {
            throw new SimException("social.already_retained", $"{npc.NameVi} đã là gia thần của bạn.");
        }

        var validRoles = new[] { "bodyguard", "accountant", "advisor", "manager" };
        var assignedRole = validRoles.Contains(role) ? role : AutoAssignRole(npc.Attributes);
        var finalStipend = Math.Max(40, stipend);

        const int recruitmentFee = 250;
        if (_wallet.Liquid < recruitmentFee)
        {
            throw new SimException("social.cash_shortage",
                $"Không đủ ngân lượng chiêu hiền đãi sĩ (cần {recruitmentFee} Quan sính lễ kết giao, hiện có {_wallet.Liquid} Quan).");
        }

        var today = _clock.Today;
        var formattedDate = GameDateCodec.Format(today);

        _db.Transaction(() =>
        {
            _wallet.Charge(recruitmentFee, "social.recruit", $"Sính lễ chiêu mộ gia thần: {npc.NameVi}", $"recruit:{npcId}:{formattedDate}");

            _db.Execute(
                "INSERT INTO kz_retainer (npc_id, role_assigned, monthly_stipend, loyalty, recruited_date) " +
                "VALUES (@npcId, @role, @stipend, 85, @date)",
                new DbArgs()
                    .Add("@npcId", npcId)
                    .Add("@role", assignedRole)
                    .Add("@stipend", finalStipend)
                    .Add("@date", formattedDate));

            _db.Execute(
                "INSERT INTO kz_memory (npc_id, date, topic, sentiment, affinity_delta, summary_vi) " +
                "VALUES (@npcId, @date, 'recruit', 'positive', 15, @summary)",
                new DbArgs()
                    .Add("@npcId", npcId)
                    .Add("@date", formattedDate)
                    .Add("@summary", $"{npc.NameVi} cảm kích tri ngộ, chính thức bái nhập môn hạ làm {RoleTitleVi(assignedRole)}."));
        });

        _log.Info($"Recruited NPC {npc.NameVi} as {assignedRole} with stipend {finalStipend}.");
        return $"Đã chiêu mộ {npc.NameVi} làm {RoleTitleVi(assignedRole)}! (Bổng lộc: {finalStipend} Quan/tháng, Lòng trung thành: 85/100).";
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

    public void OnMonthClosed(GameDate date)
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
}
