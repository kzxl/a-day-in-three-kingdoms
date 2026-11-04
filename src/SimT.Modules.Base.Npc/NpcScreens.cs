using SimT.Contracts.Modules;
using SimT.Contracts.Playing;
using SimT.Contracts.Ui;

namespace SimT.Modules.Base.Npc;

internal sealed class NpcDirectoryScreen : IScreenContribution
{
    private static readonly string[] Columns = ["Tên", "Tự / Hán", "Thân Phận", "Thành Trì", "Võ", "Soái", "Trí", "Thương"];

    private readonly INpcRegistry _registry;
    private readonly IWorldMap _map;
    private readonly IPlayerState _playerState;
    private readonly ICapabilityBroker? _capabilities;

    public NpcDirectoryScreen(INpcRegistry registry, IWorldMap map, IPlayerState playerState, ICapabilityBroker? capabilities = null)
    {
        _registry = registry;
        _map = map;
        _playerState = playerState;
        _capabilities = capabilities;
    }

    public string ScreenId => "npc.directory";

    public string Title => "Nhân Sĩ";

    public int Order => 15;

    public ScreenModel Render()
    {
        var currentCityId = _playerState.CurrentCityId;
        var currentCitadel = _map.Find(currentCityId);
        var currentCitadelName = currentCitadel?.NameVi ?? "Không rõ";

        var metNpcIds = new HashSet<long>();
        if (_capabilities is not null && _capabilities.TryGet<ISocialNetwork>(CapabilityIds.SocialNetwork, out var social) && social is not null)
        {
            foreach (var rel in social.AllRelationships())
            {
                metNpcIds.Add(rel.NpcId);
            }
        }

        var all = _registry.All;
        var localNpcs = _registry.FindByCitadel(currentCityId);
        var otherNpcs = all.Where(npc => npc.CitadelId != currentCityId).ToList();

        var distantMetNpcs = otherNpcs.Where(npc => metNpcIds.Contains(npc.Id)).ToList();
        var distantRenownedNpcs = otherNpcs
            .Where(npc => !metNpcIds.Contains(npc.Id) && IsRenowned(npc))
            .OrderByDescending(npc => Math.Max(npc.Attributes.War, Math.Max(npc.Attributes.Int, npc.Attributes.Ldr)))
            .ToList();

        var sections = new List<ScreenSection>();

        if (localNpcs.Count > 0)
        {
            sections.Add(new ScreenSection(
                $"Nhân sĩ đồng thành · {currentCitadelName} ({localNpcs.Count})",
                Columns,
                localNpcs.Select(npc => ToRow(npc, currentCitadelName, isLocal: true, hasMet: metNpcIds.Contains(npc.Id))).ToList()));
        }

        if (distantMetNpcs.Count > 0)
        {
            sections.Add(new ScreenSection(
                $"Tri kỷ & Bằng hữu phương xa ({distantMetNpcs.Count})",
                Columns,
                distantMetNpcs.Select(npc =>
                {
                    var cit = _map.Find(npc.CitadelId);
                    return ToRow(npc, cit?.NameVi ?? "Ẩn sĩ", isLocal: false, hasMet: true);
                }).ToList()));
        }

        sections.Add(new ScreenSection(
            $"Hào kiệt danh vang thiên hạ ({distantRenownedNpcs.Count})",
            Columns,
            distantRenownedNpcs.Select(npc =>
            {
                var cit = _map.Find(npc.CitadelId);
                return ToRow(npc, cit?.NameVi ?? "Ẩn sĩ", isLocal: false, hasMet: false);
            }).ToList()));

        var metCount = all.Count(n => metNpcIds.Contains(n.Id));
        var metrics = new List<ScreenMetric>
        {
            new("Đồng thành", $"{localNpcs.Count} người", $"Tại {currentCitadelName}", MetricTone.Positive),
            new("Đã kết giao", $"{metCount} nhân sĩ", "Đã gặp gỡ / tri kỷ", MetricTone.Positive),
            new("Danh vang thiên hạ", $"{distantRenownedNpcs.Count} vị", "Tiếng tăm lẫy lừng", MetricTone.Neutral),
            new("Tổng số ghi chép", $"{all.Count} nhân sĩ", "Toàn bờ cõi", MetricTone.Neutral)
        };

        var alerts = new List<ScreenAlert>();
        if (localNpcs.Count == 0)
        {
            alerts.Add(new ScreenAlert($"Tại {currentCitadelName} hiện chưa có danh nhân Tam Quốc nào ngụ cư.", AlertLevel.Info));
        }

        var status = $"Đang tại {currentCitadelName} · Gặp gỡ {localNpcs.Count} nhân sĩ địa phương · Bấm 'Hồ Sơ 🔍' để xem chi tiết & bái phỏng.";

        return new ScreenModel(
            ScreenId,
            Title,
            status,
            sections,
            Array.Empty<ScreenCommand>(),
            metrics,
            alerts);
    }

    private static bool IsRenowned(NpcDef npc)
    {
        var a = npc.Attributes;
        if (a.War >= 85 || a.Ldr >= 85 || a.Int >= 85 || a.Pol >= 85 || a.Com >= 85) return true;
        var r = npc.Role.ToLowerInvariant();
        return r.Contains("chúa") || r.Contains("vương") || r.Contains("quân sư") || r.Contains("đại tướng");
    }

