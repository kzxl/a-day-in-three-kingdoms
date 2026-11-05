using SimT.Contracts.Modules;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Society;

/// <summary>
/// dlc.society — Living World Engine & Autonomous NPC Simulation.
/// NPCs actively learn, drill martial skills, conduct commerce, govern cities,
/// travel between settlements, and record their deeds in the realm's chronicle.
/// </summary>
public sealed class SocietyModule : ISimModule
{
    public void Start(ModuleContext context)
    {
        var map = context.Capabilities.Get<IWorldMap>(CapabilityIds.WorldMap);
        var registry = context.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var player = context.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);

        var service = new SocietyService(
            context.Db,
            context.Clock,
            registry,
            map,
            player,
            context.Log,
            context.Capabilities);

        context.Capabilities.Provide(CapabilityIds.SocietyChronicle, service);
        context.Capabilities.Provide(CapabilityIds.MerchantRivalry, (IMerchantRivalry)service);
        context.Capabilities.Provide(CapabilityIds.KarmaEngine, (IKarmaEngine)service);

        var screen = new SocietyChronicleScreen(service, map, player, context.Clock);
        context.Ui.AddScreen(screen);
        context.Ui.AddCommandHandler(new SocietyCommandHandler(screen));

        context.Events.Subscribe<MonthClosed>(month => service.OnMonthClosed(month.Date));

        foreach (var invariant in SocietyInvariants.All())
        {
            context.AddInvariant(invariant);
        }

        context.Log.Info($"Society module ready with {registry.All.Count} living NPCs across {map.Citadels.Count} citadels.");
    }
}
