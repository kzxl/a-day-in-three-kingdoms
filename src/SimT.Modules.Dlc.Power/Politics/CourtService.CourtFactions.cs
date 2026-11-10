using SimT.Contracts;
using SimT.Contracts.Data;
using SimT.Contracts.Ledger;
using SimT.Contracts.Logging;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Power;

internal sealed partial class CourtService
{
    public IReadOnlyList<CourtFactionStandingView> CourtFactionStandings =>
        _db.Query(
            """
            SELECT faction_code, name_vi, standing_score, description_vi
            FROM po_court_faction_standing
            ORDER BY faction_code ASC
            """,
            r =>
            {
                var score = r.GetInt32(2);
                var attitude = score switch
                {
                    >= 50 => "🟢 Cực kỳ ủng hộ (Đồng minh cốt cán)",
                    >= 15 => "🟢 Thân thiện (Gắn kết)",
                    >= -14 => "🟡 Trung lập (Dò xét)",
                    >= -49 => "🟠 Hiềm khích (Bất mãn)",
                    _ => "🔴 Thù nghịch (Mưu toan hãm hại)"
                };
                return new CourtFactionStandingView(
                    r.GetString(0),
                    r.GetString(1),
                    score,
                    r.GetString(3),
                    attitude);
            });

    public CourtResult ShiftCourtFactionStanding(string factionCode, int delta, string reasonVi)
    {
        var code = factionCode.ToLowerInvariant();
        _db.Execute(
            """
            UPDATE po_court_faction_standing
            SET standing_score = MAX(-100, MIN(100, standing_score + @delta))
            WHERE faction_code = @code
            """,
            DbArgs.One("@delta", delta).Add("@code", code));

        _log.Info($"[CourtFaction] Standing with {code} shifted by {delta:+0;-0;0}: {reasonVi}");
        return CourtResult.Success($"Điểm quan hệ với thế lực {code} biến động {delta:+0;-0;0}.");
    }

    public CourtResult LobbyCourtFaction(string factionCode, int copperAmount)
    {
        if (copperAmount < 200)
        {
            return CourtResult.Failure("Mức vận động hành lang tối thiểu là 200 Quan.");
        }

        if (_wallet.Liquid < copperAmount)
        {
            return CourtResult.Failure($"Ngân lượng không đủ (Cần {copperAmount:N0} Quan, hiện có {_wallet.Liquid:N0} Quan).");
        }

        var code = factionCode.ToLowerInvariant();
        _wallet.Charge(copperAmount, JournalCodes.CourtStipend, $"Vận động hành lang triều đình: {code}");

        int boost = Math.Clamp(copperAmount / 25, 5, 30);

        if (code == "eunuch")
        {
            ShiftCourtFactionStanding("eunuch", boost, "Biếu xén quà cáp hoạn quan");
            ShiftCourtFactionStanding("scholar", -Math.Max(3, boost / 2), "Sĩ phu khinh thường cấu kết hoạn quan");
            return CourtResult.Success($"Đút lót {copperAmount:N0} Quan cho nội thị Thập Thường Thị (+{boost} Điểm Hoạn Quan)! Hoạn quan tươi cười hớn hở, hứa hẹn sẽ che chở bạn trước thánh thượng.");
        }
        else if (code == "scholar")
        {
            ShiftCourtFactionStanding("scholar", boost, "Tài trợ danh sĩ Thái Học");
            ShiftCourtFactionStanding("eunuch", -Math.Max(2, boost / 3), "Hoạn quan ghen ghét danh sĩ");
            SetState("reputation", Reputation + 2);
            return CourtResult.Success($"Tài trợ {copperAmount:N0} Quan cho văn sĩ Thái Học viện (+{boost} Điểm Sĩ Tộc, +2 Danh vọng)! Thanh lưu sĩ phu ngợi ca phong thái nho nhã, đức độ liêm khiết.");
        }
        else if (code == "militarist")
        {
            ShiftCourtFactionStanding("militarist", boost, "Ủng hộ quân giới biên cương");
            SetState("reputation", Reputation + 1);
            return CourtResult.Success($"Ủng hộ {copperAmount:N0} Quan khao quân tướng soái biên ải (+{boost} Điểm Quân Phái)! Các tướng lĩnh tiền tuyến cảm phục nghĩa khí, sẵn sàng tương trợ khi lâm chiến.");
        }

        return CourtResult.Failure($"Không tìm thấy thế lực triều đình mã '{factionCode}'.");
    }

