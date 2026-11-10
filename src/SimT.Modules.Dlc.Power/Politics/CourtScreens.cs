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
    private static readonly string[] CrisisColumns = ["Thời Điểm", "Biến Cố Châu Quận", "Tình Hình Khẩn Cấp", "Mức Độ", "Trạng Thái", "Quyết Sách / Hệ Quả"];
    private static readonly string[] SiegeColumns = ["Thời Điểm", "Địch Thủ Vây Thành", "Binh Lực Địch/Ta", "Chiến Thuật", "Thương Vong", "Chiến Tích & Huân Công", "Diễn Biến Chiến Cuộc"];
    private static readonly string[] MegaprojectColumns = ["Đại Công Trình", "Quy Mô & Tác Dụng", "Kinh Phí Cần", "Đã Đầu Tư", "Tiến Độ", "Trạng Thái"];
    private static readonly string[] ExpeditionColumns = ["Thời Điểm", "Chiến Dịch Viễn Chinh", "Binh Lực Xuất Chinh", "Thương Vong", "Chiến Lợi Phẩm / Huân Công", "Tường Trình Chiến Tích"];
    private static readonly string[] DigestColumns = ["Thời Điểm", "Phương Châm", "Biến Động Dân & Khố", "Binh Lực & Lương Thực", "Huân Công", "Tường Trình Nha Môn"];
    private static readonly string[] MemorialColumns = ["Ngày", "Thể Loại", "Sớ Thượng Tấu", "Kết Quả", "Công Trạng", "Hồi Âm Triều Đình"];
    private static readonly string[] CareerColumns = ["Ngày", "Biến Chuyển", "Chức Vị Cũ", "Chức Vị Mới", "Công Trạng", "Chiếu Chỉ Minh Chủ"];
    private static readonly string[] LogColumns = ["Ngày", "Thành", "Chính Sách", "Chi Phí", "Uy Tín", "Bất Ổn", "Tường Trình"];
    private static readonly string[] AideColumns = ["Danh Sĩ Châu Quận", "Đảm Nhiệm Hiện Tại", "Chỉ Số Chánh Trị / Quân Bị", "Đặc Tính Tính Cách", "Tác Động Chánh Vụ Phong Ấp"];
    private static readonly string[] ScrutinyColumns = ["Thế Lực Sở Tại", "Mức Độ Dòm Ngó", "Áp Lực Quân Phiệt", "Sản Nghiệp / Giới Hạn Che Chở", "Phụ Thu Thuế Chợ", "Tình Trạng & Trát Lệnh"];
    private static readonly string[] PhilanthropyColumns = ["Thời Điểm", "Bá Tánh Thụ Hưởng", "Ngân Khoản Phát Chẩn", "Danh Vọng & Đức Độ", "Hạ Nhiệt Dòm Ngó", "Tường Trình Thiện Hạnh"];

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
                    new ScreenCommand("launch_expedition:PACIFY_BANDITS", "Tiễu Phạt Sơn Tặc ⚔️", CommandKind.Primary),
                    new ScreenCommand("launch_expedition:BORDER_PATROL", "Thao Diễn Biên Ải 🛡️"),
                    new ScreenCommand("launch_expedition:SUPPORT_LORD", "Chi Viện Minh Chủ 🚩"),
                    new ScreenCommand("trigger_crisis", "Khảo Sát Biến Cố 🚨"),
                    new ScreenCommand("simulate_siege:FORTIFY_WALLS", "Thủ Thành Cự Địch 🏰", CommandKind.Primary),
                    new ScreenCommand("simulate_siege:AMBUSH_FIRE", "Hỏa Công Đột Kích 🔥"),
                    new ScreenCommand("eval_governance", "Khảo Hạch Quý 📜", CommandKind.Primary),
                    new ScreenCommand("resign_governor", "Bàn Giao Ấn", CommandKind.Danger)
                ]));
        }

        // Section: Active & Recent Fief Crises (Biến Cố Châu Quận)
        var crisisRows = new List<ScreenRow>();
        if (fief is not null)
        {
            foreach (var cr in _service.GetRecentFiefCrises(6))
            {
                var crCmds = new List<ScreenCommand>();
                if (cr.Status == "PENDING")
                {
                    if (cr.CrisisCode == "CRISIS_FLOOD")
                    {
                        crCmds.Add(new ScreenCommand($"resolve_crisis:{cr.Id}:DISPATCH_CIVIL", "Đắp đê khẩn"));
                        crCmds.Add(new ScreenCommand($"resolve_crisis:{cr.Id}:EMERGENCY_FUNDS", "Phát chẩn", CommandKind.Primary));
                    }
                    else if (cr.CrisisCode == "CRISIS_PLAGUE")
                    {
                        crCmds.Add(new ScreenCommand($"resolve_crisis:{cr.Id}:ESTABLISH_CLINIC", "Lập dược xá", CommandKind.Primary));
                        crCmds.Add(new ScreenCommand($"resolve_crisis:{cr.Id}:QUARANTINE", "Cách ly"));
                    }
                    else if (cr.CrisisCode == "CRISIS_BANDIT_RAID")
                    {
                        crCmds.Add(new ScreenCommand($"resolve_crisis:{cr.Id}:DISPATCH_MILITARY", "Đô Úy diệt phỉ", CommandKind.Primary));
                        crCmds.Add(new ScreenCommand($"resolve_crisis:{cr.Id}:DEFEND_WALLS", "Đóng cửa thành"));
                    }
                    else if (cr.CrisisCode == "CRISIS_SECRET_ENVOY")
                    {
                        crCmds.Add(new ScreenCommand($"resolve_crisis:{cr.Id}:BEHEAD_ENVOY", "Trảm sứ nộp đầu", CommandKind.Danger));
                        crCmds.Add(new ScreenCommand($"resolve_crisis:{cr.Id}:CONFISCATE", "Tịch thu hoàng kim", CommandKind.Primary));
                    }
                }

                crisisRows.Add(new ScreenRow(
                    [
                        $"Ngày {cr.CreatedDay}",
                        cr.TitleVi,
                        cr.DescriptionVi,
                        cr.Severity > 1 ? "⚠️ Khẩn cấp" : "Thông thường",
                        cr.Status == "PENDING" ? "🚨 Đang Chờ Quyết Sách" : "✅ Đã Bình Định",
                        cr.Status == "PENDING" ? "Chưa giải quyết" : cr.OutcomeVi
                    ],
                    crCmds));
            }
        }

        // Section: Aide Recruitment & Talent Scouting (Chiêu Hiền Sảnh)
        var candidateRows = new List<ScreenRow>();
        if (fief is not null)
        {
            var candidates = _service.GetAideCandidates(fief.CitadelId);
            foreach (var c in candidates)
            {
                var cmdList = new List<ScreenCommand>();
                if (c.IsCurrentCivilAide)
                {
                    cmdList.Add(new ScreenCommand("appoint_aide:CIVIL:0", "Bãi Miễn Trưởng Thừa", CommandKind.Danger));
                }
                else if (c.IsCurrentMilitaryAide)
                {
                    cmdList.Add(new ScreenCommand("appoint_aide:MILITARY:0", "Bãi Miễn Đô Úy", CommandKind.Danger));
                }
                else
                {
                    cmdList.Add(new ScreenCommand($"appoint_aide:CIVIL:{c.NpcId}", "Cử Trưởng Thừa 🌾", CommandKind.Primary));
                    cmdList.Add(new ScreenCommand($"appoint_aide:MILITARY:{c.NpcId}", "Cử Đô Úy ⚔️", CommandKind.Secondary));
                }

                candidateRows.Add(new ScreenRow(
                    [
                        c.NameVi,
                        c.IsCurrentCivilAide ? "★ Trưởng Thừa (Văn)" : c.IsCurrentMilitaryAide ? "★ Đô Úy (Võ)" : "Chưa bổ nhiệm",
                        $"Chính {c.Politics} · Trí {c.Intellect} · Vũ {c.War} · Mị {c.Charm}",
                        string.Join(", ", c.TraitsVi.Count > 0 ? c.TraitsVi : ["Bình thường"]),
                        c.RecommendationVi
                    ],
                    cmdList));
            }
        }

        // Section: Citadel Siege Defense Logs (Chiến Báo Thủ Thành)
        var siegeRows = new List<ScreenRow>();
        foreach (var s in _service.RecentSiegeLogs(5))
        {
            siegeRows.Add(new ScreenRow(
                [
                    $"Ngày {s.DayIndex}",
                    s.AttackerFactionName,
                    $"{s.AttackerTroops:N0} địch vs {s.DefenseTroops:N0} ta",
                    s.TacticalStance switch
                    {
                        "FORTIFY_WALLS" => "🏰 Kiên Thủ Thành Quách",
                        "SALLY_OUT" => "⚔️ Xuất Thành Nghênh Chiến",
                        _ => "🔥 Hỏa Công Phục Kích"
                    },
                    $"Ta: -{s.DefenseCasualties:N0} | Địch: -{s.AttackerCasualties:N0}",
                    $"+{s.SpoilsCopper}q · +{s.MeritsEarned} huân công",
                    s.NarrativeVi
                ],
                Array.Empty<ScreenCommand>()));
        }

        // Section 3: Fief Megaprojects (Đại Công Trình Phong Ấp)
        var projectRows = new List<ScreenRow>();
        if (fief is not null)
        {
            foreach (var p in _service.GetFiefMegaprojects())
            {
                var pCmds = new List<ScreenCommand>();
                if (!p.IsCompleted)
                {
                    pCmds.Add(new ScreenCommand($"invest_megaproject:{p.ProjectCode}:300", "Đầu tư 300q"));
                    pCmds.Add(new ScreenCommand($"invest_megaproject:{p.ProjectCode}:600", "Đầu tư 600q", CommandKind.Primary));
                }

                projectRows.Add(new ScreenRow(
                    [
                        p.NameVi,
                        p.DescriptionVi,
                        $"{p.RequiredCopper:N0} Quan",
                        $"{p.InvestedCopper:N0} Quan",
                        $"{p.ProgressPercent}%",
                        p.IsCompleted ? "✅ Đã Hoàn Thành" : $"Đang Xây ({p.ProgressPercent}%)"
                    ],
                    pCmds));
            }
        }

        // Section 4: Fief Expedition Logs (Nhật Ký Xuất Chinh)
        var expRows = new List<ScreenRow>();
        foreach (var exp in _service.RecentExpeditionLogs(5))
        {
            expRows.Add(new ScreenRow(
                [
                    $"Ngày {exp.DayIndex}",
                    exp.ExpeditionType switch
                    {
                        "PACIFY_BANDITS" => "⚔️ Tiễu Phạt Sơn Tặc",
                        "BORDER_PATROL" => "🛡️ Thao Diễn Biên Ải",
                        "SUPPORT_LORD" => "🚩 Chi Viện Minh Chủ",
                        _ => exp.ExpeditionType
                    },
                    $"{exp.TroopsDispatched:N0} binh",
                    $"{exp.Casualties:N0} người",
                    $"+{exp.SpoilsCopper}q · +{exp.MeritsEarned} huân công",
                    exp.NarrativeVi
                ],
                Array.Empty<ScreenCommand>()));
        }

        // Section 5: Monthly Governance Digest (Tổng Kết Chánh Vụ Hàng Tháng)
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

        // Section: Warlord Scrutiny & Demands (Cây To Đón Gió - Sức Ép Quân Phiệt)
        var scrutinyRows = new List<ScreenRow>();
        var scrutiny = _service.CurrentScrutiny;
        if (scrutiny is not null)
        {
            var threatBadge = scrutiny.ThreatLevel switch
            {
                "AN_TOAN" => "🟢 Bình An (0-39)",
                "DO_XET" => "🟡 Dò Xét (40-59)",
                "SACH_NHIEU" => "🟠 Sách Nhiễu (60-84)",
                _ => "🔴 Hiểm Họa (85-100)"
            };

            var cmds = new List<ScreenCommand>
            {
                new("scrutiny:donate:200", "Cứu Tế Dân Nghèo (200q) 🌾", CommandKind.Primary),
                new("scrutiny:donate:500", "Đại Phát Chẩn (500q) 🕊️"),
                new("scrutiny:donate:1000", "Công Đức Nghìn Vàng (1000q) 🏛️")
            };

            scrutinyRows.Add(new ScreenRow(
                [
                    $"🚩 {scrutiny.FactionNameVi} ({scrutiny.LeaderNameVi})",
                    threatBadge,
                    $"{scrutiny.ScrutinyScore}/100 điểm dòm ngó",
                    $"Tiền mặt: {scrutiny.LiquidWealth:N0}q / Giới hạn: {scrutiny.ShieldingThreshold:N0}q",
                    scrutiny.SurchargePercent > 0 ? $"+{scrutiny.SurchargePercent}% phụ thu" : "Không",
                    scrutiny.IsEmbargoed ? $"⛔ Cấm vận ({scrutiny.EmbargoRemainingDays} ngày)" : scrutiny.StatusSummaryVi
                ],
                cmds));

            foreach (var dem in scrutiny.ActiveDemands)
            {
                scrutinyRows.Add(new ScreenRow(
                    [
                        $"⚡ TRÁT LỆNH #{dem.Id}",
                        dem.TitleVi,
                        $"{dem.DemandCopper:N0} Quan",
                        $"Hạn: Ngày {dem.DueDayIndex}",
                        "🚨 Khẩn cấp",
                        dem.DescriptionVi
                    ],
                    [
                        new ScreenCommand($"scrutiny:pay:{dem.Id}", $"Nộp Đủ ({dem.DemandCopper}q)", CommandKind.Primary),
                        new ScreenCommand($"scrutiny:bribe:{dem.Id}", $"Đút Lót ({dem.DemandCopper / 2}q)"),
                        new ScreenCommand($"scrutiny:defy:{dem.Id}", "Kháng Lệnh ⚔️", CommandKind.Danger)
                    ]));
            }
        }

        // Section: Philanthropy Logs
        var philanthropyRows = new List<ScreenRow>();
        foreach (var phil in _service.RecentPhilanthropyLogs(5))
        {
            var citName = citadelNames.TryGetValue(phil.CitadelId, out var cn) ? cn : "Bổn châu";
            philanthropyRows.Add(new ScreenRow(
                [
                    $"Ngày {phil.DayIndex}",
                    citName,
                    $"{phil.CopperDonated:N0} Quan",
                    $"+{phil.FameEarned} Danh vọng / +{phil.VirtueEarned} Đức độ",
                    $"-{phil.ScrutinyReduced} điểm dòm ngó",
                    phil.NarrativeVi
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

        if (fief is not null)
        {
            var activeCrises = _service.GetActiveFiefCrises(fief.CitadelId);
            if (activeCrises.Count > 0)
            {
                alerts.Add(new ScreenAlert($"🚨 KHẨN BÁO PHONG ẤP: Có {activeCrises.Count} biến cố khẩn cấp đang chờ đại nhân định đoạt!", AlertLevel.Critical));
            }
            var fiefGov = _service.Governances.FirstOrDefault(g => g.CitadelId == fief.CitadelId);
            if (fiefGov is not null && fiefGov.UnrestLevel >= 35)
            {
                alerts.Add(new ScreenAlert($"⚠️ CẢNH BÁO TRỊ AN: Bất ổn {fief.CitadelNameVi} lên tới {fiefGov.UnrestLevel}%, nguy cơ bạo loạn!", AlertLevel.Warning));
            }
        }

        if (scrutiny is not null)
        {
            if (scrutiny.ThreatLevel == "HIEM_HOA")
            {
                alerts.Add(new ScreenAlert($"🚨 HIỂM HỌA QUÂN PHIỆT: Điểm dòm ngó tại {scrutiny.FactionNameVi} lên tới {scrutiny.ScrutinyScore}/100! Nguy cơ bị tịch biên sản nghiệp!", AlertLevel.Critical));
            }
            else if (scrutiny.ThreatLevel == "SACH_NHIEU")
            {
                alerts.Add(new ScreenAlert($"⚠️ SỨC ÉP QUÂN PHIỆT: Minh chủ {scrutiny.FactionNameVi} đang để mắt đòi quân phí ({scrutiny.ScrutinyScore}/100)!", AlertLevel.Warning));
            }
            if (scrutiny.IsEmbargoed)
            {
                alerts.Add(new ScreenAlert($"⛔ CẤM VẬN BUÔN BÁN: Lãnh địa {scrutiny.FactionNameVi} cấm giao thương còn {scrutiny.EmbargoRemainingDays} ngày do kháng lệnh nộp quân nhu!", AlertLevel.Warning));
            }
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

        if (candidateRows.Count > 0)
        {
            sections.Add(new ScreenSection("Chiêu Hiền Sảnh · Bổ Nhiệm Trưởng Thừa & Đô Úy Phong Ấp", AideColumns, candidateRows));
        }

        if (crisisRows.Count > 0)
        {
            sections.Add(new ScreenSection("Biến Cố Khẩn Cấp & Quyết Sách Châu Quận", CrisisColumns, crisisRows));
        }

        if (siegeRows.Count > 0)
        {
            sections.Add(new ScreenSection("Chiến Báo Công Thủ Thành Trì & Phản Kích", SiegeColumns, siegeRows));
        }

        if (projectRows.Count > 0)
        {
            sections.Add(new ScreenSection("Đại Công Trình Phong Ấp Châu Quận", MegaprojectColumns, projectRows));
        }

        if (expRows.Count > 0)
        {
            sections.Add(new ScreenSection("Nhật Ký Chiến Dịch Xuất Chinh Phong Ấp", ExpeditionColumns, expRows));
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

        sections.Add(new ScreenSection("Sức Ép Tài Phú & Quân Phiệt Dòm Ngó (Cây To Đón Gió)", ScrutinyColumns, scrutinyRows));
        if (philanthropyRows.Count > 0)
        {
            sections.Add(new ScreenSection("Nhật Ký Cứu Tế Dân Sinh & Thiện Hạnh", PhilanthropyColumns, philanthropyRows));
        }

        // Court Factions Section
        var courtStandings = _service.CourtFactionStandings;
        if (courtStandings.Count > 0)
        {
            var courtFactionColumns = new[] { "Thế Lực Triều Đình", "Đặc Tính & Chủ Trương", "Thân Thiện", "Thái Độ", "Vận Động Hành Lang" };
            var courtFactionRows = courtStandings.Select(cf =>
            {
                var scoreText = cf.StandingScore >= 0 ? $"+{cf.StandingScore}" : cf.StandingScore.ToString();
                return new ScreenRow(
                    [
                        cf.NameVi,
                        cf.DescriptionVi,
                        $"{scoreText} Điểm",
                        cf.AttitudeVi,
                        "Hỗ trợ ngân quỹ"
                    ],
                    [
                        new ScreenCommand($"court:lobby:{cf.FactionCode}:300", "Vận Động 🎁 (300Q)", CommandKind.Primary)
                    ],
                    Title: $"{cf.NameVi} ({scoreText} Điểm)",
                    Details:
                    [
                        new ScreenDetail("Thế Lực", cf.NameVi),
                        new ScreenDetail("Mô Tả", cf.DescriptionVi),
                        new ScreenDetail("Điểm Thân Thiện", $"{scoreText} (-100 đến +100)"),
                        new ScreenDetail("Thái Độ Triều Đình", cf.AttitudeVi)
                    ]);
            }).ToList();

            sections.Add(new ScreenSection("🏛️ Sóng Ngầm Quan Trường · Tam Đại Thế Lực Triều Đình", courtFactionColumns, courtFactionRows));
        }

        // Imperial Inspection Section
        var recentInspections = _service.RecentImperialInspections(5);
        if (recentInspections.Count > 0 || _service.OfficeId > 0)
        {
            var inspectColumns = new[] { "Thời Điểm", "Khâm Sai Đại Thần", "Đối Sách", "Kết Quả", "Tác Động", "Tường Thuật" };
            var inspectRows = recentInspections.Select(ins =>
            {
                var outcomeBadge = ins.Outcome switch
                {
                    "PRAISED" => "🟢 Khen Ngợi Phong Thưởng",
                    "PASSED" => "🟡 Chuẩn Y Thông Qua",
                    "REPRIMANDED" => "🟠 Khiển Trách Phạt Vạ",
                    "IMPEACHED" => "🔴 Đàn Hặc Cách Chức",
                    _ => ins.Outcome
                };
                var deltaText = ins.MeritDelta != 0
                    ? $"{ins.MeritDelta:+0;-0;0} Huân công"
                    : "—";

                return new ScreenRow(
                    [
                        $"Ngày {ins.DayIndex}",
                        ins.InspectorNameVi,
                        ins.ApproachCode,
                        outcomeBadge,
                        deltaText,
                        ins.NarrativeVi
                    ],
                    [],
                    Title: $"{ins.InspectorNameVi} — {outcomeBadge}",
                    Details:
                    [
                        new ScreenDetail("Thời Điểm", $"Ngày {ins.DayIndex}"),
                        new ScreenDetail("Khâm Sai", ins.InspectorNameVi),
                        new ScreenDetail("Đối Sách", ins.ApproachCode),
                        new ScreenDetail("Kết Quả", outcomeBadge),
                        new ScreenDetail("Biến Động", deltaText),
                        new ScreenDetail("Tường Thuật", ins.NarrativeVi)
                    ]);
            }).ToList();

            sections.Add(new ScreenSection("⚖️ Ngự Sử Đài · Khâm Sai Thanh Sát & Phong Kỷ Quan Trường", inspectColumns, inspectRows));
        }

        commands.Add(new ScreenCommand("inspect:respond:open_ledger", "Minh Bạch Sổ Sách 📜", CommandKind.Primary));
        commands.Add(new ScreenCommand("inspect:respond:persuade", "Biện Giải Hùng Biện 🗣️", CommandKind.Secondary));
        commands.Add(new ScreenCommand("inspect:respond:bribe", "Đút Lót Ngầm 🤫 (400Q)", CommandKind.Secondary));

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
                    fief is null ? "Chưa phong đất" : $"{fief.LocalTreasury:N0}q khố · {fief.GarrisonTroops:N0} binh",
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

        if (commandId == "trigger_crisis") return _service.TriggerEmergentFiefCrisis().Message;

        var parts = commandId.Split(':');
        if (parts.Length >= 2)
        {
            if (parts[0] == "resolve_crisis" && parts.Length >= 3 && long.TryParse(parts[1], out var crId))
            {
                return _service.ResolveFiefCrisis(crId, parts[2]).Message;
            }

            if (parts[0] == "simulate_siege")
            {
                var stance = parts[1];
                return _service.SimulateCitadelSiege("Đại Quân Cát Cứ", 1200, stance).Message;
            }

            if (parts[0] == "fief_directive")
            {
                return _service.SetFiefDirective(parts[1]).Message;
            }

            if (parts[0] == "appoint_aide" && parts.Length >= 3 && long.TryParse(parts[2], out var aideNpcId))
            {
                return _service.AppointFiefAide(aideNpcId, parts[1]).Message;
            }

            if (parts[0] == "invest_megaproject" && parts.Length >= 3 && int.TryParse(parts[2], out var investAmt))
            {
                return _service.InvestFiefMegaproject(parts[1], investAmt).Message;
            }

            if (parts[0] == "launch_expedition")
            {
                return _service.LaunchFiefExpedition(parts[1]).Message;
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

            if (parts[0] == "scrutiny")
            {
                if (parts.Length >= 3 && parts[1] == "pay" && long.TryParse(parts[2], out var payId))
                {
                    return _service.ResolveScrutinyDemand(payId, "PAY").Message;
                }
                if (parts.Length >= 3 && parts[1] == "bribe" && long.TryParse(parts[2], out var bribeId))
                {
                    return _service.ResolveScrutinyDemand(bribeId, "BRIBE").Message;
                }
                if (parts.Length >= 3 && parts[1] == "defy" && long.TryParse(parts[2], out var defyId))
                {
                    return _service.ResolveScrutinyDemand(defyId, "DEFY").Message;
                }
                if (parts.Length >= 3 && parts[1] == "donate" && int.TryParse(parts[2], out var donAmt))
                {
                    return _service.DonatePhilanthropy(donAmt).Message;
                }
            }
        }

        if (parts.Length == 3 && parts[0] == "policy" && long.TryParse(parts[2], out var citId))
        {
            return _service.EnactPolicy(parts[1], citId).Message;
        }

        if (commandId.StartsWith("court:lobby:"))
        {
            var lobbyParts = commandId["court:lobby:".Length..].Split(':');
            var code = lobbyParts[0];
            var amount = lobbyParts.Length > 1 && int.TryParse(lobbyParts[1], out var a) ? a : 300;
            return _service.LobbyCourtFaction(code, amount).Message;
        }

        if (commandId == "inspect:trigger")
        {
            return _service.TriggerImperialInspection().Message;
        }

        if (commandId.StartsWith("inspect:respond:"))
        {
            var approach = commandId["inspect:respond:".Length..];
            return _service.RespondToImperialInspection(approach).NarrativeVi;
        }

        return $"Lệnh '{commandId}' không thuộc DLC politics.";
    }
}
