using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SimT.Host.Wpf.Controls;

/// <summary>
/// Dedicated reusable RPG Treasury & Wealth HUD control.
/// Displays liquid silver, vault reserves, merchandise value,
/// total net worth, and economic social standing.
/// </summary>
public sealed class GameTreasuryCard : Border
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    private readonly TextBlock _liquidText;
    private readonly TextBlock _standingText;
    private readonly TextBlock _liquidDetailText;
    private readonly TextBlock _safeDetailText;
    private readonly TextBlock _goodsDetailText;
    private readonly TextBlock _netWorthDetailText;

    public GameTreasuryCard()
    {
        Background = (Brush)Application.Current.FindResource("Surface");
        BorderBrush = (Brush)Application.Current.FindResource("Line");
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(8);
        Padding = new Thickness(11, 10, 11, 10);
        Margin = new Thickness(0, 8, 0, 0);

        var stack = new StackPanel();

        // Header
        var headerDock = new DockPanel();
        _standingText = new TextBlock
        {
            Style = (Style)Application.Current.FindResource("Caption"),
            Foreground = (Brush)Application.Current.FindResource("Accent"),
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(_standingText, Dock.Right);
        headerDock.Children.Add(_standingText);

        var titleText = new TextBlock
        {
            Style = (Style)Application.Current.FindResource("Overline"),
            Text = "TÀI SẢN & KINH TẾ",
            VerticalAlignment = VerticalAlignment.Center
        };
        headerDock.Children.Add(titleText);
        stack.Children.Add(headerDock);

        // Big Cash Display
        var cashDock = new DockPanel { Margin = new Thickness(0, 3, 0, 4) };
        var unitText = new TextBlock
        {
            Text = "quan",
            Style = (Style)Application.Current.FindResource("Caption"),
            Foreground = (Brush)Application.Current.FindResource("Faint"),
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(6, 0, 0, 4)
        };
        DockPanel.SetDock(unitText, Dock.Right);
        cashDock.Children.Add(unitText);

        _liquidText = new TextBlock
        {
            Style = (Style)Application.Current.FindResource("NumberLarge"),
            Foreground = (Brush)Application.Current.FindResource("Accent"),
            FontWeight = FontWeights.Bold,
            FontSize = 22,
            RenderTransformOrigin = new Point(0.0, 0.5),
            RenderTransform = new ScaleTransform(1.0, 1.0)
        };
        cashDock.Children.Add(_liquidText);
        stack.Children.Add(cashDock);

        // Separator
        var sep = new Border
        {
            Height = 1,
            Background = (Brush)Application.Current.FindResource("Line"),
            Margin = new Thickness(0, 2, 0, 6)
        };
        stack.Children.Add(sep);

        // Breakdown Grid (2 rows x 2 cols)
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Row 0 Col 0: Tiền mặt
        var p0 = new StackPanel { Margin = new Thickness(0, 0, 4, 4) };
        p0.Children.Add(new TextBlock { Text = "🎒 Tiền mặt", Style = (Style)Application.Current.FindResource("Caption"), Foreground = (Brush)Application.Current.FindResource("Faint"), FontSize = 10 });
        _liquidDetailText = new TextBlock { Style = (Style)Application.Current.FindResource("Caption"), FontWeight = FontWeights.SemiBold, Foreground = (Brush)Application.Current.FindResource("Text") };
        p0.Children.Add(_liquidDetailText);
        Grid.SetRow(p0, 0);
        Grid.SetColumn(p0, 0);
        grid.Children.Add(p0);

        // Row 0 Col 1: Két sắt
        var p1 = new StackPanel { Margin = new Thickness(4, 0, 0, 4) };
        p1.Children.Add(new TextBlock { Text = "🏦 Két sắt", Style = (Style)Application.Current.FindResource("Caption"), Foreground = (Brush)Application.Current.FindResource("Faint"), FontSize = 10 });
        _safeDetailText = new TextBlock { Style = (Style)Application.Current.FindResource("Caption"), FontWeight = FontWeights.SemiBold, Foreground = (Brush)Application.Current.FindResource("Muted") };
        p1.Children.Add(_safeDetailText);
        Grid.SetRow(p1, 0);
        Grid.SetColumn(p1, 1);
        grid.Children.Add(p1);

        // Row 1 Col 0: Hàng hóa
        var p2 = new StackPanel { Margin = new Thickness(0, 2, 4, 0) };
        p2.Children.Add(new TextBlock { Text = "📦 Hàng hóa", Style = (Style)Application.Current.FindResource("Caption"), Foreground = (Brush)Application.Current.FindResource("Faint"), FontSize = 10 });
        _goodsDetailText = new TextBlock { Style = (Style)Application.Current.FindResource("Caption"), FontWeight = FontWeights.SemiBold, Foreground = (Brush)Application.Current.FindResource("Text") };
        p2.Children.Add(_goodsDetailText);
        Grid.SetRow(p2, 1);
        Grid.SetColumn(p2, 0);
        grid.Children.Add(p2);

        // Row 1 Col 1: Tổng tài sản
        var p3 = new StackPanel { Margin = new Thickness(4, 2, 0, 0) };
        p3.Children.Add(new TextBlock { Text = "💎 Tổng ròng", Style = (Style)Application.Current.FindResource("Caption"), Foreground = (Brush)Application.Current.FindResource("Faint"), FontSize = 10 });
        _netWorthDetailText = new TextBlock { Style = (Style)Application.Current.FindResource("Caption"), FontWeight = FontWeights.Bold, Foreground = (Brush)Application.Current.FindResource("Accent") };
        p3.Children.Add(_netWorthDetailText);
        Grid.SetRow(p3, 1);
        Grid.SetColumn(p3, 1);
        grid.Children.Add(p3);

        stack.Children.Add(grid);
        Child = stack;

        MouseEnter += (_, _) => BorderBrush = (Brush)Application.Current.FindResource("LineSoft") ?? Brushes.SlateGray;
        MouseLeave += (_, _) => BorderBrush = (Brush)Application.Current.FindResource("Line");
    }

    private long _prevLiquid = -1;

    public void Configure(long liquid, long safe, long goods, long netWorth)
    {
        if (_prevLiquid >= 0 && liquid != _prevLiquid)
        {
            var isGain = liquid > _prevLiquid;
            var flashBrush = (Brush)Application.Current.FindResource(isGain ? "Gain" : "Loss");
            _liquidText.Foreground = flashBrush;

            var scaleAnim = new System.Windows.Media.Animation.DoubleAnimation(1.18, 1.0, TimeSpan.FromMilliseconds(320))
            {
                EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };
            scaleAnim.Completed += (_, _) =>
            {
                _liquidText.Foreground = (Brush)Application.Current.FindResource("Accent");
            };
            if (_liquidText.RenderTransform is ScaleTransform st)
            {
                st.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
                st.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
            }
        }
        _prevLiquid = liquid;

        _liquidText.Text = liquid.ToString("N0", Vietnamese);
        _liquidDetailText.Text = $"{liquid:N0} Q";
        _safeDetailText.Text = safe > 0 ? $"{safe:N0} Q" : "0 Q";
        _goodsDetailText.Text = $"{goods:N0} Q";
        _netWorthDetailText.Text = $"{netWorth:N0} Q";

        _standingText.Text = netWorth switch
        {
            >= 50000 => "★ Cự Phú",
            >= 20000 => "★ Phú Thương",
            >= 8000 => "★ Tiểu Khang",
            >= 3000 => "No Đủ",
            _ => "Bần Hàn"
        };
    }
}
