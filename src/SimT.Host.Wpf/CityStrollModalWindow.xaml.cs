using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SimT.Contracts.Playing;
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

    public CityStrollModalWindow(SimKernel kernel, ICityStroll stroll, long cityId, StrollResult? initialResult = null)
    {
        InitializeComponent();
        DarkTitleBar.Apply(this);

        _kernel = kernel;
        _stroll = stroll;
        _cityId = cityId;

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
        DateCounterText.Text = $"Ngày {_kernel.Today.DayIndex} ({_kernel.Today.Month}/{_kernel.Today.Year})";

        NarrativeText.Text = result.Narrative;

        // Reward pills
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

        // Vitals status
        UpdateVitalsBar();

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

        // Animation transition
        AnimateStoryEntrance();
    }

    private void UpdateVitalsBar()
    {
        if (_kernel.Capabilities.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var attrs) && attrs is not null)
        {
            var v = attrs.Vitals;
            var isTired = v.Fatigue >= 90;
            VitalsStatusText.Text = $"❤️ Sinh Lực: {v.Health}/{v.MaxHp} · ⚡ Thể Lực: {v.CurrentStamina}/{v.MaxStamina} · 💤 Mệt Mỏi: {v.Fatigue}% · 🌪️ Áp Lực: {v.Stress}%";
            VitalsStatusText.Foreground = (Brush)(isTired ? FindResource("Loss") : FindResource("Muted"));

            if (v.Fatigue >= 95)
            {
                ContinueStrollBtn.IsEnabled = false;
                ContinueStrollBtn.ToolTip = "Bạn đang quá kiệt sức (Mệt mỏi 95%+). Hãy nghỉ ngơi trước khi dạo phố tiếp!";
            }
            else
            {
                ContinueStrollBtn.IsEnabled = true;
                ContinueStrollBtn.ToolTip = "Dạo thêm một bước phố phường (tiêu hao 5 mệt mỏi, giảm 12 căng thẳng)";
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
        StrollEventType.MarketBargain => ("💰", "Kỳ Thương Cổ Vật", Color.FromRgb(0xE6, 0x7E, 0x22)),
        StrollEventType.RighteousDeed => ("⚔️", "Trượng Nghĩa Hiệp Hành", Color.FromRgb(0xC0, 0x39, 0x2B)),
        StrollEventType.Epiphany => ("⚡", "Đốn Ngộ Binh Pháp", Color.FromRgb(0x8E, 0x44, 0xAD)),
        StrollEventType.TeahouseRumor => ("🍵", "Trà Quán Luận Thế", Color.FromRgb(0x16, 0xA0, 0x85)),
        _ => ("🌸", "Tiêu Dao Tự Tại", Color.FromRgb(0x5A, 0xC0, 0x8A))
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
