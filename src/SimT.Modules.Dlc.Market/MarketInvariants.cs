using SimT.Contracts.Data;
using SimT.Contracts.Diagnostics;

namespace SimT.Modules.Dlc.Market;

/// <summary>
/// Rules that only make sense because this DLC owns these tables. They run through the kernel
/// runner together with the core ones, so a broken writer is caught by the same audit command.
/// </summary>
internal static class MarketInvariants
{
    public sealed class NonNegativeStallStock : IInvariant
    {
        public string Id => "market.stall.non_negative_stock";

        public string? Check(IInvariantContext context)
        {
            var bad = context.Db.ScalarLong("SELECT COUNT(*) FROM mk_stall_listing WHERE stock < 0");
            return bad == 0 ? null : $"{bad} stall listing(s) have negative stock";
        }
    }

    public sealed class WarehouseWithinCapacity : IInvariant
    {
        public string Id => "market.warehouse.within_capacity";

        public string? Check(IInvariantContext context)
        {
            var over = context.Db.Query(
                """
                SELECT w.citadel_id, w.capacity, COALESCE(SUM(s.quantity), 0)
                FROM mk_warehouse w
                LEFT JOIN mk_warehouse_stock s ON s.citadel_id = w.citadel_id
                GROUP BY w.citadel_id, w.capacity
                HAVING COALESCE(SUM(s.quantity), 0) > w.capacity
                """,
                row => $"{row.GetInt64(0)}: {row.GetInt64(2)}/{row.GetInt32(1)}");

            return over.Count == 0 ? null : $"warehouse(s) over capacity: {string.Join(", ", over)}";
        }
    }

    public sealed class StockRowsRequireWarehouse : IInvariant
    {
        public string Id => "market.warehouse.stock_requires_warehouse";

        public string? Check(IInvariantContext context)
        {
            var orphans = context.Db.ScalarLong(
                """
                SELECT COUNT(*) FROM mk_warehouse_stock s
                WHERE NOT EXISTS (SELECT 1 FROM mk_warehouse w WHERE w.citadel_id = s.citadel_id)
                """);

            return orphans == 0 ? null : $"{orphans} stock row(s) exist without a rented warehouse";
        }
    }

    public sealed class StockWithinBuyBackCapacity : IInvariant
    {
        public string Id => "market.stall.within_buy_back_capacity";

        public string? Check(IInvariantContext context)
        {
            var over = context.Db.Query(
                "SELECT citadel_id, item_id, stock, baseline_stock FROM mk_stall_listing WHERE stock > baseline_stock * 2",
                row => $"{row.GetInt64(0)}/{row.GetInt64(1)}: {row.GetInt32(2)} vs baseline {row.GetInt32(3)}");

            return over.Count == 0 ? null : $"listing(s) above buy-back capacity: {string.Join(", ", over)}";
        }
    }

    public sealed class BaselinePositive : IInvariant
    {
        public string Id => "market.stall.baseline_positive";

        public string? Check(IInvariantContext context)
        {
            var bad = context.Db.ScalarLong("SELECT COUNT(*) FROM mk_stall_listing WHERE baseline_stock <= 0");
            return bad == 0 ? null : $"{bad} listing(s) have a non-positive baseline";
        }
    }

    public sealed class NonNegativeShelfQuantity : IInvariant
    {
        public string Id => "market.player_stall.non_negative_shelf";

        public string? Check(IInvariantContext context)
        {
            var bad = context.Db.ScalarLong("SELECT COUNT(*) FROM mk_player_stall_shelf WHERE quantity < 0");
            return bad == 0 ? null : $"{bad} shelf item(s) have negative quantity";
        }
    }

    public sealed class ShelfRowsRequireStall : IInvariant
    {
        public string Id => "market.player_stall.shelf_requires_stall";

        public string? Check(IInvariantContext context)
        {
            var orphans = context.Db.ScalarLong(
                """
                SELECT COUNT(*) FROM mk_player_stall_shelf s
                WHERE NOT EXISTS (SELECT 1 FROM mk_player_stall st WHERE st.citadel_id = s.citadel_id)
                """);

            return orphans == 0 ? null : $"{orphans} shelf row(s) exist without an active player stall";
        }
    }

    public static IEnumerable<IInvariant> All()
    {
        yield return new NonNegativeStallStock();
        yield return new WarehouseWithinCapacity();
        yield return new StockRowsRequireWarehouse();
        yield return new StockWithinBuyBackCapacity();
        yield return new BaselinePositive();
        yield return new NonNegativeShelfQuantity();
        yield return new ShelfRowsRequireStall();
    }
}
