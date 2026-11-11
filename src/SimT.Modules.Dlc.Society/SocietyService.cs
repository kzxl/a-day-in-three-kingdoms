using SimT.Contracts.Data;
using SimT.Contracts.Identity;
using SimT.Contracts.Logging;
using SimT.Contracts.Modules;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Society;

internal sealed partial class SocietyService : ISocietyChronicle, IMerchantRivalry, IKarmaEngine
{
    public IMerchantRivalry Rivals => this;
    public IKarmaEngine Karma => this;
    private readonly IDbSession _db;
    private readonly IGameClock _clock;
    private readonly INpcRegistry _registry;
    private readonly IWorldMap _map;
    private readonly IPlayerState _player;
    private readonly ILog _log;
    private readonly ICapabilityBroker? _broker;
    private readonly IRng? _kernelRng;
    private readonly long _campaignSeed;

    public SocietyService(
        IDbSession db,
        IGameClock clock,
        INpcRegistry registry,
        IWorldMap map,
        IPlayerState player,
        ILog log,
        ICapabilityBroker? broker = null,
        IRng? kernelRng = null,
        long campaignSeed = 0)
    {
        _db = db;
        _clock = clock;
        _registry = registry;
        _map = map;
        _player = player;
        _log = log;
        _broker = broker;
        _kernelRng = kernelRng;
        _campaignSeed = campaignSeed;

        EnsureInitialized();
    }

    /// <summary>
    /// Derives an independent, deterministic sub-stream IRng for a specific domain entity and date.
    /// Eliminates cross-NPC execution order bias while strictly binding to the Kernel campaign seed.
    /// </summary>
    private IRng CreateSubStreamRng(long entityId, long dayIndex, ulong domainSalt)
    {
        var baseSeed = _campaignSeed != 0 ? (ulong)_campaignSeed : (_kernelRng?.State ?? 0x123456789ABCDEFUL);
        var z = baseSeed ^ ((ulong)entityId * 0x9E3779B97F4A7C15UL) ^ ((ulong)dayIndex * 0xBF58476D1CE4E5B9UL) ^ domainSalt;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        var streamSeed = z ^ (z >> 31);
        return new SocietySubStreamRng(streamSeed);
    }

    public void EnsureInitialized()
    {
        EnsureActivityInitialized();
        EnsureMerchantRivalsInitialized();
        EnsureKarmaInitialized();
        EnsureHistoricalEventsInitialized();
        RefreshNpcLifetimes();
        EnsureDecisionModelInitialized();
    }

    private void EnsureActivityInitialized()
    {
        var count = _db.ScalarLong("SELECT COUNT(*) FROM so_npc_activity");
        if (count > 0)
        {
            return;
        }

        var today = _clock.Today;
        var formattedDate = GameDateCodec.Format(today);

        _db.Transaction(() =>
        {
            foreach (var npc in _registry.All)
            {
                var a = npc.Attributes;
                var focus = DetermineFocusPath(a);
                var wealth = ComputeInitialWealth(npc, focus);
                var initialRep = ComputeInitialReputation(npc);
                var initialAction = InitialAction(focus);

                _db.Execute(
                    "INSERT INTO so_npc_activity (npc_id, current_citadel_id, current_wealth, focus_path, current_action, stamina, reputation, updated_date) " +
                    "VALUES (@npcId, @citadelId, @wealth, @focus, @action, 100, @rep, @date)",
                    new DbArgs()
                        .Add("@npcId", npc.Id)
                        .Add("@citadelId", npc.CitadelId)
                        .Add("@wealth", wealth)
                        .Add("@focus", focus)
                        .Add("@action", initialAction)
                        .Add("@rep", initialRep)
                        .Add("@date", formattedDate));

                _db.Execute(
                    "INSERT INTO so_npc_stat_delta (npc_id, war_delta, ldr_delta, int_delta, pol_delta, dip_delta, com_delta, log_delta, cra_delta, updated_date) " +
                    "VALUES (@npcId, 0, 0, 0, 0, 0, 0, 0, 0, @date)",
                    new DbArgs()
                        .Add("@npcId", npc.Id)
                        .Add("@date", formattedDate));
            }

            // Create inaugural entries for prominent heroes
            var heroes = _registry.All.Where(n => n.Attributes.War >= 92 || n.Attributes.Int >= 92 || n.Attributes.Com >= 85).Take(12);
            foreach (var h in heroes)
            {
                var cit = _map.Find(h.CitadelId);
                var citName = cit?.NameVi ?? "Thiên hạ";
                _db.Execute(
                    "INSERT INTO so_journey_log (date, npc_id, npc_name_vi, citadel_id, citadel_name_vi, event_kind, headline_vi, detail_vi, stat_impact, created_tick) " +
                    "VALUES (@date, @npcId, @name, @citId, @citName, 'scholarly', @headline, @detail, 'Khởi đầu', @tick)",
                    new DbArgs()
                        .Add("@date", formattedDate)
                        .Add("@npcId", h.Id)
                        .Add("@name", h.NameVi)
                        .Add("@citId", h.CitadelId)
                        .Add("@citName", citName)
                        .Add("@headline", $"{h.NameVi} tọa trấn tại {citName}")
                        .Add("@detail", $"{h.NameVi} ({h.Role}) ngụ tại {citName}, danh vang tứ hải, anh hùng hào kiệt đều muốn kết giao.")
                        .Add("@tick", today.DayIndex));
            }
        });

        _log.Info($"Society engine initialized with {_registry.All.Count} autonomous NPCs.");
    }

