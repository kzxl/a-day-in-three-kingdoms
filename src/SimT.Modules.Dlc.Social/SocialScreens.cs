using SimT.Contracts.Playing;
using SimT.Contracts.Time;
using SimT.Contracts.Ui;

namespace SimT.Modules.Dlc.Social;

internal sealed class SocialScreen : IScreenContribution
{
    private readonly ISocialNetwork _social;
    private readonly INpcRegistry _registry;
    private readonly IWorldMap _map;
    private readonly IPlayerState _player;
    private readonly IBag _bag;
    private readonly IItemCatalog _items;
    private readonly IGameClock _clock;

    public SocialScreen(
        ISocialNetwork social,
        INpcRegistry registry,
        IWorldMap map,
        IPlayerState player,
        IBag bag,
        IItemCatalog items,
        IGameClock clock)
    {
        _social = social;
        _registry = registry;
        _map = map;
        _player = player;
        _bag = bag;
        _items = items;
        _clock = clock;
    }

    public string ScreenId => "social.network";

    public string Title => "Nhân Duyên";

    public int Order => 17;

    public ScreenModel Render()
    {
        var currentCityId = _player.CurrentCityId;
        var currentCitadel = _map.Find(currentCityId);
        var currentCitadelName = currentCitadel?.NameVi ?? "Châu quận";

        var retainers = _social.GetRetainers();
        var allRelationships = _social.AllRelationships();
        var relMap = allRelationships.ToDictionary(r => r.NpcId);

        var localNpcs = _registry.FindByCitadel(currentCityId);

        var sections = new List<ScreenSection>();

        // 1. Retainers section
        if (retainers.Count > 0)
        {
            var retainerColumns = new[] { "Tên Danh Tướng/Mưu Sĩ", "Chức Vụ", "Bổng Lộc", "Trung Thành", "Ngày Đầu Quân" };
            var retainerRows = retainers.Select(ret =>
            {
                var npc = _registry.Find(ret.NpcId);
                var name = npc?.NameVi ?? $"Mã {ret.NpcId}";
                return new ScreenRow(
                    [
                        name,
                        SocialService.RoleTitleVi(ret.RoleAssigned),
                        $"{ret.MonthlyStipend} Quan/tháng",
                        $"{ret.Loyalty}/100",
                        GameDateCodec.Format(ret.RecruitedDate)
                    ],
                    [
                        new ScreenCommand($"npc:modal:{ret.NpcId}", "Hồ Sơ 🔍", CommandKind.Primary),
                        new ScreenCommand($"dismiss:{ret.NpcId}", "Giải Trừ ✖", CommandKind.Danger)
                    ],
                    Title: $"{name} — {SocialService.RoleTitleVi(ret.RoleAssigned)}",
                    Details:
                    [
                        new ScreenDetail("Họ Tên", name),
                        new ScreenDetail("Trách Nhiệm", SocialService.RoleTitleVi(ret.RoleAssigned)),
                        new ScreenDetail("Bổng Lộc", $"{ret.MonthlyStipend} Quan mỗi tháng"),
                        new ScreenDetail("Lòng Trung Thành", $"{ret.Loyalty}/100"),
                        new ScreenDetail("Bổ Nhiệm Ngày", GameDateCodec.Format(ret.RecruitedDate))
                    ]);
            }).ToList();

            sections.Add(new ScreenSection($"🛡️ Đội Ngũ Gia Thần & Môn Khách ({retainers.Count})", retainerColumns, retainerRows));
        }

        // 2. Local NPCs & Interaction Section
        var localColumns = new[] { "Nhân Sĩ", "Thân Phận", "Hảo Cảm", "Quan Hệ", "Quà Đã Tặng", "Tương Tác" };
        var localRows = localNpcs.Select(npc =>
        {
            relMap.TryGetValue(npc.Id, out var rel);
            var affinity = rel?.Affinity ?? 0;
            var impression = rel?.Impression ?? "Sơ giao";
            var giftCount = rel?.GiftCount ?? 0;

            var isRetainer = retainers.Any(r => r.NpcId == npc.Id);

            var commands = new List<ScreenCommand>
            {
                new($"chat:{npc.Id}", "Đàm Đạo 💬", CommandKind.Primary),
                new($"gift:{npc.Id}", "Biếu Quà 🎁", CommandKind.Secondary),
                new($"npc:modal:{npc.Id}", "Hồ Sơ 🔍", CommandKind.Secondary)
            };

            if (!isRetainer && affinity >= 35)
            {
                commands.Insert(2, new ScreenCommand($"recruit:{npc.Id}", "Chiêu Mộ 🤝", CommandKind.Primary));
            }

            var affinityDisplay = affinity >= 0 ? $"+{affinity}" : affinity.ToString();

            return new ScreenRow(
                [
                    npc.NameVi,
                    npc.Role,
                    $"{affinityDisplay}/100",
                    impression,
                    $"{giftCount} lần",
                    isRetainer ? "Đã đầu quân" : affinity >= 35 ? "Có thể chiêu mộ" : "Cần bồi dưỡng"
                ],
                commands,
                Title: $"{npc.NameVi} ({npc.Role})",
                Details:
                [
                    new ScreenDetail("Họ Tên", npc.NameVi),
                    new ScreenDetail("Thân Phận", npc.Role),
                    new ScreenDetail("Hảo Cảm", $"{affinity}/100"),
                    new ScreenDetail("Mức Độ Quan Hệ", impression),
                    new ScreenDetail("Số Lần Biếu Tặng", $"{giftCount} lần"),
                    new ScreenDetail("Trạng Thái Gia Thần", isRetainer ? "Đã quy phục" : "Chưa chiêu mộ")
                ]);
        }).ToList();

        sections.Add(new ScreenSection($"💬 Nhân Sĩ Hữu Duyên Cùng Thành · {currentCitadelName} ({localNpcs.Count})", localColumns, localRows));

        var metrics = new List<ScreenMetric>
        {
            new("Gia thần dưới trướng", $"{retainers.Count} vị", "Lĩnh bổng hàng tháng", MetricTone.Positive),
            new("Hảo hữu tâm giao", $"{allRelationships.Count(r => r.Affinity >= 20)} người", "Hảo cảm ≥ 20", MetricTone.Neutral),
            new("Sinh tử tri kỷ", $"{allRelationships.Count(r => r.Affinity >= 70)} vị", "Hảo cảm ≥ 70", MetricTone.Positive),
            new("Tại đồng thành", $"{localNpcs.Count} danh sĩ", currentCitadelName, MetricTone.Neutral)
        };

        var alerts = new List<ScreenAlert>();
        if (retainers.Count == 0)
        {
            alerts.Add(new ScreenAlert("Hiện tại chưa có gia thần phò tá. Hãy đàm đạo và tặng quà để chiêu mộ hiền tài!", AlertLevel.Info));
        }

        var status = $"Đang tại {currentCitadelName} · Gặp gỡ {localNpcs.Count} nhân sĩ · Đàm đạo tăng hảo cảm & điểm kỹ năng.";

        return new ScreenModel(
            ScreenId,
            Title,
            status,
            sections,
            Array.Empty<ScreenCommand>(),
            metrics,
            alerts);
    }
}

