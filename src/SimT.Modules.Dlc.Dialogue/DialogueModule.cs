using SimT.Contracts.Modules;
using SimT.Contracts.Playing;

namespace SimT.Modules.Dlc.Dialogue;

public sealed class DialogueModule : ISimModule
{
    public void Start(ModuleContext context)
    {
        var service = new DialogueService(context);
        context.Capabilities.Provide(CapabilityIds.Dialogue, service);
    }
}
