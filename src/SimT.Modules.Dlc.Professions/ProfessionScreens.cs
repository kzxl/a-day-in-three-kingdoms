using SimT.Contracts.Playing;
using SimT.Contracts.Ui;

namespace SimT.Modules.Dlc.Professions;

internal sealed class ProfessionScreen : IScreenContribution
{
    private static readonly string[] SlotColumns =
        ["Vị Trí", "Khí Cụ", "Thuộc Tính", "Độ Bền", "Phẩm Cấp"];

    private static readonly string[] EquipmentColumns =
        ["Khí Cụ", "Thuộc Tính", "Độ Bền", "Phẩm Cấp"];

    private static readonly string[] OrderColumns =
        ["Bên Ủy Thác", "Loại", "Vật Phẩm Cần", "Số Lượng", "Thù Lao", "Danh Vọng", "Hạn Chót", "Trạng Thái"];

    private static readonly string[] RecipeColumns =
        ["Bản Vẽ", "Yêu Cầu", "Nguyên Liệu", "Phí Đúc", "Thời Gian", "Thành Phẩm"];

    private static readonly string[] Columns =
        ["Nghề", "Phân Ngành", "Danh Hiệu", "Ngày/lượt", "Sản phẩm", "Sản lượng", "Dụng cụ", "Tay nghề"];

    private static readonly string[] PerkColumns =
        ["Bí Kỹ Nghề", "Yêu Cầu Danh Vọng", "Hiệu Quả Bí Truyền", "Trạng Thái"];

    private readonly ProfessionService _service;
    private readonly IItemCatalog _items;
    private readonly IPlayerState _player;

    public ProfessionScreen(ProfessionService service, IItemCatalog items, IPlayerState player)
    {
        _service = service;
        _items = items;
        _player = player;
    }

    public string ScreenId => "professions.trades";

    public string Title => "Bách Nghệ";

    public int Order => 35;

