using SimT.Contracts.Playing;
using SimT.Contracts.Ui;

namespace SimT.Modules.Dlc.Power;

internal sealed class CourtScreen : IScreenContribution
{
    private static string Fmt(long value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static readonly string[] OfficeColumns = ["Chức vụ", "Phẩm", "Bổng/tháng", "Uy tín cần", "Trạng thái"];
    private static readonly string[] FactionColumns = ["Thế Lực / Minh Chủ", "Thủ Phủ", "Lý Tưởng Cát Cứ", "Binh Lực", "Quốc Khố", "Quan Hệ"];
    private static readonly string[] CityColumns = ["Thành Trì", "Thế Lực Chiếm Giữ", "Bất Ổn", "Phòng Thủ", "Thuế Suất", "Hành Chính Địa Phương"];
    private static readonly string[] LogColumns = ["Ngày", "Thành", "Chính Sách", "Chi Phí", "Uy Tín", "Bất Ổn", "Tường Trình"];

    private readonly CourtService _service;
    private readonly IPlayerState _player;
    private readonly IWorldMap _map;

    public CourtScreen(CourtService service, IPlayerState player, IWorldMap map)
    {
        _service = service;
        _player = player;
        _map = map;
    }

    public string ScreenId => "politics.court";

    public string Title => "Quan Lộ";

    public int Order => 55;

    public ScreenModel Render()
    {
        var citadelNames = _map.Citadels.ToDictionary(c => c.Id, c => c.NameVi);
        var factionMap = _service.Factions.ToDictionary(f => f.Id, f => f);

        // Section 1: Offices
        var officeRows = new List<ScreenRow>();
        foreach (var office in _service.Offices)
        {
            var holding = _service.OfficeId == office.Id;
            var eligible = _service.OfficeId == 0 && _service.Reputation >= office.ReputationRequired;

            officeRows.Add(new ScreenRow(
                [
                    $"{office.NameVi} ({office.NameEn})",
                    $"Phẩm {office.Rank}",
                    $"{office.MonthlyStipend} Quan",
                    office.ReputationRequired.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    holding ? "Đang tại chức" : eligible ? "Đủ điều kiện" : "Chưa đủ uy tín"
                ],
                eligible ? [new ScreenCommand($"apply:{office.Id}", "Nhậm chức")] : Array.Empty<ScreenCommand>()));
        }

        // Section 2: Factions
        var factionRows = new List<ScreenRow>();
        foreach (var faction in _service.Factions)
        {
            var isAllegiant = _service.AllegianceFactionId == faction.Id;
            var capitalName = citadelNames.TryGetValue(faction.CapitalCityId, out var cap) ? cap : $"Thành #{faction.CapitalCityId}";

            var cmdList = new List<ScreenCommand>();
            cmdList.Add(new ScreenCommand($"faction:inspect:{faction.Id}", "Khảo Sát 🏛️"));
            if (!isAllegiant && _service.Reputation >= 5)
            {
                cmdList.Add(new ScreenCommand($"join_faction:{faction.Id}", "Quy thuận"));
            }

            factionRows.Add(new ScreenRow(
                [
                    $"{faction.NameVi} ({faction.Code})",
                    capitalName,
                    faction.IdealVi,
                    $"{faction.TroopCount:N0} binh",
                    $"{faction.TreasuryCopper:N0} Quan",
                    isAllegiant ? "★ ĐANG PHỤNG SỰ" : "Bình thường"
                ],
                cmdList));
        }

        // Section 3: Regional Governance
        var govRows = new List<ScreenRow>();
        foreach (var gov in _service.Governances)
        {
            var cityName = citadelNames.TryGetValue(gov.CitadelId, out var cn) ? cn : $"Thành #{gov.CitadelId}";
            var facName = factionMap.TryGetValue(gov.FactionId, out var f) ? f.NameVi : "Vô chủ";
            var isCurrentCity = gov.CitadelId == _player.CurrentCityId;

            var actionCmds = new List<ScreenCommand>();
            actionCmds.Add(new ScreenCommand($"citadel:inspect:{gov.CitadelId}", "Khảo Sát 🏰"));
            if (isCurrentCity)
            {
                actionCmds.Add(new ScreenCommand($"policy:relief:{gov.CitadelId}", "An Dân (-15% loạn)"));
                actionCmds.Add(new ScreenCommand($"policy:levy:{gov.CitadelId}", "Tuyển Binh (+10 thủ)"));

                if (_service.OfficeId > 0)
                {
                    actionCmds.Add(new ScreenCommand($"policy:tax:{gov.CitadelId}", "Trưng Thuế (+120q)"));
                }

                if (gov.UnrestLevel >= 35)
                {
                    actionCmds.Add(new ScreenCommand($"policy:pacify:{gov.CitadelId}", "Dẹp Loạn (-25% loạn)", CommandKind.Danger));
                }
            }

            govRows.Add(new ScreenRow(
                [
                    isCurrentCity ? $"📍 {cityName} (Hiện tại)" : cityName,
                    facName,
                    $"{gov.UnrestLevel}%",
                    $"{gov.DefenseStrength} điểm",
                    $"{gov.TaxRatePercent}%",
                    isCurrentCity ? "Đang quản trị" : "Xa xôi"
                ],
                actionCmds));
        }

        // Section 4: Decree Logs
        var logRows = new List<ScreenRow>();
        foreach (var log in _service.RecentDecrees(6))
        {
            var cityName = citadelNames.TryGetValue(log.CitadelId, out var cn) ? cn : $"Thành #{log.CitadelId}";
            logRows.Add(new ScreenRow(
                [
                    $"Ngày {log.DayIndex}",
                    cityName,
                    log.PolicyCode switch
                    {
                        "relief" => "An Dân Tế Bần",
                        "tax" => "Trưng Thu Thuế",
                        "levy" => "Chiêu Mộ Nghĩa Binh",
                        "pacify" => "Tiễu Phạt Dẹp Loạn",
                        _ => log.PolicyCode
                    },
                    $"{log.CostCopper} Quan",
                    $"+{log.ReputationGain}",
                    $"{log.UnrestDelta}%",
                    log.NarrativeVi
                ],
                Array.Empty<ScreenCommand>()));
        }

        // General Commands
        var commands = new List<ScreenCommand>
        {
            new("serve", $"Cung sự ({CourtService.ServiceDays} ngày, +{CourtService.ServiceReputation} uy tín)")
        };

        if (_service.OfficeId != 0)
        {
            commands.Add(new ScreenCommand("resign", "Từ quan", CommandKind.Danger));
        }

        if (_service.AllegianceFactionId != 0)
        {
            commands.Add(new ScreenCommand("leave_faction", "Rời chư hầu", CommandKind.Secondary));
        }

        var status = _service.OfficeId == 0
            ? $"Uy tín {_service.Reputation} · Chưa tại chức · Minh chủ: {_service.AllegianceFactionName ?? "Tự do"}"
            : $"Uy tín {_service.Reputation} · {_service.OfficeName} · Bổng {_service.MonthlyStipend} Quan/tháng · Minh chủ: {_service.AllegianceFactionName ?? "Tự do"}";

        var alerts = new List<ScreenAlert>();
        if (_service.OfficeId == 0)
        {
            var next = _service.Offices.FirstOrDefault(office => office.ReputationRequired > _service.Reputation);
            if (next is not null)
            {
                alerts.Add(new ScreenAlert(
                    $"Cần thêm {next.ReputationRequired - _service.Reputation} uy tín để nhận {next.NameVi}.",
                    AlertLevel.Info));
            }
        }

        var totalStipend = _service.MonthlyStipend + (_service.AllegianceFactionId > 0 ? CourtService.FactionStipendBonus : 0);

        var sections = new List<ScreenSection>
        {
            new("Cửu Phẩm Quan Chế Triều Đình", OfficeColumns, officeRows),
            new("Thế Lực Chư Hầu Tam Quốc (12 Lộ)", FactionColumns, factionRows),
            new($"Chính Sự & Trị An Châu Quận ({govRows.Count} Thành)", CityColumns, govRows)
        };

        if (logRows.Count > 0)
        {
            sections.Add(new ScreenSection("Nhật Ký Chiếu Chỉ & Ban Bố Gần Đây", LogColumns, logRows));
        }

        return new ScreenModel(
            ScreenId,
            Title,
            status,
            sections,
            commands,
            [
                new ScreenMetric("Uy Tín", Fmt(_service.Reputation), "điểm"),
                new ScreenMetric("Tại Chức", _service.OfficeName ?? "—",
                    _service.OfficeId == 0 ? "Chưa nhận chức" : $"Bổng {_service.MonthlyStipend} quan/tháng",
                    _service.OfficeId == 0 ? MetricTone.Neutral : MetricTone.Positive),
                new ScreenMetric("Minh Chủ", _service.AllegianceFactionName ?? "Tự do",
                    _service.AllegianceFactionId == 0 ? "Chưa quy thuận" : "Quân kỳ bảo hộ",
                    _service.AllegianceFactionId == 0 ? MetricTone.Neutral : MetricTone.Positive),
                new ScreenMetric("Tổng Bổng Lộc", Fmt(totalStipend), "quan/tháng")
            ],
            alerts,
            _service.OfficeId == 0
                ? "Tích lũy uy tín qua việc nghĩa và dẹp loạn để ứng tuyển chức quan đầu tiên và lĩnh bổng lộc."
                : "Thực hiện công vụ châu quận để tăng trị an, nâng uy tín và thăng tiến lên phẩm hàm cao hơn.");
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

        if (commandId == "leave_faction")
        {
            return _service.LeaveFaction().Message;
        }

        var parts = commandId.Split(':');
        if (parts.Length == 2)
        {
            if (parts[0] == "apply" && long.TryParse(parts[1], out var officeId))
            {
                return _service.Apply(officeId).Message;
            }

            if ((parts[0] == "join" || parts[0] == "join_faction") && long.TryParse(parts[1], out var factionId))
            {
                return _service.JoinFaction(factionId).Message;
            }
        }
        else if (parts.Length == 3 && parts[0] == "policy" && long.TryParse(parts[2], out var citId))
        {
            return _service.EnactPolicy(parts[1], citId).Message;
        }

        return $"Lệnh '{commandId}' không thuộc DLC politics.";
    }
}
