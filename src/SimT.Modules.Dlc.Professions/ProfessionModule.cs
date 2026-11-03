using SimT.Contracts.Modules;
using SimT.Contracts.Playing;

namespace SimT.Modules.Dlc.Professions;

/// <summary>dlc.professions — day-labour trades with practice tiers.</summary>
public sealed class ProfessionModule : ISimModule
{
    public void Start(ModuleContext context)
    {
        var items = context.Capabilities.Get<IItemCatalog>(CapabilityIds.ItemCatalog);
        var wallet = context.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);
        var bag = context.Capabilities.Get<IBag>(CapabilityIds.PlayerBag);
        var player = context.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);

        var service = new ProfessionService(context.Db, context.Clock, wallet, bag, items, context.Log);
        service.ValidateCatalog();

        context.Capabilities.Provide(CapabilityIds.ProfessionGuild, service);
        context.Ui.AddScreen(new ProfessionScreen(service, items, player));
        context.Ui.AddCommandHandler(new ProfessionCommandHandler(service, player));

        foreach (var invariant in ProfessionInvariants.All())
        {
            context.AddInvariant(invariant);
        }

        context.Log.Info($"professions ready with {service.Trades.Count} trades");
    }
}