    public ScreenModel Render()
    {
        var cityId = _player.CurrentCityId;

        // 1. Equipped slots
        var slotRows = new List<ScreenRow>();
        foreach (var slot in _service.Slots)
        {
            var equip = slot.Equipment;
            var commands = equip is not null
                ? new List<ScreenCommand> { new ScreenCommand($"unequip:{slot.SlotCode}", "Tháo ra") }
                : new List<ScreenCommand>();

            slotRows.Add(new ScreenRow(
                [
                    slot.SlotNameVi,
                    equip?.NameVi ?? "(Để trống)",
                    equip != null ? $"+{equip.BonusValue} {equip.BonusStat}" : "—",
                    equip != null ? $"{equip.Durability}/{equip.MaxDurability}" : "—",
                    equip?.QualityRank ?? "—"
                ],
                commands,
                Title: slot.SlotNameVi));
        }

        // 2. Equipments in citadel
        var equipRows = new List<ScreenRow>();
        var equipments = _service.Equipments(cityId);
        foreach (var eq in equipments)
        {
            var isEquipped = _service.Slots.Any(s => s.EquipmentId == eq.Id);
            var commands = new List<ScreenCommand>();

            if (!isEquipped)
            {
                commands.Add(new ScreenCommand($"equip:{eq.Id}", "Mặc"));
            }

            if (eq.Durability < eq.MaxDurability)
            {
                commands.Add(new ScreenCommand($"repair:{eq.Id}", "Tu sửa (15 Quan)"));
            }

            if (eq.TemperLevel < 5)
            {
                commands.Add(new ScreenCommand($"temper:{eq.Id}", $"Tôi luyện (+{eq.TemperLevel + 1})"));
            }

            equipRows.Add(new ScreenRow(
                [
                    $"{eq.NameVi} [+{eq.TemperLevel}]" + (isEquipped ? " [Đang mặc]" : string.Empty),
                    $"+{eq.BonusValue} {eq.BonusStat}",
                    $"{eq.Durability}/{eq.MaxDurability}",
                    eq.QualityRank
                ],
                commands));
        }

        // 3. Workshop in citadel
        var workshop = _service.GetWorkshop(cityId);
        var workshopRows = new List<ScreenRow>();
        if (workshop is not null)
        {
            var trade = _service.Trades.FirstOrDefault(t => t.Id == workshop.TradeId);
            var commands = new List<ScreenCommand>();

            if (workshop.HiredArtisans < 5)
            {
                commands.Add(new ScreenCommand("workshop:hire:0", "Tuyển thợ phụ (20 Quan)"));
            }
            if (workshop.HiredArtisans > 0)
            {
                commands.Add(new ScreenCommand("workshop:dismiss:0", "Bớt thợ phụ"));
            }
            if (workshop.FurnaceLevel < 5)
            {
                commands.Add(new ScreenCommand("workshop:upgrade:0", $"Nâng lò cấp {workshop.FurnaceLevel + 1} ({workshop.FurnaceLevel * 80} Quan)"));
            }

            workshopRows.Add(new ScreenRow(
                [
                    workshop.NameVi,
                    trade?.NameVi ?? "Chế tác",
                    $"Cấp {workshop.FurnaceLevel}/5",
                    $"{workshop.HiredArtisans}/5 thợ (Lương {workshop.HiredArtisans * ProfessionService.ArtisanMonthlySalary} Quan/tháng)",
                    $"{workshop.MonthlyRent} Quan/tháng",
                    workshop.IsActive ? "Đang mở cửa" : "Đình chỉ"
                ],
                commands,
                Title: workshop.NameVi,
                Details:
                [
                    new ScreenDetail("Lò luyện", $"Cấp {workshop.FurnaceLevel} (+{workshop.FurnaceLevel * 5}% tỷ lệ Tuyệt phẩm)"),
                    new ScreenDetail("Thợ phụ", $"{workshop.HiredArtisans} người tự động gia công hàng tháng"),
                    new ScreenDetail("Tiền thuê", $"{workshop.MonthlyRent} Quan khấu trừ ngày đầu tháng")
                ]));
        }
        else
        {
            // Option to open workshop for each trade
            foreach (var trade in _service.Trades)
            {
                workshopRows.Add(new ScreenRow(
                    [
                        $"Mở Xưởng {trade.NameVi}",
                        trade.NameVi,
                        "Cấp 1",
                        "0 thợ",
                        $"{ProfessionService.WorkshopBaseRent} Quan/tháng",
                        "Chưa thuê"
                    ],
                    [new ScreenCommand($"workshop:open:{trade.Id}", $"Thuê mặt bằng ({ProfessionService.WorkshopOpenCost} Quan)")]));
            }
        }

        // 4. Guild Orders & Commissions
        var orderRows = new List<ScreenRow>();
        var orders = _service.AvailableOrders(cityId).Concat(_service.ActiveOrders()).DistinctBy(o => o.Id);
        foreach (var ord in orders)
        {
            var targetItem = _items.Find(ord.TargetItemId);
            var commands = new List<ScreenCommand>();

            if (ord.Status == "AVAILABLE")
            {
                commands.Add(new ScreenCommand($"order:accept:{ord.Id}", "Nhận đơn"));
            }
            else if (ord.Status == "ACCEPTED")
            {
                commands.Add(new ScreenCommand($"order:fulfill:{ord.Id}", "Giao nộp"));
            }

            var typeVi = ord.ClientType switch
            {
                "MILITARY" => "Quân Nhu",
                "COMMERCE" => "Thương Hội",
                "COURT" => "Triều Đình",
                _ => "Gia Tộc"
            };

            var statusVi = ord.Status switch
            {
                "AVAILABLE" => "Chờ nhận",
                "ACCEPTED" => "Đang thực hiện",
                "COMPLETED" => "Hoàn thành",
                _ => "Hết hạn"
            };

            orderRows.Add(new ScreenRow(
                [
                    ord.ClientNameVi,
                    typeVi,
                    targetItem?.NameVi ?? $"Vật phẩm #{ord.TargetItemId}",
                    Fmt(ord.QuantityNeeded),
                    $"{ord.RewardCopper} Quan",
                    $"+{ord.FameReward}",
                    $"Ngày {ord.DeadlineDay}",
                    statusVi
                ],
                commands));
        }

        // 5. Crafting recipes
        var recipeRows = new List<ScreenRow>();
        foreach (var r in _service.Recipes)
        {
            var mat = _items.Find(r.MaterialItemId)?.NameVi ?? $"Vật phẩm #{r.MaterialItemId}";
            var output = _items.Find(r.OutputItemId)?.NameVi ?? r.NameVi;
            var currentTier = _service.Tier(r.TradeId);
            var canCraft = currentTier >= r.RequiredTier;

            var commands = canCraft
                ? new List<ScreenCommand> { new ScreenCommand($"craft:{r.Id}", "Rèn đúc") }
                : new List<ScreenCommand>();

            recipeRows.Add(new ScreenRow(
                [
                    r.NameVi,
                    $"Cần bậc {r.RequiredTier} (hiện {currentTier})",
                    $"{r.MaterialQuantity}x {mat}",
                    $"{r.CraftFee} Quan",
                    $"{r.CraftDays} ngày",
                    $"{r.OutputQuantity}x {output}"
                ],
                commands));
        }

        // 6. Mastery Perks
        var perkRows = new List<ScreenRow>();
        foreach (var p in _service.Perks)
        {
            var canUnlock = !p.IsUnlocked && _service.TotalGuildFame >= p.RequiredFame;
            var commands = canUnlock
                ? new List<ScreenCommand> { new ScreenCommand($"perk:unlock:{p.Id}", "Giác ngộ") }
                : new List<ScreenCommand>();

            perkRows.Add(new ScreenRow(
                [
                    p.NameVi,
                    $"{p.RequiredFame} Danh vọng",
                    p.DescriptionVi,
                    p.IsUnlocked ? "★ Đã giác ngộ" : (canUnlock ? "Có thể lãnh hội" : "Chưa đủ danh vọng")
                ],
                commands));
        }

        // 7. Basic trades
        var rows = new List<ScreenRow>(_service.Trades.Count);
        foreach (var trade in _service.Trades)
        {
            var output = _items.Find(trade.OutputItemId);
            var tier = _service.Tier(trade.Id);
            var rank = _service.GetCraftsmanRank(trade.Id);
            var sectorVi = trade.Sector switch
            {
                "AGRI" => "🌾 Nông Lâm",
                "ALCH" => "🍶 Dược Thực",
                "TEXTILE" => "🧵 Dệt May",
                _ => "🔨 Công Xảo"
            };

            var perkBonus = _service.HasPerk("xao_doat_thien_cong") ? 1 : 0;
            var finalYield = trade.BaseQuantity + tier + perkBonus;

            rows.Add(new ScreenRow(
                [
                    $"{trade.NameVi} ({trade.NameEn})",
                    sectorVi,
                    rank,
                    Fmt(trade.DaysPerBatch),
                    output?.NameVi ?? trade.OutputItemId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Fmt(finalYield),
                    Fmt(trade.ToolFee),
                    $"bậc {tier} · {_service.PracticeCount(trade.Id)} lượt"
                ],
                [new ScreenCommand($"work:{trade.Id}", "Làm 1 lượt")],
                Title: trade.NameVi,
                Details:
                [
                    new ScreenDetail("Phân ngành", sectorVi),
                    new ScreenDetail("Danh vị", rank),
                    new ScreenDetail("Mỗi lượt", $"{trade.DaysPerBatch} ngày · {trade.ToolFee} quan dụng cụ"),
                    new ScreenDetail("Sản phẩm", output?.NameVi ?? "—"),
                    new ScreenDetail("Sản lượng", $"{finalYield} mỗi lượt"),
                    new ScreenDetail("Tay nghề", $"bậc {tier}/5 · {_service.PracticeCount(trade.Id)} lượt")
                ]));
        }

        // 8. Culinary Kitchen (Dishes)
        var dishRows = new List<ScreenRow>();
        foreach (var d in _service.Dishes)
        {
            var pItem = _items.Find(d.PrimaryItemId)?.NameVi ?? $"Vật phẩm #{d.PrimaryItemId}";
            var matText = $"{d.PrimaryQuantity}x {pItem}";
            if (d.SecondaryItemId.HasValue && d.SecondaryQuantity > 0)
            {
                var sItem = _items.Find(d.SecondaryItemId.Value)?.NameVi ?? $"Phụ #{d.SecondaryItemId}";
                matText += $", {d.SecondaryQuantity}x {sItem}";
            }

            var commands = new List<ScreenCommand> { new ScreenCommand($"dish:cook:{d.Id}", "Nấu") };
            if (d.InInventory > 0)
            {
                commands.Add(new ScreenCommand($"dish:eat:{d.Id}", "Dùng bữa"));
            }

            dishRows.Add(new ScreenRow(
                [
                    d.NameVi,
                    matText,
                    $"{d.CookFee} Quan",
                    $"-{d.FatigueRelief} Mệt, -{d.StressRelief} Stress, +{d.HealthGain} HP",
                    $"{d.InInventory} phần"
                ],
                commands,
                Title: d.NameVi,
                Details: [new ScreenDetail("Thuyết minh", d.DescriptionVi)]));
        }

        // 9. Wilderness Activities
        var wildRows = new List<ScreenRow>
        {
            new(
                [
                    "Buông Cần Câu Cá",
                    "Bến sông / Vùng nước",
                    "1 ngày · 10 Mệt mỏi",
                    "2~3 Cá khô (Cơ duyên vớt túi tiền)",
                    "Giảm 15 Căng thẳng, +WIL/PRC"
                ],
                [new ScreenCommand("wild:fish", "Buông cần câu")]),
            new(
                [
                    "Lên Núi Tầm Dược",
                    "Rừng sâu / Núi cao",
                    "1 ngày · 15 Mệt mỏi",
                    "2~3 Thảo dược (Cơ duyên hái Nhân sâm)",
                    "Mở mang nhận thức, +PRC/INT"
                ],
                [new ScreenCommand("wild:forage", "Lên núi hái thuốc")]),
            new(
                [
                    "Săn Bắn Rừng Sâu",
                    "Thảo nguyên / Rừng già",
                    "1 ngày · 20 Mệt mỏi",
                    "2~3 Thịt thú (Cơ duyên da lông/Mật gấu)",
                    "Tăng cường thể phách, +WAR/PRC/CON"
                ],
                [new ScreenCommand("wild:hunt", "Giương cung săn bắn")])
        };

        var totalPractice = _service.Trades.Sum(trade => _service.PracticeCount(trade.Id));
        var bestTier = _service.Trades.Max(trade => _service.Tier(trade.Id));

        return new ScreenModel(
            ScreenId,
            Title,
            "Quản lý xưởng chế tác tự động, trang bị binh khí hộ giáp lên nhân vật, và hoàn thành các đơn đặt hàng quân nhu thương hội.",
            [
                new ScreenSection("Trang Bị Cá Nhân & Chỉ Số Cộng", SlotColumns, slotRows),
                new ScreenSection("Xưởng Chế Tác Tại Thành", ["Tên Xưởng", "Chuyên Môn", "Lò Luyện", "Thợ Phụ", "Tiền Thuê", "Trạng Thái"], workshopRows),
                new ScreenSection("Ủy Thác & Đơn Đặt Hàng Phường Hội", OrderColumns, orderRows),
                new ScreenSection("Bếp Nấu Ẩm Thực (Mỹ Vị Tam Quốc)", ["Món Ăn", "Nguyên Liệu", "Chi Phí", "Hiệu Quả Sinh Mệnh", "Có Sẵn"], dishRows),
                new ScreenSection("Dã Ngoại Sinh Hoạt (Câu Cá, Hái Thuốc & Săn Bắn)", ["Hoạt Động", "Địa Điểm", "Tiêu Hao", "Thu Hoạch Dự Kiến", "Lợi Ích Thần Trí"], wildRows),
                new ScreenSection("Bí Kỹ Nghề Nghiệp & Giác Ngộ", PerkColumns, perkRows),
                new ScreenSection("Khí Cụ Trong Thành", EquipmentColumns, equipRows),
                new ScreenSection("Bản Vẽ Chế Tác Khí Cụ", RecipeColumns, recipeRows),
                new ScreenSection("Bách Nghệ Lao Tác (16 Ngành Nghề)", Columns, rows)
            ],
            [],
            [
                new ScreenMetric("Tổng ngành nghề", Fmt(_service.Trades.Count), "nghề"),
                new ScreenMetric("Danh vọng phường nghề", Fmt(_service.TotalGuildFame), "điểm", MetricTone.Positive),
                new ScreenMetric("Vũ khí trang bị", _service.Slots.FirstOrDefault(s => s.SlotCode == "WEAPON")?.Equipment?.NameVi ?? "Tay không", "Binh khí"),
                new ScreenMetric("Tay nghề cao nhất", $"bậc {bestTier}", "trên 5", bestTier > 0 ? MetricTone.Positive : MetricTone.Neutral)
            ],
            []);
    }

