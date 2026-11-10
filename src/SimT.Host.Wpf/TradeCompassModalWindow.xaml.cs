using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using SimT.Contracts.Data;
using SimT.Contracts.Playing;
using SimT.Contracts.Scenes;
using SimT.Contracts.Time;
using SimT.Core.Kernel;
using SimT.Host.Wpf.Controls;

namespace SimT.Host.Wpf;

public sealed partial class TradeCompassModalWindow : HubModalWindowBase
{
    private readonly SimKernel _kernel;
    private readonly IWorldMap? _map;
    private readonly IMarketTrade? _trade;
    private readonly IItemCatalog? _catalog;
    private readonly IEconomicWarfare? _warfare;
    private readonly List<ItemDef> _allItems = new();
    private string _activeCategoryFilter = "ALL";
    private ItemDef? _selectedItem;

    public TradeCompassModalWindow(SimKernel kernel)
    {
        InitializeComponent();
        _kernel = kernel;

        _kernel.Capabilities.TryGet<IWorldMap>(CapabilityIds.WorldMap, out _map);
        _kernel.Capabilities.TryGet<IMarketTrade>(CapabilityIds.MarketTrade, out _trade);
        _kernel.Capabilities.TryGet<IItemCatalog>(CapabilityIds.ItemCatalog, out _catalog);
        _kernel.Capabilities.TryGet<IEconomicWarfare>(CapabilityIds.EconomicWarfare, out _warfare);

        if (_catalog is not null)
        {
            _allItems.AddRange(_catalog.All.OrderBy(i => i.Id));
        }

        ConfigureHeader();
        PopulateCommoditiesList();

        if (_allItems.Count > 0)
        {
            SelectItem(_allItems[0]);
        }

        KeyDown += OnKeyDownHandler;
    }

    private void OnKeyDownHandler(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void ConfigureHeader()
    {
        var (currentCityName, currentRegion) = ("Lạc Dương", "Trung Nguyên");
        if (_kernel.Capabilities.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var player) && player is not null && _map is not null)
        {
            var cit = _map.Find(player.CurrentCityId);
            if (cit is not null)
            {
                currentCityName = cit.NameVi;
                currentRegion = RoleTitleVi.FormatRegion(cit.Region);
            }
        }

        HubHeader.Configure(
            glyph: "🧭",
            category: "HỆ THỐNG ĐỊNH VỊ THƯƠNG NGHIỆP",
            title: "Kinh Thương La Bàn · Bản Đồ Nhiệt Giá Cả 20 Thành",
            subtitle: $"Vị trí hiện tại: {currentCityName} ({currentRegion}) · Chu kỳ giá: {_kernel.Today.Day}/30 Tháng {_kernel.Today.Month}",
            rightBadgeTitle: "★ CHIẾN LƯỢC BUÔN",
            rightBadgeSub: "● Chênh lệch giá & Tuyến buôn vàng",
            accentColor: Color.FromRgb(0xC4, 0xA0, 0x62));
    }