    public CourtResult TriggerImperialInspection()
    {
        var cityId = GovernorCityId > 0 ? GovernorCityId : 1;
        var inspectorName = (_clock.Today.DayIndex % 3) switch
        {
            0 => "Ngự Sử Trung Thừa Hoàng Phủ Tung",
            1 => "Giám Sát Ngự Sử Tả Phong",
            _ => "Khâm Sai Thị Lang Tuân Úc"
        };

        return CourtResult.Success($"Ngự Sử Đài vừa hạ chiếu cử 【{inspectorName}】 giáng lâm thanh sát địa hạt của bạn! Hãy mau chóng chuẩn bị đối sách nghênh tiếp.");
    }

    public ImperialInspectionResult RespondToImperialInspection(string approachCode)
    {
        var approach = approachCode.ToUpperInvariant();
        var cityId = GovernorCityId > 0 ? GovernorCityId : 1;
        var today = _clock.Today.DayIndex;

        var inspectorName = (today % 3) switch
        {
            0 => "Ngự Sử Trung Thừa Hoàng Phủ Tung",
            1 => "Giám Sát Ngự Sử Tả Phong",
            _ => "Khâm Sai Thị Lang Tuân Úc"
        };

        // Query current city unrest if available
        var unrest = (int)_db.ScalarLong("SELECT COALESCE(unrest_level, 20) FROM po_city_governance WHERE citadel_id = @c", DbArgs.One("@c", cityId));

        if (approach == "OPEN_LEDGER")
        {
            if (unrest <= 35)
            {
                var merits = 20;
                AddMerits(merits);
                SetState("reputation", Reputation + 5);
                ShiftCourtFactionStanding("scholar", 15, "Thanh tra minh bạch khen ngợi");

                var narrative = $"Mở toang sổ sách nha môn, công khai thuế khóa lương thảo trước 【{inspectorName}】! Khâm Sai tấm tắc khen ngợi địa hạt trị an thanh bình, liêm khiết ái dân (+{merits} Huân công, +5 Danh vọng, +15 Điểm Sĩ Tộc)!";
                RecordImperialInspectionLog(today, cityId, inspectorName, "OPEN_LEDGER", "PRAISED", merits, 0, narrative);

                return new ImperialInspectionResult(true, "PRAISED", merits, 0, narrative);
            }
            else
            {
                var fine = 300L;
                AddMerits(-10);
                ShiftCourtFactionStanding("scholar", -10, "Bất ổn dân tình bị khiển trách");
                if (_wallet.Liquid > 0)
                {
                    var actualFine = Math.Min(_wallet.Liquid, fine);
                    _wallet.Charge(actualFine, JournalCodes.CourtStipend, "Phạt vạ thanh tra yếu kém");
                }

                var narrative = $"Khâm Sai 【{inspectorName}】 kiểm tra sổ sách phát hiện dân tình oán thán (Bất ổn {unrest}%), nha môn cai trị thiếu nghiêm cẩn. Triều đình nghiêm khắc khiển trách: giáng -10 Huân công, phạt lương 300 Quan!";
                RecordImperialInspectionLog(today, cityId, inspectorName, "OPEN_LEDGER", "REPRIMANDED", -10, -fine, narrative);

                return new ImperialInspectionResult(false, "REPRIMANDED", -10, -fine, narrative);
            }
        }
        else if (approach == "PERSUADE")
        {
            int pol = _attributes?.Attributes.Pol ?? 50;
            int intel = _attributes?.Attributes.Int ?? 50;
            bool success = (pol + intel) / 2 >= 50;

            if (success)
            {
                var merits = 12;
                AddMerits(merits);
                ShiftCourtFactionStanding("scholar", 8, "Khẩu tài biện giải");

                var narrative = $"Vận dụng tài hùng biện và mưu lược uyên bác, bạn đã phân tích cặn kẽ khó khăn do thời thế loạn lạc. Khâm Sai 【{inspectorName}】 cảm phục tài trí, gật đầu chuẩn y phê duyệt (+{merits} Huân công)!";
                RecordImperialInspectionLog(today, cityId, inspectorName, "PERSUADE", "PASSED", merits, 0, narrative);

                return new ImperialInspectionResult(true, "PASSED", merits, 0, narrative);
            }
            else
            {
                var fine = 200L;
                AddMerits(-5);
                if (_wallet.Liquid > 0)
                {
                    _wallet.Charge(Math.Min(_wallet.Liquid, fine), JournalCodes.CourtStipend, "Phạt vạ giảo biện trước khâm sai");
                }

                var narrative = $"Lời lẽ giảo biện vụng về không qua mắt được Khâm Sai lão luyện 【{inspectorName}】. Triều đình khiển trách phạt cảnh cáo 200 Quan (-5 Huân công)!";
                RecordImperialInspectionLog(today, cityId, inspectorName, "PERSUADE", "REPRIMANDED", -5, -fine, narrative);

                return new ImperialInspectionResult(false, "REPRIMANDED", -5, -fine, narrative);
            }
        }
        else if (approach == "BRIBE")
        {
            var bribeCost = 400L;
            if (_wallet.Liquid < bribeCost)
            {
                AddMerits(-25);
                ShiftCourtFactionStanding("scholar", -25, "Mưu toan hối lộ bị phanh phui");
                var narrative = $"Định ngầm đút lót 【{inspectorName}】 nhưng ngân lượng không đủ! Khâm Sai nổi trận lôi đình, lập tức dâng biểu đàn hặc tội mưu toan hối lộ (-25 Huân công, danh dự hoen ố)!";
                RecordImperialInspectionLog(today, cityId, inspectorName, "BRIBE", "IMPEACHED", -25, 0, narrative);

                return new ImperialInspectionResult(false, "IMPEACHED", -25, 0, narrative);
            }

            _wallet.Charge(bribeCost, JournalCodes.CourtStipend, "Hối lộ ngầm khâm sai đại thần");
            var merits = 15;
            AddMerits(merits);
            ShiftCourtFactionStanding("eunuch", 20, "Ăn ý chia chác cùng phe cánh");
            ShiftCourtFactionStanding("scholar", -15, "Gian trá đút lót");

            var praisedNarrative = $"Kín đáo dâng rương vàng ngọc 400 Quan cho 【{inspectorName}】. Khâm Sai tươi cười hoan hỉ, xóa sạch tì vết và tấu về kinh phong thưởng địa hạt thái bình (+{merits} Huân công, +20 Điểm Hoạn Quan)!";
            RecordImperialInspectionLog(today, cityId, inspectorName, "BRIBE", "PRAISED", merits, -bribeCost, praisedNarrative);

            return new ImperialInspectionResult(true, "PRAISED", merits, -bribeCost, praisedNarrative);
        }

        return new ImperialInspectionResult(false, "INVALID", 0, 0, "Đối sách giải trình không hợp lệ (hỗ trợ: OPEN_LEDGER, PERSUADE, BRIBE).");
    }

