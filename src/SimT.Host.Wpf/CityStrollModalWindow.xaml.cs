using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;
using SimT.Core.Kernel;

namespace SimT.Host.Wpf;

public sealed record StrollHistoryEntry(
    string TimeText,
    string LocationName,
    string Title,
    string RewardSummary,
    string Glyph);

public partial class CityStrollModalWindow : Window
{
    private static readonly List<StrollHistoryEntry> SessionHistory = [];

    private readonly SimKernel _kernel;
    private readonly ICityStroll _stroll;
    private readonly long _cityId;

    public event Action? StrollStateChanged;

    public CityStrollModalWindow(SimKernel kernel, ICityStroll stroll, long cityId, StrollResult? initialResult = null)
    {
        InitializeComponent();
        DarkTitleBar.Apply(this);

        _kernel = kernel;
        _stroll = stroll;
        _cityId = cityId;

        Loaded += (_, _) => StrollStateChanged?.Invoke();

        var result = initialResult ?? stroll.Stroll(cityId);
        DisplayResult(result);
    }

    private void DisplayResult(StrollResult result)
    {
        var (glyph, categoryName, catColor) = GetEventMeta(result.EventType);

        EventGlyphText.Text = glyph;
        IconBox.BorderBrush = new SolidColorBrush(catColor);

        CategoryBadge.Background = new SolidColorBrush(Color.FromArgb(0x28, catColor.R, catColor.G, catColor.B));
        CategoryBadge.BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, catColor.R, catColor.G, catColor.B));
        CategoryText.Text = categoryName.ToUpperInvariant();
        CategoryText.Foreground = new SolidColorBrush(catColor);

        LocationVenueText.Text = $"📍 {result.LocationName}";
        EventTitleText.Text = result.Title;
        DateCounterText.Text = $"📅 {GameDateCodec.Format(_kernel.Today)} · {GameDateCodec.DescribeVi(_kernel.Today)}";

        NarrativeText.Text = result.Narrative;

        // Interactive choices vs final reward
        if (result.RequiresChoice)
        {
            ChoicesBorder.Visibility = Visibility.Visible;
            RewardStripBorder.Visibility = Visibility.Collapsed;
            ContinueStrollBtn.IsEnabled = false;
            ContinueStrollBtn.ToolTip = "Hãy giải quyết tương tác kỳ ngộ bên trên trước khi dạo tiếp!";

            if (result.MerchantOffers is not null && result.MerchantOffers.Count > 0)
            {
                ChoicesHeadingText.Text = "❖ QUẦY HÀNG THƯƠNG NHÂN DU PHƯƠNG";
                ChoicesSubHeadingText.Text = "Chọn mua 1 món hàng quý hoặc khéo léo từ chối";
                MerchantOffersList.ItemsSource = result.MerchantOffers;
                MerchantOffersList.Visibility = Visibility.Visible;
                DeclineMerchantBtn.Visibility = Visibility.Visible;
                NpcChoicesPanel.Visibility = Visibility.Collapsed;
            }
            else if (result.Choices is not null && result.Choices.Count > 0)
            {
                ChoicesHeadingText.Text = "❖ ĐỐI THOẠI & GIAO THIỆP DANH SĨ";
                ChoicesSubHeadingText.Text = "Chọn phương thức ứng xử và đàm đạo phù hợp";
                MerchantOffersList.Visibility = Visibility.Collapsed;
                DeclineMerchantBtn.Visibility = Visibility.Collapsed;
                NpcChoicesPanel.Visibility = Visibility.Visible;
                PopulateNpcChoices(result.Choices);
            }
            else
            {
                ChoicesBorder.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            ChoicesBorder.Visibility = Visibility.Collapsed;
            RewardStripBorder.Visibility = Visibility.Visible;

            // Reward badges
            RewardBadgesPanel.Children.Clear();
            if (!string.IsNullOrWhiteSpace(result.RewardSummary))
            {
                var parts = result.RewardSummary.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var part in parts)
                {
                    var pill = CreateRewardPill(part);
                    RewardBadgesPanel.Children.Add(pill);
                }
            }
            else
            {
                var emptyPill = CreateRewardPill("Không có biến động đáng kể");
                RewardBadgesPanel.Children.Add(emptyPill);
            }

            // History entry
            if (result.Ok)
            {
                var entry = new StrollHistoryEntry(
                    $"Ngày {_kernel.Today.DayIndex}",
                    result.LocationName,
                    result.Title,
                    result.RewardSummary,
                    glyph);
                SessionHistory.Insert(0, entry);
                if (SessionHistory.Count > 10) SessionHistory.RemoveAt(SessionHistory.Count - 1);
            }

            HistoryList.ItemsSource = null;
            HistoryList.ItemsSource = SessionHistory.Take(5).ToList();
        }

        // Vitals status
        UpdateVitalsBar(result.RequiresChoice);

        // Animation transition
        AnimateStoryEntrance();

        StrollStateChanged?.Invoke();
    }

    private void PopulateNpcChoices(IReadOnlyList<StrollChoiceOption> choices)
    {
        NpcChoicesPanel.Children.Clear();

        foreach (var choice in choices)
        {
            var btn = new Button
            {
                Style = (Style)FindResource("GhostButton"),
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(12, 7, 12, 7),
                Margin = new Thickness(0, 0, 0, 6),
                Tag = choice.Key
            };

            var stack = new StackPanel();

            var titleLine = new DockPanel();
            var titleText = new TextBlock
            {
                Text = choice.LabelVi,
                FontWeight = FontWeights.Bold,
                FontSize = 12.5,
                Foreground = (Brush)FindResource("Text")
            };
            DockPanel.SetDock(titleText, Dock.Left);
            titleLine.Children.Add(titleText);

            if (!string.IsNullOrWhiteSpace(choice.CostSummary))
            {
                var costText = new TextBlock
                {
                    Text = choice.CostSummary,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 11,
                    Foreground = (Brush)FindResource("Accent"),
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                DockPanel.SetDock(costText, Dock.Right);
                titleLine.Children.Add(costText);
            }
            stack.Children.Add(titleLine);

            if (!string.IsNullOrWhiteSpace(choice.DescriptionVi))
            {
                var descText = new TextBlock
                {
                    Text = choice.DescriptionVi,
                    Style = (Style)FindResource("Caption"),
                    Foreground = (Brush)FindResource("Muted"),
                    Margin = new Thickness(0, 2, 0, 0),
                    TextWrapping = TextWrapping.Wrap
                };
                stack.Children.Add(descText);
            }

            btn.Content = stack;
            btn.Click += ChoiceOption_Click;
            NpcChoicesPanel.Children.Add(btn);
        }
    }

    private void ChoiceOption_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string key })
        {
            var resolved = _stroll.ResolveChoice(_cityId, key);
            _kernel.Save();
            DisplayResult(resolved);
        }
    }

    private void BuyOffer_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: long itemId })
        {
            var resolved = _stroll.ResolveChoice(_cityId, "buy", itemId.ToString());
            _kernel.Save();
            DisplayResult(resolved);
        }
    }

    private void DeclineMerchant_Click(object sender, RoutedEventArgs e)
    {
        var resolved = _stroll.ResolveChoice(_cityId, "decline");
        _kernel.Save();
        DisplayResult(resolved);
    }

    private void UpdateVitalsBar(bool requiresChoice = false)
    {
        if (_kernel.Capabilities.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var attrs) && attrs is not null)
        {
            var v = attrs.Vitals;
            var isTired = v.Fatigue >= 90;
            var isWarning = v.Fatigue >= 60;
            VitalsStatusText.Text = $"❤️ Sinh Lực: {v.Health}/{v.MaxHp}  |  ⚡ Thể Lực: {v.CurrentStamina}/{v.MaxStamina}  |  💤 Mệt Mỏi: {v.Fatigue}%  |  🌪️ Căng Thẳng: {v.Stress}%";
            VitalsStatusText.Foreground = (Brush)(isTired ? FindResource("Loss") : isWarning ? FindResource("Accent") : FindResource("Gain"));

            if (requiresChoice)
            {
                ContinueStrollBtn.IsEnabled = false;
                ContinueStrollBtn.ToolTip = "Hãy giải quyết tương tác kỳ ngộ bên trên trước khi dạo tiếp!";
            }
            else if (v.Fatigue >= 95)
            {
                ContinueStrollBtn.IsEnabled = false;
                ContinueStrollBtn.ToolTip = "Bạn đang quá kiệt sức (Mệt mỏi 95%+). Hãy nghỉ ngơi trước khi dạo phố tiếp!";
            }
            else
            {
                ContinueStrollBtn.IsEnabled = true;
                ContinueStrollBtn.ToolTip = "Dạo thêm một bước phố phường (tiêu hao 1 ngày, 5 mệt mỏi, giải tỏa 12 căng thẳng)";
            }
        }
    }

    private void AnimateStoryEntrance()
    {
        var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(0.2, 1.0, TimeSpan.FromMilliseconds(220));
        StoryStack.BeginAnimation(OpacityProperty, fadeIn);
    }

    private Border CreateRewardPill(string text)
    {
        var color = text.Contains("Quan") ? Color.FromRgb(0xF5, 0xD0, 0x61)
            : text.Contains("Danh") || text.Contains("Uy Danh") ? Color.FromRgb(0xE6, 0x7E, 0x22)
            : text.Contains("Đức") ? Color.FromRgb(0x5A, 0xC0, 0x8A)
            : text.Contains("EXP") || text.Contains("Kỹ Năng") ? Color.FromRgb(0x4A, 0x90, 0xE2)
            : text.Contains("Căng thẳng") ? Color.FromRgb(0x27, 0xAE, 0x60)
            : Color.FromRgb(0x8A, 0x97, 0xA8);

        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x28, color.R, color.G, color.B)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, color.R, color.G, color.B)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(6, 2, 6, 2),
            Margin = new Thickness(0, 0, 6, 2)
        };
        border.Child = new TextBlock
        {
            Text = text,
            FontSize = 10.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(color)
        };
        return border;
    }

    public static (string Glyph, string CategoryVi, Color Color) GetEventMeta(StrollEventType type) => type switch
    {
        StrollEventType.KarmaFruition => ("⚖️", "Quả Báo & Nhân Duyên", Color.FromRgb(0xF5, 0xD0, 0x61)),
        StrollEventType.NpcEncounter => ("👥", "Kỳ Ngộ Danh Sĩ", Color.FromRgb(0x4A, 0x90, 0xE2)),
        StrollEventType.MarketBargain => ("💰", "Kỳ Thương Du Phương", Color.FromRgb(0xE6, 0x7E, 0x22)),
        StrollEventType.RighteousDeed => ("⚔️", "Trượng Nghĩa Hiệp Hành", Color.FromRgb(0xC0, 0x39, 0x2B)),
        StrollEventType.Epiphany => ("⚡", "Đốn Ngộ Binh Pháp", Color.FromRgb(0x8E, 0x44, 0xAD)),
        StrollEventType.TeahouseRumor => ("🍵", "Trà Quán Luận Thế", Color.FromRgb(0x16, 0xA0, 0x85)),
        _ => ("🌸", "Phố Phường Thanh Bình", Color.FromRgb(0x5A, 0xC0, 0x8A))
    };

    private void ContinueStroll_Click(object sender, RoutedEventArgs e)
    {
        var nextResult = _stroll.Stroll(_cityId);
        _kernel.Save();
        DisplayResult(nextResult);
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }
}
