using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SimT.Contracts.Ui;

namespace SimT.Host.Wpf.Controls;

/// <summary>
/// Thematic Bazaar Trading Deck for market stalls and warehouses.
/// Replaces flat DataGrids with an interactive 2-column deck:
/// Left: Visual item slots with rarity frames, category filters, and quick price badges.
/// Right: Tactile Deal Tray with quantity slider, live cost/revenue calculator,
/// price trend sparkline chart, and direct Buy/Sell command triggers.
/// </summary>
public sealed class GameBazaarDeckControl : Grid
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");
    private static readonly Brush PanelBg = Freeze(new SolidColorBrush(Color.FromRgb(0x18, 0x12, 0x0E)));
    private static readonly Brush CardBg = Freeze(new SolidColorBrush(Color.FromRgb(0x21, 0x18, 0x12)));
    private static readonly Brush CardBorder = Freeze(new SolidColorBrush(Color.FromRgb(0x3A, 0x2C, 0x20)));
    private static readonly Brush AccentGold = Freeze(new SolidColorBrush(Color.FromRgb(0xC4, 0xA0, 0x62)));
    private static readonly Brush JadeGreen = Freeze(new SolidColorBrush(Color.FromRgb(0x3F, 0x6B, 0x55)));
    private static readonly Brush CinnabarRed = Freeze(new SolidColorBrush(Color.FromRgb(0xA8, 0x32, 0x1F)));
    private static readonly Brush TextMain = Freeze(new SolidColorBrush(Color.FromRgb(0xF3, 0xE6, 0xCC)));
    private static readonly Brush TextMuted = Freeze(new SolidColorBrush(Color.FromRgb(0x8F, 0x7F, 0x62)));
    private static readonly FontFamily MonoFont = new("Consolas");

    private readonly ScreenSection _section;
    private readonly Action<string>? _commandInvoker;
    private readonly Action<ScreenSection, ScreenRow>? _rowInspector;
    private readonly Func<ScreenRow, IReadOnlyList<long>>? _priceHistoryProvider;

    private readonly WrapPanel _shelfPanel;
    private readonly Border _dealTrayBorder;
    private readonly StackPanel _dealTrayContent;
    private readonly List<(ScreenRow Row, GameItemSlot Slot, Border ItemWrapper)> _items = new();

    private ScreenRow? _selectedRow;
    private int _tradeQuantity = 1;

    public GameBazaarDeckControl(
        ScreenSection section,
        Action<string>? commandInvoker,
        Action<ScreenSection, ScreenRow>? rowInspector = null,
        Func<ScreenRow, IReadOnlyList<long>>? priceHistoryProvider = null)
    {
        _section = section;
        _commandInvoker = commandInvoker;
        _rowInspector = rowInspector;
        _priceHistoryProvider = priceHistoryProvider;

        Margin = new Thickness(0, 4, 0, 8);
        SnapsToDevicePixels = true;

        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.35, GridUnitType.Star), MinWidth = 360 });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(350, GridUnitType.Pixel) });

        // Left Column: Shelves Container
        var leftContainer = new Border
        {
            Background = PanelBg,
            BorderBrush = CardBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10),
            Margin = new Thickness(0, 0, 10, 0)
        };

        var leftStack = new StackPanel();

        var shelfHeader = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
        var shelfTitle = new TextBlock
        {
            Text = "SẠP HÀNG THÀNH TRẤN",
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = AccentGold,
            VerticalAlignment = VerticalAlignment.Center
        };
        shelfHeader.Children.Add(shelfTitle);

        var shelfCount = new TextBlock
        {
            Text = $"{section.Rows.Count} mặt hàng",
            FontSize = 10.5,
            Foreground = TextMuted,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(shelfCount, Dock.Right);
        shelfHeader.Children.Add(shelfCount);
        leftStack.Children.Add(shelfHeader);

        var scrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxHeight = 480
        };

        _shelfPanel = new WrapPanel { Orientation = Orientation.Horizontal };
        PopulateShelves();
        scrollViewer.Content = _shelfPanel;
        leftStack.Children.Add(scrollViewer);

        leftContainer.Child = leftStack;
        SetColumn(leftContainer, 0);
        Children.Add(leftContainer);

        // Right Column: Tactical Deal Tray
        _dealTrayBorder = new Border
        {
            Background = CardBg,
            BorderBrush = CardBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12)
        };

        _dealTrayContent = new StackPanel();
        _dealTrayBorder.Child = _dealTrayContent;

        SetColumn(_dealTrayBorder, 1);
        Children.Add(_dealTrayBorder);

        // Select first item by default
        if (_items.Count > 0)
        {
            SelectItem(_items[0].Row);
        }
        else
        {
            RenderEmptyDealTray();
        }
    }

    private void PopulateShelves()
    {
        _shelfPanel.Children.Clear();
        _items.Clear();

        foreach (var row in _section.Rows)
        {
            var name = row.Cells.Count > 0 ? row.Cells[0] : (row.Title ?? "Vật phẩm");
            var buyPrice = ExtractBuyPrice(row);
            var sellPrice = ExtractSellPrice(row);
            var stock = ExtractStock(row);
            var held = ExtractHeld(row);
            var rarity = GameItemSlot.ParseRarity(row.RarityHint ?? string.Join(' ', row.Cells));
            var stats = row.Info.Take(4).Select(d => (d.Label, d.Value)).ToList();

            var slot = new GameItemSlot(name, stock, rarity, null, stats);
            var wrapper = new Border
            {
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(3),
                Margin = new Thickness(0, 0, 8, 10),
                Cursor = Cursors.Hand
            };

            var itemBox = new StackPanel { Width = 78, HorizontalAlignment = HorizontalAlignment.Center };
            itemBox.Children.Add(slot);

            // Mini price pill
            var pricePill = new Border
            {
                Background = PanelBg,
                BorderBrush = CardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(4, 2, 4, 2),
                Margin = new Thickness(0, 2, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var priceText = new TextBlock
            {
                Text = buyPrice > 0 ? $"{buyPrice:N0}q" : (sellPrice > 0 ? $"{sellPrice:N0}q" : "—"),
                FontFamily = MonoFont,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = buyPrice > 0 ? AccentGold : JadeGreen,
                TextAlignment = TextAlignment.Center
            };
            pricePill.Child = priceText;
            itemBox.Children.Add(pricePill);

            if (held > 0)
            {
                var heldText = new TextBlock
                {
                    Text = $"Có: {held}",
                    FontSize = 9.5,
                    Foreground = JadeGreen,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 1, 0, 0)
                };
                itemBox.Children.Add(heldText);
            }

            wrapper.Child = itemBox;

            var capturedRow = row;
            wrapper.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                SelectItem(capturedRow);
            };

            _items.Add((row, slot, wrapper));
            _shelfPanel.Children.Add(wrapper);
        }
    }

    private void SelectItem(ScreenRow row)
    {
        _selectedRow = row;
        _tradeQuantity = 1;

        foreach (var item in _items)
        {
            var isTarget = ReferenceEquals(item.Row, row);
            item.Slot.IsSelected = isTarget;
            item.ItemWrapper.Background = isTarget
                ? Freeze(new SolidColorBrush(Color.FromArgb(0x40, 0xC4, 0xA0, 0x62)))
                : Brushes.Transparent;
        }

        RenderDealTray(row);
        _rowInspector?.Invoke(_section, row);
    }

    private void RenderDealTray(ScreenRow row)
    {
        _dealTrayContent.Children.Clear();

        var name = row.Cells.Count > 0 ? row.Cells[0] : (row.Title ?? "Vật phẩm");
        var buyPrice = ExtractBuyPrice(row);
        var sellPrice = ExtractSellPrice(row);
        var stock = ExtractStock(row);
        var held = ExtractHeld(row);
        var supplyLabel = row.Cells.Count > 4 ? row.Cells[4] : "Bình thường";
        var rarity = GameItemSlot.ParseRarity(row.RarityHint ?? string.Join(' ', row.Cells));

        // Header: Title & Rarity
        var headerPanel = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        var inspectBtn = new Border
        {
            Width = 26,
            Height = 24,
            Background = PanelBg,
            BorderBrush = CardBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Cursor = Cursors.Hand,
            ToolTip = "Xem chi tiết kỳ vật",
            Child = new TextBlock
            {
                Text = "🔍",
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        inspectBtn.MouseLeftButtonUp += (_, _) => _rowInspector?.Invoke(_section, row);
        DockPanel.SetDock(inspectBtn, Dock.Right);
        headerPanel.Children.Add(inspectBtn);

        var titleBlock = new TextBlock
        {
            Text = name,
            FontSize = 13.5,
            FontWeight = FontWeights.Bold,
            Foreground = TextMain,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        headerPanel.Children.Add(titleBlock);
        _dealTrayContent.Children.Add(headerPanel);

        // Subtitle badge: Rarity & Supply
        var subStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
        subStack.Children.Add(new GameBadge(GameItemSlot.RarityLabelVi(rarity), BadgeVariant.Default));
        subStack.Children.Add(new Border { Width = 6 });
        subStack.Children.Add(new GameBadge(supplyLabel, BadgeVariant.Gain));
        _dealTrayContent.Children.Add(subStack);

        // Price & Stock Matrix
        var statGrid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        statGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        statGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var buyBox = CreateStatBox("GIÁ MUA", buyPrice > 0 ? $"{buyPrice:N0} quan" : "Không bán", AccentGold);
        Grid.SetColumn(buyBox, 0);
        statGrid.Children.Add(buyBox);

        var sellBox = CreateStatBox("GIÁ BÁN LẠI", sellPrice > 0 ? $"{sellPrice:N0} quan" : "Không thu", JadeGreen);
        Grid.SetColumn(sellBox, 1);
        statGrid.Children.Add(sellBox);
        _dealTrayContent.Children.Add(statGrid);

        var inventoryBox = new Border
        {
            Background = PanelBg,
            BorderBrush = CardBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(0, 0, 0, 12)
        };
        var invStack = new DockPanel();
        invStack.Children.Add(new TextBlock
        {
            Text = $"Tồn sạp: {stock}",
            FontFamily = MonoFont,
            FontSize = 11,
            Foreground = TextMuted,
            VerticalAlignment = VerticalAlignment.Center
        });
        var heldText = new TextBlock
        {
            Text = $"Trong túi: {held}",
            FontFamily = MonoFont,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = held > 0 ? JadeGreen : TextMuted,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(heldText, Dock.Right);
        invStack.Children.Add(heldText);
        inventoryBox.Child = invStack;
        _dealTrayContent.Children.Add(inventoryBox);

        // Quantity Selector
        var maxTrade = Math.Max(1, Math.Max(stock, held));
        var qtyPicker = new GameQuantityPicker(
            commandId: $"trade:{name}",
            actionLabel: "Số lượng",
            min: 1,
            max: maxTrade,
            initial: _tradeQuantity);

        qtyPicker.ValueChanged += val =>
        {
            _tradeQuantity = val;
            UpdateSummaryCalculation(row, buyPrice, sellPrice);
        };
        _dealTrayContent.Children.Add(qtyPicker);

        // Action Buttons Deck
        var actionDeck = new StackPanel { Margin = new Thickness(0, 4, 0, 10) };

        var buyCmd = row.Commands.FirstOrDefault(c => c.Id.StartsWith("buy:", StringComparison.Ordinal) || c.Id.StartsWith("npc_buy:", StringComparison.Ordinal));
        if (buyCmd is not null && stock > 0)
        {
            var buyBtn = new GameButton($"Mua Hàng ({buyPrice * _tradeQuantity:N0} Quan)", GameButtonVariant.Primary)
            {
                Margin = new Thickness(0, 0, 0, 6)
            };
            buyBtn.Click += (_, _) =>
            {
                var uri = buyCmd.Id.Split(':');
                var verb = uri[0];
                var targetId = uri.Length > 1 ? uri[1] : string.Empty;
                var execCmd = $"{verb}:{targetId}:{_tradeQuantity}";
                _commandInvoker?.Invoke(execCmd);
            };
            actionDeck.Children.Add(buyBtn);
        }

        var sellCmd = row.Commands.FirstOrDefault(c => c.Id.StartsWith("sell:", StringComparison.Ordinal));
        if (sellCmd is not null)
        {
            var canSell = held > 0;
            var sellBtn = new GameButton(
                canSell ? $"Bán Lại (+{sellPrice * Math.Min(_tradeQuantity, held):N0} Quan)" : "Trong Túi Không Có",
                GameButtonVariant.Secondary)
            {
                IsEnabled = canSell,
                Margin = new Thickness(0, 0, 0, 6)
            };
            if (canSell)
            {
                sellBtn.Click += (_, _) =>
                {
                    var uri = sellCmd.Id.Split(':');
                    var verb = uri[0];
                    var targetId = uri.Length > 1 ? uri[1] : string.Empty;
                    var actualQty = Math.Min(_tradeQuantity, held);
                    var execCmd = $"{verb}:{targetId}:{actualQty}";
                    _commandInvoker?.Invoke(execCmd);
                };
            }
            actionDeck.Children.Add(sellBtn);
        }

        // Tactical Market Cornering command (if eligible)
        var cornerCmd = row.Commands.FirstOrDefault(c => c.Id.StartsWith("warfare:corner:", StringComparison.Ordinal));
        if (cornerCmd is not null)
        {
            var cornerBtn = new GameButton(cornerCmd.Label, GameButtonVariant.Danger)
            {
                Margin = new Thickness(0, 0, 0, 6),
                ToolTip = "Thu gom lượng lớn hàng chiến lược trên sạp để thao túng giá thị trường (x2.2)"
            };
            var cid = cornerCmd.Id;
            cornerBtn.Click += (_, _) => _commandInvoker?.Invoke(cid);
            actionDeck.Children.Add(cornerBtn);
        }

        // Secondary commands (e.g. store, take, modal)
        var extraCommands = row.Commands.Where(c =>
            !c.Id.StartsWith("buy:", StringComparison.Ordinal) &&
            !c.Id.StartsWith("sell:", StringComparison.Ordinal) &&
            !c.Id.StartsWith("npc_buy:", StringComparison.Ordinal) &&
            !c.Id.StartsWith("warfare:corner:", StringComparison.Ordinal) &&
            !c.RequiresQuantity).ToList();

        if (extraCommands.Count > 0)
        {
            var extraPanel = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
            foreach (var cmd in extraCommands)
            {
                var btn = new GameButton(cmd.Label, GameButtonVariant.Secondary, isCompact: true)
                {
                    Margin = new Thickness(0, 0, 6, 6)
                };
                btn.Click += (_, _) => _commandInvoker?.Invoke(cmd.Id);
                extraPanel.Children.Add(btn);
            }
            actionDeck.Children.Add(extraPanel);
        }

        _dealTrayContent.Children.Add(actionDeck);

        // Price Chart
        if (_priceHistoryProvider is not null)
        {
            var history = _priceHistoryProvider(row);
            if (history.Count > 1)
            {
                var chartBorder = new Border
                {
                    Background = PanelBg,
                    BorderBrush = CardBorder,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6),
                    Margin = new Thickness(0, 6, 0, 0)
                };
                var chart = new GamePriceChart { Height = 100 };
                chart.SetData(name, history);
                chartBorder.Child = chart;
                _dealTrayContent.Children.Add(chartBorder);
            }
        }
    }

    private void UpdateSummaryCalculation(ScreenRow row, long buyPrice, long sellPrice)
    {
        // Re-render Deal Tray with updated _tradeQuantity
        RenderDealTray(row);
    }

    private void RenderEmptyDealTray()
    {
        _dealTrayContent.Children.Clear();
        _dealTrayContent.Children.Add(new TextBlock
        {
            Text = "Chọn một mặt hàng từ sạp để xem thông tin chi tiết và tiến hành giao thương.",
            FontSize = 11.5,
            Foreground = TextMuted,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 20, 0, 0),
            TextAlignment = TextAlignment.Center
        });
    }

    private static Border CreateStatBox(string label, string value, Brush valueBrush)
    {
        var border = new Border
        {
            Background = PanelBg,
            BorderBrush = CardBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(2)
        };
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 9.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = TextMuted
        });
        stack.Children.Add(new TextBlock
        {
            Text = value,
            FontFamily = MonoFont,
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = valueBrush,
            Margin = new Thickness(0, 2, 0, 0)
        });
        border.Child = stack;
        return border;
    }

    private static long ExtractBuyPrice(ScreenRow row)
    {
        if (row.Cells.Count > 1 && long.TryParse(row.Cells[1].Replace(".", string.Empty).Replace(",", string.Empty).Trim(), out var p))
            return p;
        return 0;
    }

    private static long ExtractSellPrice(ScreenRow row)
    {
        if (row.Cells.Count > 2 && long.TryParse(row.Cells[2].Replace(".", string.Empty).Replace(",", string.Empty).Trim(), out var p))
            return p;
        return 0;
    }

    private static int ExtractStock(ScreenRow row)
    {
        if (row.Cells.Count > 3 && int.TryParse(row.Cells[3].Replace(".", string.Empty).Replace(",", string.Empty).Trim(), out var s))
            return s;
        return 0;
    }

    private static int ExtractHeld(ScreenRow row)
    {
        if (row.Cells.Count > 5 && int.TryParse(row.Cells[5].Replace(".", string.Empty).Replace(",", string.Empty).Trim(), out var h))
            return h;
        return 0;
    }

    private static Brush Freeze(SolidColorBrush b)
    {
        b.Freeze();
        return b;
    }
}
