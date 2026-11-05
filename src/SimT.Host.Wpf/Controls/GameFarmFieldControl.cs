using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Host.Wpf.Controls;

/// <summary>
/// Hardware-accelerated Visual Farm Field & Agricultural Estate Control.
/// Replaces dry tabular data with rich visual farm plot tiles, crop growth phase meters,
/// soil fertility gauges, quick seed sowing buttons, and 1-click golden harvest triggers.
/// </summary>
public sealed class GameFarmFieldControl : Border
{
    private readonly ICultivationEstate _estate;
    private readonly IWorldMap _map;
    private readonly long _currentCityId;
    private readonly IGameClock _clock;

    public event Action<long>? RentRequested;
    public event Action<long, string>? PlantRequested;
    public event Action<long>? HarvestRequested;
    public event Action<long>? FertilizeRequested;
    public event Action<string, int>? BuySeedRequested;
    public event Action<int>? BuyFertilizerRequested;

    public GameFarmFieldControl(
        ICultivationEstate estate,
        IWorldMap map,
        long currentCityId,
        IGameClock clock)
    {
        _estate = estate;
        _map = map;
        _currentCityId = currentCityId;
        _clock = clock;

        Background = (Brush)Application.Current.FindResource("Surface");
        BorderBrush = (Brush)Application.Current.FindResource("Line");
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(8);
        Padding = new Thickness(16, 14, 16, 14);
        Margin = new Thickness(0, 0, 0, 16);

        var currentCitadel = map.Find(currentCityId);
        var currentCityName = currentCitadel?.NameVi ?? "Châu Quận";

        var currentPlots = estate.Plots.Where(p => p.CitadelId == currentCityId).ToList();
        var supply = estate.GetSupplyInventory(currentCityId);

        var mainStack = new StackPanel();

        // 1. Header: Farm Field Title & City Summary
        var headerDock = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };

        var statsPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        var readyPlots = currentPlots.Count(p => p.Rented && p.CropStatus is not null && p.ReadyDayIndex.HasValue && clock.Today.DayIndex >= p.ReadyDayIndex.Value);
        if (readyPlots > 0)
        {
            var harvestAllBtn = new Button
            {
                Content = $"🌾 Gặt Tất Cả ({readyPlots} Mẫu Chín)",
                Style = (Style)Application.Current.FindResource("PrimaryButton"),
                Padding = new Thickness(12, 5, 12, 5),
                Margin = new Thickness(0, 0, 8, 0),
                FontWeight = FontWeights.Bold
            };
            harvestAllBtn.Click += (_, _) =>
            {
                foreach (var p in currentPlots.Where(p => p.Rented && p.CropStatus is not null && p.ReadyDayIndex.HasValue && clock.Today.DayIndex >= p.ReadyDayIndex.Value))
                {
                    HarvestRequested?.Invoke(p.Id);
                }
            };
            statsPanel.Children.Add(harvestAllBtn);
        }

        var buySeedBtn = new Button
        {
            Content = "🌾 Mua Hạt Giống (5 Lúa)",
            Style = (Style)Application.Current.FindResource("GhostButton"),
            Padding = new Thickness(10, 5, 10, 5),
            Margin = new Thickness(0, 0, 6, 0),
            ToolTip = "Mua thêm 5 túi hạt giống lúa tại sạp nông tư với giá ưu đãi"
        };
        buySeedBtn.Click += (_, _) => BuySeedRequested?.Invoke("gao", 5);
        statsPanel.Children.Add(buySeedBtn);

        var buyFertBtn = new Button
        {
            Content = "🧪 Mua Phân Bón",
            Style = (Style)Application.Current.FindResource("GhostButton"),
            Padding = new Thickness(10, 5, 10, 5),
            ToolTip = "Mua 1 thùng phân bón hữu cơ phục hồi 20 điểm phì nhiêu đất"
        };
        buyFertBtn.Click += (_, _) => BuyFertilizerRequested?.Invoke(1);
        statsPanel.Children.Add(buyFertBtn);

        DockPanel.SetDock(statsPanel, Dock.Right);
        headerDock.Children.Add(statsPanel);

