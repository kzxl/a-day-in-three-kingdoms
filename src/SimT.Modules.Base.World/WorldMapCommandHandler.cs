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
        if (commandId.StartsWith("stroll", StringComparison.Ordinal))
        {
            if (!_broker.TryGet<ICityStroll>(CapabilityIds.CityStroll, out var stroll) || stroll is null)
            {
                return "Tính năng du ngoạn chưa sẵn sàng.";
            }

            if (!_broker.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var player) || player is null)
            {
                return "Không xác định được trạng thái người chơi.";
            }

            var result = stroll.Stroll(player.CurrentCityId);
            return result.Ok
                ? $"[Du Ngoạn - {result.Title}] {result.Narrative} ({result.RewardSummary})"
                : result.Narrative;
        }

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
            var raw = commandId["travel:".Length..];
            var parts = raw.Split(':');

            if (!long.TryParse(parts[0], out var targetId))
            {
                return "Mã thành trì không hợp lệ.";
            }

            var mode = TravelMode.Walk;
            if (parts.Length > 1)
            {
                mode = parts[1].ToLowerInvariant() switch
                {
                    "horse" or "ride" => TravelMode.Horse,
                    "caravan" or "escort" => TravelMode.Caravan,
                    _ => TravelMode.Walk
                };
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

            // Check if player is exhausted
            _broker.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var attrs);
            if (attrs is not null && attrs.Vitals.Fatigue >= 95)
            {
                return "Bạn đang quá kiệt sức (Mệt mỏi 95%+). Hãy nghỉ ngơi trước khi lên đường viễn hành!";
            }

            var quote = _map.QuoteTravel(player.CurrentCityId, targetId, mode);
            if (quote.ActualDays <= 0)
            {
                return "Không có đường bộ thông thương đến thành này.";
            }

            _broker.TryGet<IWallet>(CapabilityIds.PlayerWallet, out var wallet);
            if (quote.TravelFee > 0 && wallet is not null)
            {
                if (wallet.Liquid < quote.TravelFee)
                {
                    return $"Không đủ ngân lượng để {quote.ModeNameVi}: Cần {quote.TravelFee} Quan (hiện có {wallet.Liquid} Quan).";
                }
                wallet.Charge(quote.TravelFee, "WORLD.TRAVEL", $"Chi phí hành trình ({quote.ModeNameVi}) đến {targetCity.NameVi}");
            }

            // Provisions (Rations)
            var rationsDesc = string.Empty;
            if (_broker.TryGet<IBag>(CapabilityIds.PlayerBag, out var bag) && bag is not null)
            {
                var gaoQty = bag.Quantity(player.CurrentCityId, 1); // 1 = gao
                var meatQty = bag.Quantity(player.CurrentCityId, 8); // 8 = ca_kho
                var needed = quote.RationsNeeded;
                var usedGao = Math.Min(needed, gaoQty);
                if (usedGao > 0)
                {
                    bag.Remove(player.CurrentCityId, 1, usedGao);
                    needed -= usedGao;
                }
                var usedMeat = Math.Min(needed, meatQty);
                if (usedMeat > 0)
                {
                    bag.Remove(player.CurrentCityId, 8, usedMeat);
                    needed -= usedMeat;
                }
                if (usedGao + usedMeat > 0)
                {
                    rationsDesc = $" Tiêu dùng {usedGao + usedMeat} phần lương khô dự trữ.";
                }
                if (needed > 0 && wallet is not null)
                {
                    var roadFoodFee = needed * 3;
                    if (wallet.Liquid >= roadFoodFee)
                    {
                        wallet.Charge(roadFoodFee, "WORLD.TRAVEL", "Mua cơm nước tại dịch trạm ven đường");
                        rationsDesc += $" Chi {roadFoodFee} Quan cơm nước dịch trạm.";
                    }
                }
            }

            // Execute Journey
            _clock.Advance(quote.ActualDays);
            player.MoveTo(targetId, _clock.Today);

            // Fatigue & EXP
            if (attrs is not null)
            {
                attrs.ApplyFatigue(quote.FatigueExpected);
                attrs.AddExp("CON", quote.ActualDays * 3);
                attrs.AddExp("WIL", quote.ActualDays * 2);
                if (mode == TravelMode.Horse) attrs.PracticeSkill("ngu_ma", 20);
                else attrs.PracticeSkill("sinh_ton", 15);
            }

            // Dynamic Travel Encounter
            var seed = (int)(targetId * 37 + _clock.Today.DayIndex + (int)mode * 13);
            var rng = new Random(seed);
            var roll = rng.NextDouble();
            var encounterStory = string.Empty;

            var playerWar = attrs?.Attributes.War ?? 50;
            var targetEco = _map.GetEcosystem(targetId);

            if (mode != TravelMode.Caravan && targetEco.DangerLevel >= 2 && roll < 0.25)
            {
                if (playerWar >= 55)
                {
                    encounterStory = " Trên đèo gặp toán sơn tặc chặn đường! Bạn tuốt kiếm uy phong dũng mãnh kích thoái bọn cướp, nhận được lời khen ngợi của dịch lại (+15 EXP Võ Lực).";
                    attrs?.AddExp("WAR", 15);
                }
                else
                {
                    var tribute = Math.Min(25, wallet?.Liquid ?? 0);
                    if (tribute > 0 && wallet is not null)
                    {
                        wallet.Charge(tribute, "WORLD.TRAVEL", "Nộp tiền mãi lộ cho thổ phỉ");
                        encounterStory = $" Dọc đường gặp thảo khấu cản trở, bạn khéo léo dùng {tribute} Quan mãi lộ để bảo toàn nhân mã qua ải.";
                    }
                }
            }
            else if (roll < 0.40)
            {
                encounterStory = " Đi qua danh thắng non nước hùng vĩ, tâm hồn thư thái hòa cùng trời đất (giảm 10 Căng thẳng, +15 EXP Ngộ tính INT).";
                attrs?.RecoverVitals(0, 10, 0);
                attrs?.AddExp("INT", 15);
            }
            else if (roll < 0.60)
            {
                encounterStory = " Dừng chân tại trạm ngựa ven quan đạo, cùng các thương nhân trà lâu đàm đạo phương sách thông thương (+10 EXP Thương Nghiệp COM).";
                attrs?.AddExp("COM", 10);
            }
            else
            {
                encounterStory = " Đường trường bình an thuận lợi, gió mây mát rượi, quan đạo thông suốt.";
            }

            var pathDesc = quote.PathNodeIds.Count > 1
                ? $" ({string.Join(" ➔ ", quote.PathNodeIds.Select(id => _map.Find(id)?.NameVi ?? id.ToString()))})"
                : string.Empty;

            return $"Xuất hành thành công ({quote.ModeNameVi})! Vượt qua {quote.ActualDays} ngày lộ trình{pathDesc}, bạn đã đến {targetCity.NameVi} vào ngày {_clock.Today}.{rationsDesc} {encounterStory}";
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
