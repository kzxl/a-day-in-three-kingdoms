using SimT.Contracts.Data;
using SimT.Contracts.Modules;
using SimT.Contracts.Playing;

namespace SimT.Modules.Base.World;

/// <summary>base.world — settlements and roads, exposed as the <c>world.map</c> capability.</summary>
public sealed class WorldModule : ISimModule
{
    public void Start(ModuleContext context)
    {
        var map = new WorldMap(context.Db);
        context.Capabilities.Provide(CapabilityIds.WorldMap, map);
        context.Log.Info($"map ready with {map.Citadels.Count} citadels");
    }

    private sealed class WorldMap : IWorldMap
    {
        private readonly CitadelDef[] _citadels;
        private readonly Dictionary<long, CitadelDef> _byId;
        private readonly Dictionary<long, RouteStep[]> _routes;

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
        }

        public IReadOnlyList<CitadelDef> Citadels => _citadels;

        public CitadelDef? Find(long citadelId) => _byId.GetValueOrDefault(citadelId);

        public IReadOnlyList<long> RouteNeighbours(long citadelId) =>
            _routes.TryGetValue(citadelId, out var steps)
                ? steps.Select(step => step.To).ToArray()
                : Array.Empty<long>();

        public int TravelDays(long fromCitadelId, long toCitadelId)
        {
            if (!_routes.TryGetValue(fromCitadelId, out var steps))
            {
                return 0;
            }

            foreach (var step in steps)
            {
                if (step.To == toCitadelId)
                {
                    return step.Days;
                }
            }

            return 0;
        }

        private readonly record struct RouteStep(long To, int Days);
    }
}
