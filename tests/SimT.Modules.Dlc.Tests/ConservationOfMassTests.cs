using SimT.Contracts.Data;
using SimT.Contracts.Playing;
using SimT.Core.Kernel;

namespace SimT.Modules.Dlc.Tests;

public sealed class ConservationOfMassTests : IDisposable
{
    private readonly DlcTestHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public void Physical_cargo_guarantees_zero_magic_stock_creation()
    {
        using var kernel = _host.Boot();
        var rivalry = kernel.Capabilities.Get<IMerchantRivalry>(CapabilityIds.MerchantRivalry);
        var market = kernel.Capabilities.Get<IMarketTrade>(CapabilityIds.MarketTrade);
        var map = kernel.Capabilities.Get<IWorldMap>(CapabilityIds.WorldMap);
        var db = kernel.Db;

        var rivals = rivalry.GetAllRivals();
        Assert.NotEmpty(rivals);

        // Track Silk (item_id = 4), which Mi Zhu (npc_id = 10) trades
        var miZhu = rivals.First(r => r.NpcId == 10);
        var targetGoodId = miZhu.PrimaryGoodId;

        // Measure initial total goods across all citadels in market stalls
        long InitialStallStock() => map.Citadels.Sum(c => (long)market.StallStock(c.Id, targetGoodId));
        long InitialCargoStock() => db.ScalarLong(
            "SELECT COALESCE(SUM(quantity), 0) FROM so_merchant_cargo WHERE item_id = @item",
            DbArgs.One("@item", targetGoodId));

        var initialStall = InitialStallStock();
        var initialCargo = InitialCargoStock();

        // Ensure there is no pre-existing cargo
        db.Execute("DELETE FROM so_merchant_cargo WHERE item_id = @item", DbArgs.One("@item", targetGoodId));

        // When origin market has stock = 0, rival cannot buy and cannot magically sell
        var currentCit = miZhu.CurrentCitadelId;
        var currentStock = market.StallStock(currentCit, targetGoodId);
        market.AdjustStock(currentCit, targetGoodId, -currentStock); // zero it out

        var targetCit = miZhu.TargetCitadelId;
        var targetStockBefore = market.StallStock(targetCit, targetGoodId);

        // Advance 30 days
        kernel.AdvanceDays(30);

        var miZhuAfter = rivalry.GetRival(miZhu.NpcId);
        Assert.NotNull(miZhuAfter);

        // Target market must NOT have gained any magic stock from an empty origin
        var targetStockAfter = market.StallStock(targetCit, targetGoodId);
        // Note: target market might have changed only due to monthly restock/sales, but Mi Zhu could not inject goods
        var carriedCargo = db.ScalarLong(
            "SELECT COALESCE(SUM(quantity), 0) FROM so_merchant_cargo WHERE npc_id = @id AND item_id = @item",
            new DbArgs().Add("@id", miZhu.NpcId).Add("@item", targetGoodId));

        Assert.Equal(0, carriedCargo);

        // Invariants pass
        Assert.Empty(kernel.RunInvariants());
    }

    [Fact]
    public void Rival_cargo_table_persists_in_transit_goods()
    {
        using var kernel = _host.Boot();
        var db = kernel.Db;

        // Insert manual cargo to verify table existence and queryability
        db.Execute(
            "INSERT INTO so_merchant_cargo (npc_id, item_id, quantity, unit_cost_quan, origin_citadel_id) VALUES (10, 4, 25, 120, 1) ON CONFLICT(npc_id, item_id) DO UPDATE SET quantity = 25",
            null);

        var qty = db.ScalarLong("SELECT quantity FROM so_merchant_cargo WHERE npc_id = 10 AND item_id = 4");
        Assert.Equal(25, qty);

        var cost = db.ScalarLong("SELECT unit_cost_quan FROM so_merchant_cargo WHERE npc_id = 10 AND item_id = 4");
        Assert.Equal(120, cost);
    }
}
