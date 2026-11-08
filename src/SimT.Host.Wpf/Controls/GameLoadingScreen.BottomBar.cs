using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Path = System.IO.Path;

namespace SimT.Host.Wpf.Controls;

public sealed partial class GameLoadingScreen
{
    private static BitmapSource[]? s_travelerFrames;

    private TextBlock _tipText = null!;
    private StackPanel _enterPanel = null!;
    private Border _enterBtnBorder = null!;
    private Canvas _roadCanvas = null!;
    private Line _roadDashedLine = null!;
    private Border _roadFillBar = null!;
    private readonly Border[] _milestoneTicks = new Border[5];
    private Image _travelerImage = null!;
    private TextBlock _statusText = null!;
    private TextBlock _percentText = null!;

    private void BuildBottomBar(Grid root)
    {
        var bottomBorder = new Border
        {
            Height = 232,
            VerticalAlignment = VerticalAlignment.Bottom,
            Background = BottomBgBrush,
            BorderBrush = BorderLineBrush,
            BorderThickness = new Thickness(0, 1, 0, 0)
        };

        var bottomGrid = new Grid();

        BuildTipBlock(bottomGrid);
        BuildEnterButton(bottomGrid);
        BuildRoadCanvas(bottomGrid);
        BuildStatusRow(bottomGrid);

        bottomBorder.Child = bottomGrid;
        root.Children.Add(bottomBorder);
    }

    private void BuildTipBlock(Grid bottomGrid)
    {
        var tipStack = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(64, 26, 0, 0),
            Width = 760
        };

        tipStack.Children.Add(new TextBlock
        {
            Text = "MẸO",
            FontFamily = SansFont,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = GoldHeaderBrush
        });

        _tipText = new TextBlock
        {
            Text = LoadingProgressState.Tips[0],
            FontFamily = SansFont,
            FontSize = 17,
            LineHeight = 25.5,
            Foreground = SilkTextBrush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0)
        };

        tipStack.Children.Add(_tipText);
        bottomGrid.Children.Add(tipStack);
    }

    private void BuildEnterButton(Grid bottomGrid)
    {
        _enterPanel = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            // One row, kept high: the walker stands at the road's end right below it.
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 14, 64, 0),
            Visibility = Visibility.Collapsed
        };
        Panel.SetZIndex(_enterPanel, 10);

        _enterBtnBorder = new Border
        {
            Height = 48,
            Padding = new Thickness(28, 0, 28, 0),
            Background = SealBgBrush,
            BorderBrush = GoldAccentBrush,
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Right,
            Child = new TextBlock
            {
                Text = "Vào thành",
                FontFamily = SansFont,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = IvoryBrush,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        _enterBtnBorder.MouseEnter += (_, _) => _enterBtnBorder.Background = SealHoverBrush;
        _enterBtnBorder.MouseLeave += (_, _) => _enterBtnBorder.Background = SealBgBrush;
        _enterBtnBorder.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            TriggerEnterCity();
        };

        _enterPanel.Children.Add(new TextBlock
        {
            Text = "hoặc nhấn Enter",
            FontFamily = SansFont,
            FontSize = 13,
            Foreground = StepDoneTextBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 16, 0)
        });
        _enterPanel.Children.Add(_enterBtnBorder);

        bottomGrid.Children.Add(_enterPanel);
    }

    private void BuildRoadCanvas(Grid bottomGrid)
    {
        _roadCanvas = new Canvas
        {
            Height = 20,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(64, 151, 64, 0),
            ClipToBounds = false
        };

        _roadDashedLine = new Line
        {
            X1 = 0,
            Y1 = 0,
            X2 = 1152,
            Y2 = 0,
            Stroke = RoadMutedBrush,
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection([3.0, 3.0])
        };
        _roadCanvas.Children.Add(_roadDashedLine);

        _roadFillBar = new Border
        {
            Height = 4,
            Width = 0,
            Background = GoldAccentBrush
        };
        Canvas.SetLeft(_roadFillBar, 0);
        Canvas.SetTop(_roadFillBar, -1);
        _roadCanvas.Children.Add(_roadFillBar);

        for (var i = 0; i < 5; i++)
        {
            var tick = new Border
            {
                Width = 2,
                Height = 16,
                Background = RoadMutedBrush
            };
            Canvas.SetTop(tick, -7);
            _milestoneTicks[i] = tick;
            _roadCanvas.Children.Add(tick);
        }

        _travelerImage = new Image
        {
            Width = 340 * 0.62,
            Height = 170 * 0.62,
            Stretch = Stretch.Fill,
            IsHitTestVisible = false
        };
        RenderOptions.SetBitmapScalingMode(_travelerImage, BitmapScalingMode.NearestNeighbor);
        var frames = GetTravelerFrames();
        if (frames.Length > 0)
            _travelerImage.Source = frames[0];
        Canvas.SetLeft(_travelerImage, -105.4);
        Canvas.SetTop(_travelerImage, -102.54);
        _roadCanvas.Children.Add(_travelerImage);

        bottomGrid.Children.Add(_roadCanvas);
    }

    private void BuildStatusRow(Grid bottomGrid)
    {
        var statusRow = new DockPanel
        {
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(64, 172, 64, 0),
            LastChildFill = true
        };

        _percentText = new TextBlock
        {
            Text = "0%",
            FontFamily = SansFont,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = GoldHeaderBrush
        };
        DockPanel.SetDock(_percentText, Dock.Right);

        _statusText = new TextBlock
        {
            Text = "Mở sổ sách ván chơi…",
            FontFamily = SansFont,
            FontSize = 15,
            Foreground = SublineBrush
        };

        statusRow.Children.Add(_percentText);
        statusRow.Children.Add(_statusText);
        bottomGrid.Children.Add(statusRow);
    }

    private static BitmapSource[] GetTravelerFrames()
    {
        if (s_travelerFrames is not null)
            return s_travelerFrames;

        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "assets", "scenes", "art", "anim", "traveler.png");
            if (!File.Exists(path))
                path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "assets", "scenes", "art", "anim", "traveler.png"));

            if (File.Exists(path))
            {
                var sheet = new BitmapImage();
                sheet.BeginInit();
                sheet.CacheOption = BitmapCacheOption.OnLoad;
                sheet.UriSource = new Uri(path, UriKind.Absolute);
                sheet.EndInit();
                sheet.Freeze();

                var frames = new BitmapSource[12];
                for (var i = 0; i < 12; i++)
                {
                    var cropped = new CroppedBitmap(sheet, new Int32Rect(i * 340, 0, 340, 170));
                    cropped.Freeze();
                    frames[i] = cropped;
                }
                s_travelerFrames = frames;
                return frames;
            }
        }
        catch
        {
            // Ignore missing or unreadable sprite sheet in headless environments
        }

        s_travelerFrames = [];
        return s_travelerFrames;
    }
}
