using SimT.Contracts;
using SimT.Contracts.Data;
using SimT.Contracts.Ledger;
using SimT.Contracts.Playing;

namespace SimT.Modules.Dlc.Power;

internal sealed partial class CourtService
{
    private static readonly (string Code, string NameVi, string DescVi, int Cost)[] MegaprojectCatalog =
    [
        ("CANAL", "Đại Kênh Thủy Lợi", "Đào kênh dẫn nước tưới tiêu toàn cõi phong ấp. Nông Khẩn vĩnh viễn +15 điểm, tăng +1% dân số mỗi tháng.", 1500),
        ("ACADEMY", "Thái Học Viện", "Lập viện chiêu mộ hiền tài, truyền bá kinh điển. Đem lại +5 Huân công mỗi tháng và +2 Uy tín mỗi quý.", 2000),
        ("ARMORY", "Đại Dã Thần Binh Xưởng", "Xưởng đúc vũ khí quy mô lớn. Tăng sức chứa kho vũ khí +100 đơn vị và bảo đảm vũ khí phong phú.", 2500),
        ("BASTION", "Kim Thành Thang Trì", "Gia cố tường thành đá hoa cương, hào sâu hiểm trở. Thành phòng tăng vĩnh viễn +25 điểm.", 3000)
    ];

    public IReadOnlyList<FiefMegaprojectView> GetFiefMegaprojects(long citadelId = 0)
    {
        var cityId = citadelId > 0 ? citadelId : GovernorCityId;
        if (cityId <= 0) return Array.Empty<FiefMegaprojectView>();

        var existing = _db.Query(
            "SELECT project_code, status, invested_copper, required_copper, completed_day FROM po_fief_megaproject WHERE citadel_id = @citId",
            r => (Code: r.GetString(0), Status: r.GetString(1), Invested: r.GetInt32(2), Required: r.GetInt32(3), Day: r.GetInt32(4)),
            DbArgs.One("@citId", cityId)).ToDictionary(x => x.Code, x => x);

        var list = new List<FiefMegaprojectView>();
        foreach (var def in MegaprojectCatalog)
        {
            if (existing.TryGetValue(def.Code, out var row))
            {
                list.Add(new FiefMegaprojectView(def.Code, def.NameVi, def.DescVi, row.Status, row.Invested, row.Required, row.Day));
            }
            else
            {
                list.Add(new FiefMegaprojectView(def.Code, def.NameVi, def.DescVi, "NOT_STARTED", 0, def.Cost, 0));
            }
        }
        return list;
    }

    public CourtResult InvestFiefMegaproject(string projectCode, int copperAmount)
    {
        var cityId = GovernorCityId;
        if (cityId <= 0)
        {
            return CourtResult.Failure("Bạn chưa cai quản phong ấp nào để đầu tư đại công trình.");
        }

        var def = MegaprojectCatalog.FirstOrDefault(m => string.Equals(m.Code, projectCode, StringComparison.OrdinalIgnoreCase));
        if (def == default)
        {
            return CourtResult.Failure($"Mã công trình '{projectCode}' không hợp lệ.");
        }

        if (copperAmount <= 0)
        {
            return CourtResult.Failure("Số ngân lượng đầu tư phải lớn hơn 0.");
        }

        if (_wallet.Liquid < copperAmount)
        {
            return CourtResult.Failure($"Ngân sách cá nhân không đủ {copperAmount} Quan để tài trợ công trình (hiện có {_wallet.Liquid}).");
        }

        var projects = GetFiefMegaprojects(cityId);
        var current = projects.FirstOrDefault(p => string.Equals(p.ProjectCode, def.Code, StringComparison.OrdinalIgnoreCase));
        if (current is not null && current.IsCompleted)
        {
            return CourtResult.Failure($"Đại công trình '{def.NameVi}' đã hoàn thành tráng lệ, không cần đầu tư thêm.");
        }

        var currentInvested = current?.InvestedCopper ?? 0;
        var newInvested = currentInvested + copperAmount;
        var isNowCompleted = newInvested >= def.Cost;
        var status = isNowCompleted ? "COMPLETED" : "IN_PROGRESS";
        var completedDay = isNowCompleted ? _clock.Today.DayIndex : 0;

        _db.Transaction(() =>
        {
            _wallet.Charge(copperAmount, JournalCodes.CourtService, $"Tài trợ đại công trình {def.NameVi}");

            _db.Execute(
                """
                INSERT INTO po_fief_megaproject (citadel_id, project_code, status, invested_copper, required_copper, completed_day)
                VALUES (@citId, @code, @status, @inv, @req, @day)
                ON CONFLICT(citadel_id, project_code) DO UPDATE SET
                    status = excluded.status,
                    invested_copper = excluded.invested_copper,
                    completed_day = excluded.completed_day
                """,
                new DbArgs()
                    .Add("@citId", cityId)
                    .Add("@code", def.Code)
                    .Add("@status", status)
                    .Add("@inv", newInvested)
                    .Add("@req", def.Cost)
                    .Add("@day", completedDay));

            if (isNowCompleted)
            {
                AddMerits(50);
                ApplyMegaprojectCompletionBonus(cityId, def.Code);
            }
        });

        if (isNowCompleted)
        {
            return CourtResult.Success($"🎉 ĐẠI CÔNG TRÌNH KHÁNH THÀNH: {def.NameVi} tại phong ấp đã hoàn thành mỹ mãn! (+50 Huân công triều đình)");
        }

        var percent = Math.Min(100, (int)(newInvested * 100L / def.Cost));
        return CourtResult.Success($"Đã đầu tư {copperAmount} Quan vào {def.NameVi}. Tiến độ hiện tại: {percent}% ({newInvested}/{def.Cost} Quan).");
    }

