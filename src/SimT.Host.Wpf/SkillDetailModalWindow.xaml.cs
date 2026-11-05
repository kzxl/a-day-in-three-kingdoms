using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SimT.Contracts.Playing;

namespace SimT.Host.Wpf;

public partial class SkillDetailModalWindow : Window
{
    private static readonly (int Level, string Title, string Desc)[] AllRanks =
    [
        (1, "Sơ Học", "Mới bắt đầu làm quen khẩu quyết, chiêu thức còn vụng về bỡ ngỡ."),
        (2, "Nhập Môn", "Khí tức sơ khai lưu chuyển, nắm bắt được quy tắc và yếu lĩnh cơ bản."),
        (3, "Hạ Thủ", "Chiêu thức xuất chiêu ổn định, thi triển tương đối thuần thục."),
        (4, "Thành Thục", "Phản xạ tự nhiên trong chiến đấu, không còn gượng gạo trước hiểm cảnh."),
        (5, "Tiểu Thành", "Đột phá tiểu cảnh giới, uy lực phát huy vượt trội so với người thường."),
        (6, "Tinh Thông", "Biến hóa khôn lường, dung hợp yếu quyết vào từng cử chỉ hành động."),
        (7, "Đại Thành", "Danh trấn một phương, đạt cảnh giới bậc thầy khiến đối phương dè chừng."),
        (8, "Xuất Thần", "Võ học xuất thần nhập hóa, danh vọng lẫy lừng khắp các châu quận."),
        (9, "Đăng Phong", "Đăng phong tạo cực, chạm tới đỉnh cao võ học nghìn người có một."),
        (10, "Hóa Cảnh", "Phản phác quy chân, đạt tới cảnh giới huyền thoại danh tướng vô song.")
    ];

    private readonly NpcSkillDef _skill;
    private readonly NpcDef? _ownerNpc;
    private readonly bool _isProtagonist;

    public SkillDetailModalWindow(NpcSkillDef skill, NpcDef? ownerNpc = null, bool isProtagonist = false)
    {
        InitializeComponent();
        DarkTitleBar.Apply(this);

        _skill = skill;
        _ownerNpc = ownerNpc;
        _isProtagonist = isProtagonist;

        PopulateSkillDetails();
    }

