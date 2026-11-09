using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SimT.Contracts.Ui;

namespace SimT.Host.Wpf.Controls;

/// <summary>
/// Immersive Item Codex Showcase control for the Item Hub (Bách Vật Đồ Giám).
/// Visualizes commodities, weapons, farming tools, and medicinal treasures with
/// an interactive multi-column shelf grid and a detailed appraisal pedestal.
/// </summary>
public sealed class GameItemCodexControl : Grid
{
    private static readonly Brush PanelBg = Freeze(new SolidColorBrush(Color.FromRgb(0x16, 0x13, 0x11)));
    private static readonly Brush PanelBorder = Freeze(new SolidColorBrush(Color.FromRgb(0x3E, 0x2A, 0x20)));
    private static readonly Brush PedestalBg = Freeze(new SolidColorBrush(Color.FromRgb(0x1E, 0x17, 0x13)));
    private static readonly Brush CardBg = Freeze(new SolidColorBrush(Color.FromRgb(0x1A, 0x15, 0x11)));
    private static readonly Brush CardHoverBg = Freeze(new SolidColorBrush(Color.FromRgb(0x28, 0x20, 0x18)));
    private static readonly Brush CardSelectedBg = Freeze(new SolidColorBrush(Color.FromRgb(0x33, 0x26, 0x19)));
    private static readonly Brush GoldBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xD4, 0xAF, 0x37)));
    private static readonly Brush GoldDimBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x8A, 0x72, 0x35)));
    private static readonly Brush TextPrimaryBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xF0, 0xE4, 0xCE)));
    private static readonly Brush TextMutedBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x9E, 0x8F, 0x7C)));
    private static readonly Brush TextSuccessBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x5E, 0xA8, 0x72)));
    private static readonly Brush TextWarningBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xDF, 0x8A, 0x48)));

    private readonly ScreenSection _section;
    private readonly Action<string> _onCommand;
    private readonly Action<ScreenSection, ScreenRow>? _onInspect;

    private ScreenRow? _selectedRow;
    private Border? _pedestalContainer;
    private WrapPanel? _shelfWrap;

    public GameItemCodexControl(
        ScreenSection section,
        Action<string> onCommand,
        Action<ScreenSection, ScreenRow>? onInspect = null)
    {
        _section = section;
        _onCommand = onCommand;
        _onInspect = onInspect;

        Margin = new Thickness(0, 4, 0, 4);

        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Shelf Grid
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) }); // Gap
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(360) }); // Pedestal Showcase

        _selectedRow = _section.Rows.FirstOrDefault();

        // Left: Shelf Grid
        var shelfBorder = new Border
        {
            Background = PanelBg,
            BorderBrush = PanelBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12)
        };
        Grid.SetColumn(shelfBorder, 0);
        Children.Add(shelfBorder);

        var shelfStack = new StackPanel();
        shelfBorder.Child = shelfStack;

        // Shelf Header
        var shelfHeader = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
        var shelfTitle = new TextBlock
        {
            Text = $"📦 KỆ TRƯNG BÀY BÁCH VẬT ({_section.Rows.Count} Kỳ Vật)",
            FontWeight = FontWeights.Bold,
            FontSize = 13,
            Foreground = GoldBrush
        };
        DockPanel.SetDock(shelfTitle, Dock.Left);
        shelfHeader.Children.Add(shelfTitle);

        var shelfHint = new TextBlock
        {
            Text = "Bấm vào kỳ vật để giám định thông số",
            FontSize = 11,
            Foreground = TextMutedBrush,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        DockPanel.SetDock(shelfHint, Dock.Right);
        shelfHeader.Children.Add(shelfHint);
        shelfStack.Children.Add(shelfHeader);

        // Scrollable Grid of items
        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxHeight = 580
        };
        _shelfWrap = new WrapPanel { Orientation = Orientation.Horizontal };
        scroll.Content = _shelfWrap;
        shelfStack.Children.Add(scroll);

        foreach (var row in _section.Rows)
        {
            _shelfWrap.Children.Add(BuildShelfCard(row));
        }

        // Right: Pedestal Showcase
        _pedestalContainer = new Border
        {
            Background = PedestalBg,
            BorderBrush = PanelBorder,
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(16)
        };
        Grid.SetColumn(_pedestalContainer, 2);
        Children.Add(_pedestalContainer);

        RefreshPedestal();
    }

    private UIElement BuildShelfCard(ScreenRow row)
    {
        var isSelected = _selectedRow == row;
        var name = row.Cells.Count > 1 ? row.Cells[1] : (row.Title ?? "Vật phẩm");
        var category = row.Cells.Count > 2 ? row.Cells[2] : "Kỳ vật";
        var price = row.Cells.Count > 3 ? row.Cells[3] : "—";
        var bag = row.Cells.Count > 6 ? row.Cells[6] : "—";
        var icon = row.Cells.Count > 0 ? row.Cells[0] : "📦";

        var card = new Border
        {
            Background = isSelected ? CardSelectedBg : CardBg,
            BorderBrush = isSelected ? GoldBrush : PanelBorder,
            BorderThickness = new Thickness(isSelected ? 1.5 : 1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8),
            Margin = new Thickness(4),
            Width = 160,
            Cursor = Cursors.Hand
        };

        var stack = new StackPanel();
        card.Child = stack;

        // Top icon slot & category
        var topRow = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
        var iconText = new TextBlock
        {
            Text = icon,
            FontSize = 20,
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(iconText, Dock.Left);
        topRow.Children.Add(iconText);

        var inBag = bag != "—";
        if (inBag)
        {
            var bagBadge = new Border
            {
                Background = Freeze(new SolidColorBrush(Color.FromArgb(0x50, 0x5E, 0xA8, 0x72))),
                BorderBrush = TextSuccessBrush,
                BorderThickness = new Thickness(0.8),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(4, 1, 4, 1),
                HorizontalAlignment = HorizontalAlignment.Right,
                Child = new TextBlock { Text = bag, FontSize = 9.5, Foreground = TextSuccessBrush, FontWeight = FontWeights.Bold }
            };
            DockPanel.SetDock(bagBadge, Dock.Right);
            topRow.Children.Add(bagBadge);
        }
        stack.Children.Add(topRow);

        // Name
        var nameBlock = new TextBlock
        {
            Text = name,
            FontWeight = FontWeights.Bold,
            FontSize = 12,
            Foreground = isSelected ? GoldBrush : TextPrimaryBrush,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 2, 0, 2)
        };
        stack.Children.Add(nameBlock);

        // Subcategory & Price
        var subText = new TextBlock
        {
            Text = category,
            FontSize = 10,
            Foreground = TextMutedBrush,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        stack.Children.Add(subText);

        var priceText = new TextBlock
        {
            Text = price,
            FontSize = 10.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = GoldDimBrush,
            Margin = new Thickness(0, 3, 0, 0)
        };
        stack.Children.Add(priceText);

        card.MouseEnter += (_, _) =>
        {
            if (_selectedRow != row) card.Background = CardHoverBg;
        };
        card.MouseLeave += (_, _) =>
        {
            if (_selectedRow != row) card.Background = CardBg;
        };
        card.MouseLeftButtonUp += (_, _) =>
        {
            _selectedRow = row;
            RefreshShelfSelection();
            RefreshPedestal();
            _onInspect?.Invoke(_section, row);
        };

        return card;
    }

    private void RefreshShelfSelection()
    {
        if (_shelfWrap is null) return;
        for (var i = 0; i < _shelfWrap.Children.Count && i < _section.Rows.Count; i++)
        {
            if (_shelfWrap.Children[i] is Border b)
            {
                var isSelected = _section.Rows[i] == _selectedRow;
                b.Background = isSelected ? CardSelectedBg : CardBg;
                b.BorderBrush = isSelected ? GoldBrush : PanelBorder;
                b.BorderThickness = new Thickness(isSelected ? 1.5 : 1);
            }
        }
    }

    private void RefreshPedestal()
    {
        if (_pedestalContainer is null) return;

        var stack = new StackPanel();
        _pedestalContainer.Child = stack;

        var row = _selectedRow;
        if (row is null)
        {
            stack.Children.Add(new TextBlock
            {
                Text = "Chưa chọn kỳ vật nào để giám định.",
                Foreground = TextMutedBrush,
                FontStyle = FontStyles.Italic,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 40, 0, 0)
            });
            return;
        }

        var icon = row.Cells.Count > 0 ? row.Cells[0] : "📦";
        var name = row.Cells.Count > 1 ? row.Cells[1] : (row.Title ?? "Kỳ vật");
        var category = row.Cells.Count > 2 ? row.Cells[2] : "Vật phẩm";
        var price = row.Cells.Count > 3 ? row.Cells[3] : "—";
        var shelfLife = row.Cells.Count > 4 ? row.Cells[4] : "—";
        var weight = row.Cells.Count > 5 ? row.Cells[5] : "—";
        var bagQty = row.Cells.Count > 6 ? row.Cells[6] : "—";

        // Showcase Header
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        var headerTitle = new TextBlock
        {
            Text = "🔮 ĐÀI GIÁM ĐỊNH KỲ VẬT",
            FontWeight = FontWeights.Bold,
            FontSize = 13.5,
            Foreground = GoldBrush
        };
        DockPanel.SetDock(headerTitle, Dock.Left);
        header.Children.Add(headerTitle);

        var badge = new GameBadge(category, BadgeVariant.Default);
        DockPanel.SetDock(badge, Dock.Right);
        header.Children.Add(badge);
        stack.Children.Add(header);

        // Center Big Icon Display Pedestal
        var pedestalBox = new Border
        {
            Background = Freeze(new SolidColorBrush(Color.FromArgb(0x50, 0x12, 0x0E, 0x0A))),
            BorderBrush = GoldDimBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14),
            Margin = new Thickness(0, 0, 0, 14)
        };
        var pStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        pStack.Children.Add(new TextBlock
        {
            Text = icon,
            FontSize = 46,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        pStack.Children.Add(new TextBlock
        {
            Text = name,
            FontWeight = FontWeights.Bold,
            FontSize = 15,
            Foreground = TextPrimaryBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 2)
        });
        pStack.Children.Add(new TextBlock
        {
            Text = $"Đơn giá niêm yết: {price}",
            FontSize = 11.5,
            Foreground = GoldBrush,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        pedestalBox.Child = pStack;
        stack.Children.Add(pedestalBox);

        // Metrics Grid (Hạn dùng, Khối lượng, Túi đồ)
        var metaGrid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        metaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        metaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        metaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        metaGrid.Children.Add(BuildSpecPill("Hạn Dùng", shelfLife, 0));
        metaGrid.Children.Add(BuildSpecPill("Trọng Lượng", weight, 1));
        metaGrid.Children.Add(BuildSpecPill("Trong Túi", bagQty, 2, bagQty != "—" ? TextSuccessBrush : TextMutedBrush));
        stack.Children.Add(metaGrid);

        // Detailed appraisal lines from row.Details
        if (row.Details is not null && row.Details.Count > 0)
        {
            var detailBox = new Border
            {
                Background = CardBg,
                BorderBrush = PanelBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 14)
            };
            var dStack = new StackPanel();
            var interestingLabels = new[] { "Nguồn gốc & Thu thập", "Thao tác sử dụng", "Tiềm năng nâng cấp", "Tồn kho thương điếm" };
            foreach (var d in row.Details.Where(d => interestingLabels.Contains(d.Label)))
            {
                dStack.Children.Add(new TextBlock
                {
                    Text = $"• {d.Label}:",
                    FontSize = 10.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = GoldDimBrush,
                    Margin = new Thickness(0, 2, 0, 1)
                });
                dStack.Children.Add(new TextBlock
                {
                    Text = d.Value,
                    FontSize = 11,
                    Foreground = TextMutedBrush,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(10, 0, 0, 5)
                });
            }
            detailBox.Child = dStack;
            stack.Children.Add(detailBox);
        }

        // Action buttons
        if (row.Commands.Count > 0)
        {
            var btnStack = new StackPanel();
            foreach (var cmd in row.Commands)
            {
                var isPrimary = cmd.Kind == CommandKind.Primary;
                var btn = new GameButton(
                    cmd.Label,
                    isPrimary ? GameButtonVariant.Primary : GameButtonVariant.Secondary)
                {
                    FontSize = 12.5,
                    Margin = new Thickness(0, 0, 0, 6),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                var cid = cmd.Id;
                btn.Click += (_, _) => _onCommand(cid);
                btnStack.Children.Add(btn);
            }
            stack.Children.Add(btnStack);
        }
    }

    private static UIElement BuildSpecPill(string label, string val, int col, Brush? valBrush = null)
    {
        var border = new Border
        {
            Background = CardBg,
            BorderBrush = PanelBorder,
            BorderThickness = new Thickness(0.8),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(4, 3, 4, 3),
            Margin = new Thickness(2)
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
            FontSize = 11.5,
            FontWeight = FontWeights.Bold,
            Foreground = valBrush ?? TextPrimaryBrush,
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
