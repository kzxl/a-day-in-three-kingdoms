using SimT.Contracts;
using SimT.Contracts.Data;
using SimT.Contracts.Modules;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Economy;

internal sealed partial class ProfessionService
{
    private void AddGuildFame(int fame) =>
        _db.Execute("UPDATE pf_guild_fame SET total_fame = total_fame + @fame WHERE id = 1", DbArgs.One("@fame", fame));

    public ProfessionProgressionDef? GetProgression(long tradeId)
    {
        var trade = Trades.FirstOrDefault(t => t.Id == tradeId);
        if (trade is null) return null;

        var skillTier = Tier(tradeId);
        var practiceCount = PracticeCount(tradeId);

        var skillTitle = skillTier switch
        {
            0 => "Tập sự (Học việc)",
            1 => "Sơ nhập (Thạo nghề)",
            2 => "Thục luyện (Lành nghề)",
            3 => "Tinh thông (Điêu luyện)",
            4 => "Đăng phong (Kỳ tài)",
            5 => "Hóa cảnh (Tông sư)",
            _ => "Vô danh"
        };
        var efficiencyBonus = skillTier * 15;

        // Branch 2: Mastery
        var masterpieceRecipes = Recipes.Where(r => r.TradeId == tradeId && r.RequiredTier >= 3).ToList();
        var masteryLevel = masterpieceRecipes.Count;
        var canCraftMasterpiece = skillTier >= 3;
        var masteryRank = skillTier switch
        {
            < 2 => "Thợ Mới",
            < 4 => "Thợ Giỏi",
            < 5 => "Đại Sư",
            _ => "Tông Sư Xuất Trần"
        };

        // Branch 3: Enterprise
        var workshops = AllWorkshops().Where(w => w.TradeId == tradeId && w.IsActive).ToList();
        var workshopCount = workshops.Count;
        var totalArtisans = workshops.Sum(w => w.HiredArtisans);
        var totalOutput = workshops.Sum(w => w.HiredArtisans * (trade.BaseQuantity + Math.Max(0, w.FurnaceLevel - 1)));

        var enterpriseScale = totalArtisans switch
        {
            0 => "Tiểu Hộ Cá Thể",
            < 5 => "Tiểu Tác Phường (Phân xưởng)",
            < 10 => "Đại Xưởng Xã (Thương xí)",
            _ => "Cự Đầu Công Nghiệp"
        };

        // Hegemony eligibility
        var isHegemony = skillTier >= 5 && (workshops.Any(w => w.FurnaceLevel >= 2) || trade.Code is "nong_phu" or "duoc_su" or "tho_ren" or "luyen_kim");
        var hegemonyTitle = trade.Code switch
        {
            "luyen_kim" or "tho_ren" => "Thiết Tượng Trấn Quốc",
            "nong_phu" => "Thần Nông Khai Hoang",
            "duoc_su" => "Y Thánh Cứu Thế",
            _ => "Tông Sư Hóa Cảnh"
        };
        var hegemonyAction = trade.Code switch
        {
            "luyen_kim" or "tho_ren" => "Độc quyền quân giới triều đình (Tăng +20 vũ khí võ khố, +500 Quan)",
            "nong_phu" => "Đại công trình nông giang (Tăng +10 Phồn vinh, dập cơ hoang)",
            "duoc_su" => "Bách thảo cứu tế dịch bệnh (Cứu tế 500 dân, +5 Phồn Vinh)",
            _ => "Khai tông lập phái"
        };

        return new ProfessionProgressionDef(
            trade.Id,
            trade.Code,
            trade.NameVi,
            skillTier,
            practiceCount,
            skillTitle,
            efficiencyBonus,
            masteryLevel,
            masteryRank,
            masterpieceRecipes.Count,
            canCraftMasterpiece,
            workshopCount,
            totalArtisans,
            totalOutput,
            enterpriseScale,
            isHegemony,
            hegemonyTitle,
            hegemonyAction);
    }

    public IReadOnlyList<ProfessionProgressionDef> AllProgressions() =>
        Trades.Select(t => GetProgression(t.Id)).Where(p => p != null).Select(p => p!).ToList();

