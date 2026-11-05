using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SimT.Contracts.Playing;

namespace SimT.Host.Wpf.Controls;

/// <summary>
/// Hardware-accelerated interactive Path of Exile style Passive Skill Tree Canvas Control.
/// Visualizes the Bách Gia Thiên Mệnh 31-node constellation web radiating across 6 philosophical schools,
/// active golden resonance paths, interactive node allocation, points economics, and detailed inspector.
/// </summary>
public sealed partial class GameDestinyTreeControl : Border
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    private readonly IPlayerDestiny _destiny;
    private string? _selectedNodeId;

    private readonly DestinyTreeCanvas _canvas;
    private readonly TextBlock _universalPointsText;
    private readonly TextBlock _spentPointsText;
    private readonly StackPanel _resonancePillsPanel;

    // Inspector elements
    private readonly TextBlock _inspNameText;
    private readonly Border _inspSchoolBadge;
    private readonly TextBlock _inspSchoolText;
    private readonly Border _inspTierBadge;
    private readonly TextBlock _inspTierText;
    private readonly TextBlock _inspEffectText;
    private readonly TextBlock _inspPrereqText;
    private readonly Button _inspLearnBtn;
    private readonly Border _inspStatusBadge;
    private readonly TextBlock _inspStatusText;

    public event Action<string>? LearnRequested;
    public event Action? RespecRequested;
    public event Action? EvaluateRequested;

    public GameDestinyTreeControl(IPlayerDestiny destiny)
    {
        _destiny = destiny;
        var state = destiny.State;
        _canvas = new DestinyTreeCanvas(destiny);
        _canvas.NodeSelected += OnNodeSelected;
        _canvas.NodeDoubleClicked += OnNodeDoubleClicked;

        Background = (Brush)Application.Current.FindResource("Surface");
        BorderBrush = (Brush)Application.Current.FindResource("Line");
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(8);
        Padding = new Thickness(16, 14, 16, 14);
        Margin = new Thickness(0, 0, 0, 16);

        var mainStack = new StackPanel();

        // 1. Header: Title, Controls, and Points Economy
        var headerDock = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };

        var actionBtns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        
        var evalBtn = new Button
        {
            Content = "⚡ Cập Nhật Kỳ Ngộ",
            Style = (Style)Application.Current.FindResource("GhostButton"),
            Margin = new Thickness(0, 0, 6, 0),
            Padding = new Thickness(10, 5, 10, 5),
            ToolTip = "Kiểm tra toàn bộ mốc chiến tích thiên hạ để kết toán thêm điểm thiên mệnh"
        };
        evalBtn.Click += (_, _) => EvaluateRequested?.Invoke();
        actionBtns.Children.Add(evalBtn);

        var respecBtn = new Button
        {
            Content = "↺ Tẩy Tủy (Respec)",
            Style = (Style)Application.Current.FindResource("GhostButton"),
            Margin = new Thickness(0, 0, 6, 0),
            Padding = new Thickness(10, 5, 10, 5),
            ToolTip = "Hoàn trả toàn bộ bí pháp đã học về ngân sách điểm thiên mệnh"
        };
        respecBtn.Click += (_, _) => RespecRequested?.Invoke();
        actionBtns.Children.Add(respecBtn);

        var resetViewBtn = new Button
        {
            Content = "🔍 Căn Giữa",
            Style = (Style)Application.Current.FindResource("GhostButton"),
            Padding = new Thickness(10, 5, 10, 5),
            ToolTip = "Đặt lại tọa độ góc nhìn và phóng to về trung tâm Hư Tâm Nhập Thế"
        };
        resetViewBtn.Click += (_, _) => _canvas.ResetView();
        actionBtns.Children.Add(resetViewBtn);

        DockPanel.SetDock(actionBtns, Dock.Right);
        headerDock.Children.Add(actionBtns);

        var titleStack = new StackPanel();
        titleStack.Children.Add(new TextBlock
        {
            Text = "☯ BÁCH GIA THIÊN MỆNH · KHÍ VẬN ĐỒ HÌNH (PASSIVE TALENT WEB)",
            Style = (Style)Application.Current.FindResource("Overline")
        });
        titleStack.Children.Add(new TextBlock
        {
            Text = "Mạng Lưới 43 Bí Pháp Bị Động Khai Mở Khí Vận 6 Đại Học Phái Bách Gia",
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)Application.Current.FindResource("Text"),
            Margin = new Thickness(0, 2, 0, 0)
        });
        headerDock.Children.Add(titleStack);
        mainStack.Children.Add(headerDock);

        // 2. Points Economy Dashboard Strip
        var dashCard = new Border
        {
            Background = (Brush)Application.Current.FindResource("Rail"),
            BorderBrush = (Brush)Application.Current.FindResource("Line"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 0, 10)
        };
        var dashDock = new DockPanel();

        var rightHint = new TextBlock
        {
            Text = "✦ Cuộn chuột để Thu/Phóng · Kéo chuột để Di chuyển · Bấm điểm để Lĩnh hội",
            Style = (Style)Application.Current.FindResource("Caption"),
            Foreground = (Brush)Application.Current.FindResource("Faint"),
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(rightHint, Dock.Right);
        dashDock.Children.Add(rightHint);

        var pointsStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        _universalPointsText = new TextBlock
        {
            Text = $"✦ Điểm Tự Do: {state.UniversalPointsAvailable}",
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)Application.Current.FindResource("Accent"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        };
        pointsStack.Children.Add(_universalPointsText);

        _spentPointsText = new TextBlock
        {
            Text = $"Đã Dùng: {state.PointsSpent} / {state.TotalPointsEarned} Điểm",
            FontSize = 11.5,
            Foreground = (Brush)Application.Current.FindResource("Muted"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 14, 0)
        };
        pointsStack.Children.Add(_spentPointsText);

        _resonancePillsPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        PopulateResonancePills(state);
        pointsStack.Children.Add(_resonancePillsPanel);

        dashDock.Children.Add(pointsStack);
        dashCard.Child = dashDock;
        mainStack.Children.Add(dashCard);

        // 3. Hardware-Accelerated Interactive Canvas
        var canvasFrame = new Border
        {
            BorderBrush = (Brush)Application.Current.FindResource("Line"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            ClipToBounds = true,
            Child = _canvas
        };
        mainStack.Children.Add(canvasFrame);

        // 4. Selected Node Inspector Panel (PoE-Style Tooltip Box)
        var inspBorder = new Border
        {
            Background = (Brush)Application.Current.FindResource("Rail"),
            BorderBrush = (Brush)Application.Current.FindResource("Line"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14, 12, 14, 12),
            Margin = new Thickness(0, 10, 0, 0)
        };

        var inspDock = new DockPanel();

        // Right side: Action Button / Status Badge
        var actionPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
        _inspLearnBtn = new Button
        {
            Content = "✦ Lĩnh Hội Bí Pháp",
            Style = (Style)Application.Current.FindResource("PrimaryButton"),
            Padding = new Thickness(16, 8, 16, 8),
            FontWeight = FontWeights.Bold,
            Visibility = Visibility.Collapsed
        };
        _inspLearnBtn.Click += (_, _) =>
        {
            if (!string.IsNullOrEmpty(_selectedNodeId))
            {
                LearnRequested?.Invoke(_selectedNodeId);
            }
        };
        actionPanel.Children.Add(_inspLearnBtn);

        _inspStatusBadge = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 6, 10, 6),
            VerticalAlignment = VerticalAlignment.Center
        };
        _inspStatusText = new TextBlock
        {
            FontSize = 11.5,
            FontWeight = FontWeights.SemiBold
        };
        _inspStatusBadge.Child = _inspStatusText;
        actionPanel.Children.Add(_inspStatusBadge);

        DockPanel.SetDock(actionPanel, Dock.Right);
        inspDock.Children.Add(actionPanel);

        // Left side: Detailed Node Information
        var infoStack = new StackPanel();

        var headerLine = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
        _inspNameText = new TextBlock
        {
            Text = "Chọn một bí pháp trên đồ hình để xem chi tiết",
            FontSize = 15,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)Application.Current.FindResource("Text"),
            VerticalAlignment = VerticalAlignment.Center
        };
        headerLine.Children.Add(_inspNameText);

        _inspSchoolBadge = new Border
        {
            CornerRadius = new CornerRadius(3),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 1, 6, 1),
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed
        };
        _inspSchoolText = new TextBlock { FontSize = 10.5, FontWeight = FontWeights.SemiBold };
        _inspSchoolBadge.Child = _inspSchoolText;
        headerLine.Children.Add(_inspSchoolBadge);

        _inspTierBadge = new Border
        {
            CornerRadius = new CornerRadius(3),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 1, 6, 1),
            Margin = new Thickness(6, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed
        };
        _inspTierText = new TextBlock { FontSize = 10.5, FontWeight = FontWeights.SemiBold };
        _inspTierBadge.Child = _inspTierText;
        headerLine.Children.Add(_inspTierBadge);

        infoStack.Children.Add(headerLine);

        _inspEffectText = new TextBlock
        {
            Text = "Nhấp chọn bất kỳ điểm bí pháp nào trong 6 nhánh để mở rộng thiên phú nhân vật.",
            FontSize = 12.5,
            Foreground = (Brush)Application.Current.FindResource("Muted"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 3)
        };
        infoStack.Children.Add(_inspEffectText);

        _inspPrereqText = new TextBlock
        {
            Text = string.Empty,
            Style = (Style)Application.Current.FindResource("Caption"),
            Foreground = (Brush)Application.Current.FindResource("Faint")
        };
        infoStack.Children.Add(_inspPrereqText);

        inspDock.Children.Add(infoStack);
        inspBorder.Child = inspDock;
        mainStack.Children.Add(inspBorder);

        Child = mainStack;

        // Auto-select first learnable node or root
        var firstLearnable = destiny.AllNodes.FirstOrDefault(n => !state.LearnedNodeIds.Contains(n.Id) && destiny.CanLearnNode(n.Id, out _));
        _selectedNodeId = firstLearnable?.Id ?? "destiny_root";
        _canvas.SetSelection(_selectedNodeId);
        UpdateInspector();
    }

    private void PopulateResonancePills(DestinyState state)
    {
        _resonancePillsPanel.Children.Clear();
        foreach (var (school, points) in state.ResonancePointsAvailable)
        {
            if (points <= 0) continue;
            var (icon, name, color) = GetSchoolMeta(school);
            var pill = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x28, color.R, color.G, color.B)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, color.R, color.G, color.B)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(5, 1, 5, 1),
                Margin = new Thickness(4, 0, 0, 0)
            };
            pill.Child = new TextBlock
            {
                Text = $"{icon} {points}đ",
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(color),
                ToolTip = $"Điểm Ngộ Đạo chuyên biệt: {name}"
            };
            _resonancePillsPanel.Children.Add(pill);
        }
    }

    private void OnNodeSelected(string nodeId)
    {
        _selectedNodeId = nodeId;
        UpdateInspector();
    }

    private void OnNodeDoubleClicked(string nodeId)
    {
        _selectedNodeId = nodeId;
        UpdateInspector();
        if (_destiny.CanLearnNode(nodeId, out _))
        {
            LearnRequested?.Invoke(nodeId);
        }
    }

    private void UpdateInspector()
    {
        if (string.IsNullOrEmpty(_selectedNodeId)) return;
        var node = _destiny.AllNodes.FirstOrDefault(n => n.Id.Equals(_selectedNodeId, StringComparison.OrdinalIgnoreCase));
        if (node is null) return;

        var state = _destiny.State;
        var isLearned = state.LearnedNodeIds.Contains(node.Id);
        string? lockReason = null;
        var canLearn = !isLearned && _destiny.CanLearnNode(node.Id, out lockReason);

        var (icon, schoolName, schoolColor) = GetSchoolMeta(node.School);

        _inspNameText.Text = $"{icon} {node.NameVi}";

        // School badge
        _inspSchoolBadge.Visibility = Visibility.Visible;
        _inspSchoolBadge.Background = new SolidColorBrush(Color.FromArgb(0x28, schoolColor.R, schoolColor.G, schoolColor.B));
        _inspSchoolBadge.BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, schoolColor.R, schoolColor.G, schoolColor.B));
        _inspSchoolText.Text = schoolName;
        _inspSchoolText.Foreground = new SolidColorBrush(schoolColor);

        // Tier badge
        _inspTierBadge.Visibility = Visibility.Visible;
        var tierStr = node.Tier switch
        {
            DestinyNodeTier.Mastery => "✵ Mastery Đại Tông Sư (Bí Pháp Cực Đỉnh)",
            DestinyNodeTier.Keystone => "✦ Keystone Tông Sư",
            DestinyNodeTier.Notable => "★ Trọng Điểm Chân Truyền",
            DestinyNodeTier.Minor => "Tiểu Tiết Bí Pháp",
            _ => "Khởi Nguyên Gốc"
        };
        var tierColor = node.Tier switch
        {
            DestinyNodeTier.Mastery => Color.FromRgb(0xFF, 0xB7, 0x03),
            DestinyNodeTier.Keystone => Color.FromRgb(0xF5, 0xD0, 0x61),
            DestinyNodeTier.Notable => Color.FromRgb(0x4A, 0x90, 0xE2),
            DestinyNodeTier.Minor => Color.FromRgb(0x8A, 0x97, 0xA8),
            _ => Color.FromRgb(0x5A, 0xC0, 0x8A)
        };
        _inspTierBadge.Background = new SolidColorBrush(Color.FromArgb(0x28, tierColor.R, tierColor.G, tierColor.B));
        _inspTierBadge.BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, tierColor.R, tierColor.G, tierColor.B));
        _inspTierText.Text = tierStr;
        _inspTierText.Foreground = new SolidColorBrush(tierColor);

        // Effect
        _inspEffectText.Text = $"⭐ {node.SummaryVi}";
        _inspEffectText.Foreground = (Brush)Application.Current.FindResource("Text");

        // Requirements & Prereqs
        var prereqNames = node.PrerequisiteIds.Count > 0
            ? string.Join(", ", node.PrerequisiteIds.Select(p => _destiny.AllNodes.FirstOrDefault(x => x.Id == p)?.NameVi ?? p))
            : "Gốc Khởi Đầu (Không Yêu Cầu)";
        var reqSchool = node.RequiredSchoolPoints > 0 ? $"{node.RequiredSchoolPoints}đ môn phái" : "Căn bản";
        _inspPrereqText.Text = $"Chi phí: {node.PointCost} Điểm · Điểm phái yêu cầu: {reqSchool} · Mạch liên kết: {prereqNames}";

        // Status & Action button
        if (isLearned)
        {
            _inspLearnBtn.Visibility = Visibility.Collapsed;
            _inspStatusBadge.Visibility = Visibility.Visible;
            _inspStatusBadge.Background = new SolidColorBrush(Color.FromArgb(0x33, 0x5A, 0xC0, 0x8A));
            _inspStatusBadge.BorderBrush = (Brush)Application.Current.FindResource("Gain");
            _inspStatusText.Text = "✓ Đã Đắc Đạo (Hiệu ứng đang có hiệu lực)";
            _inspStatusText.Foreground = (Brush)Application.Current.FindResource("Gain");
        }
        else if (canLearn)
        {
            _inspLearnBtn.Visibility = Visibility.Visible;
            _inspLearnBtn.Content = $"✦ Lĩnh Hội ({node.PointCost} Điểm)";
            _inspStatusBadge.Visibility = Visibility.Collapsed;
        }
        else
        {
            _inspLearnBtn.Visibility = Visibility.Collapsed;
            _inspStatusBadge.Visibility = Visibility.Visible;
            _inspStatusBadge.Background = new SolidColorBrush(Color.FromArgb(0x33, 0xE0, 0x6C, 0x75));
            _inspStatusBadge.BorderBrush = (Brush)Application.Current.FindResource("Loss");
            _inspStatusText.Text = $"🔒 Chưa Khai Thông: {lockReason ?? "Chưa đủ điều kiện"}";
            _inspStatusText.Foreground = (Brush)Application.Current.FindResource("Loss");
        }
    }

    public static (string Glyph, string Name, Color Color) GetSchoolMeta(DestinySchool school) => school switch
    {
        DestinySchool.Universal => ("☯", "Hư Tâm Nhập Thế", Color.FromRgb(0xF5, 0xD0, 0x61)),
        DestinySchool.DaoChu => ("💰", "Đào Chu Chi Thuật", Color.FromRgb(0xE6, 0x7E, 0x22)),
        DestinySchool.BaDao => ("🌾", "Bá Đạo Pháp Gia", Color.FromRgb(0x27, 0xAE, 0x60)),
        DestinySchool.VuongDao => ("👑", "Vương Đạo Nho Gia", Color.FromRgb(0x29, 0x80, 0xB9)),
        DestinySchool.BinhGia => ("⚔️", "Thiết Huyết Tiêu Cục", Color.FromRgb(0xC0, 0x39, 0x2B)),
        DestinySchool.MacGia => ("⚙️", "Bách Công Kỳ Xảo", Color.FromRgb(0x16, 0xA0, 0x85)),
        DestinySchool.DaoGia => ("🍵", "Ẩn Sĩ Dưỡng Sinh", Color.FromRgb(0x8E, 0x44, 0xAD)),
        _ => ("✦", "Thiên Mệnh", Color.FromRgb(0xD9, 0xA4, 0x41))
    };

}
