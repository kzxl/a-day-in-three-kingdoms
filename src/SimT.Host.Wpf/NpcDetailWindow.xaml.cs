using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SimT.Contracts.Playing;

namespace SimT.Host.Wpf;

public partial class NpcDetailWindow : Window
{
    private long _npcId;

    public Action<long>? OnChallengeDuel { get; set; }

    public NpcDetailWindow(NpcDef npc, string citadelName, NpcStatDelta? deltas = null, NpcActivityDef? activity = null, RelationshipDef? relationship = null, RetainerDef? retainer = null)
    {
        InitializeComponent();
        DarkTitleBar.Apply(this);

        Populate(npc, citadelName, deltas, activity, relationship, retainer);
    }

    public static void ShowNpc(Window owner, NpcDef npc, string citadelName, NpcStatDelta? deltas = null, NpcActivityDef? activity = null, RelationshipDef? relationship = null, RetainerDef? retainer = null, Action<long>? onChallengeDuel = null)
    {
        var window = new NpcDetailWindow(npc, citadelName, deltas, activity, relationship, retainer)
        {
            Owner = owner,
            OnChallengeDuel = onChallengeDuel
        };
        if (onChallengeDuel is not null && npc.Attributes.War >= 30)
        {
            window.DuelChallengeButton.Visibility = Visibility.Visible;
        }
        window.ShowDialog();
    }

    private void Populate(NpcDef npc, string citadelName, NpcStatDelta? deltas, NpcActivityDef? activity, RelationshipDef? relationship, RetainerDef? retainer)
    {
        Title = $"{npc.NameVi} — Hồ Sơ Nhân Vật";
        _npcId = npc.Id;

        // Avatar char: first letter of Han name or Vietnamese name
        var avatarChar = !string.IsNullOrWhiteSpace(npc.NameHan) && npc.NameHan.Length > 0
            ? npc.NameHan[0].ToString()
            : npc.NameVi.Length > 0 ? npc.NameVi[0].ToString() : "人";
        AvatarCharText.Text = avatarChar;

        RoleBadgeSmall.Text = DetermineRoleTag(npc.Role);

        NameText.Text = npc.NameVi;
        HanNameText.Text = string.IsNullOrWhiteSpace(npc.NameHan) ? "" : npc.NameHan;
        StyleNameText.Text = string.IsNullOrWhiteSpace(npc.StyleNameVi) ? "" : $"Tự: {npc.StyleNameVi}";

        RoleText.Text = npc.Role;
        CitadelText.Text = $"Ngụ tại: {citadelName}";
        PersonalityText.Text = string.IsNullOrWhiteSpace(npc.Personality)
            ? "Khí chất bình ổn, chưa bộc lộ dã tâm."
            : $"Khí chất: {npc.Personality}";

        if (activity is not null)
        {
            DynamicLivingPanel.Visibility = Visibility.Visible;
            WealthText.Text = $"💰 {activity.CurrentWealth:N0} Quan";
            ReputationText.Text = $"★ Uy danh {activity.Reputation}";
            ActionText.Text = $"⚡ {activity.CurrentAction}";
        }
        else
        {
            DynamicLivingPanel.Visibility = Visibility.Collapsed;
        }

        if (relationship is not null)
        {
            SocialKizunaPanel.Visibility = Visibility.Visible;
            var affSign = relationship.Affinity >= 0 ? "+" : "";
            AffinityText.Text = $"♥ Hảo cảm: {affSign}{relationship.Affinity}/100 ({relationship.Impression})";
            if (retainer is not null)
            {
                RetainerText.Text = $"🛡️ Gia thần: {retainer.RoleAssigned} (Trung thành {retainer.Loyalty}/100)";
            }
            else
            {
                RetainerText.Text = relationship.Affinity >= 35 ? "🤝 Có thể chiêu mộ" : "Chưa chiêu mộ";
            }
        }
        else
        {
            SocialKizunaPanel.Visibility = Visibility.Collapsed;
        }

        BioText.Text = string.IsNullOrWhiteSpace(npc.BioVi)
            ? "Nhân sĩ danh tiếng thời Tam Quốc, hành trạng được dân gian ca tụng."
            : npc.BioVi;

        var a = npc.Attributes;
        var bonusTotal = deltas?.TotalBonus ?? 0;
        var total = a.War + a.Ldr + a.Int + a.Pol + a.Dip + a.Com + a.Log + a.Cra + a.Con + a.Wil + a.Prc + a.Lck + bonusTotal;
        TotalStatsText.Text = total.ToString();

        StatRadarChart.SetCoreStats(a, deltas);
        ArchetypeTagText.Text = StatRadarChart.ArchetypeTitle;

        // 8 Core Stats (with growth bonuses if available)
        CoreStatsGrid.Children.Clear();
        AddStatItem(CoreStatsGrid, "WAR · Võ Lực", a.War, deltas?.WarDelta ?? 0, "Khả năng chiến đấu, trảm tướng phá trận");
        AddStatItem(CoreStatsGrid, "LDR · Thống Soái", a.Ldr, deltas?.LdrDelta ?? 0, "Khả năng chỉ huy quân đội, giữ vững đội hình");
        AddStatItem(CoreStatsGrid, "INT · Trí Tuệ", a.Int, deltas?.IntDelta ?? 0, "Mưu lược quân sự, bày binh bố trận");
        AddStatItem(CoreStatsGrid, "POL · Chính Trị", a.Pol, deltas?.PolDelta ?? 0, "Quản lý kinh bang tế thế, phát triển thành thị");
        AddStatItem(CoreStatsGrid, "DIP · Ngoại Giao", a.Dip, deltas?.DipDelta ?? 0, "Du thuyết liên minh, thu phục lòng người");
        AddStatItem(CoreStatsGrid, "COM · Thương Mại", a.Com, deltas?.ComDelta ?? 0, "Đàm phán giá cả, kinh doanh sinh lời");
        AddStatItem(CoreStatsGrid, "LOG · Hậu Cần", a.Log, deltas?.LogDelta ?? 0, "Vận tải lương thảo, quản lý kho tàng");
        AddStatItem(CoreStatsGrid, "CRA · Kỹ Nghệ", a.Cra, deltas?.CraDelta ?? 0, "Chế tạo khí giới, xây đắp công trình");

        // 4 Innate Stats
        InnateStatsGrid.Children.Clear();
        AddStatItem(InnateStatsGrid, "CON · Căn Cốt", a.Con, 0, "Thể phách, quyết định sinh lực & thể lực tối đa");
        AddStatItem(InnateStatsGrid, "WIL · Định Lực", a.Wil, 0, "Ý chí kiên định, chống hoảng loạn & giảm stress");
        AddStatItem(InnateStatsGrid, "PRC · Tuệ Nhãn", a.Prc, 0, "Cơ biến, giám định hàng hóa & phát giác phục kích");
        AddStatItem(InnateStatsGrid, "LCK · Khí Vận", a.Lck, 0, "Phúc duyên trời ban, bạo kích kỳ ngộ thương trường");

        // Character Skills & Feats
        SkillsListPanel.Children.Clear();
        SkillCountText.Text = npc.Skills.Count > 0 ? $"{npc.Skills.Count} tuyệt kỹ" : "Chưa khai mở";

        if (npc.Skills.Count == 0)
        {
            SkillsListPanel.Children.Add(new TextBlock
            {
                Text = "Nhân sĩ này chưa bộc lộ đặc kỹ danh tướng riêng biệt.",
                Style = (Style)FindResource("Caption"),
                Foreground = (Brush)FindResource("Muted"),
                Margin = new Thickness(4, 2, 4, 4)
            });
        }
        else
        {
            foreach (var skill in npc.Skills)
            {
                SkillsListPanel.Children.Add(BuildSkillCard(skill));
            }
        }
    }

