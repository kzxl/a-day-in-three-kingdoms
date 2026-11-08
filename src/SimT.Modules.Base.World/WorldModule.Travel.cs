using SimT.Contracts.Playing;

namespace SimT.Modules.Base.World;

public sealed partial class WorldModule
{
    private sealed partial class WorldMap
    {
        public TravelQuote QuoteTravel(long fromCitadelId, long toCitadelId, TravelMode mode)
        {
            if (fromCitadelId == toCitadelId)
            {
                return new TravelQuote(fromCitadelId, toCitadelId, 0, 0, 0, 0, 0, new[] { fromCitadelId }, "Tại Chỗ");
            }

            var path = FindShortestPathNodes(fromCitadelId, toCitadelId);
            var baseDays = CalculatePathDays(path);
            if (baseDays <= 0)
            {
                var originalPath = FindShortestPathNodes(fromCitadelId, toCitadelId, true);
                var reason = Enumerable.Range(0, Math.Max(0, originalPath.Count - 1))
                    .Select(i => GetRouteStatus(originalPath[i], originalPath[i + 1]).BlockedReasonVi)
                    .FirstOrDefault(r => !string.IsNullOrWhiteSpace(r));
                return new TravelQuote(fromCitadelId, toCitadelId, 0, 0, 0, 0, 0,
                    Array.Empty<long>(), "Không thể lên đường", TravelLuggage.ForMode(mode), 0,
                    string.IsNullOrWhiteSpace(reason) ? "Không có đường bộ thông thương đến thành này." : reason);
            }
            var danger = Enumerable.Range(0, path.Count - 1)
                .Max(i => GetRouteStatus(path[i], path[i + 1]).Danger);

            int actualDays;
            long fee;
            int rations;
            int fatigue;
            string modeName;

            switch (mode)
            {
                case TravelMode.Horse:
                    actualDays = Math.Max(1, (int)Math.Ceiling(baseDays * 0.65));
                    fee = baseDays * 15;
                    rations = actualDays;
                    fatigue = actualDays * 4;
                    modeName = $"Dịch Mã (Cưỡi Ngựa · {TravelLuggage.HorseCapacity} đv)";
                    break;

                case TravelMode.Caravan:
                    actualDays = baseDays;
                    fee = baseDays * 25;
                    rations = baseDays;
                    fatigue = baseDays * 3;
                    modeName = $"Tiêu Cục (Hộ Tiêu · {TravelLuggage.CaravanCapacity} đv)";
                    break;

                case TravelMode.Walk:
                default:
                    actualDays = baseDays;
                    fee = 0;
                    rations = baseDays;
                    fatigue = baseDays * 7;
                    modeName = $"Bộ Hành (Đi Bộ · {TravelLuggage.WalkCapacity} đv)";
                    break;
            }

            return new TravelQuote(
                fromCitadelId, toCitadelId, baseDays, actualDays, fee, rations, fatigue,
                path, modeName, TravelLuggage.ForMode(mode), danger);
        }

        private int CalculatePathDays(IReadOnlyList<long> path)
        {
            if (path.Count <= 1) return 0;
            var total = 0;
            for (var i = 0; i < path.Count - 1; i++)
            {
                var d = TravelDays(path[i], path[i + 1]);
                total += (d > 0 ? d : 2);
            }
            return total;
        }

        private IReadOnlyList<long> FindShortestPathNodes(long fromId, long toId, bool ignoreBlocks = false)
        {
            var dist = new Dictionary<long, int> { [fromId] = 0 };
            var prev = new Dictionary<long, long>();
            var visited = new HashSet<long>();

            while (true)
            {
                long current = -1;
                var bestDist = int.MaxValue;
                foreach (var (node, d) in dist)
                {
                    if (!visited.Contains(node) && d < bestDist)
                    {
                        bestDist = d;
                        current = node;
                    }
                }

                if (current < 0 || current == toId)
                {
                    break;
                }

                visited.Add(current);
                if (!_routes.TryGetValue(current, out var steps))
                {
                    continue;
                }

                foreach (var step in steps)
                {
                    if (!ignoreBlocks && IsRouteBlocked(current, step.To))
                    {
                        continue;
                    }

                    var w = ignoreBlocks ? Math.Max(1, step.Days) : Math.Max(1, TravelDays(current, step.To));
                    var candidate = bestDist + w;
                    if (!dist.TryGetValue(step.To, out var existing) || candidate < existing)
                    {
                        dist[step.To] = candidate;
                        prev[step.To] = current;
                    }
                }
            }

            if (!prev.ContainsKey(toId))
            {
                return Array.Empty<long>();
            }

            var path = new List<long>();
            var nodeStep = toId;
            while (nodeStep != fromId)
            {
                path.Add(nodeStep);
                nodeStep = prev[nodeStep];
            }
            path.Add(fromId);
            path.Reverse();
            return path;
        }

    }
}
