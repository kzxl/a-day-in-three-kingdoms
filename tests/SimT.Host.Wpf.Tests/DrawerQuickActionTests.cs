using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SimT.Contracts.Ui;
using SimT.Host.Wpf.Controls;

namespace SimT.Host.Wpf.Tests;

public sealed class DrawerQuickActionTests
{
    [Fact]
    public void BazaarDeck_init_and_item_icon_click_update_deal_tray_without_opening_drawer()
    {
        WpfTestContext.RunOnSta(() =>
        {
            var rows = new List<ScreenRow>
            {
                new(
                    ["Gạo Trắng (Rice)", "12", "9", "45", "đủ", "5"],
                    [
                        new ScreenCommand("buy:1", "Mua 1"),
                        new ScreenCommand("buy:1", "Mua…", RequiresQuantity: true)
                    ],
                    Title: "Gạo Trắng"),
                new(
                    ["Tơ Lụa (Silk)", "85", "68", "12", "khan", "2"],
                    [
                        new ScreenCommand("buy:2", "Mua 1"),
                        new ScreenCommand("buy:2", "Mua…", RequiresQuantity: true)
                    ],
                    Title: "Tơ Lụa")
            };
            var section = new ScreenSection(
                "🌾 Lương Thực & Nhu Yếu",
                ["Vật phẩm", "Giá mua", "Giá bán", "Tồn sạp", "Mức cung", "Trong túi"],
                rows,
                SectionViewMode.BazaarTradingDeck);

            var inspectorCalls = 0;
            var control = new GameBazaarDeckControl(
                section,
                _ => { },
                (_, _) => inspectorCalls++);

            // 1. Switching to screen / constructing control must NOT auto-open drawer
            Assert.Equal(0, inspectorCalls);

            // 2. Selecting/clicking second item icon updates quick-action Deal Tray without opening drawer
            var selectMethod = typeof(GameBazaarDeckControl).GetMethod(
                "SelectItem",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(selectMethod);
            selectMethod.Invoke(control, [rows[1]]);

            var selectedRowField = typeof(GameBazaarDeckControl).GetField(
                "_selectedRow",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(selectedRowField);
            Assert.Same(rows[1], selectedRowField.GetValue(control));
            Assert.Equal(0, inspectorCalls);

            // 3. Warehouse mode in BazaarDeck wires quantity store/take directly in Deal Tray without opening drawer
            var whRows = new List<ScreenRow>
            {
                new(
                    ["Lúa Mạch", "30", "2", "90", "10"],
                    [
                        new ScreenCommand("store:5", "Gửi 1"),
                        new ScreenCommand("take:5", "Lấy 1"),
                        new ScreenCommand("store:5", "Gửi…", RequiresQuantity: true),
                        new ScreenCommand("take:5", "Lấy…", RequiresQuantity: true)
                    ],
                    Title: "Lúa Mạch")
            };
            var whSection = new ScreenSection(
                "Tồn Kho Thương Điếm",
                ["Vật phẩm", "Trong kho", "Tuổi", "Hạn", "Trong túi"],
                whRows,
                SectionViewMode.BazaarTradingDeck);

            var whInspectorCalls = 0;
            string? lastCommand = null;
            var whControl = new GameBazaarDeckControl(
                whSection,
                cmd => lastCommand = cmd,
                (_, _) => whInspectorCalls++);

            Assert.Equal(0, whInspectorCalls);
            Assert.Null(lastCommand);
            Assert.NotNull(whControl);
        });
    }

    [Fact]
    public void CaravanDispatch_init_and_route_selection_do_not_open_drawer()
    {
        WpfTestContext.RunOnSta(() =>
        {
            var rows = new List<ScreenRow>
            {
                new(
                    ["Trường An", "4", "15", "20"],
                    [new ScreenCommand("send_escort:1", "Khởi Hành")],
                    Title: "Trường An"),
                new(
                    ["Hứa Xương", "3", "10", "20"],
                    [new ScreenCommand("send_escort:2", "Khởi Hành")],
                    Title: "Hứa Xương")
            };
            var section = new ScreenSection(
                "Lộ Trình Vận Tải",
                ["Thành đích", "Ngày đường", "Cước phí", "Hàng trong túi"],
                rows,
                SectionViewMode.CaravanDispatchBoard);

            var inspectorCalls = 0;
            _ = new GameCaravanDispatchControl(
                section,
                _ => { },
                (_, _) => inspectorCalls++);

            Assert.Equal(0, inspectorCalls);
        });
    }

    [Fact]
    public void ItemCodex_and_ArtisanWorkbench_selection_update_quick_panels_without_opening_drawer()
    {
        WpfTestContext.RunOnSta(() =>
        {
            var codexRows = new List<ScreenRow>
            {
                new(["📦", "Gạo Trắng", "Lương thực", "12 quan", "90 ngày", "1 cân", "5"], [new ScreenCommand("item:modal:1", "Kỳ Vật")], Title: "Gạo Trắng"),
                new(["⚔️", "Thanh Long Đao", "Thần binh", "500 quan", "Bền", "82 cân", "1"], [new ScreenCommand("item:modal:2", "Kỳ Vật")], Title: "Thanh Long Đao")
            };
            var codexSection = new ScreenSection(
                "Bách Vật Chí",
                ["Icon", "Tên", "Loại", "Giá", "Hạn", "Nặng", "Túi"],
                codexRows,
                SectionViewMode.ItemCodexShowcase);

            var codexInspectCalls = 0;
            var codex = new GameItemCodexControl(codexSection, _ => { }, (_, _) => codexInspectCalls++);
            var shelfWrapField = typeof(GameItemCodexControl).GetField("_shelfWrap", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(shelfWrapField);
            var shelfWrap = shelfWrapField.GetValue(codex) as WrapPanel;
            Assert.NotNull(shelfWrap);
            Assert.Equal(2, shelfWrap.Children.Count);

            var secondCard = shelfWrap.Children[1] as UIElement;
            Assert.NotNull(secondCard);
            secondCard.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
            {
                RoutedEvent = UIElement.MouseLeftButtonUpEvent
            });

            Assert.Equal(0, codexInspectCalls);

            var recipeRows = new List<ScreenRow>
            {
                new(["Bản Vẽ Kiếm Sắt", "Bậc 1", "Quặng Sắt ✅", "20 quan", "2 ngày", "Kiếm Sắt"], [new ScreenCommand("craft:1", "Rèn")], Title: "Bản Vẽ Kiếm Sắt"),
                new(["Bản Vẽ Bảo Đao", "Bậc 2", "Thép Tinh Luyện ✅", "50 quan", "4 ngày", "Bảo Đao"], [new ScreenCommand("craft:2", "Rèn")], Title: "Bản Vẽ Bảo Đao")
            };
            var recipeSection = new ScreenSection(
                "Bản Vẽ Chế Tác",
                ["Tên", "Bậc", "Nguyên liệu", "Phí", "Thời gian", "Thành phẩm"],
                recipeRows,
                SectionViewMode.ArtisanWorkbench);

            var workbenchInspectCalls = 0;
            var workbench = new GameArtisanWorkbenchControl(recipeSection, _ => { }, (_, _) => workbenchInspectCalls++);
            var rackBorder = workbench.Children[1] as Border;
            var rackStack = rackBorder?.Child as StackPanel;
            var scroll = rackStack?.Children.OfType<ScrollViewer>().FirstOrDefault();
            var recipeList = scroll?.Content as StackPanel;
            Assert.NotNull(recipeList);
            Assert.Equal(2, recipeList.Children.Count);

            var secondRecipe = recipeList.Children[1] as UIElement;
            Assert.NotNull(secondRecipe);
            secondRecipe.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
            {
                RoutedEvent = UIElement.MouseLeftButtonUpEvent
            });

            Assert.Equal(0, workbenchInspectCalls);
        });
    }

    [Fact]
    public void MainWindow_xaml_does_not_contain_legacy_inspector_drawer()
    {
        var xamlPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "SimT.Host.Wpf", "MainWindow.xaml"));
        Assert.True(File.Exists(xamlPath));
        var xaml = File.ReadAllText(xamlPath);
        Assert.DoesNotContain("InspectorPanel", xaml);
        Assert.DoesNotContain("InspectorScrim", xaml);
    }
}
