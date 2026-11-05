using SimT.Contracts.Modules;
using SimT.Contracts.Playing;
using SimT.Contracts.Ui;

namespace SimT.Modules.Base.Content;

/// <summary>
/// Screen contribution for Item Information Hub & Codex (Đồ Giám Vật Phẩm & Khí Cụ).
/// Enables browsing, category filtering, detailed stat inspections, ASCII illustrations,
/// and upgrade trajectories for all commodities, tools, and agricultural supplies.
/// </summary>
public sealed class ItemHubScreen : IScreenContribution, IScreenCommandHandler
{
    private static readonly string[] HubColumns =
        ["Biểu Tượng", "Mã", "Tên Kỳ Vật", "Tiểu Loại", "Giá Gốc", "Hạn Dùng", "Khối Lượng", "Hành Trang", "Đồ Giám"];

    private readonly IItemHubService _hub;
    private readonly ICapabilityBroker _broker;
    private string _activeCategory = "all";

    public ItemHubScreen(IItemHubService hub, ICapabilityBroker broker)
    {
        _hub = hub;
        _broker = broker;
    }

    public string ScreenId => "content.item_hub";

    public string Title => "Bách Vật Đồ Giám";

    public int Order => 22;

    public ScreenModel Render()
    {
        _broker.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var player);
        _broker.TryGet<IBag>(CapabilityIds.PlayerBag, out var bag);

        var cityId = player?.CurrentCityId ?? 1L;
        var entries = _hub.FilterByCategory(_activeCategory);

        // Calculate player bag stats
        var itemsInCity = bag is not null ? _hub.Entries.Count(e => bag.Quantity(cityId, e.Item.Id) > 0) : 0;
        var toolsInCity = bag is not null ? _hub.Entries.Count(e => e.Item.Category == "tool" && bag.Quantity(cityId, e.Item.Id) > 0) : 0;

        var activeFilterLabel = _activeCategory switch
        {
            "tool" => "Nông Cụ Canh Tác",
            "seed" => "Hạt Giống & Vật Tư",
            "farming" => "Điền Trang & Nông Nghiệp",
            "grain" => "Lương Thực",
            "food" => "Thực Phẩm & Đồ Uống",
            "medicine" => "Dược Liệu & Y Đạo",
            "material" => "Khoáng Sản & Chế Tác",
            "luxury" => "Xa Xỉ & Tơ Lụa",
            _ => "Tất Cả Kỳ Vật"
        };

        _broker.TryGet<IMarketWarehouse>(CapabilityIds.MarketWarehouse, out var warehouse);
        var whInfo = warehouse?.Info(cityId);
        var whStockLookup = warehouse?.Stock(cityId).ToDictionary(s => s.ItemId, s => s.Quantity) ?? new Dictionary<long, int>();

        var metrics = new List<ScreenMetric>
        {
            new("Kỳ Vật Đã Biên Lục", $"{_hub.Entries.Count} mục", "Toàn bộ danh mục", MetricTone.Positive),
            new("Bộ Lọc Hiện Tại", activeFilterLabel, $"{entries.Count} mục hiển thị", MetricTone.Neutral),
            new("Đang Sở Hữu Tại Thành", $"{itemsInCity} / {_hub.Entries.Count} loại", "Hành trang sở tại", MetricTone.Neutral),
            new("Kho Thương Điếm", whInfo != null ? $"{whInfo.Used}/{whInfo.Capacity}" : "Chưa thuê", whInfo != null ? "Đã thuê kho" : "Chưa thuê kho", whInfo != null ? MetricTone.Positive : MetricTone.Neutral)
        };

        var rows = new List<ScreenRow>();
        foreach (var entry in entries)
        {
            var qty = bag?.Quantity(cityId, entry.Item.Id) ?? 0;
            var bagText = qty > 0 ? $"{qty} cái" : "—";
            var shelfLifeText = entry.Item.ShelfLifeDays > 0 ? $"{entry.Item.ShelfLifeDays} ngày" : "Vĩnh viễn";

            var details = new List<ScreenDetail>
            {
                new("Mã hệ thống", entry.Item.Code),
                new("Tên vật phẩm", $"{entry.Item.NameVi} ({entry.Item.NameEn})"),
                new("Phân loại chi tiết", entry.SubCategoryVi),
                new("Giá niêm yết cơ bản", $"{entry.Item.BasePrice} Quan"),
                new("Hạn sử dụng", shelfLifeText),
                new("Khối lượng vận chuyển", $"{entry.Item.Weight} Cân"),
                new("Tồn kho hành trang sở tại", qty > 0 ? $"{qty} đơn vị" : "Chưa có trong túi đồ"),
                new("Minh họa đồ giám", entry.VisualIcon)
            };

            foreach (var (k, v) in entry.DetailedStats)
            {
                details.Add(new ScreenDetail(k, v));
            }

            details.Add(new ScreenDetail("Nguồn gốc & Thu thập", entry.SourcingVi));
            details.Add(new ScreenDetail("Thao tác sử dụng", entry.UsageVi));
            details.Add(new ScreenDetail("Tiềm năng nâng cấp", entry.UpgradeRoadmapVi));

            var whQty = whStockLookup.GetValueOrDefault(entry.Item.Id, 0);
            if (whInfo != null)
            {
                details.Add(new ScreenDetail("Tồn kho thương điếm", whQty > 0 ? $"{whQty} đơn vị" : "Không có trong kho"));
            }

            var rowCommands = new List<ScreenCommand>
            {
                new($"item:modal:{entry.Item.Id}", "🔍 Chi Tiết Đồ Giám", CommandKind.Primary)
            };

            if (whInfo != null && qty > 0 && whInfo.Used < whInfo.Capacity)
            {
                rowCommands.Add(new ScreenCommand($"store:{entry.Item.Id}", "📥 Gửi Kho (1)", CommandKind.Secondary));
            }

            if (whInfo != null && whQty > 0)
            {
                rowCommands.Add(new ScreenCommand($"take:{entry.Item.Id}", "📤 Rút Túi (1)", CommandKind.Secondary));
            }

            rows.Add(new ScreenRow(
                [
                    entry.VisualIcon,
                    entry.Item.Code,
                    entry.Item.NameVi,
                    entry.SubCategoryVi,
                    $"{entry.Item.BasePrice} Quan",
                    shelfLifeText,
                    $"{entry.Item.Weight} Cân",
                    bagText,
                    "Tra cứu"
                ],
                rowCommands,
                Title: $"{entry.VisualIcon} {entry.Item.NameVi} · {entry.SubCategoryVi}",
                Details: details));
        }

