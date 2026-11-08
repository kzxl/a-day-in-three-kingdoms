using SimT.Contracts;
using SimT.Contracts.Data;
using SimT.Contracts.Ledger;
using SimT.Contracts.Logging;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Power;

/// <summary>
/// Standing at court & 12 warlord factions allegiance: service earns reputation,
/// reputation unlocks 9-rank officialdom, and an office pays monthly stipend.
/// Players can align with warlords and enact regional civil policies.
/// </summary>
internal sealed partial class CourtService : IPoliticalCourt
{
    public const int ServiceDays = 4;
    public const int ServiceReputation = 3;
    public const long ServiceCost = 15;

    public const long FactionStipendBonus = 50;

    private readonly IDbSession _db;
    private readonly IGameClock _clock;
    private readonly IWallet _wallet;
    private readonly ILog _log;
    private readonly IPlayerAttributes? _attributes;

    public CourtService(IDbSession db, IGameClock clock, IWallet wallet, ILog log, IPlayerAttributes? attributes = null)
    {
        _db = db;
        _clock = clock;
        _wallet = wallet;
        _log = log;
        _attributes = attributes;
    }

    public int Reputation => (int)State("reputation");

    public long OfficeId => State("office_id");

    public string? OfficeName =>
        _db.ScalarString("SELECT name_vi FROM po_office WHERE id=@id", DbArgs.One("@id", OfficeId));

    public long MonthlyStipend =>
        _db.ScalarLong("SELECT COALESCE(monthly_stipend, 0) FROM po_office WHERE id=@id", DbArgs.One("@id", OfficeId));

    public long AllegianceFactionId => State("allegiance_faction_id");

    public string? AllegianceFactionName =>
        Factions.FirstOrDefault(f => f.Id == AllegianceFactionId)?.NameVi;

    public IReadOnlyList<OfficeView> Offices =>
        _db.Query(
            """
            SELECT id, code, name_vi, name_en, rank, monthly_stipend, reputation_required
            FROM po_office ORDER BY rank ASC
            """,
            row => new OfficeView(
                row.GetInt64(0),
                row.GetString(1),
                row.GetString(2),
                row.GetString(3),
                row.GetInt32(4),
                row.GetInt64(5),
                row.GetInt32(6)));

    public IReadOnlyList<FactionView> Factions => ReadFactionsForYear();

    public IReadOnlyList<CityGovernanceView> Governances =>
        _db.Query(
            """
            SELECT citadel_id, faction_id, governor_npc_id, unrest_level, defense_strength, tax_rate_percent
            FROM po_city_governance ORDER BY citadel_id ASC
            """,
            row => new CityGovernanceView(
                row.GetInt64(0),
                row.GetInt64(1),
                row.GetInt64(2),
                row.GetInt32(3),
                row.GetInt32(4),
                ApplyDebateTaxRelief(row.GetInt64(0), row.GetInt32(5))));

    public void SetCityOwner(long citadelId, long factionId, long? governorNpcId = null)
    {
        if (governorNpcId.HasValue && governorNpcId.Value > 0)
        {
            _db.Execute(
                """
                INSERT INTO po_city_governance(citadel_id, faction_id, governor_npc_id, unrest_level, defense_strength, tax_rate_percent)
                VALUES(@citId, @facId, @govId, 20, 100, 10)
                ON CONFLICT(citadel_id) DO UPDATE SET faction_id = excluded.faction_id, governor_npc_id = excluded.governor_npc_id
                """,
                new DbArgs().Add("@citId", citadelId).Add("@facId", factionId).Add("@govId", governorNpcId.Value));
        }
        else
        {
            _db.Execute(
                """
                INSERT INTO po_city_governance(citadel_id, faction_id, governor_npc_id, unrest_level, defense_strength, tax_rate_percent)
                VALUES(@citId, @facId, 0, 20, 100, 10)
                ON CONFLICT(citadel_id) DO UPDATE SET faction_id = excluded.faction_id
                """,
                new DbArgs().Add("@citId", citadelId).Add("@facId", factionId));
        }
    }

