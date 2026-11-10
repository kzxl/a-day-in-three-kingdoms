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

    // =========================================================================
    // 5. Living World Chronicle Timeline Card (Thiên Hạ Biên Niên Sử)
    // =========================================================================
    private UIElement BuildChronicleTimelineCard(ISocietyChronicle? society)
    {
        var card = new GameSectionCard
        {
            Heading = "Thiên Hạ Biên Niên Sử",
            Subtitle = "Biến cố thời cuộc và diễn biến thế giới Tam Quốc gần nhất",
            Icon = "📜"
        };

        var stack = new StackPanel();

        if (society is null)
        {
            stack.Children.Add(new TextBlock { Text = "Chưa kết nối niên giám thế giới.", Foreground = TextMuted });
            card.CardBody = stack;
            return card;
        }

        var events = society.RecentEvents(limit: 4);
        if (events.Count == 0)
        {
            stack.Children.Add(new TextBlock
            {
                Text = "Thiên hạ thái bình, chưa có biến cố lớn nào được ghi nhận gần đây.",
                Foreground = TextMuted,
                FontSize = 12
            });
        }
        else
        {
            foreach (var evt in events)
            {
                stack.Children.Add(BuildChronicleTimelineItem(evt));
            }
        }

        var moreBtn = new GameButton
        {
            Content = "📜 Xem Toàn Bộ Thiên Hạ Niên Giám",
            Variant = GameButtonVariant.Secondary,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 6, 0, 0),
            MinHeight = 32
        };
        moreBtn.Click += (_, _) => RequestNavigate?.Invoke("society.chronicle");
        stack.Children.Add(moreBtn);

        card.CardBody = stack;
        return card;
    }

    private UIElement BuildChronicleTimelineItem(SocietyLogEntry evt)
    {
        var container = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        container.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
        container.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Left Timeline Column (Red dot & vertical trace)
        var timeMarker = new Canvas { Width = 20 };
        var line = new Line
        {
            X1 = 10,
            Y1 = 8,
            X2 = 10,
            Y2 = 60,
            Stroke = BorderGold,
            StrokeThickness = 1
        };
        timeMarker.Children.Add(line);

        var dot = new Ellipse
        {
            Width = 8,
            Height = 8,
            Fill = AccentCinnabar,
            Stroke = AccentGold,
            StrokeThickness = 1
        };
        Canvas.SetLeft(dot, 6);
        Canvas.SetTop(dot, 4);
        timeMarker.Children.Add(dot);

        Grid.SetColumn(timeMarker, 0);
        container.Children.Add(timeMarker);

        // Right Content
        var body = new StackPanel { Margin = new Thickness(4, 0, 0, 4) };

        var dateRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 2) };
        dateRow.Children.Add(new TextBlock
        {
            Text = $"[{GameDateCodec.ShortVi(evt.Date)}]",
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = AccentGold,
            Margin = new Thickness(0, 0, 6, 0)
        });

        var kindBadge = new GameBadge(FormatEventKind(evt.EventKind), BadgeVariant.Default) { Size = BadgeSize.Small };
        dateRow.Children.Add(kindBadge);

        if (!string.IsNullOrWhiteSpace(evt.CitadelNameVi))
        {
            dateRow.Children.Add(new TextBlock
            {
                Text = $" · {evt.CitadelNameVi}",
                FontSize = 11,
                Foreground = TextMuted
            });
        }
        body.Children.Add(dateRow);

        body.Children.Add(new TextBlock
        {
            Text = evt.HeadlineVi,
            FontWeight = FontWeights.SemiBold,
            FontSize = 12,
            Foreground = TextPrimary,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 2)
        });

        if (!string.IsNullOrWhiteSpace(evt.DetailVi) && evt.DetailVi != evt.HeadlineVi)
        {
            body.Children.Add(new TextBlock
            {
                Text = evt.DetailVi,
                FontSize = 11,
                Foreground = TextMuted,
                TextWrapping = TextWrapping.Wrap
            });
        }

        Grid.SetColumn(body, 1);
        container.Children.Add(body);

        return container;
    }

    private static string FormatEventKind(string kind) => kind switch
    {
        "martial" => "Võ Nghiệp",
        "commerce" => "Thương Đạo",
        "governance" => "Triều Chính",
        "scholarly" => "Nho Sĩ",
        "travel" => "Kỳ Ngộ",
        _ => "Thời Cuộc"
    };

    private static string ResolveSeasonVi(int month) => month switch
    {
        >= 1 and <= 3 => "Mùa Xuân",
        >= 4 and <= 6 => "Mùa Hạ",
        >= 7 and <= 9 => "Mùa Thu",
        _ => "Mùa Đông"
    };

    private static Brush Freeze(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }

    // =========================================================================
    // 1.5 RPG Starter Questline (3 Bước Khởi Nghiệp Loạn Thế)
    // =========================================================================
    private UIElement? BuildStarterQuestCard(SimKernel kernel, GameDate today, IPlayerState playerState)
    {
        var startDay = GameDate.Create(184, 1, 1).DayIndex;
        var daysPassed = (long)Math.Max(0, today.DayIndex - startDay);
        var balance = kernel.Ledger.Balance;

        if (daysPassed > 30 && balance >= 6000)
            return null;

        var card = new GameSectionCard
        {
            Heading = "Hành Trình Tân Thủ — 3 Bước Khởi Nghiệp Loạn Thế",
            Subtitle = "Lộ trình từng bước giúp lữ khách an thân lập mệnh, làm quen cuộc sống Tam Quốc và tránh bỡ ngỡ",
            Icon = "⚔️",
            Margin = new Thickness(0, 14, 0, 0)
        };

        var stack = new StackPanel();

        // Step 1: Quen nước quen lửa
        bool step1Done = daysPassed >= 1;
        stack.Children.Add(BuildStarterStepRow(
            stepNumber: 1,
            title: "🏮 Quen Nước Quen Lửa (Dạo Phố & Quán Trọ)",
            description: "Đến [Trong Thành] dạo chơi, tìm hiểu giá gạo, trà, muối và chuyện trò với danh sĩ địa phương để nắm bắt tình hình.",
            actionLabel: "Ra Phố Dạo Chơi",
            isDone: step1Done,
            action: () => RequestOpenCityScene?.Invoke()));

        // Step 2: Bát cơm manh áo
        bool step2Done = daysPassed >= 3 || balance >= 2200;
        stack.Children.Add(BuildStarterStepRow(
            stepNumber: 2,
            title: "🌾 Bát Cơm Manh Áo (Kiếm Tiền Sinh Nhai)",
            description: "Vào [Nghề Nghiệp] làm công nhật (nông phu, tiều phu, rèn) hoặc buôn chuyến hàng nhỏ tại [Chợ Thành] để tích lũy quan tiền.",
            actionLabel: "Xem Nghề Nghiệp",
            isDone: step2Done,
            action: () => RequestNavigate?.Invoke("professions.trades")));

        // Step 3: Tích lũy lộ phí & Mở rộng cơ nghiệp
        bool step3Done = balance >= 2500;
        stack.Children.Add(BuildStarterStepRow(
            stepNumber: 3,
            title: $"🗺️ Tích Lũy Lộ Phí (Mục tiêu: 2.500 Quan · Hiện có: {balance:N0} Quan)",
            description: "Tích lũy vốn để mở khóa [Kho Bãi] tránh hỏng hàng, sẵn sàng lộ phí để xuất hành quan đạo sang các châu khác trên [Thiên Hạ].",
            actionLabel: "Xem Bản Đồ",
            isDone: step3Done,
            action: () => RequestNavigate?.Invoke("world.map")));

        card.CardBody = stack;
        return card;
    }

    private UIElement BuildStarterStepRow(int stepNumber, string title, string description, string actionLabel, bool isDone, Action action)
    {
        var border = new Border
        {
            Background = ItemRowBg,
            BorderBrush = isDone ? BorderGold : ItemRowBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 0, 0, 8)
        };

        var dock = new DockPanel();

        var btn = new GameButton
        {
            Content = isDone ? "✓ Hoàn thành" : actionLabel,
            Variant = isDone ? GameButtonVariant.Secondary : GameButtonVariant.Primary,
            MinHeight = 30,
            Padding = new Thickness(10, 4, 10, 4),
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(btn, Dock.Right);
        btn.Click += (_, _) => action();
        dock.Children.Add(btn);

        var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal };

        var badge = new GameBadge(isDone ? "ĐÃ XONG" : $"BƯỚC {stepNumber}", isDone ? BadgeVariant.Gain : BadgeVariant.Gold)
        {
            Size = BadgeSize.Small,
            Margin = new Thickness(0, 0, 8, 0)
        };
        titleRow.Children.Add(badge);

        var titleBlock = new TextBlock
        {
            Text = title,
            FontWeight = FontWeights.Bold,
            FontSize = 13,
            Foreground = isDone ? TextPrimary : AccentGold,
            VerticalAlignment = VerticalAlignment.Center
        };
        titleRow.Children.Add(titleBlock);
        textStack.Children.Add(titleRow);

        var descBlock = new TextBlock
        {
            Text = description,
            Foreground = TextMuted,
            FontSize = 11.5,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 3, 0, 0)
        };
        textStack.Children.Add(descBlock);

        dock.Children.Add(textStack);
        border.Child = dock;
        return border;
    }
    private UIElement? BuildMonthlyFinancialDigestCard(SimKernel kernel, GameDate today)
    {
        var fromDay = Math.Max(1, today.DayIndex - 30);
        var lines = kernel.Ledger.Query(GameDate.FromDayIndex(fromDay), today, limit: 300);
        if (lines.Count == 0) return null;

        var income = lines.Where(l => l.Direction == SimT.Contracts.Ledger.LedgerDirection.In).Sum(l => l.Amount);
        var spend = lines.Where(l => l.Direction == SimT.Contracts.Ledger.LedgerDirection.Out).Sum(l => l.Amount);
        var net = income - spend;

        var card = new Border
        {
            Background = ItemRowBg,
            BorderBrush = ItemRowBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 12, 14, 12),
            Margin = new Thickness(0, 0, 0, 14)
        };

        var stack = new StackPanel();

        // Header
        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titleBlock = new TextBlock
        {
            Text = "TỔNG KẾT TÀI CHÍNH (30 NGÀY QUA)",
            FontSize = 10.5,
            FontWeight = FontWeights.Bold,
            Foreground = AccentGold
        };
        Grid.SetColumn(titleBlock, 0);
        headerGrid.Children.Add(titleBlock);

        var netBlock = new TextBlock
        {
            Text = $"{(net >= 0 ? "+" : "")}{net:N0} quan",
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = net >= 0 ? new SolidColorBrush(Color.FromRgb(0x4A, 0xDE, 0x80)) : new SolidColorBrush(Color.FromRgb(0xF8, 0x71, 0x71))
        };
        Grid.SetColumn(netBlock, 1);
        headerGrid.Children.Add(netBlock);
        stack.Children.Add(headerGrid);

        // Subtitle / Quick Summary Row
        var subText = new TextBlock
        {
            Text = $"Thu: {income:N0} quan · Chi: {spend:N0} quan · Số dư: {kernel.Ledger.Balance:N0} quan",
            FontSize = 11,
            Foreground = TextMuted,
            Margin = new Thickness(0, 4, 0, 8)
        };
        stack.Children.Add(subText);

        // Top Expenses Breakdown
        var topExpenses = lines
            .Where(l => l.Direction == SimT.Contracts.Ledger.LedgerDirection.Out)
            .GroupBy(l => l.JournalCode)
            .OrderByDescending(g => g.Sum(l => l.Amount))
            .Take(3)
            .Select(g => $"{g.Key}: {g.Sum(l => l.Amount):N0}")
            .ToList();

        if (topExpenses.Count > 0)
        {
            var expText = new TextBlock
            {
                Text = "Khoản lớn: " + string.Join(" · ", topExpenses),
                FontSize = 10.5,
                Foreground = TextMuted
            };
            stack.Children.Add(expText);
        }

        card.Child = stack;
        return card;
    }

    private UIElement? BuildWeatherOutlookCard(SimKernel kernel, long cityId, GameDate today)
    {
        if (!kernel.Capabilities.TryGet<IWeatherSystem>(CapabilityIds.WeatherSystem, out var weatherSys) || weatherSys is null)
        {
            return null;
        }

        var weather = weatherSys.GetWeather(cityId, today);
        var card = new GameSectionCard
        {
            Heading = "Thiên Thời & 24 Tiết Khí",
            Subtitle = "Khí hậu địa phương · Cộng hưởng thương đạo & canh tác điền trang",
            Icon = "⛅"
        };

        var root = new StackPanel();

        // Main Weather Banner
        var todayBanner = new Border
        {
            Background = ItemRowBg,
            BorderBrush = ItemRowBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14, 12, 14, 12),
            Margin = new Thickness(0, 0, 0, 10)
        };

        var bannerGrid = new Grid();
        bannerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
        bannerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var iconBlock = new TextBlock
        {
            Text = weather.Icon,
            FontSize = 26,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(iconBlock, 0);
        bannerGrid.Children.Add(iconBlock);

        var infoStack = new StackPanel
        {
            Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(infoStack, 1);

        var headlineRow = new StackPanel { Orientation = Orientation.Horizontal };
        headlineRow.Children.Add(new TextBlock
        {
            Text = $"Tiết Khí: {weather.SolarTermNameVi}",
            FontWeight = FontWeights.Bold,
            FontSize = 14,
            Foreground = AccentGold,
            VerticalAlignment = VerticalAlignment.Center
        });
        headlineRow.Children.Add(new GameBadge(weather.NameVi, BadgeVariant.Info)
        {
            Size = BadgeSize.Small,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        infoStack.Children.Add(headlineRow);

        var descBlock = new TextBlock
        {
            Text = weather.DescriptionVi,
            FontSize = 12,
            Foreground = TextMuted,
            Margin = new Thickness(0, 3, 0, 0),
            TextWrapping = TextWrapping.Wrap
        };
        infoStack.Children.Add(descBlock);

        bannerGrid.Children.Add(infoStack);
        todayBanner.Child = bannerGrid;
        root.Children.Add(todayBanner);

        // Resonance Highlights (Điền trang & Thương lộ)
        var resGrid = new UniformGrid { Columns = 2, Margin = new Thickness(0, 0, 0, 10) };

        // Farmstead Impact
        var farmImpact = new Border
        {
            Background = ItemRowBg,
            BorderBrush = ItemRowBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 4, 0)
        };
        var farmStack = new StackPanel();
        farmStack.Children.Add(new TextBlock
        {
            Text = "🌱 ĐIỀN TRANG NÔNG ĐIỀN",
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = TextPrimary
        });
        var moistSign = weather.CultivationMoistureDelta >= 0 ? "+" : "";
        farmStack.Children.Add(new TextBlock
        {
            Text = $"Tưới tiêu: {moistSign}{weather.CultivationMoistureDelta} Thủy lợi",
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = weather.CultivationMoistureDelta >= 0 ? (Brush)Application.Current.FindResource("Gain") : (Brush)Application.Current.FindResource("Loss"),
            Margin = new Thickness(0, 3, 0, 0)
        });
        farmImpact.Child = farmStack;
        resGrid.Children.Add(farmImpact);

        // Caravan & Market Impact
        var tradeImpact = new Border
        {
            Background = ItemRowBg,
            BorderBrush = ItemRowBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(4, 0, 0, 0)
        };
        var tradeStack = new StackPanel();
        tradeStack.Children.Add(new TextBlock
        {
            Text = "🐪 THƯƠNG ĐẠO & VẬN TẢI",
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = TextPrimary
        });
        var caravanNotice = weather.CaravanTravelDelayDays > 0 ? $"+{weather.CaravanTravelDelayDays} ngày trễ lộ" : "Lộ trình thông suốt";
        tradeStack.Children.Add(new TextBlock
        {
            Text = caravanNotice,
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = weather.CaravanTravelDelayDays > 0 ? (Brush)Application.Current.FindResource("Loss") : AccentGold,
            Margin = new Thickness(0, 3, 0, 0)
        });
        tradeImpact.Child = tradeStack;
        resGrid.Children.Add(tradeImpact);

        root.Children.Add(resGrid);

        // 5-Day Weather Forecast
        var forecastTitle = new TextBlock
        {
            Text = "Dự Báo Khí Tượng 5 Ngày Tới:",
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = TextMuted,
            Margin = new Thickness(0, 0, 0, 6)
        };
        root.Children.Add(forecastTitle);

        var forecastBar = new UniformGrid { Columns = 5 };
        var forecastList = weatherSys.GetForecast(cityId, today, 5);
        foreach (var item in forecastList)
        {
            var dayCard = new Border
            {
                Background = ItemRowBg,
                BorderBrush = ItemRowBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(4, 6, 4, 6),
                Margin = new Thickness(2, 0, 2, 0)
            };
            var dayStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            dayStack.Children.Add(new TextBlock
            {
                Text = $"{item.Date.Day}/{item.Date.Month}",
                FontSize = 10,
                Foreground = TextMuted,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            dayStack.Children.Add(new TextBlock
            {
                Text = item.Icon,
                FontSize = 15,
                Margin = new Thickness(0, 2, 0, 2),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            dayStack.Children.Add(new TextBlock
            {
                Text = item.NameVi,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = TextPrimary,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            dayCard.Child = dayStack;
            forecastBar.Children.Add(dayCard);
        }
        root.Children.Add(forecastBar);

        card.CardBody = root;
        return card;
    }
}
