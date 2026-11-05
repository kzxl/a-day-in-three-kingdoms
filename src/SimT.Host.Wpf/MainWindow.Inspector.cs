using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SimT.Contracts.Ui;
using SimT.Contracts.Playing;
using SimT.Host.Wpf.Controls;

namespace SimT.Host.Wpf;

public partial class MainWindow
{
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
            var cellVm = CellVm.Create(detail.Value);
            if (cellVm.HasProgress)
            {
                var bar = new GameProgressBar(detail.Label, cellVm.ProgressValue, cellVm.ProgressMax, GameProgressTone.Affection, compact: true)
                {
                    Margin = new Thickness(0, 0, 0, 7)
                };
                InspectorBody.Children.Add(bar);
                continue;
            }

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

        // Price Trend Chart Integration
        if (_kernel.Capabilities.TryGet<IMarketTrade>(CapabilityIds.MarketTrade, out var trade) && trade is not null
            && _kernel.Capabilities.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var player) && player is not null)
        {
            long? tradeItemId = null;
            foreach (var cmd in row.Commands)
            {
                if (cmd.Id.StartsWith("buy:") && long.TryParse(cmd.Id["buy:".Length..], out var bId))
                {
                    tradeItemId = bId;
                    break;
                }
                if (cmd.Id.StartsWith("sell:") && long.TryParse(cmd.Id["sell:".Length..], out var sId))
                {
                    tradeItemId = sId;
                    break;
                }
            }

            if (!tradeItemId.HasValue && _kernel.Capabilities.TryGet<IItemCatalog>(CapabilityIds.ItemCatalog, out var catalog) && catalog is not null)
            {
                var candidate = catalog.All.FirstOrDefault(i =>
                    string.Equals(i.NameVi, title, StringComparison.OrdinalIgnoreCase) ||
                    (row.Cells.Count > 0 && string.Equals(i.NameVi, row.Cells[0], StringComparison.OrdinalIgnoreCase)));
                if (candidate is not null)
                {
                    tradeItemId = candidate.Id;
                }
            }

            if (tradeItemId.HasValue)
            {
                var history = trade.PriceHistory(player.CurrentCityId, tradeItemId.Value, 15);
                if (history.Count > 0)
                {
                    var chart = new GamePriceChart();
                    chart.SetData(title, history);
                    chart.Margin = new Thickness(0, 10, 0, 10);
                    InspectorBody.Children.Add(chart);
                }
            }
        }

        // Item Hub Codex & Illustration Integration
        if (_kernel.Capabilities.TryGet<IItemHubService>(CapabilityIds.ItemHub, out var hub) && hub is not null)
        {
            ItemHubEntry? matchedEntry = null;
            var modalCmd = row.Commands.FirstOrDefault(c => c.Id.StartsWith("item:modal:"));
            if (modalCmd is not null && long.TryParse(modalCmd.Id["item:modal:".Length..], out var mId))
            {
                matchedEntry = hub.FindEntry(mId);
            }

            if (matchedEntry is null)
            {
                matchedEntry = hub.Entries.FirstOrDefault(e =>
                    string.Equals(e.Item.NameVi, title, StringComparison.OrdinalIgnoreCase)
                    || title.Contains(e.Item.NameVi, StringComparison.OrdinalIgnoreCase)
                    || (row.Cells.Count > 2 && string.Equals(e.Item.NameVi, row.Cells[2], StringComparison.OrdinalIgnoreCase))
                    || (row.Cells.Count > 1 && string.Equals(e.Item.NameVi, row.Cells[1], StringComparison.OrdinalIgnoreCase))
                    || (row.Cells.Count > 0 && string.Equals(e.Item.NameVi, row.Cells[0], StringComparison.OrdinalIgnoreCase)));
            }

            if (matchedEntry is not null)
            {
                var bannerBorder = new Border
                {
                    Background = (Brush)FindResource("SurfaceAlt"),
                    BorderBrush = (Brush)FindResource("AccentSoft"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(10, 8, 10, 8),
                    Margin = new Thickness(0, 10, 0, 10)
                };

                var bannerStack = new StackPanel();
                bannerStack.Children.Add(new TextBlock
                {
                    Text = $"{matchedEntry.Item.NameVi} · {matchedEntry.SubCategoryVi}",
                    FontWeight = FontWeights.Bold,
                    Foreground = (Brush)FindResource("Accent"),
                    FontSize = 12,
                    Margin = new Thickness(0, 0, 0, 4)
                });

                bannerStack.Children.Add(new TextBlock
                {
                    Text = matchedEntry.IllustrationArt.Trim(),
                    FontFamily = new FontFamily("Consolas, Cascadia Mono, Lucida Console, Courier New"),
                    FontSize = 10.5,
                    LineHeight = 14,
                    Foreground = (Brush)FindResource("Accent"),
                    Margin = new Thickness(0, 0, 0, 6)
                });

                var modalQuickBtn = new GameButton(
                    "Xem Đồ Giám Chi Tiết",
                    GameButtonVariant.Secondary,
                    iconPathData: "M12,3 L20,7.5 L20,16.5 L12,21 L4,16.5 L4,7.5 Z M12,12 L20,7.5 M12,12 L4,7.5 M12,12 L12,21",
                    isCompact: true)
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                var entryId = matchedEntry.Item.Id;
                modalQuickBtn.Click += (_, _) => TryOpenItemDetailModal(entryId);
                bannerStack.Children.Add(modalQuickBtn);

                bannerBorder.Child = bannerStack;
                InspectorBody.Children.Add(bannerBorder);
            }
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
                    var chip = new GameButton(
                        command.Label,
                        VariantFor(command.Kind),
                        command.Cost,
                        isCompact: true)
                    {
                        Tag = command.Id,
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
                var modalBtn = new GameButton(
                    "Xem Hồ Sơ Nhân Sĩ",
                    GameButtonVariant.Secondary,
                    iconPathData: "M12,11 A4,4 0 1,0 12,3 A4,4 0 1,0 12,11 M5,20 C5,16 8,14 12,14 C16,14 19,16 19,20")
                {
                    Margin = new Thickness(0, 8, 0, 6),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                modalBtn.Click += (_, _) => TryOpenNpcModal(inspectorNpcId);
                InspectorBody.Children.Add(modalBtn);

                var duelCmd = row.Commands.FirstOrDefault(c => c.Id.StartsWith("duel:"));
                if (duelCmd is not null)
                {
                    var duelBtn = new GameButton(
                        "Tỷ Võ Lôi Đài",
                        GameButtonVariant.Primary,
                        iconPathData: "M4,20 L20,4 M15,4 L20,4 L20,9 M20,20 L4,4 M9,4 L4,4 L4,9")
                    {
                        Margin = new Thickness(0, 0, 0, 8),
                        HorizontalAlignment = HorizontalAlignment.Stretch
                    };
                    duelBtn.Click += (_, _) => TryOpenDuelModal(inspectorNpcId);
                    InspectorBody.Children.Add(duelBtn);
                }
            }
        }

        InspectorPanel.Visibility = Visibility.Visible;
        InspectorScrim.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Quantity action using GameQuantityPicker (−, +, slider, Tối đa, live total preview).
    /// </summary>
    private FrameworkElement BuildQuantityAction(ScreenCommand command)
    {
        var picker = new GameQuantityPicker(
            commandId: command.Id,
            actionLabel: command.Label,
            buttonVariant: VariantFor(command.Kind),
            min: 1,
            max: 50,
            initial: 1,
            previewFunc: command.Preview,
            costText: command.Cost);

        picker.CommitClicked += RowCommand_Click;
        return picker;
    }

    private void CloseInspector_Click(object sender, RoutedEventArgs e) => CloseInspector();

    private void Scrim_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) => CloseInspector();

    private void CloseInspector()
    {
        _detailKey = null;
        InspectorPanel.Visibility = Visibility.Collapsed;
        InspectorScrim.Visibility = Visibility.Collapsed;
        InspectorBody.Children.Clear();
    }

    private static string DetailKey(ScreenRow row) =>
        row.Title ?? (row.Cells.Count > 0 ? row.Cells[0] : string.Empty);

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
}
