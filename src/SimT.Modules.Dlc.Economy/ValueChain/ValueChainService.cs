using SimT.Contracts;
using SimT.Contracts.Data;
using SimT.Contracts.Logging;
using SimT.Contracts.Modules;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Economy.ValueChain;

/// <summary>
/// Domain implementation of the 4-Tier Economic Value Chain and civic market consumption.
/// </summary>
public sealed class ValueChainService : IValueChainService
{
    private readonly IDbSession _db;
    private readonly IItemCatalog _items;
    private readonly IWorldMap _map;
    private readonly INpcRegistry? _npcs;
    private readonly IMarketTrade _trade;
    private readonly ILog? _log;
    private readonly ICapabilityBroker? _broker;

    private static readonly IReadOnlyList<RegionalResourceNodeDef> BuiltInResourceNodes =
    [
        new(1, "node_lac_duong", "Bắc Mang Sơn Cổ Đạo", "FOREST", 6, "go", 32, "tiger_tendon", 8, "Rừng cổ Bắc Mang cung cấp gỗ lim dựng cung thất và nơi săn mãnh hổ lấy gân."),
        new(2, "node_truong_an", "Đồng Quan & Mạch Ngọc Lam Điền", "MINE", 5, "sat", 29, "lantian_jade", 9, "Mạch quặng sắt chân núi Đồng Quan và mỏ Ngọc Bích Lam Điền trứ danh Quan Trung."),
        new(3, "node_nghiep_thanh", "Hà Bắc Đại Bình Nguyên", "FARMLAND", 9, "lua_mi", 10, "ruou", 12, "Vựa lúa mì trù phú nhất Hà Bắc, cung cấp nguyên liệu cho các tửu phường."),
        new(4, "node_trac_huyen", "Rừng Sâm U Châu", "HERB_VALLEY", 13, "sam", 11, "vai", 6, "Rừng sâu phương Bắc sản sinh Nhân Sâm Liêu Đông và vải gai dệt thủ công."),
        new(5, "node_quang_tong", "Cự Lộc Khoáng Tuyến", "MINE", 5, "sat", 15, "than_da", 10, "Mỏ sắt và vỉa than đá lộ thiên vùng Cự Lộc phục vụ rèn đúc quân giới."),
        new(6, "node_binh_nguyen", "Bến Đò Hoàng Hà", "FISHERY", 9, "lua_mi", 8, "ca_kho", 10, "Châu thổ sông Hoàng Hà bồi đắp phù sa trồng lúa mì và đánh bắt cá sông."),
        new(7, "node_hua_xuong", "Đồn Điền Dĩnh Xuyên", "FARMLAND", 1, "gao", 17, "hat_giong_lua", 14, "Hệ thống đồn điền kiểu mẫu cung cấp thóc gạo và hạt giống lúa năng suất cao."),
        new(8, "node_bac_binh", "Liêu Đông Cổ Lộ & Kỵ Trại", "HERB_VALLEY", 13, "sam", 27, "warhorse_hide", 8, "Núi tuyết biên thùy giàu Nhân Sâm đại bổ và thuộc da chiến mã Bạch Mã Nghĩa Tòng."),
        new(9, "node_ha_phi", "Từ Châu Tơ Lụa Phố & Nghi Thủy", "TEXTILE_HUB", 4, "lua", 2, "muoi", 10, "Làng nghề nuôi tằm dệt lụa Từ Châu kết hợp ruộng muối ven biển Hoài Hải."),
        new(10, "node_tuong_duong", "Lộc Môn Sơn Đạo Quán", "HERB_VALLEY", 7, "thuoc", 31, "cinnabar_ore", 9, "Đạo quán núi Lộc Môn khai thác Chu Sa Đan Thạch và bào chế linh dược."),
        new(11, "node_xich_bich", "Ô Lâm Thủy Trại", "FISHERY", 8, "ca_kho", 26, "ironwood", 11, "Ngư trường Trường Giang và rừng Gỗ Thiết Mộc Ô Lâm phục vụ đóng chiến thuyền."),
        new(12, "node_giang_lang", "Bến Cảng Trường Giang", "FISHERY", 8, "ca_kho", 5, "sat", 10, "Thương cảng sầm uất đánh bắt thủy sản và xưởng rèn Giang Lăng Thiết Khố."),
        new(13, "node_kien_nghiep", "Thái Hồ Thủy Trại & Đồi Trà", "FISHERY", 3, "tra", 2, "muoi", 12, "Đồi trà xanh Giang Nam và nghề nấu muối, đánh cá vùng Thái Hồ."),
        new(14, "node_han_trung", "Định Quân Sơn Lâm Trường", "FOREST", 26, "ironwood", 16, "dong", 11, "Rừng đại ngàn Tần Lĩnh - Kiếm Các cung cấp Gỗ Thiết Mộc và mỏ đồng thau."),
        new(15, "node_thanh_do", "Cẩm Quan Thành Thục Cẩm Phố", "TEXTILE_HUB", 12, "gam", 28, "celestial_silk", 9, "Trung tâm dệt Gấm Thục và nuôi Tơ Thiên Tằm nổi danh thiên hạ."),
        new(16, "node_uyen_thanh", "Nam Dương Thảo Dược & Mỏ Đồng", "HERB_VALLEY", 7, "thuoc", 16, "dong", 9, "Vùng đất sản sinh dược thảo Nam Dương và quặng đồng thau đúc khí cụ."),
        new(17, "node_tho_xuan", "Bến Sông Hoài Thủy", "FARMLAND", 1, "gao", 8, "ca_kho", 12, "Đồng bằng Hoài Nam hai mùa lúa chín kết hợp ngư trường sông Hoài."),
        new(18, "node_thuong_dang", "Thượng Đảng Mỏ Sắt & Thái Hành", "MINE", 5, "sat", 25, "black_iron", 14, "Đại mỏ sắt và mạch ngầm Huyền Thiết Khoáng sâu trong dãy Thái Hành Sơn."),
        new(19, "node_tay_luong", "Mỏ Đồng Kỳ Liên & Vũ Uy", "MINE", 16, "dong", 27, "warhorse_hide", 10, "Mạch khoáng sản chân núi Kỳ Liên và da chiến mã trại ngựa Vũ Uy."),
        new(20, "node_giao_chi", "Rừng Trầm Hương & Hải Cảng Phiên Ngung", "FOREST", 14, "tram_huong", 30, "south_sea_pearl", 8, "Rừng nhiệt đới Lĩnh Nam sản sinh Trầm Hương và ngư trường Nam Hải Minh Châu.")
    ];