    public IReadOnlyList<DecreeLogView> RecentDecrees(int limit = 10) =>
        _db.Query(
            """
            SELECT id, day_index, citadel_id, policy_code, cost_copper, reputation_gain, unrest_delta, narrative_vi
            FROM po_decree_log ORDER BY id DESC LIMIT @lim
            """,
            row => new DecreeLogView(
                row.GetInt64(0),
                row.GetInt64(1),
                row.GetInt64(2),
                row.GetString(3),
                row.GetInt32(4),
                row.GetInt32(5),
                row.GetInt32(6),
                row.GetString(7)),
            DbArgs.One("@lim", limit));

    public CourtResult Serve()
    {
        if (_wallet.Liquid < ServiceCost)
        {
            return CourtResult.Failure($"Cần {ServiceCost} Quan lễ nhậm, chỉ có {_wallet.Liquid}.");
        }

        _db.Transaction(() =>
        {
            _wallet.Charge(ServiceCost, JournalCodes.CourtService, "Lễ nhậm cung sự");
            _clock.Advance(ServiceDays);
            SetState("reputation", Reputation + ServiceReputation);
        });

        return CourtResult.Success(
            $"Cung sự {ServiceDays} ngày: uy tín {Reputation}, tốn {ServiceCost} Quan.");
    }

    public CourtResult Apply(long officeId)
    {
        var office = Offices.FirstOrDefault(candidate => candidate.Id == officeId);
        if (office is null)
        {
            return CourtResult.Failure("Không có chức vụ này.");
        }

        if (OfficeId == officeId)
        {
            return CourtResult.Failure($"Đã tại chức {office.NameVi}.");
        }

        if (OfficeId != 0)
        {
            return CourtResult.Failure("Đang giữ một chức vụ khác, phải từ chức mới nhận được.");
        }

        if (Reputation < office.ReputationRequired)
        {
            return CourtResult.Failure(
                $"Cần {office.ReputationRequired} uy tín để nhận {office.NameVi}, hiện có {Reputation}.");
        }

        if (office.Rank >= 4 && AllegianceFactionId == 0)
        {
            return CourtResult.Failure(
                $"Từ phẩm {office.Rank} ({office.NameVi}) trở lên là trọng chức địa phương, bắt buộc phải quy thuận một thế lực chư hầu để được đề bạt!");
        }

        if (_attributes is not null)
        {
            var attrs = _attributes.Attributes;
            if (office.Rank >= 6 && attrs.War < 60 && attrs.Ldr < 55)
            {
                return CourtResult.Failure($"Chức vị võ quan {office.NameVi} yêu cầu Võ Lực tối thiểu 60 hoặc Thống Soái 55 (hiện có Võ {attrs.War}, Thống {attrs.Ldr}).");
            }

            if (office.Rank >= 7 && (attrs.Pol < 60 || attrs.Ldr < 50))
            {
                return CourtResult.Failure($"Chức vị phiên trấn {office.NameVi} yêu cầu Trị Quốc 60 và Thống Soái 50 (hiện có Trị {attrs.Pol}, Thống {attrs.Ldr}).");
            }

            if (office.Rank >= 8 && attrs.Pol < 70)
            {
                return CourtResult.Failure($"Chức vị Tam Công yêu cầu Trị Quốc tối thiểu 70 (hiện có Trị {attrs.Pol}).");
            }

            if (office.Rank >= 9 && (attrs.Pol < 80 || attrs.Int < 75))
            {
                return CourtResult.Failure($"Chức vị Thừa Tướng tột đỉnh triều cương yêu cầu Trị Quốc 80 và Mưu Lược 75 (hiện có Trị {attrs.Pol}, Mưu {attrs.Int}).");
            }
        }

        _db.Transaction(() => SetState("office_id", officeId));

        _log.Info($"appointed to {office.Code} with reputation {Reputation}");
        return CourtResult.Success(
            $"Được bổ nhiệm {office.NameVi} (phẩm {office.Rank}), bổng {office.MonthlyStipend} Quan/tháng.");
    }

