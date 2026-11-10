using System.Data;
using SimT.Contracts;
using SimT.Contracts.Data;
using SimT.Contracts.Ledger;
using SimT.Contracts.Playing;

namespace SimT.Modules.Dlc.Power;

internal sealed partial class CourtService
{
    private static readonly (string Code, string TitleVi, string DescVi, (string Code, string LabelVi)[] Choices)[] CrisisCatalog =
    [
        ("CRISIS_FLOOD", "Thủy Nạn Vỡ Đê", "Mưa bão dài ngày khiến mực nước sông dâng cao, uy hiếp mùa màng và nhà cửa ngoại thành.",
            [("DISPATCH_CIVIL", "Trưởng Thừa đốc thúc dân phu đắp đê (80q khố)"), ("EMERGENCY_FUNDS", "Thái Thú trích 150q phát chẩn (+15 công trạng, +2 uy tín)")]),
        ("CRISIS_PLAGUE", "Dịch Khí Hoành Hành", "Thời khí chuyển mùa sinh dịch bệnh lan truyền, lòng dân hoang mang lo sợ.",
            [("ESTABLISH_CLINIC", "Lập dược xá cứu tế miễn phí (60q khố)"), ("QUARANTINE", "Phong tỏa nghiêm ngặt các cổng thành")]),
        ("CRISIS_BANDIT_RAID", "Sơn Tặc Đột Kích Thôn Trang", "Toán cướp núi lợi dụng đêm tối kéo đến cướp bóc lương thảo các thôn trang lân cận.",
            [("DISPATCH_MILITARY", "Đô Úy dẫn đồn binh xuất kích diệt phỉ (15 vk)"), ("DEFEND_WALLS", "Thu quân đóng chặt cửa thành bảo vệ bách tính")]),
        ("CRISIS_SECRET_ENVOY", "Mật Sứ Chư Hầu Chiêu Dụ", "Sứ giả địch phái người mang theo rương hoàng kim lẻn vào thành dâng thư chiêu dụ.",
            [("BEHEAD_ENVOY", "Trảm sứ thị chúng, nộp thủ cấp lên minh chủ (+20 tín nhiệm, +40 công)"), ("CONFISCATE", "Tịch thu 300 Quan hoàng kim sung quỹ nha môn")])
    ];

    public IReadOnlyList<FiefCrisisView> GetActiveFiefCrises(long citadelId = 0)
    {
        var cityId = citadelId > 0 ? citadelId : GovernorCityId;
        if (cityId <= 0) return Array.Empty<FiefCrisisView>();

        return _db.Query(
            """
            SELECT id, citadel_id, crisis_code, title_vi, description_vi, severity, status, created_day, resolved_day, resolution_choice, outcome_vi
            FROM po_fief_crisis
            WHERE citadel_id = @citId AND status = 'PENDING'
            ORDER BY id DESC
            """,
            MapCrisis,
            DbArgs.One("@citId", cityId));
    }

    public IReadOnlyList<FiefCrisisView> GetRecentFiefCrises(int limit = 5)
    {
        var cityId = GovernorCityId;
        if (cityId <= 0) return Array.Empty<FiefCrisisView>();

        return _db.Query(
            """
            SELECT id, citadel_id, crisis_code, title_vi, description_vi, severity, status, created_day, resolved_day, resolution_choice, outcome_vi
            FROM po_fief_crisis
            WHERE citadel_id = @citId
            ORDER BY id DESC LIMIT @lim
            """,
            MapCrisis,
            new DbArgs().Add("@citId", cityId).Add("@lim", limit));
    }

    private static FiefCrisisView MapCrisis(DbRecord r) => new(
        r.GetInt64(0),
        r.GetInt64(1),
        r.GetString(2),
        r.GetString(3),
        r.GetString(4),
        r.GetInt32(5),
        r.GetString(6),
        r.GetInt32(7),
        r.GetInt32(8),
        r.GetString(9),
        r.GetString(10));