    public ValueChainService(
        IDbSession db,
        IItemCatalog items,
        IWorldMap map,
        INpcRegistry? npcs,
        IMarketTrade trade,
        ILog? log,
        ICapabilityBroker? broker = null)
    {
        _db = db;
        _items = items;
        _map = map;
        _npcs = npcs;
        _trade = trade;
        _log = log;
        _broker = broker;
    }

    public IReadOnlyList<RegionalResourceNodeDef> AllResourceNodes => BuiltInResourceNodes;

    public IReadOnlyList<RegionalResourceNodeDef> GetResourceNodes(long citadelId) =>
        BuiltInResourceNodes.Where(n => n.CitadelId == citadelId).ToList();

    public int SimulateRegionalExtraction(long citadelId)
    {
        var nodes = GetResourceNodes(citadelId);
        if (nodes.Count == 0) return 0;

        var securityScore = 75;
        if (_broker != null && _broker.TryGet<IMilitaryCommand>(CapabilityIds.MilitaryCommand, out var military) && military != null)
        {
            securityScore = military.GetCitadelSecurity(citadelId);
        }

        var localNpcCount = _npcs?.FindByCitadel(citadelId).Count ?? 0;
        var laborBonus = localNpcCount / 5;
        var totalExtracted = 0;

        foreach (var node in nodes)
        {
            var primaryYield = Math.Max(1, (node.BaseMonthlyYield * Math.Clamp(securityScore, 20, 100)) / 80) + laborBonus;
            _trade.AdjustStock(citadelId, node.PrimaryItemId, primaryYield);
            totalExtracted += primaryYield;

            if (node.SecondaryItemId.HasValue)
            {
                var secondaryYield = Math.Max(1, primaryYield / 2);
                _trade.AdjustStock(citadelId, node.SecondaryItemId.Value, secondaryYield);
                totalExtracted += secondaryYield;
            }
        }

        _log?.Info($"[dlc.economy] regional extraction at citadel {citadelId} (security={securityScore}): +{totalExtracted} raw units");
        return totalExtracted;
    }

    public ValueChainProfileDef GetProfile(string itemCode)
    {
        var profile = ItemValueChain.GetProfile(itemCode);
        return new ValueChainProfileDef(
            itemCode,
            profile.Tier,
            profile.TierNameVi,
            profile.BadgeVi,
            profile.CategoryVi,
            profile.PrimaryConsumerVi,
            profile.EconomicRoleVi,
            profile.Inputs,
            profile.Outputs);
    }

