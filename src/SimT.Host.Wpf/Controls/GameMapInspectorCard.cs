using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace SimT.Host.Wpf.Controls;

/// <summary>
/// Contextual Map Inspector Card inspired by ROTK terrain & city inspector (Bottom-Right HUD).
/// Shows local citadel/territory info: controlling faction, prosperity, commerce tax, security,
/// and instant action shortcuts when a player inspects a map node.
/// </summary>
public sealed class GameMapInspectorCard : ContentControl
{
    public static readonly DependencyProperty LocationNameProperty =
        DependencyProperty.Register(
            nameof(LocationName),
            typeof(string),
            typeof(GameMapInspectorCard),
            new FrameworkPropertyMetadata("LẠC DƯƠNG"));

    public static readonly DependencyProperty FactionNameProperty =
        DependencyProperty.Register(
            nameof(FactionName),
            typeof(string),
            typeof(GameMapInspectorCard),
            new FrameworkPropertyMetadata("Triều Đình Đông Hán"));

    public static readonly DependencyProperty ProsperityTextProperty =
        DependencyProperty.Register(
            nameof(ProsperityText),
            typeof(string),
            typeof(GameMapInspectorCard),
            new FrameworkPropertyMetadata("Thịnh Vượng (Cấp 4)"));

    public static readonly DependencyProperty CommerceTaxTextProperty =
        DependencyProperty.Register(
            nameof(CommerceTaxText),
            typeof(string),
            typeof(GameMapInspectorCard),
            new FrameworkPropertyMetadata("Thuế: 5%"));

    public static readonly DependencyProperty SecurityTextProperty =
        DependencyProperty.Register(
            nameof(SecurityText),
            typeof(string),
            typeof(GameMapInspectorCard),
            new FrameworkPropertyMetadata("Trị An: Nghiêm Minh"));

    public static readonly DependencyProperty SpecialtyTextProperty =
        DependencyProperty.Register(
            nameof(SpecialtyText),
            typeof(string),
            typeof(GameMapInspectorCard),
            new FrameworkPropertyMetadata("Đặc sản: Tơ Lụa, Rượu Nho"));

    public static readonly RoutedEvent VisitMarketClickEvent =
        EventManager.RegisterRoutedEvent(
            nameof(VisitMarketClick),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(GameMapInspectorCard));

    public static readonly RoutedEvent FastTravelClickEvent =
        EventManager.RegisterRoutedEvent(
            nameof(FastTravelClick),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(GameMapInspectorCard));

    public string LocationName
    {
        get => (string)GetValue(LocationNameProperty);
        set => SetValue(LocationNameProperty, value);
    }

    public string FactionName
    {
        get => (string)GetValue(FactionNameProperty);
        set => SetValue(FactionNameProperty, value);
    }

    public string ProsperityText
    {
        get => (string)GetValue(ProsperityTextProperty);
        set => SetValue(ProsperityTextProperty, value);
    }

    public string CommerceTaxText
    {
        get => (string)GetValue(CommerceTaxTextProperty);
        set => SetValue(CommerceTaxTextProperty, value);
    }

    public string SecurityText
    {
        get => (string)GetValue(SecurityTextProperty);
        set => SetValue(SecurityTextProperty, value);
    }

    public string SpecialtyText
    {
        get => (string)GetValue(SpecialtyTextProperty);
        set => SetValue(SpecialtyTextProperty, value);
    }

    public event RoutedEventHandler VisitMarketClick
    {
        add => AddHandler(VisitMarketClickEvent, value);
        remove => RemoveHandler(VisitMarketClickEvent, value);
    }

    public event RoutedEventHandler FastTravelClick
    {
        add => AddHandler(FastTravelClickEvent, value);
        remove => RemoveHandler(FastTravelClickEvent, value);
    }

    private readonly TextBlock _locationBlock;
    private readonly TextBlock _factionBlock;
    private readonly TextBlock _prosperityBlock;
    private readonly TextBlock _taxBlock;
    private readonly TextBlock _securityBlock;
    private readonly TextBlock _specialtyBlock;