internal sealed class SocialCommandHandler : IScreenCommandHandler
{
    private readonly ISocialNetwork _social;
    private readonly IBag _bag;
    private readonly IPlayerState _player;
    private readonly IItemCatalog _items;

    public SocialCommandHandler(
        ISocialNetwork social,
        IBag bag,
        IPlayerState player,
        IItemCatalog items)
    {
        _social = social;
        _bag = bag;
        _player = player;
        _items = items;
    }

    public string ScreenId => "social.network";

    public string? Execute(string commandId)
    {
        if (commandId.StartsWith("chat:"))
        {
            if (long.TryParse(commandId["chat:".Length..], out var npcId))
            {
                return _social.ChatWithNpc(npcId);
            }
        }
        else if (commandId.StartsWith("gift:"))
        {
            var parts = commandId["gift:".Length..].Split(':');
            if (long.TryParse(parts[0], out var npcId))
            {
                if (parts.Length > 1 && long.TryParse(parts[1], out var explicitItemId))
                {
                    return _social.GiftNpc(npcId, explicitItemId);
                }

                // Auto-pick first available gift item in current city's bag
                var currentCity = _player.CurrentCityId;
                var bagRows = _bag.Rows(currentCity);
                if (bagRows.Count == 0)
                {
                    return "Trong hành trang tại thành này không có vật phẩm nào để tặng.";
                }

                var giftItem = bagRows.First();
                return _social.GiftNpc(npcId, giftItem.ItemId);
            }
        }
        else if (commandId.StartsWith("recruit:"))
        {
            if (long.TryParse(commandId["recruit:".Length..], out var npcId))
            {
                return _social.RecruitRetainer(npcId, role: "auto", stipend: 50);
            }
        }
        else if (commandId.StartsWith("dismiss:"))
        {
            if (long.TryParse(commandId["dismiss:".Length..], out var npcId))
            {
                return _social.DismissRetainer(npcId);
            }
        }

        return null;
    }
}
