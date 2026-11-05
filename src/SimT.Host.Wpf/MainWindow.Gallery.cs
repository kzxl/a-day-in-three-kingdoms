using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using SimT.Contracts.Ui;
using SimT.Host.Wpf.Controls;

namespace SimT.Host.Wpf;

public partial class MainWindow
{
    public const string UiGalleryScreenId = "ui.gallery";

    private void RenderUiGalleryScreen()
    {
        ScreenTitle.Text = "Bộ Control Chuyên Dụng Tam Quốc";
        ScreenStatus.Text = "Màn hình kiểm thử đầy đủ 10 control chuyên dụng (GameButton, GameTable, GameItemSlot, GameQuantityPicker, GameTabs, GameProgressBar, GameToast, GameConfirmDialog, GameRichTooltip, GamePortraitCard)";

        KpiStrip.Children.Clear();
        KpiStrip.Children.Add(BuildMetric(new ScreenMetric("Control chuyên dụng", "10", "đồng bộ sơn mài & lụa")));
        KpiStrip.Children.Add(BuildMetric(new ScreenMetric("Bảng màu", "Sơn mài", "Chu sa · Đồng · Ngọc")));

        AlertFeed.ItemsSource = null;
        GlobalCommands.Children.Clear();
        FilterBox.Visibility = Visibility.Collapsed;
        FilterHint.Visibility = Visibility.Collapsed;
        ContentPanel.Children.Clear();

        // 1. GameTabs
        var tabs = new GameTabs(
        [
            new GameTabItem("all", "Tất cả Control", 10),
            new GameTabItem("buttons", "Nút & Hộp thoại", 3),
            new GameTabItem("items", "Vật phẩm & Số lượng", 3),
            new GameTabItem("npcs", "Nhân sĩ & Bảng", 4)
        ]);
        tabs.SelectionChanged += (_, tab) => ShowNotification($"Đã chuyển sang thẻ: {tab.Title}", GameToastKind.Event);
        ContentPanel.Children.Add(tabs);

        // 2. GameButton + GameToast + GameConfirmDialog
        var btnWrap = new WrapPanel { Margin = new Thickness(0, 2, 0, 0) };
        var bPrimary = new GameButton("Mua hàng", GameButtonVariant.Primary, costText: "14 q",
            iconPathData: "M3,7 L21,7 L19,12 L5,12 Z M5,12 L5,20 L19,20 L19,12")
        {
            Margin = new Thickness(0, 0, 8, 8),
            ToolTip = new GameRichTooltip("Nút Chính (Chu Sa)", "GameButtonVariant.Primary",
                [("Chi phí", "14 quan"), ("Thời gian", "1 ngày")], "Dùng cho hành động chính của màn hình.")
        };
        bPrimary.Click += (_, _) => ShowNotification("Đã mua 1 kiện Lụa Thục Cẩm (+14 quan).", GameToastKind.Gain);
        btnWrap.Children.Add(bPrimary);

        var bSecondary = new GameButton("Hành trình", GameButtonVariant.Secondary, costText: "2 ngày",
            iconPathData: "M3,6 L9,3 L15,6 L21,3 L21,18 L15,21 L9,18 L3,21 Z")
        {
            Margin = new Thickness(0, 0, 8, 8)
        };
        bSecondary.Click += (_, _) => ShowNotification("Đoàn thương nhân khởi hành đi Trường An.", GameToastKind.Event);
        btnWrap.Children.Add(bSecondary);

        var bDanger = new GameButton("Hủy giao ước", GameButtonVariant.Danger,
            iconPathData: "M6,6 L18,18 M18,6 L6,18")
        {
            Margin = new Thickness(0, 0, 8, 8)
        };
        bDanger.Click += (_, _) =>
        {
            if (GameConfirmDialog.Ask(this, "Xác nhận hủy giao ước",
                    "Hủy giao ước thương hội sẽ trừ 15 điểm Tín Nghĩa. Bạn có chắc chắn muốn tiếp tục?",
                    "Chấp nhận", "Quay lại", isDanger: true))
            {
                ShowNotification("Đã hủy giao ước (-15 Tín Nghĩa).", GameToastKind.Loss);
            }
        };
        btnWrap.Children.Add(bDanger);

        var bIcon = new GameButton(string.Empty, GameButtonVariant.Icon,
            iconPathData: "M12,2 L15,9 L22,12 L15,15 L12,22 L9,15 L2,12 L9,9 Z")
        {
            Margin = new Thickness(0, 0, 8, 8),
            ToolTip = new GameRichTooltip("Nút Icon", "GameButtonVariant.Icon", description: "Nút biểu tượng nhỏ gọn.")
        };
        bIcon.Click += (_, _) => ShowNotification("Đã kích hoạt hiệu ứng kỳ ngộ.", GameToastKind.Event);
        btnWrap.Children.Add(bIcon);

        var bDisabled = new GameButton("Chưa đủ cấp", GameButtonVariant.Secondary, costText: "50 q")
        {
            IsEnabled = false,
            Margin = new Thickness(0, 0, 8, 8)
        };
        btnWrap.Children.Add(bDisabled);

        ContentPanel.Children.Add(new GameSectionCard("GameButton · GameToast · GameConfirmDialog", btnWrap));

        // 3. GameProgressBar & GameQuantityPicker
        var barsAndPickerGrid = new Grid();
        barsAndPickerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        barsAndPickerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
        barsAndPickerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(320) });

        var barsStack = new StackPanel();
        barsStack.Children.Add(new GameProgressBar("Sinh lực (Health)", 86, 100, GameProgressTone.Health) { Margin = new Thickness(0, 0, 0, 10) });
        barsStack.Children.Add(new GameProgressBar("Thể lực (Stamina)", 72, 100, GameProgressTone.Stamina) { Margin = new Thickness(0, 0, 0, 10) });
        barsStack.Children.Add(new GameProgressBar("Hảo cảm (Affection)", 64, 100, GameProgressTone.Affection) { Margin = new Thickness(0, 0, 0, 10) });
        barsStack.Children.Add(new GameProgressBar("Kinh nghiệm (Skill EXP)", 420, 500, GameProgressTone.Experience));
        Grid.SetColumn(barsStack, 0);
        barsAndPickerGrid.Children.Add(barsStack);

        var picker = new GameQuantityPicker(
            commandId: "demo:buy",
            actionLabel: "Mua Lụa Thục Cẩm",
            buttonVariant: GameButtonVariant.Primary,
            min: 1,
            max: 25,
            initial: 5,
            previewFunc: qty => $"Tổng tiền: {(qty * 14).ToString("N0", Vietnamese)} quan · Còn dư: {(2000 - qty * 14).ToString("N0", Vietnamese)} quan",
            costText: "1 ngày");
        picker.CommitClicked += (_, _) => ShowNotification($"Đã đặt mua {picker.Value} kiện Lụa Thục Cẩm.", GameToastKind.Gain);
        Grid.SetColumn(picker, 2);
        barsAndPickerGrid.Children.Add(picker);

        ContentPanel.Children.Add(new GameSectionCard("GameProgressBar · GameQuantityPicker", barsAndPickerGrid));

        // 4. GameItemSlot & GameRichTooltip
        var slotWrap = new WrapPanel();
        slotWrap.Children.Add(new GameItemSlot("Gạo Trắng", 48, GameItemRarity.Common, "Lương thực",
            [("Giá gốc", "12 quan"), ("Bảo quản", "90 ngày")], "Lương thực thiết yếu nuôi sống bách tính và quân sĩ."));
        slotWrap.Children.Add(new GameItemSlot("Trà Vũ Di", 16, GameItemRarity.Fine, "Danh trà",
            [("Giá gốc", "35 quan"), ("Hảo cảm", "+8 điểm")], "Hương trà thanh khiết vùng Giang Đông, danh sĩ ưa chuộng."));
        slotWrap.Children.Add(new GameItemSlot("Chiến Mã", 4, GameItemRarity.Master, "Quân nhu",
            [("Giá gốc", "180 quan"), ("Tốc độ", "+25%")], "Tuấn mã Lương Châu dẻo dai vượt ngàn dặm."));
        slotWrap.Children.Add(new GameItemSlot("Thục Cẩm", 9, GameItemRarity.Legendary, "Gấm vóc",
            [("Giá gốc", "260 quan"), ("Uy danh", "+12 điểm")], "Gấm thêu tay tinh xảo bậc nhất thành Thành Đô."));
        slotWrap.Children.Add(new GameItemSlot("Thanh Long Đao", 1, GameItemRarity.Imperial, "Thần binh",
            [("Võ lực", "+18"), ("Trọng lượng", "82 cân")], "Thần binh hộ quốc uy chấn thiên hạ."));
        ContentPanel.Children.Add(new GameSectionCard("GameItemSlot · GameRichTooltip", slotWrap));

        // 5. GamePortraitCard
        var portraitWrap = new WrapPanel();
        portraitWrap.Children.Add(new GamePortraitCard("Lưu Bị", "general", "Trác Huyện · Nhân đức", 78, 100, "liu_bei",
            [new ScreenCommand("talk:npc:1", "Bái phỏng", CommandKind.Primary), new ScreenCommand("npc:modal:1", "Hồ sơ", CommandKind.Secondary)],
            [("Đức độ", "95"), ("Thống lĩnh", "82")], "Anh hùng đất Trác Quận, lấy nhân nghĩa thu phục lòng người."));
        portraitWrap.Children.Add(new GamePortraitCard("Quan Vũ", "general", "Lạc Dương · Nghĩa khí", 65, 100, "guan_yu",
            [new ScreenCommand("talk:npc:2", "Bái phỏng", CommandKind.Primary), new ScreenCommand("duel:2", "Tỷ võ", CommandKind.Secondary)],
            [("Võ lực", "98"), ("Trung nghĩa", "100")], "Mỹ Nhiêm Công võ nghệ tuyệt luân, trọng nghĩa khinh tài."));
        portraitWrap.Children.Add(new GamePortraitCard("Gia Cát Lượng", "scholar", "Tương Dương · Ngọa Long", 88, 100, "zhuge_liang",
            [new ScreenCommand("talk:npc:4", "Bái phỏng", CommandKind.Primary), new ScreenCommand("npc:modal:4", "Hồ sơ", CommandKind.Secondary)],
            [("Trí lực", "100"), ("Chính trị", "96")], "Ẩn sĩ Long Trung, ngồi trong lều cỏ định thế chân vạc."));
        ContentPanel.Children.Add(new GameSectionCard("GamePortraitCard", portraitWrap));

        // 6. GameTable
        var sampleSection = new ScreenSection(
            "GameTable (Ảo hóa · Sắp xếp cột · Mũi tên tăng giảm · Nút hiện khi rê chuột)",
            ["Danh mục", "Phân loại", "Độ bền", "Biến động", "Đơn giá"],
            [
                new ScreenRow(["Lụa Thục Cẩm", "TINH CHẾ", "92/100", "+18", "260"],
                    [new ScreenCommand("demo:1", "Mua", CommandKind.Primary), new ScreenCommand("demo:2", "Bán", CommandKind.Secondary)]),
                new ScreenRow(["Trà Vũ Di", "PHỔ THÔNG", "75/100", "-6", "35"],
                    [new ScreenCommand("demo:3", "Mua", CommandKind.Primary)]),
                new ScreenRow(["Chiến Mã Lương Châu", "ĐẠI SƯ", "100/100", "+42", "180"],
                    [new ScreenCommand("demo:4", "Mua", CommandKind.Primary)])
            ]);
        ContentPanel.Children.Add(BuildCard(sampleSection));
    }

    private UIElement? TryBuildPortraitStrip(ScreenSection section)
    {
        if (_screenId is not ("npc.directory" or "social.network"))
            return null;

        var npcRows = section.Rows
            .Where(r => r.Commands.Any(c =>
                c.Id.StartsWith("talk:npc:", StringComparison.Ordinal) ||
                c.Id.StartsWith("npc:modal:", StringComparison.Ordinal)))
            .Take(8)
            .ToList();

        if (npcRows.Count == 0)
            return null;

        var wrap = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        foreach (var row in npcRows)
        {
            var name = row.Cells.Count > 0 ? row.Cells[0] : (row.Title ?? "Nhân sĩ");
            var role = row.Cells.Count > 1 ? row.Cells[1] : "Danh sĩ";
            var sub = row.Cells.Count > 2 ? row.Cells[2] : null;
            var aff = ExtractAffectionFromRow(row);

            var card = new GamePortraitCard(
                nameVi: name,
                roleVi: role,
                subtitleVi: sub,
                affectionValue: aff,
                affectionMax: 100,
                npcCodeOrRole: role,
                commands: row.Commands,
                tooltipStats: row.Info.Take(4).Select(d => (d.Label, d.Value)).ToList());

            card.CommandInvoked += cmdId => _ = ExecuteScreenCommandAsync(cmdId);
            card.CardSelected += () => ShowInspector(section, row);
            wrap.Children.Add(card);
        }

        return wrap;
    }

    private UIElement? TryBuildItemSlotStrip(ScreenSection section)
    {
        if (_screenId is not ("market.warehouse" or "market.player_stall" or "content.item_hub" or "market.stall") ||
            section.Rows.Count == 0)
        {
            return null;
        }

        var wrap = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        foreach (var row in section.Rows.Take(10))
        {
            var name = row.Cells.Count > 0 ? row.Cells[0] : (row.Title ?? "Vật phẩm");
            var qty = ExtractQuantityFromRow(row);
            var rarity = GameItemSlot.ParseRarity(row.RarityHint ?? string.Join(' ', row.Cells));
            var stats = row.Info.Take(4).Select(d => (d.Label, d.Value)).ToList();

            var slot = new GameItemSlot(name, qty, rarity, section.Heading, stats);
            slot.Clicked += () => ShowInspector(section, row);
            wrap.Children.Add(slot);
        }

        return wrap;
    }

    private static double ExtractAffectionFromRow(ScreenRow row)
    {
        foreach (var cell in row.Cells)
        {
            var slash = cell.IndexOf('/');
            if (slash > 0 && double.TryParse(cell[..slash].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
                return Math.Clamp(v, 0, 100);
        }
        return 35;
    }

    private static int ExtractQuantityFromRow(ScreenRow row)
    {
        for (var i = 1; i < row.Cells.Count; i++)
        {
            if (int.TryParse(row.Cells[i].Trim(), NumberStyles.AllowThousands, CultureInfo.GetCultureInfo("vi-VN"), out var n) && n > 0 && n <= 9999)
                return n;
        }
        return 1;
    }
}