    public CourtResult Resign()
    {
        if (OfficeId == 0)
        {
            return CourtResult.Failure("Chưa tại chức.");
        }

        var name = OfficeName;
        _db.Transaction(() => SetState("office_id", 0));
        return CourtResult.Success($"Từ chức {name}.");
    }

    public CourtResult JoinFaction(long factionId)
    {
        if (Reputation < 5)
        {
            return CourtResult.Failure($"Cần tối thiểu 5 điểm Uy tín để gia nhập thế lực chư hầu (hiện có {Reputation}).");
        }

        var faction = Factions.FirstOrDefault(f => f.Id == factionId);
        if (faction is null)
        {
            return CourtResult.Failure("Thế lực chư hầu không tồn tại.");
        }

        if (AllegianceFactionId == factionId)
        {
            return CourtResult.Failure($"Đã quy thuận dưới trướng {faction.NameVi}.");
        }

        _db.Transaction(() =>
        {
            CancelLordTask();
            SetState("allegiance_faction_id", factionId);
            _wallet.Pay(100, JournalCodes.CourtStipend, $"Bổng lộc kết nạp quân kỳ {faction.NameVi}");
        });

        _log.Info($"Player joined faction {faction.Code}");
        return CourtResult.Success($"Quy thuận minh chủ {faction.NameVi}! Nhận 100 Quan quân lương ban thưởng.");
    }

    public CourtResult LeaveFaction()
    {
        if (AllegianceFactionId == 0)
        {
            return CourtResult.Failure("Hiện chưa quy thuận thế lực chư hầu nào.");
        }

        var prevName = AllegianceFactionName ?? "chư hầu";
        _db.Transaction(() =>
        {
            CancelLordTask();
            SetState("allegiance_faction_id", 0);
            var rep = Math.Max(0, Reputation - 5);
            SetState("reputation", rep);
        });

        return CourtResult.Success($"Đã rời khỏi thế lực {prevName}. Uy tín giảm 5 điểm do bội ước.");
    }

