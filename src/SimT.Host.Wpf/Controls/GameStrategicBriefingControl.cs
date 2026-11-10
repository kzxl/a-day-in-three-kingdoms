using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using SimT.Contracts.Playing;
using SimT.Contracts.Scenes;
using SimT.Contracts.Time;
using SimT.Core.Kernel;

namespace SimT.Host.Wpf.Controls;

/// <summary>
/// Strategic Briefing Hub (Đại Điện Chiến Lược / Tiêu Điểm Đầu Ngày).
/// Provides a comprehensive world overview, daily contextual imperatives,
/// hot arbitrage trade intelligence, local notable figures, and living world chronicle stream.
/// Eliminates raw ERP spreadsheet feel with rich visual card matrices.
/// </summary>
public sealed partial class GameStrategicBriefingControl : Border
{
    private static readonly Brush SurfaceBg = Freeze(new SolidColorBrush(Color.FromRgb(0x14, 0x10, 0x0C)));
    private static readonly Brush BannerBg = Freeze(new SolidColorBrush(Color.FromRgb(0x1B, 0x14, 0x0E)));
    private static readonly Brush BorderGold = Freeze(new SolidColorBrush(Color.FromRgb(0x5A, 0x46, 0x2A)));
    private static readonly Brush AccentGold = Freeze(new SolidColorBrush(Color.FromRgb(0xC4, 0xA0, 0x62)));
    private static readonly Brush TextPrimary = Freeze(new SolidColorBrush(Color.FromRgb(0xF3, 0xE6, 0xCC)));
    private static readonly Brush TextMuted = Freeze(new SolidColorBrush(Color.FromRgb(0xB9, 0xAA, 0x8C)));
    private static readonly Brush ItemRowBg = Freeze(new SolidColorBrush(Color.FromRgb(0x1F, 0x17, 0x12)));
    private static readonly Brush ItemRowBorder = Freeze(new SolidColorBrush(Color.FromRgb(0x38, 0x29, 0x1D)));
    private static readonly Brush AccentCinnabar = Freeze(new SolidColorBrush(Color.FromRgb(0xA8, 0x32, 0x1F)));

    // Events to dispatch user actions to MainWindow
    public event Action<string>? RequestNavigate;
    public event Action? RequestOpenCityScene;
    public event Action? RequestRest;
    public event Action? RequestStroll;
    public event Action? RequestOpenTradeCompass;
    public event Action<long>? RequestTalkNpc;

    private readonly StackPanel _rootContent;

    public GameStrategicBriefingControl()
    {
        Background = SurfaceBg;
        BorderThickness = new Thickness(0);

        var scroller = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(16, 12, 16, 24)
        };

