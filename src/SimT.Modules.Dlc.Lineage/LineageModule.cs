using SimT.Contracts.Modules;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Lineage;

/// <summary>dlc.lineage — marriage, children, succession and monthly household upkeep.</summary>
public sealed class LineageModule : ISimModule
{
    public void Start(ModuleContext context)
    {
        var wallet = context.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);

        var service = new LineageService(context.Db, context.Clock, wallet, context.Rng, context.Ids, context.Log);

        context.Capabilities.Provide(CapabilityIds.LineageHouse, service);
        context.Ui.AddScreen(new FamilyScreen(service));
        context.Ui.AddCommandHandler(new FamilyCommandHandler(service));
        context.Events.Subscribe<MonthClosed>(closed => service.ChargeMonthly(closed.Date));

        foreach (var invariant in LineageInvariants.All())
        {
            context.AddInvariant(invariant);
        }

        context.Log.Info($"lineage ready (married={service.IsMarried}, children={service.ChildCount})");
    }
}
