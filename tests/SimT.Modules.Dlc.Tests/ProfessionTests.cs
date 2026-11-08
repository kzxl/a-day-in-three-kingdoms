using SimT.Contracts.Playing;
using SimT.Core.Kernel;

namespace SimT.Modules.Dlc.Tests;

/// <summary>Day labour: a batch must cost days and tools, and practice must actually pay off.</summary>
public sealed class ProfessionTests : IDisposable
{
    private readonly DlcTestHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public void A_batch_costs_days_and_tools_and_yields_goods()
    {
        using var kernel = _host.Boot();
        var player = Player(kernel);
        var trade = Guild(kernel).Trades.First(candidate => candidate.Code == "nong_phu");
        var beforeBalance = kernel.Ledger.Balance;
        var beforeDate = kernel.Today;

        var message = Command(kernel, $"work:{trade.Id}");

        Assert.Contains("Nông Phu", message);
        Assert.Equal(beforeDate.AddDays(trade.DaysPerBatch), kernel.Today);
        Assert.Equal(beforeBalance - trade.ToolFee, kernel.Ledger.Balance);
        Assert.Equal(trade.BaseQuantity, Bag(kernel).Quantity(player.CurrentCityId, trade.OutputItemId));
        Assert.Equal(1, Guild(kernel).PracticeCount(trade.Id));
    }

    [Fact]
    public void A_batch_without_cash_is_refused_and_time_does_not_move()
    {
        using var kernel = _host.Boot();
        var before = kernel.Today;

        kernel.Ledger.Move(kernel.Today, kernel.Ledger.Liquid - 1, toSafe: true, Contracts.Ledger.JournalCodes.SafeDeposit, "cất két");

        var message = Command(kernel, "work:4");

        Assert.Contains("Quan", message);
        Assert.Equal(before, kernel.Today);
        Assert.Equal(0, Guild(kernel).PracticeCount(4));
    }

    [Fact]
    public void Practice_raises_the_tier_and_the_yield()
    {
        using var kernel = _host.Boot();
        var trade = Guild(kernel).Trades.First(candidate => candidate.Code == "nong_phu");

        for (var batch = 0; batch < ProfessionServiceConstants.PracticesPerTier; batch++)
        {
            Command(kernel, $"work:{trade.Id}");
        }

        Assert.Equal(1, Guild(kernel).Tier(trade.Id));

        var before = Bag(kernel).Quantity(Player(kernel).CurrentCityId, trade.OutputItemId);
        Command(kernel, $"work:{trade.Id}");
        var gained = Bag(kernel).Quantity(Player(kernel).CurrentCityId, trade.OutputItemId) - before;

        Assert.Equal(trade.BaseQuantity + 1, gained);
    }

    [Fact]
    public void Tier_is_capped()
    {
        using var kernel = _host.Boot();
        var trade = Guild(kernel).Trades.First(candidate => candidate.Code == "nong_phu");

        for (var batch = 0; batch < 40; batch++)
        {
            Command(kernel, $"work:{trade.Id}");
        }

        Assert.Equal(5, Guild(kernel).Tier(trade.Id));
    }

    [Fact]
    public void An_unknown_trade_is_refused()
    {
        using var kernel = _host.Boot();

        var message = Command(kernel, "work:99");

        Assert.Contains("không tồn tại", message);
    }

    [Fact]
    public void Crafting_equipment_consumes_materials_and_produces_gear()
    {
        using var kernel = _host.Boot();
        var player = Player(kernel);
        var bag = Bag(kernel);

        // Seed 4 wood (item 6) for Recipe 1 (needs 2 wood, fee 10)
        bag.Add(player.CurrentCityId, 6, 4);

        var balanceBefore = kernel.Ledger.Balance;
        var msg = Command(kernel, "craft:1");

        Assert.Contains("Chế tác thành công", msg);
        Assert.Equal(2, bag.Quantity(player.CurrentCityId, 6)); // 4 - 2 = 2
        Assert.Equal(balanceBefore - 10, kernel.Ledger.Balance);

        var equips = Guild(kernel).Equipments(player.CurrentCityId);
        Assert.Contains(equips, e => e.NameVi == "Chế Tác Nông Cụ Gỗ");
    }

    [Fact]
    public void Crafting_requires_tier_level()
    {
        using var kernel = _host.Boot();
        var player = Player(kernel);
        Bag(kernel).Add(player.CurrentCityId, 6, 10);
        Bag(kernel).Add(player.CurrentCityId, 5, 10);

        // Recipe 5 requires tier 2 of trade 3 (carpenter), current tier is 0
        var msg = Command(kernel, "craft:5");

        Assert.Contains("Chưa đủ tay nghề", msg);
    }