    private static string Fmt(long value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

internal sealed class ProfessionCommandHandler : IScreenCommandHandler
{
    private readonly ProfessionService _service;
    private readonly IPlayerState _player;

    public ProfessionCommandHandler(ProfessionService service, IPlayerState player)
    {
        _service = service;
        _player = player;
    }

    public string ScreenId => "professions.trades";

    public string? Execute(string commandId)
    {
        var cityId = _player.CurrentCityId;
        var parts = commandId.Split(':');
        if (parts.Length < 2)
        {
            return "Lệnh không hợp lệ.";
        }

        var verb = parts[0];

        switch (verb)
        {
            case "work":
                if (long.TryParse(parts[1], out var tradeId))
                    return _service.Work(cityId, tradeId).Message;
                break;

            case "craft":
                if (long.TryParse(parts[1], out var recipeId))
                    return _service.Craft(cityId, recipeId).Message;
                break;

            case "repair":
                if (long.TryParse(parts[1], out var eqId))
                    return _service.Repair(cityId, eqId).Message;
                break;

            case "equip":
                if (long.TryParse(parts[1], out var equipId))
                    return _service.Equip(equipId).Message;
                break;

            case "unequip":
                return _service.Unequip(parts[1]).Message;

            case "workshop":
                if (parts.Length >= 3)
                {
                    var sub = parts[1];
                    if (sub == "open" && long.TryParse(parts[2], out var wsTradeId))
                    {
                        return _service.OpenWorkshop(cityId, wsTradeId).Message;
                    }
                    if (sub == "hire")
                    {
                        return _service.HireArtisan(cityId).Message;
                    }
                    if (sub == "dismiss")
                    {
                        return _service.DismissArtisan(cityId).Message;
                    }
                    if (sub == "upgrade")
                    {
                        return _service.UpgradeFurnace(cityId).Message;
                    }
                }
                break;

            case "perk":
                if (parts.Length >= 3 && parts[1] == "unlock" && long.TryParse(parts[2], out var perkId))
                {
                    return _service.UnlockPerk(perkId).Message;
                }
                break;

            case "order":
                if (parts.Length >= 3)
                {
                    var orderVerb = parts[1];
                    if (long.TryParse(parts[2], out var ordId))
                    {
                        if (orderVerb == "accept")
                            return _service.AcceptOrder(ordId).Message;
                        if (orderVerb == "fulfill")
                            return _service.FulfillOrder(cityId, ordId).Message;
                    }
                }
                break;

            case "wild":
                if (parts.Length >= 2)
                {
                    if (parts[1] == "fish")
                        return _service.Fish(cityId).Message;
                    if (parts[1] == "forage")
                        return _service.Forage(cityId).Message;
                    if (parts[1] == "hunt")
                        return _service.Hunt(cityId).Message;
                }
                break;

            case "dish":
                if (parts.Length >= 3)
                {
                    if (parts[1] == "cook" && long.TryParse(parts[2], out var cookDishId))
                        return _service.Cook(cityId, cookDishId).Message;
                    if (parts[1] == "eat" && long.TryParse(parts[2], out var eatDishId))
                        return _service.Eat(eatDishId).Message;
                }
                break;

            case "temper":
                if (long.TryParse(parts[1], out var temperEqId))
                    return _service.Temper(cityId, temperEqId).Message;
                break;
        }

        return $"Lệnh không hợp lệ: {commandId}";
    }
}
