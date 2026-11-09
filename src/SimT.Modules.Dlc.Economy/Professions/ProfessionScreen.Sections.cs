using SimT.Contracts.Playing;
using SimT.Contracts.Ui;

namespace SimT.Modules.Dlc.Economy;

internal sealed partial class ProfessionScreen
{
    private (ScreenSection SlotSection, ScreenSection EquipSection, ScreenSection RecipeSection, int EquipmentCount) BuildForgeSections(
        long cityId,
        WorkshopDef? workshop,
        List<ScreenAlert> alerts)
    {
        // 1. Equipped slots
        var slotRows = new List<ScreenRow>();
        foreach (var slot in _service.Slots)
        {
            var equip = slot.Equipment;
            var commands = equip is not null
                ? new List<ScreenCommand> { new ScreenCommand($"unequip:{slot.SlotCode}", "Tháo ra") }
                : new List<ScreenCommand>();

            if (equip is not null && equip.Durability * 2 < equip.MaxDurability)
            {
                alerts.Add(new ScreenAlert(
                    $"Bảo khí '{equip.NameVi}' tại ô {slot.SlotNameVi} đã hao mòn ({equip.Durability}/{equip.MaxDurability}). Hãy tu sửa sớm để tránh sứt mẻ gãy vỡ!",
                    AlertLevel.Warning));
            }

            slotRows.Add(new ScreenRow(
                [
                    slot.SlotNameVi,
                    equip?.NameVi ?? "(Để trống)",
                    equip != null ? $"+{equip.BonusValue} {equip.BonusStat}" : "—",
                    equip != null ? $"{equip.Durability}/{equip.MaxDurability}" : "—",
                    ProfessionService.FormatQualityRankVi(equip?.QualityRank)
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

            var repairCost = _service.CalculateRepairCost(cityId, eq);
            if (eq.Durability < eq.MaxDurability)
            {
                commands.Add(new ScreenCommand($"repair:{eq.Id}", $"Tu sửa ({repairCost} Quan)"));
            }

            if (eq.TemperLevel < 5)
            {
                commands.Add(new ScreenCommand($"temper:{eq.Id}", $"Tôi luyện (+{eq.TemperLevel + 1})"));
            }

            var eqSellVal = _service.CalculateEquipmentSellValue(eq);
            commands.Add(new ScreenCommand($"sell:eq:{eq.Id}", $"Bán ({eqSellVal} Quan)"));

            var creatorLabel = !string.IsNullOrWhiteSpace(eq.CreatorNameVi) ? $" · 👑 {eq.CreatorNameVi}" : string.Empty;
            var featLabel = eq.KillCount > 0 ? $" · ⚔️ Trảm {eq.KillCount} (Uy Danh {eq.FameScore})" : string.Empty;
            var effectiveBonus = ProfessionService.GetEffectiveEquipmentBonus(eq);

            equipRows.Add(new ScreenRow(
                [
                    $"{eq.NameVi} [+{eq.TemperLevel}]" + (isEquipped ? " [Đang mặc]" : string.Empty) + creatorLabel + featLabel,
                    $"+{effectiveBonus} {eq.BonusStat}",
                    $"{eq.Durability}/{eq.MaxDurability} ({ProfessionService.FormatDurabilityStateVi(eq)})",
                    ProfessionService.FormatQualityRankVi(eq.QualityRank)
                ],
                commands,
                Title: eq.NameVi,
                Details:
                [
                    new ScreenDetail("Nghệ Nhân Chế Tác", eq.CreatorNameVi ?? "Quan Xưởng Triều Đình"),
                    new ScreenDetail("Thành Trì Xuất Xưởng", $"Thành #{eq.ForgedCitadelId ?? eq.CitadelId} (Ngày {eq.ForgedDayIndex ?? 1})"),
                    new ScreenDetail("Kiểm Định Phẩm Chất", ProfessionService.FormatQualityRankVi(eq.QualityRank)),
                    new ScreenDetail("Trạng Thái Độ Bền", $"{eq.Durability}/{eq.MaxDurability} — {ProfessionService.FormatDurabilityStateVi(eq)}"),
                    new ScreenDetail("Chi Phí Tu Sửa", eq.Durability < eq.MaxDurability ? $"{repairCost} Quan (Giảm 50% nếu có sẵn nguyên liệu; +5 Độ bền tối đa nếu có Huyền Thiết)" : "Nguyên vẹn (Không cần tu sửa)"),
                    new ScreenDetail("Chiến Tích Sa Trường", eq.KillCount > 0 ? $"Trảm {eq.KillCount} địch · Uy danh +{eq.FameScore}" : "Chưa nhuốm máu sa trường"),
                    new ScreenDetail("Biên Niên Sử Bảo Khí", eq.LegendNarrative ?? "Chưa có chiến tích lưu danh sử sách.")
                ]));
        }

        // 3. Crafting recipes & workbench visual flow
        var recipeRows = new List<ScreenRow>();
        var wsFurnaceLvl = workshop != null && workshop.IsActive ? workshop.FurnaceLevel : 0;
        foreach (var r in _service.Recipes)
        {
            var matItem = _items.Find(r.MaterialItemId);
            var mat = matItem?.NameVi ?? $"Vật phẩm #{r.MaterialItemId}";
            var output = _items.Find(r.OutputItemId)?.NameVi ?? r.NameVi;
            var currentTier = _service.Tier(r.TradeId);
            var canCraftTier = currentTier >= r.RequiredTier;

            var matHeld = _bag?.Quantity(cityId, r.MaterialItemId) ?? 0;
            var hasMainMat = matHeld >= r.MaterialQuantity;

            var hasSecMat = true;
            var secInfo = "Không yêu cầu";
            var secShort = string.Empty;
            if (r.SecondaryItemId.HasValue && r.SecondaryQuantity > 0)
            {
                var secItem = _items.Find(r.SecondaryItemId.Value);
                var secName = secItem?.NameVi ?? $"Vật phẩm #{r.SecondaryItemId.Value}";
                var secHeld = _bag?.Quantity(cityId, r.SecondaryItemId.Value) ?? 0;
                hasSecMat = secHeld >= r.SecondaryQuantity;
                secInfo = $"{r.SecondaryQuantity}x {secName} (Có {secHeld})";
                secShort = $" + {r.SecondaryQuantity}x {secName}";
            }

            var hasMaterials = hasMainMat && hasSecMat;
            var canCraft = canCraftTier && hasMaterials;

            var commands = new List<ScreenCommand>();
            if (canCraft)
            {
                commands.Add(new ScreenCommand($"craft:{r.Id}", "Rèn đúc", CommandKind.Primary));
            }
            else if (!hasMainMat)
            {
                var missingMain = Math.Max(1, r.MaterialQuantity - matHeld);
                var bountyReward = Math.Max(20, (int)((matItem?.BasePrice ?? 25) * missingMain * 1.15));
                commands.Add(new ScreenCommand(
                    $"bounty:post:{r.MaterialItemId}:{missingMain}:{bountyReward}",
                    $"📌 Treo thưởng {missingMain} {mat} ({bountyReward}q)"));
            }
            else if (!hasSecMat && r.SecondaryItemId.HasValue)
            {
                var secItem = _items.Find(r.SecondaryItemId.Value);
                var secHeld = _bag?.Quantity(cityId, r.SecondaryItemId.Value) ?? 0;
                var missingSec = Math.Max(1, r.SecondaryQuantity - secHeld);
                var bountyReward = Math.Max(20, (int)((secItem?.BasePrice ?? 25) * missingSec * 1.15));
                commands.Add(new ScreenCommand(
                    $"bounty:post:{r.SecondaryItemId.Value}:{missingSec}:{bountyReward}",
                    $"📌 Treo thưởng {missingSec} {secItem?.NameVi ?? "Phụ liệu"} ({bountyReward}q)"));
            }

            var qualityPreview = currentTier >= 4
                ? (wsFurnaceLvl >= 3 ? "Cực Phẩm ★★★" : "Thượng Phẩm ★★")
                : (currentTier >= 2 ? "Trung Phẩm ★" : "Hạ Phẩm");

            var matDisplay = $"{r.MaterialQuantity}x {mat}" + (hasMainMat ? " ✅" : $" (⚠️ {matHeld}/{r.MaterialQuantity})");
            if (!string.IsNullOrEmpty(secShort))
            {
                matDisplay += hasSecMat ? " + Phụ ✅" : " + Phụ ⚠️";
            }

            recipeRows.Add(new ScreenRow(
                [
                    r.NameVi,
                    $"Bậc {r.RequiredTier} (Hiện {currentTier})",
                    matDisplay,
                    $"{r.CraftFee} Quan",
                    $"{r.CraftDays} ngày",
                    $"{r.OutputQuantity}x {output} [{qualityPreview}]"
                ],
                commands,
                Title: r.NameVi,
                Details:
                [
                    new ScreenDetail("Bản Vẽ Chế Tác", r.NameVi),
                    new ScreenDetail("Quy Trình Gia Công", $"{mat} ({r.MaterialQuantity}){secShort} ➔ [Lò Cấp {wsFurnaceLvl}] ➔ {output} ({r.OutputQuantity})"),
                    new ScreenDetail("Bậc Tay Nghề", $"Yêu cầu Bậc {r.RequiredTier} (Cấp hiện tại: Bậc {currentTier})"),
                    new ScreenDetail("Nguyên Liệu Chính", $"{r.MaterialQuantity}x {mat} (Trong túi: {matHeld})"),
                    new ScreenDetail("Nguyên Liệu Phụ", secInfo),
                    new ScreenDetail("Dự Báo Phẩm Chất", $"{qualityPreview} (Cấp lò xưởng: {wsFurnaceLvl}/5)"),
                    new ScreenDetail("Trạng Thái", canCraft ? "✅ Đủ điều kiện rèn đúc" : (canCraftTier ? "⚠️ Chưa đủ nguyên liệu" : "❌ Chưa đủ bậc tay nghề")),
                    new ScreenDetail("Phí Thuê Lò", $"{r.CraftFee} Quan"),
                    new ScreenDetail("Thời Gian Chế Tác", $"{r.CraftDays} ngày"),
                    new ScreenDetail("Thành Phẩm", $"{r.OutputQuantity}x {output}")
                ]));
        }

        return (
            new ScreenSection("Trang Bị Cá Nhân & Chỉ Số Cộng", SlotColumns, slotRows, SectionViewMode.ArtisanWorkbench),
            new ScreenSection("Khí Cụ Trong Thành", EquipmentColumns, equipRows, SectionViewMode.ArtisanWorkbench),
            new ScreenSection("Bản Vẽ Chế Tác Khí Cụ", RecipeColumns, recipeRows, SectionViewMode.ArtisanWorkbench),
            equipments.Count);
    }

    private (ScreenSection WorkshopSection, ScreenSection TradeSection) BuildWorkshopAndTradeSections(
        long cityId,
        WorkshopDef? workshop,
        List<ScreenAlert> alerts)
    {
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

            var inputId = trade is not null ? ProfessionService.GetWorkshopInputItemId(trade.Code) : null;
            var inputDesc = "Khai thác trực tiếp (Không cần nguyên liệu thô)";
            var wsStatusVi = workshop.IsActive ? "Đang mở cửa" : "Đình chỉ";
            if (inputId.HasValue)
            {
                var inputItem = _items.Find(inputId.Value);
                var heldInput = _bag?.Quantity(cityId, inputId.Value) ?? 0;
                inputDesc = $"Cần {workshop.HiredArtisans}x {inputItem?.NameVi ?? "Nguyên liệu"}/tháng (Trong túi: {heldInput})";
                if (workshop.IsActive && workshop.HiredArtisans > 0 && heldInput < workshop.HiredArtisans)
                {
                    var missingInput = Math.Max(2, workshop.HiredArtisans * 2 - heldInput);
                    var reward = (int)((inputItem?.BasePrice ?? 20) * missingInput * 1.15);
                    wsStatusVi = $"⚠️ Thiếu {inputItem?.NameVi}";
                    commands.Add(new ScreenCommand($"bounty:post:{inputId.Value}:{missingInput}:{reward}", $"📌 Treo thưởng mua {missingInput} {inputItem?.NameVi} ({reward}q)"));
                    alerts.Add(new ScreenAlert(
                        $"Xưởng '{workshop.NameVi}' đang Thiếu nguyên liệu đầu vào ({inputItem?.NameVi ?? "Nguyên liệu"}: có {heldInput}/{workshop.HiredArtisans}). Hãy bổ sung hoặc treo thưởng thu mua để thợ phụ không bị đình trệ cuối tháng!",
                        AlertLevel.Warning));
                }
            }

            commands.Add(new ScreenCommand("workshop:close:0", "Sang nhượng xưởng (+36 Quan)", CommandKind.Danger));

            workshopRows.Add(new ScreenRow(
                [
                    workshop.NameVi,
                    trade?.NameVi ?? "Chế tác",
                    $"Cấp {workshop.FurnaceLevel}/5",
                    $"{workshop.HiredArtisans}/5 thợ (Lương {workshop.HiredArtisans * ProfessionService.ArtisanMonthlySalary} Quan/tháng)",
                    $"{workshop.MonthlyRent} Quan/tháng",
                    wsStatusVi
                ],
                commands,
                Title: workshop.NameVi,
                Details:
                [
                    new ScreenDetail("Lò luyện", $"Cấp {workshop.FurnaceLevel} (+{workshop.FurnaceLevel * 5}% tỷ lệ Tuyệt phẩm, +{Math.Max(0, workshop.FurnaceLevel - 1)} sản lượng/thợ)"),
                    new ScreenDetail("Thợ phụ", $"{workshop.HiredArtisans} người tự động gia công hàng tháng"),
                    new ScreenDetail("Nguyên liệu đầu vào", inputDesc),
                    new ScreenDetail("Tiền thuê", $"{workshop.MonthlyRent} Quan khấu trừ ngày đầu tháng")
                ]));
        }
        else
        {
            var candidateTrades = _activeFilter == "workshop"
                ? _service.Trades
                : _service.Trades.Where(t => ProfessionService.GetWorkshopInputItemId(t.Code).HasValue || t.Code == "tho_moc").ToList();

            foreach (var trade in candidateTrades)
            {
                var inputId = ProfessionService.GetWorkshopInputItemId(trade.Code);
                var inputLabel = inputId.HasValue
                    ? $"Cần {_items.Find(inputId.Value)?.NameVi ?? "Nguyên liệu"}"
                    : "Khai thác tự chủ";
                workshopRows.Add(new ScreenRow(
                    [
                        $"Mở Xưởng {trade.NameVi}",
                        $"{trade.NameVi} ({inputLabel})",
                        "Cấp 1",
                        "0 thợ",
                        $"{ProfessionService.WorkshopBaseRent} Quan/tháng",
                        "Chưa thuê"
                    ],
                    [new ScreenCommand($"workshop:open:{trade.Id}", $"Thuê mặt bằng ({ProfessionService.WorkshopOpenCost} Quan)")]));
            }
        }

        // Basic trades
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
                    $"{trade.NameVi} ({trade.NameEn})" + (workshop?.TradeId == trade.Id ? " [Có Xưởng]" : string.Empty),
                    sectorVi,
                    rank,
                    Fmt(trade.DaysPerBatch),
                    output?.NameVi ?? trade.OutputItemId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Fmt(finalYield),
                    Fmt(trade.ToolFee),
                    $"bậc {tier} · {_service.PracticeCount(trade.Id)} lượt"
                ],
                [
                    new ScreenCommand($"work:{trade.Id}", "Làm 1 lượt", CommandKind.Primary),
                    new ScreenCommand($"work:batch:{trade.Id}:3", "Làm đợt 3 lượt")
                ],
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

        return (
            new ScreenSection("Xưởng Chế Tác Tại Thành", ["Tên Xưởng", "Chuyên Môn", "Lò Luyện", "Thợ Phụ", "Tiền Thuê", "Trạng Thái"], workshopRows),
            new ScreenSection("Bách Nghệ Lao Tác (16 Ngành Nghề)", Columns, rows));
    }

    private (ScreenSection OrderSection, ScreenSection BountySection, int OrderCount) BuildOrderAndBountySections(
        long cityId,
        List<ScreenAlert> alerts)
    {
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
                var daysLeft = ord.DeadlineDay - _service.CurrentDayIndex;
                if (daysLeft <= 5)
                {
                    alerts.Add(new ScreenAlert(
                        $"Đơn ủy thác của {ord.ClientNameVi} ({ord.QuantityNeeded}x {targetItem?.NameVi ?? "vật phẩm"}) sắp đến hạn chót (còn {Math.Max(0, daysLeft)} ngày)!",
                        AlertLevel.Warning));
                }
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

        var bountyRows = new List<ScreenRow>();
        foreach (var b in _service.PlayerBounties(cityId))
        {
            var bItem = _items.Find(b.TargetItemId);
            var bCommands = b.Status == "AVAILABLE"
                ? new List<ScreenCommand> { new ScreenCommand($"bounty:cancel:{b.Id}", "Thu hồi cáo thị") }
                : new List<ScreenCommand>();

            var bTypeVi = b.ClientType == "RETAINER_TASK" ? "⚔️ Sai Phái Môn Khách" : "📜 Cáo Thị Treo Thưởng";
            var bAssignee = !string.IsNullOrWhiteSpace(b.AssigneeNameVi) ? b.AssigneeNameVi : "(Đang chờ thợ nhận)";
            var bStatusVi = b.Status switch
            {
                "AVAILABLE" => "Đang niêm yết",
                "ACCEPTED" => b.CompletionDay.HasValue ? $"Đang thu thập (Xong ngày {b.CompletionDay.Value})" : "Đang thu thập",
                "COMPLETED" => "✅ Đã giao vào hành trang",
                "CANCELLED" => "Đã thu hồi",
                _ => "Hết hạn"
            };

            bountyRows.Add(new ScreenRow(
                [
                    bTypeVi,
                    bItem?.NameVi ?? $"Vật phẩm #{b.TargetItemId}",
                    Fmt(b.QuantityNeeded),
                    b.ClientType == "RETAINER_TASK" ? "Lương tháng" : $"{b.RewardCopper} Quan",
                    bAssignee,
                    bStatusVi
                ],
                bCommands));
        }

        long[] bountyItemIds = [5, 15, 16, 7, 13, 25];
        foreach (var itemId in bountyItemIds)
        {
            var itemDef = _items.Find(itemId);
            if (itemDef is null) continue;
            var qty = itemId is 13 or 25 ? 2 : 4;
            var suggestedReward = (int)(itemDef.BasePrice * qty * 1.15);
            bountyRows.Add(new ScreenRow(
                [
                    "📌 Phát Hành Cáo Thị Mới",
                    itemDef.NameVi,
                    Fmt(qty),
                    $"{suggestedReward} Quan (Ký quỹ)",
                    "Thợ mỏ / Dược sư trong thành",
                    "Sẵn sàng đăng bảng"
                ],
                [new ScreenCommand($"bounty:post:{itemId}:{qty}:{suggestedReward}", $"Treo thưởng {qty} {itemDef.NameVi} ({suggestedReward} Quan)")]));
        }

        return (
            new ScreenSection("Ủy Thác & Đơn Đặt Hàng Phường Hội", OrderColumns, orderRows),
            new ScreenSection("Bảng Cáo Thị Ủy Thác Khai Thác & Sai Phái", ["Hình Thức", "Vật Phẩm Cần", "Số Lượng", "Thù Lao / Chi Phí", "Người Nhận", "Tiến Độ"], bountyRows),
            orderRows.Count);
    }

    private (ScreenSection DishSection, ScreenSection WildSection, ScreenSection PerkSection) BuildLifeSkillSections(long cityId)
    {
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
                var dishSellVal = _service.CalculateDishSellValue(d);
                commands.Add(new ScreenCommand($"dish:sell:{d.Id}", $"Bán ({dishSellVal} Quan)"));
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

        var forestSummary = _service.GetRegionalNodeSummary(cityId, "FOREST_NODE");
        var mineSummary = _service.GetRegionalNodeSummary(cityId, "MINE_NODE");
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
                    forestSummary,
                    "1 ngày · 15 Mệt mỏi",
                    "2~3 Thảo dược (Cơ duyên hái Nhân sâm)",
                    "Mở mang nhận thức, +PRC/INT"
                ],
                [new ScreenCommand("wild:forage", "Lên núi hái thuốc")]),
            new(
                [
                    "Săn Bắn Rừng Sâu",
                    forestSummary,
                    "1 ngày · 20 Mệt mỏi",
                    "2~3 Thịt thú (Cơ duyên da lông/Mật gấu)",
                    "Tăng cường thể phách, +WAR/PRC/CON"
                ],
                [new ScreenCommand("wild:hunt", "Giương cung săn bắn")]),
            new(
                [
                    "Khai Mỏ & Lâm Trường",
                    mineSummary,
                    "1 ngày · 20 Mệt mỏi",
                    "2~4 Sắt / Đồng / Gỗ / Than đá theo mạch mỏ vùng",
                    "Rèn luyện gân cốt & công xảo, +CRA/CON"
                ],
                [new ScreenCommand("wild:mine", "Vung cuốc khai khoáng")])
        };

        return (
            new ScreenSection("Bếp Nấu Ẩm Thực (Mỹ Vị Tam Quốc)", ["Món Ăn", "Nguyên Liệu", "Chi Phí", "Hiệu Quả Sinh Mệnh", "Có Sẵn"], dishRows),
            new ScreenSection("Dã Ngoại Sinh Hoạt (Câu Cá, Hái Thuốc & Săn Bắn)", ["Hoạt Động", "Địa Điểm", "Tiêu Hao", "Thu Hoạch Dự Kiến", "Lợi Ích Thần Trí"], wildRows),
            new ScreenSection("Bí Kỹ Nghề Nghiệp & Giác Ngộ", PerkColumns, perkRows));
    }
}
