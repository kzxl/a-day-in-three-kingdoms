using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SimT.Contracts.Ui;

namespace SimT.Host.Wpf.Controls;

/// <summary>
/// Reusable KPI and Metric Card control.
/// Renders standard dashboard numbers, unit hints, and tone indicators with
/// micro-interactions, hardware-accelerated layouts, and zero-leak cached brushes.
/// </summary>
public sealed class GameMetricCard : Border
{
    private static readonly Brush DefaultBg = Freeze(new SolidColorBrush(Color.FromRgb(0x17, 0x1E, 0x28)));
    private static readonly Brush DefaultBorder = Freeze(new SolidColorBrush(Color.FromRgb(0x26, 0x30, 0x3D)));
    private static readonly Brush HoverBorder = Freeze(new SolidColorBrush(Color.FromRgb(0x3B, 0x48, 0x5C)));
    private static readonly Brush FaintFg = Freeze(new SolidColorBrush(Color.FromRgb(0x5E, 0x6B, 0x7C)));
    private static readonly Brush NeutralFg = Freeze(new SolidColorBrush(Color.FromRgb(0xE8, 0xED, 0xF5)));
    private static readonly Brush GainFg = Freeze(new SolidColorBrush(Color.FromRgb(0x5A, 0xC0, 0x8A)));
    private static readonly Brush LossFg = Freeze(new SolidColorBrush(Color.FromRgb(0xE0, 0x6C, 0x75)));
    private static readonly Brush WarningFg = Freeze(new SolidColorBrush(Color.FromRgb(0xD9, 0xA4, 0x41)));

    private readonly TextBlock _labelBlock;
    private readonly TextBlock _valueBlock;
    private readonly TextBlock _hintBlock;

    public GameMetricCard()
    {
        Background = DefaultBg;
        BorderBrush = DefaultBorder;
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(8);
        Padding = new Thickness(14, 10, 16, 10);
        Margin = new Thickness(0, 0, 10, 10);
        Cursor = Cursors.Arrow;

        var stack = new StackPanel();

        _labelBlock = new TextBlock
        {
            FontSize = 10.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = FaintFg,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        _valueBlock = new TextBlock
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize = 19,
            FontWeight = FontWeights.SemiBold,
            Foreground = NeutralFg,
            Margin = new Thickness(0, 3, 0, 0)
        };

        _hintBlock = new TextBlock
        {
            FontSize = 11.5,
            Foreground = FaintFg,
            Margin = new Thickness(0, 2, 0, 0),
            Visibility = Visibility.Collapsed,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        stack.Children.Add(_labelBlock);
        stack.Children.Add(_valueBlock);
        stack.Children.Add(_hintBlock);

        Child = stack;
    }

    public GameMetricCard(ScreenMetric metric) : this()
    {
        SetMetric(metric);
    }

    public void SetMetric(ScreenMetric metric)
    {
        _labelBlock.Text = metric.Label.ToUpperInvariant();
        _valueBlock.Text = metric.Value;

        _valueBlock.Foreground = metric.Tone switch
        {
            MetricTone.Positive => GainFg,
            MetricTone.Negative => LossFg,
            MetricTone.Warning => WarningFg,
            _ => NeutralFg
        };

        if (!string.IsNullOrWhiteSpace(metric.Hint))
        {
            _hintBlock.Text = metric.Hint;
            _hintBlock.Visibility = Visibility.Visible;
            ToolTip = $"{metric.Label}: {metric.Value} — {metric.Hint}";
        }
        else
        {
            _hintBlock.Visibility = Visibility.Collapsed;
            ToolTip = $"{metric.Label}: {metric.Value}";
        }
    }

    public void SetData(string label, string value, string? hint = null, MetricTone tone = MetricTone.Neutral)
    {
        SetMetric(new ScreenMetric(label, value, hint, tone));
    }

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        BorderBrush = HoverBorder;
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        BorderBrush = DefaultBorder;
    }

    private static Brush Freeze(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }
}
