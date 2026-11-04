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

        var playerPrc = 50;
        if (_capabilities is not null && _capabilities.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var playerAttrs) && playerAttrs is not null)
        {
            playerPrc = playerAttrs.Attributes.Prc;
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
                localNpcs.Select(npc => ToRow(npc, currentCitadelName, isLocal: true, hasMet: metNpcIds.Contains(npc.Id), playerPrc)).ToList()));
        }

        if (distantMetNpcs.Count > 0)
        {
            sections.Add(new ScreenSection(
                $"Tri kỷ & Bằng hữu phương xa ({distantMetNpcs.Count})",
                Columns,
                distantMetNpcs.Select(npc =>
                {
                    var cit = _map.Find(npc.CitadelId);
                    return ToRow(npc, cit?.NameVi ?? "Ẩn sĩ", isLocal: false, hasMet: true, playerPrc);
                }).ToList()));
        }

        sections.Add(new ScreenSection(
            $"Hào kiệt danh vang thiên hạ ({distantRenownedNpcs.Count})",
            Columns,
            distantRenownedNpcs.Select(npc =>
            {
                var cit = _map.Find(npc.CitadelId);
                return ToRow(npc, cit?.NameVi ?? "Ẩn sĩ", isLocal: false, hasMet: false, playerPrc);
            }).ToList()));

        var metCount = all.Count(n => metNpcIds.Contains(n.Id));
        var prcTitle = playerPrc switch
        {
            >= 85 => "Như Đuốc (Thấu Triệt)",
            >= 70 => "Thông Tuệ (Thức Anh Hùng)",
            >= 50 => "Mẫn Tiệp (Quan Sát)",
            _ => "Sơ Khai (Đại Cương)"
        };
        var metrics = new List<ScreenMetric>
        {
            new("Tuệ Nhãn", $"{playerPrc} điểm", prcTitle, playerPrc >= 70 ? MetricTone.Positive : MetricTone.Neutral),
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

    private static ScreenRow ToRow(NpcDef npc, string citadelName, bool isLocal, bool hasMet, int playerPrc = 50)
    {
        var a = npc.Attributes;
        var nameHan = string.IsNullOrWhiteSpace(npc.NameHan) ? "" : $" ({npc.NameHan})";
        var style = string.IsNullOrWhiteSpace(npc.StyleNameVi) ? "—" : npc.StyleNameVi;

        // Tuệ Nhãn insight tiers
        var fullReveal = hasMet || (isLocal && playerPrc >= 85);
        var behaviorReveal = fullReveal || (isLocal && playerPrc >= 70);

        string warDisplay, ldrDisplay, intDisplay, comDisplay;
        string statusNote;

        if (fullReveal)
        {
            warDisplay = a.War.ToString();
            ldrDisplay = a.Ldr.ToString();
            intDisplay = a.Int.ToString();
            comDisplay = a.Com.ToString();
            statusNote = hasMet
                ? (isLocal ? "Đồng thành · Đã diện kiến" : $"Phương xa ({citadelName}) · Từng diện kiến")
                : "Đồng thành · Tuệ Nhãn như đuốc 👁️";
        }
        else if (behaviorReveal)
        {
            warDisplay = a.War.ToString();
            ldrDisplay = a.Ldr.ToString();
            intDisplay = a.Int.ToString();
            comDisplay = a.Com.ToString();
            statusNote = "Đồng thành · Tuệ Nhãn thông tuệ 👁️";
        }
        else if (isLocal)
        {
            if (playerPrc >= 50)
            {
                // Narrow estimation (±5 points) via perceptive observation
                warDisplay = a.War >= 80 ? $"~{a.War / 5 * 5}" : $"~{Math.Max(30, a.War / 5 * 5)}";
                ldrDisplay = a.Ldr >= 80 ? $"~{a.Ldr / 5 * 5}" : $"~{Math.Max(30, a.Ldr / 5 * 5)}";
                intDisplay = a.Int >= 80 ? $"~{a.Int / 5 * 5}" : $"~{Math.Max(30, a.Int / 5 * 5)}";
                comDisplay = a.Com >= 80 ? $"~{a.Com / 5 * 5}" : $"~{Math.Max(30, a.Com / 5 * 5)}";
                statusNote = "Đồng thành · Tuệ Nhãn mẫn tiệp (±5)";
            }
            else
            {
                // Broad estimation (±10 points)
                warDisplay = $"~{Math.Max(30, a.War / 10 * 10)}";
                ldrDisplay = $"~{Math.Max(30, a.Ldr / 10 * 10)}";
                intDisplay = $"~{Math.Max(30, a.Int / 10 * 10)}";
                comDisplay = $"~{Math.Max(30, a.Com / 10 * 10)}";
                statusNote = "Đồng thành · Tuệ Nhãn sơ khai (±10)";
            }
        }
        else
        {
            if (playerPrc >= 85)
            {
                warDisplay = a.War.ToString();
                ldrDisplay = a.Ldr.ToString();
                intDisplay = a.Int.ToString();
                comDisplay = a.Com.ToString();
                statusNote = $"Phương xa ({citadelName}) · Tuệ Nhãn viễn vọng 👁️";
            }
            else if (playerPrc >= 70)
            {
                warDisplay = $"~{a.War / 5 * 5}";
                ldrDisplay = $"~{a.Ldr / 5 * 5}";
                intDisplay = $"~{a.Int / 5 * 5}";
                comDisplay = $"~{a.Com / 5 * 5}";
                statusNote = $"Phương xa · Ngụ tại {citadelName} (±5)";
            }
            else
            {
                warDisplay = a.War >= 95 ? "95+ (Tuyệt Đỉnh)" : a.War >= 80 ? $"~{a.War / 5 * 5}" : a.War >= 65 ? "6x (Khá)" : "???";
                ldrDisplay = a.Ldr >= 90 ? "90+ (Danh Tướng)" : a.Ldr >= 80 ? $"~{a.Ldr / 5 * 5}" : a.Ldr >= 65 ? "6x (Khá)" : "???";
                intDisplay = a.Int >= 90 ? "90+ (Kỳ Mưu)" : a.Int >= 80 ? $"~{a.Int / 5 * 5}" : a.Int >= 65 ? "6x (Khá)" : "???";
                comDisplay = a.Com >= 80 ? $"~{a.Com / 5 * 5}" : a.Com >= 65 ? "6x (Khá)" : "???";
                statusNote = $"Phương xa · Ngụ tại {citadelName}";
            }
        }

        var details = new List<ScreenDetail>
        {
            new("Họ Tên", $"{npc.NameVi}{nameHan}"),
            new("Tên Tự", style),
            new("Thân Phận", npc.Role),
            new("Ngụ Cư", citadelName),
            new("Tình Trạng", statusNote)
        };

        if (fullReveal)
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
        else if (behaviorReveal)
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
            details.Add(new("Căn Cốt & Khí Vận", "🔒 Cơ mật bản thể (Tuệ Nhãn bậc 3: Cần đạt bậc 4 (PRC 85+) hoặc bái phỏng để giải mã)"));
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
            details.Add(new("Căn Cốt & Khí Vận", "🔒 Cơ mật bản thể (Gặp gỡ/Đồng hành để giải mã)"));
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
