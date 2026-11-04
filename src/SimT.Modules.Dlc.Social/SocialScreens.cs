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

        // 1. Letters / Missives Section
        var letters = _social.GetLetters(limit: 15);
        var unreadLetters = _social.UnreadLetterCount();
        if (letters.Count > 0)
        {
            var letterColumns = new[] { "Ngày Gửi", "Người Gửi", "Địa Điểm", "Tiêu Đề", "Lễ Vật Đính Kèm", "Trạng Thái" };
            var letterRows = letters.Select(ltr =>
            {
                var senderCit = _map.Find(ltr.SenderCitadelId);
                var citName = senderCit?.NameVi ?? "Phương xa";
                var giftDisplay = ltr.GiftCopper > 0 && ltr.GiftItemQuantity > 0
                    ? $"🎁 {ltr.GiftCopper}Q + {ltr.GiftItemQuantity} {ltr.GiftItemNameVi}"
                    : ltr.GiftCopper > 0
                        ? $"🎁 {ltr.GiftCopper} Quan"
                        : ltr.GiftItemQuantity > 0
                            ? $"🎁 {ltr.GiftItemQuantity} {ltr.GiftItemNameVi}"
                            : "—";

                var statusDisplay = !ltr.IsRead
                    ? "🔴 Chưa đọc"
                    : ltr.IsClaimed
                        ? "✓ Đã nhận"
                        : (ltr.GiftCopper > 0 || ltr.GiftItemQuantity > 0)
                            ? "🟡 Chưa nhận quà"
                            : "Đã xem";

                var cmds = new List<ScreenCommand>
                {
                    new($"letter:read:{ltr.Id}", "Đọc Thư 📜", CommandKind.Primary)
                };

                if (!ltr.IsClaimed && (ltr.GiftCopper > 0 || ltr.GiftItemQuantity > 0))
                {
                    cmds.Add(new($"letter:claim:{ltr.Id}", "Nhận Quà 🎁", CommandKind.Primary));
                }

                cmds.Add(new($"npc:modal:{ltr.SenderNpcId}", "Hồ Sơ 🔍", CommandKind.Secondary));

                return new ScreenRow(
                    [
                        GameDateCodec.Format(ltr.SentDate),
                        ltr.SenderNameVi,
                        citName,
                        ltr.TitleVi,
                        giftDisplay,
                        statusDisplay
                    ],
                    cmds,
                    Title: $"{ltr.TitleVi} — {ltr.SenderNameVi}",
                    Details:
                    [
                        new ScreenDetail("Người Gửi", $"{ltr.SenderNameVi} (Tại {citName})"),
                        new ScreenDetail("Ngày Gửi", GameDateCodec.Format(ltr.SentDate)),
                        new ScreenDetail("Tiêu Đề", ltr.TitleVi),
                        new ScreenDetail("Lễ Vật", giftDisplay),
                        new ScreenDetail("Trạng Thái", statusDisplay),
                        new ScreenDetail("Nội Dung Thư", ltr.ContentVi)
                    ]);
            }).ToList();

            var unreadTag = unreadLetters > 0 ? $" ({unreadLetters} thư mới 🔴)" : "";
            sections.Add(new ScreenSection($"✉️ Hòm Thư Tri Kỷ & Bằng Hữu{unreadTag}", letterColumns, letterRows));
        }

        // 2. Sworn Brothers Section
        var swornBrothers = _social.GetSwornBrothers();
        if (swornBrothers.Count > 0)
        {
            var swornColumns = new[] { "Nghĩa Huynh Đệ", "Danh Phận", "Ngày Thề Ước", "Tương Trợ", "Hồ Sơ" };
            var swornRows = swornBrothers.Select(sw =>
            {
                var npc = _registry.Find(sw.NpcId);
                var name = npc?.NameVi ?? $"Mã {sw.NpcId}";
                return new ScreenRow(
                    [
                        name,
                        sw.BondTitle,
                        GameDateCodec.Format(sw.OathDate),
                        $"{sw.AssistCount} trận",
                        "Kim Lan Tri Kỷ"
                    ],
                    [
                        new ScreenCommand($"npc:modal:{sw.NpcId}", "Hồ Sơ 🔍", CommandKind.Primary)
                    ],
                    Title: $"{name} ({sw.BondTitle})",
                    Details:
                    [
                        new ScreenDetail("Họ Tên", name),
                        new ScreenDetail("Danh Phận", sw.BondTitle),
                        new ScreenDetail("Ngày Cắt Máu Ăn Thề", GameDateCodec.Format(sw.OathDate)),
                        new ScreenDetail("Số Trận Trợ Chiến", $"{sw.AssistCount} lần"),
                        new ScreenDetail("Tình Nghĩa", "Sinh tử chi giao, hoạn nạn có nhau")
                    ]);
            }).ToList();

            sections.Add(new ScreenSection($"🌸 Kim Lan Huynh Đệ · Đào Viên Thề Ước ({swornBrothers.Count}/2)", swornColumns, swornRows));
        }

        // 2. Retainers section
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
        var localColumns = new[] { "Nhân Sĩ", "Phẩm Giai", "Thân Phận", "Đặc Kỹ", "Hảo Cảm", "Quan Hệ", "Chiêu Mộ / Tình Trạng" };
        var localRows = localNpcs.Select(npc =>
        {
            relMap.TryGetValue(npc.Id, out var rel);
            var affinity = rel?.Affinity ?? 0;
            var impression = rel?.Impression ?? "Sơ giao";
            var giftCount = rel?.GiftCount ?? 0;

            var req = _social.GetRecruitRequirement(npc.Id);
            var rankBadge = req.Rank switch
            {
                NpcRank.S => "⭐ Phẩm S",
                NpcRank.A => "🔷 Phẩm A",
                NpcRank.B => "🔶 Phẩm B",
                _ => "⚪ Phẩm C"
            };

            var isRetainer = retainers.Any(r => r.NpcId == npc.Id);
            var skillDisplay = npc.Skills.Count > 0 ? string.Join(", ", npc.Skills.Select(s => s.NameVi)) : "—";

            var commands = new List<ScreenCommand>
            {
                new($"chat:{npc.Id}", "Đàm Đạo 💬", CommandKind.Primary),
                new($"gift:{npc.Id}", "Biếu Quà 🎁", CommandKind.Secondary),
                new($"npc:modal:{npc.Id}", "Hồ Sơ 🔍", CommandKind.Secondary)
            };

            var unlearnedSkill = npc.Skills.FirstOrDefault();
            var reqSkillAff = unlearnedSkill?.Tier >= 5 ? 75 : unlearnedSkill?.Tier == 4 ? 55 : 35;
            if (unlearnedSkill is not null && affinity >= reqSkillAff)
            {
                commands.Insert(1, new ScreenCommand($"learn:{npc.Id}:{unlearnedSkill.Code}", $"Thỉnh Giáo 📜 [{unlearnedSkill.NameVi}]", CommandKind.Primary));
            }

            if (!swornBrothers.Any(s => s.NpcId == npc.Id) && swornBrothers.Count < 2 && affinity >= 80)
            {
                commands.Insert(1, new ScreenCommand($"swear:{npc.Id}", "Kết Nghĩa 🤝", CommandKind.Primary));
            }

            if (!isRetainer && !req.IsWarlord && req.CanRecruit)
            {
                commands.Insert(commands.Count - 1, new ScreenCommand($"recruit:{npc.Id}", $"Chiêu Mộ 🤝 ({req.SigningFee}Q)", CommandKind.Primary));
            }

            var affinityDisplay = affinity >= 0 ? $"+{affinity}" : affinity.ToString();
            var recruitStatus = isRetainer
                ? "Đã đầu quân"
                : req.IsWarlord
                    ? "Chư hầu cát cứ"
                    : req.CanRecruit
                        ? $"Có thể chiêu mộ ({req.SigningFee}Q)"
                        : req.StatusSummary;

            return new ScreenRow(
                [
                    npc.NameVi,
                    rankBadge,
                    npc.Role,
                    skillDisplay,
                    $"{affinityDisplay}/100",
                    impression,
                    recruitStatus
                ],
                commands,
                Title: $"{npc.NameVi} ({rankBadge} · {npc.Role})",
                Details:
                [
                    new ScreenDetail("Họ Tên", npc.NameVi),
                    new ScreenDetail("Phẩm Giai", $"{rankBadge} · Sính lễ: {req.SigningFee:N0} Quan, Bổng lộc tối thiểu: {req.MinMonthlyStipend:N0} Quan/tháng"),
                    new ScreenDetail("Thân Phận", npc.Role),
                    new ScreenDetail("Đặc Kỹ", npc.Skills.Count > 0 ? string.Join(", ", npc.Skills.Select(s => $"[{s.NameVi}] {s.EffectSummary}")) : "Chưa khai mở"),
                    new ScreenDetail("Hảo Cảm", $"{affinity}/100"),
                    new ScreenDetail("Mức Độ Quan Hệ", impression),
                    new ScreenDetail("Số Lần Biếu Tặng", $"{giftCount} lần"),
                    new ScreenDetail("Tình Trạng Chiêu Mộ", req.StatusSummary)
                ]);
        }).ToList();

        sections.Add(new ScreenSection($"💬 Nhân Sĩ Hữu Duyên Cùng Thành · {currentCitadelName} ({localNpcs.Count})", localColumns, localRows));

        var metrics = new List<ScreenMetric>
        {
            new("Thư tín tri kỷ", $"{unreadLetters} thư mới", $"Tổng {letters.Count} thư trong hòm", unreadLetters > 0 ? MetricTone.Positive : MetricTone.Neutral),
            new("Nghĩa huynh đệ", $"{swornBrothers.Count}/2 vị", "Đào Viên thề ước", MetricTone.Positive),
            new("Gia thần dưới trướng", $"{retainers.Count} vị", "Lĩnh bổng hàng tháng", MetricTone.Positive),
            new("Hảo hữu tâm giao", $"{allRelationships.Count(r => r.Affinity >= 20)} người", "Hảo cảm ≥ 20", MetricTone.Neutral),
            new("Tại đồng thành", $"{localNpcs.Count} danh sĩ", currentCitadelName, MetricTone.Neutral)
        };

        var alerts = new List<ScreenAlert>();
        if (retainers.Count == 0)
        {
            alerts.Add(new ScreenAlert("Hiện tại chưa có gia thần phò tá. Hãy đàm đạo và tặng quà để chiêu mộ hiền tài!", AlertLevel.Info));
        }

        var screenActions = new List<ScreenCommand>
        {
            new("host_banquet", "Mở Đại Yến Quần Hiền 🍶 (150 Quan)", CommandKind.Primary)
        };

        var status = $"Đang tại {currentCitadelName} · Gặp gỡ {localNpcs.Count} nhân sĩ · Đàm đạo tăng hảo cảm & điểm kỹ năng.";

        return new ScreenModel(
            ScreenId,
            Title,
            status,
            sections,
            screenActions,
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
        else if (commandId.StartsWith("learn:"))
        {
            var parts = commandId["learn:".Length..].Split(':');
            if (long.TryParse(parts[0], out var npcId) && parts.Length > 1)
            {
                var skillCode = parts[1];
                return _social.LearnSkillFromNpc(npcId, skillCode);
            }
        }
        else if (commandId.StartsWith("swear:"))
        {
            if (long.TryParse(commandId["swear:".Length..], out var npcId))
            {
                return _social.SwearBrotherhood(npcId);
            }
        }
        else if (commandId == "host_banquet")
        {
            return _social.HostBanquet();
        }
        else if (commandId.StartsWith("letter:read:"))
        {
            if (long.TryParse(commandId["letter:read:".Length..], out var letterId))
            {
                return _social.ReadLetter(letterId);
            }
        }
        else if (commandId.StartsWith("letter:claim:"))
        {
            if (long.TryParse(commandId["letter:claim:".Length..], out var letterId))
            {
                return _social.ClaimLetterGift(letterId);
            }
        }

        return null;
    }
}