    public void OnMonthClosed(GameDate date)
    {
        SimulateTurn(date);
    }

    public void SimulateTurn(GameDate date)
    {
        var formattedDate = GameDateCodec.Format(date);
        var tick = date.DayIndex;

        var activities = AllActivities();
        var npcsById = _registry.All.ToDictionary(n => n.Id);

        Dictionary<long, CityGovernanceView>? govByCitadel = null;
        if (_broker is not null && _broker.TryGet<IPoliticalCourt>(CapabilityIds.CourtOffice, out var court) && court is not null)
        {
            govByCitadel = court.Governances.ToDictionary(g => g.CitadelId);
        }

        IMilitaryCommand? military = null;
        if (_broker is not null && _broker.TryGet<IMilitaryCommand>(CapabilityIds.MilitaryCommand, out var mil))
        {
            military = mil;
        }

        HashSet<long>? swornBrotherIds = null;
        Dictionary<long, RetainerDef>? retainersByNpc = null;
        ISocialNetwork? socialNet = null;
        if (_broker is not null && _broker.TryGet<ISocialNetwork>(CapabilityIds.SocialNetwork, out var social) && social is not null)
        {
            socialNet = social;
            swornBrotherIds = social.GetSwornBrothers().Select(s => s.NpcId).ToHashSet();
            retainersByNpc = social.GetRetainers().ToDictionary(r => r.NpcId);
        }

        var needsByNpc = LoadAllNpcNeedsMap();
        var goalsByNpc = LoadAllNpcGoalsMap();

        _db.Transaction(() =>
        {
            foreach (var act in activities)
            {
                if (_newlyArrivedNpcIds.Remove(act.NpcId) || act.IsDeceased || !npcsById.TryGetValue(act.NpcId, out var npc) || npc.IsDeceasedFlag)
                {
                    continue;
                }

                // Check if NPC is bound to player retinue (sworn brother or personal companion retainer)
                var isSworn = swornBrotherIds != null && swornBrotherIds.Contains(npc.Id);
                var isRetainerCompanion = retainersByNpc != null && retainersByNpc.TryGetValue(npc.Id, out var ret) && ret.RoleAssigned is "bodyguard" or "advisor";
                var isCompanion = isSworn || isRetainerCompanion;

                if (isCompanion)
                {
                    // Companions accompany protagonist to their current location
                    var playerCityId = _player.CurrentCityId;
                    if (act.CurrentCitadelId != playerCityId)
                    {
                        var playerCit = _map.Find(playerCityId);
                        var targetName = playerCit?.NameVi ?? "Bên cạnh minh chủ";
                        var actionText = isSworn ? $"Đang hộ vệ nghĩa huynh đệ tại {targetName}" : $"Túc trực bên cạnh minh chủ tại {targetName}";

                        _db.Execute(
                            "UPDATE so_npc_activity SET current_citadel_id = @newCit, current_action = @action, updated_date = @date WHERE npc_id = @id",
                            new DbArgs()
                                .Add("@newCit", playerCityId)
                                .Add("@action", actionText)
                                .Add("@date", formattedDate)
                                .Add("@id", npc.Id));

                        _registry.UpdateCitadel(npc.Id, playerCityId);
                    }
                    continue;
                }

                // Deterministic pseudo-random seed per turn per NPC
                var rng = CreateSubStreamRng(npc.Id, date.DayIndex, 0x50C1E71UL);

                ExecuteNpcDecisionTurn(npc, act, govByCitadel, military, socialNet, needsByNpc, goalsByNpc, formattedDate, date, tick, rng);
            }

            // Periodic replenishment: quarterly (month % 3 == 0) or whenever a citadel drops below 18 NPCs
            if (date.Month % 3 == 0 || AnyCitadelUnderpopulated())
            {
                ReplenishNpcsCore(date, formattedDate, tick, count: 1);
            }

            // Simulate AI Merchant Rivals progression and market competition
            SimulateMerchantTurnCore(date, formattedDate, tick);
        });

        _log.Info($"Society monthly simulation executed for date {formattedDate}.");
    }