    [Fact]
    public void Repairing_damaged_equipment_restores_durability()
    {
        using var kernel = _host.Boot();
        var player = Player(kernel);
        var equips = Guild(kernel).Equipments(player.CurrentCityId);

        // Equipment 2 has durability 80/100
        var equip2 = equips.First(e => e.Id == 2);
        Assert.Equal(80, equip2.Durability);

        var balanceBefore = kernel.Ledger.Balance;
        var msg = Command(kernel, "repair:2");

        Assert.Contains("tu sửa", msg);
        Assert.Equal(balanceBefore - 15, kernel.Ledger.Balance);

        var refreshed = Guild(kernel).Equipments(player.CurrentCityId).First(e => e.Id == 2);
        Assert.Equal(100, refreshed.Durability);
    }

    [Fact]
    public void Invariants_hold_after_many_batches()
    {
        using var kernel = _host.Boot();

        for (var batch = 0; batch < 12; batch++)
        {
            Command(kernel, "work:1");
            Command(kernel, "work:3");
        }

        Assert.Empty(kernel.RunInvariants());
    }

    [Fact]
    public void Masterpiece_recipes_are_seeded_including_weapons_and_alchemy()
    {
        using var kernel = _host.Boot();
        var recipes = Guild(kernel).Recipes;

        Assert.True(recipes.Count >= 11);
        Assert.Contains(recipes, r => r.NameVi.Contains("Thanh Long Yển Nguyệt Đao") && r.RequiredTier == 3);
        Assert.Contains(recipes, r => r.NameVi.Contains("Bát Xà Mâu") && r.RequiredTier == 3);
        Assert.Contains(recipes, r => r.NameVi.Contains("Thần Tý Cung") && r.RequiredTier == 2);
        Assert.Contains(recipes, r => r.NameVi.Contains("Bách Thảo Cao") && r.TradeId == 5);
        Assert.Contains(recipes, r => r.NameVi.Contains("Kim Sáng Dược") && r.TradeId == 5);
        Assert.Contains(recipes, r => r.NameVi.Contains("Cửu Chuyển Hoàn Đan") && r.RequiredTier == 4);
    }

    [Fact]
    public void Herbalist_duoc_su_trade_is_available_and_workable()
    {
        using var kernel = _host.Boot();
        var trades = Guild(kernel).Trades;
        var duocSu = trades.FirstOrDefault(t => t.Code == "duoc_su");

        Assert.NotNull(duocSu);
        Assert.Equal("Dược Sư", duocSu.NameVi);
        Assert.Equal(7, duocSu.OutputItemId);

        var player = Player(kernel);
        var bag = Bag(kernel);
        var initialMeds = bag.Quantity(player.CurrentCityId, 7);

        var msg = Command(kernel, $"work:{duocSu.Id}");
        Assert.Contains("làm Dược Sư", msg);
        Assert.Contains("+2 Thuốc", msg);
        Assert.True(bag.Quantity(player.CurrentCityId, 7) > initialMeds);
    }

    [Fact]
    public void High_tier_smith_crafts_masterpiece_equipment()
    {
        using var kernel = _host.Boot();
        var player = Player(kernel);
        var bag = Bag(kernel);

        // Raise trade 4 (luyen_kim) to tier 4
        for (var i = 0; i < 20; i++)
        {
            Command(kernel, "work:4");
        }
        Assert.True(Guild(kernel).Tier(4) >= 4);

        // Seed materials for Recipe 6 (Thanh Long Yển Nguyệt Đao: 6 sat, 2 go)
        bag.Add(player.CurrentCityId, 5, 20);
        bag.Add(player.CurrentCityId, 6, 10);

        var msg = Command(kernel, "craft:6");
        Assert.Contains("Chế tác thành công", msg);

        var equips = Guild(kernel).Equipments(player.CurrentCityId);
        var craftedDao = equips.FirstOrDefault(e => e.NameVi.Contains("Thanh Long"));
        Assert.NotNull(craftedDao);
        Assert.True(craftedDao.QualityRank is "FINE" or "MASTER" or "LEGENDARY");
        Assert.Equal("WAR", craftedDao.BonusStat);
        Assert.True(craftedDao.BonusValue >= 4);
        Assert.True(craftedDao.MaxDurability >= 100);
    }

