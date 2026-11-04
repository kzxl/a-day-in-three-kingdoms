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

        var map = context.Capabilities.Get<IWorldMap>(CapabilityIds.WorldMap);
        var playerState = context.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);
        context.Ui.AddScreen(new NpcDirectoryScreen(registry, map, playerState, context.Capabilities));
        context.Ui.AddCommandHandler(new NpcCommandHandler(registry));
    }

    private sealed class NpcRegistry : INpcRegistry
    {
        private readonly IDbSession _db;
        private readonly List<NpcDef> _all;
        private readonly Dictionary<long, NpcDef> _byId;
        private readonly Dictionary<string, NpcDef> _byCode;
        private readonly Dictionary<long, List<NpcDef>> _byCitadel;
        private readonly object _gate = new();

        public NpcRegistry(IDbSession db)
        {
            _db = db;

            var skillRows = db.Query(
                @"SELECT npc_id, code, name_vi, category, tier, description, effect_summary
                  FROM np_skill
                  ORDER BY tier DESC, id ASC",
                row => (
                    NpcId: row.GetInt64(0),
                    Skill: new NpcSkillDef(
                        row.GetString(1),
                        row.GetString(2),
                        row.GetString(3),
                        row.GetInt32(4),
                        row.GetString(5),
                        row.GetString(6))));

            var skillsByNpc = new Dictionary<long, List<NpcSkillDef>>();
            foreach (var item in skillRows)
            {
                if (!skillsByNpc.TryGetValue(item.NpcId, out var list))
                {
                    list = new List<NpcSkillDef>();
                    skillsByNpc[item.NpcId] = list;
                }
                list.Add(item.Skill);
            }

            _all = db.Query(
                @"SELECT n.id, n.code, n.name_vi, a.name_han, a.style_name_vi, n.role, n.citadel_id,
                         COALESCE(a.war_attr, 50), COALESCE(a.ldr_attr, 50), COALESCE(a.int_attr, 50),
                         COALESCE(a.pol_attr, 50), COALESCE(a.dip_attr, 50), COALESCE(a.com_attr, 50),
                         COALESCE(a.log_attr, 50), COALESCE(a.cra_attr, 50), COALESCE(a.con_attr, 50),
                         COALESCE(a.wil_attr, 50), COALESCE(a.prc_attr, 50), COALESCE(a.lck_attr, 50),
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
                    new CharacterAttributes(
                        row.GetInt32(7),
                        row.GetInt32(8),
                        row.GetInt32(9),
                        row.GetInt32(10),
                        row.GetInt32(11),
                        row.GetInt32(12),
                        row.GetInt32(13),
                        row.GetInt32(14),
                        row.GetInt32(15),
                        row.GetInt32(16),
                        row.GetInt32(17),
                        row.GetInt32(18)),
                    row.GetStringOrNull(19),
                    row.GetStringOrNull(20),
                    skillsByNpc.TryGetValue(row.GetInt64(0), out var sList) ? sList : Array.Empty<NpcSkillDef>()))
                .ToList();

            _byId = new Dictionary<long, NpcDef>(_all.Count);
            _byCode = new Dictionary<string, NpcDef>(_all.Count, StringComparer.OrdinalIgnoreCase);
            _byCitadel = new Dictionary<long, List<NpcDef>>();

            foreach (var npc in _all)
            {
                _byId[npc.Id] = npc;
                _byCode[npc.Code] = npc;

                if (!_byCitadel.TryGetValue(npc.CitadelId, out var list))
                {
                    list = new List<NpcDef>();
                    _byCitadel[npc.CitadelId] = list;
                }
                list.Add(npc);
            }
        }

        public IReadOnlyList<NpcDef> All
        {
            get
            {
                lock (_gate)
                {
                    return _all.ToArray();
                }
            }
        }

        public NpcDef? Find(long npcId)
        {
            lock (_gate)
            {
                return _byId.GetValueOrDefault(npcId);
            }
        }

        public NpcDef? FindByCode(string code)
        {
            lock (_gate)
            {
                return _byCode.GetValueOrDefault(code);
            }
        }

        public IReadOnlyList<NpcDef> FindByCitadel(long citadelId)
        {
            lock (_gate)
            {
                return _byCitadel.TryGetValue(citadelId, out var list)
                    ? list.ToArray()
                    : Array.Empty<NpcDef>();
            }
        }

        public void UpdateCitadel(long npcId, long newCitadelId)
        {
            lock (_gate)
            {
                if (!_byId.TryGetValue(npcId, out var existing))
                {
                    return;
                }

                if (existing.CitadelId == newCitadelId)
                {
                    return;
                }

                var oldCitadelId = existing.CitadelId;
                var updated = existing with { CitadelId = newCitadelId };

                _byId[npcId] = updated;
                _byCode[updated.Code] = updated;

                for (var i = 0; i < _all.Count; i++)
                {
                    if (_all[i].Id == npcId)
                    {
                        _all[i] = updated;
                        break;
                    }
                }

                _db.Execute(
                    "UPDATE np_npc SET citadel_id = @citId WHERE id = @id",
                    new DbArgs().Add("@citId", newCitadelId).Add("@id", npcId));

                if (_byCitadel.TryGetValue(oldCitadelId, out var oldList))
                {
                    oldList.RemoveAll(n => n.Id == npcId);
                }

                if (!_byCitadel.TryGetValue(newCitadelId, out var newList))
                {
                    newList = new List<NpcDef>();
                    _byCitadel[newCitadelId] = newList;
                }
                newList.Add(updated);
            }
        }

        public NpcDef RegisterDynamicNpc(
            string code,
            string nameVi,
            string role,
            long citadelId,
            CharacterAttributes attributes,
            string? personality = null,
            string? bioVi = null,
            IReadOnlyList<NpcSkillDef>? skills = null)
        {
            lock (_gate)
            {
                var newId = _db.ScalarLong("SELECT COALESCE(MAX(id), 0) + 1 FROM np_npc");
                var skillList = skills ?? Array.Empty<NpcSkillDef>();

                _db.Execute(
                    "INSERT INTO np_npc (id, code, name_vi, role, citadel_id) VALUES (@id, @code, @name, @role, @citId)",
                    new DbArgs()
                        .Add("@id", newId)
                        .Add("@code", code)
                        .Add("@name", nameVi)
                        .Add("@role", role)
                        .Add("@citId", citadelId));

                _db.Execute(
                    @"INSERT INTO np_attribute (npc_id, war_attr, ldr_attr, int_attr, pol_attr, dip_attr, com_attr, log_attr, cra_attr, con_attr, wil_attr, prc_attr, lck_attr, personality, bio_vi)
                      VALUES (@id, @war, @ldr, @int, @pol, @dip, @com, @log, @cra, @con, @wil, @prc, @lck, @personality, @bio)",
                    new DbArgs()
                        .Add("@id", newId)
                        .Add("@war", attributes.War)
                        .Add("@ldr", attributes.Ldr)
                        .Add("@int", attributes.Int)
                        .Add("@pol", attributes.Pol)
                        .Add("@dip", attributes.Dip)
                        .Add("@com", attributes.Com)
                        .Add("@log", attributes.Log)
                        .Add("@cra", attributes.Cra)
                        .Add("@con", attributes.Con)
                        .Add("@wil", attributes.Wil)
                        .Add("@prc", attributes.Prc)
                        .Add("@lck", attributes.Lck)
                        .Add("@personality", personality)
                        .Add("@bio", bioVi));

                foreach (var sk in skillList)
                {
                    _db.Execute(
                        @"INSERT OR IGNORE INTO np_skill (npc_id, code, name_vi, category, tier, description, effect_summary)
                          VALUES (@npcId, @code, @name, @category, @tier, @desc, @effect)",
                        new DbArgs()
                            .Add("@npcId", newId)
                            .Add("@code", sk.Code)
                            .Add("@name", sk.NameVi)
                            .Add("@category", sk.Category)
                            .Add("@tier", sk.Tier)
                            .Add("@desc", sk.Description)
                            .Add("@effect", sk.EffectSummary));
                }

                var npc = new NpcDef(
                    newId,
                    code,
                    nameVi,
                    null,
                    null,
                    role,
                    citadelId,
                    attributes,
                    personality,
                    bioVi,
                    skillList);

                _all.Add(npc);
                _byId[newId] = npc;
                _byCode[code] = npc;

                if (!_byCitadel.TryGetValue(citadelId, out var citList))
                {
                    citList = new List<NpcDef>();
                    _byCitadel[citadelId] = citList;
                }
                citList.Add(npc);

                return npc;
            }
        }
    }
}