    public CourtResult TriggerEmergentFiefCrisis()
    {
        var cityId = GovernorCityId;
        if (cityId <= 0) return CourtResult.Failure("Bạn chưa cai quản phong ấp nào.");

        var active = GetActiveFiefCrises(cityId);
        if (active.Count > 0)
        {
            return CourtResult.Failure($"Hiện phong ấp đang có biến cố '{active[0].TitleVi}' chờ xử lý.");
        }

        var idx = (int)(_clock.Today.DayIndex % CrisisCatalog.Length);
        var def = CrisisCatalog[idx];

        _db.Execute(
            """
            INSERT INTO po_fief_crisis (citadel_id, crisis_code, title_vi, description_vi, severity, status, created_day)
            VALUES (@citId, @code, @title, @desc, 1, 'PENDING', @day)
            """,
            new DbArgs()
                .Add("@citId", cityId)
                .Add("@code", def.Code)
                .Add("@title", def.TitleVi)
                .Add("@desc", def.DescVi)
                .Add("@day", _clock.Today.DayIndex));

        return CourtResult.Success($"🚨 BIẾN CỐ PHONG ẤP BÙNG PHÁT: {def.TitleVi} — {def.DescVi}");
    }

    public CourtResult ResolveFiefCrisis(long crisisId, string choiceCode)
    {
        var cityId = GovernorCityId;
        if (cityId <= 0) return CourtResult.Failure("Bạn chưa cai quản phong ấp nào.");

        var crisis = _db.Query(
            "SELECT id, crisis_code, title_vi FROM po_fief_crisis WHERE id = @id AND citadel_id = @citId AND status = 'PENDING'",
            r => (Id: r.GetInt64(0), Code: r.GetString(1), Title: r.GetString(2)),
            new DbArgs().Add("@id", crisisId).Add("@citId", cityId)).FirstOrDefault();

        if (crisis.Id == 0) return CourtResult.Failure("Biến cố không tồn tại hoặc đã được xử lý.");

        var choice = choiceCode.ToUpperInvariant();
        string outcome = "";

        _db.Transaction(() =>
        {
            switch (crisis.Code)
            {
                case "CRISIS_FLOOD":
                    if (choice == "DISPATCH_CIVIL")
                    {
                        _db.Execute("UPDATE po_governor_fief SET local_treasury = MAX(0, local_treasury - 80), agriculture_score = MIN(100, agriculture_score + 4) WHERE citadel_id = @citId", DbArgs.One("@citId", cityId));
                        outcome = "Trưởng Thừa chỉ huy dân phu đắp đê kịp thời, mùa màng bình yên vô sự (-80q ngân khố, Nông Khẩn +4).";
                        AddMerits(10);
                    }
                    else
                    {
                        if (_wallet.Liquid >= 150) _wallet.Charge(150, JournalCodes.CourtService, "Cứu tế thủy tai phong ấp");
                        _db.Execute("UPDATE po_city_governance SET unrest_level = MAX(0, unrest_level - 10) WHERE citadel_id = @citId", DbArgs.One("@citId", cityId));
                        SetState("reputation", Reputation + 2);
                        AddMerits(15);
                        outcome = "Thái Thú mở kho phát chẩn cứu mạng muôn dân, tiếng thơm đồn xa khắp cõi (+15 Huân công, +2 Uy tín, -10% Loạn).";
                    }
                    break;

                case "CRISIS_PLAGUE":
                    if (choice == "ESTABLISH_CLINIC")
                    {
                        _db.Execute("UPDATE po_governor_fief SET local_treasury = MAX(0, local_treasury - 60) WHERE citadel_id = @citId", DbArgs.One("@citId", cityId));
                        _db.Execute("UPDATE po_city_governance SET unrest_level = MAX(0, unrest_level - 8) WHERE citadel_id = @citId", DbArgs.One("@citId", cityId));
                        AddMerits(12);
                        outcome = "Lập dược xá cứu chữa kịp thời, dịch bệnh nhanh chóng tiêu tan (-60q ngân khố, -8% Loạn).";
                    }
                    else
                    {
                        _db.Execute("UPDATE po_city_governance SET unrest_level = MIN(100, unrest_level + 5) WHERE citadel_id = @citId", DbArgs.One("@citId", cityId));
                        outcome = "Giữ nghiêm giới nghiêm cách ly, ngăn dịch bệnh lây lan nhưng dân chúng có chút oán thán (+5% Loạn).";
                    }
                    break;

                case "CRISIS_BANDIT_RAID":
                    if (choice == "DISPATCH_MILITARY")
                    {
                        _db.Execute("UPDATE ml_citadel_armory SET weapons_stock = MAX(0, weapons_stock - 15) WHERE citadel_id = @citId", DbArgs.One("@citId", cityId));
                        _db.Execute("UPDATE po_governor_fief SET local_treasury = local_treasury + 120, order_score = MIN(100, order_score + 6) WHERE citadel_id = @citId", DbArgs.One("@citId", cityId));
                        _db.Execute("UPDATE po_city_governance SET unrest_level = MAX(0, unrest_level - 10) WHERE citadel_id = @citId", DbArgs.One("@citId", cityId));
                        AddMerits(20);
                        outcome = "Đô Úy dũng mãnh xuất trận chém tướng đoạt cờ, thu 120 Quan sung khố, dẹp sạch thổ phỉ (+20 Huân công, +6 Trị An).";
                    }
                    else
                    {
                        _db.Execute("UPDATE po_governor_fief SET agriculture_score = MAX(0, agriculture_score - 5) WHERE citadel_id = @citId", DbArgs.One("@citId", cityId));
                        outcome = "Cố thủ cổng thành bảo vệ dân cư, thôn ngoại ô bị cướp phá nhẹ (Nông Khẩn -5).";
                    }
                    break;

                case "CRISIS_SECRET_ENVOY":
                    if (choice == "BEHEAD_ENVOY")
                    {
                        if (AllegianceFactionId > 0)
                        {
                            _db.Execute("UPDATE po_lord_trust SET trust_level = MIN(100, trust_level + 20) WHERE faction_id = @facId", DbArgs.One("@facId", AllegianceFactionId));
                        }
                        AddMerits(40);
                        outcome = "Trảm sứ thị chúng, gửi thủ cấp về đại doanh minh chủ tỏ lòng dạ son sắt (+20 Tín nhiệm chúa công, +40 Huân công)!";
                    }
                    else
                    {
                        _db.Execute("UPDATE po_governor_fief SET local_treasury = local_treasury + 300 WHERE citadel_id = @citId", DbArgs.One("@citId", cityId));
                        outcome = "Tịch thu toàn bộ 300 Quan hoàng kim của sứ giả sung vào khố nha môn, đuổi sứ ra khỏi cõi (+300 Quan ngân khố).";
                    }
                    break;

                default:
                    outcome = "Quyết sách đã được ban bố và thực thi.";
                    break;
            }

            _db.Execute(
                """
                UPDATE po_fief_crisis
                SET status = 'RESOLVED', resolved_day = @day, resolution_choice = @ch, outcome_vi = @out
                WHERE id = @id
                """,
                new DbArgs().Add("@day", _clock.Today.DayIndex).Add("@ch", choice).Add("@out", outcome).Add("@id", crisisId));
        });

        return CourtResult.Success($"✅ XỬ LÝ HOÀN TẤT BIẾN CỐ: {outcome}");
    }