    private void PopulateSkillDetails()
    {
        // 1. Icon & Category
        var (catName, glyph, catBrush) = _skill.Category switch
        {
            "martial" => ("Võ Đạo", "⚔️", (Brush)new SolidColorBrush(Color.FromRgb(0xE0, 0x6C, 0x75))),
            "strategist" => ("Mưu Lược", "🧠", (Brush)new SolidColorBrush(Color.FromRgb(0x4D, 0x96, 0xFF))),
            "governance" => ("Trị Quốc", "📜", (Brush)new SolidColorBrush(Color.FromRgb(0x5A, 0xC0, 0x8A))),
            "commerce" => ("Thương Mại", "💰", (Brush)Application.Current.FindResource("Accent")),
            _ => ("Kỳ Tài", "✨", (Brush)new SolidColorBrush(Color.FromRgb(0xBD, 0x93, 0xF9)))
        };

        IconGlyphText.Text = glyph;
        CategoryTagSmall.Text = catName.ToUpperInvariant();

        SkillNameText.Text = _skill.NameVi;
        SkillCodeText.Text = $"MÃ HIỆU: #{_skill.Code.ToUpperInvariant()}";

        CategoryText.Text = $"📂 {catName}";
        CategoryText.Foreground = catBrush;
        CategoryBadge.BorderBrush = catBrush;

        TierStarsText.Text = $"{new string('★', Math.Clamp(_skill.Tier, 1, 5))} (Phẩm cấp {_skill.Tier}/5)";

        var rankTitle = SkillRankTitles.ForLevel(_skill.Level);
        LevelRankText.Text = $"Cấp {_skill.Level} · {rankTitle}";

        // 2. Texts
        DescriptionText.Text = _skill.Description;
        EffectSummaryText.Text = _skill.EffectSummary;

        // 3. 10-Tier Ladder
        CurrentLevelIndicatorText.Text = $"Đang Đạt: Cấp {_skill.Level}/10 ({rankTitle})";
        LevelProgressBar.Value = Math.Clamp(_skill.Level, 1, 10);

        LevelLadderPanel.Children.Clear();
        foreach (var (lvl, title, desc) in AllRanks)
        {
            var isCurrent = lvl == _skill.Level;
            var isAchieved = lvl <= _skill.Level;

            var rowBorder = new Border
            {
                Background = isCurrent
                    ? (Brush)Application.Current.FindResource("AccentSoft")
                    : (Brush)Application.Current.FindResource("Surface"),
                BorderBrush = isCurrent
                    ? (Brush)Application.Current.FindResource("Accent")
                    : (Brush)Application.Current.FindResource("Line"),
                BorderThickness = new Thickness(isCurrent ? 1.5 : 1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 0, 0, 4)
            };

            var dock = new DockPanel();

            // Right tag: Indicator
            if (isCurrent)
            {
                var curTag = new Border
                {
                    Background = (Brush)Application.Current.FindResource("Accent"),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(6, 1, 6, 1),
                    VerticalAlignment = VerticalAlignment.Center
                };
                curTag.Child = new TextBlock
                {
                    Text = "✦ CẢNH GIỚI HIỆN TẠI",
                    FontSize = 9.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = (Brush)Application.Current.FindResource("Bg")
                };
                DockPanel.SetDock(curTag, Dock.Right);
                dock.Children.Add(curTag);
            }
            else if (isAchieved)
            {
                var passText = new TextBlock
                {
                    Text = "✓ Đã lĩnh hội",
                    FontSize = 10,
                    Foreground = (Brush)Application.Current.FindResource("Gain"),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 0, 0, 0)
                };
                DockPanel.SetDock(passText, Dock.Right);
                dock.Children.Add(passText);
            }

            var leftStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            var lvlBadge = new TextBlock
            {
                Text = $"Cấp {lvl:D2}",
                FontWeight = FontWeights.Bold,
                FontSize = 11.5,
                Foreground = isCurrent
                    ? (Brush)Application.Current.FindResource("Accent")
                    : isAchieved
                        ? (Brush)Application.Current.FindResource("Text")
                        : (Brush)Application.Current.FindResource("Faint"),
                Width = 50
            };
            leftStack.Children.Add(lvlBadge);

            var titleBlock = new TextBlock
            {
                Text = title,
                FontWeight = FontWeights.SemiBold,
                FontSize = 11.5,
                Foreground = isCurrent
                    ? (Brush)Application.Current.FindResource("Accent")
                    : (Brush)Application.Current.FindResource("Text"),
                Width = 85
            };
            leftStack.Children.Add(titleBlock);

            var descBlock = new TextBlock
            {
                Text = desc,
                FontSize = 11,
                Foreground = (Brush)Application.Current.FindResource(isCurrent ? "Text" : "Muted"),
                VerticalAlignment = VerticalAlignment.Center
            };
            leftStack.Children.Add(descBlock);

            dock.Children.Add(leftStack);
            rowBorder.Child = dock;
            LevelLadderPanel.Children.Add(rowBorder);
        }

        // 4. Owner context
        if (_ownerNpc != null)
        {
            OwnerContextBorder.Visibility = Visibility.Visible;
            OwnerGlyphText.Text = _isProtagonist ? "👑" : "👤";
            OwnerNameText.Text = _isProtagonist ? $"{_ownerNpc.NameVi} (Nhân Vật Chính)" : $"{_ownerNpc.NameVi} ({_ownerNpc.Role})";
            OwnerDetailText.Text = $"Võ Lực: {_ownerNpc.Attributes.War} · Thống Soái: {_ownerNpc.Attributes.Ldr} · Trí Tuệ: {_ownerNpc.Attributes.Int} · Chính Trị: {_ownerNpc.Attributes.Pol}";

            MentorshipTipText.Text = _isProtagonist
                ? "💡 Tuyệt kỹ này đã được bạn lĩnh hội và kích hoạt. Hãy rèn luyện hoặc đối kháng thường xuyên để nâng cao độ thuần thục thăng cấp cảnh giới."
                : $"💡 Bạn có thể tiếp cận {_ownerNpc.NameVi} tại các đại thành trì, kết giao thiện cảm hoặc bái sư để thỉnh giáo bí kíp này.";
        }
        else
        {
            OwnerContextBorder.Visibility = Visibility.Collapsed;
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