    private void PopulateCommoditiesList()
    {
        ItemsListPanel.Children.Clear();
        if (_trade is null || _map is null) return;

        var citadels = _map.Citadels;
        var filtered = _activeCategoryFilter switch
        {
            "FOOD" => _allItems.Where(i => i.Code is "gao" or "lua_mi" or "ca_kho" or "thit").ToList(),
            "MATERIAL" => _allItems.Where(i => i.Code is "go" or "than_da" or "dong" or "sat" or "muoi").ToList(),
            "LUXURY" => _allItems.Where(i => i.Code is "tra" or "gam" or "lua" or "ruou" or "sam" or "tram_huong" or "ngoc_bich").ToList(),
            _ => _allItems
        };

        foreach (var item in filtered)
        {
            long minP = long.MaxValue;
            long maxP = 0;
            foreach (var c in citadels)
            {
                var price = _trade.QuoteBuy(c.Id, item.Id);
                if (price < minP) minP = price;
                if (price > maxP) maxP = price;
            }

            var margin = maxP - minP;
            var roi = minP > 0 ? (margin * 100 / minP) : 0;

            var card = new Border
            {
                Background = (Brush)FindResource("SurfaceAlt"),
                BorderBrush = (Brush)FindResource("Line"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 6),
                Cursor = Cursors.Hand
            };

            var dock = new DockPanel();

            var iconBorder = new Border
            {
                Width = 32,
                Height = 32,
                Background = (Brush)FindResource("Bg"),
                BorderBrush = (Brush)FindResource("Line"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            iconBorder.Child = new TextBlock
            {
                Text = ResolveItemIcon(item.Code),
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(iconBorder, Dock.Left);
            dock.Children.Add(iconBorder);

            var roiBadge = new Border
            {
                Background = roi >= 80 ? (Brush)FindResource("Gain") : (Brush)FindResource("Surface"),
                BorderBrush = (Brush)FindResource("Line"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                VerticalAlignment = VerticalAlignment.Center
            };
            roiBadge.Child = new TextBlock
            {
                Text = $"+{roi}%",
                FontSize = 10.5,
                FontWeight = FontWeights.Bold,
                Foreground = roi >= 80 ? Brushes.White : (Brush)FindResource("Accent")
            };
            DockPanel.SetDock(roiBadge, Dock.Right);
            dock.Children.Add(roiBadge);

            var isCornered = _warfare?.ActiveCorneredMarkets.Any(c => c.ItemId == item.Id) == true;
            var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            info.Children.Add(new TextBlock
            {
                Text = isCornered ? $"⚔️ {item.NameVi} [Độc Quyền]" : item.NameVi,
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource(isCornered ? "Accent" : "Text")
            });
            info.Children.Add(new TextBlock
            {
                Text = $"Giá: {minP} ➔ {maxP} Quan",
                Style = (Style)FindResource("Caption"),
                Foreground = (Brush)FindResource("Muted")
            });
            dock.Children.Add(info);

            card.Child = dock;
            card.MouseEnter += (_, _) => card.BorderBrush = (Brush)FindResource("Accent");
            card.MouseLeave += (_, _) => card.BorderBrush = (Brush)FindResource("Line");
            card.MouseLeftButtonDown += (_, _) => SelectItem(item);

            ItemsListPanel.Children.Add(card);
        }
    }

    private void SelectItem(ItemDef item)
    {
        _selectedItem = item;
        if (_trade is null || _map is null) return;

        SelectedItemIcon.Text = ResolveItemIcon(item.Code);
        SelectedItemTitle.Text = item.NameVi;
        SelectedItemCategoryText.Text = ResolveCategoryTitle(item.Category);
        SelectedItemDescription.Text = $"Giá cơ sở: {item.BasePrice} Quan · Hạn lưu kho: {item.ShelfLifeDays} ngày · Khối lượng: {item.Weight} cân/đơn vị";

        var citadels = _map.Citadels;
        var cityPrices = new List<(CitadelDef City, long Price, int Stock)>(citadels.Count);

        foreach (var c in citadels)
        {
            var p = _trade.QuoteBuy(c.Id, item.Id);
            var s = _trade.StallStock(c.Id, item.Id);
            cityPrices.Add((c, p, s));
        }

        var sortedByPrice = cityPrices.OrderBy(cp => cp.Price).ToList();
        var cheapest = sortedByPrice.First();
        var mostExpensive = sortedByPrice.Last();

        CheapestCityText.Text = $"{cheapest.City.NameVi} ({RoleTitleVi.FormatRegion(cheapest.City.Region)})";
        MinPriceText.Text = $"{cheapest.Price:N0} Quan";

        MostExpensiveCityText.Text = $"{mostExpensive.City.NameVi} ({RoleTitleVi.FormatRegion(mostExpensive.City.Region)})";
        MaxPriceText.Text = $"{mostExpensive.Price:N0} Quan";

        var margin = mostExpensive.Price - cheapest.Price;
        var roi = cheapest.Price > 0 ? (margin * 100 / cheapest.Price) : 0;

        GrossMarginText.Text = $"+{margin:N0} Quan/đv";
        RoiText.Text = $"+{roi}%";

        RenderTopRoutes(item, cityPrices);
        RenderHeatmap(item, cityPrices, cheapest.Price, mostExpensive.Price);
    }

    private void RenderTopRoutes(ItemDef item, List<(CitadelDef City, long Price, int Stock)> cityPrices)
    {
        RoutesPanel.Children.Clear();
        if (_map is null) return;

        // Calculate potential arbitrage pairs
        var routes = new List<(CitadelDef From, CitadelDef To, long Margin, int Days, long ProfitTrip)>();

        foreach (var buyCity in cityPrices)
        {
            foreach (var sellCity in cityPrices)
            {
                if (buyCity.City.Id == sellCity.City.Id) continue;
                var margin = sellCity.Price - buyCity.Price;
                if (margin <= 0) continue;

                var days = _map.TravelDays(buyCity.City.Id, sellCity.City.Id);
                if (days <= 0) continue;

                var profitTrip = margin * 40; // 40 units per standard pack caravan
                routes.Add((buyCity.City, sellCity.City, margin, days, profitTrip));
            }
        }

        var topRoutes = routes
            .OrderByDescending(r => r.ProfitTrip / Math.Max(1, r.Days))
            .Take(3)
            .ToList();

        var smuggleOpps = _warfare?.AvailableSmuggleOpportunities
            .Where(o => o.ItemId == item.Id)
            .ToList() ?? [];

        foreach (var opp in smuggleOpps)
        {
            var smuggleRow = new Border
            {
                Background = (Brush)FindResource("SurfaceAlt"),
                BorderBrush = (Brush)FindResource("Accent"),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 0, 0, 6)
            };
            var sDock = new DockPanel();
            var sRight = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(sRight, Dock.Right);
            sRight.Children.Add(new TextBlock
            {
                Text = $"+{opp.ProfitMarginPercent}% (Chợ Đen)",
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                Foreground = (Brush)FindResource("Gain"),
                HorizontalAlignment = HorizontalAlignment.Right
            });
            sRight.Children.Add(new TextBlock
            {
                Text = $"Rủi ro tuần binh: {opp.InterceptionRiskPercent}%",
                Style = (Style)FindResource("Caption"),
                Foreground = (Brush)FindResource("Loss"),
                HorizontalAlignment = HorizontalAlignment.Right
            });
            sDock.Children.Add(sRight);

            var sLeft = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            sLeft.Children.Add(new TextBlock
            {
                Text = $"🔥 [BUÔN LẬU TIỀN TUYẾN] {opp.OriginCitadelNameVi}  ➔  🏰 {opp.DestCitadelNameVi}",
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("Accent"),
                FontSize = 13
            });
            sLeft.Children.Add(new TextBlock
            {
                Text = $"Giá nhập {opp.SmuggleBuyPrice}q ➔ Bán lậu {opp.SmuggleSellPrice}q · Vượt phong tỏa chiến sự",
                Style = (Style)FindResource("Caption"),
                Foreground = (Brush)FindResource("Muted"),
                Margin = new Thickness(0, 2, 0, 0)
            });
            sDock.Children.Add(sLeft);
            smuggleRow.Child = sDock;
            RoutesPanel.Children.Add(smuggleRow);
        }

        if (topRoutes.Count == 0 && smuggleOpps.Count == 0)
        {
            RoutesPanel.Children.Add(new TextBlock
            {
                Text = "Hiện không có tuyến buôn sinh lời đáng kể cho mặt hàng này.",
                Style = (Style)FindResource("Caption"),
                Foreground = (Brush)FindResource("Muted")
            });
            return;
        }

        var rank = 1;
        foreach (var route in topRoutes)
        {
            var row = new Border
            {
                Background = (Brush)FindResource("SurfaceAlt"),
                BorderBrush = (Brush)FindResource("Line"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 0, 0, 6)
            };

            var dock = new DockPanel();

            // Right: Profit summary pill
            var profitStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(profitStack, Dock.Right);
            profitStack.Children.Add(new TextBlock
            {
                Text = $"+{route.ProfitTrip:N0} Quan/chuyến",
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                Foreground = (Brush)FindResource("Gain"),
                HorizontalAlignment = HorizontalAlignment.Right
            });
            profitStack.Children.Add(new TextBlock
            {
                Text = $"+{route.Margin:N0} Quan/đv (40 đơn vị)",
                Style = (Style)FindResource("Caption"),
                Foreground = (Brush)FindResource("Muted"),
                HorizontalAlignment = HorizontalAlignment.Right
            });
            dock.Children.Add(profitStack);

            // Left: Route path & travel days
            var leftStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var pathRow = new StackPanel { Orientation = Orientation.Horizontal };
            pathRow.Children.Add(new TextBlock
            {
                Text = $"#{rank} {route.From.NameVi}",
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("Ink"),
                FontSize = 13
            });
            pathRow.Children.Add(new TextBlock
            {
                Text = "  ➔  ",
                Foreground = (Brush)FindResource("Accent"),
                FontWeight = FontWeights.Bold
            });
            pathRow.Children.Add(new TextBlock
            {
                Text = route.To.NameVi,
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("Ink"),
                FontSize = 13
            });
            leftStack.Children.Add(pathRow);

            var subRow = new TextBlock
            {
                Text = $"Lộ trình: {route.Days} ngày đường · {RoleTitleVi.FormatRegion(route.From.Region)} đến {RoleTitleVi.FormatRegion(route.To.Region)}",
                Style = (Style)FindResource("Caption"),
                Foreground = (Brush)FindResource("Muted"),
                Margin = new Thickness(0, 2, 0, 0)
            };
            leftStack.Children.Add(subRow);

            dock.Children.Add(leftStack);
            row.Child = dock;
            RoutesPanel.Children.Add(row);
            rank++;
        }
    }

    private void RenderHeatmap(ItemDef item, List<(CitadelDef City, long Price, int Stock)> cityPrices, long minPrice, long maxPrice)
    {
        HeatmapPanel.Children.Clear();

        var regions = cityPrices
            .GroupBy(cp => cp.City.Region)
            .OrderBy(g => g.Key);

        foreach (var regGroup in regions)
        {
            var regionTitle = RoleTitleVi.FormatRegion(regGroup.Key);
            var section = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };

            section.Children.Add(new TextBlock
            {
                Text = $"● {regionTitle.ToUpperInvariant()}",
                Style = (Style)FindResource("Overline"),
                Foreground = (Brush)FindResource("Faint"),
                Margin = new Thickness(0, 0, 0, 6)
            });

            var grid = new UniformGrid { Columns = 4 };
            foreach (var (city, price, stock) in regGroup)
            {
                var isMin = price == minPrice;
                var isMax = price == maxPrice;

                var card = new Border
                {
                    Background = isMin ? (Brush)FindResource("SurfaceAlt") : (isMax ? (Brush)FindResource("SurfaceAlt") : (Brush)FindResource("Bg")),
                    BorderBrush = isMin ? (Brush)FindResource("Gain") : (isMax ? (Brush)FindResource("Loss") : (Brush)FindResource("Line")),
                    BorderThickness = new Thickness(isMin || isMax ? 1.5 : 1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 6, 6, 6),
                    Margin = new Thickness(2, 2, 2, 2)
                };

                var isCorneredCity = _warfare?.ActiveCorneredMarkets.Any(c => c.CitadelId == city.Id && c.ItemId == item.Id) == true;
                var cStack = new StackPanel();
                var headerRow = new DockPanel();
                headerRow.Children.Add(new TextBlock
                {
                    Text = isCorneredCity ? $"⚔️ {city.NameVi} (x2.2)" : city.NameVi,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 11,
                    Foreground = (Brush)FindResource(isCorneredCity ? "Accent" : "Text")
                });
                cStack.Children.Add(headerRow);

                var priceRow = new DockPanel { Margin = new Thickness(0, 2, 0, 0) };
                priceRow.Children.Add(new TextBlock
                {
                    Text = $"{price:N0} Q",
                    FontWeight = FontWeights.Bold,
                    FontSize = 12,
                    Foreground = isMin ? (Brush)FindResource("Gain") : (isMax ? (Brush)FindResource("Loss") : (Brush)FindResource("Accent"))
                });
                DockPanel.SetDock(priceRow.Children[0], Dock.Left);

                var stockBlock = new TextBlock
                {
                    Text = $"Kho:{stock}",
                    Style = (Style)FindResource("Caption"),
                    FontSize = 9.5,
                    Foreground = (Brush)FindResource("Muted"),
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                priceRow.Children.Add(stockBlock);
                cStack.Children.Add(priceRow);

                card.Child = cStack;
                grid.Children.Add(card);
            }

            section.Children.Add(grid);
            HeatmapPanel.Children.Add(section);
        }
    }

    private static string ResolveItemIcon(string code) => code switch
    {
        "gao" or "lua_mi" => "🌾",
        "ca_kho" => "🐟",
        "thit" => "🍖",
        "muoi" => "🧂",
        "tra" => "🍵",
        "ruou" => "🍶",
        "vai" or "lua" or "gam" => "👘",
        "than_da" => "🪵",
        "go" => "🪵",
        "dong" => "🪙",
        "sat" or "cuoc_nong" => "⚔️",
        "thuoc" or "sam" => "🌿",
        "tram_huong" or "ngoc_bich" => "💎",
        _ => "📦"
    };

    private static string ResolveCategoryTitle(string cat) => cat switch
    {
        "food" or "grain" => "Nông Sản & Lương Thực",
        "raw" or "material" => "Khoáng Sản & Vật Liệu",
        "luxury" => "Kỳ Trân & Xa Xỉ Phẩm",
        "medicine" => "Dược Liệu & Y Đạo",
        _ => "Thương Phẩm Khác"
    };

    private void FilterAll_Click(object sender, RoutedEventArgs e)
    {
        _activeCategoryFilter = "ALL";
        PopulateCommoditiesList();
    }

    private void FilterFood_Click(object sender, RoutedEventArgs e)
    {
        _activeCategoryFilter = "FOOD";
        PopulateCommoditiesList();
    }

    private void FilterMaterial_Click(object sender, RoutedEventArgs e)
    {
        _activeCategoryFilter = "MATERIAL";
        PopulateCommoditiesList();
    }

    private void FilterLuxury_Click(object sender, RoutedEventArgs e)
    {
        _activeCategoryFilter = "LUXURY";
        PopulateCommoditiesList();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
