using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using SimT.Contracts;
using SimT.Contracts.Playing;
using SimT.Contracts.Products;
using SimT.Contracts.Time;
using SimT.Contracts.Ui;
using SimT.Core.Kernel;

namespace SimT.Host.Wpf;

/// <summary>
/// The shell: a rail for who you are, when it is and how much you hold; a page with headline
/// numbers, alerts, cards and a month footer; an inspector for the selected row. Every number
/// comes from a module's read model — the host knows no rules.
/// </summary>
public partial class MainWindow : Window
{
    private const string LedgerScreenId = "core.ledger";

    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    private readonly SimKernel _kernel;
    private readonly SaveManager _saves;
    private readonly CampaignMeta _meta;
    private bool _loaded;
    private string _screenId = string.Empty;
    private string? _detailKey;
    private DataGrid? _firstGrid;
    private ScreenSection? _firstSection;
    private string _filter = string.Empty;

    public MainWindow(SimKernel kernel, SaveManager saves, CampaignMeta meta)
    {
        InitializeComponent();
        _kernel = kernel;
        _saves = saves;
        _meta = meta;

        CampaignText.Text = meta.Name;
        VersionText.Text = ShortVersion();
        VersionText.TextTrimming = TextTrimming.CharacterEllipsis;
        DarkTitleBar.Apply(this);

        BuildRail();
        UpdateHeader();
        _loaded = true;
        RenderSelected();
    }

    // ---------------------------------------------------------------- rail

    private void BuildRail()
    {
        var entries = _kernel.Ui.Screens
            .Select(screen => new NavEntry(screen.ScreenId, screen.Title, GlyphFor(screen.ScreenId)))
            .ToList();

        entries.Add(new NavEntry(LedgerScreenId, "Sổ Cái", "≡"));

        NavList.ItemsSource = entries;
        NavList.SelectedIndex = 0;
    }

    private static string GlyphFor(string screenId) => screenId switch
    {
        "market.stall" => "◈",
        "market.player_stall" => "🎪",
        "market.warehouse" => "▤",
        "caravan.routes" => "⇢",
        "professions.trades" => "⚒",
        "cultivation.farm" => "❖",
        "lineage.family" => "⚭",
        "politics.court" => "⌂",
        "npc.directory" => "👤",
        "society.chronicle" => "📜",
        "social.network" => "🤝",
        "military.arena" => "⚔",
        _ => "•"
    };

