using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SimT.Contracts.Ledger;
using SimT.Contracts.Time;
using SimT.Contracts.Ui;
using SimT.Host.Wpf.Controls;

namespace SimT.Host.Wpf;

public partial class MainWindow
{
    private int _selectedSectionTab;

    private void RenderSelected()
    {
        if (string.Equals(_screenId, UiGalleryScreenId, StringComparison.Ordinal) &&
            NavList.SelectedItem is not NavEntry)
        {
            RenderUiGalleryScreen();
            return;
        }

        if (NavList.SelectedItem is not NavEntry entry)
            return;

        EnsureActiveGroupExpanded(entry);

        if (!string.Equals(_screenId, entry.ScreenId, StringComparison.Ordinal))
        {
            _detailKey = null;
            _selectedSectionTab = 0;
            SceneOverlay.HideScene();
        }

        _screenId = entry.ScreenId;
        _firstGrid = null;
        _firstSection = null;
        SyncStrategicNavigation(_screenId);

        if (entry.ScreenId == CitySceneScreenId) { RenderCityScene(); RefreshBadges(); return; }
        if (entry.ScreenId == CityHubScreenId) { RenderCityHub(); RefreshBadges(); return; }
        if (entry.ScreenId == LedgerScreenId) { RenderLedger(); RefreshBadges(); AnimatePageTransition(); return; }
        if (entry.ScreenId == "society.chronicle") { RenderChronicle(); RefreshBadges(); AnimatePageTransition(); return; }

        var contribution = _kernel.Ui.Screens.FirstOrDefault(screen => screen.ScreenId == entry.ScreenId);
        if (contribution is null)
        {
            ScreenTitle.Text = entry.Title;
            ScreenStatus.Text = "Module cung cấp màn hình này đang tắt.";
            UpdateRecommendationBanner(null);
            KpiStrip.Children.Clear();
            AlertFeed.ItemsSource = null;
            GlobalCommands.Children.Clear();
            ContentPanel.Children.Clear();
            return;
        }

        var model = contribution.Render();
        UpdateRecommendationBanner(model.Recommendation);

        if (entry.ScreenId == "world.map")
        {
            RenderWorldMapScreen(model);
            TryOpenMapScene(model.Recommendation);
            RefreshBadges();
            AnimatePageTransition();
            return;
        }

        if (entry.ScreenId == "player.destiny")
        {
            RenderDestinyTreeScreen(model);
            RefreshBadges();
            AnimatePageTransition();
            return;
        }

        if (entry.ScreenId == "cultivation.farm")
        {
            RenderFarmFieldScreen(model);
            RefreshBadges();
            AnimatePageTransition();
            return;
        }

        if (entry.ScreenId == "social.network")
            TryOpenSocialScene(model.Recommendation);
        else if (entry.ScreenId == "market.stall")
            TryOpenMarketScene(model.Recommendation);
        else if (entry.ScreenId == "npc.directory")
            TryOpenDossierScene(model.Recommendation);

        ScreenTitle.Text = model.Title;
        ScreenStatus.Text = model.StatusLine;

        KpiStrip.Children.Clear();
        foreach (var metric in model.Headline)
            KpiStrip.Children.Add(BuildMetric(metric));

        AlertFeed.ItemsSource = model.Attention.Count == 0
            ? null
            : model.Attention.Select(alert => new AlertVm(alert)).ToList();

        GlobalCommands.Children.Clear();
        foreach (var command in model.Commands.Where(command => !command.RequiresQuantity))
            GlobalCommands.Children.Add(CommandButton(command, command.Id));

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

        if (model.Sections.Count > 1)
        {
            var tabItems = new List<GameTabItem>
            {
                new("all", "Tất cả", model.Sections.Sum(s => s.Rows.Count))
            };
            for (var i = 0; i < model.Sections.Count; i++)
                tabItems.Add(new GameTabItem($"sec:{i}", model.Sections[i].Heading, model.Sections[i].Rows.Count));

            var tabs = new GameTabs(tabItems, Math.Clamp(_selectedSectionTab, 0, tabItems.Count - 1));
            tabs.SelectionChanged += (idx, _) =>
            {
                _selectedSectionTab = idx;
                RenderSelected();
            };
            ContentPanel.Children.Add(tabs);
        }

        for (var i = 0; i < model.Sections.Count; i++)
        {
            if (_selectedSectionTab > 0 && _selectedSectionTab - 1 != i)
                continue;
            ContentPanel.Children.Add(BuildCard(Filtered(model.Sections[i])));
        }

        if (model.Sections.Count == 0)
            ContentPanel.Children.Add(EmptyState(model.StatusLine));

        ContentPanel.Children.Add(MonthFooter());
        RestoreDetail(model);
        RefreshBadges();
        AnimatePageTransition();
    }

    private void AnimatePageTransition()
    {
        var animOpacity = new System.Windows.Media.Animation.DoubleAnimation(0.70, 1.0, TimeSpan.FromMilliseconds(100))
        {
            EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
        };
        var animTranslate = new System.Windows.Media.Animation.DoubleAnimation(6.0, 0.0, TimeSpan.FromMilliseconds(100))
        {
            EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
        };
        PageTransform.BeginAnimation(TranslateTransform.YProperty, animTranslate);
        PageRoot.BeginAnimation(OpacityProperty, animOpacity);
    }

