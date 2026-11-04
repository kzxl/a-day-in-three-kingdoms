using SimT.Contracts.Playing;
using SimT.Contracts.Time;
using SimT.Contracts.Ui;

namespace SimT.Modules.Dlc.Military;

internal sealed class MilitaryScreen : IScreenContribution
{
    private readonly IMilitaryCommand _military;
    private readonly INpcRegistry _registry;
    private readonly IWorldMap _map;
    private readonly IPlayerState _player;
    private readonly IPlayerAttributes _attributes;
    private readonly IBag _bag;
    private readonly IItemCatalog _items;
    private readonly IGameClock _clock;

    public MilitaryScreen(
        IMilitaryCommand military,
        INpcRegistry registry,
        IWorldMap map,
        IPlayerState player,
        IPlayerAttributes attributes,
        IBag bag,
        IItemCatalog items,
        IGameClock clock)
    {
        _military = military;
        _registry = registry;
        _map = map;
        _player = player;
        _attributes = attributes;
        _bag = bag;
        _items = items;
        _clock = clock;
    }

    public string ScreenId => "military.arena";

    public string Title => "Binh Nghiệp";

    public int Order => 18;

    public ScreenModel Render()
    {
        var currentCityId = _player.CurrentCityId;
        var currentCitadel = _map.Find(currentCityId);
        var currentCitadelName = currentCitadel?.NameVi ?? "Châu quận";

        var localWarriors = _registry.FindByCitadel(currentCityId)
            .OrderByDescending(n => n.Attributes.War)
            .ToList();

        var strongholds = _military.StrongholdsAt(currentCityId);
        var requisitions = _military.ActiveRequisitions(currentCityId);

        var sections = new List<ScreenSection>();

        // 1. Martial Arena Section
        var arenaColumns = new[] { "Võ Tướng / Hảo Thủ", "Thân Phận", "Binh Chủng", "Võ Lực", "Căn Cốt", "Định Lực", "Thách Đấu" };
        var arenaRows = localWarriors.Select(w =>
        {
            var a = w.Attributes;
            var branch = _military.ResolveNpcBranch(w.Id);
            return new ScreenRow(
                [
                    w.NameVi,
                    w.Role,
                    $"{TroopBranchHelper.Icon(branch)} {TroopBranchHelper.NameVi(branch)}",
                    a.War.ToString(),
                    a.Con.ToString(),
                    a.Wil.ToString(),
                    "Sẵn sàng giao đấu"
                ],
                [
                    new ScreenCommand($"duel:{w.Id}", "Tỷ Võ ⚔", CommandKind.Primary),
                    new ScreenCommand($"npc:modal:{w.Id}", "Hồ Sơ 🔍", CommandKind.Secondary)
                ],
                Title: $"{w.NameVi} — {w.Role}",
                Details:
                [
                    new ScreenDetail("Họ Tên", w.NameVi),
                    new ScreenDetail("Thân Phận", w.Role),
                    new ScreenDetail("Binh Chủng Sở Trường", $"{TroopBranchHelper.Icon(branch)} {TroopBranchHelper.NameVi(branch)}"),
                    new ScreenDetail("Tương Khắc Quân Cơ", branch switch
                    {
                        TroopBranch.Infantry => "Khắc Cung Binh (+25% sát thương), sợ Kỵ Binh (-15% phòng thủ)",
                        TroopBranch.Cavalry => "Khắc Bộ Binh (+25% sát thương), sợ Cung Binh (-15% phòng thủ)",
                        TroopBranch.Archers => "Khắc Kỵ Binh (+25% sát thương), sợ Bộ Binh (-15% phòng thủ)",
                        _ => ""
                    }),
                    new ScreenDetail("Võ Lực", a.War.ToString()),
                    new ScreenDetail("Căn Cốt", a.Con.ToString()),
                    new ScreenDetail("Định Lực", a.Wil.ToString()),
                    new ScreenDetail("Tiểu Sử", w.BioVi ?? "Dũng tướng Tam Quốc.")
                ]);
        }).ToList();

        sections.Add(new ScreenSection($"🥋 Lôi Đài Tỷ Võ · {currentCitadelName} ({localWarriors.Count} danh sĩ)", arenaColumns, arenaRows));

        // 2. Strongholds Section
        var shColumns = new[] { "Cứ Điểm Ngoại Thành", "Thế Lực Thổ Phỉ", "Binh Chủng Trấn Thủ", "Độ Nguy Hiểm", "Bạc Thu Hoạch", "Vật Phẩm", "Đã Phạt" };
        var shRows = strongholds.Select(sh =>
        {
            var item = _items.FindByCode(sh.ItemRewardCode);
            var itemName = item?.NameVi ?? sh.ItemRewardCode;
            var dangerStars = new string('★', Math.Min(5, sh.DangerLevel)) + $" (Cấp {sh.DangerLevel})";

            return new ScreenRow(
                [
                    sh.NameVi,
                    FactionVi(sh.BanditFaction),
                    $"{TroopBranchHelper.Icon(sh.Branch)} {TroopBranchHelper.NameVi(sh.Branch)}",
                    dangerStars,
                    $"{sh.LootGold} Quan",
                    $"2 x {itemName}",
                    $"{sh.ClearedCount} lần"
                ],
                [
                    new ScreenCommand($"assault:{sh.Id}", "Tiễu Trừ 🚩", CommandKind.Danger)
                ],
                Title: $"{sh.NameVi} ({FactionVi(sh.BanditFaction)})",
                Details:
                [
                    new ScreenDetail("Căn Cứ", sh.NameVi),
                    new ScreenDetail("Phe Phái", FactionVi(sh.BanditFaction)),
                    new ScreenDetail("Binh Chủng Sào Huyệt", $"{TroopBranchHelper.Icon(sh.Branch)} {TroopBranchHelper.NameVi(sh.Branch)}"),
                    new ScreenDetail("Cấp Độ Nguy Hiểm", $"Cấp {sh.DangerLevel}/10"),
                    new ScreenDetail("Chiến Lợi Phẩm Bạc", $"{sh.LootGold} Quan"),
                    new ScreenDetail("Vật Phẩm Thu Được", $"2 x {itemName}"),
                    new ScreenDetail("Số Lần Tiễu Phạt", $"{sh.ClearedCount} lần"),
                    new ScreenDetail("Lần Quét Cuối", sh.LastClearedDate.HasValue ? GameDateCodec.Format(sh.LastClearedDate.Value) : "Chưa từng")
                ]);
        }).ToList();

        sections.Add(new ScreenSection($"⛰️ Cứ Điểm Sơn Tặc Ngoại Thành ({strongholds.Count})", shColumns, shRows));

        // 3. Military Supply Requisitions
        if (requisitions.Count > 0)
        {
            var reqColumns = new[] { "Mục Tiêu Quân Lương", "Số Lượng Yêu Cầu", "Kho Hiện Có", "Đơn Giá Thưởng", "Hạn Định", "Bàn Giao" };
            var reqRows = requisitions.Select(r =>
            {
                var item = _items.FindByCode(r.TargetItemCode);
                var inStock = item is not null ? _bag.Quantity(currentCityId, item.Id) : 0;
                var canFulfill = inStock >= r.QuantityNeeded;

                return new ScreenRow(
                    [
                        r.TargetItemName,
                        $"{r.QuantityNeeded} đơn vị",
                        $"{inStock} đơn vị",
                        $"{r.RewardPerUnit} Quan/đv",
                        GameDateCodec.Format(r.DeadlineDate),
                        canFulfill ? "Đủ hàng bàn giao" : "Còn thiếu hàng"
                    ],
                    [
                        new ScreenCommand($"fulfill:{r.Id}", "Giao Lương 🌾", canFulfill ? CommandKind.Primary : CommandKind.Secondary)
                    ],
                    Title: $"Quân Cơ: {r.TargetItemName}",
                    Details:
                    [
                        new ScreenDetail("Quân Nhu", r.TargetItemName),
                        new ScreenDetail("Yêu Cầu", $"{r.QuantityNeeded} đơn vị"),
                        new ScreenDetail("Tồn Kho Địa Phương", $"{inStock} đơn vị"),
                        new ScreenDetail("Tổng Tiền Thưởng", $"{r.QuantityNeeded * r.RewardPerUnit} Quan"),
                        new ScreenDetail("Hạn Cuối", GameDateCodec.Format(r.DeadlineDate))
                    ]);
            }).ToList();

            sections.Add(new ScreenSection($"🌾 Lệnh Tiếp Tế Quân Lương Doanh ({requisitions.Count})", reqColumns, reqRows));
        }

        var pAttr = _attributes.Attributes;
        var totalCleared = strongholds.Sum(s => s.ClearedCount);

        var metrics = new List<ScreenMetric>
        {
            new("Võ Lực của bạn", $"{pAttr.War} điểm", $"Căn cốt {pAttr.Con} · Định lực {pAttr.Wil}", MetricTone.Positive),
            new("Sào huyệt ngoại thành", $"{strongholds.Count} cứ điểm", "Cần tiễu trừ an dân", MetricTone.Warning),
            new("Lệnh quân nhu", $"{requisitions.Count} quân lệnh", "Cung ứng quân lương", MetricTone.Neutral),
            new("Chiến tích tiễu phạt", $"{totalCleared} lần bình định", "Bảo vệ bá tánh", MetricTone.Positive)
        };

        var status = $"Khu vực {currentCitadelName} · Lôi đài mở cửa tỷ võ · Sơn tặc quanh vùng cần tiễu phạt.";

        return new ScreenModel(
            ScreenId,
            Title,
            status,
            sections,
            Array.Empty<ScreenCommand>(),
            metrics);
    }

