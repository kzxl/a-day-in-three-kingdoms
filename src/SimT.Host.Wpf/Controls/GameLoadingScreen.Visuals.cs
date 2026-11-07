using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using WpfPath = System.Windows.Shapes.Path;

namespace SimT.Host.Wpf.Controls;

public sealed partial class GameLoadingScreen
{
    private static readonly SolidColorBrush BgBrush = FrozenBrush(0x15, 0x11, 0x0D);
    private static readonly SolidColorBrush DarkOverlayBrush = FrozenBrush(0x94, 0x15, 0x11, 0x0D);
    private static readonly SolidColorBrush SealBgBrush = FrozenBrush(0xA8, 0x32, 0x1F);
    private static readonly SolidColorBrush SealHoverBrush = FrozenBrush(0xBB, 0x3D, 0x27);
    private static readonly SolidColorBrush IvoryBrush = FrozenBrush(0xF3, 0xE6, 0xCC);
    private static readonly SolidColorBrush SilkTextBrush = FrozenBrush(0xEA, 0xDF, 0xC6);
    private static readonly SolidColorBrush GoldHeaderBrush = FrozenBrush(0xE0, 0xC4, 0x8A);
    private static readonly SolidColorBrush GoldAccentBrush = FrozenBrush(0xC4, 0xA0, 0x62);
    private static readonly SolidColorBrush TopRightBrush = FrozenBrush(0xCD, 0xBF, 0x9F);
    private static readonly SolidColorBrush SublineBrush = FrozenBrush(0xD6, 0xC8, 0xA8);
    private static readonly SolidColorBrush BoxBgBrush = FrozenBrush(0xE6, 0x1D, 0x16, 0x11);
    private static readonly SolidColorBrush BottomBgBrush = FrozenBrush(0xEB, 0x15, 0x11, 0x0D);
    private static readonly SolidColorBrush BorderLineBrush = FrozenBrush(0x3A, 0x2C, 0x20);
    private static readonly SolidColorBrush StepDoneTextBrush = FrozenBrush(0xB9, 0xAA, 0x8C);
    private static readonly SolidColorBrush StepPendingTextBrush = FrozenBrush(0x9A, 0x8A, 0x6E);
    private static readonly SolidColorBrush StepPendingRingBrush = FrozenBrush(0x7A, 0x6A, 0x52);
    private static readonly SolidColorBrush RoadMutedBrush = FrozenBrush(0x5A, 0x46, 0x32);

    private static readonly FontFamily SansFont = new("Be Vietnam Pro, Segoe UI");
    private static readonly FontFamily SerifFont = new("Noto Serif, Georgia, SimSun, Segoe UI");
    private static readonly FontFamily HanFont = new("Ma Shan Zheng, KaiTi, SimSun, Microsoft YaHei, Noto Serif");

    private TextBlock _campaignTurnText = null!;
    private TextBlock _cityNameText = null!;
    private TextBlock _cityHanText = null!;
    private TextBlock _sublineText = null!;
    private readonly StepRowVisual[] _stepRows = new StepRowVisual[6];

    private sealed record StepRowVisual(
        WpfPath CheckPath,
        Grid ActivePulseGrid,
        Ellipse ActiveRing,
        ScaleTransform ActiveRingScale,
        Ellipse PendingCircle,
        TextBlock Label);