    [Fact]
    public void Equip_and_unequip_item_changes_slots_and_player_bonus()
    {
        using var kernel = _host.Boot();
        var guild = Guild(kernel);
        var eq = kernel.Capabilities.Get<IPlayerEquipment>(CapabilityIds.PlayerEquipment);
        var attr = kernel.Capabilities.Get<IPlayerAttributes>(CapabilityIds.PlayerAttributes);

        // Verify initial empty slots
        var weaponSlot = eq.GetSlot("WEAPON");
        Assert.NotNull(weaponSlot);
        Assert.Null(weaponSlot.EquipmentId);
        Assert.Equal(0, eq.GetTotalBonus("WAR"));

        var baseWar = attr.Attributes.War;

        // Equip weapon 1 (Đao Sắt Thao Diễn, +3 WAR)
        var equipMsg = Command(kernel, "equip:1");
        Assert.Contains("Đã trang bị", equipMsg);
        Assert.Equal(3, eq.GetTotalBonus("WAR"));
        Assert.Equal(baseWar + 3, attr.Attributes.War);

        // Unequip weapon
        var unequipMsg = Command(kernel, "unequip:WEAPON");
        Assert.Contains("Đã gỡ trang bị", unequipMsg);
        Assert.Equal(0, eq.GetTotalBonus("WAR"));
        Assert.Equal(baseWar, attr.Attributes.War);
    }

    [Fact]
    public void Opening_workshop_and_hiring_artisans_produces_monthly_yield()
    {
        using var kernel = _host.Boot();
        var player = Player(kernel);
        var bag = Bag(kernel);
        var guild = Guild(kernel);

        // Open woodcraft workshop at city 1 (trade 3: Thợ mộc)
        var openMsg = Command(kernel, "workshop:open:3");
        Assert.Contains("Mở thành công", openMsg);

        var ws = guild.GetWorkshop(player.CurrentCityId);
        Assert.NotNull(ws);
        Assert.Equal(3, ws.TradeId);
        Assert.Equal(0, ws.HiredArtisans);

        // Hire 2 artisans
        Command(kernel, "workshop:hire:0");
        Command(kernel, "workshop:hire:0");

        var wsAfterHire = guild.GetWorkshop(player.CurrentCityId);
        Assert.Equal(2, wsAfterHire!.HiredArtisans);

        var woodBefore = bag.Quantity(player.CurrentCityId, 6); // Item 6: Gỗ

        // Advance 30 days to trigger month close
        for (var day = 0; day < 30; day++)
        {
            kernel.Clock.Advance(1);
        }

        var woodAfter = bag.Quantity(player.CurrentCityId, 6);
        Assert.True(woodAfter > woodBefore, "Artisans must automatically produce goods on month close.");
    }

    [Fact]
    public void Accepting_and_fulfilling_crafting_order_grants_copper_and_fame()
    {
        using var kernel = _host.Boot();
        var player = Player(kernel);
        var bag = Bag(kernel);
        var guild = Guild(kernel);

        // Order 1 needs 10 iron items (item 5), gives 350 copper + 15 fame
        var acceptMsg = Command(kernel, "order:accept:1");
        Assert.Contains("Đã tiếp nhận", acceptMsg);

        // Seed 10 iron items into bag
        bag.Add(player.CurrentCityId, 5, 10);

        var initialBalance = kernel.Ledger.Balance;
        var initialFame = guild.TotalGuildFame;

        var fulfillMsg = Command(kernel, "order:fulfill:1");
        Assert.Contains("Giao nộp thành công", fulfillMsg);
        Assert.Equal(initialBalance + 350, kernel.Ledger.Balance);
        Assert.Equal(initialFame + 15, guild.TotalGuildFame);
        Assert.Equal(0, bag.Quantity(player.CurrentCityId, 5));
    }

    [Fact]
    public void Craftsman_rank_and_titles_upgrade_with_practice()
    {
        using var kernel = _host.Boot();
        var guild = Guild(kernel);

        Assert.Equal("Học Đồ", guild.GetCraftsmanRank(1));

        // 4 batches = tier 1 (Thợ Phụ)
        for (var i = 0; i < 4; i++)
        {
            Command(kernel, "work:1");
        }
        Assert.Equal("Thợ Phụ", guild.GetCraftsmanRank(1));

        // 4 more batches = tier 2 (Thợ Nghề)
        for (var i = 0; i < 4; i++)
        {
            Command(kernel, "work:1");
        }
        Assert.Equal("Thợ Nghề", guild.GetCraftsmanRank(1));
    }