    private void ApplyMegaprojectCompletionBonus(long cityId, string code)
    {
        switch (code)
        {
            case "CANAL":
                _db.Execute("UPDATE po_governor_fief SET agriculture_score = MIN(100, agriculture_score + 15) WHERE citadel_id = @citId", DbArgs.One("@citId", cityId));
                break;
            case "ARMORY":
                _db.Execute("UPDATE ml_citadel_armory SET weapons_capacity = weapons_capacity + 100, weapons_stock = weapons_stock + 50 WHERE citadel_id = @citId", DbArgs.One("@citId", cityId));
                break;
            case "BASTION":
                _db.Execute("UPDATE po_governor_fief SET fortification_score = MIN(100, fortification_score + 25) WHERE citadel_id = @citId", DbArgs.One("@citId", cityId));
                _db.Execute("UPDATE po_city_governance SET defense_strength = defense_strength + 20 WHERE citadel_id = @citId", DbArgs.One("@citId", cityId));
                break;
        }
    }

    public CourtResult LaunchFiefExpedition(string expeditionType)
    {
        var cityId = GovernorCityId;
        if (cityId <= 0)
        {
            return CourtResult.Failure("Bạn chưa cai quản phong ấp nào để hạ lệnh xuất quân viễn chinh.");
        }

        var fief = PlayerFief;
        if (fief is null)
        {
            return CourtResult.Failure("Không tìm thấy thông tin phong ấp.");
        }

        var gov = Governances.FirstOrDefault(g => g.CitadelId == cityId);
        var garrison = fief.GarrisonTroops;
        var armory = _db.Query(
            "SELECT weapons_stock FROM ml_citadel_armory WHERE citadel_id = @citId",
            r => r.GetInt32(0),
            DbArgs.One("@citId", cityId)).FirstOrDefault();

        var type = expeditionType.ToUpperInvariant();
        int reqTroops = 0;
        int reqWeapons = 0;
        int casualties = 0;
        int weaponsLost = 0;
        int spoils = 0;
        int merits = 0;
        string narrative = "";

        switch (type)
        {
            case "PACIFY_BANDITS":
            {
                reqTroops = 300;
                reqWeapons = 20;
                if (garrison < reqTroops) return CourtResult.Failure($"Cần tối thiểu {reqTroops} binh đồn trú (hiện có {garrison}).");
                if (armory < reqWeapons) return CourtResult.Failure($"Cần tối thiểu {reqWeapons} vũ khí trong kho binh giới (hiện có {armory}).");

                casualties = 22;
                weaponsLost = 6;
                spoils = 260;
                merits = 25;
                narrative = $"Xuất binh {reqTroops} tiễu trừ sơn tặc sào huyệt quanh châu quận. Đại thắng trở về, thu {spoils} Quan sung vào khố nha môn, giảm loạn -12%. Tổn thất {casualties} binh mã.";

                _db.Transaction(() =>
                {
                    _db.Execute(
                        "UPDATE po_city_governance SET unrest_level = MAX(0, unrest_level - 12), defense_strength = defense_strength - @cas WHERE citadel_id = @citId",
                        new DbArgs().Add("@cas", casualties / 15).Add("@citId", cityId));
                    _db.Execute(
                        "UPDATE ml_citadel_armory SET weapons_stock = MAX(0, weapons_stock - @w) WHERE citadel_id = @citId",
                        new DbArgs().Add("@w", weaponsLost).Add("@citId", cityId));
                    _db.Execute(
                        "UPDATE po_governor_fief SET local_treasury = local_treasury + @s, order_score = MIN(100, order_score + 8) WHERE citadel_id = @citId",
                        new DbArgs().Add("@s", spoils).Add("@citId", cityId));
                    AddMerits(merits);
                    RecordExpeditionLog(cityId, type, reqTroops, casualties, spoils, merits, narrative);
                });
                break;
            }

            case "BORDER_PATROL":
            {
                reqTroops = 400;
                reqWeapons = 20;
                if (garrison < reqTroops) return CourtResult.Failure($"Cần tối thiểu {reqTroops} binh đồn trú (hiện có {garrison}).");
                if (armory < reqWeapons) return CourtResult.Failure($"Cần tối thiểu {reqWeapons} vũ khí trong kho binh giới (hiện có {armory}).");

                casualties = 8;
                weaponsLost = 4;
                merits = 15;
                narrative = $"Thao diễn thao trường và phong tỏa các hẻm núi biên ải. Binh sĩ nhuần nhuyễn trận hình, Thành phòng +8 điểm, Uy tín +3. Tổn thất nhẹ {casualties} người.";

                _db.Transaction(() =>
                {
                    _db.Execute(
                        "UPDATE po_city_governance SET defense_strength = defense_strength + 6 WHERE citadel_id = @citId",
                        DbArgs.One("@citId", cityId));
                    _db.Execute(
                        "UPDATE ml_citadel_armory SET weapons_stock = MAX(0, weapons_stock - @w) WHERE citadel_id = @citId",
                        new DbArgs().Add("@w", weaponsLost).Add("@citId", cityId));
                    _db.Execute(
                        "UPDATE po_governor_fief SET fortification_score = MIN(100, fortification_score + 8) WHERE citadel_id = @citId",
                        DbArgs.One("@citId", cityId));
                    SetState("reputation", Reputation + 3);
                    AddMerits(merits);
                    RecordExpeditionLog(cityId, type, reqTroops, casualties, spoils, merits, narrative);
                });
                break;
            }

            case "SUPPORT_LORD":
            {
                if (AllegianceFactionId <= 0) return CourtResult.Failure("Bạn chưa quy thuận chư hầu minh chủ nào để chi viện tiền tuyến.");
                reqTroops = 800;
                reqWeapons = 40;
                if (garrison < reqTroops) return CourtResult.Failure($"Cần tối thiểu {reqTroops} binh mã hùng hậu để chi viện (hiện có {garrison}).");
                if (armory < reqWeapons) return CourtResult.Failure($"Cần tối thiểu {reqWeapons} vũ khí (hiện có {armory}).");

                casualties = 90;
                weaponsLost = 35;
                var reinforced = reqTroops - casualties;
                merits = 60;
                narrative = $"Điều động {reqTroops} đồn binh cấp tốc chi viện đại bản doanh của minh chủ. Gia nhập chiến đoàn tiền tuyến, chúa công hạ chiếu khen ngợi trung liệt (+60 Huân công)!";

                _db.Transaction(() =>
                {
                    _db.Execute(
                        "UPDATE po_city_governance SET defense_strength = MAX(20, defense_strength - @dec) WHERE citadel_id = @citId",
                        new DbArgs().Add("@dec", reqTroops / 15).Add("@citId", cityId));
                    _db.Execute(
                        "UPDATE ml_citadel_armory SET weapons_stock = MAX(0, weapons_stock - @w) WHERE citadel_id = @citId",
                        new DbArgs().Add("@w", weaponsLost).Add("@citId", cityId));
                    _db.Execute(
                        "UPDATE po_faction SET troop_count = troop_count + @reinf WHERE id = @facId",
                        new DbArgs().Add("@reinf", reinforced).Add("@facId", AllegianceFactionId));
                    _db.Execute(
                        "UPDATE po_lord_trust SET trust_level = MIN(100, trust_level + 15) WHERE faction_id = @facId",
                        DbArgs.One("@facId", AllegianceFactionId));
                    AddMerits(merits);
                    RecordExpeditionLog(cityId, type, reqTroops, casualties, spoils, merits, narrative);
                });
                break;
            }

            default:
                return CourtResult.Failure("Loại hình xuất quân không hợp lệ (hỗ trợ: PACIFY_BANDITS, BORDER_PATROL, SUPPORT_LORD).");
        }

        return CourtResult.Success($"⚔️ {narrative}");
    }