    private void ApplyNpcGrowth(long npcId, string date, int war, int ldr, int @int, int pol, int dip, int com, int log, int cra)
    {
        if (war == 0 && ldr == 0 && @int == 0 && pol == 0 && dip == 0 && com == 0 && log == 0 && cra == 0)
        {
            return;
        }

        _db.Execute(
            "UPDATE so_npc_stat_delta SET " +
            "war_delta = war_delta + @war, " +
            "ldr_delta = ldr_delta + @ldr, " +
            "int_delta = int_delta + @int, " +
            "pol_delta = pol_delta + @pol, " +
            "dip_delta = dip_delta + @dip, " +
            "com_delta = com_delta + @com, " +
            "log_delta = log_delta + @log, " +
            "cra_delta = cra_delta + @cra, " +
            "updated_date = @date WHERE npc_id = @id",
            new DbArgs()
                .Add("@war", war)
                .Add("@ldr", ldr)
                .Add("@int", @int)
                .Add("@pol", pol)
                .Add("@dip", dip)
                .Add("@com", com)
                .Add("@log", log)
                .Add("@cra", cra)
                .Add("@date", date)
                .Add("@id", npcId));
    }

    private void UpdateActivity(long npcId, string date, string action, int goldDelta, int repDelta, long currentWealth)
    {
        var newWealth = Math.Max(0, currentWealth + goldDelta);

        _db.Execute(
            "UPDATE so_npc_activity SET " +
            "current_wealth = @wealth, " +
            "current_action = @action, " +
            "reputation = reputation + @rep, " +
            "updated_date = @date WHERE npc_id = @id",
            new DbArgs()
                .Add("@wealth", newWealth)
                .Add("@action", action)
                .Add("@rep", repDelta)
                .Add("@date", date)
                .Add("@id", npcId));

        if (goldDelta != 0)
        {
            var direction = goldDelta > 0 ? "IN" : "OUT";
            var amount = Math.Abs(goldDelta);
            _db.Execute(
                "INSERT INTO so_ledger (npc_id, date, direction, amount, reason, balance_after) " +
                "VALUES (@npcId, @date, @dir, @amount, @reason, @bal)",
                new DbArgs()
                    .Add("@npcId", npcId)
                    .Add("@date", date)
                    .Add("@dir", direction)
                    .Add("@amount", amount)
                    .Add("@reason", action)
                    .Add("@bal", newWealth));
        }
    }

    private void LogEvent(
        string date,
        long npcId,
        string nameVi,
        long citadelId,
        string citadelNameVi,
        string eventKind,
        string headline,
        string detail,
        string impact,
        long tick)
    {
        _db.Execute(
            "INSERT INTO so_journey_log (date, npc_id, npc_name_vi, citadel_id, citadel_name_vi, event_kind, headline_vi, detail_vi, stat_impact, created_tick) " +
            "VALUES (@date, @npcId, @name, @citId, @citName, @kind, @headline, @detail, @impact, @tick)",
            new DbArgs()
                .Add("@date", date)
                .Add("@npcId", npcId)
                .Add("@name", nameVi)
                .Add("@citId", citadelId)
                .Add("@citName", citadelNameVi)
                .Add("@kind", eventKind)
                .Add("@headline", headline)
                .Add("@detail", detail)
                .Add("@impact", impact)
                .Add("@tick", tick));
    }