    public CourtResult EnactPolicy(string policyCode, long citadelId)
    {
        var gov = Governances.FirstOrDefault(g => g.CitadelId == citadelId);
        if (gov is null)
        {
            return CourtResult.Failure("Thành trì không có ban quản trị chính trị.");
        }

        switch (policyCode.ToLowerInvariant())
        {
            case "relief": // An dân tế bần
            {
                const long cost = 80;
                if (_wallet.Liquid < cost)
                {
                    return CourtResult.Failure($"Cần {cost} Quan cứu tế, ngân lượng không đủ.");
                }

                var unrestDelta = -15;
                var newUnrest = Math.Max(0, gov.UnrestLevel + unrestDelta);
                var actualDelta = newUnrest - gov.UnrestLevel;

                _db.Transaction(() =>
                {
                    _wallet.Charge(cost, JournalCodes.CourtService, "An dân tế bần chẩn cứu bá tánh");
                    _db.Execute(
                        "UPDATE po_city_governance SET unrest_level = @unrest WHERE citadel_id = @citId",
                        new DbArgs().Add("@unrest", newUnrest).Add("@citId", citadelId));
                    SetState("reputation", Reputation + 3);
                    _db.Execute(
                        """
                        INSERT INTO po_decree_log (day_index, citadel_id, policy_code, cost_copper, reputation_gain, unrest_delta, narrative_vi)
                        VALUES (@day, @citId, 'relief', @cost, 3, @delta, 'Mở kho tế bần, bá tánh quy phục, lòng dân an định.')
                        """,
                        new DbArgs().Add("@day", _clock.Today.DayIndex).Add("@citId", citadelId).Add("@cost", cost).Add("@delta", actualDelta));
                });

                return CourtResult.Success($"Thi hành An Dân: Giảm {Math.Abs(actualDelta)}% bất ổn, uy tín +3, tiêu {cost} Quan.");
            }

            case "tax": // Trưng thu thuế bổ sung
            {
                if (OfficeId == 0)
                {
                    return CourtResult.Failure("Phải là quan chức triều đình mới có quyền trưng thu thuế.");
                }

                const long revenue = 120;
                var unrestDelta = 10;
                var newUnrest = Math.Min(100, gov.UnrestLevel + unrestDelta);
                var actualDelta = newUnrest - gov.UnrestLevel;

                _db.Transaction(() =>
                {
                    _wallet.Pay(revenue, JournalCodes.CourtStipend, "Trưng thu thuế khóa địa phương");
                    _db.Execute(
                        "UPDATE po_city_governance SET unrest_level = @unrest WHERE citadel_id = @citId",
                        new DbArgs().Add("@unrest", newUnrest).Add("@citId", citadelId));
                    _db.Execute(
                        """
                        INSERT INTO po_decree_log (day_index, citadel_id, policy_code, cost_copper, reputation_gain, unrest_delta, narrative_vi)
                        VALUES (@day, @citId, 'tax', 0, 0, @delta, 'Trưng thu thuế khóa sung công, lòng dân có phần oán thán.')
                        """,
                        new DbArgs().Add("@day", _clock.Today.DayIndex).Add("@citId", citadelId).Add("@delta", actualDelta));
                });

                return CourtResult.Success($"Trưng thu thuế hoàn tất: Thu về {revenue} Quan, bất ổn tăng +{actualDelta}%.");
            }

            case "levy": // Chiêu mộ dân binh
            {
                const long cost = 50;
                if (_wallet.Liquid < cost)
                {
                    return CourtResult.Failure($"Cần {cost} Quan thao luyện chiêu binh, ngân lượng không đủ.");
                }

                var newDefense = gov.DefenseStrength + 10;
                var newUnrest = Math.Max(0, gov.UnrestLevel - 5);

                _db.Transaction(() =>
                {
                    _wallet.Charge(cost, JournalCodes.CourtService, "Chiêu mộ nghĩa dũng dân binh");
                    _db.Execute(
                        "UPDATE po_city_governance SET defense_strength = @def, unrest_level = @unrest WHERE citadel_id = @citId",
                        new DbArgs().Add("@def", newDefense).Add("@unrest", newUnrest).Add("@citId", citadelId));
                    SetState("reputation", Reputation + 2);
                    _db.Execute(
                        """
                        INSERT INTO po_decree_log (day_index, citadel_id, policy_code, cost_copper, reputation_gain, unrest_delta, narrative_vi)
                        VALUES (@day, @citId, 'levy', @cost, 2, -5, 'Chiêu mộ tráng đinh lập đội tuần phòng, thành trì vững vàng.')
                        """,
                        new DbArgs().Add("@day", _clock.Today.DayIndex).Add("@citId", citadelId).Add("@cost", cost));
                });

                return CourtResult.Success($"Chiêu binh thành công: Phòng thủ +10, bất ổn -5%, uy tín +2, tiêu {cost} Quan.");
            }

            case "land_reclaim": // Đồn điền khai hoang
            {
                const long cost = 100;
                if (_wallet.Liquid < cost)
                {
                    return CourtResult.Failure($"Cần {cost} Quan mở đồn điền khuyến nông, ngân lượng không đủ.");
                }

                var newDefense = gov.DefenseStrength + 15;
                var newUnrest = Math.Max(0, gov.UnrestLevel - 10);

                _db.Transaction(() =>
                {
                    _wallet.Charge(cost, JournalCodes.CourtService, "Đồn điền khai hoang khuyến khích canh nông");
                    _db.Execute(
                        "UPDATE po_city_governance SET defense_strength = @def, unrest_level = @unrest WHERE citadel_id = @citId",
                        new DbArgs().Add("@def", newDefense).Add("@unrest", newUnrest).Add("@citId", citadelId));
                    SetState("reputation", Reputation + 5);
                    _attributes?.GainFame(30, "Lập đồn điền khẩn hoang, khuyến khích canh nông yên dân.", virtueDelta: 5);
                    _db.Execute(
                        """
                        INSERT INTO po_decree_log (day_index, citadel_id, policy_code, cost_copper, reputation_gain, unrest_delta, narrative_vi)
                        VALUES (@day, @citId, 'land_reclaim', @cost, 5, -10, 'Thi hành quốc sách đồn điền, khẩn hoang lập ấp, bá tánh no ấm.')
                        """,
                        new DbArgs().Add("@day", _clock.Today.DayIndex).Add("@citId", citadelId).Add("@cost", cost));
                });

                return CourtResult.Success($"Đồn điền hoàn tất: Phòng thủ +15, bất ổn -10%, uy tín +5, đức độ +5, tiêu {cost} Quan.");
            }

            case "pacify": // Dẹp loạn dân biến
            {
                if (gov.UnrestLevel < 35)
                {
                    return CourtResult.Failure($"Bất ổn hiện tại ({gov.UnrestLevel}%) chưa tới mức bạo động (cần >= 35%).");
                }

                const long cost = 40;
                if (_wallet.Liquid < cost)
                {
                    return CourtResult.Failure($"Cần {cost} Quan khao quân tiễu loạn.");
                }

                var unrestDelta = -25;
                var newUnrest = Math.Max(0, gov.UnrestLevel + unrestDelta);
                var actualDelta = newUnrest - gov.UnrestLevel;

                _db.Transaction(() =>
                {
                    _wallet.Charge(cost, JournalCodes.CourtService, "Khao quân dẹp loạn thổ phỉ");
                    _db.Execute(
                        "UPDATE po_city_governance SET unrest_level = @unrest WHERE citadel_id = @citId",
                        new DbArgs().Add("@unrest", newUnrest).Add("@citId", citadelId));
                    SetState("reputation", Reputation + 4);
                    _db.Execute(
                        """
                        INSERT INTO po_decree_log (day_index, citadel_id, policy_code, cost_copper, reputation_gain, unrest_delta, narrative_vi)
                        VALUES (@day, @citId, 'pacify', @cost, 4, @delta, 'Xuất quân dẹp tan bạo loạn, lập lại kỷ cương bờ cõi.')
                        """,
                        new DbArgs().Add("@day", _clock.Today.DayIndex).Add("@citId", citadelId).Add("@cost", cost).Add("@delta", actualDelta));
                });

                return CourtResult.Success($"Dẹp loạn thành công: Giảm {Math.Abs(actualDelta)}% bất ổn, uy tín +4, tiêu {cost} Quan.");
            }

            default:
                return CourtResult.Failure($"Chính sách '{policyCode}' không hợp lệ.");
        }
    }

