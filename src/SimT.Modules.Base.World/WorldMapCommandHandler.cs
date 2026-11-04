using SimT.Contracts.Modules;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;
using SimT.Contracts.Ui;

namespace SimT.Modules.Base.World;

/// <summary>
/// Command handler for World Map interactions (inter-city travel and market scouting).
/// </summary>
internal sealed class WorldMapCommandHandler : IScreenCommandHandler
{
    private readonly IWorldMap _map;
    private readonly ICapabilityBroker _broker;
    private readonly IGameClock _clock;

    public WorldMapCommandHandler(IWorldMap map, ICapabilityBroker broker, IGameClock clock)
    {
        _map = map;
        _broker = broker;
        _clock = clock;
    }

    public string ScreenId => "world.map";

    public string? Execute(string commandId)
    {
        if (commandId.StartsWith("inspect:", StringComparison.Ordinal))
        {
            if (long.TryParse(commandId["inspect:".Length..], out var cityId))
            {
                var city = _map.Find(cityId);
                return city is not null
                    ? $"Đã định vị {city.NameVi}. Xem thông tin chi tiết, đặc sản và danh sĩ tại thanh tra bên phải."
                    : "Không tìm thấy thành trì.";
            }
        }

        if (commandId.StartsWith("travel:", StringComparison.Ordinal))
        {
            if (!long.TryParse(commandId["travel:".Length..], out var targetId))
            {
                return "Mã thành trì không hợp lệ.";
            }

            if (!_broker.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var player) || player is null)
            {
                return "Không xác định được trạng thái người chơi.";
            }

            if (player.CurrentCityId == targetId)
            {
                return "Bạn hiện đã đang ở tại thành này rồi!";
            }

            var targetCity = _map.Find(targetId);
            if (targetCity is null)
            {
                return "Thành trì đích không tồn tại trên bản đồ.";
            }

            var directDays = _map.TravelDays(player.CurrentCityId, targetId);
            var days = directDays > 0 ? directDays : CalculateShortestPath(player.CurrentCityId, targetId);

            if (days <= 0)
            {
                return "Không có đường bộ thông thương đến thành này.";
            }

            // Check if player is exhausted
            if (_broker.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var attrs) && attrs is not null)
            {
                if (attrs.Vitals.Fatigue >= 95)
                {
                    return "Bạn đang quá kiệt sức (Mệt mỏi 95%+). Hãy nghỉ ngơi trước khi lên đường viễn hành!";
                }
            }

            // Execute Journey
            _clock.Advance(days);
            player.MoveTo(targetId, _clock.Today);

            // Encounter flavor
            var flavor = string.Empty;
            var seed = (int)(targetId * 37 + _clock.Today.DayIndex);
            var rng = new Random(seed);
            var roll = rng.NextDouble();
            if (roll < 0.25)
            {
                flavor = " Trên đường gặp gỡ một đội thương tiêu, bạn cùng họ đồng hành đàm đạo, thuận lợi tiến nhập thành trì.";
            }
            else if (roll < 0.45)
            {
                flavor = " Qua ải gặp tráng quan phong cảnh hữu tình, tâm tình sảng khoái, cảm thụ khí tượng giang sơn.";
            }

            return $"Xuất hành thành công! Vượt qua {days} ngày lộ trình, bạn đã đến {targetCity.NameVi} vào ngày {_clock.Today}.{flavor}";
        }

        return $"Lệnh không rõ: {commandId}";
    }

    private int CalculateShortestPath(long fromId, long toId)
    {
        var queue = new Queue<(long Node, int Dist)>();
        var visited = new HashSet<long>();
        queue.Enqueue((fromId, 0));
        visited.Add(fromId);

        while (queue.Count > 0)
        {
            var (curr, dist) = queue.Dequeue();
            if (curr == toId)
            {
                return dist;
            }

            foreach (var n in _map.RouteNeighbours(curr))
            {
                if (visited.Add(n))
                {
                    var d = _map.TravelDays(curr, n);
                    queue.Enqueue((n, dist + Math.Max(1, d)));
                }
            }
        }

        return 4;
    }
}