    public void RecordEvent(GameDate date, long npcId, string npcNameVi, long citadelId, string headlineVi, string detailVi, string category = "travel", string statImpact = "")
    {
        var cit = _map.Find(citadelId);
        var citName = cit?.NameVi ?? "Châu quận";
        var formattedDate = GameDateCodec.Format(date);
        var validCategory = category is "martial" or "commerce" or "governance" or "scholarly" or "travel" ? category : "travel";
        LogEvent(formattedDate, npcId, npcNameVi, citadelId, citName, validCategory, headlineVi, detailVi, statImpact, date.DayIndex);
    }

    public IReadOnlyList<SocietyLogEntry> RecentEvents(int limit = 50, string? category = null, long? citadelId = null)
    {
        var sql = "SELECT id, date, npc_id, npc_name_vi, citadel_id, citadel_name_vi, event_kind, headline_vi, detail_vi, stat_impact " +
                  "FROM so_journey_log ";

        var clauses = new List<string>();
        var args = new DbArgs();

        if (!string.IsNullOrWhiteSpace(category))
        {
            clauses.Add("event_kind = @kind");
            args.Add("@kind", category);
        }

        if (citadelId.HasValue)
        {
            clauses.Add("citadel_id = @citId");
            args.Add("@citId", citadelId.Value);
        }

        if (clauses.Count > 0)
        {
            sql += "WHERE " + string.Join(" AND ", clauses) + " ";
        }

        sql += "ORDER BY id DESC LIMIT " + Math.Clamp(limit, 1, 200);

        return _db.Query(sql, record => new SocietyLogEntry(
            record.GetInt64(0),
            GameDateCodec.Parse(record.GetString(1)),
            record.GetInt64(2),
            record.GetString(3),
            record.GetInt64(4),
            record.GetString(5),
            record.GetString(6),
            record.GetString(7),
            record.GetString(8),
            record.GetStringOrNull(9)),
            args);
    }

    public NpcActivityDef? GetNpcActivity(long npcId)
    {
        return _db.QueryOne(
            "SELECT npc_id, current_citadel_id, current_wealth, focus_path, current_action, stamina, reputation, updated_date " +
            "FROM so_npc_activity WHERE npc_id = @id",
            record =>
            {
                var id = record.GetInt64(0);
                var action = record.GetString(4);
                return new NpcActivityDef(
                    id,
                    record.GetInt64(1),
                    record.GetInt64(2),
                    record.GetString(3),
                    action,
                    record.GetInt32(5),
                    record.GetInt32(6),
                    GameDateCodec.Parse(record.GetString(7)),
                    IsNpcDeceased(id, action),
                    GetNpcAllegiance(id));
            },
            DbArgs.One("@id", npcId));
    }

    public IReadOnlyList<NpcActivityDef> AllActivities()
    {
        return _db.Query(
            "SELECT npc_id, current_citadel_id, current_wealth, focus_path, current_action, stamina, reputation, updated_date " +
            "FROM so_npc_activity",
            record =>
            {
                var id = record.GetInt64(0);
                var action = record.GetString(4);
                return new NpcActivityDef(
                    id,
                    record.GetInt64(1),
                    record.GetInt64(2),
                    record.GetString(3),
                    action,
                    record.GetInt32(5),
                    record.GetInt32(6),
                    GameDateCodec.Parse(record.GetString(7)),
                    IsNpcDeceased(id, action),
                    GetNpcAllegiance(id));
            });
    }