    private void UpdateHeader()
    {
        var today = _kernel.Today;
        YearText.Text = today.Year.ToString(Vietnamese);
        YearSuffixText.Text = "SCN";
        SeasonText.Text = $"{GameDateCodec.SeasonVi(today.Month)} · Tháng {GameDateCodec.MonthNameVi(today.Month)}";
        DateText.Text = $"Ngày {today.Day}/{GameDate.DaysPerMonth} · {GameDateCodec.ShortVi(today)}";

        TurnText.Text = $"Lượt {_kernel.Turn} · {GameDateCodec.ShortVi(_kernel.Product.StartDate)} khởi nghiệp";

        var liquid = _kernel.Ledger.Liquid;
        var safe = _kernel.Ledger.Safe;
        var goods = GoodsValue();
        CashText.Text = liquid.ToString("N0", Vietnamese);
        CashHintText.Text = safe > 0 ? $"quan · trong két {safe.ToString("N0", Vietnamese)}" : "quan";
        GoodsText.Text = $"hàng hóa {goods.ToString("N0", Vietnamese)} quan";
        NetWorthText.Text = $"tổng {(_kernel.Ledger.Balance + goods).ToString("N0", Vietnamese)} quan";

        if (_kernel.Capabilities.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var playerState) && playerState is not null)
        {
            PlayerNameText.Text = playerState.Name;

            var citadelName = "Lạc Dương";
            if (_kernel.Capabilities.TryGet<IWorldMap>(CapabilityIds.WorldMap, out var map) && map is not null)
            {
                var cit = map.Find(playerState.CurrentCityId);
                if (cit is not null)
                {
                    citadelName = $"{cit.NameVi} ({cit.Region})";
                }
            }
            PlayerLocationText.Text = $"📍 {citadelName}";

            if (_kernel.Capabilities.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var attrs) && attrs is not null)
            {
                var v = attrs.Vitals;
                StaminaText.Text = $"{v.CurrentStamina}/{v.MaxStamina}";
                var ratio = v.MaxStamina > 0 ? Math.Clamp((double)v.CurrentStamina / v.MaxStamina, 0.05, 1.0) : 1.0;
                StaminaBar.Width = Math.Max(4, ratio * 200);
                HealthText.Text = $"HP {v.Health}/{v.MaxHp} · Mệt {v.Fatigue}%";
            }
        }

        RenderCalendar(today);
    }

    /// <summary>Goods at sell prices; zero when the market module is disabled.</summary>
    private long GoodsValue() =>
        _kernel.Capabilities.TryGet<IMarketPortfolio>(CapabilityIds.MarketPortfolio, out var portfolio) && portfolio is not null
            ? portfolio.InventoryValue()
            : 0;

    /// <summary>
    /// The month as thirty cells: past days fade, today is solid, day 30 is the close marker.
    /// A management sim always shows the clock you are spending.
    /// </summary>
    private void RenderCalendar(GameDate today)
    {
        var cells = new List<DayVm>(GameDate.DaysPerMonth);
        for (var day = 1; day <= GameDate.DaysPerMonth; day++)
        {
            var isToday = day == today.Day;
            var isClose = day == GameDate.DaysPerMonth;
            var isPast = day < today.Day;

            cells.Add(new DayVm(
                day.ToString(Vietnamese),
                isToday ? "#D9A441" : isPast ? "#1A212C" : isClose ? "#2E2617" : "#171E28",
                isToday ? "#1A1408" : isPast ? "#4C5666" : isClose ? "#D9A441" : "#8A97A8",
                isToday ? $"Hôm nay · ngày {day}" : isClose ? "Ngày cuối tháng · chốt tháng" : $"Ngày {day}"));
        }

        CalendarStrip.ItemsSource = cells;
        var remaining = GameDate.DaysPerMonth - today.Day + 1;
        MonthLabel.Text = $"Tháng {GameDateCodec.MonthNameVi(today.Month)} · Lượt {_kernel.Turn} · còn {remaining} ngày";
    }

    // ------------------------------------------------------------- screens

    private void Nav_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loaded)
        {
            RenderSelected();
        }
    }

    /// <summary>Selects a screen by id; used by the demo launcher and future shortcuts.</summary>
    public bool SelectScreen(string screenId)
    {
        if (NavList.ItemsSource is not IEnumerable<NavEntry> entries)
        {
            return false;
        }

        var index = entries
            .Select((entry, position) => (entry, position))
            .Where(pair => string.Equals(pair.entry.ScreenId, screenId, StringComparison.Ordinal))
            .Select(pair => pair.position)
            .DefaultIfEmpty(-1)
            .First();

        if (index < 0)
        {
            return false;
        }

        NavList.SelectedIndex = index;
        RenderSelected();
        return true;
    }

    /// <summary>Selects the first row of the first card; used by the demo launcher.</summary>
    public bool SelectFirstRow()
    {
        if (_firstSection is null || _firstSection.Rows.Count == 0)
        {
            return false;
        }

        UpdateLayout();
        if (_firstGrid is not null)
        {
            _firstGrid.SelectedIndex = 0;
        }

        ShowInspector(_firstSection, _firstSection.Rows[0]);
        return true;
    }

    /// <summary>Runs a command through the normal path; used by the demo launcher.</summary>
    public void RunCommandForDemo(string commandId) =>
        RowCommand_Click(new Button { Tag = commandId }, new RoutedEventArgs());

    /// <summary>Runs one day through the real button path; used by the demo launcher.</summary>
    public void AdvanceOneDayForDemo() => Advance(1);

    public int RenderAllScreens()
    {
        var rendered = 0;
        for (var index = 0; index < NavList.Items.Count; index++)
        {
            NavList.SelectedIndex = index;
            rendered++;
        }

        UpdateLayout();
        return rendered;
    }

    private void RenderSelected()
    {
        if (NavList.SelectedItem is not NavEntry entry)
        {
            return;
        }

        if (!string.Equals(_screenId, entry.ScreenId, StringComparison.Ordinal))
        {
            _detailKey = null;
        }

        _screenId = entry.ScreenId;
        _firstGrid = null;
        _firstSection = null;

        if (entry.ScreenId == LedgerScreenId)
        {
            RenderLedger();
            RefreshBadges();
            return;
        }

        var contribution = _kernel.Ui.Screens.FirstOrDefault(screen => screen.ScreenId == entry.ScreenId);
        if (contribution is null)
        {
            ScreenTitle.Text = entry.Title;
            ScreenStatus.Text = "Module cung cấp màn hình này đang tắt.";
            KpiStrip.Children.Clear();
            AlertFeed.ItemsSource = null;
            GlobalCommands.Children.Clear();
            ContentPanel.Children.Clear();
            return;
        }

        var model = contribution.Render();

        ScreenTitle.Text = model.Title;
        ScreenStatus.Text = model.StatusLine;

        KpiStrip.Children.Clear();
        foreach (var metric in model.Headline)
        {
            KpiStrip.Children.Add(BuildMetric(metric));
        }

        AlertFeed.ItemsSource = model.Attention.Count == 0
            ? null
            : model.Attention.Select(alert => new AlertVm(alert)).ToList();

        GlobalCommands.Children.Clear();
        foreach (var command in model.Commands.Where(command => !command.RequiresQuantity))
        {
            GlobalCommands.Children.Add(CommandButton(command, command.Id));
        }

        var longest = model.Sections.Count == 0 ? 0 : model.Sections.Max(section => section.Rows.Count);
        FilterBox.Visibility = longest > 6 ? Visibility.Visible : Visibility.Collapsed;
        if (longest <= 6)
        {
            _filter = string.Empty;
            FilterBox.Text = string.Empty;
        }

        FilterHint.Visibility = FilterBox.Visibility == Visibility.Visible && string.IsNullOrEmpty(FilterBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;

        ContentPanel.Children.Clear();
        foreach (var section in model.Sections)
        {
            ContentPanel.Children.Add(BuildCard(Filtered(section)));
        }

        if (model.Sections.Count == 0)
        {
            ContentPanel.Children.Add(EmptyState(model.StatusLine));
        }

        ContentPanel.Children.Add(MonthFooter());
        RestoreDetail(model);
        RefreshBadges();
    }

    private void RenderLedger()
    {
        var lines = _kernel.Ledger.Recent(300);

        ScreenTitle.Text = "Sổ Cái";
        ScreenStatus.Text = $"{lines.Count} bút toán gần nhất · mọi khoản tiền đi qua một nơi duy nhất";
        KpiStrip.Children.Clear();
        KpiStrip.Children.Add(BuildMetric(new ScreenMetric("Số dư", _kernel.Ledger.Balance.ToString("N0", Vietnamese), "quan")));
        KpiStrip.Children.Add(BuildMetric(new ScreenMetric("Trong két", _kernel.Ledger.Safe.ToString("N0", Vietnamese), "quan")));
        AlertFeed.ItemsSource = null;
        GlobalCommands.Children.Clear();

        var rows = lines
            .Select(line => new ScreenRow(
                [
                    GameDateCodec.ShortVi(line.Date),
                    line.JournalCode,
                    line.Direction == SimT.Contracts.Ledger.LedgerDirection.In ? "thu" : "chi",
                    (line.Direction == SimT.Contracts.Ledger.LedgerDirection.In ? "+" : "−")
                        + line.Amount.ToString("N0", Vietnamese),
                    line.BalanceAfter.ToString("N0", Vietnamese),
                    line.ModuleId,
                    line.Description
                ],
                Array.Empty<ScreenCommand>(),
                Title: line.Description,
                Details:
                [
                    new ScreenDetail("Ngày", GameDateCodec.DescribeVi(line.Date)),
                    new ScreenDetail("Bút toán", line.JournalCode),
                    new ScreenDetail("Số tiền", line.Amount.ToString("N0", Vietnamese) + " quan"),
                    new ScreenDetail("Số dư sau", line.BalanceAfter.ToString("N0", Vietnamese) + " quan"),
                    new ScreenDetail("Nguồn", line.ModuleId)
                ]))
            .ToList();

        ContentPanel.Children.Clear();
        ContentPanel.Children.Add(BuildCard(
            new ScreenSection("Bút toán", ["Ngày", "Mã", "Loại", "Số tiền", "Số dư", "Nguồn", "Diễn giải"], rows),
            wideColumn: 6));

        ContentPanel.Children.Add(MonthFooter());
        if (_detailKey is null)
        {
            CloseInspector();
        }
    }

    // -------------------------------------------------------------- widgets

    private Border BuildMetric(ScreenMetric metric)
    {
        var tone = metric.Tone switch
        {
            MetricTone.Positive => (Brush)FindResource("Gain"),
            MetricTone.Negative => (Brush)FindResource("Loss"),
            MetricTone.Warning => (Brush)FindResource("Accent"),
            _ => (Brush)FindResource("Text")
        };

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = metric.Label.ToUpperInvariant(), Style = (Style)FindResource("Overline") });
        stack.Children.Add(new TextBlock
        {
            Text = metric.Value,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 19,
            FontWeight = FontWeights.SemiBold,
            Foreground = tone,
            Margin = new Thickness(0, 3, 0, 0)
        });

        if (!string.IsNullOrWhiteSpace(metric.Hint))
        {
            stack.Children.Add(new TextBlock
            {
                Text = metric.Hint,
                Style = (Style)FindResource("Caption"),
                Foreground = (Brush)FindResource("Faint"),
                Margin = new Thickness(0, 1, 0, 0)
            });
        }

        return new Border
        {
            Background = (Brush)FindResource("Surface"),
            BorderBrush = (Brush)FindResource("Line"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(13, 10, 16, 10),
            Margin = new Thickness(0, 0, 10, 10),
            Child = stack,
            ToolTip = string.IsNullOrWhiteSpace(metric.Hint)
                ? $"{metric.Label}: {metric.Value}"
                : $"{metric.Label}: {metric.Value} — {metric.Hint}"
        };
    }

    private Button CommandButton(ScreenCommand command, string id)
    {
        var button = new Button
        {
            Content = command.Label,
            Tag = id,
            Style = (Style)FindResource(StyleFor(command.Kind)),
            Margin = new Thickness(0, 0, 8, 0),
            ToolTip = command.Cost is null ? null : $"Tốn {command.Cost}"
        };

        button.Click += RowCommand_Click;
        return button;
    }

    private string StyleFor(CommandKind kind) => kind switch
    {
        CommandKind.Primary => "PrimaryButton",
        CommandKind.Danger => "DangerButton",
        _ => "GhostButton"
    };

    /// <summary>Rows matching the search box; a cell hit is enough, case-insensitive.</summary>
    private ScreenSection Filtered(ScreenSection section)
    {
        if (string.IsNullOrWhiteSpace(_filter))
        {
            return section;
        }

        var needle = _filter.Trim();
        var rows = section.Rows
            .Where(row => row.Cells.Any(cell => cell.Contains(needle, StringComparison.CurrentCultureIgnoreCase)))
            .ToList();

        return section with { Rows = rows };
    }

    private void Filter_TextChanged(object sender, TextChangedEventArgs e)
    {
        var text = FilterBox.Text ?? string.Empty;
        if (string.Equals(text, _filter, StringComparison.Ordinal))
        {
            return;
        }

        _filter = text;
        FilterHint.Visibility = string.IsNullOrEmpty(text) ? Visibility.Visible : Visibility.Collapsed;
        RenderSelected();
    }

    /// <summary>Space advances a day, W a week, digits jump between screens, Esc closes the inspector.</summary>
    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.OriginalSource is TextBox && e.Key != System.Windows.Input.Key.Escape)
        {
            return;
        }

        if (e.OriginalSource is ButtonBase && e.Key is System.Windows.Input.Key.Space or System.Windows.Input.Key.Enter)
        {
            return;
        }

        if (System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control
            && e.Key == System.Windows.Input.Key.F)
        {
            if (FilterBox.Visibility == Visibility.Visible)
            {
                FilterBox.Focus();
                FilterBox.SelectAll();
                e.Handled = true;
            }

            return;
        }

        switch (e.Key)
        {
            case System.Windows.Input.Key.Space:
                Advance(1);
                e.Handled = true;
                break;
            case System.Windows.Input.Key.W:
                Advance(7);
                e.Handled = true;
                break;
            case System.Windows.Input.Key.Enter:
                if (_firstGrid?.SelectedItem is RowVm selectedRow)
                {
                    ShowInspector(selectedRow.Section, selectedRow.Row);
                    e.Handled = true;
                }

                break;
            case System.Windows.Input.Key.Escape:
                CloseInspector();
                e.Handled = true;
                break;
            case System.Windows.Input.Key.D1:
            case System.Windows.Input.Key.D2:
            case System.Windows.Input.Key.D3:
            case System.Windows.Input.Key.D4:
            case System.Windows.Input.Key.D5:
            case System.Windows.Input.Key.D6:
            case System.Windows.Input.Key.D7:
            case System.Windows.Input.Key.D8:
            case System.Windows.Input.Key.D9:
                var index = e.Key - System.Windows.Input.Key.D1;
                if (index < NavList.Items.Count)
                {
                    NavList.SelectedIndex = index;
                    e.Handled = true;
                }

                break;
        }
    }

    private Border BuildCard(ScreenSection section, int wideColumn = -1)
    {
        var stack = new StackPanel();

        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        var count = new TextBlock
        {
            Text = $"{section.Rows.Count} dòng",
            Style = (Style)FindResource("Caption"),
            Foreground = (Brush)FindResource("Faint"),
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(count, Dock.Right);
        header.Children.Add(count);
        header.Children.Add(new TextBlock
        {
            Text = section.Heading.ToUpperInvariant(),
            Style = (Style)FindResource("Overline"),
            VerticalAlignment = VerticalAlignment.Center
        });

        stack.Children.Add(header);
        stack.Children.Add(BuildGrid(section, wideColumn));

        return new Border
        {
            Background = (Brush)FindResource("Surface"),
            BorderBrush = (Brush)FindResource("Line"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 12, 14, 10),
            Margin = new Thickness(0, 0, 0, 14),
            Child = stack
        };
    }

    private DataGrid BuildGrid(ScreenSection section, int wideColumn)
    {
        var rows = section.Rows.Select(row => new RowVm(section, row, this)).ToList();
        var columns = section.Columns;

        if (rows.Count == 0)
        {
            return new DataGrid { ItemsSource = rows, MaxHeight = 120 };
        }

        var numeric = new bool[columns.Count];
        for (var index = 0; index < columns.Count; index++)
        {
            var column = index;
            numeric[column] = rows.All(row =>
                column < row.Cells.Length
                && (string.IsNullOrWhiteSpace(row.Cells[column])
                    || long.TryParse(row.Cells[column], NumberStyles.AllowLeadingSign | NumberStyles.AllowThousands, Vietnamese, out _)));
        }

        // Exactly one column may stretch, and it should carry the longest text.
        var flexColumn = wideColumn;
        if (flexColumn < 0)
        {
            var best = -1;
            var bestLength = -1.0;
            for (var index = 0; index < columns.Count; index++)
            {
                if (numeric[index])
                {
                    continue;
                }

                var average = rows.Average(row => row.Cells[index].Length);
                if (average > bestLength)
                {
                    bestLength = average;
                    best = index;
                }
            }

            flexColumn = best;
        }

        var grid = new DataGrid
        {
            ItemsSource = rows,
            Margin = new Thickness(-4, 0, -4, 0),
            MaxHeight = 440
        };

        for (var index = 0; index < columns.Count; index++)
        {
            var text = new DataGridTextColumn
            {
                Header = columns[index],
                Binding = new Binding($"Cells[{index}]"),
                Width = index == flexColumn
                    ? new DataGridLength(1, DataGridLengthUnitType.Star)
                    : DataGridLength.Auto
            };

            if (index == flexColumn)
            {
                // Star columns shrink below their content when a table has many columns; keep
                // the descriptive one readable instead of clipping "đang lớn (còn 5 ngày)".
                text.MinWidth = 150;
            }

            if (numeric[index])
            {
                var style = new Style(typeof(TextBlock));
                style.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));
                style.Setters.Add(new Setter(TextBlock.FontFamilyProperty, new FontFamily("Consolas")));
                text.ElementStyle = style;

                var header = new Style(typeof(DataGridColumnHeader), (Style)FindResource(typeof(DataGridColumnHeader)));
                header.Setters.Add(new Setter(DataGridColumnHeader.HorizontalContentAlignmentProperty, HorizontalAlignment.Right));
                text.HeaderStyle = header;
            }

            grid.Columns.Add(text);
        }

        if (section.Rows.Any(row => row.Commands.Any(command => !command.RequiresQuantity)))
        {
            grid.Columns.Add(new DataGridTemplateColumn
            {
                Header = string.Empty,
                CellTemplate = (DataTemplate)FindResource("ActionsCell"),
                Width = DataGridLength.Auto
            });
        }

        grid.Columns.Add(new DataGridTemplateColumn
        {
            Header = string.Empty,
            CellTemplate = (DataTemplate)FindResource("DetailCell"),
            Width = DataGridLength.Auto
        });

        if (_firstGrid is null)
        {
            _firstGrid = grid;
            _firstSection = section;
        }

        // A click selects a row for quick work. Opening the drawer is an explicit act:
        // double-click, the ⋯ button, or Enter — otherwise every click would reflow the page.
        grid.MouseDoubleClick += (_, _) =>
        {
            if (grid.SelectedItem is RowVm selected)
            {
                ShowInspector(section, selected.Row);

                var modalCmd = selected.Row.Commands.FirstOrDefault(c => c.Id.StartsWith("npc:modal:"));
                if (modalCmd is not null && long.TryParse(modalCmd.Id["npc:modal:".Length..], out var id))
                {
                    TryOpenNpcModal(id);
                }
            }
        };

        return grid;
    }

    private Border EmptyState(string message) => new()
    {
        Background = (Brush)FindResource("Surface"),
        BorderBrush = (Brush)FindResource("Line"),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(8),
        Padding = new Thickness(18, 22, 18, 22),
        Margin = new Thickness(0, 0, 0, 14),
        Child = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(message) ? "Chưa có gì để hiển thị." : message,
            Style = (Style)FindResource("Caption"),
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        }
    };

    /// <summary>Quiet line under the cards: how long the current month still runs.</summary>
    private TextBlock MonthFooter()
    {
        var today = _kernel.Today;
        var remaining = GameDate.DaysPerMonth - today.Day + 1;

        return new TextBlock
        {
            Text = remaining <= 1
                ? "Hôm nay là ngày cuối tháng; chốt tháng sẽ trừ địa tô, tiền kho và phí gia đình."
                : $"Còn {remaining} ngày nữa hết tháng; chốt tháng tự trừ địa tô, tiền kho và phí gia đình.",
            Style = (Style)FindResource("Caption"),
            Foreground = (Brush)FindResource("Faint"),
            Margin = new Thickness(2, 2, 0, 10)
        };
    }

    // ------------------------------------------------------------ inspector

    private void ShowInspector(ScreenSection section, ScreenRow row)
    {
        var title = row.Title ?? (row.Cells.Count > 0 ? row.Cells[0] : section.Heading);
        _detailKey = DetailKey(row);

        InspectorBody.Children.Clear();
        InspectorBody.Children.Add(new TextBlock
        {
            Text = title,
            Style = (Style)FindResource("H1"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10)
        });

        var details = row.Info.Count > 0
            ? row.Info
            : section.Columns
                .Select((column, index) => new ScreenDetail(column, index < row.Cells.Count ? row.Cells[index] : "—"))
                .ToList();

        foreach (var detail in details)
        {
            var line = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            var value = new TextBlock
            {
                Text = detail.Value,
                TextAlignment = TextAlignment.Right,
                TextWrapping = TextWrapping.Wrap
            };
            DockPanel.SetDock(value, Dock.Right);
            line.Children.Add(value);
            line.Children.Add(new TextBlock
            {
                Text = detail.Label,
                Style = (Style)FindResource("Caption"),
                Foreground = (Brush)FindResource("Faint")
            });
            InspectorBody.Children.Add(line);
        }

        if (row.Commands.Count > 0)
        {
            InspectorBody.Children.Add(new Border
            {
                Height = 1,
                Background = (Brush)FindResource("Line"),
                Margin = new Thickness(0, 10, 0, 12)
            });

            var quick = row.Commands.Where(command => !command.RequiresQuantity).ToList();
            if (quick.Count > 0)
            {
                var bar = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
                foreach (var command in quick)
                {
                    var chip = new Button
                    {
                        Content = command.Label,
                        Tag = command.Id,
                        Style = (Style)FindResource(command.Kind == CommandKind.Danger ? "DangerButton" : "ChipButton"),
                        Margin = new Thickness(0, 0, 6, 6)
                    };
                    chip.Click += RowCommand_Click;
                    bar.Children.Add(chip);
                }

                InspectorBody.Children.Add(bar);
            }

            foreach (var command in row.Commands.Where(command => command.RequiresQuantity))
            {
                InspectorBody.Children.Add(BuildQuantityAction(command));
            }

            var modalCmd = row.Commands.FirstOrDefault(c => c.Id.StartsWith("npc:modal:"));
            if (modalCmd is not null && long.TryParse(modalCmd.Id["npc:modal:".Length..], out var inspectorNpcId))
            {
                var modalBtn = new Button
                {
                    Content = "🔍 Xem Toàn Bộ Hồ Sơ Nhân Sĩ (Modal)",
                    Style = (Style)FindResource("PrimaryButton"),
                    Margin = new Thickness(0, 8, 0, 8),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                modalBtn.Click += (_, _) => TryOpenNpcModal(inspectorNpcId);
                InspectorBody.Children.Add(modalBtn);
            }
        }

        InspectorPanel.Visibility = Visibility.Visible;
        InspectorScrim.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Quantity action: pick a number, read what it will cost, then commit. The preview text is
    /// supplied by the module, so the host stays free of pricing rules.
    /// </summary>
    private FrameworkElement BuildQuantityAction(ScreenCommand command)
    {
        var quantity = new TextBox
        {
            Text = "1",
            Width = 54,
            TextAlignment = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };

        var preview = new TextBlock
        {
            Style = (Style)FindResource("Caption"),
            Foreground = (Brush)FindResource("Faint"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 0)
        };

        var button = new Button
        {
            Content = command.Label,
            Style = (Style)FindResource(StyleFor(command.Kind)),
            Margin = new Thickness(8, 0, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };

        void Refresh()
        {
            var amount = ReadQuantity(quantity);
            preview.Text = command.Preview?.Invoke(amount) ?? string.Empty;
            button.Tag = $"{command.Id}:{amount}";
        }

        quantity.TextChanged += (_, _) => Refresh();
        button.Click += RowCommand_Click;

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(quantity, 0);
        Grid.SetColumn(button, 1);
        row.Children.Add(quantity);
        row.Children.Add(button);

        var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        stack.Children.Add(new TextBlock
        {
            Text = "SỐ LƯỢNG",
            Style = (Style)FindResource("Overline"),
            Margin = new Thickness(0, 0, 0, 4)
        });
        stack.Children.Add(row);
        stack.Children.Add(preview);

        if (command.Cost is not null)
        {
            stack.Children.Add(new TextBlock
            {
                Text = $"Tốn {command.Cost}",
                Style = (Style)FindResource("Caption"),
                Foreground = (Brush)FindResource("Accent"),
                Margin = new Thickness(0, 2, 0, 0)
            });
        }
        Refresh();
        return stack;
    }

    private static int ReadQuantity(TextBox box) =>
        int.TryParse(box.Text, NumberStyles.Integer, Vietnamese, out var value) && value > 0 ? value : 1;

    private void CloseInspector_Click(object sender, RoutedEventArgs e) => CloseInspector();

    private void Scrim_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) => CloseInspector();

    /// <summary>The ⋯ button on a row: open the drawer for that row.</summary>
    private void OpenDetail_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: RowVm row })
        {
            ShowInspector(row.Section, row.Row);
        }
    }

    private void CloseInspector()
    {
        _detailKey = null;
        InspectorPanel.Visibility = Visibility.Collapsed;
        InspectorScrim.Visibility = Visibility.Collapsed;
        InspectorBody.Children.Clear();
    }

    /// <summary>Identity of a row across refreshes: the module's title, or its first cell.</summary>
    private static string DetailKey(ScreenRow row) =>
        row.Title ?? (row.Cells.Count > 0 ? row.Cells[0] : string.Empty);

    /// <summary>Re-opens the drawer on the same row after an action re-rendered the page.</summary>
    private void RestoreDetail(ScreenModel model)
    {
        if (_detailKey is null)
        {
            InspectorPanel.Visibility = Visibility.Collapsed;
            InspectorScrim.Visibility = Visibility.Collapsed;
            return;
        }

        foreach (var section in model.Sections)
        {
            var row = section.Rows.FirstOrDefault(candidate => DetailKey(candidate) == _detailKey);
            if (row is not null)
            {
                ShowInspector(section, row);
                return;
            }
        }

        CloseInspector();
    }

    // ---------------------------------------------------------- rail badges

    /// <summary>Counts attention items per screen so the rail shows where the trouble is.</summary>
    private void RefreshBadges()
    {
        if (NavList.ItemsSource is not IEnumerable<NavEntry> entries)
        {
            return;
        }

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var screen in _kernel.Ui.Screens)
        {
            var alerts = screen.Render().Attention.Count;
            if (alerts > 0)
            {
                counts[screen.ScreenId] = alerts;
            }
        }

        foreach (var entry in entries)
        {
            entry.SetBadge(counts.GetValueOrDefault(entry.ScreenId));
        }
    }

    // -------------------------------------------------------------- actions

    private void Advance_Click(object sender, RoutedEventArgs e) => Advance(1);

    
    private void Rest_Click(object sender, RoutedEventArgs e)
    {
        if (_kernel.Capabilities.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var attrs) && attrs is not null)
        {
            attrs.Rest(1);
        }

        Advance(1);
        StatusText.Text = "Đã nghỉ ngơi tĩnh dưỡng 1 ngày: Thể lực hồi phục trọn vẹn, tinh thần sảng khoái.";
    }

