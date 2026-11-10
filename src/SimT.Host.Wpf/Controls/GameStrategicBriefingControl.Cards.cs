using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;
using SimT.Core.Kernel;

namespace SimT.Host.Wpf.Controls;

public sealed partial class GameStrategicBriefingControl
{
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