    public CourtResult SimulateCitadelSiege(string attackerName, int enemyTroops, string tacticalStance)
    {
        var cityId = GovernorCityId;
        if (cityId <= 0) return CourtResult.Failure("Bạn chưa cai quản phong ấp nào.");

        var fief = PlayerFief;
        if (fief is null) return CourtResult.Failure("Không tìm thấy thông tin phong ấp.");

        var armory = _db.Query("SELECT weapons_stock FROM ml_citadel_armory WHERE citadel_id = @citId", r => r.GetInt32(0), DbArgs.One("@citId", cityId)).FirstOrDefault();
        var garrison = fief.GarrisonTroops;
        var fort = fief.Fortification;
        var stance = tacticalStance.ToUpperInvariant();

        int defCasualties;
        int attCasualties;
        int spoils;
        int merits;
        string outcome;
        string phases;

        switch (stance)
        {
            case "FORTIFY_WALLS":
                phases = "Giai đoạn 1: Vạn tiễn tề phát từ mặt thành bẻ gãy mũi xung kích. Giai đoạn 2: Lăn đá đổ dầu sôi thiêu rụi thang dây công thành.";
                defCasualties = (int)(enemyTroops * 0.08);
                attCasualties = (int)(enemyTroops * 0.45);
                spoils = 180;
                merits = 30;
                outcome = "VICTORY";
                break;

            case "SALLY_OUT":
                phases = "Giai đoạn 1: Đô Úy mở cổng thành dẫn kỵ binh thiết kỵ đột kích sườn quân giặc. Giai đoạn 2: Bạch nhược chiến giáp lá cà chém rơi soái kỳ.";
                defCasualties = (int)(enemyTroops * 0.18);
                attCasualties = (int)(enemyTroops * 0.65);
                spoils = 380;
                merits = 45;
                outcome = "VICTORY";
                break;

            default: // AMBUSH_FIRE
                phases = "Giai đoạn 1: Mở cổng thành vờ bỏ chạy dụ quân giặc vào nội hào. Giai đoạn 2: Phóng hỏa tiễn nổ tung kho rơm, lửa thiêu ngút trời đại phá quân thù.";
                defCasualties = (int)(enemyTroops * 0.06);
                attCasualties = (int)(enemyTroops * 0.70);
                spoils = 450;
                merits = 50;
                outcome = "VICTORY";
                break;
        }

        var narrative = $"Đại phá {enemyTroops} quân {attackerName} công thành! Quân địch tháo chạy tán loạn, thu {spoils} Quan chiến lợi phẩm, nhận +{merits} Huân công.";

        _db.Transaction(() =>
        {
            _db.Execute(
                "UPDATE po_city_governance SET defense_strength = MAX(30, defense_strength - @cas), unrest_level = MAX(0, unrest_level - 15) WHERE citadel_id = @citId",
                new DbArgs().Add("@cas", defCasualties / 20).Add("@citId", cityId));
            _db.Execute(
                "UPDATE ml_citadel_armory SET weapons_stock = MAX(0, weapons_stock - 12) WHERE citadel_id = @citId",
                DbArgs.One("@citId", cityId));
            _db.Execute(
                "UPDATE po_governor_fief SET local_treasury = local_treasury + @s, order_score = MIN(100, order_score + 10) WHERE citadel_id = @citId",
                new DbArgs().Add("@s", spoils).Add("@citId", cityId));

            AddMerits(merits);

            _db.Execute(
                """
                INSERT INTO po_fief_siege_log (citadel_id, day_index, attacker_faction_name, attacker_troops, defense_troops, tactical_stance, outcome, defense_casualties, attacker_casualties, spoils_copper, merits_earned, battle_phases_vi, narrative_vi)
                VALUES (@citId, @day, @att, @atTr, @defTr, @st, @out, @defCas, @attCas, @sp, @mer, @ph, @narr)
                """,
                new DbArgs()
                    .Add("@citId", cityId)
                    .Add("@day", _clock.Today.DayIndex)
                    .Add("@att", attackerName)
                    .Add("@atTr", enemyTroops)
                    .Add("@defTr", garrison)
                    .Add("@st", stance)
                    .Add("@out", outcome)
                    .Add("@defCas", defCasualties)
                    .Add("@attCas", attCasualties)
                    .Add("@sp", spoils)
                    .Add("@mer", merits)
                    .Add("@ph", phases)
                    .Add("@narr", narrative));
        });

        return CourtResult.Success($"🛡️ ĐẠI THẮNG THỦ THÀNH: {narrative} ({phases})");
    }

    public IReadOnlyList<FiefSiegeLogView> RecentSiegeLogs(int limit = 5)
    {
        return _db.Query(
            """
            SELECT id, citadel_id, day_index, attacker_faction_name, attacker_troops, defense_troops, tactical_stance, outcome, defense_casualties, attacker_casualties, spoils_copper, merits_earned, battle_phases_vi, narrative_vi
            FROM po_fief_siege_log
            ORDER BY id DESC LIMIT @lim
            """,
            r => new FiefSiegeLogView(
                r.GetInt64(0),
                r.GetInt64(1),
                r.GetInt32(2),
                r.GetString(3),
                r.GetInt32(4),
                r.GetInt32(5),
                r.GetString(6),
                r.GetString(7),
                r.GetInt32(8),
                r.GetInt32(9),
                r.GetInt32(10),
                r.GetInt32(11),
                r.GetString(12),
                r.GetString(13)),
            DbArgs.One("@lim", limit));
    }
}
