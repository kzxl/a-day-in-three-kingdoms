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
public sealed class GameDestinyTreeControl : Border
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
            Text = "Mạng Lưới 31 Bí Pháp Bị Động Khai Mở Khí Vận 6 Đại Học Phái Bách Gia",
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
            DestinyNodeTier.Keystone => "✦ Keystone Tông Sư",
            DestinyNodeTier.Notable => "★ Trọng Điểm Chân Truyền",
            DestinyNodeTier.Minor => "Tiểu Tiết Bí Pháp",
            _ => "Khởi Nguyên Gốc"
        };
        var tierColor = node.Tier switch
        {
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

    /// <summary>
    /// Hardware-accelerated Direct3D / DrawingContext Canvas that plots the constellation passive web.
    /// </summary>
    private sealed class DestinyTreeCanvas : FrameworkElement
    {
        private readonly IPlayerDestiny _destiny;
        private string? _selectedNodeId;
        private string? _hoveredNodeId;

        private double _panX;
        private double _panY;
        private double _zoom = 1.0;
        private bool _isDragging;
        private Point _lastDragPos;

        public event Action<string>? NodeSelected;
        public event Action<string>? NodeDoubleClicked;

        // Static constellation layout dictionary (31 nodes)
        private static readonly Dictionary<string, (double X, double Y)> NodePositions = new(StringComparer.OrdinalIgnoreCase)
        {
            // Center Root (Universal)
            ["destiny_root"] = (0, 0),

            // 1. Đào Chu (East, 0 deg)
            ["daochu_root"] = (90, 0),
            ["daochu_minor_bargain"] = (175, -42),
            ["daochu_minor_margin"] = (175, 42),
            ["daochu_notable_rumor"] = (255, 0),
            ["daochu_keystone_merchant_king"] = (345, 0),

            // 2. Bá Đạo (South, 90 deg)
            ["badao_root"] = (0, 90),
            ["badao_minor_soil"] = (-42, 175),
            ["badao_minor_yield"] = (42, 175),
            ["badao_notable_steward"] = (0, 255),
            ["badao_keystone_military_farm"] = (0, 345),

            // 3. Vương Đạo (West, 180 deg)
            ["vuongdao_root"] = (-90, 0),
            ["vuongdao_minor_fame"] = (-175, -42),
            ["vuongdao_minor_retainer"] = (-175, 42),
            ["vuongdao_notable_court"] = (-255, 0),
            ["vuongdao_keystone_peach_garden"] = (-345, 0),

            // 4. Binh Gia (North, 270 deg)
            ["binhgia_root"] = (0, -90),
            ["binhgia_minor_duel"] = (-42, -175),
            ["binhgia_minor_bandit"] = (42, -175),
            ["binhgia_notable_spoils"] = (0, -255),
            ["binhgia_keystone_warlord"] = (0, -345),

            // 5. Mặc Gia (South-East, 45 deg)
            ["macgia_root"] = (68, 68),
            ["macgia_minor_preserve"] = (160, 95),
            ["macgia_minor_speed"] = (95, 160),
            ["macgia_notable_ice_cellar"] = (195, 195),
            ["macgia_keystone_wooden_ox"] = (265, 265),

            // 6. Đạo Gia (North-West, 225 deg)
            ["daogia_root"] = (-68, -68),
            ["daogia_minor_longevity"] = (-160, -95),
            ["daogia_minor_vitality"] = (-95, -160),
            ["daogia_notable_astronomy"] = (-195, -195),
            ["daogia_keystone_five_animals"] = (-265, -265)
        };

        // Frozen drawing assets
        private static readonly Brush TreeBgBrush = new SolidColorBrush(Color.FromRgb(0x0A, 0x0E, 0x16));
        private static readonly Pen RingGuidePen = new(new SolidColorBrush(Color.FromArgb(0x24, 0x4A, 0x90, 0xE2)), 1.0)
        {
            DashStyle = new DashStyle([4, 4], 0)
        };
        private static readonly Pen AxisGuidePen = new(new SolidColorBrush(Color.FromArgb(0x18, 0x8A, 0x97, 0xA8)), 1.0)
        {
            DashStyle = new DashStyle([6, 6], 0)
        };

        private static readonly Pen EdgeLearnedGlowPen = new(new SolidColorBrush(Color.FromArgb(0x40, 0xF5, 0xD0, 0x61)), 6.0);
        private static readonly Pen EdgeLearnedPen = new(new SolidColorBrush(Color.FromRgb(0xF5, 0xD0, 0x61)), 2.8);
        private static readonly Pen EdgeCanLearnPen = new(new SolidColorBrush(Color.FromRgb(0x4A, 0x90, 0xE2)), 2.0)
        {
            DashStyle = new DashStyle([3, 3], 0)
        };
        private static readonly Pen EdgeInactivePen = new(new SolidColorBrush(Color.FromRgb(0x23, 0x2E, 0x3E)), 1.2);

        private static readonly Pen FocusRingPen = new(new SolidColorBrush(Color.FromRgb(0xF5, 0xD0, 0x61)), 2.5);
        private static readonly Pen UnlockableAuraPen = new(new SolidColorBrush(Color.FromRgb(0x5A, 0xC0, 0x8A)), 2.0)
        {
            DashStyle = new DashStyle([2, 2], 0)
        };

        private static readonly Brush DarkSurfaceBrush = new SolidColorBrush(Color.FromRgb(0x14, 0x1A, 0x26));
        private static readonly Brush LockedNodeBrush = new SolidColorBrush(Color.FromRgb(0x0E, 0x14, 0x1E));
        private static readonly Pen LockedBorderPen = new(new SolidColorBrush(Color.FromRgb(0x24, 0x31, 0x42)), 1.2);

        private static readonly Brush WhiteTextBrush = new SolidColorBrush(Color.FromRgb(0xEA, 0xF0, 0xF8));
        private static readonly Brush GoldTextBrush = new SolidColorBrush(Color.FromRgb(0xF5, 0xD0, 0x61));
        private static readonly Brush MutedTextBrush = new SolidColorBrush(Color.FromRgb(0x75, 0x86, 0x9C));

        static DestinyTreeCanvas()
        {
            TreeBgBrush.Freeze();
            RingGuidePen.Freeze();
            AxisGuidePen.Freeze();
            EdgeLearnedGlowPen.Freeze();
            EdgeLearnedPen.Freeze();
            EdgeCanLearnPen.Freeze();
            EdgeInactivePen.Freeze();
            FocusRingPen.Freeze();
            UnlockableAuraPen.Freeze();
            DarkSurfaceBrush.Freeze();
            LockedNodeBrush.Freeze();
            LockedBorderPen.Freeze();
            WhiteTextBrush.Freeze();
            GoldTextBrush.Freeze();
            MutedTextBrush.Freeze();
        }

        public DestinyTreeCanvas(IPlayerDestiny destiny)
        {
            _destiny = destiny;
            Height = 490;
            MinHeight = 420;
            ClipToBounds = true;
            Cursor = Cursors.Arrow;

            MouseMove += OnMouseMove;
            MouseLeave += (_, _) =>
            {
                _hoveredNodeId = null;
                if (_isDragging && !Mouse.LeftButton.HasFlag(MouseButtonState.Pressed) && !Mouse.RightButton.HasFlag(MouseButtonState.Pressed))
                {
                    _isDragging = false;
                    ReleaseMouseCapture();
                }
                InvalidateVisual();
            };
            LostMouseCapture += (_, _) =>
            {
                _isDragging = false;
                Cursor = Cursors.Arrow;
            };
            MouseLeftButtonDown += OnMouseLeftButtonDown;
            MouseLeftButtonUp += OnMouseLeftButtonUp;
            MouseRightButtonDown += OnMouseRightButtonDown;
            MouseRightButtonUp += OnMouseRightButtonUp;
            MouseWheel += OnMouseWheel;
        }

        public void SetSelection(string nodeId)
        {
            _selectedNodeId = nodeId;
            InvalidateVisual();
        }

        public void ResetView()
        {
            _panX = 0;
            _panY = 0;
            _zoom = 1.0;
            InvalidateVisual();
        }

        private Point GetNodeCanvasPos(DestinyNodeDef node, double cx, double cy)
        {
            if (NodePositions.TryGetValue(node.Id, out var offset))
            {
                return new Point(cx + offset.X * _zoom + _panX, cy + offset.Y * _zoom + _panY);
            }
            return new Point(cx + node.GridX * 85.0 * _zoom + _panX, cy + node.GridY * 85.0 * _zoom + _panY);
        }

        private string? HitTest(Point mousePos)
        {
            var cx = ActualWidth * 0.5;
            var cy = ActualHeight * 0.5;

            foreach (var node in _destiny.AllNodes)
            {
                var pt = GetNodeCanvasPos(node, cx, cy);
                var radius = GetNodeRadius(node.Tier) * _zoom;
                var hitRadius = Math.Max(radius + 8.0, 16.0);
                var dx = mousePos.X - pt.X;
                var dy = mousePos.Y - pt.Y;
                if (dx * dx + dy * dy <= hitRadius * hitRadius)
                {
                    return node.Id;
                }
            }
            return null;
        }

        private static double GetNodeRadius(DestinyNodeTier tier) => tier switch
        {
            DestinyNodeTier.Keystone => 21.0,
            DestinyNodeTier.Notable => 15.5,
            DestinyNodeTier.Minor => 11.5,
            _ => 20.0
        };

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            var pos = e.GetPosition(this);

            if (_isDragging)
            {
                if (e.LeftButton != MouseButtonState.Pressed && e.RightButton != MouseButtonState.Pressed)
                {
                    _isDragging = false;
                    ReleaseMouseCapture();
                    Cursor = Cursors.Arrow;
                    return;
                }

                _panX += pos.X - _lastDragPos.X;
                _panY += pos.Y - _lastDragPos.Y;
                _lastDragPos = pos;
                InvalidateVisual();
                return;
            }

            var hit = HitTest(pos);
            if (hit != _hoveredNodeId)
            {
                _hoveredNodeId = hit;
                Cursor = hit is not null ? Cursors.Hand : Cursors.Arrow;
                InvalidateVisual();
            }
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(this);
            var hit = HitTest(pos);
            if (hit is not null)
            {
                _selectedNodeId = hit;
                if (e.ClickCount >= 2)
                {
                    NodeDoubleClicked?.Invoke(hit);
                }
                else
                {
                    NodeSelected?.Invoke(hit);
                }
                InvalidateVisual();
            }
            else
            {
                _isDragging = true;
                _lastDragPos = pos;
                CaptureMouse();
            }
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                ReleaseMouseCapture();
            }
        }

        private void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = true;
            _lastDragPos = e.GetPosition(this);
            CaptureMouse();
        }

        private void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                ReleaseMouseCapture();
            }
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Only zoom when mouse cursor is strictly within the canvas bounds
            if (!IsMouseOver)
            {
                if (IsMouseCaptured)
                {
                    _isDragging = false;
                    ReleaseMouseCapture();
                }
                return;
            }

            var mouseCheck = e.GetPosition(this);
            if (mouseCheck.X < 0 || mouseCheck.X > ActualWidth || mouseCheck.Y < 0 || mouseCheck.Y > ActualHeight)
            {
                return;
            }

            var zoomDelta = e.Delta > 0 ? 1.12 : 0.89;
            var targetZoom = Math.Clamp(_zoom * zoomDelta, 0.65, 1.85);

            var mouse = e.GetPosition(this);
            var cx = ActualWidth * 0.5;
            var cy = ActualHeight * 0.5;

            // Zoom towards mouse position
            _panX = (_panX - (mouse.X - cx)) * (targetZoom / _zoom) + (mouse.X - cx);
            _panY = (_panY - (mouse.Y - cy)) * (targetZoom / _zoom) + (mouse.Y - cy);
            _zoom = targetZoom;
            e.Handled = true;

            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            var w = ActualWidth;
            var h = ActualHeight;
            if (w <= 10 || h <= 10) return;

            var cx = w * 0.5;
            var cy = h * 0.5;
            var centerPoint = new Point(cx + _panX, cy + _panY);

            // 1. Background
            dc.DrawRectangle(TreeBgBrush, null, new Rect(0, 0, w, h));

            // 2. Concentric Orbital Rings (PoE Constellation aesthetic)
            double[] orbitalRadii = [90.0, 175.0, 255.0, 345.0];
            foreach (var r in orbitalRadii)
            {
                dc.DrawEllipse(null, RingGuidePen, centerPoint, r * _zoom, r * _zoom);
            }

            // Radial axis lines
            double[] angles = [0, 45, 90, 135, 180, 225, 270, 315];
            foreach (var deg in angles)
            {
                var rad = deg * Math.PI / 180.0;
                var pEnd = new Point(centerPoint.X + Math.Cos(rad) * 360 * _zoom, centerPoint.Y + Math.Sin(rad) * 360 * _zoom);
                dc.DrawLine(AxisGuidePen, centerPoint, pEnd);
            }

            var state = _destiny.State;
            var nodeMap = _destiny.AllNodes.ToDictionary(n => n.Id, StringComparer.OrdinalIgnoreCase);

            // 3. Draw Connecting Links (Edges)
            foreach (var node in _destiny.AllNodes)
            {
                var pTarget = GetNodeCanvasPos(node, cx, cy);
                var isTargetLearned = state.LearnedNodeIds.Contains(node.Id);
                var isTargetCanLearn = !isTargetLearned && _destiny.CanLearnNode(node.Id, out _);

                foreach (var prereqId in node.PrerequisiteIds)
                {
                    if (!nodeMap.TryGetValue(prereqId, out var prereqNode)) continue;
                    var pSource = GetNodeCanvasPos(prereqNode, cx, cy);
                    var isSourceLearned = state.LearnedNodeIds.Contains(prereqNode.Id);

                    if (isSourceLearned && isTargetLearned)
                    {
                        // Active learned beam with radiant glow
                        dc.DrawLine(EdgeLearnedGlowPen, pSource, pTarget);
                        dc.DrawLine(EdgeLearnedPen, pSource, pTarget);
                    }
                    else if (isSourceLearned && isTargetCanLearn)
                    {
                        // Ready-to-allocate pathway
                        dc.DrawLine(EdgeCanLearnPen, pSource, pTarget);
                    }
                    else
                    {
                        // Locked / future pathway
                        dc.DrawLine(EdgeInactivePen, pSource, pTarget);
                    }
                }
            }

            // 4. Draw Nodes
            foreach (var node in _destiny.AllNodes)
            {
                var pt = GetNodeCanvasPos(node, cx, cy);
                var isLearned = state.LearnedNodeIds.Contains(node.Id);
                var canLearn = !isLearned && _destiny.CanLearnNode(node.Id, out _);
                var isSelected = node.Id.Equals(_selectedNodeId, StringComparison.OrdinalIgnoreCase);
                var isHovered = node.Id.Equals(_hoveredNodeId, StringComparison.OrdinalIgnoreCase);

                var (_, _, schoolColor) = GetSchoolMeta(node.School);
                var radius = GetNodeRadius(node.Tier) * _zoom;

                // Selection or hover ring
                if (isSelected)
                {
                    dc.DrawEllipse(null, FocusRingPen, pt, radius + 5 * _zoom, radius + 5 * _zoom);
                }
                else if (isHovered)
                {
                    var hoverPen = new Pen(new SolidColorBrush(Color.FromRgb(schoolColor.R, schoolColor.G, schoolColor.B)), 2.0);
                    hoverPen.Freeze();
                    dc.DrawEllipse(null, hoverPen, pt, radius + 4 * _zoom, radius + 4 * _zoom);
                }

                // Unlockable pulsating aura
                if (canLearn && !isSelected)
                {
                    dc.DrawEllipse(null, UnlockableAuraPen, pt, radius + 3.5 * _zoom, radius + 3.5 * _zoom);
                }

                // Node Body
                Brush fillBrush;
                Pen borderPen;

                if (isLearned)
                {
                    fillBrush = new SolidColorBrush(Color.FromRgb(schoolColor.R, schoolColor.G, schoolColor.B));
                    borderPen = new Pen(WhiteTextBrush, 1.8 * _zoom);
                }
                else if (canLearn)
                {
                    fillBrush = DarkSurfaceBrush;
                    borderPen = new Pen(new SolidColorBrush(Color.FromRgb(schoolColor.R, schoolColor.G, schoolColor.B)), 2.2 * _zoom);
                }
                else
                {
                    fillBrush = LockedNodeBrush;
                    borderPen = LockedBorderPen;
                }

                fillBrush.Freeze();
                borderPen.Freeze();

                // Draw shape based on Tier
                if (node.Tier == DestinyNodeTier.Keystone)
                {
                    // Ornate Diamond for Keystones
                    DrawDiamond(dc, fillBrush, borderPen, pt, radius * 1.25);
                }
                else if (node.Tier == DestinyNodeTier.Notable)
                {
                    // Ornate Double Ring for Notables
                    dc.DrawEllipse(fillBrush, borderPen, pt, radius, radius);
                    var innerPen = new Pen(new SolidColorBrush(Color.FromArgb(0x88, schoolColor.R, schoolColor.G, schoolColor.B)), 1.2);
                    innerPen.Freeze();
                    dc.DrawEllipse(null, innerPen, pt, radius * 0.72, radius * 0.72);
                }
                else
                {
                    // Clean Circle for Minors and Roots
                    dc.DrawEllipse(fillBrush, borderPen, pt, radius, radius);
                }

                // Node Inner Glyph
                string glyph = node.Tier switch
                {
                    DestinyNodeTier.Keystone => "✦",
                    DestinyNodeTier.Notable => "★",
                    DestinyNodeTier.Root when node.School == DestinySchool.Universal => "☯",
                    DestinyNodeTier.Root => GetSchoolMeta(node.School).Glyph,
                    _ => isLearned ? "✓" : "•"
                };

                var glyphBrush = isLearned ? WhiteTextBrush : canLearn ? GoldTextBrush : MutedTextBrush;
                var glyphSize = Math.Max(7.0, (radius * 0.9));
                var ftGlyph = new FormattedText(
                    glyph,
                    Vietnamese,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    glyphSize,
                    glyphBrush,
                    1.0);
                dc.DrawText(ftGlyph, new Point(pt.X - ftGlyph.Width * 0.5, pt.Y - ftGlyph.Height * 0.5));

                // Node Name Label
                var showLabel = node.Tier is DestinyNodeTier.Keystone or DestinyNodeTier.Notable || isSelected || isHovered;
                if (showLabel)
                {
                    var labelBrush = isLearned ? GoldTextBrush : (canLearn ? WhiteTextBrush : MutedTextBrush);
                    var fontSize = (node.Tier == DestinyNodeTier.Keystone ? 11.0 : 10.0) * Math.Clamp(_zoom, 0.85, 1.2);
                    var ftName = new FormattedText(
                        node.NameVi,
                        Vietnamese,
                        FlowDirection.LeftToRight,
                        new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, node.Tier == DestinyNodeTier.Keystone ? FontWeights.Bold : FontWeights.SemiBold, FontStretches.Normal),
                        fontSize,
                        labelBrush,
                        1.0);

                    var labelY = pt.Y + radius + 4.0;
                    // Label dark backdrop pill for maximum readability
                    var labelRect = new Rect(pt.X - ftName.Width * 0.5 - 4, labelY - 1, ftName.Width + 8, ftName.Height + 2);
                    var labelBg = new SolidColorBrush(Color.FromArgb(0xCC, 0x0A, 0x0E, 0x16));
                    labelBg.Freeze();
                    dc.DrawRoundedRectangle(labelBg, null, labelRect, 3, 3);
                    dc.DrawText(ftName, new Point(pt.X - ftName.Width * 0.5, labelY));
                }
            }
        }

        private static void DrawDiamond(DrawingContext dc, Brush fill, Pen border, Point center, double radius)
        {
            var pTop = new Point(center.X, center.Y - radius);
            var pRight = new Point(center.X + radius, center.Y);
            var pBottom = new Point(center.X, center.Y + radius);
            var pLeft = new Point(center.X - radius, center.Y);

            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(pTop, isFilled: true, isClosed: true);
                ctx.LineTo(pRight, isStroked: true, isSmoothJoin: false);
                ctx.LineTo(pBottom, isStroked: true, isSmoothJoin: false);
                ctx.LineTo(pLeft, isStroked: true, isSmoothJoin: false);
            }
            geo.Freeze();
            dc.DrawGeometry(fill, border, geo);
        }
    }
}