    public IReadOnlyList<HegemonyActionDef> AvailableHegemonyActions(long citadelId)
    {
        var actions = new List<HegemonyActionDef>();

        // 1. Blacksmith / Smelter Hegemony
        var smelterTrade = Trades.FirstOrDefault(t => t.Code is "luyen_kim" or "tho_ren");
        var smelterTier = smelterTrade is not null ? Tier(smelterTrade.Id) : 0;
        var hasForge = AllWorkshops().Any(w => w.CitadelId == citadelId && w.IsActive && w.FurnaceLevel >= 2);
        var armoryEnactedThisMonth = _db.ScalarLong(
            "SELECT COUNT(*) FROM pf_hegemony_enactment WHERE citadel_id = @citadelId AND hegemony_code = 'IMPERIAL_ARMORY' AND enacted_day_index >= @monthStart",
            new DbArgs().Add("@citadelId", citadelId).Add("@monthStart", _clock.Today.DayIndex - 30)) > 0;

        actions.Add(new HegemonyActionDef(
            "IMPERIAL_ARMORY",
            "Độc Quyền Quân Giới Triều Đình",
            "Ký khế ước độc quyền cung ứng binh khí giáp trụ cho quân trấn, bổ sung +20 vũ khí cho võ khố và nhận 500 Quan bổng lộc triều đình.",
            "luyen_kim",
            5,
            0,
            smelterTier >= 5 && hasForge && !armoryEnactedThisMonth,
            smelterTier < 5 ? "Cần Luyện Kim Tier 5" : (!hasForge ? "Cần Lò Luyện cấp 2+ tại thành này" : (armoryEnactedThisMonth ? "Đã cung ứng tháng này" : "Khả dụng"))));

        // 2. Agriculture Hegemony
        var farmerTrade = Trades.FirstOrDefault(t => t.Code == "nong_phu");
        var farmerTier = farmerTrade is not null ? Tier(farmerTrade.Id) : 0;
        var irrigationEnacted = _db.ScalarLong(
            "SELECT COUNT(*) FROM pf_hegemony_enactment WHERE citadel_id = @citadelId AND hegemony_code = 'GRAND_IRRIGATION'",
            DbArgs.One("@citadelId", citadelId)) > 0;

        actions.Add(new HegemonyActionDef(
            "GRAND_IRRIGATION",
            "Đại Khai Nông Giang Thủy Lợi",
            "Chỉ đạo đào kênh dẫn nước, bồi đắp phù sa toàn châu quận: Tăng +10 điểm Phồn Vinh, xóa bỏ nạn đói và bơm +50 lương thảo vào kho chợ.",
            "nong_phu",
            5,
            600,
            farmerTier >= 5 && !irrigationEnacted && _wallet.Liquid >= 600,
            farmerTier < 5 ? "Cần Nông Phu Tier 5" : (irrigationEnacted ? "Đã hoàn thành đại công trình tại châu này" : (_wallet.Liquid < 600 ? "Thiếu ngân lượng (cần 600 Quan)" : "Khả dụng"))));

        // 3. Apothecary Hegemony
        var apothecaryTrade = Trades.FirstOrDefault(t => t.Code == "duoc_su");
        var apothecaryTier = apothecaryTrade is not null ? Tier(apothecaryTrade.Id) : 0;
        var reliefEnactedThisMonth = _db.ScalarLong(
            "SELECT COUNT(*) FROM pf_hegemony_enactment WHERE citadel_id = @citadelId AND hegemony_code = 'PLAGUE_RELIEF' AND enacted_day_index >= @monthStart",
            new DbArgs().Add("@citadelId", citadelId).Add("@monthStart", _clock.Today.DayIndex - 30)) > 0;

        actions.Add(new HegemonyActionDef(
            "PLAGUE_RELIEF",
            "Bách Thảo Cứu Tế Dịch Bệnh",
            "Phát tán linh đan diệt trừ dịch lệ, dập tắt cơ hoang: Cứu sống 500 dân tị nạn, tăng +5 Phồn Vinh và thu phục lòng người.",
            "duoc_su",
            5,
            300,
            apothecaryTier >= 5 && !reliefEnactedThisMonth && _wallet.Liquid >= 300,
            apothecaryTier < 5 ? "Cần Dược Sư Tier 5" : (reliefEnactedThisMonth ? "Đã cứu tế dịch bệnh tháng này" : (_wallet.Liquid < 300 ? "Thiếu ngân lượng (cần 300 Quan)" : "Khả dụng"))));

        return actions;
    }