    private void RecordExpeditionLog(long citadelId, string type, int troops, int casualties, int spoils, int merits, string narrative)
    {
        _db.Execute(
            """
            INSERT INTO po_fief_expedition_log (citadel_id, day_index, expedition_type, troops_dispatched, casualties, spoils_copper, merits_earned, narrative_vi)
            VALUES (@citId, @day, @type, @tr, @cas, @sp, @mer, @narr)
            """,
            new DbArgs()
                .Add("@citId", citadelId)
                .Add("@day", _clock.Today.DayIndex)
                .Add("@type", type)
                .Add("@tr", troops)
                .Add("@cas", casualties)
                .Add("@sp", spoils)
                .Add("@mer", merits)
                .Add("@narr", narrative));
    }

    public IReadOnlyList<FiefExpeditionLogView> RecentExpeditionLogs(int limit = 5)
    {
        return _db.Query(
            """
            SELECT id, citadel_id, day_index, expedition_type, troops_dispatched, casualties, spoils_copper, merits_earned, narrative_vi
            FROM po_fief_expedition_log
            ORDER BY id DESC LIMIT @lim
            """,
            r => new FiefExpeditionLogView(
                r.GetInt64(0),
                r.GetInt64(1),
                r.GetInt32(2),
                r.GetString(3),
                r.GetInt32(4),
                r.GetInt32(5),
                r.GetInt32(6),
                r.GetInt32(7),
                r.GetString(8)),
            DbArgs.One("@lim", limit));
    }
}