        public MarketDemandStatus EstimateDemand(long citadelId, long itemId, string itemCode, string itemNameVi, string category, int stock, int baseline, int npcCount)
    {
        if (baseline <= 0) baseline = 40;
        var monthlyDemand = CalculateMonthlyDemand(itemCode, category, npcCount);

        string velocity;
        if (stock > baseline * 1.3)
        {
            velocity = "📦 Dư thừa (Giá mềm)";
        }
        else if (stock < baseline * 0.5)
        {
            velocity = "⚡ Tiêu thụ cao (Khan hiếm)";
        }
        else
        {
            velocity = "⚖️ Ổn định";
        }

        var dailyRate = Math.Max(1.0, monthlyDemand) / 30.0;
        var daysUntilDepletion = stock == 0 ? 0 : Math.Min(999, (int)(stock / dailyRate));

        return new MarketDemandStatus(
            citadelId,
            itemId,
            itemCode,
            itemNameVi,
            stock,
            baseline,
            monthlyDemand,
            velocity,
            daysUntilDepletion);
    }

    public MarketDemandStatus GetDemandStatus(long citadelId, long itemId)
    {
        var item = _items.Find(itemId) ?? throw new SimException("market.item_missing", $"Item {itemId} not found.");
        var stock = _trade.StallStock(citadelId, itemId);
        var baseline = _trade.StallBaseline(citadelId, itemId);
        var npcCount = _npcs?.FindByCitadel(citadelId).Count ?? 0;
        return EstimateDemand(citadelId, itemId, item.Code, item.NameVi, item.Category, stock, baseline, npcCount);
    }

    public IReadOnlyList<MarketDemandStatus> GetCitadelDemands(long citadelId)
    {
        var npcCount = _npcs?.FindByCitadel(citadelId).Count ?? 0;
        var result = new List<MarketDemandStatus>(_items.All.Count);
        foreach (var item in _items.All)
        {
            var stock = _trade.StallStock(citadelId, item.Id);
            var baseline = _trade.StallBaseline(citadelId, item.Id);
            result.Add(EstimateDemand(citadelId, item.Id, item.Code, item.NameVi, item.Category, stock, baseline, npcCount));
        }
        return result;
    }

    public void SimulateMonthlyCivicConsumption(GameDate date)
    {
        // When stock is in surplus (stock > baseline_stock), local population and resident NPCs
        // organically consume the excess goods, returning the market toward baseline equilibrium.
        var surplusRows = _db.Query(
            "SELECT citadel_id, item_id, stock, baseline_stock FROM mk_stall_listing WHERE stock > baseline_stock",
            row => (City: row.GetInt64(0), Item: row.GetInt64(1), Stock: row.GetInt32(2), Baseline: row.GetInt32(3)));

        foreach (var row in surplusRows)
        {
            var surplus = row.Stock - row.Baseline;
            var consumed = Math.Max(1, (surplus * 40) / 100);
            _db.Execute(
                "UPDATE mk_stall_listing SET stock = stock - @consumed WHERE citadel_id = @city AND item_id = @item",
                new DbArgs().Add("@city", row.City).Add("@item", row.Item).Add("@consumed", consumed));
        }

        if (surplusRows.Count > 0)
        {
            _log?.Info($"[dlc.economy] civic consumption digested surplus across {surplusRows.Count} stall listings on {date}");
        }
    }

    private static int CalculateMonthlyDemand(string itemCode, string category, int npcCount)
    {
        switch (itemCode.ToLowerInvariant())
        {
            case "gao":
            case "lua_mi":
                return Math.Max(3, (npcCount / 4) + 2);

            case "muoi":
                return Math.Max(2, (npcCount / 6) + 1);

            case "tra":
            case "ruou":
                return Math.Max(1, (npcCount / 8) + 1);

            case "vai":
            case "cuoc_nong":
            case "liem_gat":
                return Math.Max(1, (npcCount / 10) + 1);

            case "thuoc":
            case "sat":
            case "dong":
                return Math.Max(1, (npcCount / 12) + 1);

            case "gam":
            case "sam":
            case "tram_huong":
                return Math.Max(1, npcCount / 15);

            default:
                return category switch
                {
                    "grain" or "food" => 3,
                    "material" => 2,
                    _ => 1
                };
        }
    }
}
