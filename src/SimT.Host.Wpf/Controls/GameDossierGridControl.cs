using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SimT.Contracts.Ui;

namespace SimT.Host.Wpf.Controls;

/// <summary>
/// Social & Lineage Dossier Grid visualizing sworn brotherhood oaths, retainers,
/// marital pedigree, letters, and martial mental methods (Tâm Pháp).
/// </summary>
public sealed class GameDossierGridControl : Grid
{
    private static readonly Brush PanelBg = Freeze(new SolidColorBrush(Color.FromRgb(0x16, 0x13, 0x11)));
    private static readonly Brush PanelBorder = Freeze(new SolidColorBrush(Color.FromRgb(0x3E, 0x2A, 0x20)));
    private static readonly Brush CardBg = Freeze(new SolidColorBrush(Color.FromRgb(0x1E, 0x17, 0x13)));
    private static readonly Brush CardHoverBg = Freeze(new SolidColorBrush(Color.FromRgb(0x28, 0x20, 0x18)));
    private static readonly Brush GoldBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xD4, 0xAF, 0x37)));
    private static readonly Brush GoldDimBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x8A, 0x72, 0x35)));
    private static readonly Brush PeachBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xE2, 0x8B, 0x98)));
    private static readonly Brush TextPrimaryBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xF0, 0xE4, 0xCE)));
    private static readonly Brush TextMutedBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x9E, 0x8F, 0x7C)));
    private static readonly Brush TextSuccessBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x5E, 0xA8, 0x72)));

    private readonly ScreenSection _section;
    private readonly Action<string> _onCommand;
    private readonly Action<ScreenSection, ScreenRow>? _onInspect;

    public GameDossierGridControl(
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
            wrap.Children.Add(BuildDossierCard(row));
        }

        if (_section.Rows.Count == 0)
        {
            wrap.Children.Add(new TextBlock
            {
                Text = "Hiện chưa có danh sách nào trong mục này.",
                Foreground = TextMutedBrush,
                FontStyle = FontStyles.Italic,
                Margin = new Thickness(12)
            });
        }
    }

    private UIElement BuildDossierCard(ScreenRow row)
    {
        var card = new Border
        {
            Background = CardBg,
            BorderBrush = PanelBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12),
            Margin = new Thickness(6),
            Width = 275,
            Cursor = Cursors.Hand
        };

        var stack = new StackPanel();
        card.Child = stack;

        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        var title = row.Title ?? (row.Cells.Count > 0 ? row.Cells[0] : "Nhân Vật");
        var nameBlock = new TextBlock
        {
            Text = title,
            FontWeight = FontWeights.Bold,
            FontSize = 13.5,
            Foreground = TextPrimaryBrush,
            MaxWidth = 175,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        DockPanel.SetDock(nameBlock, Dock.Left);
        header.Children.Add(nameBlock);

        var badgeText = row.Cells.Count > 1 ? row.Cells[1] : string.Empty;
        if (!string.IsNullOrWhiteSpace(badgeText))
        {
            var b = new GameBadge(badgeText, badgeText.Contains("Huynh Đệ") ? BadgeVariant.Gain : BadgeVariant.Default);
            DockPanel.SetDock(b, Dock.Right);
            header.Children.Add(b);
        }
        stack.Children.Add(header);

        // Body content: Cells 2+
        var bodyStack = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
        for (var i = 2; i < row.Cells.Count && i < 5; i++)
        {
            var colHeader = i < _section.Columns.Count ? _section.Columns[i] : $"#{i}";
            bodyStack.Children.Add(new TextBlock
            {
                Text = $"{colHeader}: {row.Cells[i]}",
                FontSize = 11,
                Foreground = colHeader.Contains("Hảo Cảm") || colHeader.Contains("Tâm Pháp") ? GoldBrush : TextMutedBrush,
                Margin = new Thickness(0, 1, 0, 1),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
        }
        stack.Children.Add(bodyStack);

        // Buttons
        if (row.Commands.Count > 0)
        {
            var bp = new WrapPanel { Orientation = Orientation.Horizontal };
            foreach (var cmd in row.Commands)
            {
                var isPrimary = cmd.Kind == CommandKind.Primary || cmd.Id.StartsWith("duel:") || cmd.Id.StartsWith("accept:");
                var btn = new GameButton(cmd.Label, isPrimary ? GameButtonVariant.Primary : GameButtonVariant.Secondary, isCompact: true)
                {
                    Margin = new Thickness(0, 0, 4, 4)
                };
                var cid = cmd.Id;
                btn.Click += (_, _) => _onCommand(cid);
                bp.Children.Add(btn);
            }
            stack.Children.Add(bp);
        }

        card.MouseEnter += (_, _) => card.Background = CardHoverBg;
        card.MouseLeave += (_, _) => card.Background = CardBg;
        card.MouseLeftButtonUp += (_, _) => _onInspect?.Invoke(_section, row);

        return card;
    }

    private static T Freeze<T>(T freezable) where T : Freezable
    {
        if (freezable.CanFreeze) freezable.Freeze();
        return freezable;
    }
}