    [Fact]
    public void All_16_trades_are_loaded_across_four_sectors()
    {
        using var kernel = _host.Boot();
        var trades = Guild(kernel).Trades;

        Assert.Equal(16, trades.Count);

        var sectors = trades.Select(t => t.Sector).Distinct().ToHashSet();
        Assert.Contains("AGRI", sectors);
        Assert.Contains("CRAFT", sectors);
        Assert.Contains("TEXTILE", sectors);
        Assert.Contains("ALCH", sectors);

        // Verify representative trades in each sector
        Assert.Contains(trades, t => t.Code == "canh_nong" && t.Sector == "AGRI");
        Assert.Contains(trades, t => t.Code == "duc_dong" && t.Sector == "CRAFT");
        Assert.Contains(trades, t => t.Code == "det_gam" && t.Sector == "TEXTILE");
        Assert.Contains(trades, t => t.Code == "nau_ruou" && t.Sector == "ALCH");
    }

    [Fact]
    public void Mastery_perks_can_be_unlocked_and_boost_yield_and_durability()
    {
        using var kernel = _host.Boot();
        var guild = Guild(kernel);
        var player = Player(kernel);
        var bag = Bag(kernel);

        // Fulfill order 1 (+15 fame) to reach 25 fame (starts with 10)
        Command(kernel, "order:accept:1");
        bag.Add(player.CurrentCityId, 5, 10);
        Command(kernel, "order:fulfill:1");
        Assert.True(guild.TotalGuildFame >= 25);

        // Unlock Perk 1: Xảo Đoạt Thiên Công (needs 15 fame) -> +1 yield
        var unlockMsg = Command(kernel, "perk:unlock:1");
        Assert.Contains("Giác ngộ thành công", unlockMsg);

        var gaoBefore = bag.Quantity(player.CurrentCityId, 1);
        Command(kernel, "work:1"); // Base 4 + tier 0 + 1 perk bonus = 5
        var gaoGained = bag.Quantity(player.CurrentCityId, 1) - gaoBefore;
        Assert.Equal(5, gaoGained);
    }

    [Fact]
    public void Fishing_activity_yields_fish_advances_day_and_relieves_stress()
    {
        using var kernel = _host.Boot();
        var player = Player(kernel);
        var bag = Bag(kernel);
        var attrs = kernel.Capabilities.Get<IPlayerAttributes>(CapabilityIds.PlayerAttributes);

        var todayBefore = kernel.Today;
        var fishBefore = bag.Quantity(player.CurrentCityId, 8); // 8: ca_kho

        var msg = Command(kernel, "wild:fish");

        Assert.Contains("buông cần tại", msg);
        Assert.Equal(todayBefore.AddDays(1), kernel.Today);
        Assert.True(bag.Quantity(player.CurrentCityId, 8) > fishBefore);
    }

    [Fact]
    public void Foraging_activity_yields_herbs_advances_day_and_grants_exp()
    {
        using var kernel = _host.Boot();
        var player = Player(kernel);
        var bag = Bag(kernel);

        var todayBefore = kernel.Today;
        var herbsBefore = bag.Quantity(player.CurrentCityId, 7); // 7: thuoc

        var msg = Command(kernel, "wild:forage");

        Assert.Contains("vượt suối trèo non tầm dược", msg);
        Assert.Equal(todayBefore.AddDays(1), kernel.Today);
        Assert.True(bag.Quantity(player.CurrentCityId, 7) > herbsBefore);
    }

    [Fact]
    public void Cooking_and_consuming_dishes_relieves_fatigue_and_heals()
    {
        using var kernel = _host.Boot();
        var player = Player(kernel);
        var bag = Bag(kernel);
        var attrs = kernel.Capabilities.Get<IPlayerAttributes>(CapabilityIds.PlayerAttributes);

        // Dish 1: com_nam requires 2 gao (1) + 1 muoi (2), cook fee 5
        bag.Add(player.CurrentCityId, 1, 2);
        bag.Add(player.CurrentCityId, 2, 1);

        var cookMsg = Command(kernel, "dish:cook:1");
        Assert.Contains("Nấu thành công", cookMsg);

        attrs.ApplyFatigue(50);
        Assert.Equal(50, attrs.Vitals.Fatigue);

        var eatMsg = Command(kernel, "dish:eat:1");
        Assert.Contains("Thưởng thức Cơm Nắm Hành Quân", eatMsg);
        Assert.Equal(35, attrs.Vitals.Fatigue); // 50 - 15 = 35
    }

