using SimT.Contracts.Ui;

namespace SimT.Modules.Dlc.Politics;

internal sealed class CourtScreen : IScreenContribution
{
    private static readonly string[] Columns = ["Chức vụ", "Phẩm", "Bổng/tháng", "Uy tín cần", "Trạng thái"];

    private readonly CourtService _service;

    public CourtScreen(CourtService service) => _service = service;

    public string ScreenId => "politics.court";

    public string Title => "Quan Lộ";

    public int Order => 55;

    public ScreenModel Render()
    {
        var rows = new List<ScreenRow>();
        foreach (var office in _service.Offices)
        {
            var holding = _service.OfficeId == office.Id;
            var eligible = _service.OfficeId == 0 && _service.Reputation >= office.ReputationRequired;

            rows.Add(new ScreenRow(
                [
                    $"{office.NameVi} ({office.NameEn})",
                    office.Rank.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    office.MonthlyStipend.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    office.ReputationRequired.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    holding ? "đang tại chức" : eligible ? "đủ điều kiện" : "chưa đủ uy tín"
                ],
                eligible ? [new ScreenCommand($"apply:{office.Id}", "Nhậm chức")] : Array.Empty<ScreenCommand>()));
        }

        var commands = new List<ScreenCommand>
        {
            new("serve", $"Cung sự ({CourtService.ServiceDays} ngày, +{CourtService.ServiceReputation} uy tín)")
        };

        if (_service.OfficeId != 0)
        {
            commands.Add(new ScreenCommand("resign", "Từ chức"));
        }

        var status = _service.OfficeId == 0
            ? $"Uy tín {_service.Reputation} · chưa tại chức"
            : $"Uy tín {_service.Reputation} · {_service.OfficeName} · bổng {_service.MonthlyStipend} Quan/tháng";

        return new ScreenModel(
            ScreenId,
            Title,
            status,
            [new ScreenSection("Chức vụ", Columns, rows)],
            commands);
    }
}

internal sealed class CourtCommandHandler : IScreenCommandHandler
{
    private readonly CourtService _service;

    public CourtCommandHandler(CourtService service) => _service = service;

    public string ScreenId => "politics.court";

    public string? Execute(string commandId)
    {
        if (commandId == "serve")
        {
            return _service.Serve().Message;
        }

        if (commandId == "resign")
        {
            return _service.Resign().Message;
        }

        var separator = commandId.IndexOf(':');
        if (separator > 0 && commandId[..separator] == "apply" && long.TryParse(commandId[(separator + 1)..], out var officeId))
        {
            return _service.Apply(officeId).Message;
        }

        return $"Lệnh '{commandId}' không thuộc DLC politics.";
    }
}
