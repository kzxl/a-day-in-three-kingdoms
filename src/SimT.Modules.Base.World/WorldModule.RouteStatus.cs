using SimT.Contracts.Playing;

namespace SimT.Modules.Base.World;

public sealed partial class WorldModule
{
    private sealed partial class WorldMap
    {
        public void SetRouteHazard(long fromCitadelId, long toCitadelId, int dangerDelta,
            bool isBlocked, long expiresDayIndex, string? reasonVi = null)
        {
            var start = (int)(_clock?.Today.DayIndex ?? 0);
            SetRouteHazard(fromCitadelId, toCitadelId, isBlocked ? "route_block" : "route_danger",
                start, (int)Math.Max(1, expiresDayIndex - start), 1000 + Math.Max(0, dangerDelta) * 10,
                reasonVi ?? (isBlocked ? "Chiến sự phong tỏa tuyến đường." : "Chiến sự dọc tuyến đường."));
        }

        public RouteStatus GetRouteStatus(long fromCitadelId, long toCitadelId)
        {
            // City danger already prices in local bandits; a road is only as risky as its worse end (0-2).
            // Every stronghold starts uncleared, so counting them again here painted the whole map red.
            var danger = Math.Clamp(Math.Max(GetEcosystem(fromCitadelId).DangerLevel,
                GetEcosystem(toCitadelId).DangerLevel) - 1, 0, 3);
            if (!TryGetActiveRouteHazard(fromCitadelId, toCitadelId, out var hazard))
                return new(danger, "");
            return new(Math.Max(danger, Math.Clamp((hazard.SeverityPermille - 1000) / 400 + 1, 1, 3)),
                hazard.HazardType == "route_block" ? hazard.NoteVi : "");
        }
    }
}
