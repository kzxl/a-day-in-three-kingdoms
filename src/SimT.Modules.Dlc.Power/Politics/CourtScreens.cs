using SimT.Contracts.Playing;
using SimT.Contracts.Ui;

namespace SimT.Modules.Dlc.Power;

internal sealed class CourtScreen : IScreenContribution
{
    private static string Fmt(long value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static readonly string[] OfficeColumns = ["Chức vụ", "Phẩm", "Bổng/tháng", "Uy tín cần", "Trạng thái"];
    private static readonly string[] FactionColumns = ["Thế Lực / Minh Chủ", "Thủ Phủ", "Lý Tưởng Cát Cứ", "Binh Lực", "Quốc Khố", "Quan Hệ"];
    private static readonly string[] CityColumns = ["Thành Trì", "Thế Lực Chiếm Giữ", "Bất Ổn", "Phòng Thủ", "Thuế Suất", "Hành Chính Địa Phương"];
    private static readonly string[] FiefColumns = ["Phong Ấp Châu Quận", "Dân Số & Binh Lực", "Nông Khẩn", "Thương Nghiệp", "Thành Phòng", "Trị An", "Ban Bệ Phụ Tá", "Phương Châm", "Ngân Khố Nha Môn"];
    private static readonly string[] DigestColumns = ["Thời Điểm", "Phương Châm", "Biến Động Dân & Khố", "Binh Lực & Lương Thực", "Huân Công", "Tường Trình Nha Môn"];
    private static readonly string[] MemorialColumns = ["Ngày", "Thể Loại", "Sớ Thượng Tấu", "Kết Quả", "Công Trạng", "Hồi Âm Triều Đình"];
    private static readonly string[] CareerColumns = ["Ngày", "Biến Chuyển", "Chức Vị Cũ", "Chức Vị Mới", "Công Trạng", "Chiếu Chỉ Minh Chủ"];
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
                    office.NameVi,
                    $"Phẩm {office.Rank}",
                    $"{office.MonthlyStipend} Quan",
                    office.ReputationRequired.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    holding ? "★ Đang tại chức" : eligible ? "Đủ điều kiện" : "Chưa đủ uy tín"
                ],
                eligible ? [new ScreenCommand($"apply:{office.Id}", "Nhậm chức")] : Array.Empty<ScreenCommand>()));
        }

        // Section 2: Fiefdom (Thái Thú Phủ)
        var fiefRows = new List<ScreenRow>();
        var fief = _service.PlayerFief;
        if (fief is not null)
        {
            fiefRows.Add(new ScreenRow(
                [
                    $"🏛️ {fief.CitadelNameVi} (Trấn nhậm)",
                    $"👥 {fief.Population:N0} dân · ⚔️ {fief.GarrisonTroops:N0} binh ({fief.WeaponsStock} vk)",
                    $"{fief.Agriculture}/100",
                    $"{fief.Commerce}/100",
                    $"{fief.Fortification}/100",
                    $"{fief.PublicOrder}/100",
                    $"Văn: {fief.CivilAideNameVi} | Võ: {fief.MilitaryAideNameVi}",
                    fief.PolicyDirective switch
                    {
                        "AGRI" => "🌾 Trọng Nông Tích Cốc",
                        "COMM" => "💰 Trọng Thương Thu Thuế",
                        "MILITARY" => "⚔️ Quân Bị Tiễu Phỉ",
                        _ => "⚖️ Cân Bằng Toàn Diện"
                    },
                    $"{fief.LocalTreasury:N0} Quan"
                ],
                [
                    new ScreenCommand("fief_directive:AGRI", "Đổi: Trọng Nông 🌾"),
                    new ScreenCommand("fief_directive:COMM", "Đổi: Trọng Thương 💰"),
                    new ScreenCommand("fief_directive:MILITARY", "Đổi: Quân Bị ⚔️"),
                    new ScreenCommand("fief_directive:BALANCE", "Đổi: Cân Bằng ⚖️"),
                    new ScreenCommand("develop_fief:agriculture", "Khuyến Nông (65q)"),
                    new ScreenCommand("develop_fief:commerce", "Mở Thị (65q)"),
                    new ScreenCommand("develop_fief:defense", "Cố Thủ (65q)"),
                    new ScreenCommand("develop_fief:order", "Tuần Phòng (65q)"),
                    new ScreenCommand("eval_governance", "Khảo Hạch Quý 📜", CommandKind.Primary),
                    new ScreenCommand("resign_governor", "Bàn Giao Ấn", CommandKind.Danger)
                ]));
        }

        // Section 3: Monthly Governance Digest (Tổng Kết Chánh Vụ Hàng Tháng)
        var digestRows = new List<ScreenRow>();
        foreach (var d in _service.RecentMonthlyDigests(6))
        {
            digestRows.Add(new ScreenRow(
                [
                    $"Năm {d.Year} Tháng {d.Month}",
                    d.Directive switch
                    {
                        "AGRI" => "🌾 Trọng Nông",
                        "COMM" => "💰 Trọng Thương",
                        "MILITARY" => "⚔️ Quân Bị",
                        _ => "⚖️ Cân Bằng"
                    },
                    $"Dân số: {(d.PopulationDelta >= 0 ? "+" : "")}{d.PopulationDelta:N0} · Khố: {(d.TreasuryDelta >= 0 ? "+" : "")}{d.TreasuryDelta:N0}q",
                    $"Lương: -{d.FoodConsumed:N0} · Binh: +{d.GarrisonDelta:N0}",
                    $"+{d.MeritsEarned} huân công",
                    d.SummaryVi
                ],
                Array.Empty<ScreenCommand>()));
        }

        // Section 3: Memorials (Triều Hội Thượng Tấu)
        var memorialRows = new List<ScreenRow>();
        foreach (var mem in _service.RecentMemorials(5))
        {
            memorialRows.Add(new ScreenRow(
                [
                    $"Ngày {mem.DayIndex}",
                    mem.Category,
                    mem.TitleVi,
                    mem.Outcome == "ACCEPTED" ? "✅ Chuẩn Y" : "❌ Bác Bỏ",
                    $"+{mem.MeritAwarded}",
                    mem.ImpactNarrativeVi
                ],
                Array.Empty<ScreenCommand>()));
        }

        // Section 4: Factions
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

        // Section 5: Regional Governance
        var govRows = new List<ScreenRow>();
        foreach (var gov in _service.Governances)
        {
            var cityName = citadelNames.TryGetValue(gov.CitadelId, out var cn) ? cn : $"Thành #{gov.CitadelId}";
            var facName = factionMap.TryGetValue(gov.FactionId, out var f) ? f.NameVi : "Vô chủ";
            var isCurrentCity = gov.CitadelId == _player.CurrentCityId;
            var isMyFief = _service.GovernorCityId == gov.CitadelId;

            var actionCmds = new List<ScreenCommand>();
            actionCmds.Add(new ScreenCommand($"citadel:inspect:{gov.CitadelId}", "Khảo Sát 🏰"));

            if (isCurrentCity)
            {
                actionCmds.Add(new ScreenCommand($"policy:relief:{gov.CitadelId}", "An Dân (-15% loạn)"));
                actionCmds.Add(new ScreenCommand($"policy:levy:{gov.CitadelId}", "Tuyển Binh (+10 thủ)"));

                if (_service.OfficeId > 0)
                {
                    actionCmds.Add(new ScreenCommand($"policy:tax:{gov.CitadelId}", "Trưng Thuế (+120q)"));
                    actionCmds.Add(new ScreenCommand($"policy:land_reclaim:{gov.CitadelId}", "Đồn Điền (100q)", CommandKind.Primary));
                }

                if (gov.UnrestLevel >= 35)
                {
                    actionCmds.Add(new ScreenCommand($"policy:pacify:{gov.CitadelId}", "Dẹp Loạn (-25% loạn)", CommandKind.Danger));
                }

                var rank = _service.Offices.FirstOrDefault(o => o.Id == _service.OfficeId)?.Rank ?? 0;
                if (rank >= 3 && _service.GovernorCityId == 0 && gov.FactionId == _service.AllegianceFactionId)
                {
                    actionCmds.Add(new ScreenCommand($"appoint_governor:{gov.CitadelId}", "Tiếp Quản Thái Thú", CommandKind.Primary));
                }
            }

            govRows.Add(new ScreenRow(
                [
                    isMyFief ? $"👑 {cityName} (Phong ấp)" : isCurrentCity ? $"📍 {cityName} (Hiện tại)" : cityName,
                    facName,
                    $"{gov.UnrestLevel}%",
                    $"{gov.DefenseStrength} điểm",
                    $"{gov.TaxRatePercent}%",
                    isMyFief ? "Thái thú cai quản" : isCurrentCity ? "Đang quản trị" : "Xa xôi"
                ],
                actionCmds));
        }

        // Section 6: Career Logs
        var careerRows = new List<ScreenRow>();
        foreach (var car in _service.RecentCareerLogs(6))
        {
            careerRows.Add(new ScreenRow(
                [
                    $"Ngày {car.DayIndex}",
                    car.ChangeType switch
                    {
                        "PROMOTION" => "🎉 Thăng Quan",
                        "DEMOTION" => "⚠️ Giáng Cấp",
                        "APPOINTMENT" => "📜 Bổ Nhiệm",
                        "DISMISSAL" => "🚪 Từ Nhiệm",
                        _ => car.ChangeType
                    },
                    car.OldOfficeNameVi,
                    car.NewOfficeNameVi,
                    car.MeritsDelta >= 0 ? $"+{car.MeritsDelta}" : $"{car.MeritsDelta}",
                    car.ReasonVi
                ],
                Array.Empty<ScreenCommand>()));
        }

        // Section 7: Decree Logs
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
                        "land_reclaim" => "Đồn Điền Khai Hoang",
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
            commands.Add(new ScreenCommand("promote", "Thăng Quan [Cầu Phong]", CommandKind.Primary));
            commands.Add(new ScreenCommand("resign", "Từ quan", CommandKind.Danger));
        }

        if (_service.AllegianceFactionId != 0)
        {
            var leader = _service.Factions.First(f => f.Id == _service.AllegianceFactionId).LeaderNpcId;
            commands.Add(new ScreenCommand($"talk:npc:{leader}", "Bái kiến chúa công"));
            commands.Add(new ScreenCommand("lord_report", "Trình lên"));
            commands.Add(new ScreenCommand("memorial:reform:khuyen_nong", "Thượng Tấu Canh Tân"));
            commands.Add(new ScreenCommand("memorial:military:diet_phi", "Thượng Tấu Binh Cơ"));
            commands.Add(new ScreenCommand("leave_faction", "Rời chư hầu", CommandKind.Secondary));
        }

        var status = _service.OfficeId == 0
            ? $"Uy tín {_service.Reputation} · Công trạng {_service.Merits} · Chưa tại chức · Minh chủ: {_service.AllegianceFactionName ?? "Tự do"}"
            : $"Uy tín {_service.Reputation} · Công trạng {_service.Merits} · {_service.OfficeName} · Bổng {_service.MonthlyStipend} Quan/tháng · Minh chủ: {_service.AllegianceFactionName ?? "Tự do"}";

        var alerts = new List<ScreenAlert>();
        if (_service.LordTasks is { } tasks)
        {
            if (_service.AllegianceFactionId != 0)
                alerts.Add(new ScreenAlert($"Tín nhiệm {tasks.Trust}/100 · Trượt liên tiếp {tasks.ConsecutiveFailures}/3", AlertLevel.Info));
            if (!string.IsNullOrWhiteSpace(tasks.Notice)) alerts.Add(new ScreenAlert(tasks.Notice, AlertLevel.Info));
        }

        var next = _service.Offices.FirstOrDefault(office => office.ReputationRequired > _service.Reputation);
        if (next is not null && _service.OfficeId == 0)
        {
            alerts.Add(new ScreenAlert($"Cần thêm {next.ReputationRequired - _service.Reputation} uy tín để nhận {next.NameVi}.", AlertLevel.Info));
        }

        var totalStipend = _service.MonthlyStipend + (_service.AllegianceFactionId > 0 ? CourtService.FactionStipendBonus : 0);

        var sections = new List<ScreenSection>
        {
            new("Cửu Phẩm Quan Chế Triều Đình", OfficeColumns, officeRows, SectionViewMode.ImperialCourtChamber)
        };

        if (fiefRows.Count > 0)
        {
            sections.Add(new ScreenSection("Thái Thú Phủ & Phong Ấp Châu Quận", FiefColumns, fiefRows, SectionViewMode.ImperialCourtChamber));
        }

        if (digestRows.Count > 0)
        {
            sections.Add(new ScreenSection("Tổng Kết Chánh Vụ Phong Ấp Hàng Tháng", DigestColumns, digestRows));
        }

        if (memorialRows.Count > 0)
        {
            sections.Add(new ScreenSection("Triều Hội Nghị Sự & Thượng Tấu", MemorialColumns, memorialRows));
        }

        sections.Add(new ScreenSection("Thế Lực Chư Hầu Tam Quốc (12 Lộ)", FactionColumns, factionRows, SectionViewMode.ImperialCourtChamber));
        sections.Add(new ScreenSection($"Chính Sự & Trị An Châu Quận ({govRows.Count} Thành)", CityColumns, govRows, SectionViewMode.ImperialCourtChamber));

        if (careerRows.Count > 0)
        {
            sections.Add(new ScreenSection("Biên Niên Tiến Thoái Quan Trường (Thăng Quan & Giáng Cấp)", CareerColumns, careerRows));
        }

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
                new ScreenMetric("Công Trạng", Fmt(_service.Merits), "huân công", MetricTone.Positive),
                new ScreenMetric("Tại Chức", _service.OfficeName ?? "—",
                    _service.OfficeId == 0 ? "Chưa nhận chức" : $"Bổng {_service.MonthlyStipend} quan/tháng",
                    _service.OfficeId == 0 ? MetricTone.Neutral : MetricTone.Positive),
                new ScreenMetric("Phong Ấp", fief?.CitadelNameVi ?? "Chưa có",
                    fief is null ? "Chưa phong đất" : "Trấn nhậm thái thú",
                    fief is null ? MetricTone.Neutral : MetricTone.Positive),
                new ScreenMetric("Minh Chủ", _service.AllegianceFactionName ?? "Tự do",
                    _service.AllegianceFactionId == 0 ? "Chưa quy thuận" : "Quân kỳ bảo hộ",
                    _service.AllegianceFactionId == 0 ? MetricTone.Neutral : MetricTone.Positive),
                new ScreenMetric("Tổng Bổng Lộc", Fmt(totalStipend), "quan/tháng")
            ],
            alerts,
            _service.OfficeId == 0
                ? "Tích lũy uy tín và công trạng qua việc nghĩa, dẹp loạn để ứng tuyển chức quan đầu tiên và lĩnh bổng lộc."
                : "Thực hiện công vụ châu quận, dâng sớ thượng tấu và cai quản phong ấp để thăng quan tiến chức; giữ trị an để tránh bị giáng chức.");
    }
}

