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
            context.Capabilities,
            context.Rng,
            context.Campaign?.Seed ?? 0);

        service.SetRumorContext(context.Rng, context.ActionTime);
        context.Capabilities.Provide(CapabilityIds.TavernRumors, (ITavernRumors)service);
        context.Capabilities.Provide(CapabilityIds.HistoricalRumorEvents, (IEventRumorSource)service);
        context.Events.Subscribe<SimT.Contracts.Events.PlayerRelocatedEvent>(e => service.VerifyRumors(e.ToCityId));
        context.Events.Subscribe<DayAdvanced>(day =>
        {
            service.OnDayAdvanced(day.Date);
            service.VerifyRumors();
        });
        context.Events.Subscribe<SimT.Contracts.Events.KarmaEventTriggeredEvent>(_ => service.VerifyRumors());
        context.Capabilities.Provide(CapabilityIds.SocietyChronicle, service);
        context.Capabilities.Provide(CapabilityIds.MerchantRivalry, (IMerchantRivalry)service);
        context.Capabilities.Provide(CapabilityIds.KarmaEngine, (IKarmaEngine)service);
        context.Capabilities.Provide(CapabilityIds.EspionageNetwork, (IEspionageNetwork)service);

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
