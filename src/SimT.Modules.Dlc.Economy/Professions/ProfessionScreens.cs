using SimT.Contracts.Playing;
using SimT.Contracts.Ui;

namespace SimT.Modules.Dlc.Economy;

internal sealed partial class ProfessionScreen : IScreenContribution
{
    private static readonly string[] SlotColumns =
        ["Vị Trí", "Khí Cụ", "Thuộc Tính", "Độ Bền", "Phẩm Cấp"];

    private static readonly string[] EquipmentColumns =
        ["Khí Cụ", "Thuộc Tính", "Độ Bền", "Phẩm Cấp"];

    private static readonly string[] OrderColumns =
        ["Bên Ủy Thác", "Loại", "Vật Phẩm Cần", "Số Lượng", "Thù Lao", "Danh Vọng", "Hạn Chót", "Trạng Thái"];

    private static readonly string[] RecipeColumns =
        ["Bản Vẽ", "Yêu Cầu", "Nguyên Liệu", "Phí Đúc", "Thời Gian", "Thành Phẩm"];

    private static readonly string[] Columns =
        ["Nghề", "Phân Ngành", "Danh Hiệu", "Ngày/lượt", "Sản phẩm", "Sản lượng", "Dụng cụ", "Tay nghề"];

    private static readonly string[] PerkColumns =
        ["Bí Kỹ Nghề", "Yêu Cầu Danh Vọng", "Hiệu Quả Bí Truyền", "Trạng Thái"];

    private readonly ProfessionService _service;
    private readonly IItemCatalog _items;
    private readonly IPlayerState _player;
    private readonly IBag? _bag;
    private string _activeFilter = "all";

    public ProfessionScreen(ProfessionService service, IItemCatalog items, IPlayerState player, IBag? bag = null)
    {
        _service = service;
        _items = items;
        _player = player;
        _bag = bag;
    }

    public string ScreenId => "professions.trades";

    public string Title => "Bách Nghệ";

    public int Order => 35;

    public void SetFilter(string filter) =>
        _activeFilter = string.IsNullOrWhiteSpace(filter) ? "all" : filter.ToLowerInvariant();

    public ScreenModel Render()
    {
        var cityId = _player.CurrentCityId;
        var alerts = new List<ScreenAlert>();
        var workshop = _service.GetWorkshop(cityId);

        var (slotSection, equipSection, recipeSection, equipCount) = BuildForgeSections(cityId, workshop, alerts);
        var (workshopSection, tradeSection) = BuildWorkshopAndTradeSections(cityId, workshop, alerts);
        var (orderSection, bountySection, orderCount) = BuildOrderAndBountySections(cityId, alerts);
        var (dishSection, wildSection, perkSection) = BuildLifeSkillSections(cityId);

        var bestTier = _service.Trades.Max(trade => _service.Tier(trade.Id));

        IReadOnlyList<ScreenSection> visibleSections = _activeFilter switch
        {
            "forge" => [slotSection, equipSection, recipeSection],
            "workshop" => [workshopSection, tradeSection],
            "orders" => [orderSection, bountySection],
            "life" => [wildSection, dishSection, perkSection],
            _ =>
            [
                slotSection,
                workshopSection,
                orderSection,
                bountySection,
                dishSection,
                wildSection,
                perkSection,
                equipSection,
                recipeSection,
                tradeSection
            ]
        };

        var topCommands = new List<ScreenCommand>
        {
            new("filter:all", "🌐 Toàn Cảnh Bách Nghệ", _activeFilter == "all" ? CommandKind.Primary : CommandKind.Secondary),
            new("filter:forge", $"⚒️ Lò Rèn & Bảo Khí ({equipCount})", _activeFilter == "forge" ? CommandKind.Primary : CommandKind.Secondary),
            new("filter:workshop", $"🏛️ Phường Xưởng & Lao Tác ({_service.Trades.Count})", _activeFilter == "workshop" ? CommandKind.Primary : CommandKind.Secondary),
            new("filter:orders", $"📜 Thương Hội & Cáo Thị ({orderCount})", _activeFilter == "orders" ? CommandKind.Primary : CommandKind.Secondary),
            new("filter:life", "🍶 Dã Ngoại, Ẩm Thực & Bí Kỹ", _activeFilter == "life" ? CommandKind.Primary : CommandKind.Secondary)
        };

        return new ScreenModel(
            ScreenId,
            Title,
            "Quản lý xưởng chế tác tự động, trang bị binh khí hộ giáp lên nhân vật, và hoàn thành các đơn đặt hàng quân nhu thương hội.",
            visibleSections,
            topCommands,
            [
                new ScreenMetric("Tổng ngành nghề", Fmt(_service.Trades.Count), "nghề"),
                new ScreenMetric("Danh vọng phường nghề", Fmt(_service.TotalGuildFame), "điểm", MetricTone.Positive),
                new ScreenMetric("Vũ khí trang bị", _service.Slots.FirstOrDefault(s => s.SlotCode == "WEAPON")?.Equipment?.NameVi ?? "Tay không", "Binh khí"),
                new ScreenMetric("Tay nghề cao nhất", $"bậc {bestTier}", "trên 5", bestTier > 0 ? MetricTone.Positive : MetricTone.Neutral)
            ],
            alerts,
            "Rèn luyện nghề thủ công, hoàn thành đơn đặt hàng phường hội hoặc chế tác binh khí để tăng chỉ số chiến đấu.");
    }