internal sealed class CourtCommandHandler : IScreenCommandHandler
{
    private readonly CourtService _service;

    public CourtCommandHandler(CourtService service) => _service = service;

    public string ScreenId => "politics.court";

    public string? Execute(string commandId)
    {
        if (commandId == "lord_report") return _service.LordTasks?.Report();
        if (commandId == "serve") return _service.Serve().Message;
        if (commandId == "promote") return _service.RequestPromotion().Message;
        if (commandId == "demote") return _service.DemoteOffice("Bị minh chủ hạ chiếu khiển trách, giáng bớt phẩm hàm.").Message;
        if (commandId == "resign") return _service.Resign().Message;
        if (commandId == "resign_governor") return _service.ResignGovernor().Message;
        if (commandId == "leave_faction") return _service.LeaveFaction().Message;
        if (commandId == "eval_governance")
        {
            var eval = _service.EvaluateQuarterlyGovernance();
            return eval is null
                ? "Chưa cai quản phong ấp nào để tiến hành khảo hạch."
                : $"{eval.Grade} · {eval.RankAction}: {eval.NarrativeVi} (Công trạng: {(eval.MeritChange >= 0 ? "+" : "")}{eval.MeritChange}, Thưởng: {eval.BonusStipend} Quan)";
        }

        var parts = commandId.Split(':');
        if (parts.Length >= 2)
        {
            if (parts[0] == "fief_directive")
            {
                return _service.SetFiefDirective(parts[1]).Message;
            }

            if (parts[0] == "appoint_aide" && parts.Length >= 3 && long.TryParse(parts[2], out var aideNpcId))
            {
                return _service.AppointFiefAide(aideNpcId, parts[1]).Message;
            }

            if (parts[0] == "apply" && long.TryParse(parts[1], out var officeId))
            {
                return _service.Apply(officeId).Message;
            }

            if ((parts[0] == "join" || parts[0] == "join_faction") && long.TryParse(parts[1], out var factionId))
            {
                return _service.JoinFaction(factionId).Message;
            }

            if (parts[0] == "appoint_governor" && long.TryParse(parts[1], out var govCitId))
            {
                return _service.AppointGovernor(govCitId).Message;
            }

            if (parts[0] == "develop_fief")
            {
                return _service.DevelopFief(parts[1]).Message;
            }

            if (parts[0] == "memorial")
            {
                var category = parts[1];
                var proposal = parts.Length > 2 ? parts[2] : "Hiến sách an bang định quốc";
                return _service.SubmitMemorial(category, proposal).Message;
            }
        }

        if (parts.Length == 3 && parts[0] == "policy" && long.TryParse(parts[2], out var citId))
        {
            return _service.EnactPolicy(parts[1], citId).Message;
        }

        return $"Lệnh '{commandId}' không thuộc DLC politics.";
    }
}
