using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SimT.Contracts.Modules;

namespace SimT.Host.Wpf.Controls;

/// <summary>
/// Dedicated reusable RPG DLC & Module item card control.
/// Displays module metadata, active state badge, provided capabilities,
/// dependencies, and unlocked screens.
/// </summary>
public sealed class GameDlcCard : Border
{
    private readonly TextBlock _iconText;
    private readonly TextBlock _nameText;
    private readonly TextBlock _idText;
    private readonly TextBlock _versionText;
    private readonly Border _statusBadge;
    private readonly TextBlock _statusText;
    private readonly TextBlock _descText;
    private readonly TextBlock _providesText;
    private readonly TextBlock _requiresText;
    private readonly TextBlock _screensText;

    public GameDlcCard()
    {
        Background = (Brush)Application.Current.FindResource("SurfaceAlt");
        BorderBrush = (Brush)Application.Current.FindResource("Line");
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(6);
        Padding = new Thickness(12, 10, 12, 10);
        Margin = new Thickness(0, 0, 0, 8);

        var stack = new StackPanel();

        // Header Line
        var headerDock = new DockPanel();

        _statusBadge = new Border
        {
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2, 6, 2),
            VerticalAlignment = VerticalAlignment.Center
        };
        _statusText = new TextBlock
        {
            FontSize = 10,
            FontWeight = FontWeights.Bold
        };
        _statusBadge.Child = _statusText;
        DockPanel.SetDock(_statusBadge, Dock.Right);
        headerDock.Children.Add(_statusBadge);

        var titleStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        _iconText = new TextBlock
        {
            FontSize = 16,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        titleStack.Children.Add(_iconText);

        _nameText = new TextBlock
        {
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)Application.Current.FindResource("Text"),
            VerticalAlignment = VerticalAlignment.Center
        };
        titleStack.Children.Add(_nameText);

        _versionText = new TextBlock
        {
            FontSize = 11,
            Foreground = (Brush)Application.Current.FindResource("Faint"),
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        titleStack.Children.Add(_versionText);

        headerDock.Children.Add(titleStack);
        stack.Children.Add(headerDock);

        // ID line & Description
        var idLine = new DockPanel { Margin = new Thickness(0, 3, 0, 4) };
        _idText = new TextBlock
        {
            Style = (Style)Application.Current.FindResource("Caption"),
            Foreground = (Brush)Application.Current.FindResource("Faint"),
            FontSize = 10.5
        };
        idLine.Children.Add(_idText);
        stack.Children.Add(idLine);

        _descText = new TextBlock
        {
            Style = (Style)Application.Current.FindResource("Caption"),
            Foreground = (Brush)Application.Current.FindResource("Muted"),
            Margin = new Thickness(0, 0, 0, 6),
            TextWrapping = TextWrapping.Wrap
        };
        stack.Children.Add(_descText);

        // Technical details grid
        var techBorder = new Border
        {
            Background = (Brush)Application.Current.FindResource("Surface"),
            BorderBrush = (Brush)Application.Current.FindResource("Line"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 6, 8, 6)
        };
        var techStack = new StackPanel();

        var provLine = new DockPanel { Margin = new Thickness(0, 0, 0, 2) };
        provLine.Children.Add(new TextBlock { Text = "⚡ Cung cấp: ", Style = (Style)Application.Current.FindResource("Caption"), Foreground = (Brush)Application.Current.FindResource("Faint"), Width = 75 });
        _providesText = new TextBlock { Style = (Style)Application.Current.FindResource("Caption"), Foreground = (Brush)Application.Current.FindResource("Accent"), TextWrapping = TextWrapping.Wrap };
        provLine.Children.Add(_providesText);
        techStack.Children.Add(provLine);

        var reqLine = new DockPanel { Margin = new Thickness(0, 0, 0, 2) };
        reqLine.Children.Add(new TextBlock { Text = "🔗 Yêu cầu: ", Style = (Style)Application.Current.FindResource("Caption"), Foreground = (Brush)Application.Current.FindResource("Faint"), Width = 75 });
        _requiresText = new TextBlock { Style = (Style)Application.Current.FindResource("Caption"), Foreground = (Brush)Application.Current.FindResource("Text"), TextWrapping = TextWrapping.Wrap };
        reqLine.Children.Add(_requiresText);
        techStack.Children.Add(reqLine);

        var scrLine = new DockPanel();
        scrLine.Children.Add(new TextBlock { Text = "🖥️ Màn hình: ", Style = (Style)Application.Current.FindResource("Caption"), Foreground = (Brush)Application.Current.FindResource("Faint"), Width = 75 });
        _screensText = new TextBlock { Style = (Style)Application.Current.FindResource("Caption"), Foreground = (Brush)Application.Current.FindResource("Gain"), TextWrapping = TextWrapping.Wrap };
        scrLine.Children.Add(_screensText);
        techStack.Children.Add(scrLine);

        techBorder.Child = techStack;
        stack.Children.Add(techBorder);

        Child = stack;

        MouseEnter += (_, _) => BorderBrush = (Brush)Application.Current.FindResource("LineSoft") ?? Brushes.SlateGray;
        MouseLeave += (_, _) => BorderBrush = (Brush)Application.Current.FindResource("Line");
    }

    public void Configure(ModuleManifest m, bool isEnabled, string viName, string glyph, string description)
    {
        _iconText.Text = glyph;
        _nameText.Text = viName;
        _versionText.Text = $"v{m.Version}";
        _idText.Text = $"Mã định danh: {m.Id} · API: {m.ApiVersion} · Loại: {m.Kind.ToUpperInvariant()}";
        _descText.Text = description;

        if (isEnabled)
        {
            _statusBadge.Background = (Brush)Application.Current.FindResource("Surface");
            _statusBadge.BorderBrush = (Brush)Application.Current.FindResource("Gain");
            _statusBadge.BorderThickness = new Thickness(1);
            _statusText.Text = "● ĐANG KÍCH HOẠT";
            _statusText.Foreground = (Brush)Application.Current.FindResource("Gain");
        }
        else
        {
            _statusBadge.Background = (Brush)Application.Current.FindResource("Surface");
            _statusBadge.BorderBrush = (Brush)Application.Current.FindResource("Line");
            _statusBadge.BorderThickness = new Thickness(1);
            _statusText.Text = "○ TẮT";
            _statusText.Foreground = (Brush)Application.Current.FindResource("Faint");
        }

        _providesText.Text = m.Provides.Count > 0 ? string.Join(", ", m.Provides) : "Không (Dịch vụ nội bộ)";
        _requiresText.Text = m.Requires.Count > 0 ? string.Join(", ", m.Requires) : "Độc lập (Không phụ thuộc)";
        _screensText.Text = m.Screens.Count > 0 ? string.Join(", ", m.Screens) : "Không (Chạy ngầm)";
    }
}