    private void BuildVisualTree()
    {
        Background = BgBrush;
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;
        ClipToBounds = true;

        var root = new Grid
        {
            Background = BgBrush,
            ClipToBounds = true
        };

        var bgImage = new Image
        {
            Source = TitleWindow.GetSharedBackgroundImage(),
            Stretch = Stretch.UniformToFill,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        root.Children.Add(bgImage);
        root.Children.Add(new Border { Background = DarkOverlayBrush });

        BuildTopBar(root);
        BuildCenterLeft(root);
        BuildPrepBox(root);
        BuildBottomBar(root);

        Content = root;
    }

    private void BuildTopBar(Grid root)
    {
        var topLeft = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(64, 40, 0, 0)
        };

        var seal = new Border
        {
            Width = 34,
            Height = 34,
            Background = SealBgBrush,
            Margin = new Thickness(0, 0, 12, 0),
            Child = new TextBlock
            {
                Text = "漢",
                FontFamily = HanFont,
                FontSize = 22,
                Foreground = IvoryBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        var brandText = new TextBlock
        {
            Text = "MỘT NGÀY Ở TAM QUỐC",
            FontFamily = SansFont,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = GoldHeaderBrush,
            VerticalAlignment = VerticalAlignment.Center
        };

        topLeft.Children.Add(seal);
        topLeft.Children.Add(brandText);
        root.Children.Add(topLeft);

        _campaignTurnText = new TextBlock
        {
            Text = "Ván: Lữ Khách · Lượt 1",
            FontFamily = SansFont,
            FontSize = 14,
            Foreground = TopRightBrush,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 48, 64, 0)
        };
        root.Children.Add(_campaignTurnText);
    }

    private void BuildCenterLeft(Grid root)
    {
        var panel = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(64, 168, 0, 0),
            MaxWidth = 720
        };

        panel.Children.Add(new TextBlock
        {
            Text = "ĐANG VÀO",
            FontFamily = SansFont,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = GoldHeaderBrush
        });

        var titleRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 10, 0, 0)
        };

        _cityNameText = new TextBlock
        {
            Text = "Lạc Dương",
            FontFamily = SerifFont,
            FontSize = 76,
            FontWeight = FontWeights.Bold,
            Foreground = IvoryBrush,
            VerticalAlignment = VerticalAlignment.Bottom
        };

        _cityHanText = new TextBlock
        {
            Text = "洛陽",
            FontFamily = HanFont,
            FontSize = 46,
            Foreground = GoldAccentBrush,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(20, 0, 0, 10)
        };

        titleRow.Children.Add(_cityNameText);
        titleRow.Children.Add(_cityHanText);
        panel.Children.Add(titleRow);

        _sublineText = new TextBlock
        {
            Text = "Trung Nguyên · 02/05/184 · Mùa Hạ",
            FontFamily = SansFont,
            FontSize = 17,
            Foreground = SublineBrush,
            Margin = new Thickness(0, 14, 0, 0)
        };
        panel.Children.Add(_sublineText);

        root.Children.Add(panel);
    }

    private void BuildPrepBox(Grid root)
    {
        var box = new Border
        {
            Width = 352,
            Padding = new Thickness(24, 20, 24, 20),
            Background = BoxBgBrush,
            BorderBrush = BorderLineBrush,
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 152, 64, 0)
        };

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = "CHUẨN BỊ",
            FontFamily = SansFont,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = GoldHeaderBrush,
            Margin = new Thickness(0, 0, 0, 16)
        });

        for (var i = 0; i < 6; i++)
        {
            var row = CreateStepRow(LoadingProgressState.StepLabels[i], isLast: i == 5, out var visual);
            _stepRows[i] = visual;
            stack.Children.Add(row);
        }

        box.Child = stack;
        root.Children.Add(box);
    }

    private static StackPanel CreateStepRow(string labelText, bool isLast, out StepRowVisual visual)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Height = 22,
            Margin = new Thickness(0, 0, 0, isLast ? 0 : 13)
        };

        var iconSlot = new Grid
        {
            Width = 18,
            Height = 18,
            Margin = new Thickness(0, 0, 14, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        var checkPath = new WpfPath
        {
            Data = Geometry.Parse("M 3.75,9.375 L 7.125,12.75 L 14.25,5.25"),
            Stroke = GoldAccentBrush,
            StrokeThickness = 1.8,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Visibility = Visibility.Collapsed
        };

        var pulseGrid = new Grid
        {
            Width = 18,
            Height = 18,
            Visibility = Visibility.Collapsed
        };
        var ringScale = new ScaleTransform(1.0, 1.0);
        var ring = new Ellipse
        {
            Width = 20,
            Height = 20,
            Stroke = GoldAccentBrush,
            StrokeThickness = 1.8,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = ringScale
        };
        var dot = new Ellipse
        {
            Width = 10,
            Height = 10,
            Fill = GoldAccentBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        pulseGrid.Children.Add(ring);
        pulseGrid.Children.Add(dot);

        var pendingCircle = new Ellipse
        {
            Width = 8,
            Height = 8,
            Stroke = StepPendingRingBrush,
            StrokeThickness = 1.5,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        iconSlot.Children.Add(checkPath);
        iconSlot.Children.Add(pulseGrid);
        iconSlot.Children.Add(pendingCircle);

        var label = new TextBlock
        {
            Text = labelText,
            FontFamily = SansFont,
            FontSize = 15,
            Foreground = StepPendingTextBrush,
            VerticalAlignment = VerticalAlignment.Center
        };

        row.Children.Add(iconSlot);
        row.Children.Add(label);

        visual = new StepRowVisual(checkPath, pulseGrid, ring, ringScale, pendingCircle, label);
        return row;
    }

    private static SolidColorBrush FrozenBrush(byte r, byte g, byte b) =>
        FrozenBrush(0xFF, r, g, b);

    private static SolidColorBrush FrozenBrush(byte a, byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }
}
