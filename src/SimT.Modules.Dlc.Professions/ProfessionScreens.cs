using SimT.Contracts.Playing;
using SimT.Contracts.Ui;

namespace SimT.Modules.Dlc.Professions;

internal sealed class ProfessionScreen : IScreenContribution
{
    private static readonly string[] Columns =
        ["Nghề", "Ngày/lượt", "Sản phẩm", "Sản lượng", "Dụng cụ", "Tay nghề"];

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
                [new ScreenCommand($"work:{trade.Id}", "Làm 1 lượt")]));
        }

        return new ScreenModel(
            ScreenId,
            Title,
            "Mỗi lượt tốn tiền dụng cụ và số ngày của nghề; tay nghề tăng sản lượng.",
            [new ScreenSection("Nghề đang làm được", Columns, rows)],
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
        if (separator <= 0 || !long.TryParse(commandId[(separator + 1)..], out var tradeId))
        {
            return "Lệnh không hợp lệ.";
        }

        if (commandId[..separator] != "work")
        {
            return $"Động từ '{commandId[..separator]}' không thuộc DLC professions.";
        }

        return _service.Work(_player.CurrentCityId, tradeId).Message;
    }
}
