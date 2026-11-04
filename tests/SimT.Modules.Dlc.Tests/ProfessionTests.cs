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
}

/// <summary>Mirror of the service constants for tests, so a change on either side is visible.</summary>
internal static class ProfessionServiceConstants
{
    public const int PracticesPerTier = 4;
}