    public GameMapInspectorCard()
    {
        Focusable = false;

        Border root = new()
        {
            Background = new SolidColorBrush(Color.FromArgb(0xF2, 0x1A, 0x14, 0x0E)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x4A, 0x38, 0x24)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 10, 12, 10),
            Width = 240
        };

        StackPanel panel = new();

        // Header: Location name & Faction
        DockPanel header = new() { Margin = new Thickness(0, 0, 0, 6) };
        _factionBlock = new TextBlock
        {
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(0xC4, 0xA0, 0x62)),
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(_factionBlock, Dock.Right);
        _locationBlock = new TextBlock
        {
            FontFamily = new FontFamily("Noto Serif, Georgia, SimSun, Segoe UI"),
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0xF3, 0xE6, 0xCC)),
            VerticalAlignment = VerticalAlignment.Center
        };
        header.Children.Add(_factionBlock);
        header.Children.Add(_locationBlock);
        panel.Children.Add(header);

        // Divider
        panel.Children.Add(new Border
        {
            Height = 1,
            Background = new SolidColorBrush(Color.FromRgb(0x35, 0x27, 0x1A)),
            Margin = new Thickness(0, 0, 0, 6)
        });

        // Metrics Grid (2 columns)
        Grid metricsGrid = new() { Margin = new Thickness(0, 0, 0, 6) };
        metricsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        metricsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        metricsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        metricsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _prosperityBlock = new TextBlock { FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0xB9, 0xAA, 0x8C)), Margin = new Thickness(0, 0, 0, 3) };
        _taxBlock = new TextBlock { FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0x34, 0xD3, 0x99)), Margin = new Thickness(0, 0, 0, 3) };
        _securityBlock = new TextBlock { FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0xB9, 0xAA, 0x8C)) };

        Grid.SetColumn(_prosperityBlock, 0);
        Grid.SetRow(_prosperityBlock, 0);
        Grid.SetColumn(_taxBlock, 1);
        Grid.SetRow(_taxBlock, 0);
        Grid.SetColumn(_securityBlock, 0);
        Grid.SetRow(_securityBlock, 1);

        metricsGrid.Children.Add(_prosperityBlock);
        metricsGrid.Children.Add(_taxBlock);
        metricsGrid.Children.Add(_securityBlock);
        panel.Children.Add(metricsGrid);

        // Specialty
        _specialtyBlock = new TextBlock
        {
            FontSize = 10.5,
            Foreground = new SolidColorBrush(Color.FromRgb(0x8F, 0x7F, 0x62)),
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 0, 0, 8)
        };
        panel.Children.Add(_specialtyBlock);

        // Action shortcuts
        UniformGrid actions = new() { Columns = 2 };
        GameButton btnMarket = new()
        {
            Label = "Ghé Chợ",
            Variant = GameButtonVariant.Primary,
            IsCompact = true,
            Margin = new Thickness(0, 0, 3, 0)
        };
        btnMarket.Click += (s, e) => RaiseEvent(new RoutedEventArgs(VisitMarketClickEvent));

        GameButton btnTravel = new()
        {
            Label = "Hành Trình",
            Variant = GameButtonVariant.Secondary,
            IsCompact = true,
            Margin = new Thickness(3, 0, 0, 0)
        };
        btnTravel.Click += (s, e) => RaiseEvent(new RoutedEventArgs(FastTravelClickEvent));

        actions.Children.Add(btnMarket);
        actions.Children.Add(btnTravel);
        panel.Children.Add(actions);

        root.Child = panel;
        Content = root;

        Loaded += (s, e) => Sync();
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        Sync();
    }

    private void Sync()
    {
        if (_locationBlock == null) return;
        _locationBlock.Text = LocationName;
        _factionBlock.Text = FactionName;
        _prosperityBlock.Text = ProsperityText;
        _taxBlock.Text = CommerceTaxText;
        _securityBlock.Text = SecurityText;
        _specialtyBlock.Text = SpecialtyText;
    }
}
