using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SimT.Host.Wpf.Controls;

/// <summary>
/// Tactical Advisor Speech Box inspired by ROTK advisor dialogues.
/// Displays advisor portrait, advisor name & title, strategic guidance / market intel quote,
/// and an optional 1-click Quick Action button.
/// </summary>
public sealed class GameAdvisorBox : ContentControl
{
    public static readonly DependencyProperty AdvisorNameProperty =
        DependencyProperty.Register(
            nameof(AdvisorName),
            typeof(string),
            typeof(GameAdvisorBox),
            new FrameworkPropertyMetadata("CỐ VẤN CHIẾN LƯỢC"));

    public static readonly DependencyProperty AdvisorTitleProperty =
        DependencyProperty.Register(
            nameof(AdvisorTitle),
            typeof(string),
            typeof(GameAdvisorBox),
            new FrameworkPropertyMetadata("Quân Sư / Chưởng Quỹ"));

    public static readonly DependencyProperty AdviceMessageProperty =
        DependencyProperty.Register(
            nameof(AdviceMessage),
            typeof(string),
            typeof(GameAdvisorBox),
            new FrameworkPropertyMetadata("Thời tiết thuận lợi, hàng hóa lưu thông êm ả. Đại nhân có thể điều động thêm đoàn buôn."));

    public static readonly DependencyProperty ActionButtonLabelProperty =
        DependencyProperty.Register(
            nameof(ActionButtonLabel),
            typeof(string),
            typeof(GameAdvisorBox),
            new FrameworkPropertyMetadata(null));

    public static readonly DependencyProperty AvatarInitialsProperty =
        DependencyProperty.Register(
            nameof(AvatarInitials),
            typeof(string),
            typeof(GameAdvisorBox),
            new FrameworkPropertyMetadata("Sư"));

    public static readonly RoutedEvent ActionClickEvent =
        EventManager.RegisterRoutedEvent(
            nameof(ActionClick),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(GameAdvisorBox));

    public string AdvisorName
    {
        get => (string)GetValue(AdvisorNameProperty);
        set => SetValue(AdvisorNameProperty, value);
    }

    public string AdvisorTitle
    {
        get => (string)GetValue(AdvisorTitleProperty);
        set => SetValue(AdvisorTitleProperty, value);
    }

    public string AdviceMessage
    {
        get => (string)GetValue(AdviceMessageProperty);
        set => SetValue(AdviceMessageProperty, value);
    }

    public string? ActionButtonLabel
    {
        get => (string?)GetValue(ActionButtonLabelProperty);
        set => SetValue(ActionButtonLabelProperty, value);
    }

    public string AvatarInitials
    {
        get => (string)GetValue(AvatarInitialsProperty);
        set => SetValue(AvatarInitialsProperty, value);
    }

    public event RoutedEventHandler ActionClick
    {
        add => AddHandler(ActionClickEvent, value);
        remove => RemoveHandler(ActionClickEvent, value);
    }

    private readonly Border _rootBorder;
    private readonly TextBlock _nameText;
    private readonly TextBlock _titleText;
    private readonly TextBlock _messageText;
    private readonly GameButton _actionBtn;
    private readonly Border _avatarBorder;
    private readonly TextBlock _avatarText;

    public GameAdvisorBox()
    {
        Focusable = false;

        _rootBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xEE, 0x1A, 0x13, 0x0E)), // Translucent dark lacquer
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x4A, 0x38, 0x24)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 10, 12, 10),
            MinHeight = 68
        };

        Grid grid = new();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Avatar frame
        _avatarBorder = new Border
        {
            Width = 44,
            Height = 44,
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(Color.FromRgb(0x35, 0x24, 0x17)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xC4, 0xA0, 0x62)),
            BorderThickness = new Thickness(1.2),
            Margin = new Thickness(0, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        _avatarText = new TextBlock
        {
            FontFamily = new FontFamily("Noto Serif, Georgia, SimSun, Segoe UI"),
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0xF3, 0xE6, 0xCC)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _avatarBorder.Child = _avatarText;
        Grid.SetColumn(_avatarBorder, 0);
        grid.Children.Add(_avatarBorder);

        // Text content
        StackPanel textPanel = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };

        StackPanel headerPanel = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 3) };
        _nameText = new TextBlock
        {
            FontFamily = new FontFamily("Noto Serif, Georgia, SimSun, Segoe UI"),
            FontSize = 12.5,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0xC4, 0xA0, 0x62)),
            Margin = new Thickness(0, 0, 8, 0)
        };
        _titleText = new TextBlock
        {
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(0x8F, 0x7F, 0x62)),
            VerticalAlignment = VerticalAlignment.Center
        };
        headerPanel.Children.Add(_nameText);
        headerPanel.Children.Add(_titleText);
        textPanel.Children.Add(headerPanel);

        _messageText = new TextBlock
        {
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xD4, 0xBE)),
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 17
        };
        textPanel.Children.Add(_messageText);

        Grid.SetColumn(textPanel, 1);
        grid.Children.Add(textPanel);

        // Action button
        _actionBtn = new GameButton
        {
            Variant = GameButtonVariant.Primary,
            IsCompact = true,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 0, 0)
        };
        _actionBtn.Click += (s, e) => RaiseEvent(new RoutedEventArgs(ActionClickEvent));

        Grid.SetColumn(_actionBtn, 2);
        grid.Children.Add(_actionBtn);

        _rootBorder.Child = grid;
        Content = _rootBorder;

        Loaded += (s, e) => UpdateBindings();
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.Property == AdvisorNameProperty && _nameText != null)
        {
            _nameText.Text = AdvisorName;
        }
        else if (e.Property == AdvisorTitleProperty && _titleText != null)
        {
            _titleText.Text = AdvisorTitle;
        }
        else if (e.Property == AdviceMessageProperty && _messageText != null)
        {
            _messageText.Text = AdviceMessage;
        }
        else if (e.Property == AvatarInitialsProperty && _avatarText != null)
        {
            _avatarText.Text = AvatarInitials;
        }
        else if (e.Property == ActionButtonLabelProperty && _actionBtn != null)
        {
            _actionBtn.Label = ActionButtonLabel ?? string.Empty;
            _actionBtn.Visibility = string.IsNullOrEmpty(ActionButtonLabel) ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    private void UpdateBindings()
    {
        _nameText.Text = AdvisorName;
        _titleText.Text = AdvisorTitle;
        _messageText.Text = AdviceMessage;
        _avatarText.Text = AvatarInitials;
        _actionBtn.Label = ActionButtonLabel ?? string.Empty;
        _actionBtn.Visibility = string.IsNullOrEmpty(ActionButtonLabel) ? Visibility.Collapsed : Visibility.Visible;
    }
}
