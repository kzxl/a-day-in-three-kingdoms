using SimT.Contracts.Modules;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Politics;

/// <summary>dlc.politics — reputation, 9-rank officialdom, 12 warlord factions, and regional civil affairs.</summary>
public sealed class PoliticsModule : ISimModule
{
    public void Start(ModuleContext context)
    {
        var wallet = context.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);
        var player = context.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);
        var map = context.Capabilities.Get<IWorldMap>(CapabilityIds.WorldMap);

        var service = new CourtService(context.Db, context.Clock, wallet, context.Log);

        // Force a state read at boot: a missing migration must fail here, not mid-game.
        _ = service.Reputation;
        _ = service.AllegianceFactionId;

        context.Capabilities.Provide(CapabilityIds.CourtOffice, service);
        context.Ui.AddScreen(new CourtScreen(service, player, map));
        context.Ui.AddCommandHandler(new CourtCommandHandler(service));
        context.Events.Subscribe<MonthClosed>(closed => service.PayStipend(closed.Date));

        foreach (var invariant in CourtInvariants.All())
        {
            context.AddInvariant(invariant);
        }

        context.Log.Info($"politics ready with {service.Offices.Count} offices and {service.Factions.Count} factions");
    }
}
