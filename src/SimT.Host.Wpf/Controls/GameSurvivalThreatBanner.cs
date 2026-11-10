using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SimT.Contracts.Playing;

namespace SimT.Host.Wpf.Controls;

/// <summary>
/// Proactive survival & threat awareness banner for the protagonist.
/// Surfaces low-health, extreme-fatigue, mental breakdown, or hostile zone alarms
/// with a 1-click mitigation trigger.
/// </summary>
public sealed class GameSurvivalThreatBanner : Border
{
    private readonly Border _badgeBorder;
    private readonly TextBlock _badgeText;
    private readonly TextBlock _messageText;
    private readonly Border _actionButton;
    private readonly TextBlock _actionLabel;

    private string _currentActionCommand = string.Empty;

    public event Action<string>? ThreatActionTriggered;

    public GameSurvivalThreatBanner()
    {
        CornerRadius = new CornerRadius(5);
        BorderThickness = new Thickness(1);
        Padding = new Thickness(12, 7, 12, 7);
        Margin = new Thickness(0, 0, 0, 8);
        Visibility = Visibility.Collapsed;

        var dock = new DockPanel();

        // Left accent bar
        var accentStrip = new Border
        {
            Width = 3,
            CornerRadius = new CornerRadius(1.5),
            Margin = new Thickness(0, 1, 10, 1)
        };
        DockPanel.SetDock(accentStrip, Dock.Left);
        dock.Children.Add(accentStrip);

        // Badge: e.g. "🩸 NGUY CẤP"
        _badgeBorder = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(7, 2, 7, 2),
            Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        _badgeText = new TextBlock
        {
            FontSize = 10.5,
            FontWeight = FontWeights.Bold
        };
        _badgeBorder.Child = _badgeText;
        DockPanel.SetDock(_badgeBorder, Dock.Left);
        dock.Children.Add(_badgeBorder);

        // Right 1-click action button
        _actionButton = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 3, 10, 3),
            Cursor = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center
        };
        _actionLabel = new TextBlock
        {
            FontSize = 11,
            FontWeight = FontWeights.Bold
        };
        _actionButton.Child = _actionLabel;
        _actionButton.MouseLeftButtonDown += (_, e) =>
        {
            if (!string.IsNullOrEmpty(_currentActionCommand))
            {
                ThreatActionTriggered?.Invoke(_currentActionCommand);
            }
            e.Handled = true;
        };
        DockPanel.SetDock(_actionButton, Dock.Right);
        dock.Children.Add(_actionButton);

        // Center warning message
        _messageText = new TextBlock
        {
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };
        dock.Children.Add(_messageText);

        Child = dock;
    }

    public SurvivalThreatAssessment Update(
        CharacterVitals? vitals,
        int dangerLevel = 1,
        bool isAtWar = false,
        bool hasWarlordExtortion = false,
        bool hasRetainerBetrayalRisk = false)
    {
        var assessment = SurvivalThreatEvaluator.Evaluate(vitals, dangerLevel, isAtWar, hasWarlordExtortion, hasRetainerBetrayalRisk);

        if (assessment.Kind == SurvivalThreatKind.None)
        {
            Visibility = Visibility.Collapsed;
            _currentActionCommand = string.Empty;
            return assessment;
        }

        var borderBrush = (Brush)new BrushConverter().ConvertFromString(assessment.BorderColorHex)!;
        var bgBrush = (Brush)new BrushConverter().ConvertFromString(assessment.BackgroundColorHex)!;

        Background = bgBrush;
        BorderBrush = borderBrush;

        _badgeBorder.Background = bgBrush;
        _badgeBorder.BorderBrush = borderBrush;
        _badgeText.Text = assessment.BadgeText;
        _badgeText.Foreground = borderBrush;

        _messageText.Text = assessment.Message;
        _messageText.Foreground = (Brush)Application.Current.FindResource("Ink");

        _actionButton.Background = (Brush)Application.Current.FindResource("Surface");
        _actionButton.BorderBrush = borderBrush;
        _actionLabel.Text = assessment.ActionLabel;
        _actionLabel.Foreground = borderBrush;

        _currentActionCommand = assessment.ActionCommand;
        Visibility = Visibility.Visible;

        return assessment;
    }
}
