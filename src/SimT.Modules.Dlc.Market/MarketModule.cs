using SimT.Contracts.Modules;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;
using SimT.Contracts.Ui;

namespace SimT.Modules.Dlc.Market;

/// <summary>
/// One handler instance per screen. Command ids are <c>verb:itemId</c>, so the screen model
/// stays data-only and the host needs no knowledge of market verbs.
/// </summary>
internal sealed class MarketCommandHandler : IScreenCommandHandler
{
    private readonly MarketService _service;
    private readonly IPlayerState _player;

    public MarketCommandHandler(string screenId, MarketService service, IPlayerState player)
    {
        ScreenId = screenId;
        _service = service;
        _player = player;
    }

    public string ScreenId { get; }

    public string? Execute(string commandId)
    {
        var cityId = _player.CurrentCityId;

        if (string.Equals(commandId, "rent", StringComparison.Ordinal))
        {
            _service.Rent(cityId, out var rentMessage);
            return rentMessage;
        }

        // Forms: verb:item  and  verb:item:quantity
        if (string.Equals(commandId, "stall:rent", StringComparison.Ordinal) || string.Equals(commandId, "rent_stall", StringComparison.Ordinal))
        {
            _service.RentStall(cityId, out var rentMsg);
            return rentMsg;
        }

        if (string.Equals(commandId, "stall:stock_all", StringComparison.Ordinal))
        {
            _service.StockAll(cityId, out var stockMsg);
            return stockMsg;
        }

        var parts = commandId.Split(':');
        if (parts.Length >= 2 && string.Equals(parts[0], "stall", StringComparison.Ordinal))
        {
            var action = parts[1];
            if (string.Equals(action, "stock", StringComparison.Ordinal) && parts.Length >= 3 && long.TryParse(parts[2], out var stockItemId))
            {
                var qty = parts.Length >= 4 && int.TryParse(parts[3], out var pQty) ? pQty : 1;
                _service.Stock(cityId, stockItemId, qty, out var msg);
                return msg;
            }

            if (string.Equals(action, "unstock", StringComparison.Ordinal) && parts.Length >= 3 && long.TryParse(parts[2], out var unstockItemId))
            {
                var qty = parts.Length >= 4 && int.TryParse(parts[3], out var pQty) ? pQty : 1;
                _service.Unstock(cityId, unstockItemId, qty, out var msg);
                return msg;
            }

            if (string.Equals(action, "markup", StringComparison.Ordinal) && parts.Length >= 4 && long.TryParse(parts[2], out var markupItemId) && int.TryParse(parts[3], out var percent))
            {
                var current = _service.Shelves(cityId).FirstOrDefault(s => s.ItemId == markupItemId);
                if (current is null) return "Không tìm thấy hàng trên quầy.";
                var newPrice = Math.Max(1L, current.SellingPrice * (100 + percent) / 100);
                _service.SetPrice(cityId, markupItemId, newPrice, out var msg);
                return msg;
            }
        }

        if (parts.Length >= 2 && string.Equals(parts[0], "npc_buy", StringComparison.Ordinal) && long.TryParse(parts[1], out var offerId))
        {
            var qty = parts.Length >= 3 && int.TryParse(parts[2], out var parsedQty) && parsedQty > 0 ? parsedQty : 1;
            return _service.BuyFromNpcOffer(cityId, offerId, qty).Message;
        }

        if (parts.Length < 2 || !long.TryParse(parts[1], out var itemId))
        {
            return "Lệnh không hợp lệ.";
        }

        var verb = parts[0];
        var quantity = parts.Length > 2 && int.TryParse(parts[2], out var parsed) && parsed > 0 ? parsed : 1;

        switch (verb)
        {
            case "buy":
                return _service.Buy(cityId, itemId, quantity).Message;
            case "sell":
                return _service.Sell(cityId, itemId, quantity).Message;
            case "store":
                _service.Store(cityId, itemId, quantity, out var storeMessage);
                return storeMessage;
            case "take":
                _service.Take(cityId, itemId, quantity, out var takeMessage);
                return takeMessage;
            default:
                return $"Động từ '{verb}' không thuộc DLC market.";
        }
    }
}

/// <summary>
/// dlc.market — the first DLC. It requires three base modules and reaches them only through
/// capabilities, so it can be removed from the product manifest without touching Core.
/// </summary>
public sealed class MarketModule : ISimModule
{
    public void Start(ModuleContext context)
    {
        var items = context.Capabilities.Get<IItemCatalog>(CapabilityIds.ItemCatalog);
        var map = context.Capabilities.Get<IWorldMap>(CapabilityIds.WorldMap);
        var wallet = context.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);
        var bag = context.Capabilities.Get<IBag>(CapabilityIds.PlayerBag);
        var player = context.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);

        var npcs = context.Capabilities.TryGet<INpcRegistry>(CapabilityIds.NpcRegistry, out var nr) ? nr : null;
        var service = new MarketService(context.Db, context.Clock, context.ActionTime, wallet, bag, items, map, context.Log, npcs);
        service.EnsureListings(items, map);

        context.Capabilities.Provide(CapabilityIds.MarketTrade, service);
        context.Capabilities.Provide(CapabilityIds.MarketPortfolio, service);
        context.Capabilities.Provide(CapabilityIds.MarketWarehouse, service);
        context.Capabilities.Provide(CapabilityIds.PlayerStall, service);
        context.Capabilities.Provide(CapabilityIds.NpcMarket, service);

        context.Ui.AddScreen(new StallScreen(service, service, items, map, bag, player, context.Clock, wallet, context.ActionTime, npcs));
        context.Ui.AddScreen(new PlayerStallScreen(service, service, items, map, bag, player, context.Clock, wallet, context.ActionTime));
        context.Ui.AddScreen(new WarehouseScreen(service, items, map, bag, player, context.ActionTime));
        context.Ui.AddCommandHandler(new MarketCommandHandler("market.stall", service, player));
        context.Ui.AddCommandHandler(new MarketCommandHandler("market.player_stall", service, player));
        context.Ui.AddCommandHandler(new MarketCommandHandler("market.warehouse", service, player));

        context.Events.Subscribe<MonthClosed>(closed => service.ChargeMonthlyRent(closed.Date));
        context.Events.Subscribe<MonthClosed>(closed => service.ChargeMonthlyStallRent(closed.Date));
        context.Events.Subscribe<MonthClosed>(closed => service.RestockMonthly(closed.Date));
        context.Events.Subscribe<DayAdvanced>(day => service.SpoilOneDay(day.Date));
        context.Events.Subscribe<DayAdvanced>(day => service.DailyRetailSales(day.Date));
        context.Events.Subscribe<DayAdvanced>(day => service.RecordDailyPrices(day.Date));

        foreach (var invariant in MarketInvariants.All())
        {
            context.AddInvariant(invariant);
        }

        context.Log.Info($"market ready: {items.All.Count} items, {map.Citadels.Count} citadels");
    }
}
