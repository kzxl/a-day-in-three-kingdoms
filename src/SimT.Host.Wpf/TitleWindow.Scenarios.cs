using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SimT.Contracts.Products;
using SimT.Host.Wpf.Controls;
namespace SimT.Host.Wpf;

public partial class TitleWindow
{
    private Border? _scenarioPicker;
    private void ShowScenarioPicker()
    {
        if (_scenarioPicker is not null) return;
        var paper = new StackPanel { Margin = new Thickness(24) };
        paper.Children.Add(new TextBlock { Text = "CHỌN THỜI ĐIỂM KHỞI NGHIỆP", FontSize = 23,
            FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("Accent") });
        paper.Children.Add(new TextBlock { Text = "Mỗi thời điểm mở ra một thế cục. Chọn một thẻ để bước vào thiên hạ.",
            Margin = new Thickness(0, 7, 0, 18), Foreground = (Brush)FindResource("Muted") });
        var grid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 };
        foreach (var scenario in StartingScenarios.All)
        {
            var body = new StackPanel { Margin = new Thickness(15) };
            body.Children.Add(new TextBlock { Text = scenario.StartDate.Year.ToString(), FontSize = 35,
                FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("Accent") });
            body.Children.Add(new TextBlock { Text = scenario.NameVi, FontSize = 18, FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("Ink"), Margin = new Thickness(0, 2, 0, 9) });
            body.Children.Add(new TextBlock { Text = scenario.DescriptionVi, TextWrapping = TextWrapping.Wrap,
                FontSize = 13, Foreground = (Brush)FindResource("Muted"), MinHeight = 57 });
            var factions = new WrapPanel { Margin = new Thickness(0, 9, 0, 9) };
            foreach (var faction in scenario.Factions)
            {
                var line = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 14, 5) };
                line.Children.Add(new Border { Width = 8, Height = 8, Margin = new Thickness(0, 0, 5, 0),
                    Background = (Brush)new BrushConverter().ConvertFromString(faction.ColorHex)! });
                line.Children.Add(new TextBlock { Text = faction.NameVi, FontSize = 12, Foreground = (Brush)FindResource("Ink") });
                factions.Children.Add(line);
            }
            body.Children.Add(factions);
            body.Children.Add(new TextBlock { Text = $"{scenario.StartDate.Day:00}/{scenario.StartDate.Month:00}/{scenario.StartDate.Year} · Gợi ý: {scenario.SuggestedCityVi}",
                FontSize = 12, Foreground = (Brush)FindResource("Accent") });
            var button = new Button { Content = body, HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Background = (Brush)FindResource("SurfaceAlt"), BorderBrush = (Brush)FindResource("Line"),
                BorderThickness = new Thickness(1), Margin = new Thickness(6), Padding = new Thickness(0),
                ToolTip = $"Khởi nghiệp tại {scenario.SuggestedCityVi}" };
            button.Click += (_, _) => BeginScenario(scenario);
            grid.Children.Add(button);
        }
        paper.Children.Add(grid);
        var back = new GameButton { Label = "Trở lại", Variant = GameButtonVariant.Secondary,
            HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(6, 14, 0, 0) };
        back.Click += (_, _) => HideScenarioPicker();
        paper.Children.Add(back);
        var scroll = new ScrollViewer { Content = paper, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        _scenarioPicker = new Border { Background = (Brush)FindResource("Bg"),
            BorderBrush = (Brush)FindResource("Accent"), BorderThickness = new Thickness(1),
            Margin = new Thickness(24), Child = scroll };
        System.Windows.Input.KeyboardNavigation.SetTabNavigation(_scenarioPicker, System.Windows.Input.KeyboardNavigationMode.Cycle);
        ((Grid)Content).Children.Add(_scenarioPicker);
        ((Button)grid.Children[0]).Focus();
    }

    private void HideScenarioPicker()
    {
        if (_scenarioPicker is not null) ((Grid)Content).Children.Remove(_scenarioPicker);
        _scenarioPicker = null;
        NameBox.Focus();
    }

    private void BeginScenario(StartingScenario scenario)
    {
        if (_isEntering) return;
        var name = string.IsNullOrWhiteSpace(NameBox.Text) ? "Lữ Khách" : NameBox.Text.Trim();
        var state = LoadingProgressState.ForNewSave(name, scenario);
        _ = RunLoadingFlowAsync(state, () =>
        {
            state.MarkStepDone(0);
            return _saves.CreateAndOpen(name, state.OnKernelLog, scenario);
        });
    }
}