    private static string Fmt(long value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

internal sealed class ProfessionCommandHandler : IScreenCommandHandler
{
    private readonly ProfessionService _service;
    private readonly IPlayerState _player;
    private readonly ProfessionScreen? _screen;

    public ProfessionCommandHandler(ProfessionService service, IPlayerState player, ProfessionScreen? screen = null)
    {
        _service = service;
        _player = player;
        _screen = screen;
    }

    public string ScreenId => "professions.trades";

    public string? Execute(string commandId)
    {
        var cityId = _player.CurrentCityId;
        var parts = commandId.Split(':');
        if (parts.Length < 2)
        {
            return "Lệnh không hợp lệ.";
        }

        var verb = parts[0];

        switch (verb)
        {
            case "filter":
                _screen?.SetFilter(parts[1]);
                return $"Đã chuyển phân khu hiển thị: {parts[1]}.";

            case "work":
                if (parts[1] == "batch" && parts.Length >= 4 &&
                    long.TryParse(parts[2], out var batchTradeId) &&
                    int.TryParse(parts[3], out var batchCount))
                {
                    return _service.WorkBatch(cityId, batchTradeId, batchCount).Message;
                }
                if (long.TryParse(parts[1], out var tradeId))
                    return _service.Work(cityId, tradeId).Message;
                break;

            case "craft":
                if (long.TryParse(parts[1], out var recipeId))
                    return _service.Craft(cityId, recipeId).Message;
                break;

            case "repair":
                if (long.TryParse(parts[1], out var eqId))
                    return _service.Repair(cityId, eqId).Message;
                break;

            case "equip":
                if (long.TryParse(parts[1], out var equipId))
                    return _service.Equip(equipId).Message;
                break;

            case "unequip":
                return _service.Unequip(parts[1]).Message;

            case "workshop":
                if (parts.Length >= 3)
                {
                    var sub = parts[1];
                    if (sub == "open" && long.TryParse(parts[2], out var wsTradeId))
                    {
                        return _service.OpenWorkshop(cityId, wsTradeId).Message;
                    }
                    if (sub == "hire")
                    {
                        return _service.HireArtisan(cityId).Message;
                    }
                    if (sub == "dismiss")
                    {
                        return _service.DismissArtisan(cityId).Message;
                    }
                    if (sub == "upgrade")
                    {
                        return _service.UpgradeFurnace(cityId).Message;
                    }
                    if (sub == "close")
                    {
                        return _service.CloseWorkshop(cityId).Message;
                    }
                }
                break;

            case "perk":
                if (parts.Length >= 3 && parts[1] == "unlock" && long.TryParse(parts[2], out var perkId))
                {
                    return _service.UnlockPerk(perkId).Message;
                }
                break;

            case "order":
                if (parts.Length >= 3)
                {
                    var orderVerb = parts[1];
                    if (long.TryParse(parts[2], out var ordId))
                    {
                        if (orderVerb == "accept")
                            return _service.AcceptOrder(ordId).Message;
                        if (orderVerb == "fulfill")
                            return _service.FulfillOrder(cityId, ordId).Message;
                    }
                }
                break;

            case "bounty":
                if (parts.Length >= 3)
                {
                    var bountyVerb = parts[1];
                    if (bountyVerb == "post" && parts.Length >= 5 &&
                        long.TryParse(parts[2], out var bItemId) &&
                        int.TryParse(parts[3], out var bQty) &&
                        int.TryParse(parts[4], out var bReward))
                    {
                        return _service.PostBounty(cityId, bItemId, bQty, bReward).Message;
                    }
                    if (bountyVerb == "dispatch" && parts.Length >= 5 &&
                        long.TryParse(parts[2], out var retNpcId) &&
                        long.TryParse(parts[3], out var retItemId) &&
                        int.TryParse(parts[4], out var retQty))
                    {
                        return _service.DispatchRetainerBounty(cityId, retNpcId, retItemId, retQty).Message;
                    }
                    if (bountyVerb == "cancel" && long.TryParse(parts[2], out var cancelOrdId))
                    {
                        return _service.CancelBounty(cancelOrdId).Message;
                    }
                }
                break;

            case "wild":
                if (parts.Length >= 2)
                {
                    if (parts[1] == "fish")
                        return _service.Fish(cityId).Message;
                    if (parts[1] == "forage")
                        return _service.Forage(cityId).Message;
                    if (parts[1] == "hunt")
                        return _service.Hunt(cityId).Message;
                    if (parts[1] == "mine")
                        return _service.Mine(cityId).Message;
                }
                break;

            case "dish":
                if (parts.Length >= 3)
                {
                    if (parts[1] == "cook" && long.TryParse(parts[2], out var cookDishId))
                        return _service.Cook(cityId, cookDishId).Message;
                    if (parts[1] == "eat" && long.TryParse(parts[2], out var eatDishId))
                        return _service.Eat(eatDishId).Message;
                    if (parts[1] == "sell" && long.TryParse(parts[2], out var sellDishId))
                    {
                        var qty = parts.Length >= 4 && int.TryParse(parts[3], out var q) ? q : 1;
                        return _service.SellDish(cityId, sellDishId, qty).Message;
                    }
                }
                break;

            case "sell":
                if (parts.Length >= 3 && parts[1] == "eq" && long.TryParse(parts[2], out var sellEqId))
                {
                    return _service.SellEquipment(cityId, sellEqId).Message;
                }
                break;

            case "temper":
                if (long.TryParse(parts[1], out var temperEqId))
                    return _service.Temper(cityId, temperEqId).Message;
                break;
        }

        return $"Lệnh không hợp lệ: {commandId}";
    }
}