        var navCommands = new List<ScreenCommand>
        {
            new("filter:all", "🌐 Tất Cả (24)", _activeCategory == "all" ? CommandKind.Primary : CommandKind.Secondary),
            new("filter:farming", "🌾 Điền Trang (7)", _activeCategory == "farming" ? CommandKind.Primary : CommandKind.Secondary),
            new("filter:tool", "⛏️ Nông Cụ (3)", _activeCategory == "tool" ? CommandKind.Primary : CommandKind.Secondary),
            new("filter:seed", "🌱 Hạt Giống (4)", _activeCategory == "seed" ? CommandKind.Primary : CommandKind.Secondary),
            new("filter:grain", "🍚 Lương Thực (2)", _activeCategory == "grain" ? CommandKind.Primary : CommandKind.Secondary),
            new("filter:medicine", "🌿 Dược Liệu (2)", _activeCategory == "medicine" ? CommandKind.Primary : CommandKind.Secondary),
            new("filter:material", "⚒️ Khoáng Sản (5)", _activeCategory == "material" ? CommandKind.Primary : CommandKind.Secondary),
            new("filter:luxury", "✨ Xa Xỉ (4)", _activeCategory == "luxury" ? CommandKind.Primary : CommandKind.Secondary)
        };

        var status = $"Đang duyệt '{activeFilterLabel}' · {entries.Count} kỳ vật hiển thị · Nhấn vào từng hàng để xem hình minh họa và thông số đồ giám chuyên sâu.";

        return new ScreenModel(
            ScreenId,
            Title,
            status,
            [
                new ScreenSection($"Bách Vật Đồ Giám — Danh Mục {activeFilterLabel}", HubColumns, rows)
            ],
            navCommands,
            metrics);
    }

    public string? Execute(string commandId)
    {
        if (commandId.StartsWith("filter:", StringComparison.OrdinalIgnoreCase))
        {
            _activeCategory = commandId["filter:".Length..].Trim().ToLowerInvariant();
            return $"Đã lọc theo danh mục: {_activeCategory}.";
        }

        if (commandId.StartsWith("inspect:", StringComparison.OrdinalIgnoreCase))
        {
            var idStr = commandId["inspect:".Length..].Trim();
            if (long.TryParse(idStr, out var id))
            {
                var entry = _hub.FindEntry(id);
                return entry is not null
                    ? $"{entry.VisualIcon} {entry.Item.NameVi}: {entry.LoreDescriptionVi}"
                    : "Không tìm thấy kỳ vật.";
            }
        }

        if (commandId.StartsWith("store:", StringComparison.OrdinalIgnoreCase))
        {
            var parts = commandId.Split(':');
            if (parts.Length >= 2 && long.TryParse(parts[1], out var storeItemId))
            {
                var qty = parts.Length >= 3 && int.TryParse(parts[2], out var q) ? q : 1;
                _broker.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var player);
                _broker.TryGet<IMarketWarehouse>(CapabilityIds.MarketWarehouse, out var wh);
                if (wh != null && player != null)
                {
                    wh.Store(player.CurrentCityId, storeItemId, qty, out var msg);
                    return msg;
                }
            }
        }

        if (commandId.StartsWith("take:", StringComparison.OrdinalIgnoreCase))
        {
            var parts = commandId.Split(':');
            if (parts.Length >= 2 && long.TryParse(parts[1], out var takeItemId))
            {
                var qty = parts.Length >= 3 && int.TryParse(parts[2], out var q) ? q : 1;
                _broker.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var player);
                _broker.TryGet<IMarketWarehouse>(CapabilityIds.MarketWarehouse, out var wh);
                if (wh != null && player != null)
                {
                    wh.Take(player.CurrentCityId, takeItemId, qty, out var msg);
                    return msg;
                }
            }
        }

        return null;
    }
}
