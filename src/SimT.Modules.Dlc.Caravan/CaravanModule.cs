using SimT.Contracts.Modules;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Caravan;

/// <summary>
/// dlc.caravan — freight between settlements. It depends on four base capabilities and reaches
/// them only through the broker, so it can be removed from the product manifest without
/// touching any other module.
/// </summary>
public sealed class CaravanModule : ISimModule
{
    public void Start(ModuleContext context)
    {
        var items = context.Capabilities.Get<IItemCatalog>(CapabilityIds.ItemCatalog);
        var map = context.Capabilities.Get<IWorldMap>(CapabilityIds.WorldMap);
        var wallet = context.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);
        var bag = context.Capabilities.Get<IBag>(CapabilityIds.PlayerBag);
        var player = context.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);
        var npcs = context.Capabilities.TryGet<INpcRegistry>(CapabilityIds.NpcRegistry, out var nr) ? nr : null;

        var freightPerUnit = context.Config.GetLong("freightPerUnit", CaravanService.DefaultFreightPerUnit);
        var service = new CaravanService(context.Db, context.Clock, wallet, bag, map, items, player, context.Ids, context.Log, context.ActionTime, freightPerUnit, npcs);

        context.Capabilities.Provide(CapabilityIds.CaravanGuild, service);
        context.Ui.AddScreen(new CaravanScreen(service, map, bag, player, context.Clock, items, npcs));
        context.Ui.AddCommandHandler(new CaravanCommandHandler(service, player));

        context.Events.Subscribe<DayAdvanced>(day => service.OnDay(day.Date));

        foreach (var invariant in CaravanInvariants.All())
        {
            context.AddInvariant(invariant);
        }

        context.Log.Info($"caravan ready with {items.All.Count} tradeable goods and {map.Citadels.Count} citadels");
    }
}