private void AdvanceWeek_Click(object sender, RoutedEventArgs e) => Advance(7);

    private void Advance(int days)
    {
        try
        {
            var from = _kernel.Today;
            _kernel.AdvanceDays(days);
            _kernel.Save();
            UpdateHeader();
            RenderSelected();
            ShowReport(from, _kernel.Today);
            StatusText.Text = $"Đã sang {GameDateCodec.DescribeVi(_kernel.Today)}.";
        }
        catch (Exception error)
        {
            StatusText.Text = Describe(error);
        }
    }

    /// <summary>
    /// What the days just spent actually did: money in, money out, and the charges the month
    /// close took. The old turn report in one card.
    /// </summary>
    private void ShowReport(GameDate from, GameDate to)
    {
        var lines = _kernel.Ledger.Query(from, to, limit: 500);
        var income = lines.Where(line => line.Direction == SimT.Contracts.Ledger.LedgerDirection.In).Sum(line => line.Amount);
        var spend = lines.Where(line => line.Direction == SimT.Contracts.Ledger.LedgerDirection.Out).Sum(line => line.Amount);
        var charges = lines
            .Where(line => line.Direction == SimT.Contracts.Ledger.LedgerDirection.Out)
            .GroupBy(line => line.JournalCode)
            .OrderByDescending(group => group.Sum(line => line.Amount))
            .Take(3)
            .Select(group => $"{group.Key} {group.Sum(line => line.Amount).ToString("N0", Vietnamese)}");

        var days = (int)(to.DayIndex - from.DayIndex);
        var heading = $"BÁO CÁO · {GameDateCodec.ShortVi(from)} → {GameDateCodec.ShortVi(to)} ({days} ngày)";
        var summary = $"thu {income.ToString("N0", Vietnamese)} · chi {spend.ToString("N0", Vietnamese)} · "
                      + $"ròng {(income - spend).ToString("N0", Vietnamese)} quan";

        var card = new StackPanel();
        card.Children.Add(new TextBlock { Text = heading, Style = (Style)FindResource("Overline") });
        card.Children.Add(new TextBlock { Text = summary, Margin = new Thickness(0, 4, 0, 0) });
        if (charges.Any())
        {
            card.Children.Add(new TextBlock
            {
                Text = "khoản lớn nhất: " + string.Join(" · ", charges),
                Style = (Style)FindResource("Caption"),
                Foreground = (Brush)FindResource("Faint"),
                Margin = new Thickness(0, 3, 0, 0)
            });
        }

        ContentPanel.Children.Insert(0, new Border
        {
            Background = (Brush)FindResource("SurfaceAlt"),
            BorderBrush = (Brush)FindResource("Line"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 10, 14, 10),
            Margin = new Thickness(0, 0, 0, 14),
            Child = card
        });
    }

    private void RowCommand_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string commandId })
        {
            return;
        }

        if (commandId.StartsWith("npc:modal:") && long.TryParse(commandId["npc:modal:".Length..], out var npcId))
        {
            TryOpenNpcModal(npcId);
            return;
        }

        var screenId = ScreenForCommand();
        if (!_kernel.TryGetCommandHandler(screenId, out var handler) || handler is null)
        {
            StatusText.Text = $"Màn hình '{screenId}' không nhận lệnh.";
            return;
        }

        try
        {
            StatusText.Text = handler.Execute(commandId) ?? "Xong.";
        }
        catch (Exception error)
        {
            StatusText.Text = Describe(error);
        }

        _kernel.Save();
        UpdateHeader();
        RenderSelected();
    }

    private string ScreenForCommand() =>
        NavList.SelectedItem is NavEntry entry ? entry.ScreenId : _screenId;

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        _kernel.Save();
        _saves.Touch(_meta, GameDateCodec.Format(_kernel.Today));
        _kernel.Dispose();
    }

    /// <summary>
    /// Turns any failure into a line the player can read: a coded simulation error keeps its
    /// code, anything else is reported as an internal fault instead of a crash.
    /// </summary>
    private void PlayerCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        OpenPlayerProfileModal();
    }

    private void OpenPlayerProfileModal()
    {
        if (!_kernel.Capabilities.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var playerState) || playerState is null ||
            !_kernel.Capabilities.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var attributes) || attributes is null)
        {
            StatusText.Text = "Hồ sơ nhân vật chưa sẵn sàng.";
            return;
        }

        var citadelName = "Lạc Dương";
        if (_kernel.Capabilities.TryGet<IWorldMap>(CapabilityIds.WorldMap, out var map) && map is not null)
        {
            var cit = map.Find(playerState.CurrentCityId);
            if (cit is not null)
            {
                citadelName = $"{cit.NameVi} ({cit.Region})";
            }
        }

        var v = attributes.Vitals;
        var s = attributes.Social;
        var protagonistDef = new NpcDef(
            0,
            "protagonist",
            playerState.Name,
            "主角",
            "Vô Danh",
            "Thương Nhân",
            playerState.CurrentCityId,
            attributes.Attributes,
            "Chí tại bốn phương, cơ biến mẫn tiệp, khéo xoay sở thương trường.",
            $"Khởi nghiệp thời loạn thế Tam Quốc. Hiện ngụ cư tại {citadelName}. " +
            $"Thể lực hiện tại: {v.CurrentStamina}/{v.MaxStamina} (Mệt mỏi: {v.Fatigue}%, Áp lực: {v.Stress}%). " +
            $"Danh vọng thiên hạ: {s.Fame} điểm, Đức độ: {s.Virtue} điểm, Tín nghĩa: {s.Credibility} điểm.");

        NpcDetailWindow.ShowNpc(this, protagonistDef, citadelName);
        StatusText.Text = $"Đã mở hồ sơ nhân vật chính: {playerState.Name}.";
    }

    private void TryOpenNpcModal(long npcId)
    {
        if (!_kernel.Capabilities.TryGet<INpcRegistry>(CapabilityIds.NpcRegistry, out var registry) || registry is null)
        {
            StatusText.Text = "Hệ thống nhân sĩ chưa sẵn sàng.";
            return;
        }

        var npc = registry.Find(npcId);
        if (npc is null)
        {
            StatusText.Text = $"Không tìm thấy nhân sĩ mã {npcId}.";
            return;
        }

        NpcActivityDef? activity = null;
        NpcStatDelta? deltas = null;
        var currentCitId = npc.CitadelId;

        if (_kernel.Capabilities.TryGet<ISocietyChronicle>(CapabilityIds.SocietyChronicle, out var society) && society is not null)
        {
            activity = society.GetNpcActivity(npcId);
            deltas = society.GetStatDeltas(npcId);
            if (activity is not null)
            {
                currentCitId = activity.CurrentCitadelId;
            }
        }

        RelationshipDef? relationship = null;
        RetainerDef? retainer = null;
        if (_kernel.Capabilities.TryGet<ISocialNetwork>(CapabilityIds.SocialNetwork, out var social) && social is not null)
        {
            relationship = social.GetRelationship(npcId);
            retainer = social.GetRetainer(npcId);
        }

        var citadelName = "Ẩn sĩ";
        if (_kernel.Capabilities.TryGet<IWorldMap>(CapabilityIds.WorldMap, out var map) && map is not null)
        {
            var cit = map.Find(currentCitId);
            if (cit is not null)
            {
                citadelName = cit.NameVi;
            }
        }

        NpcDetailWindow.ShowNpc(this, npc, citadelName, deltas, activity, relationship, retainer);
        StatusText.Text = $"Đang xem hồ sơ: {npc.NameVi} ({npc.StyleNameVi ?? npc.Role}).";
    }

    private static string Describe(Exception error) => error switch
    {
        SimException sim => $"[{sim.Code}] {sim.Message}",
        _ => $"[internal] {error.GetType().Name}: {error.Message}"
    };

    private static string ShortVersion()
    {
        var full = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            ?? "unknown";

        var plus = full.IndexOf('+');
        if (plus < 0)
        {
            return $"SimT {full}";
        }

        var hash = full[(plus + 1)..];
        return $"SimT {full[..plus]} · {hash[..Math.Min(7, hash.Length)]}";
    }

    // ------------------------------------------------------------- models

    private sealed class NavEntry : INotifyPropertyChanged
    {
        private string _badge = string.Empty;
        private Visibility _badgeVisibility = Visibility.Collapsed;

        public NavEntry(string screenId, string title, string glyph)
        {
            ScreenId = screenId;
            Title = title;
            Glyph = glyph;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string ScreenId { get; }

        public string Title { get; }

        public string Glyph { get; }

        public string Badge
        {
            get => _badge;
            private set => Set(ref _badge, value);
        }

        public Visibility BadgeVisibility
        {
            get => _badgeVisibility;
            private set => Set(ref _badgeVisibility, value);
        }

        public void SetBadge(int count)
        {
            Badge = count > 0 ? count.ToString(Vietnamese) : string.Empty;
            BadgeVisibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Set<T>(ref T field, T value, [CallerMemberName] string? property = null)
        {
            if (Equals(field, value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }
    }

    /// <summary>Row shape the DataGrid binds to: cells, chip buttons and the model behind it.</summary>
    private sealed class RowVm
    {
        public RowVm(ScreenSection section, ScreenRow row, MainWindow owner)
        {
            Section = section;
            Row = row;
            Cells = row.Cells.ToArray();
            Chips = row.Commands
                .Where(command => !command.RequiresQuantity)
                .Select(command => new ChipVm(
                    command,
                    (Style)owner.FindResource(command.Kind == CommandKind.Danger ? "DangerButton" : "ChipButton")))
                .ToList();
        }

        public ScreenSection Section { get; }

        public ScreenRow Row { get; }

        public string[] Cells { get; }

        public IReadOnlyList<ChipVm> Chips { get; }
    }

    private sealed record ChipVm(ScreenCommand Command, Style Style)
    {
        public string Id => Command.Id;

        public string Label => Command.Label;
    }

    private sealed record DayVm(string Label, string Background, string Foreground, string Tip);

    private sealed class AlertVm
    {
        public AlertVm(ScreenAlert alert)
        {
            Message = alert.Message;
            ScreenId = alert.ScreenId;
            Accent = alert.Level switch
            {
                AlertLevel.Critical => "#E06C75",
                AlertLevel.Warning => "#D9A441",
                _ => "#5E6B7C"
            };
        }

        public string Message { get; }

        public string? ScreenId { get; }

        public string Accent { get; }
    }
}
