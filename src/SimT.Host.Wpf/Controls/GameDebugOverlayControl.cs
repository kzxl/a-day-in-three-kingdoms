using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SimT.Contracts.Diagnostics;
using SimT.Contracts.Scenes;

namespace SimT.Host.Wpf.Controls;

/// <summary>
/// Sovereign Diagnostics &amp; Performance HUD Overlay.
/// Displays real-time FPS, frame latency, draw call batches, visible entities,
/// active scene stack, and detects per-frame GC allocations to prevent micro-stuttering.
/// Toggled via F3.
/// </summary>
public sealed class GameDebugOverlayControl : Border
{
    private static readonly Brush BgBrush = Freeze(new SolidColorBrush(Color.FromArgb(0xEE, 0x12, 0x13, 0x16)));
    private static readonly Brush BorderClr = Freeze(new SolidColorBrush(Color.FromArgb(0xFF, 0x2E, 0x32, 0x3C)));
    private static readonly Brush TitleClr = Freeze(new SolidColorBrush(Color.FromArgb(0xFF, 0x4E, 0xC9, 0xB0)));
    private static readonly Brush LabelClr = Freeze(new SolidColorBrush(Color.FromArgb(0xFF, 0x8A, 0x8F, 0x9D)));
    private static readonly Brush ValClr = Freeze(new SolidColorBrush(Color.FromArgb(0xFF, 0xDC, 0xDC, 0xAA)));
    private static readonly Brush GreenClr = Freeze(new SolidColorBrush(Color.FromArgb(0xFF, 0x6A, 0x99, 0x55)));
    private static readonly Brush YellowClr = Freeze(new SolidColorBrush(Color.FromArgb(0xFF, 0xDC, 0xA1, 0x48)));
    private static readonly Brush RedClr = Freeze(new SolidColorBrush(Color.FromArgb(0xFF, 0xF4, 0x47, 0x47)));

    private readonly TextBlock _fpsVal;
    private readonly TextBlock _frameTimeVal;
    private readonly TextBlock _drawCallsVal;
    private readonly TextBlock _visibleVal;
    private readonly TextBlock _gcAllocVal;
    private readonly TextBlock _sceneVal;

    public FrameDiagnostics Diagnostics { get; } = new();
    public ISceneManager? SceneManager { get; set; }

    public GameDebugOverlayControl()
    {
        HorizontalAlignment = HorizontalAlignment.Right;
        VerticalAlignment = VerticalAlignment.Top;
        Margin = new Thickness(12);
        Padding = new Thickness(12, 10, 12, 10);
        Background = BgBrush;
        BorderBrush = BorderClr;
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(6);
        IsHitTestVisible = false;
        Visibility = Visibility.Collapsed;

        var stack = new StackPanel { Width = 190 };

        var header = new TextBlock
        {
            Text = "● SIMT ENGINE DIAGNOSTICS",
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            Foreground = TitleClr,
            Margin = new Thickness(0, 0, 0, 8)
        };
        stack.Children.Add(header);

        _fpsVal = CreateMetricRow(stack, "FPS:");
        _frameTimeVal = CreateMetricRow(stack, "Frame Time:");
        _drawCallsVal = CreateMetricRow(stack, "Draw Calls:");
        _visibleVal = CreateMetricRow(stack, "Active Items:");
        _gcAllocVal = CreateMetricRow(stack, "GC Alloc/Frame:");
        _sceneVal = CreateMetricRow(stack, "Active Scene:");

        Child = stack;
    }

    public void ToggleVisibility()
    {
        Visibility = Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
    }

    public void Refresh(double deltaTimeSeconds)
    {
        if (Visibility != Visibility.Visible) return;

        var fps = Diagnostics.CurrentFps;
        _fpsVal.Text = $"{fps:F1}";
        _fpsVal.Foreground = fps >= 55 ? GreenClr : (fps >= 30 ? YellowClr : RedClr);

        _frameTimeVal.Text = $"{Diagnostics.FrameTimeMs:F2} ms";
        _drawCallsVal.Text = Diagnostics.DrawCalls.ToString();
        _visibleVal.Text = Diagnostics.ActiveGameObjects.ToString();

        var gcBytes = Diagnostics.BytesAllocatedThisFrame;
        if (gcBytes == 0)
        {
            _gcAllocVal.Text = "0 B (Zero-Alloc)";
            _gcAllocVal.Foreground = GreenClr;
        }
        else if (gcBytes < 4096)
        {
            _gcAllocVal.Text = $"{gcBytes:N0} B";
            _gcAllocVal.Foreground = YellowClr;
        }
        else
        {
            _gcAllocVal.Text = $"{gcBytes / 1024.0:F1} KB";
            _gcAllocVal.Foreground = RedClr;
        }

        var scene = SceneManager?.CurrentScene;
        _sceneVal.Text = scene != null ? scene.SceneId : "None";
    }

    private static TextBlock CreateMetricRow(StackPanel parent, string label)
    {
        var dock = new DockPanel { Margin = new Thickness(0, 1, 0, 1) };
        var lbl = new TextBlock
        {
            Text = label,
            FontSize = 11,
            Foreground = LabelClr,
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(lbl, Dock.Left);
        dock.Children.Add(lbl);

        var val = new TextBlock
        {
            Text = "---",
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = ValClr,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        dock.Children.Add(val);

        parent.Children.Add(dock);
        return val;
    }

    private static Brush Freeze(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }
}
