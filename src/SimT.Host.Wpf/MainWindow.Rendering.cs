using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using SimT.Contracts;
using SimT.Contracts.Ledger;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;
using SimT.Contracts.Ui;
using SimT.Host.Wpf.Controls;

namespace SimT.Host.Wpf;

public partial class MainWindow
{
    private void RenderSelected()
    {
        if (NavList.SelectedItem is not NavEntry entry)
        {
            return;
        }

        if (!string.Equals(_screenId, entry.ScreenId, StringComparison.Ordinal))
        {
            _detailKey = null;
            SceneOverlay.HideScene();
        }

        _screenId = entry.ScreenId;
        _firstGrid = null;
        _firstSection = null;

        if (entry.ScreenId == CityHubScreenId)
        {
            RenderCityHub();
            RefreshBadges();
            return;
        }

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

        if (entry.ScreenId == "world.map")
        {
            RenderWorldMapScreen(model);
            RefreshBadges();
            return;
        }

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
                    line.Direction == LedgerDirection.In ? "thu" : "chi",
                    (line.Direction == LedgerDirection.In ? "+" : "−")
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

    private void RenderWorldMapScreen(ScreenModel model)
    {
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

        FilterBox.Visibility = Visibility.Collapsed;
        FilterHint.Visibility = Visibility.Collapsed;

        ContentPanel.Children.Clear();

        // 1. Interactive Three Kingdoms Map Canvas & Departure Console
        if (_kernel.Capabilities.TryGet<IWorldMap>(CapabilityIds.WorldMap, out var map) && map is not null &&
            _kernel.Capabilities.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var player) && player is not null)
        {
            var mapCtrl = new Controls.GameWorldMapControl(map, player.CurrentCityId);
            mapCtrl.TravelRequested += async (targetId, mode) =>
            {
                var modeStr = mode switch
                {
                    TravelMode.Horse => "horse",
                    TravelMode.Caravan => "caravan",
                    _ => "walk"
                };
                await ExecuteScreenCommandAsync($"travel:{targetId}:{modeStr}", "world.map");
            };
            ContentPanel.Children.Add(mapCtrl);
        }

        // 2. Tabular Citadel Sections below the map
        foreach (var section in model.Sections)
        {
            ContentPanel.Children.Add(BuildCard(Filtered(section)));
        }

        ContentPanel.Children.Add(MonthFooter());
        RestoreDetail(model);
        RefreshBadges();
    }

    private GameMetricCard BuildMetric(ScreenMetric metric) => new(metric);

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

    private Border BuildCard(ScreenSection section, int wideColumn = -1)
    {
        return new GameSectionCard
        {
            Heading = section.Heading,
            HeaderRight = new GameBadge($"{section.Rows.Count} dòng", BadgeVariant.Default),
            CardBody = BuildGrid(section, wideColumn)
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
        var visibleColumns = Math.Min(6, columns.Count);
        for (var index = 0; index < visibleColumns; index++)
        {
            var column = index;
            numeric[column] = rows.All(row =>
                column < row.Cells.Length
                && (string.IsNullOrWhiteSpace(row.Cells[column].Text) || row.Cells[column].IsNumeric));
        }

        // Exactly one column may stretch, and it should carry the longest text.
        var flexColumn = wideColumn;
        if (flexColumn < 0)
        {
            var best = -1;
            var bestLength = -1.0;
            for (var index = 0; index < visibleColumns; index++)
            {
                if (numeric[index])
                {
                    continue;
                }

                var average = rows.Average(row => row.Cells[index].Text.Length);
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
            MaxHeight = 440,
            EnableRowVirtualization = true
        };
        VirtualizingPanel.SetIsVirtualizing(grid, true);
        VirtualizingPanel.SetVirtualizationMode(grid, VirtualizationMode.Recycling);

        for (var index = 0; index < visibleColumns; index++)
        {
            var templateCol = new DataGridTemplateColumn
            {
                Header = columns[index],
                SortMemberPath = $"Cells[{index}]",
                Width = index == flexColumn
                    ? new DataGridLength(1, DataGridLengthUnitType.Star)
                    : DataGridLength.Auto
            };

            if (index == flexColumn)
            {
                templateCol.MinWidth = 150;
            }

            if (numeric[index])
            {
                var header = new Style(typeof(DataGridColumnHeader), (Style)FindResource(typeof(DataGridColumnHeader)));
                header.Setters.Add(new Setter(DataGridColumnHeader.HorizontalContentAlignmentProperty, HorizontalAlignment.Right));
                templateCol.HeaderStyle = header;
            }

            var factory = new FrameworkElementFactory(typeof(ContentControl));
            factory.SetBinding(ContentControl.ContentProperty, new Binding($"Cells[{index}]"));
            factory.SetValue(ContentControl.FocusableProperty, false);
            factory.SetValue(ContentControl.IsHitTestVisibleProperty, false);
            templateCol.CellTemplate = new DataTemplate { VisualTree = factory };

            grid.Columns.Add(templateCol);
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

    private void RenderCityHub()
    {
        ScreenTitle.Text = "Trong Thành";
        ScreenStatus.Text = "Trung tâm thành trì · bấm địa điểm hoặc danh sĩ để tương tác";
        KpiStrip.Children.Clear();
        AlertFeed.ItemsSource = null;
        GlobalCommands.Children.Clear();
        FilterBox.Visibility = Visibility.Collapsed;
        FilterHint.Visibility = Visibility.Collapsed;
        ContentPanel.Children.Clear();
        ContentPanel.Children.Add(EmptyState("Cảnh Trong Thành đang hiển thị trên lớp phủ tương tác."));
        TryOpenCityScene();
    }

    private Border EmptyState(string message) => new GameEmptyState(
        "Chưa Có Dữ Liệu",
        string.IsNullOrWhiteSpace(message) ? "Chưa có thông tin hoặc đối tượng nào được ghi nhận tại phân khu này." : message,
        "—");

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
}
