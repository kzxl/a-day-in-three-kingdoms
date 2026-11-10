using SimT.Contracts.Modules;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Power;

/// <summary>
/// dlc.power — the consolidated power & military pillar module.
/// Unifies Politics (9-rank officialdom, warlord factions, regional civil affairs, decree governance)
/// and Military (martial arena, tactical duels, bandit stronghold expeditions, commissary supplies).
/// </summary>
public sealed class PowerModule : ISimModule
{
    public void Start(ModuleContext context)
    {
        var map = context.Capabilities.Get<IWorldMap>(CapabilityIds.WorldMap);
        var registry = context.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var player = context.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);
        var attributes = context.Capabilities.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var attrs) ? attrs : null;
        var wallet = context.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);
        var bag = context.Capabilities.Get<IBag>(CapabilityIds.PlayerBag);
        var items = context.Capabilities.Get<IItemCatalog>(CapabilityIds.ItemCatalog);
        var equipment = context.Capabilities.TryGet<IPlayerEquipment>(CapabilityIds.PlayerEquipment, out var pe) ? pe : null;

        // 1. Politics / Court Service
        var courtService = new CourtService(context.Db, context.Clock, wallet, context.Log, attributes, context.Capabilities)
        {
            Capabilities = context.Capabilities
        };
        _ = courtService.Reputation;
        _ = courtService.AllegianceFactionId;

        context.Capabilities.Provide(CapabilityIds.CourtOffice, courtService);
        context.Ui.AddScreen(new CourtScreen(courtService, player, map));
        context.Ui.AddCommandHandler(new CourtCommandHandler(courtService));
        context.Events.Subscribe<MonthClosed>(closed => courtService.PayStipend(closed.Date));

        // 2. Military Service
        var militaryService = new MilitaryService(
            context.Db,
            context.Clock,
            wallet,
            bag,
            registry,
            map,
            player,
            attributes!,
            items,
            context.Log,
            equipment,
            context.Capabilities);

        context.Capabilities.Provide(CapabilityIds.MilitaryCommand, militaryService);
        context.Ui.AddScreen(new MilitaryScreen(militaryService, registry, map, player, attributes!, bag, items, context.Clock));
        context.Ui.AddCommandHandler(new MilitaryCommandHandler(militaryService));

        var lordTasks = new LordTaskService(context, courtService);
        courtService.LordTasks = lordTasks;
        context.Capabilities.Provide(ILordTasks.CapabilityId, lordTasks);
        context.Events.Subscribe<MonthClosed>(closed => lordTasks.AssignMonth(closed.Date));
        context.Events.Subscribe<MonthClosed>(closed =>
        {
            courtService.SimulateMonthlyFiefAutonomy(closed.Date);
            courtService.SimulateWarlordScrutiny(closed.Date);
            foreach (var citadel in map.Citadels)
            {
                courtService.SimulateMonthlyCityFoodAndFamine(citadel.Id, closed.Date);
                militaryService.SimulateMonthlyArmoryAndProcurement(citadel.Id, closed.Date);
            }
        });
        context.Events.Subscribe<DayAdvanced>(_ => lordTasks.Tick());
        lordTasks.Tick();

        // 3. Geopolitical Conflict Engine
        var conflictEngine = new GeopoliticalConflictEngine(
            context.Db,
            context.Clock,
            map,
            context.Log,
            context.Capabilities);

        context.Capabilities.Provide(CapabilityIds.GeopoliticalWar, conflictEngine);
        context.Events.Subscribe<MonthClosed>(closed =>
        {
            conflictEngine.AdvanceConflictMonth(closed.Date.DayIndex, 0);
        });

        // Invariants
        foreach (var invariant in PowerInvariants.All())
        {
            context.AddInvariant(invariant);
        }

        context.Log.Info($"power pillar ready: politics ({courtService.Offices.Count} offices, {courtService.Factions.Count} factions), military ({militaryService.AllStrongholds().Count} strongholds)");
    }
}
