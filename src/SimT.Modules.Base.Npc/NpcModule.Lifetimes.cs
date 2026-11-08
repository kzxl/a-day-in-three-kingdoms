using SimT.Contracts.Data;
using SimT.Contracts.Playing;
namespace SimT.Modules.Base.Npc;

public sealed partial class NpcModule
{
    private sealed partial class NpcRegistry
    {
        private sealed record Lifetime(long Id, int? Birth, int? Death, long Home);
        private Dictionary<long, Lifetime> _lifetimes = new();
        private int _year;
        private bool _lifetimeEnabled;
        private bool IsAvailable(NpcDef npc) => !_lifetimeEnabled ||
            !_lifetimes.TryGetValue(npc.Id, out var life) || !life.Birth.HasValue || _year >= life.Birth + 15;

        public bool IsPresent(long npcId)
        {
            lock (_gate) return _byId.TryGetValue(npcId, out var npc) && IsAvailable(npc);
        }

        public IReadOnlyList<NpcLifetimeDeath> Deaths => !_lifetimeEnabled ? [] : _lifetimes.Values
            .Where(l => l.Death.HasValue && _year > l.Death && _byId[l.Id].IsDeceasedFlag)
            .Select(l => new NpcLifetimeDeath(l.Id, _byId[l.Id].NameVi, _byId[l.Id].CitadelId, l.Death!.Value)).ToArray();

        public void InitializeLifetimes(int year)
        {
            _lifetimeEnabled = _db.ScalarLong("SELECT enabled FROM np_lifetime_campaign WHERE id=1") == 1;
            _lifetimes = _db.Query("SELECT npc_id, birth_year, death_year, home_citadel_id FROM np_lifetime",
                r => new Lifetime(r.GetInt64(0), r.IsDbNull(1) ? null : r.GetInt32(1),
                    r.IsDbNull(2) ? null : r.GetInt32(2), r.GetInt64(3))).ToDictionary(l => l.Id);
            _year = year;
            if (_lifetimeEnabled)
            {
                foreach (var life in _lifetimes.Values.Where(l => l.Birth.HasValue && year < l.Birth + 15))
                    UpdateCitadel(life.Id, life.Home);
                AdvanceLifetimes(year);
            }
        }

        public void AdvanceLifetimes(int year)
        {
            _year = year;
            if (!_lifetimeEnabled) return;
            lock (_gate)
            {
                foreach (var life in _lifetimes.Values.Where(l => l.Death.HasValue && year > l.Death))
                    if (!_byId[life.Id].IsDeceasedFlag)
                        MarkDeceased(life.Id, $"Qua đời năm {life.Death}");
            }
        }
    }
}
