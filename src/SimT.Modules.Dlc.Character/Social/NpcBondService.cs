using SimT.Contracts.Data;
using SimT.Contracts.Modules;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Character;

internal sealed class NpcBondService(ModuleContext context) : INpcBonds
{
    private readonly IDbSession _db = context.Db;
    public IReadOnlyList<NpcBond> ForNpc(long npcId) => _db.Query(
        "SELECT npc_a,npc_b,kind,strength,since_date,source FROM kz_npc_bond WHERE npc_a=@id OR npc_b=@id ORDER BY strength DESC,npc_a,npc_b,kind",
        x => new NpcBond(x.GetInt64(0), x.GetInt64(1), x.GetString(2), x.GetInt32(3), GameDateCodec.Parse(x.GetString(4)), x.GetString(5)), DbArgs.One("@id", npcId));

    public void Initialize()
    {
        if (_db.ScalarLong("SELECT COUNT(*) FROM kz_bond_state WHERE key='initialized'") > 0) return;
        var sharedState = context.Rng.State;
        // Use the kernel RNG in a separate initialization domain, preserving existing draws.
        context.Rng.Reseed(sharedState ^ 0x424F4E4453454544UL);
        try
        {
            _db.Transaction(() =>
            {
                var registry = context.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
                var all = registry.All.OrderBy(n => n.Id).ToArray();
                var historical = all.Where(n => ForNpc(n.Id).Any(b => b.Source == "history")).Select(n => n.Id).ToHashSet();
                var eligible = all.Where(n => !historical.Contains(n.Id)).ToArray();
                var targets = eligible.ToDictionary(n => n.Id, _ => context.Rng.Next(1, 4));
                // Give everyone a first bond before filling extra slots; later NPCs cannot be stranded.
                foreach (int pass in new[] { 1, 3 })
                foreach (var npc in eligible)
                {
                    var peers = all.Where(n => n.Id != npc.Id && !historical.Contains(n.Id) &&
                        (n.CitadelId == npc.CitadelId || n.Role == npc.Role)).ToList();
                    int target = pass == 1 ? 1 : targets[npc.Id];
                    while (ForNpc(npc.Id).Count < target && peers.Count > 0)
                    {
                        var index = context.Rng.Next(0, peers.Count);
                        var other = peers[index]; peers.RemoveAt(index);
                        if (ForNpc(other.Id).Count >= 3 || ForNpc(npc.Id).Any(b => b.Other(npc.Id) == other.Id)) continue;
                        Insert(npc.Id, other.Id, context.Rng.Next(0, 5) == 0 ? "rival" : "friend", context.Rng.Next(25, 76), context.Clock.Today);
                    }
                }
                _db.Execute("INSERT INTO kz_bond_state VALUES ('initialized','1')");
            });
        }
        finally
        {
            context.Rng.Reseed(sharedState);
        }
    }

    private void Insert(long a, long b, string kind, int strength, GameDate date) => _db.Execute(
        "INSERT OR IGNORE INTO kz_npc_bond VALUES (@a,@b,@k,@s,@d,'sim')",
        new DbArgs().Add("@a", Math.Min(a,b)).Add("@b",Math.Max(a,b)).Add("@k",kind).Add("@s",strength).Add("@d",GameDateCodec.Format(date)));

    public void OnMonthClosed(GameDate date)
    {
        string stamp = GameDateCodec.Format(date);
        if (_db.ScalarLong("SELECT COUNT(*) FROM kz_bond_state WHERE key=@key", DbArgs.One("@key", "month:" + stamp)) > 0) return;
        _db.Transaction(() =>
        {
            context.Capabilities.TryGet<ISocietyChronicle>(CapabilityIds.SocietyChronicle, out var society);
            var all = context.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry).All.OrderBy(n => n.Id).ToArray();
            var changes = 0;
            int start = all.Length == 0 ? 0 : context.Rng.Next(0, all.Length);
            foreach (var npc in all.Skip(start).Concat(all.Take(start)))
            {
                if (changes >= NpcBondRules.MonthlyChangeLimit) break;
                foreach (var bond in ForNpc(npc.Id).Where(b => b.NpcA == npc.Id && b.Source == "sim"))
                {
                    if (context.Rng.Next(0, 8) != 0) continue;
                    _db.Execute("UPDATE kz_npc_bond SET strength=MAX(0,strength-5) WHERE npc_a=@a AND npc_b=@b AND kind=@k",
                        new DbArgs().Add("@a",bond.NpcA).Add("@b",bond.NpcB).Add("@k",bond.Kind));
                    if (bond.Strength > 5) continue;
                    _db.Execute("DELETE FROM kz_npc_bond WHERE npc_a=@a AND npc_b=@b AND kind=@k",
                        new DbArgs().Add("@a",bond.NpcA).Add("@b",bond.NpcB).Add("@k",bond.Kind));
                    society?.RecordRelationshipEvent(date,npc.Id,$"Duyên cũ phai nhạt: {npc.NameVi} và {all.First(n => n.Id == bond.NpcB).NameVi}");
                    changes++; break;
                }
                if (changes >= NpcBondRules.MonthlyChangeLimit || context.Rng.Next(0, 12) != 0) continue;
                var peers = all.Where(n => n.Id != npc.Id && n.CurrentCitadelId(context.Capabilities) == npc.CurrentCitadelId(context.Capabilities)
                    && !ForNpc(npc.Id).Any(b => b.Other(npc.Id) == n.Id)).ToArray();
                if (peers.Length == 0) continue;
                var friend = peers[context.Rng.Next(0,peers.Length)];
                Insert(npc.Id,friend.Id,"friend",40,date); changes++;
                society?.RecordRelationshipEvent(date,npc.Id,$"Kết giao: {npc.NameVi} và {friend.NameVi} trở thành bạn hữu.");
            }
            _db.Execute("INSERT INTO kz_bond_state VALUES (@key,'1')",DbArgs.One("@key","month:"+stamp));
        });
    }
}
