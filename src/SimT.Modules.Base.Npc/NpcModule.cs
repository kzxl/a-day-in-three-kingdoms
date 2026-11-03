using SimT.Contracts.Modules;

namespace SimT.Modules.Base.Npc;

/// <summary>
/// base.npc — placeholder for the character lifecycle DLC. It ships a table and a module so
/// the composition and migration machinery is exercised, but exposes no capability yet.
/// </summary>
public sealed class NpcModule : ISimModule
{
    public void Start(ModuleContext context)
    {
        var count = context.Db.ScalarLong("SELECT COUNT(*) FROM np_npc");
        context.Log.Info($"npc stub ready with {count} characters");
    }
}
