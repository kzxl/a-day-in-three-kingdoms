using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SimT.Contracts.Playing;

namespace SimT.Host.Wpf.Controls;

/// <summary>
/// Dedicated reusable RPG Protagonist HUD Card.
/// Displays prominent current citadel location, protagonist name and title,
/// dual vitals gauges (HP & Stamina), fatigue and stress status pills,
/// and interactive profile trigger.
/// </summary>
public sealed class GamePlayerHudCard : Border
{
    private readonly TextBlock _cityNameText;
    private readonly Border _regionBadge;
    private readonly TextBlock _regionText;
    private readonly TextBlock _nameText;
    private readonly TextBlock _fameText;
    private readonly TextBlock _hpValText;
    private readonly GameGaugeBar _hpGauge;
    private readonly TextBlock _staminaValText;
    private readonly GameGaugeBar _staminaGauge;
    private readonly TextBlock _fatiguePill;
    private readonly TextBlock _stressPill;

    public event Action? ProfileClicked;

    public GamePlayerHudCard()
    {
        Background = (Brush)Application.Current.FindResource("Surface");
        BorderBrush = (Brush)Application.Current.FindResource("Line");
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(8);
        Padding = new Thickness(11, 10, 11, 10);
        Margin = new Thickness(0, 8, 0, 0);
        Cursor = Cursors.Hand;
        ToolTip = "Bấm để xem toàn bộ hồ sơ 12 thuộc tính, kỹ năng và quan hệ nhân vật chính";

        var stack = new StackPanel();

        // 1. Header: "VỊ TRÍ HIỆN TẠI" & "Hồ Sơ 👤"
        var headerDock = new DockPanel();
        var profileBadge = new Border
        {
            Background = (Brush)Application.Current.FindResource("AccentSoft"),
            BorderBrush = (Brush)Application.Current.FindResource("Line"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2, 6, 2),
            VerticalAlignment = VerticalAlignment.Center
        };
        var profileText = new TextBlock
        {
            Text = "Hồ Sơ 👤",
            FontSize = 10.5,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)Application.Current.FindResource("Accent")
        };
        profileBadge.Child = profileText;
        DockPanel.SetDock(profileBadge, Dock.Right);
        headerDock.Children.Add(profileBadge);

        var titleText = new TextBlock
        {
            Style = (Style)Application.Current.FindResource("Overline"),
            Text = "VỊ TRÍ HIỆN TẠI",
            VerticalAlignment = VerticalAlignment.Center
        };
        headerDock.Children.Add(titleText);
        stack.Children.Add(headerDock);

