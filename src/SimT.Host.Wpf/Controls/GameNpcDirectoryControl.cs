using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SimT.Contracts.Ui;

namespace SimT.Host.Wpf.Controls;

/// <summary>
/// Immersive NPC Directory Deck visualizing Three Kingdoms heroes, generals, scholars,
/// and local luminaries with portrait badges, key attributes (WAR/LDR/INT/COM), and quick interaction actions.
/// </summary>
public sealed class GameNpcDirectoryControl : Grid
{
    private static readonly Brush PanelBg = Freeze(new SolidColorBrush(Color.FromRgb(0x16, 0x13, 0x11)));
    private static readonly Brush PanelBorder = Freeze(new SolidColorBrush(Color.FromRgb(0x3E, 0x2A, 0x20)));
    private static readonly Brush CardBg = Freeze(new SolidColorBrush(Color.FromRgb(0x1E, 0x17, 0x13)));
    private static readonly Brush CardHoverBg = Freeze(new SolidColorBrush(Color.FromRgb(0x28, 0x20, 0x18)));
    private static readonly Brush GoldBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xD4, 0xAF, 0x37)));
    private static readonly Brush GoldDimBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x8A, 0x72, 0x35)));
    private static readonly Brush CrimsonBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xB8, 0x38, 0x2B)));
    private static readonly Brush CyanBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x4A, 0x90, 0xB0)));
    private static readonly Brush GreenBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x5E, 0xA8, 0x72)));
    private static readonly Brush TextPrimaryBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xF0, 0xE4, 0xCE)));
    private static readonly Brush TextMutedBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x9E, 0x8F, 0x7C)));

    private readonly ScreenSection _section;
    private readonly Action<string> _onCommand;
    private readonly Action<ScreenSection, ScreenRow>? _onInspect;

    public GameNpcDirectoryControl(
        ScreenSection section,
        Action<string> onCommand,
        Action<ScreenSection, ScreenRow>? onInspect = null)
    {
        _section = section;
        _onCommand = onCommand;
        _onInspect = onInspect;

        Margin = new Thickness(0, 4, 0, 4);

        var wrap = new WrapPanel { Orientation = Orientation.Horizontal };
        Children.Add(wrap);

        foreach (var row in _section.Rows)
        {
            wrap.Children.Add(BuildNpcCard(row));
        }

        if (_section.Rows.Count == 0)
        {
            wrap.Children.Add(new TextBlock
            {
                Text = "Hiện chưa có danh sĩ nào được ghi chép trong danh mục này.",
                Foreground = TextMutedBrush,
                FontStyle = FontStyles.Italic,
                Margin = new Thickness(12)
            });
        }
    }

    private UIElement BuildNpcCard(ScreenRow row)
    {
        var card = new Border
        {
            Background = CardBg,
            BorderBrush = PanelBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12),
            Margin = new Thickness(6),
            Width = 280,
            Cursor = Cursors.Hand
        };

        var stack = new StackPanel();
        card.Child = stack;

        // Top Row: Avatar placeholder / Name / Role
        var topDock = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        var avatarBorder = new Border
        {
            Width = 40,
            Height = 40,
            CornerRadius = new CornerRadius(4),
            Background = Freeze(new SolidColorBrush(Color.FromArgb(0x40, 0x8A, 0x72, 0x35))),
            BorderBrush = GoldDimBrush,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 10, 0)
        };
        var initial = (row.Title ?? (row.Cells.Count > 0 ? row.Cells[0] : "士")).Substring(0, 1);
        avatarBorder.Child = new TextBlock
        {
            Text = initial,
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            Foreground = GoldBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(avatarBorder, Dock.Left);
        topDock.Children.Add(avatarBorder);

        var nameRoleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var title = row.Title ?? (row.Cells.Count > 0 ? row.Cells[0] : "Danh Sĩ");
        var nameBlock = new TextBlock
        {
            Text = title,
            FontWeight = FontWeights.Bold,
            FontSize = 13.5,
            Foreground = TextPrimaryBrush,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 180
        };
        nameRoleStack.Children.Add(nameBlock);

        var role = row.Details?.FirstOrDefault(d => d.Label == "Thân Phận")?.Value
                   ?? (row.Cells.Count > 1 ? row.Cells[1] : "Hào kiệt");
        var roleBlock = new TextBlock
        {
            Text = role,
            FontSize = 10.5,
            Foreground = TextMutedBrush,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 1, 0, 0)
        };
        nameRoleStack.Children.Add(roleBlock);
        topDock.Children.Add(nameRoleStack);
        stack.Children.Add(topDock);

        // Status & Location Note
        var location = row.Details?.FirstOrDefault(d => d.Label == "Ngụ Cư")?.Value
                       ?? (row.Cells.Count > 2 ? row.Cells[2] : "Phương xa");
        var locBlock = new TextBlock
        {
            Text = $"📍 Ngụ tại: {location}",
            FontSize = 11,
            Foreground = GoldBrush,
            Margin = new Thickness(0, 0, 0, 8),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        stack.Children.Add(locBlock);

        // Attribute Strip (WAR, LDR, INT, COM)
        var war = row.Details?.FirstOrDefault(d => d.Label.Contains("WAR"))?.Value ?? "—";
        var ldr = row.Details?.FirstOrDefault(d => d.Label.Contains("LDR"))?.Value ?? "—";
        var intl = row.Details?.FirstOrDefault(d => d.Label.Contains("INT"))?.Value ?? "—";
        var com = row.Details?.FirstOrDefault(d => d.Label.Contains("COM"))?.Value ?? "—";

        var attrGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        attrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        attrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        attrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        attrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        attrGrid.Children.Add(BuildAttrBox("Võ", war, 0, CrimsonBrush));
        attrGrid.Children.Add(BuildAttrBox("Thống", ldr, 1, GoldBrush));
        attrGrid.Children.Add(BuildAttrBox("Trí", intl, 2, CyanBrush));
        attrGrid.Children.Add(BuildAttrBox("Thương", com, 3, GreenBrush));
        stack.Children.Add(attrGrid);

        // Action Buttons
        if (row.Commands.Count > 0)
        {
            var btnPanel = new WrapPanel { Orientation = Orientation.Horizontal };
            foreach (var cmd in row.Commands)
            {
                var isPrimary = cmd.Kind == CommandKind.Primary || cmd.Id.StartsWith("npc:modal:");
                var btn = new GameButton(cmd.Label, isPrimary ? GameButtonVariant.Primary : GameButtonVariant.Secondary, isCompact: true)
                {
                    Margin = new Thickness(0, 0, 4, 4)
                };
                var cid = cmd.Id;
                btn.Click += (_, _) => _onCommand(cid);
                btnPanel.Children.Add(btn);
            }
            stack.Children.Add(btnPanel);
        }

        card.MouseEnter += (_, _) => card.Background = CardHoverBg;
        card.MouseLeave += (_, _) => card.Background = CardBg;
        card.MouseLeftButtonUp += (_, _) => _onInspect?.Invoke(_section, row);

        return card;
    }

    private static UIElement BuildAttrBox(string label, string val, int col, Brush valBrush)
    {
        var border = new Border
        {
            Background = Freeze(new SolidColorBrush(Color.FromArgb(0x40, 0x11, 0x0E, 0x0A))),
            BorderBrush = PanelBorder,
            BorderThickness = new Thickness(0.8),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(3, 2, 3, 2),
            Margin = new Thickness(1.5)
        };
        Grid.SetColumn(border, col);
        var s = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        s.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 9.5,
            Foreground = TextMutedBrush,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        s.Children.Add(new TextBlock
        {
            Text = val,
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = valBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 1, 0, 0)
        });
        border.Child = s;
        return border;
    }

    private static T Freeze<T>(T freezable) where T : Freezable
    {
        if (freezable.CanFreeze) freezable.Freeze();
        return freezable;
    }
}