        _rootContent = new StackPanel();
        scroller.Content = _rootContent;
        Child = scroller;
    }

    /// <summary>
    /// Binds active simulation kernel to render contextual game state.
    /// </summary>
    public void Bind(SimKernel kernel)
    {
        _rootContent.Children.Clear();

        if (!kernel.Capabilities.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var playerState) || playerState is null)
        {
            _rootContent.Children.Add(new TextBlock
            {
                Text = "Chưa thể kết nối trạng thái nhân vật.",
                Foreground = TextMuted,
                Margin = new Thickness(20)
            });
            return;
        }

        var cityId = playerState.CurrentCityId;
        var today = kernel.Today;

        kernel.Capabilities.TryGet<IWorldMap>(CapabilityIds.WorldMap, out var map);
        kernel.Capabilities.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var attrs);
        kernel.Capabilities.TryGet<INpcRegistry>(CapabilityIds.NpcRegistry, out var registry);
        kernel.Capabilities.TryGet<ISocietyChronicle>(CapabilityIds.SocietyChronicle, out var society);
        kernel.Capabilities.TryGet<IMarketTrade>(CapabilityIds.MarketTrade, out var trade);
        kernel.Capabilities.TryGet<IItemCatalog>(CapabilityIds.ItemCatalog, out var catalog);

        var citadel = map?.Find(cityId);
        var ecosystem = map?.GetEcosystem(cityId);

        // 1. Top Citadel Overview Hero Banner
        _rootContent.Children.Add(BuildCitadelHeroBanner(kernel, citadel, ecosystem, today));

        // 1.5 RPG Starter Questline (Hành Trình Tân Thủ - 3 Bước Khởi Đầu)
        var starterCard = BuildStarterQuestCard(kernel, today, playerState);
        if (starterCard is not null)
        {
            _rootContent.Children.Add(starterCard);
        }

        AddWishJournal(kernel);
        AddLordTaskJournal(kernel);

        // 2. Main 2-Column Strategic Arena
        var arenaGrid = new Grid { Margin = new Thickness(0, 14, 0, 0) };
        arenaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.15, GridUnitType.Star) });
        arenaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        arenaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });

        var leftCol = new StackPanel();
        var rightCol = new StackPanel();

        Grid.SetColumn(leftCol, 0);
        Grid.SetColumn(rightCol, 2);

        arenaGrid.Children.Add(leftCol);
        arenaGrid.Children.Add(rightCol);
        _rootContent.Children.Add(arenaGrid);

        // LEFT COLUMN: Daily Imperatives & Hot Trade Opportunities
        leftCol.Children.Add(BuildDailyAgendaCard(kernel, cityId, today, attrs));
        var weatherCard = BuildWeatherOutlookCard(kernel, cityId, today);
        if (weatherCard is not null)
        {
            leftCol.Children.Add(weatherCard);
        }
        var monthlyCard = BuildMonthlyFinancialDigestCard(kernel, today);
        if (monthlyCard is not null)
        {
            leftCol.Children.Add(monthlyCard);
        }
        leftCol.Children.Add(BuildHotTradeCard(cityId, map, trade, catalog));

        // RIGHT COLUMN: Notable Figures & Living World Chronicle Feed
        rightCol.Children.Add(BuildNotableFiguresCard(cityId, registry, society));
        rightCol.Children.Add(BuildChronicleTimelineCard(society));
    }

    // =========================================================================
    // 1. Citadel Hero Banner
    // =========================================================================
    private UIElement BuildCitadelHeroBanner(SimKernel kernel, CitadelDef? citadel, CitadelEcosystemDef? eco, GameDate today)
    {
        var banner = new Border
        {
            Background = BannerBg,
            BorderBrush = BorderGold,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(18, 14, 18, 14)
        };

        var dock = new DockPanel();

        // Right side: Quick Action Buttons (Street Scene & World Map)
        var actionStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(actionStack, Dock.Right);

        var strollBtn = new GameButton
        {
            Content = "🏮 Ra Phố Dạo Chơi",
            Variant = GameButtonVariant.Primary,
            Margin = new Thickness(0, 0, 10, 0),
            MinHeight = 36,
            Padding = new Thickness(14, 6, 14, 6)
        };
        strollBtn.Click += (_, _) => RequestOpenCityScene?.Invoke();
        actionStack.Children.Add(strollBtn);

        var mapBtn = new GameButton
        {
            Content = "🗺️ Thiên Hạ",
            Variant = GameButtonVariant.Secondary,
            MinHeight = 36,
            Padding = new Thickness(14, 6, 14, 6)
        };
        mapBtn.Click += (_, _) => RequestNavigate?.Invoke("world.map");
        actionStack.Children.Add(mapBtn);

        dock.Children.Add(actionStack);

        // Left side: Citadel Metadata & Ecosystem Tags
        var leftInfo = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

        var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };

        var sealIcon = new Border
        {
            Background = AccentCinnabar,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2, 6, 2),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        sealIcon.Child = new TextBlock
        {
            Text = "城",
            FontFamily = new FontFamily("KaiTi, STKaiti, SimSun, Georgia"),
            FontWeight = FontWeights.Bold,
            FontSize = 13,
            Foreground = TextPrimary
        };
        titleRow.Children.Add(sealIcon);

        var cityName = citadel?.NameVi ?? "Lạc Dương";
        var nameBlock = new TextBlock
        {
            Text = $"THÀNH {cityName.ToUpperInvariant()}",
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            FontFamily = new FontFamily("KaiTi, STKaiti, SimSun, Georgia"),
            Foreground = AccentGold,
            VerticalAlignment = VerticalAlignment.Center
        };
        titleRow.Children.Add(nameBlock);

        var regionVi = citadel is not null ? RoleTitleVi.FormatRegion(citadel.Region) : "Trung Nguyên";
        var regionBadge = new GameBadge(regionVi, BadgeVariant.Gold)
        {
            Size = BadgeSize.Small,
            Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        titleRow.Children.Add(regionBadge);

        if (kernel.Capabilities.TryGet<IWeatherSystem>(CapabilityIds.WeatherSystem, out var weatherSys) && weatherSys is not null && citadel is not null)
        {
            var weather = weatherSys.GetWeather(citadel.Id, today);
            var weatherBadge = new GameBadge($"{weather.Icon} {weather.SolarTermNameVi} · {weather.NameVi}", BadgeVariant.Info)
            {
                Size = BadgeSize.Small,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = $"Khí hậu tiết khí: {weather.SolarTermNameVi}\nThời tiết: {weather.NameVi} ({weather.DescriptionVi})\nTưới tiêu: {(weather.CultivationMoistureDelta >= 0 ? "+" : "")}{weather.CultivationMoistureDelta}"
            };
            titleRow.Children.Add(weatherBadge);
        }

        leftInfo.Children.Add(titleRow);

        // Subtitle: Geography, climate & season
        var seasonVi = ResolveSeasonVi(today.Month);
        var climate = eco?.ClimateVi ?? "Ôn hòa mát mẻ";
        var topo = eco?.TopographyVi ?? "Đồng bằng phù sa màu mỡ";
        var descText = $"{seasonVi} · {climate} · Địa thế {topo}";

        var subBlock = new TextBlock
        {
            Text = descText,
            Foreground = TextMuted,
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 6)
        };
        leftInfo.Children.Add(subBlock);

        // Specialties Tags
        var specRow = new StackPanel { Orientation = Orientation.Horizontal };
        specRow.Children.Add(new TextBlock
        {
            Text = "Sản vật địa phương: ",
            FontSize = 11,
            Foreground = TextMuted,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0)
        });

        var spec1 = eco?.PrimarySpecialtyVi ?? "Lụa Thục";
        var spec2 = eco?.SecondarySpecialtyVi ?? "Gốm Sứ Triều Đình";
        specRow.Children.Add(new GameBadge(spec1, BadgeVariant.Rose) { Size = BadgeSize.Small, Margin = new Thickness(0, 0, 6, 0) });
        specRow.Children.Add(new GameBadge(spec2, BadgeVariant.Info) { Size = BadgeSize.Small });

        leftInfo.Children.Add(specRow);

        dock.Children.Add(leftInfo);
        banner.Child = dock;
        return banner;
    }

    // =========================================================================
    // 2. Daily Agenda Card (Quân Cơ Mật Lệnh)
    // =========================================================================
    private UIElement BuildDailyAgendaCard(SimKernel kernel, long cityId, GameDate today, IPlayerAttributes? attrs)
    {
        var card = new GameSectionCard
        {
            Heading = "Quân Cơ Mật Lệnh",
            Subtitle = "Gợi ý ưu tiên và công việc thiết yếu cần xử lý hôm nay",
            Icon = "📜"
        };

        var stack = new StackPanel();

        // 0a. Urgent Political Court Alert (Extortion Demand or High Scrutiny)
        if (kernel.Capabilities.TryGet<IPoliticalCourt>(CapabilityIds.CourtOffice, out var court) && court is not null)
        {
            var demand = court.ActiveScrutinyDemands.FirstOrDefault();
            var scrutiny = court.CurrentScrutiny;
            if (demand is not null)
            {
                stack.Children.Add(BuildAgendaRow(
                    "🚨 Tống Tiền Tối Hậu Thư",
                    $"Sứ giả của {demand.FactionNameVi} đòi nộp {demand.DemandCopper:N0} Quan trước ngày #{demand.DueDayIndex} ({demand.TitleVi}). Cần phúc đáp ngay để tránh tịch biên kho bãi!",
                    "Ứng Phó Quan Phủ",
                    () => RequestNavigate?.Invoke("politics.court"),
                    BadgeVariant.Loss));
            }
            else if (scrutiny is not null && scrutiny.ScrutinyScore >= 50)
            {
                stack.Children.Add(BuildAgendaRow(
                    $"⚖️ Nghi Kỵ Chư Hầu ({scrutiny.ScrutinyScore}/100)",
                    $"Mức độ nghi kỵ tại {scrutiny.FactionNameVi} đã lên cấp {scrutiny.ThreatLevel}. Nên hiến lương thảo hoặc vận động phe phái che chở.",
                    "Giải Trừ Nghi Kỵ",
                    () => RequestNavigate?.Invoke("politics.court"),
                    BadgeVariant.Warning));
            }
        }

        // 0b. Retainer Betrayal Risk Alert
        if (kernel.Capabilities.TryGet<ISocialNetwork>(CapabilityIds.SocialNetwork, out var social) && social is not null)
        {
            var riskyRetainer = social.GetRetainers()
                .FirstOrDefault(r => r.Loyalty < 55 && social.GetPsycheProfile(r.NpcId).Ambition >= 70);
            if (riskyRetainer is not null)
            {
                kernel.Capabilities.TryGet<INpcRegistry>(CapabilityIds.NpcRegistry, out var npcReg);
                var retainerName = npcReg?.Find(riskyRetainer.NpcId)?.NameVi ?? $"Gia thần #{riskyRetainer.NpcId}";
                var ambition = social.GetPsycheProfile(riskyRetainer.NpcId).Ambition;
                stack.Children.Add(BuildAgendaRow(
                    $"🕵️ Gia Thần Dao Động: {retainerName}",
                    $"Trung thành còn {riskyRetainer.Loyalty}/100, dã tâm {ambition}/100. Hãy ban thưởng hoặc đàm đạo trấn an trước khi mật báo bị rò rỉ.",
                    "Trấn An Danh Sĩ",
                    () => RequestNavigate?.Invoke("npc.directory"),
                    BadgeVariant.Warning));
            }
        }

        // 0c. Contraband Smuggling Opportunity
        if (kernel.Capabilities.TryGet<IEconomicWarfare>(CapabilityIds.EconomicWarfare, out var warfare) && warfare is not null)
        {
            var route = warfare.AvailableSmuggleOpportunities
                .FirstOrDefault(r => r.OriginCitadelId == cityId || r.DestCitadelId == cityId);
            if (route is not null)
            {
                stack.Children.Add(BuildAgendaRow(
                    $"🔥 Thời Cơ Buôn Lậu: {route.ItemNameVi}",
                    $"Tuyến ngầm {route.OriginCitadelNameVi} → {route.DestCitadelNameVi} đang lãi +{route.ProfitMarginPercent}% (Tuần binh {route.InterceptionRiskPercent}%).",
                    "Vận Buôn Lậu",
                    () => RequestNavigate?.Invoke("market.stall"),
                    BadgeVariant.Purple));
            }
        }

        // 1. Health & Vitals recommendation
        var fatigue = attrs?.Vitals.Fatigue ?? 0;
        var currentStam = attrs?.Vitals.CurrentStamina ?? 100;
        var maxStam = attrs?.Vitals.MaxStamina ?? 100;

        if (fatigue >= 75)
        {
            stack.Children.Add(BuildAgendaRow(
                "⚠️ Thể Lực Kiệt Quệ",
                $"Mệt mỏi tích tụ {fatigue}%, thể lực còn {currentStam}/{maxStam}. Nguy cơ cao bị ngất xỉu hoặc giảm hiệu suất đàm đạo. Quản gia khuyên nên tĩnh dưỡng.",
                "Nghỉ Ngơi",
                () => RequestRest?.Invoke(),
                BadgeVariant.Loss));
        }
        else
        {
            stack.Children.Add(BuildAgendaRow(
                "🌿 Khí Lực Dồi Dào",
                $"Thể lực {currentStam}/{maxStam} (Mệt mỏi {fatigue}%). Khí huyết tràn trề, thích hợp tản bộ phố phường tìm kỳ ngộ hoặc tỷ võ kết giao.",
                "Du Ngoạn Phố",
                () => RequestStroll?.Invoke(),
                BadgeVariant.Gain));
        }

        // 2. Financial settlement cycle
        var daysLeft = GameDate.DaysPerMonth - today.Day + 1;
        if (daysLeft <= 4)
        {
            stack.Children.Add(BuildAgendaRow(
                "📅 Kỳ Thu Thuế Cuối Tháng",
                $"Chỉ còn {daysLeft} ngày là hết tháng. Nha môn sẽ tự động trừ địa tô điền trang và phí thuê kho bãi. Hãy rà soát tiền mặt.",
                "Xem Sổ Cái",
                () => RequestNavigate?.Invoke("finance.ledger"),
                BadgeVariant.Warning));
        }
        else
        {
            stack.Children.Add(BuildAgendaRow(
                "📊 Chu Kỳ Giao Thương",
                $"Hôm nay là ngày {today.Day}/30 của tháng. Thương nhân các nơi đổ về trao đổi hàng hóa tấp nập tại các sạp hàng trung tâm.",
                "Khảo Sát Chợ",
                () => RequestNavigate?.Invoke("market.stall"),
                BadgeVariant.Info));
        }

        // 3. Official Court & Arena Opportunities
        stack.Children.Add(BuildAgendaRow(
            "🏛️ Quan Phủ & Nha Môn",
            "Nha môn địa phương đang mở cửa tiếp nhận kiến nghị, phân giải tranh chấp và xem xét bổ nhiệm quan chức sĩ phu.",
            "Vào Quan Phủ",
            () => RequestNavigate?.Invoke("politics.court"),
            BadgeVariant.Purple));

        stack.Children.Add(BuildAgendaRow(
            "⚔️ Lôi Đài Hào Kiệt",
            "Võ đài thành trấn đang mở hội luận võ. Các tráng sĩ, hiệp khách khắp nơi tề tựu tỷ thí rèn luyện bản lĩnh và uy danh.",
            "Đến Lôi Đài",
            () => RequestNavigate?.Invoke("military.arena"),
            BadgeVariant.Default));

        card.CardBody = stack;
        return card;
    }

    private UIElement BuildAgendaRow(string title, string detail, string btnLabel, Action action, BadgeVariant badgeVariant)
    {
        var border = new Border
        {
            Background = ItemRowBg,
            BorderBrush = ItemRowBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 0, 0, 8)
        };

        var dock = new DockPanel();

        var btn = new GameButton
        {
            Content = btnLabel,
            Variant = GameButtonVariant.Secondary,
            MinHeight = 30,
            Padding = new Thickness(12, 4, 12, 4),
            VerticalAlignment = VerticalAlignment.Center
        };
        btn.Click += (_, _) => action();
        DockPanel.SetDock(btn, Dock.Right);
        dock.Children.Add(btn);

        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };

        var titleStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 2) };
        titleStack.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = FontWeights.SemiBold,
            FontSize = 13,
            Foreground = TextPrimary,
            VerticalAlignment = VerticalAlignment.Center
        });
        info.Children.Add(titleStack);

        info.Children.Add(new TextBlock
        {
            Text = detail,
            FontSize = 11,
            Foreground = TextMuted,
            TextWrapping = TextWrapping.Wrap
        });

        dock.Children.Add(info);
        border.Child = dock;
        return border;
    }

    // =========================================================================
    // 3. Hot Trade Opportunities Card (Bắt Mạch Thương Đạo)
    // =========================================================================
    private UIElement BuildHotTradeCard(long cityId, IWorldMap? map, IMarketTrade? trade, IItemCatalog? catalog)
    {
        var card = new GameSectionCard
        {
            Heading = "Bắt Mạch Thương Đạo",
            Subtitle = "Cơ hội chênh lệch giá hấp dẫn giữa thành hiện tại và lân bang",
            Icon = "💰"
        };

        var stack = new StackPanel();

        if (map is null || trade is null || catalog is null)
        {
            stack.Children.Add(new TextBlock { Text = "Chưa có thông tin tình báo thương mại.", Foreground = TextMuted });
            card.CardBody = stack;
            return card;
        }

        var neighbors = map.RouteNeighbours(cityId);
        if (neighbors.Count == 0)
        {
            stack.Children.Add(new TextBlock { Text = "Thành trì hiện tại không có quan đạo trực tiếp nối lân bang.", Foreground = TextMuted });
            card.CardBody = stack;
            return card;
        }

        // Find top 3 arbitrage opportunities
        var items = catalog.All.Take(12).ToList();
        var opportunities = new List<(ItemDef Item, CitadelDef TargetCitadel, long BuyPrice, long SellPrice, long Profit, double MarginPct)>();

        foreach (var item in items)
        {
            var buyPrice = trade.QuoteBuy(cityId, item.Id);
            if (buyPrice <= 0) continue;

            foreach (var targetId in neighbors)
            {
                var sellPrice = trade.QuoteSell(targetId, item.Id);
                var profit = sellPrice - buyPrice;
                if (profit > 0)
                {
                    var targetCitadel = map.Find(targetId);
                    if (targetCitadel is not null)
                    {
                        var marginPct = (profit * 100.0) / buyPrice;
                        opportunities.Add((item, targetCitadel, buyPrice, sellPrice, profit, marginPct));
                    }
                }
            }
        }

        var topOpportunities = opportunities.OrderByDescending(o => o.MarginPct).Take(3).ToList();

        if (topOpportunities.Count == 0)
        {
            stack.Children.Add(new TextBlock
            {
                Text = "Thị trường quanh khu vực đang bão hòa, chưa có chênh lệch giá đột biến.",
                Foreground = TextMuted,
                FontSize = 12
            });
        }
        else
        {
            foreach (var opp in topOpportunities)
            {
                stack.Children.Add(BuildTradeOpportunityRow(opp.Item, opp.TargetCitadel, opp.BuyPrice, opp.SellPrice, opp.Profit, opp.MarginPct));
            }
        }

        // Bottom quick caravan link
        var footerBtn = new GameButton
        {
            Content = "🐎 Mở Điều Phối Thương Đoàn Viễn Hành",
            Variant = GameButtonVariant.Secondary,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 6, 0, 0),
            MinHeight = 32
        };
        footerBtn.Click += (_, _) => RequestNavigate?.Invoke("caravan.routes");
        var compassBtn = new GameButton
        {
            Content = "🧭 La Bàn Giá [B]",
            Variant = GameButtonVariant.Primary,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 6, 0, 0),
            MinHeight = 32
        };
        compassBtn.Click += (_, _) => RequestOpenTradeCompass?.Invoke();
        stack.Children.Add(compassBtn);
        stack.Children.Add(footerBtn);

        card.CardBody = stack;
        return card;
    }

    private UIElement BuildTradeOpportunityRow(ItemDef item, CitadelDef target, long buyPrice, long sellPrice, long profit, double marginPct)
    {
        var border = new Border
        {
            Background = ItemRowBg,
            BorderBrush = ItemRowBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 0, 0, 8)
        };

        var dock = new DockPanel();

        var buyBtn = new GameButton
        {
            Content = "Đến Chợ Mua",
            Variant = GameButtonVariant.Secondary,
            MinHeight = 28,
            Padding = new Thickness(10, 3, 10, 3),
            VerticalAlignment = VerticalAlignment.Center
        };
        buyBtn.Click += (_, _) => RequestNavigate?.Invoke("market.stall");
        DockPanel.SetDock(buyBtn, Dock.Right);
        dock.Children.Add(buyBtn);

        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };

        var topRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 2) };
        topRow.Children.Add(new TextBlock
        {
            Text = item.NameVi,
            FontWeight = FontWeights.Bold,
            FontSize = 13,
            Foreground = TextPrimary,
            VerticalAlignment = VerticalAlignment.Center
        });

        var marginBadge = new GameBadge($"+{marginPct:F0}% lời", BadgeVariant.Gain)
        {
            Size = BadgeSize.Small,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        topRow.Children.Add(marginBadge);
        info.Children.Add(topRow);

        var detailText = $"Mua tại thành: {buyPrice}q ➔ Chở sang {target.NameVi} bán: {sellPrice}q (+{profit}q/kiện)";
        info.Children.Add(new TextBlock
        {
            Text = detailText,
            FontSize = 11,
            Foreground = TextMuted
        });

        dock.Children.Add(info);
        border.Child = dock;
        return border;
    }

    // =========================================================================
    // 4. Notable Figures In Town Card (Danh Sĩ Tại Thành)
    // =========================================================================
    private UIElement BuildNotableFiguresCard(long cityId, INpcRegistry? registry, ISocietyChronicle? society)
    {
        var card = new GameSectionCard
        {
            Heading = "Danh Sĩ Tại Thành",
            Subtitle = "Những nhân vật kiệt xuất đang có mặt tại thành trì",
            Icon = "👥"
        };

        var stack = new StackPanel();

        if (registry is null)
        {
            stack.Children.Add(new TextBlock { Text = "Chưa có danh bạ nhân sĩ.", Foreground = TextMuted });
            card.CardBody = stack;
            return card;
        }

        var npcs = registry.FindByCitadel(cityId).Take(3).ToList();
        if (npcs.Count == 0)
        {
            stack.Children.Add(new TextBlock
            {
                Text = "Hiện chưa có danh sĩ hiển hách nào lưu lại. Đa phần là thường dân và khách bộ hành.",
                Foreground = TextMuted,
                FontSize = 12
            });
        }
        else
        {
            foreach (var npc in npcs)
            {
                var act = society?.GetNpcActivity(npc.Id);
                stack.Children.Add(BuildNpcRow(npc, act));
            }
        }

        var directoryBtn = new GameButton
        {
            Content = "🕸️ Mở Danh Bạ & Mạng Lưới Quan Hệ",
            Variant = GameButtonVariant.Secondary,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 6, 0, 0),
            MinHeight = 32
        };
        directoryBtn.Click += (_, _) => RequestNavigate?.Invoke("social.network");
        stack.Children.Add(directoryBtn);

        card.CardBody = stack;
        return card;
    }

    private UIElement BuildNpcRow(NpcDef npc, NpcActivityDef? activity)
    {
        var border = new Border
        {
            Background = ItemRowBg,
            BorderBrush = ItemRowBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 0, 0, 8)
        };

        var dock = new DockPanel();

        var talkBtn = new GameButton
        {
            Content = "Đàm Đạo",
            Variant = GameButtonVariant.Secondary,
            MinHeight = 28,
            Padding = new Thickness(10, 3, 10, 3),
            VerticalAlignment = VerticalAlignment.Center
        };
        talkBtn.Click += (_, _) => RequestTalkNpc?.Invoke(npc.Id);
        DockPanel.SetDock(talkBtn, Dock.Right);
        dock.Children.Add(talkBtn);

        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };

        var nameRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 2) };
        nameRow.Children.Add(new TextBlock
        {
            Text = npc.NameVi,
            FontWeight = FontWeights.Bold,
            FontSize = 13,
            Foreground = TextPrimary,
            VerticalAlignment = VerticalAlignment.Center
        });

        var roleBadge = new GameBadge(RoleTitleVi.Format(npc.Role), BadgeVariant.Gold)
        {
            Size = BadgeSize.Small,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        nameRow.Children.Add(roleBadge);
        info.Children.Add(nameRow);

        var actionText = !string.IsNullOrWhiteSpace(activity?.CurrentAction)
            ? $"Hành tung: {activity.CurrentAction}"
            : "Đang dừng chân ngơi nghỉ tại thành trấn";

        info.Children.Add(new TextBlock
        {
            Text = actionText,
            FontSize = 11,
            Foreground = TextMuted
        });

        dock.Children.Add(info);
        border.Child = dock;
        return border;
    }

}
