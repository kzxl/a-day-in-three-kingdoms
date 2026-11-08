using SimT.Contracts.Playing;
using SimT.Contracts.Ui;

namespace SimT.Modules.Dlc.Tests;

public sealed class ItemHubTests : IDisposable
{
    private readonly DlcTestHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public void Hub_populates_all_catalog_items_with_rich_codex_data()
    {
        using var kernel = _host.Boot();
        var hub = kernel.Capabilities.Get<IItemHubService>(CapabilityIds.ItemHub);
        var catalog = kernel.Capabilities.Get<IItemCatalog>(CapabilityIds.ItemCatalog);

        Assert.NotNull(hub);
        Assert.Equal(catalog.All.Count, hub.Entries.Count);
        Assert.True(hub.Entries.Count >= 24, "Must cover all 24 historical commodities and farming tools");

        foreach (var entry in hub.Entries)
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.SubCategoryVi));
            Assert.False(string.IsNullOrWhiteSpace(entry.VisualIcon));
            Assert.False(string.IsNullOrWhiteSpace(entry.IllustrationArt));
            Assert.False(string.IsNullOrWhiteSpace(entry.LoreDescriptionVi));
            Assert.NotEmpty(entry.DetailedStats);
            Assert.False(string.IsNullOrWhiteSpace(entry.SourcingVi));
            Assert.False(string.IsNullOrWhiteSpace(entry.UsageVi));
            Assert.False(string.IsNullOrWhiteSpace(entry.UpgradeRoadmapVi));
        }
    }

    [Fact]
    public void Farming_tools_and_supplies_have_specific_operational_specs()
    {
        using var kernel = _host.Boot();
        var hub = kernel.Capabilities.Get<IItemHubService>(CapabilityIds.ItemHub);

        // Hoe
        var hoe = hub.FindEntryByCode("cuoc_nong");
        Assert.NotNull(hoe);
        Assert.Contains("Cuốc", hoe.Item.NameVi);
        Assert.True(hoe.DetailedStats.ContainsKey("Thao tác hỗ trợ"));
        Assert.Contains("Gieo hạt", hoe.DetailedStats["Thao tác hỗ trợ"]);
        Assert.Contains("nâng cấp", hoe.UpgradeRoadmapVi, StringComparison.OrdinalIgnoreCase);

        // Sickle
        var sickle = hub.FindEntryByCode("liem_gat");
        Assert.NotNull(sickle);
        Assert.Contains("Liềm", sickle.Item.NameVi);
        Assert.True(sickle.DetailedStats.ContainsKey("Thao tác hỗ trợ"));
        Assert.Contains("Gặt lúa", sickle.DetailedStats["Thao tác hỗ trợ"]);

        // Fertilizer
        var fertilizer = hub.FindEntryByCode("phan_bon");
        Assert.NotNull(fertilizer);
        Assert.True(fertilizer.DetailedStats.ContainsKey("Hiệu ứng bón phân"));
        Assert.Contains("+25", fertilizer.DetailedStats["Hiệu ứng bón phân"]);

        // Rice Seed
        var seed = hub.FindEntryByCode("hat_giong_lua");
        Assert.NotNull(seed);
        Assert.True(seed.DetailedStats.ContainsKey("Thời gian sinh trưởng"));
        Assert.True(seed.DetailedStats.ContainsKey("Sản phẩm đầu ra"));
    }

    [Theory]
    [InlineData("all", 32)]
    [InlineData("tool", 3)]
    [InlineData("grain", 3)]
    [InlineData("medicine", 4)]
    [InlineData("material", 8)]
    [InlineData("luxury", 7)]
    public void Category_filtering_yields_expected_subsets(string categoryKey, int expectedCount)
    {
        using var kernel = _host.Boot();
        var hub = kernel.Capabilities.Get<IItemHubService>(CapabilityIds.ItemHub);

        var filtered = hub.FilterByCategory(categoryKey);
        Assert.Equal(expectedCount, filtered.Count);
    }

    [Fact]
    public void Hub_allows_extensibility_via_custom_entry_registration()
    {
        using var kernel = _host.Boot();
        var hub = kernel.Capabilities.Get<IItemHubService>(CapabilityIds.ItemHub);

        var customItem = new ItemDef(999, "thiet_giap_custom", "Thiết Giáp Chiến Binh", "iron_armor", "equipment", 300, 0, 20);
        var customEntry = new ItemHubEntry(
            customItem,
            "Trang Bị Hộ Thân",
            "🛡️",
            """
            ┌───────────────────┐
            │   [ CHIẾN GIÁP ]  │
            │     /  |  \       │
            └───────────────────┘
            """,
            "Giáp sắt đúc thử nghiệm cho thợ rèn cấp cao.",
            new Dictionary<string, string> { ["Phòng thủ"] = "+15", ["Độ bền"] = "100/100" },
            "Rèn tại xưởng Lạc Dương",
            "Trang bị vào ô ARMOR",
            "Có thể tôi luyện lên +5");

        hub.RegisterCustomEntry(customEntry);

        var retrieved = hub.FindEntry(999);
        Assert.NotNull(retrieved);
        Assert.Equal("thiet_giap_custom", retrieved.Item.Code);
        Assert.Equal("🛡️", retrieved.VisualIcon);
    }

    [Fact]
    public void Item_hub_screen_renders_metrics_and_interactive_rows()
    {
        using var kernel = _host.Boot();

        Assert.True(kernel.TryGetCommandHandler("content.item_hub", out var handler));
        Assert.NotNull(handler);

        // Find the screen contribution
        var screen = kernel.Ui.Screens.FirstOrDefault(s => s.ScreenId == "content.item_hub");
        Assert.NotNull(screen);
        Assert.Equal("Bách Vật Đồ Giám", screen.Title);
        Assert.Equal(22, screen.Order);

        var model = screen.Render();
        Assert.NotEmpty(model.Headline);
        Assert.Single(model.Sections);
        Assert.Equal(32, model.Sections[0].Rows.Count);

        // Verify row details have modal command
        var firstRow = model.Sections[0].Rows[0];
        Assert.Contains(firstRow.Commands, c => c.Id.StartsWith("item:modal:"));
        Assert.NotEmpty(firstRow.Info);

        // Execute category filter
        var filterMsg = handler.Execute("filter:tool");
        Assert.Contains("tool", filterMsg);

        var filteredModel = screen.Render();
        Assert.Equal(3, filteredModel.Sections[0].Rows.Count);
    }
    [Fact]
    public void ItemHub_supports_warehouse_exchange_when_rented()
    {
        using var kernel = _host.Boot();
        var warehouse = kernel.Capabilities.Get<IMarketWarehouse>(CapabilityIds.MarketWarehouse);
        var bag = kernel.Capabilities.Get<IBag>(CapabilityIds.PlayerBag);
        var player = kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);
        var wallet = kernel.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);

        Assert.NotNull(warehouse);
        Assert.NotNull(bag);
        Assert.NotNull(player);

        var cityId = player.CurrentCityId;

        // 1. Rent warehouse
        var rentOk = warehouse.Rent(cityId, out var rentMsg);
        Assert.True(rentOk, rentMsg);

        // 2. Add 10 units of item 1 (lúa gạo) to bag
        bag.Add(cityId, 1, 10);
        Assert.Equal(10, bag.Quantity(cityId, 1));

        // 3. Screen shows warehouse metrics and store command
        var screen = kernel.Ui.Screens.FirstOrDefault(s => s.ScreenId == "content.item_hub");
        Assert.NotNull(screen);
        var model = screen.Render();
        Assert.Contains(model.Headline, m => m.Label == "Kho Thương Điếm" && m.Value.Contains("/"));

        var gaoRow = model.Sections[0].Rows.FirstOrDefault(r => r.Cells[1] == "Gạo" || r.Commands.Any(c => c.Id == "store:1"));
        Assert.NotNull(gaoRow);
        Assert.Contains(gaoRow.Commands, c => c.Id == "store:1");

        // 4. Execute store command through ItemHubScreen handler (store 4 units)
        Assert.True(kernel.TryGetCommandHandler("content.item_hub", out var handler));
        Assert.NotNull(handler);

        var storeResult = handler.Execute("store:1:4");
        Assert.NotNull(storeResult);
        Assert.Equal(6, bag.Quantity(cityId, 1));

        var stock = warehouse.Stock(cityId);
        var whItem1 = stock.FirstOrDefault(s => s.ItemId == 1);
        Assert.NotNull(whItem1);
        Assert.Equal(4, whItem1.Quantity);

        // 5. Execute take command through ItemHubScreen handler (take 2 units back)
        var takeResult = handler.Execute("take:1:2");
        Assert.NotNull(takeResult);
        Assert.Equal(8, bag.Quantity(cityId, 1));

        stock = warehouse.Stock(cityId);
        whItem1 = stock.FirstOrDefault(s => s.ItemId == 1);
        Assert.NotNull(whItem1);
        Assert.Equal(2, whItem1.Quantity);
    }
}