        // 2. Current Location: Icon + Large Font Citadel Name + Region Badge
        var locRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 3, 0, 0)
        };

        var pinIcon = new TextBlock
        {
            Text = "📍",
            FontSize = 13.5,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0)
        };
        locRow.Children.Add(pinIcon);

        _cityNameText = new TextBlock
        {
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)Application.Current.FindResource("Text"),
            VerticalAlignment = VerticalAlignment.Center
        };
        locRow.Children.Add(_cityNameText);

        _regionBadge = new Border
        {
            Background = (Brush)Application.Current.FindResource("AccentSoft"),
            BorderBrush = (Brush)Application.Current.FindResource("Line"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(5, 1, 5, 1),
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        _regionText = new TextBlock
        {
            FontSize = 10.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.FindResource("Accent"),
            VerticalAlignment = VerticalAlignment.Center
        };
        _regionBadge.Child = _regionText;
        locRow.Children.Add(_regionBadge);
        stack.Children.Add(locRow);

        // 3. Subtle Divider Line separating location from protagonist vitals
        var divider = new Border
        {
            Height = 1,
            Background = (Brush)Application.Current.FindResource("Line"),
            Margin = new Thickness(0, 7, 0, 6),
            Opacity = 0.7
        };
        stack.Children.Add(divider);

        // 4. Protagonist: Name & Fame Standing
        _nameText = new TextBlock
        {
            FontSize = 13.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.FindResource("Text"),
            Margin = new Thickness(0, 0, 0, 1)
        };
        stack.Children.Add(_nameText);

        _fameText = new TextBlock
        {
            Style = (Style)Application.Current.FindResource("Caption"),
            FontSize = 10.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xD0, 0x61)),
            Margin = new Thickness(0, 0, 0, 4)
        };
        stack.Children.Add(_fameText);

        // 5. Vitals 1: HP
        var hpLine = new DockPanel { Margin = new Thickness(0, 2, 0, 0) };
        _hpValText = new TextBlock
        {
            Style = (Style)Application.Current.FindResource("Caption"),
            FontSize = 10.5,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)Application.Current.FindResource("Gain")
        };
        DockPanel.SetDock(_hpValText, Dock.Right);
        hpLine.Children.Add(_hpValText);

        var hpLabel = new TextBlock
        {
            Text = "❤️ Sinh lực",
            Style = (Style)Application.Current.FindResource("Caption"),
            FontSize = 10.5,
            Foreground = (Brush)Application.Current.FindResource("Faint")
        };
        hpLine.Children.Add(hpLabel);
        stack.Children.Add(hpLine);

        _hpGauge = new GameGaugeBar
        {
            Height = 4,
            Margin = new Thickness(0, 2, 0, 4)
        };
        stack.Children.Add(_hpGauge);

        // 6. Vitals 2: Stamina
        var stLine = new DockPanel { Margin = new Thickness(0, 2, 0, 0) };
        _staminaValText = new TextBlock
        {
            Style = (Style)Application.Current.FindResource("Caption"),
            FontSize = 10.5,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x4A, 0x90, 0xE2))
        };
        DockPanel.SetDock(_staminaValText, Dock.Right);
        stLine.Children.Add(_staminaValText);

        var stLabel = new TextBlock
        {
            Text = "⚡ Thể lực",
            Style = (Style)Application.Current.FindResource("Caption"),
            FontSize = 10.5,
            Foreground = (Brush)Application.Current.FindResource("Faint")
        };
        stLine.Children.Add(stLabel);
        stack.Children.Add(stLine);

        _staminaGauge = new GameGaugeBar
        {
            Height = 4,
            Margin = new Thickness(0, 2, 0, 6)
        };
        stack.Children.Add(_staminaGauge);

        // 7. Status Ribbon: Fatigue & Stress
        var ribbonDock = new DockPanel();
        _fatiguePill = new TextBlock
        {
            Style = (Style)Application.Current.FindResource("Caption"),
            FontSize = 10.5,
            Foreground = (Brush)Application.Current.FindResource("Muted")
        };
        ribbonDock.Children.Add(_fatiguePill);

        _stressPill = new TextBlock
        {
            Style = (Style)Application.Current.FindResource("Caption"),
            FontSize = 10.5,
            HorizontalAlignment = HorizontalAlignment.Right,
            Foreground = (Brush)Application.Current.FindResource("Faint")
        };
        DockPanel.SetDock(_stressPill, Dock.Right);
        ribbonDock.Children.Add(_stressPill);
        stack.Children.Add(ribbonDock);

        Child = stack;

        // Click trigger
        MouseLeftButtonDown += (_, e) =>
        {
            ProfileClicked?.Invoke();
            e.Handled = true;
        };

        MouseEnter += (_, _) => BorderBrush = (Brush)Application.Current.FindResource("Accent");
        MouseLeave += (_, _) => BorderBrush = (Brush)Application.Current.FindResource("Line");
    }

    public void Configure(string name, string cityName, string? regionName, CharacterVitals? vitals, string? fameStanding = null)
    {
        _nameText.Text = name;

        var cleanCity = cityName.StartsWith("📍") ? cityName[1..].Trim() : cityName.Trim();
        _cityNameText.Text = cleanCity;

        if (!string.IsNullOrWhiteSpace(regionName))
        {
            _regionText.Text = regionName.Trim();
            _regionBadge.Visibility = Visibility.Visible;
        }
        else
        {
            _regionBadge.Visibility = Visibility.Collapsed;
        }

        if (!string.IsNullOrWhiteSpace(fameStanding))
        {
            _fameText.Text = fameStanding;
            _fameText.Visibility = Visibility.Visible;
        }
        else
        {
            _fameText.Visibility = Visibility.Collapsed;
        }

        if (vitals is null)
        {
            return;
        }

        // HP
        _hpValText.Text = $"{vitals.Health}/{vitals.MaxHp}";
        var hpRatio = vitals.MaxHp > 0 ? (double)vitals.Health / vitals.MaxHp : 1.0;
        var hpBrush = (Brush)(hpRatio > 0.4 ? Application.Current.FindResource("Gain") : Application.Current.FindResource("Loss"));
        _hpValText.Foreground = hpBrush;
        _hpGauge.AnimateProgress(vitals.Health, vitals.MaxHp, hpBrush);

        // Stamina
        _staminaValText.Text = $"{vitals.CurrentStamina}/{vitals.MaxStamina}";
        var stBrush = new SolidColorBrush(Color.FromRgb(0x4A, 0x90, 0xE2));
        _staminaGauge.AnimateProgress(vitals.CurrentStamina, vitals.MaxStamina, stBrush);

        // Fatigue
        var isExhausted = vitals.Fatigue >= 70;
        _fatiguePill.Text = $"💤 Mệt {vitals.Fatigue}%";
        _fatiguePill.Foreground = (Brush)(isExhausted ? Application.Current.FindResource("Loss") : Application.Current.FindResource("Muted"));
        _fatiguePill.FontWeight = isExhausted ? FontWeights.Bold : FontWeights.Normal;

        // Stress
        var isStressed = vitals.Stress >= 50;
        _stressPill.Text = $"🌪️ Áp lực {vitals.Stress}%";
        _stressPill.Foreground = (Brush)(isStressed ? Application.Current.FindResource("Loss") : Application.Current.FindResource("Faint"));
    }

    public void Configure(string name, string locationOrCity, CharacterVitals? vitals, string? fameStanding = null)
        => Configure(name, locationOrCity, null, vitals, fameStanding);
}
