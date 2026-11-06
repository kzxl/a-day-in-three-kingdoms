using SimT.Contracts.Data;
using SimT.Contracts.Modules;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;
using SimT.Contracts.Ui;

namespace SimT.Modules.Base.World;

/// <summary>
/// Command handler for World Map interactions (inter-city travel, luggage transfer, and scouting).
/// </summary>
internal sealed class WorldMapCommandHandler(
    IDbSession db,
    IWorldMap map,
    ICapabilityBroker broker,
    IGameClock clock) : IScreenCommandHandler
{
    private readonly IDbSession _db = db;
    private readonly IWorldMap _map = map;
    private readonly ICapabilityBroker _broker = broker;
    private readonly IGameClock _clock = clock;

    public string ScreenId => "world.map";

    public string? Execute(string commandId)
    {
        if (commandId.StartsWith("stroll", StringComparison.Ordinal))
        {
            if (!_broker.TryGet<ICityStroll>(CapabilityIds.CityStroll, out var stroll) || stroll is null)
                return "Tính năng du ngoạn chưa sẵn sàng.";
            if (!_broker.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var player) || player is null)
                return "Không xác định được trạng thái người chơi.";

            var result = stroll.Stroll(player.CurrentCityId);
            return result.Ok
                ? $"[Du Ngoạn - {result.Title}] {result.Narrative} ({result.RewardSummary})"
                : result.Narrative;
        }

        if (commandId.StartsWith("inspect:", StringComparison.Ordinal) &&
            long.TryParse(commandId["inspect:".Length..], out var cityId))
        {
            var city = _map.Find(cityId);
            return city is not null
                ? $"Đã định vị {city.NameVi}. Xem thông tin chi tiết, đặc sản và danh sĩ tại thanh tra bên phải."
                : "Không tìm thấy thành trì.";
        }

        if (commandId.StartsWith("travel:", StringComparison.Ordinal))
            return ExecuteTravel(commandId["travel:".Length..]);

        return $"Lệnh không rõ: {commandId}";
    }

    private string ExecuteTravel(string raw)
    {
        var parts = raw.Split(':');
        if (!long.TryParse(parts[0], out var targetId))
            return "Mã thành trì không hợp lệ.";

        var mode = TravelMode.Walk;
        if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]))
        {
            switch (parts[1].Trim().ToLowerInvariant())
            {
                case "walk":
                    mode = TravelMode.Walk;
                    break;
                case "horse" or "ride":
                    mode = TravelMode.Horse;
                    break;
                case "caravan" or "escort":
                    mode = TravelMode.Caravan;
                    break;
                default:
                    return "Phương tiện di chuyển không hợp lệ.";
            }
        }

        if (!_broker.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var player) || player is null)
            return "Không xác định được trạng thái người chơi.";

        var fromCityId = player.CurrentCityId;
        if (fromCityId == targetId)
            return "Bạn hiện đã đang ở tại thành này rồi!";

        var targetCity = _map.Find(targetId);
        if (targetCity is null)
            return "Thành trì đích không tồn tại trên bản đồ.";

        var fromCityName = _map.Find(fromCityId)?.NameVi ?? $"#{fromCityId}";
        _broker.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var attrs);
        if (attrs is not null && attrs.Vitals.Fatigue >= 95)
            return "Bạn đang quá kiệt sức (Mệt mỏi 95%+). Hãy nghỉ ngơi trước khi lên đường viễn hành!";

        var quote = _map.QuoteTravel(fromCityId, targetId, mode);
        if (quote.ActualDays <= 0)
            return "Không có đường bộ thông thương đến thành này.";

        _broker.TryGet<IWallet>(CapabilityIds.PlayerWallet, out var wallet);
        if (quote.TravelFee > 0 && wallet is not null && wallet.Liquid < quote.TravelFee)
            return $"Không đủ ngân lượng để {quote.ModeNameVi}: Cần {quote.TravelFee} Quan (hiện có {wallet.Liquid} Quan).";

        _broker.TryGet<IBag>(CapabilityIds.PlayerBag, out var bag);
        _broker.TryGet<IItemCatalog>(CapabilityIds.ItemCatalog, out var items);
        var currentRows = bag?.Rows(fromCityId) ?? Array.Empty<BagRow>();
        var gaoQty = currentRows.FirstOrDefault(r => r.ItemId == 1)?.Quantity ?? 0;
        var meatQty = currentRows.FirstOrDefault(r => r.ItemId == 8)?.Quantity ?? 0;
        var (usedGao, usedMeat) = TravelLuggage.ComputeRationsUsed(quote.RationsNeeded, gaoQty, meatQty);
        var missingRations = Math.Max(0, quote.RationsNeeded - usedGao - usedMeat);
        var available = TravelLuggage.BuildAvailableAfterRations(currentRows, items, quote.RationsNeeded);

        Dictionary<long, int> selectedLuggage;
        if (parts.Length > 3)
            return "Cú pháp hành lý không hợp lệ.";
        if (parts.Length == 3)
        {
            if (!TravelLuggage.TryValidateExplicitLuggage(parts[2], available, quote.CarryCapacity, out selectedLuggage, out var err))
                return err;
        }
        else
        {
            selectedLuggage = TravelLuggage.SelectDefaultLuggage(available, quote.CarryCapacity);
        }

        var rationsDesc = string.Empty;
        _db.Transaction(() =>
        {
            if (quote.TravelFee > 0 && wallet is not null)
                wallet.Charge(quote.TravelFee, "WORLD.TRAVEL", $"Chi phí hành trình ({quote.ModeNameVi}) đến {targetCity.NameVi}");

            if (bag is not null)
            {
                if (usedGao > 0) bag.Remove(fromCityId, 1, usedGao);
                if (usedMeat > 0) bag.Remove(fromCityId, 8, usedMeat);
                if (usedGao + usedMeat > 0)
                    rationsDesc = $" Tiêu dùng {usedGao + usedMeat} phần lương khô dự trữ.";

                if (missingRations > 0 && wallet is not null)
                {
                    var roadFoodFee = missingRations * 3L;
                    if (wallet.Liquid >= roadFoodFee)
                    {
                        wallet.Charge(roadFoodFee, "WORLD.TRAVEL", "Mua cơm nước tại dịch trạm ven đường");
                        rationsDesc += $" Chi {roadFoodFee} Quan cơm nước dịch trạm.";
                    }
                }

                foreach (var (itemId, qty) in selectedLuggage.OrderBy(kv => kv.Key))
                {
                    if (qty <= 0) continue;
                    bag.Remove(fromCityId, itemId, qty);
                    bag.Add(targetId, itemId, qty);
                }
            }

            player.MoveTo(targetId, _clock.Today.AddDays(quote.ActualDays));
        });

        _clock.Advance(quote.ActualDays);

        if (attrs is not null)
        {
            attrs.ApplyFatigue(quote.FatigueExpected);
            attrs.AddExp("CON", quote.ActualDays * 3);
            attrs.AddExp("WIL", quote.ActualDays * 2);
            if (mode == TravelMode.Horse) attrs.PracticeSkill("ngu_ma", 20);
            else attrs.PracticeSkill("sinh_ton", 15);
        }

        var encounterStory = ResolveTravelEncounter(targetId, mode, attrs, wallet);
        var pathDesc = quote.PathNodeIds.Count > 1
            ? $" ({string.Join(" ➔ ", quote.PathNodeIds.Select(id => _map.Find(id)?.NameVi ?? id.ToString()))})"
            : string.Empty;

        var carriedSummary = selectedLuggage.Count > 0
            ? $"Hành lý mang theo: {TravelLuggage.FormatItemQuantities(selectedLuggage.Select(kv => (kv.Key, kv.Value)), items)}."
            : "Hành lý mang theo: không mang hàng.";
        var leftRows = bag?.Rows(fromCityId) ?? Array.Empty<BagRow>();
        var leftSummary = leftRows.Count > 0
            ? $"Để lại tại {fromCityName}: {TravelLuggage.FormatItemQuantities(leftRows.Select(r => (r.ItemId, r.Quantity)), items)}."
            : $"Không để lại hàng tại {fromCityName}.";

        return $"Xuất hành thành công ({quote.ModeNameVi})! Vượt qua {quote.ActualDays} ngày lộ trình{pathDesc}, bạn đã đến {targetCity.NameVi} vào ngày {_clock.Today}.{rationsDesc} {carriedSummary} {leftSummary}{encounterStory}";
    }

    private string ResolveTravelEncounter(
        long targetId,
        TravelMode mode,
        IPlayerAttributes? attrs,
        IWallet? wallet)
    {
        var seed = (int)(targetId * 37 + _clock.Today.DayIndex + (int)mode * 13);
        var rng = new Random(seed);
        var roll = rng.NextDouble();
        var playerWar = attrs?.Attributes.War ?? 50;
        var targetEco = _map.GetEcosystem(targetId);

        if (mode != TravelMode.Caravan && targetEco.DangerLevel >= 2 && roll < 0.25)
        {
            if (playerWar >= 55)
            {
                attrs?.AddExp("WAR", 15);
                return " Trên đèo gặp toán sơn tặc chặn đường! Bạn tuốt kiếm uy phong dũng mãnh kích thoái bọn cướp, nhận được lời khen ngợi của dịch lại (+15 EXP Võ Lực).";
            }

            var tribute = Math.Min(25, wallet?.Liquid ?? 0);
            if (tribute > 0 && wallet is not null)
            {
                wallet.Charge(tribute, "WORLD.TRAVEL", "Nộp tiền mãi lộ cho thổ phỉ");
                return $" Dọc đường gặp thảo khấu cản trở, bạn khéo léo dùng {tribute} Quan mãi lộ để bảo toàn nhân mã qua ải.";
            }
        }
        else if (roll < 0.40)
        {
            attrs?.RecoverVitals(0, 10, 0);
            attrs?.AddExp("INT", 15);
            return " Đi qua danh thắng non nước hùng vĩ, tâm hồn thư thái hòa cùng trời đất (giảm 10 Căng thẳng, +15 EXP Ngộ tính INT).";
        }
        else if (roll < 0.60)
        {
            attrs?.AddExp("COM", 10);
            return " Dừng chân tại trạm ngựa ven quan đạo, cùng các thương nhân trà lâu đàm đạo phương sách thông thương (+10 EXP Thương Nghiệp COM).";
        }

        return " Đường trường bình an thuận lợi, gió mây mát rượi, quan đạo thông suốt.";
    }
}