    public NpcStatDelta GetStatDeltas(long npcId)
    {
        var row = _db.QueryOne(
            "SELECT npc_id, war_delta, ldr_delta, int_delta, pol_delta, dip_delta, com_delta, log_delta, cra_delta, updated_date " +
            "FROM so_npc_stat_delta WHERE npc_id = @id",
            record => new NpcStatDelta(
                record.GetInt64(0),
                record.GetInt32(1),
                record.GetInt32(2),
                record.GetInt32(3),
                record.GetInt32(4),
                record.GetInt32(5),
                record.GetInt32(6),
                record.GetInt32(7),
                record.GetInt32(8),
                GameDateCodec.Parse(record.GetString(9))),
            DbArgs.One("@id", npcId));

        return row ?? NpcStatDelta.Empty(npcId, _clock.Today);
    }

    private static string DetermineFocusPath(CharacterAttributes a)
    {
        if (a.War >= 75 && a.War >= Math.Max(a.Com, Math.Max(a.Int, a.Pol)))
        {
            return "martial";
        }
        if (a.Com >= 70 && a.Com >= Math.Max(a.Int, a.Pol))
        {
            return "merchant";
        }
        if (a.Int >= 75 && a.Int >= a.Pol)
        {
            return "strategist";
        }
        if (a.Pol >= 70)
        {
            return "governor";
        }
        return "wanderer";
    }

    private Dictionary<long, NpcStatDelta> LoadAllStatDeltasMap()
    {
        return _db.Query(
            "SELECT npc_id, war_delta, ldr_delta, int_delta, pol_delta, dip_delta, com_delta, log_delta, cra_delta, updated_date FROM so_npc_stat_delta",
            record => new NpcStatDelta(
                record.GetInt64(0),
                record.GetInt32(1),
                record.GetInt32(2),
                record.GetInt32(3),
                record.GetInt32(4),
                record.GetInt32(5),
                record.GetInt32(6),
                record.GetInt32(7),
                record.GetInt32(8),
                GameDateCodec.Parse(record.GetString(9))))
            .ToDictionary(d => d.NpcId);
    }

    internal static int ComputeInitialReputation(NpcDef npc)
    {
        var a = npc.Attributes;
        var role = npc.Role ?? string.Empty;
        var baseAttrScore = (a.War + a.Ldr + a.Int + a.Pol + a.Dip + a.Com) / 3; // ~80..190

        var roleBonus = 0;
        if (role.Contains("Hoàng Đế") || role.Contains("Thái Sư") || role.Contains("Tướng Quốc"))
            roleBonus = 720;
        else if (role.Contains("Quân Chủ") || role.Contains("Minh Chủ") || role.Contains("Châu Mục"))
            roleBonus = 520;
        else if (role.Contains("Thái Thú") || role.Contains("Thứ Sử") || role.Contains("Đại Tướng") || role.Contains("Giáo Tổ"))
            roleBonus = 340;
        else if (Math.Max(a.War, Math.Max(a.Int, a.Ldr)) >= 94)
            roleBonus = 280;
        else if (Math.Max(a.War, Math.Max(a.Int, Math.Max(a.Ldr, a.Pol))) >= 86)
            roleBonus = 140;

        return Math.Clamp(baseAttrScore + roleBonus, 45, 960);
    }

    private static long ComputeInitialWealth(NpcDef npc, string focus)
    {
        var role = npc.Role ?? string.Empty;
        // Rulers and Governors already follow CityGovernance deterministically (govScore = 300),
        // so giving them realistic noble treasuries preserves 100% of non-ruler AI goal selection!
        if (role.Contains("Hoàng Đế") || role.Contains("Thái Sư") || role.Contains("Tướng Quốc"))
            return 26000 + (npc.Attributes.Pol * 40);
        if (role.Contains("Quân Chủ") || role.Contains("Châu Mục"))
            return 16000 + (npc.Attributes.Pol * 30);
        if (role.Contains("Thái Thú") || role.Contains("Giáo Tổ"))
            return 7500 + (npc.Attributes.Pol * 20);

        return InitialWealth(focus);
    }

    private static long InitialWealth(string focus) => focus switch
    {
        "merchant" => 2500,
        "governor" => 3000,
        "martial" => 1500,
        "strategist" => 1200,
        _ => 800
    };

    private static string InitialAction(string focus) => focus switch
    {
        "martial" => "Luyện tập thương thuật tại binh doanh",
        "merchant" => "Khảo sát thời giá tại chợ phiên",
        "strategist" => "Đọc sách nghiên cứu binh thư cổ kim",
        "governor" => "Xử lý công văn tại nha môn",
        _ => "Ngao du sơn thủy kết giao danh sĩ"
    };

