using SimT.Contracts.Modules;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Professions;

/// <summary>dlc.professions — day-labour trades with practice tiers, workshops, and crafting orders.</summary>
public sealed class ProfessionModule : ISimModule
{
    public void Start(ModuleContext context)
    {
        var items = context.Capabilities.Get<IItemCatalog>(CapabilityIds.ItemCatalog);
        var wallet = context.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);
        var bag = context.Capabilities.Get<IBag>(CapabilityIds.PlayerBag);
        var player = context.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);
        var attributes = context.Capabilities.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var attr) ? attr : null;

        var service = new ProfessionService(context.Db, context.Clock, wallet, bag, items, context.Log, attributes);
        service.ValidateCatalog();

        context.Capabilities.Provide(CapabilityIds.ProfessionGuild, service);
        context.Capabilities.Provide(CapabilityIds.PlayerEquipment, service);

        context.Ui.AddScreen(new ProfessionScreen(service, items, player));
        context.Ui.AddCommandHandler(new ProfessionCommandHandler(service, player));

        context.Events.Subscribe<MonthClosed>(closed => service.OnMonth(closed.Date));

        foreach (var invariant in ProfessionInvariants.All())
        {
            context.AddInvariant(invariant);
        }

        context.Log.Info($"professions ready with {service.Trades.Count} trades, workshops and equipment slots");
    }
}
