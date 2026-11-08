using SimT.Contracts.Time;
using SimT.Contracts.Ui;

namespace SimT.Modules.Dlc.Character;

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
                    GameDateCodec.ShortVi(GameDate.FromDayIndex(member.DayIndex)),
                    member.IsHeir ? "người kế vị" : "—"
                ],
                rowCommands,
                Title: member.Name,
                Details:
                [
                    new ScreenDetail("Vai trò", member.Role),
                    new ScreenDetail("Ngày", GameDateCodec.ShortVi(GameDate.FromDayIndex(member.DayIndex))),
                    new ScreenDetail("Kế vị", member.IsHeir ? "đang là người kế vị" : "chưa")
                ]));
        }

        var commands = new List<ScreenCommand>();
        if (!_service.IsMarried)
        {
            var suitors = _service.EligibleNpcSuitors();
            foreach (var suitor in suitors)
            {
                commands.Add(new ScreenCommand($"marry:{suitor.Npc.Id}", $"Cầu hôn {suitor.Npc.NameVi} ({suitor.Dowry} Quan)"));
            }
            commands.Add(new ScreenCommand("marry", suitors.Count > 0 ? $"Cầu hôn thứ dân ({LineageService.DowryCost} Quan)" : $"Cầu hôn ({LineageService.DowryCost} Quan)"));
        }
        else
        {
            commands.Add(new ScreenCommand("beget", $"Sinh con ({LineageService.ChildCost} Quan)"));
        }

        commands.Add(new ScreenCommand("hold_offering", "Đại Lễ Tế Tổ 🕯️ (80 Quan)"));
        if (_service.ShrineLevel < 5)
        {
            commands.Add(new ScreenCommand("upgrade_shrine", $"Tu Bổ Từ Đường (Cấp {_service.ShrineLevel + 1} · {_service.ShrineLevel * 500} Quan)"));
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

        var children = _service.Children;
        var sections = new List<ScreenSection>();

        if (_service.IsMarried)
        {
            var treeCols = new[] { "Thế Hệ", "Danh Xưng & Thân Tộc", "Tình Trạng", "Huyết Thống / Kế Thừa", "Định Hướng" };
            var treeRows = new List<ScreenRow>
            {
                new(
                    [
                        "Đời Thứ Nhất",
                        $"🏛️ Gia Chủ & Chính Thất [{_service.SpouseName}]",
                        "Chủ Hộ Gia Tộc",
                        $"Gia uy {_service.ClanPrestige} điểm",
                        "Bình an hưng thịnh"
                    ],
                    Array.Empty<ScreenCommand>(),
                    Title: "Gia Chủ & Chính Thất")
            };

            for (int i = 0; i < children.Count; i++)
            {
                var ch = children[i];
                var branch = i == children.Count - 1 ? "└── " : "├── ";
                var heirTag = ch.IsHeir ? "★ ĐÍCH TỬ KẾ THỪA" : "Thứ tử";
                var ageTag = ch.IsAdult ? $"{ch.Age} tuổi (Đã nhược quán)" : $"{ch.Age} tuổi (Thiếu niên)";
                var mentorDesc = ch.MentorName != null ? $"{ch.MentorName} (+{ch.StatBonus} {ch.FocusStat})" : "Chưa bái sư";

                treeRows.Add(new(
                    [
                        "Đời Thứ Hai",
                        $"{branch}👦 {ch.NameVi}",
                        ageTag,
                        $"{heirTag} · Sư phụ: {mentorDesc}",
                        ch.IsAdult ? "Có thể gánh vác gia nghiệp" : "Đang được dưỡng dục"
                    ],
                    ch.IsHeir ? Array.Empty<ScreenCommand>() : new[] { new ScreenCommand($"heir:{ch.Id}", "Lập Kế Vị") },
                    Title: $"{ch.NameVi} ({heirTag})",
                    Details:
                    [
                        new ScreenDetail("Thân Tộc", $"Đời thứ hai · Con của {_service.SpouseName}"),
                        new ScreenDetail("Tuổi & Trạng Thái", ageTag),
                        new ScreenDetail("Kế Thừa", heirTag),
                        new ScreenDetail("Gia Sư Bồi Dưỡng", mentorDesc)
                    ]));
            }

            sections.Add(new ScreenSection("🌿 Cây Phả Hệ Gia Tộc (Gia Tộc Tương Truyền)", treeCols, treeRows));
        }

        sections.Add(new("Gia phả", Columns, rows));
        if (children.Count > 0)
        {
            var childCols = new[] { "Tử Tức", "Tuổi Tác", "Kế Vị", "Danh Sư Giáo Dưỡng", "Thiên Hướng", "Chỉ Số Tăng Thêm" };
            var childRows = children.Select(c =>
            {
                var rowCmds = new List<ScreenCommand>();
                if (string.IsNullOrEmpty(c.MentorName))
                {
                    rowCmds.Add(new ScreenCommand($"tutor:{c.Id}:WAR", "Mời Thầy Võ ⚔"));
                    rowCmds.Add(new ScreenCommand($"tutor:{c.Id}:INT", "Mời Thầy Văn 📜"));
                }
                else
                {
                    rowCmds.Add(new ScreenCommand($"teach:{c.Id}", "Dạy Bảo 📖 (25 Quan)"));
                }

                if (!c.IsHeir)
                {
                    rowCmds.Add(new ScreenCommand($"heir:{c.Id}", "Lập Kế Vị"));
                }

                return new ScreenRow(
                    [
                        c.NameVi,
                        $"{c.Age} tuổi" + (c.IsAdult ? " (Nhược quán)" : " (Ấu thơ)"),
                        c.IsHeir ? "★ Đích Tử Kế Vị" : "Thứ tử",
                        c.MentorName ?? "Chưa có",
                        c.FocusStat ?? "—",
                        $"+{c.StatBonus} {c.FocusStat ?? ""}"
                    ],
                    rowCmds);
            }).ToList();

            sections.Add(new ScreenSection("Tử Tức & Giáo Dưỡng", childCols, childRows));
        }

        return new ScreenModel(
            ScreenId,
            Title,
            status,
            sections,
            commands,
            [
                new ScreenMetric("Chính thất", _service.SpouseName ?? "—", _service.IsMarried ? "đã thành hôn" : "chưa cưới",
                    _service.IsMarried ? MetricTone.Positive : MetricTone.Neutral),
                new ScreenMetric("Con", _service.ChildCount.ToString(System.Globalization.CultureInfo.InvariantCulture), "người"),
                new ScreenMetric("Kế vị", _service.HeirName ?? "—", _service.HeirName is null ? "chưa lập" : "đã định"),
                new ScreenMetric("Gia uy", $"{_service.ClanPrestige}", "điểm danh gia", MetricTone.Positive),
                new ScreenMetric("Từ đường", $"Cấp {_service.ShrineLevel}/5", "từ đường gia tộc"),
                new ScreenMetric("Phí tháng", _service.MonthlyUpkeep.ToString(System.Globalization.CultureInfo.InvariantCulture), "quan/tháng")
            ],
            alerts,
            !_service.IsMarried
                ? "Kết duyên lương phối và tu sửa từ đường để khai mở huyết mạch gia tộc và bồi dưỡng người kế vị."
                : "Bồi dưỡng con cái theo thầy giỏi và tu sửa từ đường gia tộc để gia tăng gia uy.");
    }
}