    private void RecordImperialInspectionLog(long dayIndex, long citadelId, string inspectorName, string approach, string outcome, int meritDelta, long copperDelta, string narrative)
    {
        _db.Execute(
            """
            INSERT INTO po_imperial_inspection_log (day_index, citadel_id, inspector_name_vi, approach_code, outcome, merit_delta, copper_delta, narrative_vi)
            VALUES (@day, @c, @name, @app, @out, @merit, @cop, @nar)
            """,
            DbArgs.One("@day", dayIndex)
                .Add("@c", citadelId)
                .Add("@name", inspectorName)
                .Add("@app", approach)
                .Add("@out", outcome)
                .Add("@merit", meritDelta)
                .Add("@cop", copperDelta)
                .Add("@nar", narrative));
    }

    public IReadOnlyList<ImperialInspectionLogView> RecentImperialInspections(int limit = 5)
    {
        return _db.Query(
            """
            SELECT id, day_index, citadel_id, inspector_name_vi, approach_code, outcome, merit_delta, copper_delta, narrative_vi
            FROM po_imperial_inspection_log
            ORDER BY id DESC
            LIMIT @limit
            """,
            r => new ImperialInspectionLogView(
                r.GetInt64(0),
                r.GetInt64(1),
                r.GetInt64(2),
                r.GetString(3),
                r.GetString(4),
                r.GetString(5),
                r.GetInt32(6),
                r.GetInt64(7),
                r.GetString(8)),
            DbArgs.One("@limit", limit));
    }

