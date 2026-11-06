using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SimT.Host.Wpf.Controls;

public enum ProposalSeverity
{
    Opportunity, // Green/Gold for profit, merchant routes, talent discovery
    Alert,       // Yellow/Orange for warnings, price fluctuations
    Crisis       // Red for bandits, sieges, disasters
}

/// <summary>
/// Strategic Proposal & Event Alert Card inspired by ROTK suggestion slots.
/// Displays actionable advice/events docked at the top-right of the screen
/// so the player can react to emergent world events with one click.
/// </summary>
public sealed class GameProposalCard : ContentControl
{
    public static readonly DependencyProperty ProposerNameProperty =
        DependencyProperty.Register(
            nameof(ProposerName),
            typeof(string),
            typeof(GameProposalCard),
            new FrameworkPropertyMetadata("ĐỀ XUẤT"));

    public static readonly DependencyProperty ProposalTitleProperty =
        DependencyProperty.Register(
            nameof(ProposalTitle),
            typeof(string),
            typeof(GameProposalCard),
            new FrameworkPropertyMetadata("Cơ hội giao thương mới"));

    public static readonly DependencyProperty ProposalDetailsProperty =
        DependencyProperty.Register(
            nameof(ProposalDetails),
            typeof(string),
            typeof(GameProposalCard),
            new FrameworkPropertyMetadata(null));

    public static readonly DependencyProperty ActionLabelProperty =
        DependencyProperty.Register(
            nameof(ActionLabel),
            typeof(string),
            typeof(GameProposalCard),
            new FrameworkPropertyMetadata("Xem xét"));

    public static readonly DependencyProperty SeverityProperty =
        DependencyProperty.Register(
            nameof(Severity),
            typeof(ProposalSeverity),
            typeof(GameProposalCard),
            new FrameworkPropertyMetadata(ProposalSeverity.Opportunity, OnSeverityChanged));

    public static readonly RoutedEvent ActionClickEvent =
        EventManager.RegisterRoutedEvent(
            nameof(ActionClick),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(GameProposalCard));

    public static readonly RoutedEvent DismissClickEvent =
        EventManager.RegisterRoutedEvent(
            nameof(DismissClick),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(GameProposalCard));

    public string ProposerName
    {
        get => (string)GetValue(ProposerNameProperty);
        set => SetValue(ProposerNameProperty, value);
    }

    public string ProposalTitle
    {
        get => (string)GetValue(ProposalTitleProperty);
        set => SetValue(ProposalTitleProperty, value);
    }

    public string? ProposalDetails
    {
        get => (string?)GetValue(ProposalDetailsProperty);
        set => SetValue(ProposalDetailsProperty, value);
    }

    public string ActionLabel
    {
        get => (string)GetValue(ActionLabelProperty);
        set => SetValue(ActionLabelProperty, value);
    }

    public ProposalSeverity Severity
    {
        get => (ProposalSeverity)GetValue(SeverityProperty);
        set => SetValue(SeverityProperty, value);
    }

    public event RoutedEventHandler ActionClick
    {
        add => AddHandler(ActionClickEvent, value);
        remove => RemoveHandler(ActionClickEvent, value);
    }

    public event RoutedEventHandler DismissClick
    {
        add => AddHandler(DismissClickEvent, value);
        remove => RemoveHandler(DismissClickEvent, value);
    }

    private readonly Border _rootBorder;
    private readonly Border _severityStrip;
    private readonly TextBlock _proposerText;
    private readonly TextBlock _titleText;
    private readonly TextBlock _detailsText;
    private readonly GameButton _actionBtn;
    private readonly Button _dismissBtn;