    private void UpdateRecommendationBanner(string? recommendation)
    {
        if (string.IsNullOrWhiteSpace(recommendation))
        {
            RecommendationBanner.Visibility = Visibility.Collapsed;
            RecommendationText.Text = string.Empty;
            return;
        }

        RecommendationText.Text = recommendation;
        RecommendationBanner.Visibility = Visibility.Visible;
    }

    private void RenderLedger()
    {
        var lines = _kernel.Ledger.Recent(300);

        ScreenTitle.Text = "Sổ Cái";
        ScreenStatus.Text = $"{lines.Count} bút toán gần nhất · mọi khoản tiền đi qua một nơi duy nhất";
        UpdateRecommendationBanner("Đối chiếu dòng tiền thu chi mỗi tháng để cân đối chi phí kho bãi, ruộng đất và bổng lộc gia thần.");
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
            wideColumn: 5));

        ContentPanel.Children.Add(MonthFooter());
        if (_detailKey is null)
            CloseInspector();
    }

    private GameMetricCard BuildMetric(ScreenMetric metric) => new(metric);

    private GameButton CommandButton(ScreenCommand command, string id)
    {
        var button = new GameButton(command.Label, VariantFor(command.Kind), command.Cost)
        {
            Tag = id,
            Margin = new Thickness(0, 0, 8, 0),
            ToolTip = command.Cost is null
                ? null
                : new GameRichTooltip(command.Label, $"Chi phí: {command.Cost}")
        };

        button.Click += RowCommand_Click;
        return button;
    }

    private static GameButtonVariant VariantFor(CommandKind kind) => kind switch
    {
        CommandKind.Primary => GameButtonVariant.Primary,
        CommandKind.Danger => GameButtonVariant.Danger,
        _ => GameButtonVariant.Secondary
    };

    private string StyleFor(CommandKind kind) => kind switch
    {
        CommandKind.Primary => "PrimaryButton",
        CommandKind.Danger => "DangerButton",
        _ => "GhostButton"
    };

    private ScreenSection Filtered(ScreenSection section)
    {
        if (string.IsNullOrWhiteSpace(_filter))
            return section;

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
            return;

        _filter = text;
        FilterHint.Visibility = string.IsNullOrEmpty(text) ? Visibility.Visible : Visibility.Collapsed;
        RenderSelected();
    }

    private Border BuildCard(ScreenSection section, int wideColumn = -1)
    {
        var bodyStack = new StackPanel();

        var portraitStrip = TryBuildPortraitStrip(section);
        if (portraitStrip is not null)
            bodyStack.Children.Add(portraitStrip);

        var slotStrip = TryBuildItemSlotStrip(section);
        if (slotStrip is not null)
            bodyStack.Children.Add(slotStrip);

        bodyStack.Children.Add(BuildGrid(section, wideColumn));

        return new GameSectionCard
        {
            Heading = section.Heading,
            HeaderRight = new GameBadge($"{section.Rows.Count} dòng", BadgeVariant.Default),
            CardBody = bodyStack
        };
    }

    private UIElement BuildGrid(ScreenSection section, int wideColumn)
    {
        var rows = section.Rows.Select(row => new RowVm(section, row, this)).ToList();
        var columns = section.Columns;

        if (rows.Count == 0)
            return EmptyState("Chưa có dữ liệu trong mục này.");

        var visibleColumns = Math.Min(6, columns.Count);
        var numeric = new bool[columns.Count];
        for (var index = 0; index < visibleColumns; index++)
        {
            var column = index;
            numeric[column] = rows.All(row =>
                column < row.Cells.Length
                && (string.IsNullOrWhiteSpace(row.Cells[column].Text) || row.Cells[column].IsNumeric));
        }

        var flexColumn = wideColumn;
        if (flexColumn < 0 || flexColumn >= visibleColumns)
        {
            var best = 0;
            var bestLength = -1.0;
            for (var index = 0; index < visibleColumns; index++)
            {
                if (numeric[index]) continue;
                var average = rows.Average(row => row.Cells[index].Text.Length);
                if (average > bestLength)
                {
                    bestLength = average;
                    best = index;
                }
            }
            flexColumn = best;
        }

        var table = new GameTable(section, rows, visibleColumns, flexColumn, numeric);
        table.ActionClicked += RowCommand_Click;
        table.RowInspected += (sec, row) =>
        {
            ShowInspector(sec, row);
            var modalCmd = row.Commands.FirstOrDefault(c => c.Id.StartsWith("npc:modal:"));
            if (modalCmd is not null && long.TryParse(modalCmd.Id["npc:modal:".Length..], out var id))
                TryOpenNpcModal(id);
        };

        if (_firstGrid is null)
        {
            _firstGrid = table;
            _firstSection = section;
        }

        return table;
    }

    private Border EmptyState(string message) => new GameEmptyState(
        "Chưa Có Dữ Liệu",
        string.IsNullOrWhiteSpace(message) ? "Chưa có thông tin hoặc đối tượng nào được ghi nhận tại phân khu này." : message,
        "—");

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