    public void SimulateMonthlyCourtIntrigue(GameDate date)
    {
        if (OfficeId <= 0)
        {
            return;
        }

        var standings = CourtFactionStandings;
        foreach (var fac in standings)
        {
            if (fac.FactionCode == "eunuch")
            {
                if (fac.StandingScore >= 50)
                {
                    _wallet.Pay(60, JournalCodes.CourtStipend, "Lộc ngầm nội thị Thập Thường Thị chia chác", $"court.eunuch.perk:{GameDateCodec.Format(date)}");
                }
                else if (fac.StandingScore <= -40)
                {
                    SetState("reputation", Math.Max(0, Reputation - 2));
                }
            }
            else if (fac.FactionCode == "scholar")
            {
                if (fac.StandingScore >= 50)
                {
                    SetState("reputation", Reputation + 2);
                    AddMerits(5);
                }
                else if (fac.StandingScore <= -40)
                {
                    SetState("reputation", Math.Max(0, Reputation - 3));
                }
            }
            else if (fac.FactionCode == "militarist")
            {
                if (fac.StandingScore >= 50)
                {
                    AddMerits(8);
                }
                else if (fac.StandingScore <= -40)
                {
                    AddMerits(-5);
                }
            }
        }

        // Quarterly surprise inspection when local unrest is high
        if (date.Month % 3 == 0)
        {
            var cityId = GovernorCityId > 0 ? GovernorCityId : 1;
            var unrest = (int)_db.ScalarLong("SELECT COALESCE(unrest_level, 20) FROM po_city_governance WHERE citadel_id = @c", DbArgs.One("@c", cityId));
            var scholarScore = standings.FirstOrDefault(s => s.FactionCode == "scholar")?.StandingScore ?? 0;

            if (unrest > 45 || scholarScore <= -40)
            {
                RespondToImperialInspection("OPEN_LEDGER");
            }
        }
    }

    internal void OnMarketCornered(long citadelId, int severity)
    {
        var factionId = _db.ScalarLong("SELECT COALESCE(faction_id, 1) FROM po_city_governance WHERE citadel_id = @c", DbArgs.One("@c", citadelId));
        if (factionId <= 0) factionId = 1;

        var scrutinyDelta = Math.Clamp(severity * 15, 15, 45);
        _db.Execute(
            """
            INSERT INTO po_warlord_scrutiny (faction_id, scrutiny_score, last_levy_day, embargo_until_day)
            VALUES (@f, @s, 0, 0)
            ON CONFLICT(faction_id) DO UPDATE SET
                scrutiny_score = MIN(100, po_warlord_scrutiny.scrutiny_score + @s)
            """,
            DbArgs.One("@f", factionId).Add("@s", scrutinyDelta));

        ShiftCourtFactionStanding("scholar", -10 * severity, "Đầu cơ lũng đoạn chợ búa làm khổ bá tánh");
        ShiftCourtFactionStanding("eunuch", 5, "Gian thương thao túng giá cả");
    }

    internal void OnContrabandDelivered(long citadelId, int quantity)
    {
        var factionId = _db.ScalarLong("SELECT COALESCE(faction_id, 1) FROM po_city_governance WHERE citadel_id = @c", DbArgs.One("@c", citadelId));
        if (factionId <= 0) factionId = 1;

        _db.Execute(
            """
            INSERT INTO po_warlord_scrutiny (faction_id, scrutiny_score, last_levy_day, embargo_until_day)
            VALUES (@f, 0, 0, 0)
            ON CONFLICT(faction_id) DO UPDATE SET
                scrutiny_score = MAX(0, po_warlord_scrutiny.scrutiny_score - 15)
            """,
            DbArgs.One("@f", factionId));

        ShiftCourtFactionStanding("militarist", 10, "Phá vòng vây tiếp tế nhu yếu phẩm tiền tuyến");
        AddMerits(Math.Max(5, quantity / 10));
    }
}
