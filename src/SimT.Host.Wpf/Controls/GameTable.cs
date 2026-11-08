using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using SimT.Contracts.Ui;
using RowVm = SimT.Host.Wpf.MainWindow.RowVm;

namespace SimT.Host.Wpf.Controls;

/// <summary>
/// Custom virtualized Tam Quốc table control replacing the standard WPF DataGrid.
/// </summary>
public sealed class GameTable : Grid
{
    private static readonly Brush HeaderBg = Freeze(new(Color.FromRgb(0x18, 0x12, 0x0E)));
    private static readonly Brush HeaderBorder = Freeze(new(Color.FromRgb(0x3A, 0x2C, 0x20)));
    private static readonly Brush HeaderFg = Freeze(new(Color.FromRgb(0x8F, 0x7F, 0x62)));
    private static readonly Brush HeaderActiveFg = Freeze(new(Color.FromRgb(0xC4, 0xA0, 0x62)));
    private static readonly Brush RowEvenBg = Freeze(new(Color.FromRgb(0x1D, 0x16, 0x11)));
    private static readonly Brush RowOddBg = Freeze(new(Color.FromRgb(0x21, 0x19, 0x13)));
    private static readonly Brush RowHoverBg = Freeze(new(Color.FromRgb(0x2A, 0x1F, 0x16)));
    private static readonly Brush RowSelectedBg = Freeze(new(Color.FromRgb(0x30, 0x23, 0x18)));
    private static readonly Brush RowBorder = Freeze(new(Color.FromRgb(0x2E, 0x22, 0x18)));
    private static readonly Brush RowHoverBorder = Freeze(new(Color.FromRgb(0xC4, 0xA0, 0x62)));
    private static readonly Brush CinnabarMark = Freeze(new(Color.FromRgb(0xA8, 0x32, 0x1F)));
    private static readonly Brush JadeBrush = Freeze(new(Color.FromRgb(0x3F, 0x6B, 0x55)));
    private static readonly Brush BronzeBrush = Freeze(new(Color.FromRgb(0xC4, 0xA0, 0x62)));
    private static readonly Style RowContainerStyle = CreateRowContainerStyle();
    private static readonly ControlTemplate FlatListTemplate = CreateFlatListTemplate();
    private static readonly Geometry SortAscGeom = ParseFrozen("M4,14 L10,6 L16,14 Z");
    private static readonly Geometry SortDescGeom = ParseFrozen("M4,6 L10,14 L16,6 Z");
    private static readonly Geometry GainGeom = ParseFrozen("M4,13 L9,6 L14,13 Z");
    private static readonly Geometry LossGeom = ParseFrozen("M4,6 L9,13 L14,6 Z");
    private static readonly Geometry DotsGeom = ParseFrozen("M4,12 A1.5,1.5 0 1,0 4,12.1 M12,12 A1.5,1.5 0 1,0 12,12.1 M20,12 A1.5,1.5 0 1,0 20,12.1");
    private static readonly Dictionary<string, Geometry> CategoryGeomCache = new(StringComparer.Ordinal);

    private readonly ScreenSection _section;
    private readonly List<RowVm> _allRows;
    private readonly int _visibleColumns;
    private readonly int _flexColumn;
    private readonly bool[] _numeric;
    private readonly bool _hasActions;
    private readonly ListBox _listBox;
    private Grid _headerGrid;
    private int _sortColumn = -1;
    private bool _sortAscending = true;

    public event Action<ScreenSection, ScreenRow>? RowInspected;
    public event RoutedEventHandler? ActionClicked;

    public GameTable(ScreenSection section, IReadOnlyList<RowVm> rows, int visibleColumns, int flexColumn, bool[] numeric)
    {
        _section = section;
        _allRows = rows.ToList();
        _visibleColumns = visibleColumns;
        _flexColumn = flexColumn;
        _numeric = numeric;
        _hasActions = section.Rows.Any(r => r.Commands.Any(c => !c.RequiresQuantity));

        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        _headerGrid = BuildHeaderBar();
        SetRow(_headerGrid, 0);
        Children.Add(_headerGrid);

        _listBox = new ListBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            MaxHeight = _allRows.Count <= 16 ? double.PositiveInfinity : 440,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            ItemContainerStyle = RowContainerStyle
        };
        if (_allRows.Count <= 16)
        {
            _listBox.Template = FlatListTemplate;
        }
        else
        {
            ScrollViewer.SetHorizontalScrollBarVisibility(_listBox, ScrollBarVisibility.Disabled);
            ScrollViewer.SetCanContentScroll(_listBox, true);
            VirtualizingPanel.SetIsVirtualizing(_listBox, true);
            VirtualizingPanel.SetVirtualizationMode(_listBox, VirtualizationMode.Recycling);
        }

