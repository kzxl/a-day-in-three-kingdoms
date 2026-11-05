using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SimT.Host.Wpf.Controls;

/// <summary>
/// Tam Quốc quantity selector with − / + step buttons, a slider, a Max ("Tối đa") button,
/// live total/preview display, and a commit GameButton.
/// Replaces the plain TextBox quantity input in the Inspector.
/// </summary>
public sealed class GameQuantityPicker : Border
{
    private static readonly Brush CardBg = Freeze(new SolidColorBrush(Color.FromRgb(0x21, 0x18, 0x12)));
    private static readonly Brush CardBorder = Freeze(new SolidColorBrush(Color.FromRgb(0x3A, 0x2C, 0x20)));
    private static readonly Brush ValueBg = Freeze(new SolidColorBrush(Color.FromRgb(0x15, 0x11, 0x0D)));
    private static readonly Brush InkFg = Freeze(new SolidColorBrush(Color.FromRgb(0xF3, 0xE6, 0xCC)));
    private static readonly Brush AccentFg = Freeze(new SolidColorBrush(Color.FromRgb(0xC4, 0xA0, 0x62)));
    private static readonly Brush MutedFg = Freeze(new SolidColorBrush(Color.FromRgb(0xB9, 0xAA, 0x8C)));
    private static readonly FontFamily MonoFont = new("Consolas");

    private readonly TextBlock _valueText;
    private readonly Slider _slider;
    private readonly TextBlock _previewText;
    private readonly GameButton _commitButton;
    private readonly Func<int, string>? _previewFunc;
    private readonly string _commandPrefix;
    private int _min = 1;
    private int _max = 50;
    private int _value = 1;
    private bool _suppressSlider;

    public event RoutedEventHandler? CommitClicked;
    public event Action<int>? ValueChanged;

    public GameQuantityPicker(
        string commandId,
        string actionLabel,
        GameButtonVariant buttonVariant = GameButtonVariant.Primary,
        int min = 1,
        int max = 50,
        int initial = 1,
        Func<int, string>? previewFunc = null,
        string? costText = null)
    {
        _commandPrefix = commandId;
        _previewFunc = previewFunc;
        _min = Math.Max(1, min);
        _max = Math.Max(_min, max);
        _value = Math.Clamp(initial, _min, _max);

        Background = CardBg;
        BorderBrush = CardBorder;
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(6);
        Padding = new Thickness(11, 9, 11, 9);
        Margin = new Thickness(0, 0, 0, 10);

        var root = new StackPanel();

        // Row 1: Header + current numeric badge
        var headRow = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
        _valueText = new TextBlock
        {
            FontFamily = MonoFont,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = AccentFg,
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(_valueText, Dock.Right);
        headRow.Children.Add(_valueText);

        headRow.Children.Add(new TextBlock
        {
            Text = "SỐ LƯỢNG GIAO DỊCH",
            FontSize = 10.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = MutedFg,
            VerticalAlignment = VerticalAlignment.Center
        });
        root.Children.Add(headRow);

        // Row 2: [ - ]  [ Slider ]  [ + ]  [ Tối đa ]
        var ctrlGrid = new Grid { Margin = new Thickness(0, 0, 0, 7) };
        ctrlGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ctrlGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ctrlGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ctrlGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var minusBtn = new GameButton(string.Empty, GameButtonVariant.Icon, iconPathData: "M5,12 L19,12", isCompact: true)
        {
            Width = 32,
            ToolTip = "Giảm 1"
        };
        minusBtn.Click += (_, _) => SetQuantity(_value - 1);
        Grid.SetColumn(minusBtn, 0);
        ctrlGrid.Children.Add(minusBtn);

        _slider = new Slider
        {
            Minimum = _min,
            Maximum = _max,
            Value = _value,
            SmallChange = 1,
            LargeChange = 5,
            IsSnapToTickEnabled = true,
            TickFrequency = 1,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 8, 0),
            Foreground = AccentFg,
            Background = ValueBg
        };
        _slider.ValueChanged += (_, e) =>
        {
            if (!_suppressSlider)
            {
                SetQuantity((int)Math.Round(e.NewValue));
            }
        };
        Grid.SetColumn(_slider, 1);
        ctrlGrid.Children.Add(_slider);

        var plusBtn = new GameButton(string.Empty, GameButtonVariant.Icon, iconPathData: "M5,12 L19,12 M12,5 L12,19", isCompact: true)
        {
            Width = 32,
            Margin = new Thickness(0, 0, 6, 0),
            ToolTip = "Tăng 1"
        };
        plusBtn.Click += (_, _) => SetQuantity(_value + 1);
        Grid.SetColumn(plusBtn, 2);
        ctrlGrid.Children.Add(plusBtn);

        var maxBtn = new GameButton("Tối đa", GameButtonVariant.Secondary, isCompact: true)
        {
            ToolTip = $"Chọn mức tối đa ({_max})"
        };
        maxBtn.Click += (_, _) => SetQuantity(_max);
        Grid.SetColumn(maxBtn, 3);
        ctrlGrid.Children.Add(maxBtn);

        root.Children.Add(ctrlGrid);

        // Row 3: Live preview of total price & remaining balance
        _previewText = new TextBlock
        {
            FontSize = 11.5,
            Foreground = InkFg,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        };
        root.Children.Add(_previewText);

        // Row 4: Commit button
        _commitButton = new GameButton(actionLabel, buttonVariant, costText)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _commitButton.Click += (_, e) => CommitClicked?.Invoke(_commitButton, e);
        root.Children.Add(_commitButton);

        Child = root;
        RefreshState();
    }

    public int Value
    {
        get => _value;
        set => SetQuantity(value);
    }

    public GameButton ActionButton => _commitButton;

    public void SetQuantity(int newValue)
    {
        _value = Math.Clamp(newValue, _min, _max);
        _suppressSlider = true;
        _slider.Value = _value;
        _suppressSlider = false;
        RefreshState();
        ValueChanged?.Invoke(_value);
    }

    private void RefreshState()
    {
        _valueText.Text = $"{_value.ToString("N0", CultureInfo.InvariantCulture)} / {_max.ToString("N0", CultureInfo.InvariantCulture)}";
        var preview = _previewFunc?.Invoke(_value);
        _previewText.Text = string.IsNullOrWhiteSpace(preview)
            ? $"Số lượng đã chọn: {_value}"
            : preview;
        _previewText.Visibility = Visibility.Visible;
        _commitButton.Tag = $"{_commandPrefix}:{_value}";
    }

    private static Brush Freeze(SolidColorBrush b)
    {
        b.Freeze();
        return b;
    }
}