    public WorkResult EnactHegemony(long citadelId, string hegemonyCode)
    {
        var available = AvailableHegemonyActions(citadelId).FirstOrDefault(a => a.Code == hegemonyCode);
        if (available is null)
        {
            return WorkResult.Fail("Quyền năng bá quyền không tồn tại.");
        }

        if (!available.IsAvailable)
        {
            return WorkResult.Fail($"Chưa đủ điều kiện thi triển: {available.StatusSummary}");
        }

        var today = _clock.Today;
        var resultMessage = string.Empty;

        _db.Transaction(() =>
        {
            switch (hegemonyCode)
            {
                case "IMPERIAL_ARMORY":
                {
                    _wallet.Pay(500, "professions.hegemony", "Bổng lộc độc quyền quân giới triều đình");
                    _db.Execute(
                        """
                        UPDATE ml_citadel_armory 
                        SET weapons_stock = MIN(weapons_capacity, weapons_stock + 20),
                            last_status_vi = 'Độc quyền quân giới triều đình gia cố võ khố'
                        WHERE citadel_id = @citadel
                        """,
                        DbArgs.One("@citadel", citadelId));

                    AddGuildFame(30);
                    _db.Execute(
                        "INSERT INTO pf_hegemony_enactment(citadel_id, trade_id, hegemony_code, enacted_day_index, summary_vi) VALUES(@cit, 4, 'IMPERIAL_ARMORY', @day, 'Độc quyền quân giới triều đình (+20 binh giới võ khố, +500 Quan)')",
                        new DbArgs().Add("@cit", citadelId).Add("@day", today.DayIndex));

                    resultMessage = "Thi triển Bá Quyền Thiết Tượng: Độc quyền rèn giáp trụ cho quân đoàn thành trì, tăng +20 vũ khí võ khố, nhận 500 Quan bổng lộc triều đình!";
                    break;
                }

                case "GRAND_IRRIGATION":
                {
                    _wallet.Charge(available.CostCopper, "professions.hegemony", "Chi phí đại công trình thủy lợi nông giang");
                    _db.Execute(
                        """
                        UPDATE po_city_demographics 
                        SET prosperity_score = MIN(100, prosperity_score + 10),
                            starvation_months = 0,
                            famine_status_vi = 'Thủy lợi đại khai, ngũ cốc phong đăng'
                        WHERE citadel_id = @citadel
                        """,
                        DbArgs.One("@citadel", citadelId));

                    // Inject grain into city market
                    _db.Execute(
                        "UPDATE mk_stall_listing SET stock = stock + 50 WHERE citadel_id = @citadel AND item_id = 1",
                        DbArgs.One("@citadel", citadelId));

                    AddGuildFame(35);
                    _db.Execute(
                        "INSERT INTO pf_hegemony_enactment(citadel_id, trade_id, hegemony_code, enacted_day_index, summary_vi) VALUES(@cit, 1, 'GRAND_IRRIGATION', @day, 'Đại công trình thủy lợi (+10 Phồn Vinh, dập cơ hoang, +50 gạo)')",
                        new DbArgs().Add("@cit", citadelId).Add("@day", today.DayIndex));

                    resultMessage = "Thi triển Bá Quyền Thần Nông: Đại khai nông giang thủy lợi thành công! Tăng +10 Phồn Vinh, dập tắt cơ hoang, bổ sung +50 lương thảo vào kho chợ!";
                    break;
                }

                case "PLAGUE_RELIEF":
                {
                    _wallet.Charge(available.CostCopper, "professions.hegemony", "Chi phí bách thảo cứu tế dịch bệnh");
                    _db.Execute(
                        """
                        UPDATE po_city_demographics 
                        SET population = population + 500,
                            prosperity_score = MIN(100, prosperity_score + 5),
                            famine_status_vi = 'Y thánh tế thế, dịch lệ tiêu tan'
                        WHERE citadel_id = @citadel
                        """,
                        DbArgs.One("@citadel", citadelId));

                    AddGuildFame(40);
                    var apothecaryId = Trades.FirstOrDefault(t => t.Code == "duoc_su")?.Id ?? 5;
                    _db.Execute(
                        "INSERT INTO pf_hegemony_enactment(citadel_id, trade_id, hegemony_code, enacted_day_index, summary_vi) VALUES(@cit, @tid, 'PLAGUE_RELIEF', @day, 'Bách thảo cứu tế dịch bệnh (+5 phồn vinh, cứu 500 dân)')",
                        new DbArgs().Add("@cit", citadelId).Add("@tid", apothecaryId).Add("@day", today.DayIndex));

                    resultMessage = "Thi triển Bá Quyền Y Thánh: Phổ tế Bách Thảo Linh Đơn dập tắt dịch bệnh, tăng 5 điểm phồn vinh và cứu sống 500 sinh linh bá tánh!";
                    break;
                }

                default:
                    throw new SimException("professions.hegemony_unknown", $"Mã bá quyền không hợp lệ: {hegemonyCode}");
            }
        });

        return WorkResult.Success(resultMessage);
    }
}