        _listBox.MouseDoubleClick += (_, _) =>
        {
            if (_listBox.SelectedItem is ListBoxItem { Tag: RowVm selected })
                RowInspected?.Invoke(_section, selected.Row);
        };

        SetRow(_listBox, 1);
        Children.Add(_listBox);
        RebindRows();
    }

    public int SelectedIndex { get => _listBox.SelectedIndex; set => _listBox.SelectedIndex = value; }
    public RowVm? SelectedRowVm => (_listBox.SelectedItem as ListBoxItem)?.Tag as RowVm;

    private Grid BuildHeaderBar()
    {
        var bar = new Grid { Background = HeaderBg, Margin = new Thickness(0, 0, 0, 4) };
        ApplyColumnLayout(bar);

        for (var i = 0; i < _visibleColumns; i++)
        {
            var colIdx = i;
            var isSorted = _sortColumn == colIdx;
            var btn = new Border
            {
                Background = Brushes.Transparent, BorderBrush = HeaderBorder,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(colIdx == 0 ? 36 : 8, 6, 8, 6), Cursor = Cursors.Hand
            };

            var stack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = _numeric[colIdx] ? HorizontalAlignment.Right : HorizontalAlignment.Left
            };
            stack.Children.Add(new TextBlock
            {
                Text = _section.Columns[colIdx].ToUpperInvariant(), FontSize = 10.5, FontWeight = FontWeights.SemiBold,
                Foreground = isSorted ? HeaderActiveFg : HeaderFg, VerticalAlignment = VerticalAlignment.Center
            });

            if (isSorted)
            {
                stack.Children.Add(new Path
                {
                    Data = _sortAscending ? SortAscGeom : SortDescGeom, Fill = HeaderActiveFg,
                    Width = 8, Height = 8, Stretch = Stretch.Uniform,
                    Margin = new Thickness(5, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center
                });
            }

            btn.Child = stack;
            btn.MouseLeftButtonDown += (_, e) => { e.Handled = true; SortByColumn(colIdx); };
            SetColumn(btn, colIdx);
            bar.Children.Add(btn);
        }

        var filler = new Border { BorderBrush = HeaderBorder, BorderThickness = new Thickness(0, 0, 0, 1) };
        SetColumn(filler, _visibleColumns);
        SetColumnSpan(filler, 2);
        bar.Children.Add(filler);
        return bar;
    }

    private void SortByColumn(int colIdx)
    {
        _sortAscending = _sortColumn == colIdx ? !_sortAscending : true;
        _sortColumn = colIdx;
        Children.Remove(_headerGrid);
        _headerGrid = BuildHeaderBar();
        SetRow(_headerGrid, 0);
        Children.Insert(0, _headerGrid);
        RebindRows();
    }

    private void RebindRows()
    {
        IEnumerable<RowVm> ordered = _allRows;
        if (_sortColumn >= 0)
        {
            ordered = _sortAscending
                ? _allRows.OrderBy(r => _sortColumn < r.Cells.Length ? r.Cells[_sortColumn] : null)
                : _allRows.OrderByDescending(r => _sortColumn < r.Cells.Length ? r.Cells[_sortColumn] : null);
        }

        var list = ordered.ToList();
        _listBox.Items.Clear();
        for (var idx = 0; idx < list.Count; idx++)
            _listBox.Items.Add(BuildRowElement(list[idx], idx));
    }

    private ListBoxItem BuildRowElement(RowVm vm, int rowIndex)
    {
        var baseBg = rowIndex % 2 == 0 ? RowEvenBg : RowOddBg;
        var card = new Border
        {
            Background = baseBg, BorderBrush = RowBorder, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5), MinHeight = 38, Padding = new Thickness(6, 4, 8, 4),
            Margin = new Thickness(0, 0, 0, 3), SnapsToDevicePixels = true
        };

        var rowGrid = new Grid();
        ApplyColumnLayout(rowGrid);

        for (var col = 0; col < _visibleColumns; col++)
        {
            var cellVm = col < vm.Cells.Length ? vm.Cells[col] : CellVm.Create(string.Empty);
            var cellElement = BuildCellVisual(vm.Row, cellVm, isFirstColumn: col == 0);
            SetColumn(cellElement, col);
            rowGrid.Children.Add(cellElement);
        }

        Border? selectBar = null;
        StackPanel? actionsPanel = null;
        void EnsureInteractiveChrome(bool sel, bool hov)
        {
            if (sel && selectBar is null)
            {
                selectBar = new Border
                {
                    Width = 3, CornerRadius = new CornerRadius(1.5), Background = CinnabarMark,
                    HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 2, 0, 2)
                };
                SetColumn(selectBar, 0);
                rowGrid.Children.Add(selectBar);
            }
            if ((hov || sel) && actionsPanel is null && _hasActions && vm.Chips.Count > 0)
            {
                actionsPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                foreach (var chip in vm.Chips)
                {
                    var variant = chip.Command.Kind switch
                    {
                        CommandKind.Primary => GameButtonVariant.Primary,
                        CommandKind.Danger => GameButtonVariant.Danger,
                        _ => GameButtonVariant.Secondary
                    };
                    var btn = new GameButton(chip.Label, variant, isCompact: true) { Tag = chip.Id, Margin = new Thickness(0, 0, 5, 0) };
                    btn.Click += (s, e) => { e.Handled = true; ActionClicked?.Invoke(s, e); };
                    actionsPanel.Children.Add(btn);
                }
                SetColumn(actionsPanel, _visibleColumns);
                rowGrid.Children.Add(actionsPanel);
            }
        }

        var detailBtn = new Border
        {
            Width = 28, Height = 26, Background = HeaderBg, BorderBrush = HeaderBorder, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4), Cursor = Cursors.Hand, HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center, ToolTip = "Xem chi tiết dòng này (double-click hoặc Enter)",
            Child = new Path { Data = DotsGeom, Stroke = BronzeBrush, StrokeThickness = 1.5, Width = 11.5, Height = 11.5, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
        };
        detailBtn.MouseLeftButtonDown += (_, e) => { e.Handled = true; RowInspected?.Invoke(_section, vm.Row); };
        SetColumn(detailBtn, _visibleColumns + 1);
        rowGrid.Children.Add(detailBtn);

        card.Child = rowGrid;
        var item = new ListBoxItem { Content = card, DataContext = vm, Tag = vm,
            Opacity = vm.Row.IsMuted ? 0.45 : 1.0 };

        void SyncRowState()
        {
            var sel = item.IsSelected;
            var hov = card.IsMouseOver;
            if (hov || sel) EnsureInteractiveChrome(sel, hov);
            card.Background = sel ? RowSelectedBg : (hov ? RowHoverBg : baseBg);
            card.BorderBrush = hov || sel ? RowHoverBorder : RowBorder;
            if (selectBar is not null) selectBar.Background = sel ? CinnabarMark : Brushes.Transparent;
            if (actionsPanel is not null) actionsPanel.Visibility = (hov || sel) ? Visibility.Visible : Visibility.Hidden;
        }

        card.MouseEnter += (_, _) => SyncRowState();
        card.MouseLeave += (_, _) => SyncRowState();
        item.Selected += (_, _) => SyncRowState();
        item.Unselected += (_, _) => SyncRowState();
        return item;
    }

    private UIElement BuildCellVisual(ScreenRow rawRow, CellVm cell, bool isFirstColumn)
    {
        var content = BuildCellContent(rawRow, cell);
        if (!isFirstColumn)
        {
            content.Margin = new Thickness(6, 0, 6, 0);
            return content;
        }

        var host = new DockPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 6, 0) };
        var leadIcon = BuildLeadingIcon(rawRow, cell.Text);
        leadIcon.Margin = new Thickness(0, 0, 8, 0);
        DockPanel.SetDock(leadIcon, Dock.Left);
        host.Children.Add(leadIcon);
        host.Children.Add(content);
        return host;
    }

    private static FrameworkElement BuildCellContent(ScreenRow rawRow, CellVm cell)
    {
        if (cell.HasProgress)
            return new GameProgressBar(null, cell.ProgressValue, cell.ProgressMax, GameProgressTone.Stamina, compact: true)
            {
                Width = 108, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center
            };

        if (cell.IsBadge)
            return new Border
            {
                Background = cell.BadgeBackground, BorderBrush = cell.BadgeBorder, BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 2, 6, 2),
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { Text = cell.Text, Foreground = cell.BadgeForeground, FontSize = 11, FontWeight = FontWeights.SemiBold }
            };

        var isGain = cell.Text.StartsWith('+') || (cell.IsNumeric && cell.NumericValue > 0 && rawRow.Cells.Contains("thu"));
        var isLoss = cell.Text.StartsWith('−') || cell.Text.StartsWith('-') || (cell.IsNumeric && cell.NumericValue < 0);
        if (isGain || isLoss)
        {
            var numStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            numStack.Children.Add(new Path
            {
                Data = isGain ? GainGeom : LossGeom, Fill = isGain ? JadeBrush : CinnabarMark,
                Width = 7, Height = 7, Stretch = Stretch.Uniform,
                Margin = new Thickness(0, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center
            });
            numStack.Children.Add(new TextBlock
            {
                Text = cell.Text, FontFamily = cell.FontFamily,
                Foreground = isGain ? JadeBrush : CinnabarMark,
                FontSize = 12.5, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center
            });
            return numStack;
        }

        return new TextBlock
        {
            Text = cell.Text, FontFamily = cell.FontFamily, Foreground = cell.TextForeground, FontSize = 12.5,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = cell.IsNumeric ? TextAlignment.Right : TextAlignment.Left,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
    }

    private static FrameworkElement BuildLeadingIcon(ScreenRow row, string firstCellText)
    {
        var isItemRow = row.Commands.Any(c =>
            c.Id.StartsWith("buy:", StringComparison.Ordinal) ||
            c.Id.StartsWith("sell:", StringComparison.Ordinal) ||
            c.Id.StartsWith("npc_buy:", StringComparison.Ordinal) ||
            c.Id.StartsWith("item:modal:", StringComparison.Ordinal) ||
            c.Id.StartsWith("store:", StringComparison.Ordinal) ||
            c.Id.StartsWith("take:", StringComparison.Ordinal) ||
            c.Id.StartsWith("stall:", StringComparison.Ordinal));
        var isNpcRow = !isItemRow && row.Commands.Any(c =>
            c.Id.StartsWith("talk:npc:", StringComparison.Ordinal) ||
            c.Id.StartsWith("npc:modal:", StringComparison.Ordinal) ||
            c.Id.StartsWith("duel:", StringComparison.Ordinal));
        if (isNpcRow || (!isItemRow && !string.IsNullOrWhiteSpace(row.IconHint) && row.IconHint.StartsWith("npc:", StringComparison.Ordinal)))
            return GamePortraitCard.BuildAvatarElement(row, 24);

        var pathData = GameItemSlot.ResolveCategoryIconPath(firstCellText, row.IconHint);
        if (!CategoryGeomCache.TryGetValue(pathData, out var geom))
            CategoryGeomCache[pathData] = geom = ParseFrozen(pathData);

        return new Border
        {
            Width = 22, Height = 22, Background = HeaderBg, BorderBrush = HeaderBorder, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
            Child = new Path { Data = geom, Stroke = BronzeBrush, StrokeThickness = 1.4, Width = 12, Height = 12, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
        };
    }

    private void ApplyColumnLayout(Grid g)
    {
        g.ColumnDefinitions.Clear();
        for (var i = 0; i < _visibleColumns; i++)
        {
            var star = i == _flexColumn ? 1.6 : (_numeric[i] ? 0.85 : 1.0);
            var minW = i == _flexColumn ? 140 : (_numeric[i] ? 82 : 96);
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(star, GridUnitType.Star), MinWidth = minW });
        }
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
    }

    private static Style CreateRowContainerStyle()
    {
        var style = new Style(typeof(ListBoxItem));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0)));
        style.Setters.Add(new Setter(Control.MarginProperty, new Thickness(0)));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
        style.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));
        style.Setters.Add(new Setter(Control.TemplateProperty, new ControlTemplate(typeof(ListBoxItem)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) }));
        return style;
    }

    private static ControlTemplate CreateFlatListTemplate()
    {
        var panel = new FrameworkElementFactory(typeof(StackPanel));
        panel.SetValue(Panel.IsItemsHostProperty, true);
        var template = new ControlTemplate(typeof(ListBox)) { VisualTree = panel };
        template.Seal();
        return template;
    }
    private static Geometry ParseFrozen(string data) { var g = Geometry.Parse(data); g.Freeze(); return g; }
    private static Brush Freeze(SolidColorBrush b) { b.Freeze(); return b; }
}
