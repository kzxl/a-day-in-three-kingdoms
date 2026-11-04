using SimT.Contracts.Modules;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;
using SimT.Contracts.Ui;

namespace SimT.Modules.Base.World;

/// <summary>
/// World Map & Citadel Network Screen (Bản Đồ Thiên Hạ).
/// Provides complete geographic overview of the 15 citadels,
/// trade routes, travel times, regional price factors, and local notables.
/// </summary>
internal sealed class WorldMapScreen : IScreenContribution
{
    private static readonly string[] Columns =
        ["Thành Trì", "Châu Quận", "Khoảng Cách", "Vật Giá", "Thương Cơ", "Danh Sĩ Tại Thành"];

    private readonly IWorldMap _map;
    private readonly ICapabilityBroker _broker;
    private readonly IGameClock _clock;

    public WorldMapScreen(IWorldMap map, ICapabilityBroker broker, IGameClock clock)
    {
        _map = map;
        _broker = broker;
        _clock = clock;
    }

    public string ScreenId => "world.map";

    public string Title => "Bản Đồ";

    public int Order => 15;

    public ScreenModel Render()
    {
        _broker.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var player);
        _broker.TryGet<INpcRegistry>(CapabilityIds.NpcRegistry, out var npcs);
        _broker.TryGet<IItemCatalog>(CapabilityIds.ItemCatalog, out var items);
        _broker.TryGet<IMarketTrade>(CapabilityIds.MarketTrade, out var trade);
        _broker.TryGet<IBag>(CapabilityIds.PlayerBag, out var bag);

        var currentCityId = player?.CurrentCityId ?? 1L;
        var currentCity = _map.Find(currentCityId) ?? _map.Citadels[0];

        // 1. Headline metrics
        var directNeighbours = _map.RouteNeighbours(currentCity.Id);
        var headline = new List<ScreenMetric>
        {
            new("Thành Đang Trú", currentCity.NameVi, currentCity.Region, MetricTone.Neutral),
            new("Hệ Số Vật Giá", $"{currentCity.PriceFactorPermille / 10.0:0.#}%",
                currentCity.PriceFactorPermille > 1050 ? "Đắt đỏ" : currentCity.PriceFactorPermille < 950 ? "Rẻ (Nguồn hàng)" : "Chuẩn",
                currentCity.PriceFactorPermille > 1050 ? MetricTone.Warning : MetricTone.Neutral),
            new("Tuyến Nối Trực Tiếp", directNeighbours.Count.ToString(), "thành lân cận", MetricTone.Neutral),
            new("Thiên Hạ Đại Thành", _map.Citadels.Count.ToString(), "thành trì", MetricTone.Neutral)
        };

        // 2. Group Citadels by 5 Strategic Regions
        var regionGroups = new Dictionary<string, (string Title, List<CitadelDef> Citadels)>
        {
            ["capital"] = ("🏛️ Trung Nguyên & Quan Trung (Đế Đô & Trọng Trấn)", new List<CitadelDef>()),
            ["north"] = ("🌾 Hà Bắc & U Châu (Thảo Nguyên, Chiến Mã & Lương Thảo)", new List<CitadelDef>()),
            ["south"] = ("🌊 Giang Đông & Từ Châu (Thương Cảng, Gốm Sứ & Trà Mạn)", new List<CitadelDef>()),
            ["central"] = ("⛵ Kinh Tương Cửu Quận (Yết Hầu Giao Lộ Thiên Hạ)", new List<CitadelDef>()),
            ["west"] = ("⛰️ Ba Thục & Hán Trung (Thiên Phủ Chi Quốc, Dược Liệu & Thục Cẩm)", new List<CitadelDef>())
        };

        foreach (var c in _map.Citadels)
        {
            var key = c.Region switch
            {
                "trung_nguyen" or "quan_trung" or "du_chau" => "capital",
                "ha_bac" or "ky_chau" or "u_chau" or "thanh_chau" => "north",
                "giang_dong" or "duong_chau" or "tu_chau" => "south",
                "kinh_chau" => "central",
                _ => "west"
            };
            regionGroups[key].Citadels.Add(c);
        }

        var sections = new List<ScreenSection>();

