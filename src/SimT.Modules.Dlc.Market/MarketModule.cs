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

        var separator = commandId.IndexOf(':');
        if (separator <= 0 || !long.TryParse(commandId[(separator + 1)..], out var itemId))
        {
            return "Lệnh không hợp lệ.";
        }

        var verb = commandId[..separator];
        switch (verb)
        {
            case "buy":
                return _service.Buy(cityId, itemId, 1).Message;
            case "sell":
                return _service.Sell(cityId, itemId, 1).Message;
            case "store":
                _service.Store(cityId, itemId, 1, out var storeMessage);
                return storeMessage;
            case "take":
                _service.Take(cityId, itemId, 1, out var takeMessage);
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

        var service = new MarketService(context.Db, context.Clock, wallet, bag, items, map, context.Log);
        service.EnsureListings(items, map);

        context.Capabilities.Provide(CapabilityIds.MarketTrade, service);
        context.Capabilities.Provide(CapabilityIds.MarketWarehouse, service);

        context.Ui.AddScreen(new StallScreen(service, items, map, bag, player, context.Clock));
        context.Ui.AddScreen(new WarehouseScreen(service, items, map, bag, player));
        context.Ui.AddCommandHandler(new MarketCommandHandler("market.stall", service, player));
        context.Ui.AddCommandHandler(new MarketCommandHandler("market.warehouse", service, player));

        context.Events.Subscribe<MonthClosed>(closed => service.ChargeMonthlyRent(closed.Date));
        context.Events.Subscribe<MonthClosed>(closed => service.RestockMonthly(closed.Date));
        context.Events.Subscribe<DayAdvanced>(day => service.SpoilOneDay(day.Date));

        foreach (var invariant in MarketInvariants.All())
        {
            context.AddInvariant(invariant);
        }

        context.Log.Info($"market ready: {items.All.Count} items, {map.Citadels.Count} citadels");
    }
}
