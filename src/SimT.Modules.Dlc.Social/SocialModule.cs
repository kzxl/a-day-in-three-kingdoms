using SimT.Contracts.Modules;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Social;

/// <summary>
/// dlc.social — Relationship matrix, episodic memory, and retainer system.
/// Allows the player to converse, present gifts, nurture affinity, and recruit retainers.
/// </summary>
public sealed class SocialModule : ISimModule
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

        var service = new SocialService(
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

        context.Capabilities.Provide(CapabilityIds.SocialNetwork, service);

        var screen = new SocialScreen(service, registry, map, player, bag, items, context.Clock, attributes);
        context.Ui.AddScreen(screen);
        context.Ui.AddCommandHandler(new SocialCommandHandler(service, bag, player, items, attributes));

        context.Events.Subscribe<MonthClosed>(month => service.OnMonthClosed(month.Date));

        foreach (var invariant in SocialInvariants.All())
        {
            context.AddInvariant(invariant);
        }

        context.Log.Info("Social network module ready with relationship matrix and retainer management.");
    }
}