    private static ScreenRow ToRow(NpcDef npc, string citadelName, bool isLocal, bool hasMet)
    {
        var a = npc.Attributes;
        var nameHan = string.IsNullOrWhiteSpace(npc.NameHan) ? "" : $" ({npc.NameHan})";
        var style = string.IsNullOrWhiteSpace(npc.StyleNameVi) ? "—" : npc.StyleNameVi;

        string warDisplay, ldrDisplay, intDisplay, comDisplay;
        string statusNote;

        if (hasMet)
        {
            warDisplay = a.War.ToString();
            ldrDisplay = a.Ldr.ToString();
            intDisplay = a.Int.ToString();
            comDisplay = a.Com.ToString();
            statusNote = isLocal ? "Đồng thành · Đã diện kiến" : $"Phương xa ({citadelName}) · Từng diện kiến";
        }
        else if (isLocal)
        {
            warDisplay = a.War >= 80 ? $"~{a.War / 5 * 5}" : "???";
            ldrDisplay = a.Ldr >= 80 ? $"~{a.Ldr / 5 * 5}" : "???";
            intDisplay = a.Int >= 80 ? $"~{a.Int / 5 * 5}" : "???";
            comDisplay = a.Com >= 80 ? $"~{a.Com / 5 * 5}" : "???";
            statusNote = "Đồng thành · Chưa diện kiến (Bấm 'Hồ Sơ 🔍' để bái phỏng)";
        }
        else
        {
            warDisplay = a.War >= 95 ? "95+ (Tuyệt Đỉnh)" : a.War >= 80 ? $"~{a.War / 5 * 5}" : "???";
            ldrDisplay = a.Ldr >= 90 ? "90+ (Danh Tướng)" : a.Ldr >= 80 ? $"~{a.Ldr / 5 * 5}" : "???";
            intDisplay = a.Int >= 90 ? "90+ (Kỳ Mưu)" : a.Int >= 80 ? $"~{a.Int / 5 * 5}" : "???";
            comDisplay = a.Com >= 80 ? $"~{a.Com / 5 * 5}" : "???";
            statusNote = $"Phương xa · Danh vang thiên hạ (Ngụ tại {citadelName})";
        }

        var details = new List<ScreenDetail>
        {
            new("Họ Tên", $"{npc.NameVi}{nameHan}"),
            new("Tên Tự", style),
            new("Thân Phận", npc.Role),
            new("Ngụ Cư", citadelName),
            new("Tình Trạng", statusNote)
        };

        if (hasMet)
        {
            details.Add(new("Đặc Kỹ", npc.Skills.Count > 0 ? string.Join(", ", npc.Skills.Select(s => $"[{s.NameVi}]")) : "Chưa khai mở"));
            details.Add(new("Võ Lực (WAR)", a.War.ToString()));
            details.Add(new("Thống Soái (LDR)", a.Ldr.ToString()));
            details.Add(new("Trí Tuệ (INT)", a.Int.ToString()));
            details.Add(new("Chính Trị (POL)", a.Pol.ToString()));
            details.Add(new("Ngoại Giao (DIP)", a.Dip.ToString()));
            details.Add(new("Thương Mại (COM)", a.Com.ToString()));
            details.Add(new("Hậu Cần (LOG)", a.Log.ToString()));
            details.Add(new("Kỹ Nghệ (CRA)", a.Cra.ToString()));
            details.Add(new("Căn Cốt (CON)", a.Con.ToString()));
            details.Add(new("Ý Chí (WIL)", a.Wil.ToString()));
            details.Add(new("Tuệ Nhãn (PRC)", a.Prc.ToString()));
            details.Add(new("Khí Vận (LCK)", a.Lck.ToString()));
            details.Add(new("Tính Cách", npc.Personality ?? "Điềm đạm bình ổn"));
            details.Add(new("Tiểu Sử", npc.BioVi ?? "Hành trạng chưa rõ trong thư tịch."));
        }
        else
        {
            details.Add(new("Đặc Kỹ", npc.Skills.Count > 0 ? $"{npc.Skills.Count} tuyệt kỹ (Chưa diện kiến để chứng thực)" : "Chưa rõ"));
            details.Add(new("Võ Lực (WAR)", warDisplay));
            details.Add(new("Thống Soái (LDR)", ldrDisplay));
            details.Add(new("Trí Tuệ (INT)", intDisplay));
            details.Add(new("Thương Mại (COM)", comDisplay));
            details.Add(new("Căn Cốt & Khí Vận", "🔒 Cơ mật bản thể (Cần gặp gỡ đàm đạo để thấu hiểu)"));
            details.Add(new("Hành Trạng", npc.BioVi ?? "Hành trạng lưu truyền trong dân gian."));
        }

        return new ScreenRow(
            [
                npc.NameVi,
                $"{style}{nameHan}",
                npc.Role,
                citadelName,
                warDisplay,
                ldrDisplay,
                intDisplay,
                comDisplay
            ],
            [
                new ScreenCommand($"npc:modal:{npc.Id}", "Hồ Sơ 🔍", CommandKind.Primary)
            ],
            Title: $"{npc.NameVi} — {style}",
            Details: details);
    }
}

internal sealed class NpcCommandHandler : IScreenCommandHandler
{
    private readonly INpcRegistry _registry;

    public NpcCommandHandler(INpcRegistry registry) => _registry = registry;

    public string ScreenId => "npc.directory";

    public string? Execute(string commandId)
    {
        if (commandId.StartsWith("npc:modal:"))
        {
            if (long.TryParse(commandId["npc:modal:".Length..], out var id))
            {
                var npc = _registry.Find(id);
                if (npc is not null)
                {
                    return $"Đang xem hồ sơ: {npc.NameVi} ({npc.StyleNameVi ?? npc.Role})";
                }
            }
            return "Không tìm thấy thông tin nhân sĩ.";
        }

        return null;
    }
}