    private UIElement BuildSkillCard(NpcSkillDef skill)
    {
        var card = new Border
        {
            Background = (Brush)FindResource("SurfaceAlt"),
            BorderBrush = (Brush)FindResource("Line"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 0, 0, 8)
        };

        var stack = new StackPanel();

        var headerDock = new DockPanel();

        // Right side: Category badge + stars
        var badgeStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(badgeStack, Dock.Right);

        var stars = new string('★', Math.Clamp(skill.Tier, 1, 5));
        badgeStack.Children.Add(new TextBlock
        {
            Text = stars,
            Foreground = (Brush)FindResource("Gain"),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        });

        var catName = skill.Category switch
        {
            "martial" => "Võ Đạo",
            "strategist" => "Mưu Lược",
            "governance" => "Trị Quốc",
            "commerce" => "Thương Mại",
            _ => "Kỳ Tài"
        };
        var catBorder = new Border
        {
            Background = (Brush)FindResource("Surface"),
            BorderBrush = (Brush)FindResource("Line"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2, 6, 2)
        };
        catBorder.Child = new TextBlock
        {
            Text = catName,
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("Accent")
        };
        badgeStack.Children.Add(catBorder);

        headerDock.Children.Add(badgeStack);

        // Left side: Skill Name
        headerDock.Children.Add(new TextBlock
        {
            Text = skill.NameVi,
            FontSize = 13.5,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("Text")
        });

        stack.Children.Add(headerDock);

        // Description
        stack.Children.Add(new TextBlock
        {
            Text = skill.Description,
            Style = (Style)FindResource("Caption"),
            Foreground = (Brush)FindResource("Muted"),
            Margin = new Thickness(0, 4, 0, 2),
            TextWrapping = TextWrapping.Wrap
        });

        // Effect Summary tag
        var effectBorder = new Border
        {
            Background = (Brush)FindResource("AccentSoft"),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(6, 2, 6, 2),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 4, 0, 0)
        };
        effectBorder.Child = new TextBlock
        {
            Text = $"⚡ Hiệu Quả: {skill.EffectSummary}",
            FontSize = 10.5,
            FontWeight = FontWeights.Medium,
            Foreground = (Brush)FindResource("Accent")
        };
        stack.Children.Add(effectBorder);

