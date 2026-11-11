using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SimT.Contracts.Ui;

namespace SimT.Host.Wpf.Controls;

/// <summary>
/// Estate Pouch & Facility Control visualizing agricultural tools, seed bags,
/// fertilizer supplies, and manor fortress (Ổ Bảo) architectural facilities.
/// </summary>
public sealed class GameEstatePouchControl : Grid
{
    private static readonly Brush PanelBg = Freeze(new SolidColorBrush(Color.FromRgb(0x16, 0x13, 0x11)));
    private static readonly Brush PanelBorder = Freeze(new SolidColorBrush(Color.FromRgb(0x3E, 0x2A, 0x20)));
    private static readonly Brush CardBg = Freeze(new SolidColorBrush(Color.FromRgb(0x1E, 0x17, 0x13)));
    private static readonly Brush CardHoverBg = Freeze(new SolidColorBrush(Color.FromRgb(0x28, 0x20, 0x18)));
    private static readonly Brush GoldBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xD4, 0xAF, 0x37)));
    private static readonly Brush GoldDimBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x8A, 0x72, 0x35)));
    private static readonly Brush GreenBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x5E, 0xA8, 0x72)));
    private static readonly Brush WarningBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xDF, 0x8A, 0x48)));
    private static readonly Brush TextPrimaryBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xF0, 0xE4, 0xCE)));
    private static readonly Brush TextMutedBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x9E, 0x8F, 0x7C)));

    private readonly ScreenSection _section;
    private readonly Action<string> _onCommand;
    private readonly Action<ScreenSection, ScreenRow>? _onInspect;

    public GameEstatePouchControl(
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

        var heading = section.Heading;
        var isFacility = heading.Contains("Công Trình", StringComparison.OrdinalIgnoreCase) ||
                         heading.Contains("Ổ Bảo", StringComparison.OrdinalIgnoreCase);

        foreach (var row in _section.Rows)
        {
            wrap.Children.Add(isFacility ? BuildFacilityCard(row) : BuildSupplyCard(row));
        }

        if (_section.Rows.Count == 0)
        {
            wrap.Children.Add(new TextBlock
            {
                Text = "Hiện chưa có vật phẩm hoặc công trình nào trong mục này.",
                Foreground = TextMutedBrush,
                FontStyle = FontStyles.Italic,
                Margin = new Thickness(12)
            });
        }
    }

    private UIElement BuildSupplyCard(ScreenRow row)
    {
        var card = new Border
        {
            Background = CardBg,
            BorderBrush = PanelBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12),
            Margin = new Thickness(6),
            Width = 260,
            Cursor = Cursors.Hand
        };

        var stack = new StackPanel();
        card.Child = stack;

        var type = row.Cells.Count > 0 ? row.Cells[0] : "Vật tư";
        var name = row.Cells.Count > 1 ? row.Cells[1] : (row.Title ?? "Vật tư");
        var desc = row.Cells.Count > 2 ? row.Cells[2] : "";
        var stock = row.Cells.Count > 3 ? row.Cells[3] : "—";
        var price = row.Cells.Count > 4 ? row.Cells[4] : "—";
        var status = row.Cells.Count > 5 ? row.Cells[5] : "—";

        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
        var nameBlock = new TextBlock
        {
            Text = name,
            FontWeight = FontWeights.Bold,
            FontSize = 13,
            Foreground = TextPrimaryBrush,
            MaxWidth = 170,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        DockPanel.SetDock(nameBlock, Dock.Left);
        header.Children.Add(nameBlock);

        var typeBadge = new GameBadge(type, BadgeVariant.Default);
        DockPanel.SetDock(typeBadge, Dock.Right);
        header.Children.Add(typeBadge);
        stack.Children.Add(header);

        var hasStock = !stock.Contains("Chưa có") && !stock.Contains("Hết hàng") && !stock.StartsWith("0");
        var stockBlock = new TextBlock
        {
            Text = $"Tồn kho: {stock} ({status})",
            FontSize = 11,
            Foreground = hasStock ? GreenBrush : WarningBrush,
            Margin = new Thickness(0, 0, 0, 2)
        };
        stack.Children.Add(stockBlock);

        var descBlock = new TextBlock
        {
            Text = desc,
            FontSize = 10.5,
            Foreground = TextMutedBrush,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 0, 0, 4)
        };
        stack.Children.Add(descBlock);

        var priceBlock = new TextBlock
        {
            Text = $"Đơn giá: {price}",
            FontSize = 10.5,
            Foreground = GoldDimBrush,
            Margin = new Thickness(0, 0, 0, 6)
        };
        stack.Children.Add(priceBlock);

        if (row.Commands.Count > 0)
        {
            var btnPanel = new WrapPanel { Orientation = Orientation.Horizontal };
            foreach (var cmd in row.Commands)
            {
                var isPrimary = cmd.Kind == CommandKind.Primary;
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
        if (row.Commands.Count == 0)
            card.MouseLeftButtonUp += (_, _) => _onInspect?.Invoke(_section, row);

        return card;
    }

    private UIElement BuildFacilityCard(ScreenRow row)
    {
        var card = new Border
        {
            Background = CardBg,
            BorderBrush = PanelBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12),
            Margin = new Thickness(6),
            Width = 270,
            Cursor = Cursors.Hand
        };

        var stack = new StackPanel();
        card.Child = stack;

        var name = row.Cells.Count > 0 ? row.Cells[0] : (row.Title ?? "Công trình");
        var tier = row.Cells.Count > 1 ? row.Cells[1] : "Bậc 0";
        var title = row.Cells.Count > 2 ? row.Cells[2] : "";
        var effect = row.Cells.Count > 3 ? row.Cells[3] : "—";
        var nextCost = row.Cells.Count > 4 ? row.Cells[4] : "—";

        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
        var nameBlock = new TextBlock
        {
            Text = $"🏰 {name}",
            FontWeight = FontWeights.Bold,
            FontSize = 13.5,
            Foreground = TextPrimaryBrush,
            MaxWidth = 175,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        DockPanel.SetDock(nameBlock, Dock.Left);
        header.Children.Add(nameBlock);

        var tierBadge = new GameBadge(tier, tier.Contains("★") ? BadgeVariant.Gain : BadgeVariant.Default);
        DockPanel.SetDock(tierBadge, Dock.Right);
        header.Children.Add(tierBadge);
        stack.Children.Add(header);

        if (!string.IsNullOrWhiteSpace(title))
        {
            stack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 11,
                Foreground = GoldBrush,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 4)
            });
        }

        stack.Children.Add(new TextBlock
        {
            Text = effect,
            FontSize = 10.5,
            Foreground = TextMutedBrush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 4)
        });

        stack.Children.Add(new TextBlock
        {
            Text = $"Nâng cấp: {nextCost}",
            FontSize = 10.5,
            Foreground = GoldDimBrush,
            Margin = new Thickness(0, 0, 0, 6)
        });

        if (row.Commands.Count > 0)
        {
            var btnPanel = new WrapPanel { Orientation = Orientation.Horizontal };
            foreach (var cmd in row.Commands)
            {
                var isPrimary = cmd.Kind == CommandKind.Primary;
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
        if (row.Commands.Count == 0)
            card.MouseLeftButtonUp += (_, _) => _onInspect?.Invoke(_section, row);

        return card;
    }

    private static T Freeze<T>(T freezable) where T : Freezable
    {
        if (freezable.CanFreeze) freezable.Freeze();
        return freezable;
    }
}