        foreach (var (_, (groupTitle, citadels)) in regionGroups)
        {
            var rows = new List<ScreenRow>();

            foreach (var c in citadels)
            {
                var isCurrent = (c.Id == currentCity.Id);
                var directDays = _map.TravelDays(currentCity.Id, c.Id);
                var shortestDays = isCurrent ? 0 : (directDays > 0 ? directDays : CalculateShortestPath(currentCity.Id, c.Id));

                var distanceText = isCurrent
                    ? "📍 Đang Ở Đây"
                    : (directDays > 0 ? $"⚡ {directDays} ngày (Trực tiếp)" : $"🐎 {shortestDays} ngày (Liên thành)");

                var factorText = $"{c.PriceFactorPermille / 10.0:0.#}% " +
                                 (c.PriceFactorPermille >= 1100 ? "📈 Cao" : c.PriceFactorPermille <= 950 ? "📉 Rẻ" : "≈ Chuẩn");

                // Market arbitrage comparison vs current city
                var arbitrageTip = isCurrent
                    ? "Trung tâm giao dịch"
                    : (c.PriceFactorPermille > currentCity.PriceFactorPermille
                        ? $"Xuất khẩu +{((c.PriceFactorPermille - currentCity.PriceFactorPermille) * 100.0 / currentCity.PriceFactorPermille):0.#}% 💰"
                        : $"Nhập khẩu -{((currentCity.PriceFactorPermille - c.PriceFactorPermille) * 100.0 / currentCity.PriceFactorPermille):0.#}% 📦");

                // Local NPCs & Ecosystem
                var localNpcs = npcs?.FindByCitadel(c.Id) ?? Array.Empty<NpcDef>();
                var notableNames = localNpcs.Count > 0
                    ? string.Join(", ", localNpcs.Take(3).Select(n => n.NameVi)) + (localNpcs.Count > 3 ? $" (+{localNpcs.Count - 3})" : "")
                    : "Ẩn cư chưa rõ";

                var eco = _map.GetEcosystem(c.Id);

                // Commands
                var commands = new List<ScreenCommand>();
                if (!isCurrent)
                {
                    commands.Add(new ScreenCommand($"travel:{c.Id}", directDays > 0 ? "Lên Đường 🐎" : "Viễn Hành 🐎", CommandKind.Primary));
                    commands.Add(new ScreenCommand($"travel:{c.Id}:horse", "Dịch Mã ⚡"));
                    commands.Add(new ScreenCommand($"travel:{c.Id}:caravan", "Tiêu Cục 🛡️"));
                }
                commands.Add(new ScreenCommand($"inspect:{c.Id}", "Khảo Sát 🔍"));

                // Sublocations
                var sublocations = _map.Sublocations(c.Id);
                var sublocDesc = sublocations.Count > 0
                    ? string.Join(" · ", sublocations.Select(s => s.NameVi))
                    : "Chợ Trời, Khách Điếm, Nha Môn";

                var heldGoods = bag?.TotalQuantity(c.Id) ?? 0;

                var dangerStars = new string('★', Math.Min(3, eco.DangerLevel)) + new string('☆', Math.Max(0, 3 - eco.DangerLevel));

                var details = new List<ScreenDetail>
                {
                    new("Thành Trì", $"{c.NameVi} ({c.NameEn})"),
                    new("Châu Quận", c.Region.Replace('_', ' ').ToUpperInvariant()),
                    new("Hệ Sinh Thái", $"{eco.BiomeNameVi} · {eco.ClimateVi}"),
                    new("Địa Thế Vùng", eco.TopographyVi),
                    new("Sản Vật Bản Địa", $"{eco.PrimarySpecialtyVi} & {eco.SecondarySpecialtyVi}"),
                    new("Hiểm Trở / Thổ Phỉ", $"{dangerStars} (Cấp {eco.DangerLevel})"),
                    new("Khoảng Cách", isCurrent ? "Bản thành" : $"{shortestDays} ngày đường"),
                    new("Chỉ Số Vật Giá", $"{c.PriceFactorPermille / 10.0:0.0}% ({factorText})"),
                    new("Chiến Lược Buôn", arbitrageTip),
                    new("Kho Hàng Cá Nhân", $"{heldGoods} kiện hàng lưu kho"),
                    new("Công Trình Địa Phương", sublocDesc),
                    new("Danh Sĩ Thường Trú", localNpcs.Count > 0 ? string.Join(", ", localNpcs.Select(n => $"{n.NameVi} ({n.Role})")) : "Chưa có danh sĩ cư ngụ")
                };

                rows.Add(new ScreenRow(
                    [
                        isCurrent ? $"★ {c.NameVi}" : c.NameVi,
                        c.Region,
                        distanceText,
                        factorText,
                        arbitrageTip,
                        notableNames
                    ],
                    commands,
                    Title: $"{c.NameVi} · {c.Region}",
                    Details: details));
            }

            sections.Add(new ScreenSection(groupTitle, Columns, rows));
        }

        var attention = new List<ScreenAlert>();
        if (directNeighbours.Count == 0)
        {
            attention.Add(new ScreenAlert("Thành này bị cô lập, không có đường bộ thông thương ra bên ngoài.", AlertLevel.Warning));
        }

        return new ScreenModel(
            ScreenId,
            Title,
            $"Bản Đồ Thiên Hạ — Vị trí hiện tại: {currentCity.NameVi}",
            sections,
            Array.Empty<ScreenCommand>(),
            headline,
            attention);
    }

    private int CalculateShortestPath(long fromId, long toId)
    {
        // Simple BFS shortest path on 15 citadels
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

        return 5; // Fallback estimate
    }
}
