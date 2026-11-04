using SimT.Contracts.Modules;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Cultivation;

/// <summary>dlc.cultivation — rented plots, seasonal sowing, harvest and monthly land rent.</summary>
public sealed class CultivationModule : ISimModule
{
    public void Start(ModuleContext context)
    {
        var items = context.Capabilities.Get<IItemCatalog>(CapabilityIds.ItemCatalog);
        var map = context.Capabilities.Get<IWorldMap>(CapabilityIds.WorldMap);
        var wallet = context.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);
        var bag = context.Capabilities.Get<IBag>(CapabilityIds.PlayerBag);
        var player = context.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);

        var service = new FarmService(context.Db, context.Clock, wallet, bag, items, context.Ids, context.ActionTime, context.Log);
        service.ValidateCrops();
        service.EnsurePlots(map);

        context.Capabilities.Provide(CapabilityIds.CultivationEstate, service);
        context.Ui.AddScreen(new FarmScreen(service, map, player, context.Clock));
        context.Ui.AddCommandHandler(new FarmCommandHandler(service, player));

        context.Events.Subscribe<DayAdvanced>(day => service.OnDay(day.Date));
        context.Events.Subscribe<MonthClosed>(closed => service.ChargeMonthlyRent(closed.Date));
        context.Events.Subscribe<MonthClosed>(closed => service.RecoverSoil(closed.Date));

        foreach (var invariant in FarmInvariants.All())
        {
            context.AddInvariant(invariant);
        }

        context.Log.Info($"cultivation ready with {service.Plots.Count} plots over {map.Citadels.Count} citadels");
    }
}