    [Fact]
    public void Blacksmithing_tempering_enhances_equipment_durability_and_bonus()
    {
        using var kernel = _host.Boot();
        var player = Player(kernel);
        var bag = Bag(kernel);
        var guild = Guild(kernel);

        // Equipment 1 at citadel 1 starts with Bonus 3 (WAR), Durability 100/100, TemperLevel 0
        // Level 1 Temper needs 2 sat (5), 1 than_da (15), 25 Quan fee
        bag.Add(player.CurrentCityId, 5, 2);
        bag.Add(player.CurrentCityId, 15, 1);

        var temperMsg = Command(kernel, "temper:1");
        Assert.Contains("Tôi luyện thành công", temperMsg);

        var eq = guild.Equipments(player.CurrentCityId).First(e => e.Id == 1);
        Assert.Equal(1, eq.TemperLevel);
        Assert.Equal(4, eq.BonusValue); // 3 + 1 = 4
        Assert.Equal(115, eq.MaxDurability); // 100 + 15 = 115
    }

    [Fact]
    public void Hunting_activity_yields_meat_advances_day_and_learns_skill()
    {
        using var kernel = _host.Boot();
        var player = Player(kernel);
        var bag = Bag(kernel);
        var attrs = kernel.Capabilities.Get<IPlayerAttributes>(CapabilityIds.PlayerAttributes);

        var todayBefore = kernel.Today;
        var meatBefore = bag.Quantity(player.CurrentCityId, 8); // 8: ca_kho (meat/provisions)

        var msg = Command(kernel, "wild:hunt");

        Assert.Contains("săn bắn", msg);
        Assert.Equal(todayBefore.AddDays(1), kernel.Today);
        Assert.True(bag.Quantity(player.CurrentCityId, 8) > meatBefore);
    }

    [Fact]
    public void Wilderness_activities_train_life_skills_in_player_attributes()
    {
        using var kernel = _host.Boot();
        var player = Player(kernel);
        var attrs = kernel.Capabilities.Get<IPlayerAttributes>(CapabilityIds.PlayerAttributes);

        for (int i = 0; i < 20 && !attrs.HasSkill("dieu_ngu"); i++) Command(kernel, "wild:fish");
        Assert.True(attrs.HasSkill("dieu_ngu"));

        for (int i = 0; i < 20 && !attrs.HasSkill("tam_duoc"); i++) Command(kernel, "wild:forage");
        Assert.True(attrs.HasSkill("tam_duoc"));

        for (int i = 0; i < 20 && !attrs.HasSkill("thu_liep"); i++) Command(kernel, "wild:hunt");
        Assert.True(attrs.HasSkill("thu_liep"));
    }

    [Fact]
    public void Regional_foraging_in_u_chau_yields_ginseng()
    {
        using var kernel = _host.Boot();
        var player = Player(kernel);
        var bag = Bag(kernel);

        // Move to Trác Huyện (citadel 4, region: u_chau)
        player.MoveTo(4, kernel.Today);
        Assert.Equal(4, player.CurrentCityId);

        var samBefore = bag.Quantity(4, 13); // 13: sam (Nhân sâm Liêu Đông)
        var msg = Command(kernel, "wild:forage");

        Assert.Contains("Nhân Sâm Liêu Đông", msg);
        Assert.True(bag.Quantity(4, 13) > samBefore);
    }

    [Fact]
    public void Natural_skill_awakening_and_rank_titles()
    {
        using var kernel = _host.Boot();
        var attrs = kernel.Capabilities.Get<IPlayerAttributes>(CapabilityIds.PlayerAttributes);

        Assert.False(attrs.HasSkill("dieu_ngu"));

        var chance = attrs.GetAwakeningChancePermille("dieu_ngu");
        Assert.True(chance >= 50 && chance <= 950);

        for (int i = 0; i < 20 && !attrs.HasSkill("dieu_ngu"); i++)
        {
            Command(kernel, "wild:fish");
        }

        Assert.True(attrs.HasSkill("dieu_ngu"));

        var prog = attrs.GetSkillProgress("dieu_ngu");
        Assert.NotNull(prog);
        Assert.Equal(1, prog.Level);
        Assert.Contains("Sơ Học", SkillRankTitles.ForLevel(prog.Level));
    }