internal sealed class FamilyCommandHandler : IScreenCommandHandler
{
    private readonly LineageService _service;

    public FamilyCommandHandler(LineageService service) => _service = service;

    public string ScreenId => "lineage.family";

    public string? Execute(string commandId)
    {
        if (commandId.StartsWith("marry:"))
        {
            if (long.TryParse(commandId["marry:".Length..], out var npcId))
            {
                return _service.Marry(npcId).Message;
            }
        }

        if (commandId == "marry")
        {
            return _service.Marry().Message;
        }

        if (commandId == "beget")
        {
            return _service.Beget().Message;
        }

        if (commandId == "upgrade_shrine")
        {
            return _service.UpgradeShrine().Message;
        }

        if (commandId == "hold_offering")
        {
            return _service.HoldOffering().Message;
        }

        if (commandId.StartsWith("teach:"))
        {
            if (long.TryParse(commandId["teach:".Length..], out var cId))
            {
                return _service.TeachChild(cId).Message;
            }
        }

        if (commandId.StartsWith("tutor:"))
        {
            var parts = commandId["tutor:".Length..].Split(':');
            if (long.TryParse(parts[0], out var cId) && parts.Length > 1)
            {
                var stat = parts[1];
                var tutorName = stat == "WAR" ? "Võ Sư Danh Môn" : "Hàn Lâm Viện Nho Sĩ";
                return _service.AssignTutor(cId, tutorName, stat).Message;
            }
        }

        var separator = commandId.IndexOf(':');
        if (separator > 0 && commandId[..separator] == "heir" && long.TryParse(commandId[(separator + 1)..], out var childId))
        {
            return _service.DesignateHeir(childId).Message;
        }

        return $"Lệnh '{commandId}' không thuộc DLC lineage.";
    }
}
