using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SimT.Contracts.Ledger;
using SimT.Contracts.Playing;
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
        }

        PlayScreenNavigationAudio(entry.ScreenId);
        _screenId = entry.ScreenId;
        _firstGrid = null;
        _firstSection = null;
        SyncStrategicNavigation(_screenId);

        if (entry.ScreenId == CitySceneScreenId) { RenderCityScene(); RefreshBadges(); return; }
        if (entry.ScreenId == CityHubScreenId) { RenderCityHub(); RefreshBadges(); return; }
        if (entry.ScreenId == LedgerScreenId) { SceneOverlay.HideScene(); RenderLedger(); RefreshBadges(); AnimatePageTransition(); return; }
        if (entry.ScreenId == "society.chronicle") { RenderChronicle(); RefreshBadges(); AnimatePageTransition(); return; }

        var contribution = _kernel.Ui.Screens.FirstOrDefault(screen => screen.ScreenId == entry.ScreenId);
        if (contribution is null)
        {
            SceneOverlay.HideScene();
            ClearWpfPageContent(entry.Title, "Module cung cấp màn hình này đang tắt.");
            return;
        }

        if (TryOpenFeatureScene(entry.ScreenId))
        {
            ClearWpfPageContent(entry.Title);
            RefreshBadges();
            return;
        }

        SceneOverlay.HideScene();
        var model = contribution.Render();
        UpdateRecommendationBanner(model.Recommendation);

        if (entry.ScreenId == "world.map")
        {
            RenderWorldMapScreen(model);
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

        var immediateLimit = (_selectedSectionTab == 0 && !_isSmoke && _detailKey is null) ? 3 : model.Sections.Count;
        var deferredSections = new List<ScreenSection>();
        for (var i = 0; i < model.Sections.Count; i++)
        {
            if (_selectedSectionTab > 0 && _selectedSectionTab - 1 != i)
                continue;
            if (i < immediateLimit)
                ContentPanel.Children.Add(BuildCard(Filtered(model.Sections[i])));
            else
                deferredSections.Add(model.Sections[i]);
        }

        if (model.Sections.Count == 0)
            ContentPanel.Children.Add(EmptyState(model.StatusLine));

        var footer = MonthFooter();
        ContentPanel.Children.Add(footer);
        if (deferredSections.Count > 0)
        {
            var expectedScreen = _screenId;
            var expectedTab = _selectedSectionTab;
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                if (_screenId != expectedScreen || _selectedSectionTab != expectedTab) return;
                var insertIdx = ContentPanel.Children.IndexOf(footer);
                if (insertIdx < 0) insertIdx = ContentPanel.Children.Count;
                foreach (var sec in deferredSections)
                    ContentPanel.Children.Insert(insertIdx++, BuildCard(Filtered(sec)));
            }));
        }
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

    private void ClearWpfPageContent(string title, string status = "")
    {
        ScreenTitle.Text = title;
        ScreenStatus.Text = status;
        UpdateRecommendationBanner(null);
        KpiStrip.Children.Clear();
        AlertFeed.ItemsSource = null;
        GlobalCommands.Children.Clear();
        FilterBox.Visibility = Visibility.Collapsed;
        FilterHint.Visibility = Visibility.Collapsed;
        ContentPanel.Children.Clear();
        CloseInspector();
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
            ToolTip = command.Cost is null ? null : new GameRichTooltip(command.Label, $"Chi phí: {command.Cost}")
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
        var prefKey = $"{_screenId}:{section.Heading}";
        var isTablePreferred = _sectionTableViewPreferences.TryGetValue(prefKey, out var pref) && pref;

        var headerRight = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        headerRight.Children.Add(new GameBadge($"{section.Rows.Count} mục", BadgeVariant.Default));

        if (section.ViewMode != SectionViewMode.DefaultTable)
        {
            var toggleBtn = new GameButton(
                isTablePreferred ? "🎨 Trực Quan" : "📋 Bảng Biểu",
                GameButtonVariant.Secondary,
                isCompact: true)
            {
                Margin = new Thickness(8, 0, 0, 0),
                ToolTip = isTablePreferred ? "Chuyển sang giao diện trực quan chuyên biệt" : "Chuyển sang dạng bảng số liệu chuẩn (DataGrid)"
            };
            toggleBtn.Click += (_, _) =>
            {
                _sectionTableViewPreferences[prefKey] = !isTablePreferred;
                RenderSelected();
            };
            headerRight.Children.Add(toggleBtn);
        }

        if (!isTablePreferred && section.ViewMode == SectionViewMode.BazaarTradingDeck)
        {
            bodyStack.Children.Add(new GameBazaarDeckControl(
                section,
                cmd => _ = ExecuteScreenCommandAsync(cmd),
                (sec, row) => ShowInspector(sec, row),
                GetPriceHistoryForRow));
        }
        else if (!isTablePreferred && section.ViewMode == SectionViewMode.CaravanDispatchBoard)
        {
            var currentCityName = _kernel.Capabilities.TryGet<IWorldMap>(CapabilityIds.WorldMap, out var map) &&
                                  _kernel.Capabilities.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var player) &&
                                  player is not null && map is not null
                                  ? (map.Find(player.CurrentCityId)?.NameVi ?? "Bản Thành") : "Bản Thành";
            bodyStack.Children.Add(new GameCaravanDispatchControl(
                section,
                cmd => _ = ExecuteScreenCommandAsync(cmd),
                (sec, row) => ShowInspector(sec, row),
                currentCityName));
        }
        else if (!isTablePreferred && section.ViewMode == SectionViewMode.ArtisanWorkbench)
        {
            bodyStack.Children.Add(new GameArtisanWorkbenchControl(
                section,
                cmd => _ = ExecuteScreenCommandAsync(cmd),
                (sec, row) => ShowInspector(sec, row)));
        }
        else
        {
            var portraitStrip = TryBuildPortraitStrip(section);
            if (portraitStrip is not null)
                bodyStack.Children.Add(portraitStrip);

            var slotStrip = TryBuildItemSlotStrip(section);
            if (slotStrip is not null)
                bodyStack.Children.Add(slotStrip);

            bodyStack.Children.Add(BuildGrid(section, wideColumn));
        }

        return new GameSectionCard
        {
            Heading = section.Heading,
            HeaderRight = headerRight,
            CardBody = bodyStack
        };
    }

    private IReadOnlyList<long> GetPriceHistoryForRow(ScreenRow row)
    {
        if (_kernel.Capabilities.TryGet<IMarketTrade>(CapabilityIds.MarketTrade, out var trade) && trade is not null &&
            _kernel.Capabilities.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var player) && player is not null)
        {
            long? tradeItemId = null;
            foreach (var cmd in row.Commands)
            {
                var uri = CommandUri.Parse(cmd.Id);
                if ((uri.Action == "buy" || uri.Action == "sell" || uri.Action == "store" || uri.Action == "take") && (uri.TryGetTargetLong(out var tradeId) || uri.TryGetParamLong(out tradeId)))
                {
                    tradeItemId = tradeId;
                    break;
                }
            }

            if (!tradeItemId.HasValue && _kernel.Capabilities.TryGet<IItemCatalog>(CapabilityIds.ItemCatalog, out var catalog) && catalog is not null)
            {
                var title = row.Title ?? (row.Cells.Count > 0 ? row.Cells[0] : string.Empty);
                var candidate = catalog.All.FirstOrDefault(i =>
                    string.Equals(i.NameVi, title, StringComparison.OrdinalIgnoreCase) ||
                    (row.Cells.Count > 0 && row.Cells[0].StartsWith(i.NameVi, StringComparison.OrdinalIgnoreCase)));
                if (candidate is not null) tradeItemId = candidate.Id;
            }

            if (tradeItemId.HasValue)
            {
                return trade.PriceHistory(player.CurrentCityId, tradeItemId.Value, 15);
            }
        }
        return Array.Empty<long>();
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