    /// <summary>Monthly stipend while in office and faction subsidies. Real cashflow, paid at the boundary.</summary>
    public void PayStipend(GameDate date)
    {
        var stipend = MonthlyStipend;
        if (stipend > 0)
        {
            _wallet.Pay(
                stipend,
                JournalCodes.CourtStipend,
                $"Bổng lộc tháng {date.Month:00}/{date.Year}",
                $"politics.stipend:{date.DayIndex}");
            _log.Info($"stipend {stipend} paid on {date}");
        }

        // Faction allegiance bonus stipend
        if (AllegianceFactionId > 0)
        {
            _wallet.Pay(
                FactionStipendBonus,
                JournalCodes.CourtStipend,
                $"Phụ cấp quân lương {AllegianceFactionName}",
                $"politics.faction_stipend:{date.DayIndex}");
        }

        // Natural unrest simulation across citadels: peaceful drift towards 15%
        _db.Execute(
            """
            UPDATE po_city_governance 
            SET unrest_level = CASE 
                WHEN unrest_level > 25 THEN unrest_level - 1 
                WHEN unrest_level < 10 THEN unrest_level + 1 
                ELSE unrest_level 
            END
            """);
    }

    private long State(string key)
    {
        var value = _db.ScalarString("SELECT value FROM po_state WHERE key=@key", DbArgs.One("@key", key));
        if (value is null)
        {
            throw new SimException("politics.state_missing", $"po_state has no '{key}' row; migration did not run.");
        }

        return long.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private void SetState(string key, long value) =>
        _db.Execute(
            "INSERT INTO po_state(key, value) VALUES(@key, @value) ON CONFLICT(key) DO UPDATE SET value=excluded.value",
            new DbArgs().Add("@key", key).Add("@value", value));
}
