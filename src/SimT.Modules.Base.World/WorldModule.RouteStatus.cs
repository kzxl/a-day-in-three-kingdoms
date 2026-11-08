using SimT.Contracts.Modules;
using SimT.Contracts.Playing;

namespace SimT.Modules.Base.World;

public sealed partial class WorldModule
{
    private sealed partial class WorldMap
    {
        private readonly ICapabilityBroker? _capabilities;

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
            var danger = Math.Clamp(Math.Max(GetEcosystem(fromCitadelId).DangerLevel,
                GetEcosystem(toCitadelId).DangerLevel) - 1, 0, 3);
            if (_capabilities is not null &&
                _capabilities.TryGet<IMilitaryCommand>(CapabilityIds.MilitaryCommand, out var military) && military is not null &&
                military.AllStrongholds().Any(s => (s.CitadelId == fromCitadelId || s.CitadelId == toCitadelId) && s.ClearedCount == 0))
                danger = Math.Max(2, danger);
            if (!TryGetActiveRouteHazard(fromCitadelId, toCitadelId, out var hazard))
                return new(danger, "");
            return new(Math.Max(danger, Math.Clamp((hazard.SeverityPermille - 1000) / 400 + 1, 1, 3)),
                hazard.HazardType == "route_block" ? hazard.NoteVi : "");
        }
    }
}