    [Fact]
    public void Probabilistic_awakening_chance_scales_with_attributes_and_accumulated_attempts()
    {
        using var kernel = _host.Boot();
        var attrs = kernel.Capabilities.Get<IPlayerAttributes>(CapabilityIds.PlayerAttributes);

        var initialChance = attrs.GetAwakeningChancePermille("tam_duoc");
        // Base 250 + PRC (60-50)*12 + INT (60-50)*8 + LCK (70-50)*5 = 550 permille (55.0%)
        Assert.Equal(550, initialChance);

        var result = attrs.PracticeSkill("tam_duoc", 20);
        if (!result.Awakened)
        {
            var nextChance = attrs.GetAwakeningChancePermille("tam_duoc");
            Assert.True(nextChance > initialChance);
            Assert.Equal(0, result.Level);
        }
        else
        {
            Assert.Equal(1, result.Level);
            Assert.True(attrs.HasSkill("tam_duoc"));
        }
    }

    [Fact]
    public void Skill_level_can_reach_level_10_hoa_canh()
    {
        using var kernel = _host.Boot();
        var attrs = kernel.Capabilities.Get<IPlayerAttributes>(CapabilityIds.PlayerAttributes);

        // Practice repeatedly until max level 10
        for (int i = 0; i < 600; i++)
        {
            attrs.PracticeSkill("dieu_ngu", 50);
        }

        var prog = attrs.GetSkillProgress("dieu_ngu");
        Assert.NotNull(prog);
        Assert.Equal(10, prog.Level);
        Assert.Contains("Hóa Cảnh", SkillRankTitles.ForLevel(prog.Level));
    }

    [Fact]
    public void Player_can_sell_crafted_equipment_for_copper_coins()
    {
        using var kernel = _host.Boot();
        var guild = Guild(kernel);
        var wallet = kernel.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);

        var eqs = guild.Equipments(1);
        Assert.NotEmpty(eqs);
        var targetEq = eqs.First(e => e.Id == 2); // Cuốc Sắt Khai Điền

        var sellValue = guild.CalculateEquipmentSellValue(targetEq);
        Assert.True(sellValue >= 10);

        var balanceBefore = wallet.Liquid;
        var result = guild.SellEquipment(1, targetEq.Id);

        Assert.True(result.Ok);
        Assert.Equal(balanceBefore + sellValue, wallet.Liquid);