        var titleStack = new StackPanel();
        titleStack.Children.Add(new TextBlock
        {
            Text = "🌾 ĐIỀN TRANG NÔNG TANG · SA BÀN CANH TÁC THÀNH TRÌ",
            Style = (Style)Application.Current.FindResource("Overline")
        });
        titleStack.Children.Add(new TextBlock
        {
            Text = $"Khu Vực Canh Tác: {currentCityName} ({currentPlots.Count} Thửa Ruộng Thuộc Địa Giới)",
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)Application.Current.FindResource("Text"),
            Margin = new Thickness(0, 2, 0, 0)
        });
        headerDock.Children.Add(titleStack);
        mainStack.Children.Add(headerDock);

        // 2. Supply Bar: Seeds, Fertilizer, Tools
        var supplyBar = new Border
        {
            Background = (Brush)Application.Current.FindResource("Rail"),
            BorderBrush = (Brush)Application.Current.FindResource("Line"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 7, 12, 7),
            Margin = new Thickness(0, 0, 0, 12)
        };
        var supplyDock = new DockPanel();

        var toolStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        toolStack.Children.Add(CreatePill($"🛠️ Nông Cụ: {(supply.HasHoe ? "Cuốc Sắt ✓" : "Chưa có cuốc")}, {(supply.HasWateringScoop ? "Gàu Tát Nước ✓" : "Thiếu gàu")}",
            supply.HasHoe ? Color.FromRgb(0x5A, 0xC0, 0x8A) : Color.FromRgb(0x8A, 0x97, 0xA8)));
        DockPanel.SetDock(toolStack, Dock.Right);
        supplyDock.Children.Add(toolStack);

        var seedStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        seedStack.Children.Add(CreatePill($"🌾 Hạt Lúa: {supply.RiceSeeds}", supply.RiceSeeds > 0 ? Color.FromRgb(0xF5, 0xD0, 0x61) : Color.FromRgb(0x75, 0x86, 0x9C)));
        seedStack.Children.Add(CreatePill($"🍃 Hạt Trà: {supply.TeaSeeds}", supply.TeaSeeds > 0 ? Color.FromRgb(0x27, 0xAE, 0x60) : Color.FromRgb(0x75, 0x86, 0x9C)));
        seedStack.Children.Add(CreatePill($"🐛 Dâu Tằm: {supply.MulberrySeedlings}", supply.MulberrySeedlings > 0 ? Color.FromRgb(0x29, 0x80, 0xB9) : Color.FromRgb(0x75, 0x86, 0x9C)));
        seedStack.Children.Add(CreatePill($"🧪 Phân Bón: {supply.FertilizerCount}", supply.FertilizerCount > 0 ? Color.FromRgb(0x16, 0xA0, 0x85) : Color.FromRgb(0x75, 0x86, 0x9C)));
        supplyDock.Children.Add(seedStack);

        supplyBar.Child = supplyDock;
        mainStack.Children.Add(supplyBar);

        // 3. Grid of Farmland Plots (Tiles)
        var plotsPanel = new WrapPanel { Orientation = Orientation.Horizontal };

        foreach (var plot in currentPlots)
        {
            var plotCard = BuildPlotCard(plot, clock.Today.DayIndex, supply);
            plotsPanel.Children.Add(plotCard);
        }

        if (currentPlots.Count == 0)
        {
            var emptyText = new TextBlock
            {
                Text = "Thành trấn này không có quỹ đất canh tác thuộc sở hữu triều đình.",
                Style = (Style)Application.Current.FindResource("Caption"),
                Foreground = (Brush)Application.Current.FindResource("Faint"),
                Margin = new Thickness(0, 10, 0, 10)
            };
            mainStack.Children.Add(emptyText);
        }
        else
        {
            mainStack.Children.Add(plotsPanel);
        }

        Child = mainStack;
    }

    private Border BuildPlotCard(PlotView plot, long currentDay, FarmSupplyInventory supply)
    {
        var card = new Border
        {
            BorderBrush = (Brush)Application.Current.FindResource("Line"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 0, 10, 10),
            Width = 270,
            MinHeight = 175
        };

        var stack = new StackPanel();

        // 1. Top row: Plot Id & Area & Rent
        var topDock = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
        var areaPill = new Border
        {
            Background = (Brush)Application.Current.FindResource("AccentSoft"),
            BorderBrush = (Brush)Application.Current.FindResource("Line"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(5, 1, 5, 1)
        };
        areaPill.Child = new TextBlock
        {
            Text = $"{plot.AreaMu} Mẫu Đất",
            FontSize = 10.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.FindResource("Accent")
        };
        DockPanel.SetDock(areaPill, Dock.Right);
        topDock.Children.Add(areaPill);

        var plotTitle = new TextBlock
        {
            Text = $"Thửa Đất #{plot.Id}",
            FontSize = 13.5,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)Application.Current.FindResource("Text")
        };
        topDock.Children.Add(plotTitle);
        stack.Children.Add(topDock);

        // 2. Soil & Irrigation Bar
        var soilLine = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
        var soilVal = new TextBlock
        {
            Text = $"Thổ Nhưỡng: {plot.Soil}%",
            FontSize = 10.5,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)(plot.Soil > 60 ? Application.Current.FindResource("Gain") : plot.Soil > 30 ? Application.Current.FindResource("Accent") : Application.Current.FindResource("Loss"))
        };
        DockPanel.SetDock(soilVal, Dock.Right);
        soilLine.Children.Add(soilVal);

        var irrText = new TextBlock
        {
            Text = $"💧 Thủy Lợi: Cấp {plot.IrrigationLevel}",
            Style = (Style)Application.Current.FindResource("Caption"),
            FontSize = 10.5,
            Foreground = (Brush)Application.Current.FindResource("Faint")
        };
        soilLine.Children.Add(irrText);
        stack.Children.Add(soilLine);

        var soilGauge = new GameGaugeBar
        {
            Height = 4,
            Maximum = 100,
            Value = plot.Soil,
            BarBrush = (Brush)(plot.Soil > 60 ? Application.Current.FindResource("Gain") : plot.Soil > 30 ? Application.Current.FindResource("Accent") : Application.Current.FindResource("Loss")),
            Margin = new Thickness(0, 0, 0, 8)
        };
        stack.Children.Add(soilGauge);

        // 3. State Presentation
        if (!plot.Rented)
        {
            // UNRENTED
            card.Background = (Brush)Application.Current.FindResource("Rail");

            var stateText = new TextBlock
            {
                Text = $"Đất công triều đình cho thuê: {plot.MonthlyRent} Quan/tháng.",
                Style = (Style)Application.Current.FindResource("Caption"),
                Foreground = (Brush)Application.Current.FindResource("Muted"),
                Margin = new Thickness(0, 4, 0, 8)
            };
            stack.Children.Add(stateText);

            var rentBtn = new Button
            {
                Content = $"✦ Thuê Thửa #{plot.Id} ({plot.MonthlyRent} Quan)",
                Style = (Style)Application.Current.FindResource("PrimaryButton"),
                Padding = new Thickness(10, 6, 10, 6),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            rentBtn.Click += (_, _) => RentRequested?.Invoke(plot.Id);
            stack.Children.Add(rentBtn);
        }
        else if (plot.CropStatus is null)
        {
            // RENTED & EMPTY (Ready to plant)
            card.Background = new SolidColorBrush(Color.FromRgb(0x13, 0x1B, 0x22));

            var readyLabel = new TextBlock
            {
                Text = "🌱 Đất cày tơi xốp, sẵn sàng gieo hạt giống:",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.FindResource("Gain"),
                Margin = new Thickness(0, 2, 0, 6)
            };
            stack.Children.Add(readyLabel);

            var plantGrid = new UniformGrid { Columns = 3, Margin = new Thickness(0, 0, 0, 4) };

            var riceBtn = new Button
            {
                Content = "🌾 Lúa",
                Style = (Style)Application.Current.FindResource("GhostButton"),
                Padding = new Thickness(4, 4, 4, 4),
                Margin = new Thickness(0, 0, 3, 0),
                IsEnabled = supply.RiceSeeds > 0,
                ToolTip = supply.RiceSeeds > 0 ? "Gieo hạt giống lúa nước" : "Hết hạt giống lúa (hãy mua thêm bên trên)"
            };
            riceBtn.Click += (_, _) => PlantRequested?.Invoke(plot.Id, "gao");
            plantGrid.Children.Add(riceBtn);

            var teaBtn = new Button
            {
                Content = "🍃 Trà",
                Style = (Style)Application.Current.FindResource("GhostButton"),
                Padding = new Thickness(4, 4, 4, 4),
                Margin = new Thickness(2, 0, 2, 0),
                IsEnabled = supply.TeaSeeds > 0,
                ToolTip = supply.TeaSeeds > 0 ? "Gieo hạt giống chè búp" : "Hết hạt giống chè"
            };
            teaBtn.Click += (_, _) => PlantRequested?.Invoke(plot.Id, "tra");
            plantGrid.Children.Add(teaBtn);

            var mulBtn = new Button
            {
                Content = "🐛 Dâu",
                Style = (Style)Application.Current.FindResource("GhostButton"),
                Padding = new Thickness(4, 4, 4, 4),
                Margin = new Thickness(3, 0, 0, 0),
                IsEnabled = supply.MulberrySeedlings > 0,
                ToolTip = supply.MulberrySeedlings > 0 ? "Trồng dâu tằm nuôi tằm dệt lụa" : "Hết cây giống dâu tằm"
            };
            mulBtn.Click += (_, _) => PlantRequested?.Invoke(plot.Id, "dau_tam");
            plantGrid.Children.Add(mulBtn);
            stack.Children.Add(plantGrid);

            if (plot.Soil < 80 && supply.FertilizerCount > 0)
            {
                var fertBtn = new Button
                {
                    Content = "🧪 Bón Phân (+20 Thổ Nhưỡng)",
                    Style = (Style)Application.Current.FindResource("GhostButton"),
                    Padding = new Thickness(6, 3, 6, 3),
                    Margin = new Thickness(0, 4, 0, 0),
                    FontSize = 10.5
                };
                fertBtn.Click += (_, _) => FertilizeRequested?.Invoke(plot.Id);
                stack.Children.Add(fertBtn);
            }
        }
        else
        {
            // GROWING OR READY
            var isReady = plot.ReadyDayIndex.HasValue && currentDay >= plot.ReadyDayIndex.Value;
            var (cropGlyph, cropName) = plot.CropStatus switch
            {
                "gao" => ("🌾", "Lúa Gạo Nước"),
                "tra" => ("🍃", "Chè San Tuyết"),
                "dau_tam" => ("🐛", "Dâu Tằm Tơ Lụa"),
                _ => ("🌿", "Hoa Màu Điền Trang")
            };

            if (isReady)
            {
                // RIPE - READY TO HARVEST
                card.Background = new SolidColorBrush(Color.FromRgb(0x23, 0x1E, 0x12));
                card.BorderBrush = (Brush)Application.Current.FindResource("Accent");

                var ripeTitle = new TextBlock
                {
                    Text = $"{cropGlyph} {cropName} ĐÃ CHÍN VÀNG!",
                    FontSize = 11.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = (Brush)Application.Current.FindResource("Accent"),
                    Margin = new Thickness(0, 2, 0, 3)
                };
                stack.Children.Add(ripeTitle);

                var yieldText = new TextBlock
                {
                    Text = $"Sản lượng thu hoạch: +{plot.YieldQuantity ?? 15} phần nông sản",
                    Style = (Style)Application.Current.FindResource("Caption"),
                    Foreground = (Brush)Application.Current.FindResource("Text"),
                    Margin = new Thickness(0, 0, 0, 8)
                };
                stack.Children.Add(yieldText);

                var harvestBtn = new Button
                {
                    Content = $"🌾 GẶT HÁI NGAY (+{plot.YieldQuantity ?? 15})",
                    Style = (Style)Application.Current.FindResource("PrimaryButton"),
                    Padding = new Thickness(10, 7, 10, 7),
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                harvestBtn.Click += (_, _) => HarvestRequested?.Invoke(plot.Id);
                stack.Children.Add(harvestBtn);
            }
            else
            {
                // GROWING
                card.Background = new SolidColorBrush(Color.FromRgb(0x11, 0x1A, 0x15));

                var daysLeft = plot.ReadyDayIndex.HasValue ? Math.Max(1, plot.ReadyDayIndex.Value - currentDay) : 1;
                var growingTitle = new TextBlock
                {
                    Text = $"{cropGlyph} {cropName} đang lớn",
                    FontSize = 11.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (Brush)Application.Current.FindResource("Gain"),
                    Margin = new Thickness(0, 2, 0, 3)
                };
                stack.Children.Add(growingTitle);

                var daysText = new TextBlock
                {
                    Text = $"Còn khoảng {daysLeft} ngày là đến mùa gặt (Dự thu: ~{plot.YieldQuantity ?? 15})",
                    Style = (Style)Application.Current.FindResource("Caption"),
                    Foreground = (Brush)Application.Current.FindResource("Muted"),
                    Margin = new Thickness(0, 0, 0, 6)
                };
                stack.Children.Add(daysText);

                if (plot.Soil < 70 && supply.FertilizerCount > 0)
                {
                    var fertBtn = new Button
                    {
                        Content = "🧪 Bón Phân Dưỡng Cây (+20)",
                        Style = (Style)Application.Current.FindResource("GhostButton"),
                        Padding = new Thickness(6, 3, 6, 3),
                        FontSize = 10.5
                    };
                    fertBtn.Click += (_, _) => FertilizeRequested?.Invoke(plot.Id);
                    stack.Children.Add(fertBtn);
                }
            }
        }

        card.Child = stack;
        return card;
    }

    private static Border CreatePill(string label, Color color)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x28, color.R, color.G, color.B)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, color.R, color.G, color.B)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(6, 2, 6, 2),
            Margin = new Thickness(0, 0, 6, 0)
        };
        border.Child = new TextBlock
        {
            Text = label,
            FontSize = 10.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(color)
        };
        return border;
    }
}