    public IReadOnlyList<NpcDef> ReplenishNpcs(GameDate date, int count = 1)
    {
        var formattedDate = GameDateCodec.Format(date);
        var tick = date.DayIndex;
        IReadOnlyList<NpcDef> result = Array.Empty<NpcDef>();
        _db.Transaction(() =>
        {
            result = ReplenishNpcsCore(date, formattedDate, tick, count);
        });
        return result;
    }

    private IReadOnlyList<NpcDef> ReplenishNpcsCore(GameDate date, string formattedDate, long tick, int count)
    {
        if (count <= 0) return Array.Empty<NpcDef>();

        // Cap dynamic population to prevent unbounded memory/database inflation (R_PERF)
        if (_registry.All.Count >= 400)
        {
            return Array.Empty<NpcDef>();
        }

        var spawned = new List<NpcDef>();
        var citadels = _map.Citadels;
        if (citadels.Count == 0) return Array.Empty<NpcDef>();

        var existingNames = new HashSet<string>(_registry.All.Select(n => n.NameVi), StringComparer.OrdinalIgnoreCase);

        var surnames = new[]
        {
            "Triệu", "Lý", "Chu", "Ngô", "Trịnh", "Vương", "Phùng", "Trần", "Chử", "Vệ",
            "Tưởng", "Thẩm", "Hàn", "Dương", "Tần", "Hứa", "Hà", "Lữ", "Khổng", "Tào",
            "Nghiêm", "Kim", "Ngụy", "Đào", "Khương", "Tạ", "Lôi", "Hạ", "Đoàn", "Liêu"
        };

        var middleGivenNames = new[]
        {
            "Văn Huy", "Bá Đạt", "Tử Kính", "Trọng Viễn", "Thúc Minh", "Quý Hòa", "Đức Nhuận",
            "Cảnh Sơn", "Phụng Tiên", "Huyền Thông", "Nguyên Phương", "Tử Hào", "Hoài Viễn",
            "An Bang", "Trấn Quốc", "Tông Diệu", "Trọng Hằng", "Sĩ Trưng", "Dực Sơn", "Kiến Lâm",
            "Minh Lương", "Quốc Bảo", "Đạo Vinh", "Diên Khánh", "Khắc Minh", "Bá Xuyên"
        };

        var archetypes = new[]
        {
            (Focus: "merchant", Role: "Lữ Hành Thương Nhân", SkillName: "Thương Tài", SkillCode: "thuong_tai_dyn",
                SkillEffect: "Giảm 5% giá mua tại sạp chợ", SkillDesc: "Nhạy bén giá cả thương trường, đàm phán mua rẻ bán đắt."),
            (Focus: "martial", Role: "Du Hiệp Kiếm Khách", SkillName: "Kỵ Xạ", SkillCode: "ky_xa_dyn",
                SkillEffect: "+10% sát thương bạo kích cung tiễn", SkillDesc: "Kỹ năng cung tiễn điêu luyện trên lưng ngựa."),
            (Focus: "strategist", Role: "Ẩn Danh Mưu Sĩ", SkillName: "Biện Thuyết", SkillCode: "bien_thuyet_dyn",
                SkillEffect: "+15% hiệu quả đàm phán ngoại giao", SkillDesc: "Hùng biện sắc sảo, nhìn thấu tâm lý đối phương."),
            (Focus: "governor", Role: "Châu Huyện Hiền Sĩ", SkillName: "Khuyến Nông", SkillCode: "khuyen_nong_dyn",
                SkillEffect: "+10% sản lượng nông trang địa phương", SkillDesc: "Khuyến khích khai hoang mở đất, tích cốc phòng đói."),
            (Focus: "wanderer", Role: "Phương Ngoại Kỳ Sĩ", SkillName: "Dược Lý", SkillCode: "duoc_ly_dyn",
                SkillEffect: "Tăng tốc độ hồi phục thể lực", SkillDesc: "Am hiểu y lý thảo dược cứu độ thế gian.")
        };

        for (var i = 0; i < count; i++)
        {
            var rng = CreateSubStreamRng(i * 1009L + _registry.All.Count, (int)(tick % int.MaxValue), 0x50C1E71_002UL);

            // Rebalance population: pick citadel with fewest NPCs
            var targetCit = citadels
                .OrderBy(c => _registry.FindByCitadel(c.Id).Count)
                .ThenBy(c => c.Id)
                .First();

            var arch = archetypes[rng.Next(archetypes.Length)];

            string nameVi = string.Empty;
            for (var attempt = 0; attempt < 50; attempt++)
            {
                var s = surnames[rng.Next(surnames.Length)];
                var mg = middleGivenNames[rng.Next(middleGivenNames.Length)];
                var candidate = $"{s} {mg}";
                if (!existingNames.Contains(candidate))
                {
                    nameVi = candidate;
                    break;
                }
            }
            if (string.IsNullOrEmpty(nameVi))
            {
                nameVi = $"Sĩ Nhân {tick % 1000}_{i + 1}";
            }
            existingNames.Add(nameVi);

            CharacterAttributes attrs = arch.Focus switch
            {
                "merchant" => new CharacterAttributes(
                    War: 40 + rng.Next(15), Ldr: 45 + rng.Next(15), Int: 62 + rng.Next(16),
                    Pol: 58 + rng.Next(15), Dip: 65 + rng.Next(16), Com: 72 + rng.Next(15),
                    Log: 65 + rng.Next(15), Cra: 50 + rng.Next(15), Con: 55 + rng.Next(15),
                    Wil: 60 + rng.Next(15), Prc: 64 + rng.Next(15), Lck: 60 + rng.Next(20)),
                "martial" => new CharacterAttributes(
                    War: 75 + rng.Next(15), Ldr: 66 + rng.Next(15), Int: 45 + rng.Next(15),
                    Pol: 40 + rng.Next(15), Dip: 42 + rng.Next(15), Com: 40 + rng.Next(15),
                    Log: 52 + rng.Next(15), Cra: 50 + rng.Next(15), Con: 70 + rng.Next(18),
                    Wil: 72 + rng.Next(16), Prc: 68 + rng.Next(15), Lck: 55 + rng.Next(15)),
                "strategist" => new CharacterAttributes(
                    War: 38 + rng.Next(15), Ldr: 50 + rng.Next(15), Int: 76 + rng.Next(15),
                    Pol: 68 + rng.Next(15), Dip: 70 + rng.Next(15), Com: 52 + rng.Next(15),
                    Log: 60 + rng.Next(15), Cra: 48 + rng.Next(15), Con: 50 + rng.Next(15),
                    Wil: 65 + rng.Next(15), Prc: 68 + rng.Next(15), Lck: 58 + rng.Next(15)),
                "governor" => new CharacterAttributes(
                    War: 45 + rng.Next(15), Ldr: 65 + rng.Next(15), Int: 68 + rng.Next(15),
                    Pol: 74 + rng.Next(15), Dip: 66 + rng.Next(15), Com: 58 + rng.Next(15),
                    Log: 64 + rng.Next(15), Cra: 52 + rng.Next(15), Con: 56 + rng.Next(15),
                    Wil: 62 + rng.Next(15), Prc: 60 + rng.Next(15), Lck: 56 + rng.Next(15)),
                _ => new CharacterAttributes(
                    War: 55 + rng.Next(15), Ldr: 50 + rng.Next(15), Int: 60 + rng.Next(15),
                    Pol: 50 + rng.Next(15), Dip: 55 + rng.Next(15), Com: 52 + rng.Next(15),
                    Log: 55 + rng.Next(15), Cra: 72 + rng.Next(16), Con: 64 + rng.Next(15),
                    Wil: 70 + rng.Next(15), Prc: 68 + rng.Next(15), Lck: 60 + rng.Next(15))
            };

            var code = $"npc_dyn_{tick}_{rng.Next(1000, 9999)}";
            var skills = new[]
            {
                new NpcSkillDef(arch.SkillCode, arch.SkillName, arch.Focus switch {
                    "martial" => "martial",
                    "merchant" => "commerce",
                    "governor" => "governance",
                    "strategist" => "strategist",
                    _ => "special"
                }, 3, arch.SkillDesc, arch.SkillEffect, Level: Math.Clamp(2 + rng.Next(4), 1, 10))
            };

            var newNpc = _registry.RegisterDynamicNpc(
                code,
                nameVi,
                arch.Role,
                targetCit.Id,
                attrs,
                personality: "Cần cù nhẫn nại",
                bioVi: $"Nhân sĩ mới nổi tại {targetCit.NameVi}, từng bước xây dựng sự nghiệp thời loạn thế.",
                skills);

            var focus = arch.Focus;
            var wealth = InitialWealth(focus);
            var initialAction = InitialAction(focus);

            _db.Execute(
                "INSERT INTO so_npc_activity (npc_id, current_citadel_id, current_wealth, focus_path, current_action, stamina, reputation, updated_date) " +
                "VALUES (@npcId, @citadelId, @wealth, @focus, @action, 100, 50, @date)",
                new DbArgs()
                    .Add("@npcId", newNpc.Id)
                    .Add("@citadelId", targetCit.Id)
                    .Add("@wealth", wealth)
                    .Add("@focus", focus)
                    .Add("@action", initialAction)
                    .Add("@date", formattedDate));

            _db.Execute(
                "INSERT INTO so_npc_stat_delta (npc_id, war_delta, ldr_delta, int_delta, pol_delta, dip_delta, com_delta, log_delta, cra_delta, updated_date) " +
                "VALUES (@npcId, 0, 0, 0, 0, 0, 0, 0, 0, @date)",
                new DbArgs()
                    .Add("@npcId", newNpc.Id)
                    .Add("@date", formattedDate));

            var eventKind = arch.Focus switch
            {
                "martial" => "martial",
                "merchant" => "commerce",
                "governor" => "governance",
                _ => "scholarly"
            };

            var headline = $"Nhân tài mới xuất thế: {newNpc.NameVi} tại {targetCit.NameVi}";
            var detail = $"{newNpc.NameVi} ({newNpc.Role}) vừa xuất đầu lộ diện tại {targetCit.NameVi}, chí hướng rộng lớn, bắt đầu tạo dựng thanh danh thời loạn thế.";

            _db.Execute(
                "INSERT INTO so_journey_log (date, npc_id, npc_name_vi, citadel_id, citadel_name_vi, event_kind, headline_vi, detail_vi, stat_impact, created_tick) " +
                "VALUES (@date, @npcId, @name, @citId, @citName, @kind, @headline, @detail, 'Xuất thế', @tick)",
                new DbArgs()
                    .Add("@date", formattedDate)
                    .Add("@npcId", newNpc.Id)
                    .Add("@name", newNpc.NameVi)
                    .Add("@citId", targetCit.Id)
                    .Add("@citName", targetCit.NameVi)
                    .Add("@kind", eventKind)
                    .Add("@headline", headline)
                    .Add("@detail", detail)
                    .Add("@tick", tick));

            spawned.Add(newNpc);
            _log.Info($"Society replenishment: spawned {newNpc.NameVi} ({newNpc.Role}) in {targetCit.NameVi}.");
        }

        return spawned;
    }

    private bool AnyCitadelUnderpopulated()
    {
        foreach (var c in _map.Citadels)
        {
            if (_registry.FindByCitadel(c.Id).Count < 18)
            {
                return true;
            }
        }
        return false;
    }


    private sealed class SocietySubStreamRng : IRng
    {
        private const ulong Gamma = 0x9E3779B97F4A7C15UL;
        private ulong _state;

        public SocietySubStreamRng(ulong seed)
        {
            _state = seed == 0 ? 0x0123456789ABCDEFUL : seed;
        }

        public ulong State => _state;

        public void Reseed(ulong state) => _state = state == 0 ? 0x0123456789ABCDEFUL : state;

        public ulong NextULong()
        {
            _state += Gamma;
            var z = _state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        public int Next(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            var span = (ulong)(long)(maxExclusive - minInclusive);
            return (int)(minInclusive + (long)(NextULong() % span));
        }

        public double NextDouble() => (NextULong() >> 11) * (1.0 / (1UL << 53));
    }
}
