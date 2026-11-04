using SimT.Contracts.Modules;
using SimT.Contracts.Playing;

namespace SimT.Modules.Dlc.Military;

/// <summary>
/// dlc.military — Martial arena, bandit expeditions, and military commissary requisitions.
/// </summary>
public sealed class MilitaryModule : ISimModule
{
    public void Start(ModuleContext context)
    {
        var map = context.Capabilities.Get<IWorldMap>(CapabilityIds.WorldMap);
        var registry = context.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var player = context.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);
        var attributes = context.Capabilities.Get<IPlayerAttributes>(CapabilityIds.PlayerAttributes);
        var wallet = context.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);
        var bag = context.Capabilities.Get<IBag>(CapabilityIds.PlayerBag);
        var items = context.Capabilities.Get<IItemCatalog>(CapabilityIds.ItemCatalog);

        var service = new MilitaryService(
            context.Db,
            context.Clock,
            wallet,
            bag,
            registry,
            map,
            player,
            attributes,
            items,
            context.Log);

        context.Capabilities.Provide(CapabilityIds.MilitaryCommand, service);

        var screen = new MilitaryScreen(service, registry, map, player, attributes, bag, items, context.Clock);
        context.Ui.AddScreen(screen);
        context.Ui.AddCommandHandler(new MilitaryCommandHandler(service));

        foreach (var invariant in MilitaryInvariants.All())
        {
            context.AddInvariant(invariant);
        }

        context.Log.Info($"Military warfare module ready with {service.AllStrongholds().Count} bandit strongholds.");
    }
}
