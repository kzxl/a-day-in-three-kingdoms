using SimT.Contracts.Data;
using SimT.Contracts.Modules;
using SimT.Contracts.Playing;

namespace SimT.Modules.Base.Npc;

/// <summary>
/// base.npc — historical and local characters, exposed as the <c>npc.registry</c> capability.
/// </summary>
public sealed class NpcModule : ISimModule
{
    public void Start(ModuleContext context)
    {
        var registry = new NpcRegistry(context.Db);
        context.Capabilities.Provide(CapabilityIds.NpcRegistry, registry);
        context.Log.Info($"npc registry ready with {registry.All.Count} characters");
    }

    private sealed class NpcRegistry : INpcRegistry
    {
        private readonly NpcDef[] _all;
        private readonly Dictionary<long, NpcDef> _byId;
        private readonly Dictionary<string, NpcDef> _byCode;
        private readonly Dictionary<long, NpcDef[]> _byCitadel;

        public NpcRegistry(IDbSession db)
        {
            _all = db.Query(
                @"SELECT n.id, n.code, n.name_vi, a.name_han, a.style_name_vi, n.role, n.citadel_id,
                         COALESCE(a.war_attr, 50), COALESCE(a.int_attr, 50), COALESCE(a.pol_attr, 50),
                         COALESCE(a.ldr_attr, 50), COALESCE(a.dip_attr, 50), COALESCE(a.com_attr, 50),
                         COALESCE(a.log_attr, 50), COALESCE(a.cra_attr, 50),
                         a.personality, a.bio_vi
                  FROM np_npc n
                  LEFT JOIN np_attribute a ON a.npc_id = n.id
                  ORDER BY n.id",
                row => new NpcDef(
                    row.GetInt64(0),
                    row.GetString(1),
                    row.GetString(2),
                    row.GetStringOrNull(3),
                    row.GetStringOrNull(4),
                    row.GetString(5),
                    row.GetInt64(6),
                    row.GetInt32(7),
                    row.GetInt32(8),
                    row.GetInt32(9),
                    row.GetInt32(10),
                    row.GetInt32(11),
                    row.GetInt32(12),
                    row.GetInt32(13),
                    row.GetInt32(14),
                    row.GetStringOrNull(15),
                    row.GetStringOrNull(16)))
                .ToArray();

            _byId = new Dictionary<long, NpcDef>(_all.Length);
            _byCode = new Dictionary<string, NpcDef>(_all.Length, StringComparer.OrdinalIgnoreCase);
            var byCitadel = new Dictionary<long, List<NpcDef>>();

            foreach (var npc in _all)
            {
                _byId[npc.Id] = npc;
                _byCode[npc.Code] = npc;

                if (!byCitadel.TryGetValue(npc.CitadelId, out var list))
                {
                    list = new List<NpcDef>();
                    byCitadel[npc.CitadelId] = list;
                }
                list.Add(npc);
            }

            _byCitadel = byCitadel.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());
        }

        public IReadOnlyList<NpcDef> All => _all;

        public NpcDef? Find(long npcId) => _byId.GetValueOrDefault(npcId);

        public NpcDef? FindByCode(string code) => _byCode.GetValueOrDefault(code);

        public IReadOnlyList<NpcDef> FindByCitadel(long citadelId) =>
            _byCitadel.TryGetValue(citadelId, out var list)
                ? list
                : Array.Empty<NpcDef>();
    }
}
