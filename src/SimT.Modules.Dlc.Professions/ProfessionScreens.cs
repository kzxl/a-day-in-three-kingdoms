using SimT.Contracts.Playing;
using SimT.Contracts.Ui;

namespace SimT.Modules.Dlc.Professions;

internal sealed class ProfessionScreen : IScreenContribution
{
    private static readonly string[] Columns =
        ["Nghề", "Ngày/lượt", "Sản phẩm", "Sản lượng", "Dụng cụ", "Tay nghề"];

    private static readonly string[] RecipeColumns =
        ["Bản Vẽ", "Yêu Cầu", "Nguyên Liệu", "Phí Đúc", "Thời Gian", "Thành Phẩm"];

    private static readonly string[] EquipmentColumns =
        ["Khí Cụ", "Thuộc Tính", "Độ Bền", "Phẩm Cấp"];

    private readonly ProfessionService _service;
    private readonly IItemCatalog _items;
    private readonly IPlayerState _player;

    public ProfessionScreen(ProfessionService service, IItemCatalog items, IPlayerState player)
    {
        _service = service;
        _items = items;
        _player = player;
    }

    public string ScreenId => "professions.trades";

    public string Title => "Bách Nghệ";

    public int Order => 35;

    public ScreenModel Render()
    {
        var cityId = _player.CurrentCityId;
        var rows = new List<ScreenRow>(_service.Trades.Count);
        foreach (var trade in _service.Trades)
        {
            var output = _items.Find(trade.OutputItemId);
            var tier = _service.Tier(trade.Id);
            rows.Add(new ScreenRow(
                [
                    $"{trade.NameVi} ({trade.NameEn})",
                    Fmt(trade.DaysPerBatch),
                    output?.NameVi ?? trade.OutputItemId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Fmt(trade.BaseQuantity + tier),
                    Fmt(trade.ToolFee),
                    $"bậc {tier} · {_service.PracticeCount(trade.Id)} lượt"
                ],
                [new ScreenCommand($"work:{trade.Id}", "Làm 1 lượt")],
                Title: trade.NameVi,
                Details:
                [
                    new ScreenDetail("Mỗi lượt", $"{trade.DaysPerBatch} ngày · {trade.ToolFee} quan dụng cụ"),
                    new ScreenDetail("Sản phẩm", output?.NameVi ?? "—"),
                    new ScreenDetail("Sản lượng", $"{trade.BaseQuantity + tier} mỗi lượt"),
                    new ScreenDetail("Tay nghề", $"bậc {tier}/5 · {_service.PracticeCount(trade.Id)} lượt")
                ]));
        }

        var recipeRows = new List<ScreenRow>();
        foreach (var r in _service.Recipes)
        {
            var mat = _items.Find(r.MaterialItemId)?.NameVi ?? $"Vật phẩm #{r.MaterialItemId}";
            var output = _items.Find(r.OutputItemId)?.NameVi ?? r.NameVi;
            var currentTier = _service.Tier(r.TradeId);
            var canCraft = currentTier >= r.RequiredTier;

            var commands = canCraft
                ? new List<ScreenCommand> { new ScreenCommand($"craft:{r.Id}", "Rèn đúc") }
                : new List<ScreenCommand>();

            recipeRows.Add(new ScreenRow(
                [
                    r.NameVi,
                    $"Cần bậc {r.RequiredTier} (hiện {currentTier})",
                    $"{r.MaterialQuantity}x {mat}",
                    $"{r.CraftFee} Quan",
                    $"{r.CraftDays} ngày",
                    $"{r.OutputQuantity}x {output}"
                ],
                commands));
        }

        var equipRows = new List<ScreenRow>();
        var equipments = _service.Equipments(cityId);
        foreach (var eq in equipments)
        {
            var commands = eq.Durability < eq.MaxDurability
                ? new List<ScreenCommand> { new ScreenCommand($"repair:{eq.Id}", "Tu sửa (15 Quan)") }
                : new List<ScreenCommand>();

            equipRows.Add(new ScreenRow(
                [
                    eq.NameVi,
                    $"+{eq.BonusValue} {eq.BonusStat}",
                    $"{eq.Durability}/{eq.MaxDurability}",
                    eq.QualityRank
                ],
                commands));
        }

        var totalPractice = _service.Trades.Sum(trade => _service.PracticeCount(trade.Id));
        var bestTier = _service.Trades.Max(trade => _service.Tier(trade.Id));

        return new ScreenModel(
            ScreenId,
            Title,
            "Mỗi lượt tốn tiền dụng cụ và số ngày của nghề; tay nghề tăng sản lượng và mở khóa chế tác khí cụ.",
            [
                new ScreenSection("Nghề Lao Tác Cơ Bản", Columns, rows),
                new ScreenSection("Bản Vẽ Chế Tác Khí Cụ", RecipeColumns, recipeRows),
                new ScreenSection("Khí Cụ & Binh Khí Đang Có", EquipmentColumns, equipRows)
            ],
            [],
            [
                new ScreenMetric("Nghề", Fmt(_service.Trades.Count), "loại"),
                new ScreenMetric("Bản vẽ rèn", Fmt(_service.Recipes.Count), "bản"),
                new ScreenMetric("Lượt đã làm", Fmt(totalPractice), "lượt"),
                new ScreenMetric("Tay nghề cao nhất", $"bậc {bestTier}", "trên 5", bestTier > 0 ? MetricTone.Positive : MetricTone.Neutral)
            ],
            []);
    }

    private static string Fmt(long value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

internal sealed class ProfessionCommandHandler : IScreenCommandHandler
{
    private readonly ProfessionService _service;
    private readonly IPlayerState _player;

    public ProfessionCommandHandler(ProfessionService service, IPlayerState player)
    {
        _service = service;
        _player = player;
    }

    public string ScreenId => "professions.trades";

    public string? Execute(string commandId)
    {
        var separator = commandId.IndexOf(':');
        if (separator <= 0 || !long.TryParse(commandId[(separator + 1)..], out var targetId))
        {
            return "Lệnh không hợp lệ.";
        }

        var verb = commandId[..separator];
        var cityId = _player.CurrentCityId;

        return verb switch
        {
            "work" => _service.Work(cityId, targetId).Message,
            "craft" => _service.Craft(cityId, targetId).Message,
            "repair" => _service.Repair(cityId, targetId).Message,
            _ => $"Động từ '{verb}' không thuộc DLC professions."
        };
    }
}
