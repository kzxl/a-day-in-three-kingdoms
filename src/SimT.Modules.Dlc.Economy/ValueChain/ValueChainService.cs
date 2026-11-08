using SimT.Contracts;
using SimT.Contracts.Data;
using SimT.Contracts.Logging;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Economy.ValueChain;

/// <summary>
/// Domain implementation of the 4-Tier Economic Value Chain and civic market consumption.
/// </summary>
public sealed class ValueChainService : IValueChainService
{
    private readonly IDbSession _db;
    private readonly IItemCatalog _items;
    private readonly IWorldMap _map;
    private readonly INpcRegistry? _npcs;
    private readonly IMarketTrade _trade;
    private readonly ILog? _log;

    public ValueChainService(
        IDbSession db,
        IItemCatalog items,
        IWorldMap map,
        INpcRegistry? npcs,
        IMarketTrade trade,
        ILog? log)
    {
        _db = db;
        _items = items;
        _map = map;
        _npcs = npcs;
        _trade = trade;
        _log = log;
    }

    public ValueChainProfileDef GetProfile(string itemCode)
    {
        var profile = ItemValueChain.GetProfile(itemCode);
        return new ValueChainProfileDef(
            itemCode,
            profile.Tier,
            profile.TierNameVi,
            profile.BadgeVi,
            profile.CategoryVi,
            profile.PrimaryConsumerVi,
            profile.EconomicRoleVi,
            profile.Inputs,
            profile.Outputs);
    }

        public MarketDemandStatus EstimateDemand(long citadelId, long itemId, string itemCode, string itemNameVi, string category, int stock, int baseline, int npcCount)
    {
        if (baseline <= 0) baseline = 40;
        var monthlyDemand = CalculateMonthlyDemand(itemCode, category, npcCount);

        string velocity;
        if (stock > baseline * 1.3)
        {
            velocity = "📦 Dư thừa (Giá mềm)";
        }
        else if (stock < baseline * 0.5)
        {
            velocity = "⚡ Tiêu thụ cao (Khan hiếm)";
        }
        else
        {
            velocity = "⚖️ Ổn định";
        }

        var dailyRate = Math.Max(1.0, monthlyDemand) / 30.0;
        var daysUntilDepletion = stock == 0 ? 0 : Math.Min(999, (int)(stock / dailyRate));

        return new MarketDemandStatus(
            citadelId,
            itemId,
            itemCode,
            itemNameVi,
            stock,
            baseline,
            monthlyDemand,
            velocity,
            daysUntilDepletion);
    }

    public MarketDemandStatus GetDemandStatus(long citadelId, long itemId)
    {
        var item = _items.Find(itemId) ?? throw new SimException("market.item_missing", $"Item {itemId} not found.");
        var stock = _trade.StallStock(citadelId, itemId);
        var baseline = _trade.StallBaseline(citadelId, itemId);
        var npcCount = _npcs?.FindByCitadel(citadelId).Count ?? 0;
        return EstimateDemand(citadelId, itemId, item.Code, item.NameVi, item.Category, stock, baseline, npcCount);
    }

    public IReadOnlyList<MarketDemandStatus> GetCitadelDemands(long citadelId)
    {
        var npcCount = _npcs?.FindByCitadel(citadelId).Count ?? 0;
        var result = new List<MarketDemandStatus>(_items.All.Count);
        foreach (var item in _items.All)
        {
            var stock = _trade.StallStock(citadelId, item.Id);
            var baseline = _trade.StallBaseline(citadelId, item.Id);
            result.Add(EstimateDemand(citadelId, item.Id, item.Code, item.NameVi, item.Category, stock, baseline, npcCount));
        }
        return result;
    }

    public void SimulateMonthlyCivicConsumption(GameDate date)
    {
        // When stock is in surplus (stock > baseline_stock), local population and resident NPCs
        // organically consume the excess goods, returning the market toward baseline equilibrium.
        var surplusRows = _db.Query(
            "SELECT citadel_id, item_id, stock, baseline_stock FROM mk_stall_listing WHERE stock > baseline_stock",
            row => (City: row.GetInt64(0), Item: row.GetInt64(1), Stock: row.GetInt32(2), Baseline: row.GetInt32(3)));

        foreach (var row in surplusRows)
        {
            var surplus = row.Stock - row.Baseline;
            var consumed = Math.Max(1, (surplus * 40) / 100);
            _db.Execute(
                "UPDATE mk_stall_listing SET stock = stock - @consumed WHERE citadel_id = @city AND item_id = @item",
                new DbArgs().Add("@city", row.City).Add("@item", row.Item).Add("@consumed", consumed));
        }

        if (surplusRows.Count > 0)
        {
            _log?.Info($"[dlc.economy] civic consumption digested surplus across {surplusRows.Count} stall listings on {date}");
        }
    }

    private static int CalculateMonthlyDemand(string itemCode, string category, int npcCount)
    {
        switch (itemCode.ToLowerInvariant())
        {
            case "gao":
            case "lua_mi":
                return Math.Max(3, (npcCount / 4) + 2);

            case "muoi":
                return Math.Max(2, (npcCount / 6) + 1);

            case "tra":
            case "ruou":
                return Math.Max(1, (npcCount / 8) + 1);

            case "vai":
            case "cuoc_nong":
            case "liem_gat":
                return Math.Max(1, (npcCount / 10) + 1);

            case "thuoc":
            case "sat":
            case "dong":
                return Math.Max(1, (npcCount / 12) + 1);

            case "gam":
            case "sam":
            case "tram_huong":
                return Math.Max(1, npcCount / 15);

            default:
                return category switch
                {
                    "grain" or "food" => 3,
                    "material" => 2,
                    _ => 1
                };
        }
    }
}
