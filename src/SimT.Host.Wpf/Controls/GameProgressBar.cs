using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SimT.Host.Wpf.Controls;

public enum GameProgressTone
{
    Health,
    Stamina,
    Affection,
    Experience,
    Warning
}

/// <summary>
/// Custom Tam Quốc progress bar with milestone tick marks (25%, 50%, 75%) and numeric readout.
/// Used for health, stamina, affection, durability, and skill experience.
/// </summary>
public sealed class GameProgressBar : Grid
{
    private static readonly Brush TrackBg = Freeze(new SolidColorBrush(Color.FromRgb(0x15, 0x11, 0x0D)));
    private static readonly Pen TrackBorderPen = FreezePen(new Pen(Freeze(new SolidColorBrush(Color.FromRgb(0x3A, 0x2C, 0x20))), 1.0));
    private static readonly Pen TickPen = FreezePen(new Pen(Freeze(new SolidColorBrush(Color.FromArgb(0x88, 0x15, 0x11, 0x0D))), 1.0));
    private static readonly Pen OuterTickPen = FreezePen(new Pen(Freeze(new SolidColorBrush(Color.FromArgb(0x66, 0x8F, 0x7F, 0x62))), 1.0));

    private static readonly Brush HealthBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xA8, 0x32, 0x1F)));
    private static readonly Brush StaminaBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x3F, 0x6B, 0x55)));
    private static readonly Brush AffectionBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xC4, 0xA0, 0x62)));
    private static readonly Brush ExpBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x52, 0x7A, 0x8A)));
    private static readonly Brush WarnBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xC9, 0x7A, 0x3E)));
    private static readonly Brush LabelFg = Freeze(new SolidColorBrush(Color.FromRgb(0xB9, 0xAA, 0x8C)));
    private static readonly Brush NumberFg = Freeze(new SolidColorBrush(Color.FromRgb(0xF3, 0xE6, 0xCC)));
    private static readonly FontFamily MonoFont = new("Consolas");

    private readonly DockPanel _headerRow;
    private readonly TextBlock _labelBlock;
    private readonly TextBlock _valueBlock;
    private readonly TickTrackCanvas _canvas;
    private double _value;
    private double _maximum = 100.0;
    private GameProgressTone _tone = GameProgressTone.Affection;
    private string? _customValueText;

    public GameProgressBar(
        string? label = null,
        double value = 0,
        double maximum = 100,
        GameProgressTone tone = GameProgressTone.Affection,
        bool compact = false)
    {
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _headerRow = new DockPanel
        {
            Margin = new Thickness(0, 0, 0, 3),
            Visibility = compact && string.IsNullOrWhiteSpace(label) ? Visibility.Collapsed : Visibility.Visible
        };

        _valueBlock = new TextBlock
        {
            FontFamily = MonoFont,
            FontSize = compact ? 10.5 : 11.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = NumberFg,
            TextAlignment = TextAlignment.Right
        };
        DockPanel.SetDock(_valueBlock, Dock.Right);
        _headerRow.Children.Add(_valueBlock);

        _labelBlock = new TextBlock
        {
            Text = label ?? string.Empty,
            FontSize = compact ? 10.5 : 11.5,
            Foreground = LabelFg,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        _headerRow.Children.Add(_labelBlock);
        SetRow(_headerRow, 0);
        Children.Add(_headerRow);

        _canvas = new TickTrackCanvas
        {
            Height = compact ? 8 : 11
        };
        SetRow(_canvas, 1);
        Children.Add(_canvas);

        _tone = tone;
        SetProgress(value, maximum);
    }

    public string Label
    {
        get => _labelBlock.Text;
        set
        {
            _labelBlock.Text = value;
            _headerRow.Visibility = Visibility.Visible;
        }
    }

    public double Value
    {
        get => _value;
        set => SetProgress(value, _maximum, _customValueText);
    }

    public double Maximum
    {
        get => _maximum;
        set => SetProgress(_value, value, _customValueText);
    }

    public GameProgressTone Tone
    {
        get => _tone;
        set
        {
            _tone = value;
            _canvas.FillBrush = BrushForTone(value);
            _canvas.InvalidateVisual();
        }
    }

    public void SetProgress(double value, double maximum, string? customValueText = null)
    {
        _value = value;
        _maximum = maximum > 0 ? maximum : 100.0;
        _customValueText = customValueText;

        _valueBlock.Text = customValueText
            ?? $"{Math.Round(_value).ToString("N0", CultureInfo.InvariantCulture)} / {Math.Round(_maximum).ToString("N0", CultureInfo.InvariantCulture)}";

        _canvas.Ratio = Math.Clamp(_value / _maximum, 0.0, 1.0);
        _canvas.FillBrush = BrushForTone(_tone);
        _canvas.InvalidateVisual();
    }

    private static Brush BrushForTone(GameProgressTone tone) => tone switch
    {
        GameProgressTone.Health => HealthBrush,
        GameProgressTone.Stamina => StaminaBrush,
        GameProgressTone.Experience => ExpBrush,
        GameProgressTone.Warning => WarnBrush,
        _ => AffectionBrush
    };

    private sealed class TickTrackCanvas : FrameworkElement
    {
        public double Ratio { get; set; }
        public Brush FillBrush { get; set; } = AffectionBrush;

        protected override void OnRender(DrawingContext dc)
        {
            var w = ActualWidth;
            var h = ActualHeight;
            if (w <= 2 || h <= 2) return;

            var rect = new Rect(0.5, 0.5, w - 1, h - 1);
            dc.DrawRoundedRectangle(TrackBg, TrackBorderPen, rect, 2, 2);

            if (Ratio > 0.005)
            {
                var fillW = Math.Max(3.0, (w - 2) * Ratio);
                dc.DrawRoundedRectangle(FillBrush, null, new Rect(1, 1, fillW, h - 2), 1.5, 1.5);
            }

            // Milestone ticks at 25%, 50%, 75%
            double[] ticks = [0.25, 0.50, 0.75];
            foreach (var t in ticks)
            {
                var x = Math.Round(w * t) + 0.5;
                dc.DrawLine(TickPen, new Point(x, 1), new Point(x, h - 1));
                dc.DrawLine(OuterTickPen, new Point(x, 1), new Point(x, 3.5));
            }
        }
    }

    private static Brush Freeze(SolidColorBrush b)
    {
        b.Freeze();
        return b;
    }

    private static Pen FreezePen(Pen p)
    {
        p.Freeze();
        return p;
    }
}