    private static string FactionVi(string faction) => faction switch
    {
        "khan_vang" => "Khăn Vàng Tàn Quân",
        "hac_son" => "Hắc Sơn Khởi Nghĩa Quân",
        "bach_ba" => "Bạch Ba Tặc",
        _ => "Lộ Phỉ Man Tộc"
    };
}

internal sealed class MilitaryCommandHandler : IScreenCommandHandler
{
    private readonly IMilitaryCommand _military;

    public MilitaryCommandHandler(IMilitaryCommand military)
    {
        _military = military;
    }

    public string ScreenId => "military.arena";

    public string? Execute(string commandId)
    {
        if (commandId.StartsWith("duel:"))
        {
            var raw = commandId["duel:".Length..];
            var parts = raw.Split(':');
            if (long.TryParse(parts[0], out var npcId))
            {
                var branch = parts.Length > 1 && Enum.TryParse<TroopBranch>(parts[1], true, out var b) ? b : TroopBranch.Infantry;
                var duel = _military.ChallengeArena(npcId, branch);
                return duel.CombatSummaryVi;
            }
        }
        else if (commandId.StartsWith("assault:"))
        {
            if (long.TryParse(commandId["assault:".Length..], out var shId))
            {
                return _military.AssaultStronghold(shId);
            }
        }
        else if (commandId.StartsWith("fulfill:"))
        {
            if (long.TryParse(commandId["fulfill:".Length..], out var reqId))
            {
                return _military.FulfillRequisition(reqId);
            }
        }

        return null;
    }
}
