using SimT.Contracts.Playing;
using SimT.Contracts.Ui;

namespace SimT.Modules.Base.Npc;

internal sealed class NpcDirectoryScreen : IScreenContribution
{
    private static readonly string[] Columns = ["Tên", "Tự / Hán", "Thân Phận", "Thành Trì", "Võ", "Soái", "Trí", "Thương"];

    private readonly INpcRegistry _registry;
    private readonly IWorldMap _map;
    private readonly IPlayerState _playerState;

    public NpcDirectoryScreen(INpcRegistry registry, IWorldMap map, IPlayerState playerState)
    {
        _registry = registry;
        _map = map;
        _playerState = playerState;
    }

    public string ScreenId => "npc.directory";

    public string Title => "Nhân Sĩ";

    public int Order => 15;

    public ScreenModel Render()
    {
        var currentCityId = _playerState.CurrentCityId;
        var currentCitadel = _map.Find(currentCityId);
        var currentCitadelName = currentCitadel?.NameVi ?? "Không rõ";

        var all = _registry.All;
        var localNpcs = _registry.FindByCitadel(currentCityId);
        var otherNpcs = all.Where(npc => npc.CitadelId != currentCityId).ToList();

        var sections = new List<ScreenSection>();

        if (localNpcs.Count > 0)
        {
            sections.Add(new ScreenSection(
                $"Nhân sĩ đồng thành · {currentCitadelName} ({localNpcs.Count})",
                Columns,
                localNpcs.Select(npc => ToRow(npc, currentCitadelName)).ToList()));
        }

        sections.Add(new ScreenSection(
            $"Quần anh thiên hạ ({otherNpcs.Count})",
            Columns,
            otherNpcs.Select(npc =>
            {
                var cit = _map.Find(npc.CitadelId);
                return ToRow(npc, cit?.NameVi ?? "Ẩn sĩ");
            }).ToList()));

        var metrics = new List<ScreenMetric>
        {
            new("Đồng thành", $"{localNpcs.Count} người", $"Tại {currentCitadelName}", MetricTone.Positive),
            new("Toàn thiên hạ", $"{all.Count} nhân sĩ", "15 châu quận", MetricTone.Neutral),
            new("Hổ tướng", $"{all.Count(n => n.Attributes.War >= 85)} vị", "Võ lực ≥ 85", MetricTone.Positive),
            new("Mưu thần", $"{all.Count(n => n.Attributes.Int >= 85)} vị", "Trí tuệ ≥ 85", MetricTone.Positive)
        };

        var alerts = new List<ScreenAlert>();
        if (localNpcs.Count == 0)
        {
            alerts.Add(new ScreenAlert($"Tại {currentCitadelName} hiện chưa có danh nhân Tam Quốc nào ngụ cư.", AlertLevel.Info));
        }

        var status = $"Đang tại {currentCitadelName} · Gặp gỡ {localNpcs.Count} nhân sĩ địa phương · Bấm 'Hồ Sơ 🔍' để xem chi tiết.";

        return new ScreenModel(
            ScreenId,
            Title,
            status,
            sections,
            Array.Empty<ScreenCommand>(),
            metrics,
            alerts);
    }

    private static ScreenRow ToRow(NpcDef npc, string citadelName)
    {
        var a = npc.Attributes;
        var nameHan = string.IsNullOrWhiteSpace(npc.NameHan) ? "" : $" ({npc.NameHan})";
        var style = string.IsNullOrWhiteSpace(npc.StyleNameVi) ? "—" : npc.StyleNameVi;

        return new ScreenRow(
            [
                npc.NameVi,
                $"{style}{nameHan}",
                npc.Role,
                citadelName,
                a.War.ToString(),
                a.Ldr.ToString(),
                a.Int.ToString(),
                a.Com.ToString()
            ],
            [
                new ScreenCommand($"npc:modal:{npc.Id}", "Hồ Sơ 🔍", CommandKind.Primary)
            ],
            Title: $"{npc.NameVi} — {style}",
            Details:
            [
                new ScreenDetail("Họ Tên", $"{npc.NameVi}{nameHan}"),
                new ScreenDetail("Tên Tự", style),
                new ScreenDetail("Thân Phận", npc.Role),
                new ScreenDetail("Ngụ Cư", citadelName),
                new ScreenDetail("Võ Lực (WAR)", a.War.ToString()),
                new ScreenDetail("Thống Soái (LDR)", a.Ldr.ToString()),
                new ScreenDetail("Trí Tuệ (INT)", a.Int.ToString()),
                new ScreenDetail("Chính Trị (POL)", a.Pol.ToString()),
                new ScreenDetail("Ngoại Giao (DIP)", a.Dip.ToString()),
                new ScreenDetail("Thương Mại (COM)", a.Com.ToString()),
                new ScreenDetail("Hậu Cần (LOG)", a.Log.ToString()),
                new ScreenDetail("Kỹ Nghệ (CRA)", a.Cra.ToString()),
                new ScreenDetail("Căn Cốt (CON)", a.Con.ToString()),
                new ScreenDetail("Ý Chí (WIL)", a.Wil.ToString()),
                new ScreenDetail("Tuệ Nhãn (PRC)", a.Prc.ToString()),
                new ScreenDetail("Khí Vận (LCK)", a.Lck.ToString()),
                new ScreenDetail("Tính Cách", npc.Personality ?? "Điềm đạm bình ổn"),
                new ScreenDetail("Tiểu Sử", npc.BioVi ?? "Hành trạng chưa rõ trong thư tịch.")
            ]);
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