        var eqsAfter = guild.Equipments(1);
        Assert.DoesNotContain(eqsAfter, e => e.Id == targetEq.Id);
    }

    [Fact]
    public void Player_can_cook_and_sell_dishes_to_local_market()
    {
        using var kernel = _host.Boot();
        var guild = Guild(kernel);
        var wallet = kernel.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);
        var bag = Bag(kernel);

        // Seed ingredients for com_nam (Id 1: 2 gao, 1 muoi)
        bag.Add(1, 1, 10); // 10 Gạo
        bag.Add(1, 2, 10); // 10 Muối

        var cookRes = guild.Cook(1, 1);
        Assert.True(cookRes.Ok);

        var dish = guild.Dishes.First(d => d.Id == 1);
        Assert.True(dish.InInventory >= 1);

        var sellValue = guild.CalculateDishSellValue(dish);
        Assert.True(sellValue >= 25);

        var balanceBefore = wallet.Liquid;
        var sellRes = guild.SellDish(1, 1, 1);

        Assert.True(sellRes.Ok);
        Assert.Equal(balanceBefore + sellValue, wallet.Liquid);

        var dishAfter = guild.Dishes.First(d => d.Id == 1);
        Assert.Equal(dish.InInventory - 1, dishAfter.InInventory);
    }

    [Fact]
    public void Dynamic_npc_orders_generate_on_month_and_can_be_fulfilled()
    {
        using var kernel = _host.Boot();
        var guild = Guild(kernel);
        var wallet = kernel.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);
        var bag = Bag(kernel);

        // Advance 30 days to trigger MonthClosed and dynamic NPC orders
        kernel.AdvanceDays(31);

        var orders = guild.AvailableOrders(1);
        Assert.NotEmpty(orders);

        var order = orders.First();
        var acceptRes = guild.AcceptOrder(order.Id);
        Assert.True(acceptRes.Ok);

        // Provide target items to bag
        bag.Add(order.CitadelId, order.TargetItemId, order.QuantityNeeded);

        var fameBefore = guild.TotalGuildFame;
        var balanceBefore = wallet.Liquid;

        var fulfillRes = guild.FulfillOrder(order.CitadelId, order.Id);
        Assert.True(fulfillRes.Ok);

        Assert.True(wallet.Liquid >= balanceBefore + order.RewardCopper);
        Assert.Equal(fameBefore + order.FameReward, guild.TotalGuildFame);
    }

    [Fact]
    public void Workbench_visual_recipe_flow_renders_quality_preview_and_material_checks()
    {
        using var kernel = _host.Boot();
        var screen = kernel.Ui.Screens.First(s => s.ScreenId == "professions.trades");
        var model = screen.Render();

        var recipeSection = model.Sections.First(s => s.Heading == "Bản Vẽ Chế Tác Khí Cụ");
        Assert.NotEmpty(recipeSection.Rows);

        var firstRow = recipeSection.Rows[0];
        Assert.Contains("Bậc", firstRow.Cells[1]);
        Assert.Contains("Quan", firstRow.Cells[3]);
        Assert.Contains("ngày", firstRow.Cells[4]);

        var detail = firstRow.Details!.First(d => d.Label == "Quy Trình Gia Công");
        Assert.Contains("➔", detail.Value);

        // Add materials to bag and re-render
        var bag = Bag(kernel);
        var player = Player(kernel);
        var guild = Guild(kernel);
        var recipe = guild.Recipes[0];
        bag.Add(player.CurrentCityId, recipe.MaterialItemId, recipe.MaterialQuantity * 2);

        var modelAfter = screen.Render();
        var rowAfter = modelAfter.Sections.First(s => s.Heading == "Bản Vẽ Chế Tác Khí Cụ").Rows[0];
        Assert.Contains("✅", rowAfter.Cells[2]);
    }

    private static string Command(SimKernel kernel, string commandId)
    {
        Assert.True(kernel.TryGetCommandHandler("professions.trades", out var handler), "professions screen is not registered");
        return handler!.Execute(commandId) ?? string.Empty;
    }

    private static IPlayerState Player(SimKernel kernel) =>
        kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);

    private static IBag Bag(SimKernel kernel) =>
        kernel.Capabilities.Get<IBag>(CapabilityIds.PlayerBag);

    private static IProfessionGuild Guild(SimKernel kernel) =>
        kernel.Capabilities.Get<IProfessionGuild>(CapabilityIds.ProfessionGuild);

    [Fact]
    public void Crafted_equipment_records_creator_signature_and_provenance()
    {
        using var kernel = _host.Boot();
        var guild = Guild(kernel);
        var player = Player(kernel);
        var bag = Bag(kernel);

        // Supply materials for recipe 3 (Đúc Binh Đao Bằng Sắt: 2 sat)
        bag.Add(player.CurrentCityId, 5, 10);

        var msg = Command(kernel, "craft:3");
        Assert.Contains("Chế tác thành công", msg);

        var equips = guild.Equipments(player.CurrentCityId);
        var crafted = equips.LastOrDefault(e => e.NameVi.Contains("Binh Đao"));
        Assert.NotNull(crafted);
        Assert.Equal(player.Name, crafted.CreatorNameVi);
        Assert.Equal(player.PlayerId, crafted.CreatorId);
        Assert.Equal(player.CurrentCityId, crafted.ForgedCitadelId);
        Assert.True(crafted.FameScore >= 0);
        Assert.NotNull(crafted.LotCode);
        Assert.StartsWith("LOT-", crafted.LotCode);
        Assert.Contains($"-C{player.CurrentCityId:D2}-", crafted.LotCode);

        var screen = kernel.Ui.Screens.First(s => s.ScreenId == "professions.trades");
        var model = screen.Render();
        var equipSection = model.Sections.First(s => s.Heading == "Khí Cụ Trong Thành");
        var craftedRow = equipSection.Rows.LastOrDefault(r => r.Cells[0].Contains("Binh Đao"));
        Assert.NotNull(craftedRow);
        Assert.DoesNotContain(crafted.LotCode, craftedRow.Cells[0]);
        Assert.Contains(craftedRow.Details!, d => d.Label == "Nghệ Nhân Chế Tác" && d.Value == player.Name);
        Assert.Contains(craftedRow.Details!, d => d.Label == "Trạng Thái Độ Bền");
    }

    [Fact]
    public void Combat_victory_records_feats_on_weapon_and_broadcasts_chronicle()
    {
        using var kernel = _host.Boot();
        var guild = Guild(kernel);
        var military = kernel.Capabilities.Get<IMilitaryCommand>(CapabilityIds.MilitaryCommand);
        var chronicle = kernel.Capabilities.Get<ISocietyChronicle>(CapabilityIds.SocietyChronicle);
        var eq = kernel.Capabilities.Get<IPlayerEquipment>(CapabilityIds.PlayerEquipment);

        // Equip weapon 1
        Command(kernel, "equip:1");
        var weaponBefore = eq.GetSlot("WEAPON")?.Equipment;
        Assert.NotNull(weaponBefore);

        var targetStronghold = military.StrongholdsAt(1).First();
        var summary = military.AssaultStronghold(targetStronghold.Id);
        Assert.NotEmpty(summary);

        var weaponAfter = guild.Equipments(1).FirstOrDefault(e => e.Id == weaponBefore.Id);
        Assert.NotNull(weaponAfter);
        Assert.True(weaponAfter.KillCount >= 10);
        Assert.True(weaponAfter.FameScore >= 15);
        Assert.NotNull(weaponAfter.LegendNarrative);
        Assert.Contains("tiêu diệt thủ ác", weaponAfter.LegendNarrative);

        // Check broadcast to living world chronicle
        var martialEvents = chronicle.RecentEvents(limit: 20, category: "martial");
        Assert.Contains(martialEvents, e => e.HeadlineVi.Contains("lập đại công"));
    }

    [Fact]
    public void Wilderness_mining_extracts_regional_specialty_ores_and_trains_khai_khoang()
    {
        using var kernel = _host.Boot();
        var player = Player(kernel);
        var bag = Bag(kernel);

        // Move to Thượng Đảng (18) — Thượng Đảng Mỏ Sắt (Iron #5 + Black Iron #25)
        player.MoveTo(18, kernel.Today);
        var ironBefore = bag.Quantity(18, 5);
        var blackIronBefore = bag.Quantity(18, 25);

        var msg = Command(kernel, "wild:mine");
        Assert.Contains("Thượng Đảng Mỏ Sắt", msg);
        Assert.True(bag.Quantity(18, 5) > ironBefore);
        Assert.True(bag.Quantity(18, 25) > blackIronBefore);

        // Move to Tây Lương (19) — Mỏ Đồng Kỳ Liên (Bronze #16)
        player.MoveTo(19, kernel.Today);
        var bronzeBefore = bag.Quantity(19, 16);
        var msgBronze = Command(kernel, "wild:mine");
        Assert.Contains("Mỏ Đồng Kỳ Liên", msgBronze);
        Assert.True(bag.Quantity(19, 16) > bronzeBefore);

        // Move to Giao Chỉ (20) — Rừng Trầm Hương Lĩnh Nam (Agarwood #14)
        player.MoveTo(20, kernel.Today);
        var agarBefore = bag.Quantity(20, 14);
        var msgForage = Command(kernel, "wild:forage");
        Assert.Contains("Trầm Hương", msgForage);
        Assert.True(bag.Quantity(20, 14) > agarBefore);
    }

    [Fact]
    public void Equipment_durability_degrades_bonus_and_black_iron_repair_reinforces_max_durability()
    {
        using var kernel = _host.Boot();
        var eqService = kernel.Capabilities.Get<IPlayerEquipment>(CapabilityIds.PlayerEquipment);
        var guild = Guild(kernel);
        var bag = Bag(kernel);

        // Equip weapon 1 (Đao Sắt Thao Diễn, +3 WAR, 100/100 durability)
        Command(kernel, "equip:1");
        Assert.Equal(3, eqService.GetTotalBonus("WAR"));

        // Degrade by 60 -> 40/100 (< 50% max durability => chipped blade, 50% bonus = +1 WAR)
        eqService.DegradeEquipped("WEAPON", 60);
        Assert.Equal(1, eqService.GetTotalBonus("WAR"));

        // Degrade by 40 -> 0/100 (broken => +0 WAR)
        eqService.DegradeEquipped("WEAPON", 40);
        Assert.Equal(0, eqService.GetTotalBonus("WAR"));

        // Supply 1 black_iron (#25) and repair weapon 1 -> restores durability and reinforces MaxDurability +5 (105/105)
        bag.Add(1, 25, 1);
        var repairMsg = Command(kernel, "repair:1");
        Assert.Contains("Đại tu bằng Huyền Thiết", repairMsg);
        Assert.Contains("105/105", repairMsg);

        var repaired = guild.Equipments(1).First(e => e.Id == 1);
        Assert.Equal(105, repaired.MaxDurability);
        Assert.Equal(105, repaired.Durability);
        Assert.Equal(3, eqService.GetTotalBonus("WAR"));
    }

}

internal static class ProfessionServiceConstants
{
    public const int PracticesPerTier = 4;
}