    public GameProposalCard()
    {
        Focusable = false;

        _rootBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xF2, 0x1F, 0x17, 0x11)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x42, 0x33, 0x22)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(10, 8, 10, 8),
            Width = 260
        };

        Grid grid = new();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Left severity indicator bar
        _severityStrip = new Border
        {
            Width = 3.5,
            CornerRadius = new CornerRadius(1.5),
            Margin = new Thickness(0, 1, 10, 1)
        };
        Grid.SetColumn(_severityStrip, 0);
        grid.Children.Add(_severityStrip);

        // Content
        StackPanel centerPanel = new() { VerticalAlignment = VerticalAlignment.Center };
        _proposerText = new TextBlock
        {
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0xC4, 0xA0, 0x62)),
            Margin = new Thickness(0, 0, 0, 1)
        };
        _titleText = new TextBlock
        {
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0xF3, 0xE6, 0xCC)),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        _detailsText = new TextBlock
        {
            FontSize = 10.5,
            Foreground = new SolidColorBrush(Color.FromRgb(0x8F, 0x7F, 0x62)),
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 1, 0, 0)
        };
        centerPanel.Children.Add(_proposerText);
        centerPanel.Children.Add(_titleText);
        centerPanel.Children.Add(_detailsText);

        Grid.SetColumn(centerPanel, 1);
        grid.Children.Add(centerPanel);

        // Buttons panel
        StackPanel rightPanel = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };

        _actionBtn = new GameButton
        {
            Variant = GameButtonVariant.Primary,
            IsCompact = true,
            Label = ActionLabel
        };
        _actionBtn.Click += (s, e) => RaiseEvent(new RoutedEventArgs(ActionClickEvent));
        rightPanel.Children.Add(_actionBtn);

        _dismissBtn = new Button
        {
            Content = "×",
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.FromRgb(0x8F, 0x7F, 0x62)),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Margin = new Thickness(6, 0, 0, 0),
            Padding = new Thickness(4, 0, 4, 0),
            ToolTip = "Bỏ qua đề xuất này"
        };
        _dismissBtn.Click += (s, e) => RaiseEvent(new RoutedEventArgs(DismissClickEvent));
        rightPanel.Children.Add(_dismissBtn);

        Grid.SetColumn(rightPanel, 2);
        grid.Children.Add(rightPanel);

        _rootBorder.Child = grid;
        Content = _rootBorder;

        ApplySeverity();
        Loaded += (s, e) => SyncBindings();
    }

    private static void OnSeverityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GameProposalCard card)
        {
            card.ApplySeverity();
        }
    }

    private void ApplySeverity()
    {
        Color barColor = Severity switch
        {
            ProposalSeverity.Opportunity => Color.FromRgb(0x3F, 0x6B, 0x55), // Forest jade green
            ProposalSeverity.Alert => Color.FromRgb(0xC4, 0xA0, 0x62),       // Bronze gold
            ProposalSeverity.Crisis => Color.FromRgb(0xA8, 0x32, 0x1F),      // Cinnabar red
            _ => Color.FromRgb(0xC4, 0xA0, 0x62)
        };
        _severityStrip.Background = new SolidColorBrush(barColor);
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.Property == ProposerNameProperty && _proposerText != null)
        {
            _proposerText.Text = ProposerName;
        }
        else if (e.Property == ProposalTitleProperty && _titleText != null)
        {
            _titleText.Text = ProposalTitle;
        }
        else if (e.Property == ProposalDetailsProperty && _detailsText != null)
        {
            _detailsText.Text = ProposalDetails ?? string.Empty;
            _detailsText.Visibility = string.IsNullOrEmpty(ProposalDetails) ? Visibility.Collapsed : Visibility.Visible;
        }
        else if (e.Property == ActionLabelProperty && _actionBtn != null)
        {
            _actionBtn.Label = ActionLabel;
        }
    }

    private void SyncBindings()
    {
        _proposerText.Text = ProposerName;
        _titleText.Text = ProposalTitle;
        _detailsText.Text = ProposalDetails ?? string.Empty;
        _detailsText.Visibility = string.IsNullOrEmpty(ProposalDetails) ? Visibility.Collapsed : Visibility.Visible;
        _actionBtn.Label = ActionLabel;
    }
}
