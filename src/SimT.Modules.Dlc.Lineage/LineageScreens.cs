using SimT.Contracts.Time;
using SimT.Contracts.Ui;

namespace SimT.Modules.Dlc.Lineage;

internal sealed class FamilyScreen : IScreenContribution
{
    private static readonly string[] Columns = ["Thành viên", "Vai trò", "Ngày", "Kế vị"];

    private readonly LineageService _service;

    public FamilyScreen(LineageService service) => _service = service;

    public string ScreenId => "lineage.family";

    public string Title => "Gia Đạo";

    public int Order => 50;

    public ScreenModel Render()
    {
        var rows = new List<ScreenRow>();
        foreach (var member in _service.Members())
        {
            var rowCommands = member.Role == "Tử tức" && !member.IsHeir
                ? new[] { new ScreenCommand($"heir:{member.Id}", "Lập kế vị") }
                : Array.Empty<ScreenCommand>();

            rows.Add(new ScreenRow(
                [
                    member.Name,
                    member.Role,
                    GameDate.FromDayIndex(member.DayIndex).ToString(),
                    member.IsHeir ? "người kế vị" : "—"
                ],
                rowCommands,
                Title: member.Name,
                Details:
                [
                    new ScreenDetail("Vai trò", member.Role),
                    new ScreenDetail("Ngày", GameDate.FromDayIndex(member.DayIndex).ToString()),
                    new ScreenDetail("Kế vị", member.IsHeir ? "đang là người kế vị" : "chưa")
                ]));
        }

        var commands = new List<ScreenCommand>();
        if (!_service.IsMarried)
        {
            commands.Add(new ScreenCommand("marry", $"Cầu hôn ({LineageService.DowryCost} Quan)"));
        }
        else
        {
            commands.Add(new ScreenCommand("beget", $"Sinh con ({LineageService.ChildCost} Quan)"));
        }

        var status = _service.IsMarried
            ? $"Chính thất: {_service.SpouseName} · {_service.ChildCount} con · phí tháng {_service.MonthlyUpkeep} Quan"
            : "Chưa lập gia thất.";

        var alerts = new List<ScreenAlert>();
        if (!_service.IsMarried)
        {
            alerts.Add(new ScreenAlert("Chưa lập gia thất: chưa có người nối dõi.", AlertLevel.Info));
        }
        else if (_service.ChildCount > 0 && _service.HeirName is null)
        {
            alerts.Add(new ScreenAlert("Đã có con nhưng chưa lập người kế vị.", AlertLevel.Warning));
        }

        return new ScreenModel(
            ScreenId,
            Title,
            status,
            [new ScreenSection("Gia phả", Columns, rows)],
            commands,
            [
                new ScreenMetric("Chính thất", _service.SpouseName ?? "—", _service.IsMarried ? "đã thành hôn" : "chưa cưới",
                    _service.IsMarried ? MetricTone.Positive : MetricTone.Neutral),
                new ScreenMetric("Con", _service.ChildCount.ToString(System.Globalization.CultureInfo.InvariantCulture), "người"),
                new ScreenMetric("Kế vị", _service.HeirName ?? "—", _service.HeirName is null ? "chưa lập" : "đã định"),
                new ScreenMetric("Phí tháng", _service.MonthlyUpkeep.ToString(System.Globalization.CultureInfo.InvariantCulture), "quan/tháng")
            ],
            alerts);
    }
}

internal sealed class FamilyCommandHandler : IScreenCommandHandler
{
    private readonly LineageService _service;

    public FamilyCommandHandler(LineageService service) => _service = service;

    public string ScreenId => "lineage.family";

    public string? Execute(string commandId)
    {
        if (commandId == "marry")
        {
            return _service.Marry().Message;
        }

        if (commandId == "beget")
        {
            return _service.Beget().Message;
        }

        var separator = commandId.IndexOf(':');
        if (separator > 0 && commandId[..separator] == "heir" && long.TryParse(commandId[(separator + 1)..], out var childId))
        {
            return _service.DesignateHeir(childId).Message;
        }

        return $"Lệnh '{commandId}' không thuộc DLC lineage.";
    }
}