        card.Child = stack;
        return card;
    }

    private void AddStatItem(Panel container, string label, int baseValue, int bonus, string tip)
    {
        var effective = baseValue + bonus;
        var rank = effective switch
        {
            >= 95 => "S+",
            >= 90 => "S",
            >= 80 => "A",
            >= 70 => "B",
            >= 60 => "C",
            _ => "D"
        };

        var barBrush = (Brush)(effective >= 90
            ? FindResource("Accent")
            : effective >= 80
                ? FindResource("Gain")
                : effective >= 70
                    ? new SolidColorBrush(Color.FromRgb(0x4A, 0x90, 0xE2))
                    : FindResource("Muted"));

        var card = new Border
        {
            Background = (Brush)FindResource("SurfaceAlt"),
            BorderBrush = (Brush)FindResource("Line"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(4, 4, 4, 4),
            ToolTip = tip
        };

        var stack = new StackPanel();

        // Line 1: Label + Rank + Value (+Bonus)
        var line = new DockPanel();

        var valueStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(valueStack, Dock.Right);

        var valueText = new TextBlock
        {
            Text = effective.ToString(),
            Style = (Style)FindResource("Number"),
            FontWeight = FontWeights.Bold,
            Foreground = barBrush,
            FontSize = 14
        };
        valueStack.Children.Add(valueText);

        if (bonus > 0)
        {
            var bonusText = new TextBlock
            {
                Text = $" (+{bonus})",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("Gain"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(3, 0, 0, 0)
            };
            valueStack.Children.Add(bonusText);
        }

        line.Children.Add(valueStack);

        var rankBorder = new Border
        {
            Background = (Brush)FindResource("Surface"),
            BorderBrush = (Brush)FindResource("Line"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(5, 1, 5, 1),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(rankBorder, Dock.Right);
        rankBorder.Child = new TextBlock
        {
            Text = rank,
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            Foreground = barBrush
        };
        line.Children.Add(rankBorder);

        var labelText = new TextBlock
        {
            Text = label,
            Style = (Style)FindResource("Caption"),
            Foreground = (Brush)FindResource("Text"),
            VerticalAlignment = VerticalAlignment.Center
        };
        line.Children.Add(labelText);
        stack.Children.Add(line);

        // Line 2: Progress Track & Fill
        var track = new Border
        {
            Background = (Brush)FindResource("Surface"),
            BorderBrush = (Brush)FindResource("Line"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2),
            Height = 6,
            Margin = new Thickness(0, 6, 0, 0)
        };

        var fillGrid = new Grid();
        var ratio = Math.Clamp(effective / 120.0, 0.05, 1.0);
        fillGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ratio, GridUnitType.Star) });
        fillGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0 - ratio, GridUnitType.Star) });

        var bar = new Border
        {
            Background = barBrush,
            CornerRadius = new CornerRadius(1)
        };
        Grid.SetColumn(bar, 0);
        fillGrid.Children.Add(bar);

        track.Child = fillGrid;
        stack.Children.Add(track);

        card.Child = stack;
        container.Children.Add(card);
    }

    private static string DetermineRoleTag(string role)
    {
        var r = role.ToLowerInvariant();
        if (r.Contains("tướng") || r.Contains("quân") || r.Contains("soái")) return "VÕ";
        if (r.Contains("sư") || r.Contains("mưu") || r.Contains("học")) return "VĂN";
        if (r.Contains("thương") || r.Contains("buôn") || r.Contains("bản")) return "THƯƠNG";
        if (r.Contains("chủ") || r.Contains("vương") || r.Contains("đế")) return "CHÚA";
        return "SĨ";
    }

    private void DuelChallenge_Click(object sender, RoutedEventArgs e)
    {
        var id = _npcId;
        Close();
        OnChallengeDuel?.Invoke(id);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }
}
