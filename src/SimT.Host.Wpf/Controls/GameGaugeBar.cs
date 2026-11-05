using System.Windows;
using System.Windows.Media;

namespace SimT.Host.Wpf.Controls;

/// <summary>
/// Hardware-accelerated, zero-allocation progress and gauge bar control for RPG vitals and stats.
/// Directly draws track and fill geometry in Direct3D via DrawingContext, bypassing
/// heavy XAML ColumnDefinition/Grid layout overhead.
/// </summary>
public sealed class GameGaugeBar : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(double),
            typeof(GameGaugeBar),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(
            nameof(Maximum),
            typeof(double),
            typeof(GameGaugeBar),
            new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BarBrushProperty =
        DependencyProperty.Register(
            nameof(BarBrush),
            typeof(Brush),
            typeof(GameGaugeBar),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TrackBrushProperty =
        DependencyProperty.Register(
            nameof(TrackBrush),
            typeof(Brush),
            typeof(GameGaugeBar),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.Register(
            nameof(CornerRadius),
            typeof(double),
            typeof(GameGaugeBar),
            new FrameworkPropertyMetadata(2.0, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Brush DefaultTrackBrush = new SolidColorBrush(Color.FromRgb(0x17, 0x1E, 0x28));
    private static readonly Pen TrackPen = new(new SolidColorBrush(Color.FromRgb(0x26, 0x30, 0x3D)), 1.0);
    private static readonly Brush DefaultBarBrush = new SolidColorBrush(Color.FromRgb(0xD9, 0xA4, 0x41));

    static GameGaugeBar()
    {
        DefaultTrackBrush.Freeze();
        TrackPen.Freeze();
        DefaultBarBrush.Freeze();
    }

    public GameGaugeBar()
    {
        Height = 6;
        MinHeight = 4;
        ClipToBounds = true;
    }

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public Brush? BarBrush
    {
        get => (Brush?)GetValue(BarBrushProperty);
        set => SetValue(BarBrushProperty, value);
    }

    public Brush? TrackBrush
    {
        get => (Brush?)GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public double CornerRadius
    {
        get => (double)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public void SetProgress(double value, double maximum, Brush? barBrush = null)
    {
        Value = value;
        Maximum = maximum;
        if (barBrush is not null)
        {
            BarBrush = barBrush;
        }
    }

    public void AnimateProgress(double targetValue, double maximum, Brush? barBrush = null, int durationMs = 350)
    {
        Maximum = maximum;
        if (barBrush is not null)
        {
            BarBrush = barBrush;
        }

        var anim = new System.Windows.Media.Animation.DoubleAnimation(Value, targetValue, TimeSpan.FromMilliseconds(durationMs))
        {
            EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
        };
        BeginAnimation(ValueProperty, anim);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        var r = CornerRadius;
        var track = TrackBrush ?? DefaultTrackBrush;
        dc.DrawRoundedRectangle(track, TrackPen, new Rect(0, 0, w, h), r, r);

        var max = Maximum > 0 ? Maximum : 100.0;
        var ratio = Math.Clamp(Value / max, 0.0, 1.0);
        if (ratio > 0.002)
        {
            var fillW = Math.Max(r * 2, w * ratio);
            var fill = BarBrush ?? DefaultBarBrush;
            dc.DrawRoundedRectangle(fill, null, new Rect(0, 0, Math.Min(w, fillW), h), r, r);
        }
    }
}
