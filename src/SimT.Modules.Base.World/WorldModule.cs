using SimT.Contracts.Data;
using SimT.Contracts.Modules;
using SimT.Contracts.Playing;

namespace SimT.Modules.Base.World;

/// <summary>base.world — settlements, roads, and sublocations, exposed as the <c>world.map</c> capability.</summary>
public sealed class WorldModule : ISimModule
{
    public void Start(ModuleContext context)
    {
        var map = new WorldMap(context.Db);
        context.Capabilities.Provide(CapabilityIds.WorldMap, map);
        context.Ui.AddScreen(new WorldMapScreen(map, context.Capabilities, context.Clock));
        context.Ui.AddCommandHandler(new WorldMapCommandHandler(map, context.Capabilities, context.Clock));
        context.Log.Info($"map ready with {map.Citadels.Count} citadels and WorldMapScreen registered");
    }

    private sealed class WorldMap : IWorldMap
    {
        private readonly CitadelDef[] _citadels;
        private readonly Dictionary<long, CitadelDef> _byId;
        private readonly Dictionary<long, RouteStep[]> _routes;
        private readonly Dictionary<long, long[]> _neighbours;
        private readonly int[,] _travelMatrix;
        private readonly Dictionary<long, SublocationDef[]> _sublocations;

        public WorldMap(IDbSession db)
        {
            _citadels = db.Query(
                    "SELECT id, code, name_vi, name_en, region, price_factor_permille FROM wd_citadel ORDER BY id",
                    row => new CitadelDef(
                        row.GetInt64(0),
                        row.GetString(1),
                        row.GetString(2),
                        row.GetString(3),
                        row.GetString(4),
                        row.GetInt32(5)))
                .ToArray();

            _byId = new Dictionary<long, CitadelDef>(_citadels.Length);
            foreach (var citadel in _citadels)
            {
                _byId[citadel.Id] = citadel;
            }

            var routes = new Dictionary<long, List<RouteStep>>();
            foreach (var step in db.Query(
                         "SELECT citadel_id, other_id, travel_days FROM wd_route ORDER BY citadel_id, other_id",
                         row => (From: row.GetInt64(0), To: row.GetInt64(1), Days: row.GetInt32(2))))
            {
                if (!routes.TryGetValue(step.From, out var list))
                {
                    list = new List<RouteStep>();
                    routes[step.From] = list;
                }

                list.Add(new RouteStep(step.To, step.Days));
            }

            _routes = routes.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());
            _neighbours = _routes.ToDictionary(pair => pair.Key, pair => pair.Value.Select(s => s.To).ToArray());

            var maxId = _citadels.Length > 0 ? (int)_citadels.Max(c => c.Id) : 0;
            _travelMatrix = new int[maxId + 1, maxId + 1];
            foreach (var (from, steps) in _routes)
            {
                if (from <= maxId)
                {
                    foreach (var step in steps)
                    {
                        if (step.To <= maxId)
                        {
                            _travelMatrix[from, step.To] = step.Days;
                        }
                    }
                }
            }

            var sublocations = new Dictionary<long, List<SublocationDef>>();
            foreach (var sub in db.Query(
                         "SELECT id, citadel_id, code, name_vi, kind FROM wd_sublocation ORDER BY citadel_id, sort_order",
                         row => new SublocationDef(
                             row.GetInt64(0),
                             row.GetInt64(1),
                             row.GetString(2),
                             row.GetString(3),
                             row.GetString(4))))
            {
                if (!sublocations.TryGetValue(sub.CitadelId, out var list))
                {
                    list = new List<SublocationDef>();
                    sublocations[sub.CitadelId] = list;
                }

                list.Add(sub);
            }

            _sublocations = sublocations.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());
        }

        public IReadOnlyList<CitadelDef> Citadels => _citadels;

        public CitadelDef? Find(long citadelId) => _byId.GetValueOrDefault(citadelId);

        public IReadOnlyList<long> RouteNeighbours(long citadelId) =>
            _neighbours.TryGetValue(citadelId, out var list)
                ? list
                : Array.Empty<long>();

        public int TravelDays(long fromCitadelId, long toCitadelId)
        {
            if (fromCitadelId >= 0 && fromCitadelId < _travelMatrix.GetLength(0) &&
                toCitadelId >= 0 && toCitadelId < _travelMatrix.GetLength(1))
            {
                return _travelMatrix[fromCitadelId, toCitadelId];
            }

            return 0;
        }

        public IReadOnlyList<SublocationDef> Sublocations(long citadelId) =>
            _sublocations.TryGetValue(citadelId, out var list)
                ? list
                : Array.Empty<SublocationDef>();

        private readonly record struct RouteStep(long To, int Days);
    }
}
