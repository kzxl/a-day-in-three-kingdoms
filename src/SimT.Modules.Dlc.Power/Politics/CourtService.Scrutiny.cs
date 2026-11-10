using SimT.Contracts;
using SimT.Contracts.Data;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Power;

internal sealed partial class CourtService
{
    public WarlordScrutinyView? CurrentScrutiny
    {
        get
        {
            var currentCityId = _capabilities?.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var ps) == true && ps is not null ? ps.CurrentCityId : 1;
            if (currentCityId <= 0) currentCityId = 1;

            var factionId = _db.ScalarLong("SELECT faction_id FROM po_city_governance WHERE citadel_id = @c", DbArgs.One("@c", currentCityId));
            if (factionId <= 0) factionId = 1;

            var faction = Factions.FirstOrDefault(f => f.Id == factionId);
            var factionName = faction?.NameVi ?? "Triều Đình";
            var leaderName = faction?.NameVi ?? "Minh Chủ";

            var row = _db.Query(
                "SELECT scrutiny_score, last_levy_day, embargo_until_day FROM po_warlord_scrutiny WHERE faction_id = @f",
                r => (Score: r.GetInt32(0), LastLevyDay: r.GetInt32(1), EmbargoUntil: r.GetInt32(2)),
                DbArgs.One("@f", factionId)).FirstOrDefault();

            var scrutinyScore = row.Score;
            var embargoUntil = row.EmbargoUntil;
            var currentDay = _clock.Today.DayIndex;
            var isEmbargoed = embargoUntil > currentDay;
            var remainingEmbargo = (int)Math.Max(0, (long)embargoUntil - currentDay);

            var liquidWealth = _wallet.Balance;
            var rep = Reputation;
            var officeRank = (int)_db.ScalarLong("SELECT COALESCE(rank, 0) FROM po_office WHERE id = @id", DbArgs.One("@id", OfficeId));
            var baseShield = 500L + (rep * 25L) + (officeRank * 1500L);
            if (AllegianceFactionId == factionId && factionId > 0)
            {
                baseShield = (long)(baseShield * 1.5);
            }

            string threatLevel;
            string statusSummary;
            int surcharge;

            if (scrutinyScore < 40)
            {
                threatLevel = "AN_TOAN";
                statusSummary = "Bình an: Quan phủ và quân phiệt không để mắt đến tài sản của bạn.";
                surcharge = 0;
            }
            else if (scrutinyScore < 60)
            {
                threatLevel = "DO_XET";
                statusSummary = "Dò xét: Sai nha tuần sát các kho hàng, phụ thu thuế chợ +10%.";
                surcharge = 10;
            }
            else if (scrutinyScore < 85)
            {
                threatLevel = "SACH_NHIEU";
                statusSummary = "Sách nhiễu: Chúa công hạ trát ép nộp quân nhu, phụ thu thuế chợ +20%.";
                surcharge = 20;
            }
            else
            {
                threatLevel = "HIEM_HOA";
                statusSummary = "Hiểm họa: Nguy cơ bị kê biên sản nghiệp và cấm vận buôn bán! Phụ thu thuế chợ +35%.";
                surcharge = 35;
            }

            var activeDemands = _db.Query(
                """
                SELECT d.id, d.faction_id, f.name_vi, d.demand_type, d.title_vi, d.description_vi, d.demand_copper, d.due_day_index, d.status
                FROM po_scrutiny_demand d
                LEFT JOIN po_faction f ON f.id = d.faction_id
                WHERE d.faction_id = @f AND d.status = 'PENDING'
                ORDER BY d.id DESC
                """,
                r => new ScrutinyDemandView(
                    r.GetInt64(0),
                    r.GetInt64(1),
                    r.IsDBNull(2) ? factionName : r.GetString(2),
                    r.GetString(3),
                    r.GetString(4),
                    r.GetString(5),
                    r.GetInt32(6),
                    r.GetInt32(7),
                    r.GetString(8)),
                DbArgs.One("@f", factionId));

            return new WarlordScrutinyView(
                factionId,
                factionName,
                leaderName,
                scrutinyScore,
                threatLevel,
                statusSummary,
                liquidWealth,
                baseShield,
                surcharge,
                isEmbargoed,
                remainingEmbargo,
                activeDemands);
        }
    }

    public IReadOnlyList<ScrutinyDemandView> ActiveScrutinyDemands =>
        _db.Query(
            """
            SELECT d.id, d.faction_id, f.name_vi, d.demand_type, d.title_vi, d.description_vi, d.demand_copper, d.due_day_index, d.status
            FROM po_scrutiny_demand d
            LEFT JOIN po_faction f ON f.id = d.faction_id
            WHERE d.status = 'PENDING'
            ORDER BY d.id DESC
            """,
            r => new ScrutinyDemandView(
                r.GetInt64(0),
                r.GetInt64(1),
                r.IsDBNull(2) ? "Triều Đình" : r.GetString(2),
                r.GetString(3),
                r.GetString(4),
                r.GetString(5),
                r.GetInt32(6),
                r.GetInt32(7),
                r.GetString(8)));

    public IReadOnlyList<PhilanthropyLogView> RecentPhilanthropyLogs(int limit = 5) =>
        _db.Query(
            """
            SELECT id, citadel_id, day_index, copper_donated, fame_earned, virtue_earned, scrutiny_reduced, narrative_vi
            FROM po_philanthropy_log
            ORDER BY id DESC LIMIT @lim
            """,
            r => new PhilanthropyLogView(
                r.GetInt64(0),
                r.GetInt64(1),
                r.GetInt32(2),
                r.GetInt32(3),
                r.GetInt32(4),
                r.GetInt32(5),
                r.GetInt32(6),
                r.GetString(7)),
            DbArgs.One("@lim", limit));

    public void SimulateWarlordScrutiny(GameDate date)
    {
        var currentCityId = _capabilities?.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var ps) == true && ps is not null ? ps.CurrentCityId : 1;
        if (currentCityId <= 0) currentCityId = 1;

        var factionId = _db.ScalarLong("SELECT faction_id FROM po_city_governance WHERE citadel_id = @c", DbArgs.One("@c", currentCityId));
        if (factionId <= 0) factionId = 1;

        var liquidWealth = _wallet.Balance;
        var rep = Reputation;
        var officeRank = (int)_db.ScalarLong("SELECT COALESCE(rank, 0) FROM po_office WHERE id = @id", DbArgs.One("@id", OfficeId));
        var baseShield = 500L + (rep * 25L) + (officeRank * 1500L);
        if (AllegianceFactionId == factionId && factionId > 0)
        {
            baseShield = (long)(baseShield * 1.5);
        }

        int delta;
        if (liquidWealth > baseShield)
        {
            var excess = liquidWealth - baseShield;
            delta = Math.Clamp((int)(excess / 300), 5, 25);
        }
        else
        {
            delta = -10;
        }

        _db.Execute(
            """
            INSERT INTO po_warlord_scrutiny (faction_id, scrutiny_score, last_levy_day, embargo_until_day)
            VALUES (@f, MAX(0, MIN(100, @initScore)), 0, 0)
            ON CONFLICT(faction_id) DO UPDATE SET
                scrutiny_score = MAX(0, MIN(100, scrutiny_score + @delta))
            """,
            DbArgs.One("@f", factionId)
                .Add("@initScore", Math.Max(0, delta))
                .Add("@delta", delta));

        var scrutiny = (int)_db.ScalarLong("SELECT scrutiny_score FROM po_warlord_scrutiny WHERE faction_id = @f", DbArgs.One("@f", factionId));
        var lastLevy = (int)_db.ScalarLong("SELECT last_levy_day FROM po_warlord_scrutiny WHERE faction_id = @f", DbArgs.One("@f", factionId));

        if (scrutiny >= 60 && (date.DayIndex - lastLevy) >= 30)
        {
            var hasPending = _db.ScalarLong("SELECT COUNT(*) FROM po_scrutiny_demand WHERE faction_id = @f AND status = 'PENDING'", DbArgs.One("@f", factionId)) > 0;
            if (!hasPending)
            {
                var faction = Factions.FirstOrDefault(f => f.Id == factionId);
                var factionName = faction?.NameVi ?? "Minh Chủ";
                var demandCopper = Math.Clamp((int)(liquidWealth * 0.20), 300, 3000);
                var dueDay = date.DayIndex + 30;

                _db.Execute(
                    """
                    INSERT INTO po_scrutiny_demand (faction_id, demand_type, title_vi, description_vi, demand_copper, due_day_index, status)
                    VALUES (@f, 'WAR_LEVY', @title, @desc, @amt, @due, 'PENDING')
                    """,
                    DbArgs.One("@f", factionId)
                        .Add("@title", $"Hịch Văn Quân Nhu: {factionName} Thảo Phạt Tiền Tuyến")
                        .Add("@desc", $"Ngươi tích lũy tài phú quá thịnh tại bổn địa mà chưa báo quốc. Nay tiền tuyến chinh chiến khẩn cấp, lệnh nộp {demandCopper:N0} Quan trợ chiến!")
                        .Add("@amt", demandCopper)
                        .Add("@due", dueDay));

                _db.Execute("UPDATE po_warlord_scrutiny SET last_levy_day = @day WHERE faction_id = @f",
                    DbArgs.One("@day", date.DayIndex).Add("@f", factionId));

                _log.Info($"[WarlordScrutiny] Issued War Levy Demand of {demandCopper} Quan from faction {factionId} (Scrutiny: {scrutiny})");
            }
        }

        if (scrutiny >= 85 && liquidWealth >= 2000)
        {
            var seizureFine = (long)(liquidWealth * 0.25);
            if (seizureFine > 0)
            {
                try
                {
                    _wallet.Charge(seizureFine, "SCRUTINY_SEIZURE", "Sai nha quân phiệt cưỡng chế tịch thu 25% ngân khố do nghi án tàng trữ phi pháp");
                    _db.Execute("UPDATE po_warlord_scrutiny SET scrutiny_score = 50 WHERE faction_id = @f", DbArgs.One("@f", factionId));
                    _log.Warn($"[WarlordScrutiny] Triggered Asset Seizure: {seizureFine} Quan confiscated by faction {factionId}");
                }
                catch
                {
                    // wallet insufficient
                }
            }
        }
    }

    public CourtResult ResolveScrutinyDemand(long demandId, string choiceCode)
    {
        var demand = _db.Query(
            "SELECT faction_id, demand_copper, status, title_vi FROM po_scrutiny_demand WHERE id = @id",
            r => (FactionId: r.GetInt64(0), DemandCopper: r.GetInt32(1), Status: r.GetString(2), TitleVi: r.GetString(3)),
            DbArgs.One("@id", demandId)).FirstOrDefault();

        if (demand.DemandCopper <= 0) return CourtResult.Failure("Không tìm thấy trát lệnh yêu cầu.");
        if (demand.Status != "PENDING") return CourtResult.Failure("Trát lệnh này đã được xử lý trước đó.");

        var normChoice = choiceCode.ToUpperInvariant();
        if (normChoice == "PAY")
        {
            if (_wallet.Balance < demand.DemandCopper)
            {
                return CourtResult.Failure($"Ngân quỹ không đủ {demand.DemandCopper:N0} Quan để giao nộp.");
            }

            _wallet.Charge(demand.DemandCopper, "SCRUTINY_LEVY", $"Nộp quân phí theo hịch văn #{demandId}");
            _db.Execute("UPDATE po_scrutiny_demand SET status = 'PAID' WHERE id = @id", DbArgs.One("@id", demandId));

            var meritsGain = Math.Max(5, demand.DemandCopper / 50);
            AddMerits(meritsGain);

            _db.Execute(
                "UPDATE po_warlord_scrutiny SET scrutiny_score = MAX(0, scrutiny_score - 40) WHERE faction_id = @f",
                DbArgs.One("@f", demand.FactionId));

            return CourtResult.Success($"Đã giao nộp {demand.DemandCopper:N0} Quan quân nhu. Minh chủ ban thưởng +{meritsGain} Huân công, thế lực giảm bớt dòm ngó (-40 điểm).");
        }

        if (normChoice == "BRIBE")
        {
            var bribeCost = demand.DemandCopper / 2;
            if (_wallet.Balance < bribeCost)
            {
                return CourtResult.Failure($"Không đủ {bribeCost:N0} Quan để mua chuộc sai nha.");
            }

            _wallet.Charge(bribeCost, "SCRUTINY_BRIBE", $"Đút lót sai nha giảm trừ trát thuế #{demandId}");
            _db.Execute("UPDATE po_scrutiny_demand SET status = 'BRIBED' WHERE id = @id", DbArgs.One("@id", demandId));

            var isSuccess = (demandId + _clock.Today.DayIndex) % 4 != 0; // 75% success
            if (isSuccess)
            {
                _db.Execute(
                    "UPDATE po_warlord_scrutiny SET scrutiny_score = MAX(0, scrutiny_score - 20) WHERE faction_id = @f",
                    DbArgs.One("@f", demand.FactionId));
                return CourtResult.Success($"Đút lót {bribeCost:N0} Quan thành công. Sai nha nhận ngân lượng đã lấp liếm sổ sách, áp lực dòm ngó giảm nhẹ (-20 điểm).");
            }
            else
            {
                _db.Execute(
                    "UPDATE po_warlord_scrutiny SET scrutiny_score = MIN(100, scrutiny_score + 25) WHERE faction_id = @f",
                    DbArgs.One("@f", demand.FactionId));
                return CourtResult.Failure($"Bại lộ! Khâm sai thanh liêm phát giác hành vi hối lộ, tịch thu ngân lượng và tăng cường thẩm xét (+25 điểm dòm ngó)!");
            }
        }

        if (normChoice == "DEFY")
        {
            _db.Execute("UPDATE po_scrutiny_demand SET status = 'DEFYED' WHERE id = @id", DbArgs.One("@id", demandId));
            var embargoUntil = _clock.Today.DayIndex + 60;
            _db.Execute(
                "UPDATE po_warlord_scrutiny SET scrutiny_score = MIN(100, scrutiny_score + 35), embargo_until_day = @embargo WHERE faction_id = @f",
                DbArgs.One("@embargo", embargoUntil).Add("@f", demand.FactionId));

            return CourtResult.Success("Ngươi khẳng khái xé trát cự tuyệt nộp mãi lộ! Quân phiệt thịnh nộ ban hành lệnh cấm vận buôn bán tại lãnh địa trong 60 ngày (+35 điểm dòm ngó).");
        }

        return CourtResult.Failure($"Quyết sách '{choiceCode}' không hợp lệ.");
    }

    public CourtResult DonatePhilanthropy(int copperAmount)
    {
        if (copperAmount < 100)
        {
            return CourtResult.Failure("Mức quyên góp cứu tế tối thiểu là 100 Quan.");
        }

        if (_wallet.Balance < copperAmount)
        {
            return CourtResult.Failure($"Ngân quỹ không đủ {copperAmount:N0} Quan để phát chẩn.");
        }

        _wallet.Charge(copperAmount, "PHILANTHROPY", $"Quyên góp {copperAmount:N0} Quan phát chẩn cứu tế bá tánh");

        var fameGain = Math.Max(1, copperAmount / 100 * 2);
        var virtueGain = Math.Max(1, copperAmount / 100 * 3);
        var scrutinyRed = Math.Max(5, copperAmount / 100 * 5);

        SetState("reputation", Reputation + fameGain);

        _db.Execute(
            "UPDATE po_warlord_scrutiny SET scrutiny_score = MAX(0, scrutiny_score - @red)",
            DbArgs.One("@red", scrutinyRed));

        var currentCityId = _capabilities?.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var ps) == true && ps is not null ? ps.CurrentCityId : 1;
        var narrative = $"Thương nhân trích {copperAmount:N0} Quan cứu tế nạn dân, sửa cầu đắp đê. Tiếng thơm vang xa: Uy tín +{fameGain}, Đức độ +{virtueGain}, Tiêu giảm {scrutinyRed} điểm dòm ngó quân phiệt.";

        _db.Execute(
            """
            INSERT INTO po_philanthropy_log (citadel_id, day_index, copper_donated, fame_earned, virtue_earned, scrutiny_reduced, narrative_vi)
            VALUES (@c, @day, @copper, @fame, @virtue, @red, @narrative)
            """,
            DbArgs.One("@c", currentCityId)
                .Add("@day", _clock.Today.DayIndex)
                .Add("@copper", copperAmount)
                .Add("@fame", fameGain)
                .Add("@virtue", virtueGain)
                .Add("@red", scrutinyRed)
                .Add("@narrative", narrative));

        return CourtResult.Success(narrative);
    }
}
