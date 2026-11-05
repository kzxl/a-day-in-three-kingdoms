using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SimT.Contracts.Ui;
using SimT.Contracts.Playing;

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

        // Price Trend Chart Integration: If item has trade price history, render GamePriceChart
        if (_kernel.Capabilities.TryGet<SimT.Contracts.Playing.IMarketTrade>(CapabilityIds.MarketTrade, out var trade) && trade is not null
            && _kernel.Capabilities.TryGet<SimT.Contracts.Playing.IPlayerState>(CapabilityIds.PlayerState, out var player) && player is not null)
        {
            long? tradeItemId = null;
            foreach (var cmd in row.Commands)
            {
                var uri = CommandUri.Parse(cmd.Id);
                if ((uri.Action == "buy" || uri.Action == "sell") && (uri.TryGetTargetLong(out var tradeId) || uri.TryGetParamLong(out tradeId)))
                {
                    tradeItemId = tradeId;
                    break;
                }
            }

            if (!tradeItemId.HasValue && _kernel.Capabilities.TryGet<SimT.Contracts.Playing.IItemCatalog>(CapabilityIds.ItemCatalog, out var catalog) && catalog is not null)
            {
                var candidate = catalog.All.FirstOrDefault(i => string.Equals(i.NameVi, title, StringComparison.OrdinalIgnoreCase) || (row.Cells.Count > 0 && string.Equals(i.NameVi, row.Cells[0], StringComparison.OrdinalIgnoreCase)));
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
                    var chart = new Controls.GamePriceChart();
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
            if (modalCmd is not null)
            {
                var uri = CommandUri.Parse(modalCmd.Id);
                if (uri.TryGetParamLong(out var mId) || uri.TryGetTargetLong(out mId))
                {
                    matchedEntry = hub.FindEntry(mId);
                }
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
                    Text = $"{matchedEntry.VisualIcon} {matchedEntry.Item.NameVi} · {matchedEntry.SubCategoryVi}",
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

                var modalQuickBtn = new Button
                {
                    Content = "🔍 Xem Đồ Giám Chi Tiết & Thông Số",
                    Style = (Style)FindResource("ChipButton"),
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
                    Margin = new Thickness(0, 8, 0, 6),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                modalBtn.Click += (_, _) => TryOpenNpcModal(inspectorNpcId);
                InspectorBody.Children.Add(modalBtn);

                var duelCmd = row.Commands.FirstOrDefault(c => c.Id.StartsWith("duel:"));
                if (duelCmd is not null)
                {
                    var duelBtn = new Button
                    {
                        Content = "⚔️ Tỷ Võ Lôi Đài (Đơn Đấu Chiến Thuật)",
                        Style = (Style)FindResource("PrimaryButton"),
                        Margin = new Thickness(0, 0, 0, 8),
                        HorizontalAlignment = HorizontalAlignment.Stretch
                    };
                    duelBtn.Click += (_, _) => TryOpenDuelModal(inspectorNpcId);
                    InspectorBody.Children.Add(duelBtn);
                }
            }
        }

        InspectorScrim.Visibility = Visibility.Visible;
        InspectorPanel.Visibility = Visibility.Visible;

        var slideAnim = new System.Windows.Media.Animation.DoubleAnimation(380.0, 0.0, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
        };
        var fadeAnim = new System.Windows.Media.Animation.DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(180));
        InspectorTransform.BeginAnimation(TranslateTransform.XProperty, slideAnim);
        InspectorScrim.BeginAnimation(OpacityProperty, fadeAnim);
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
        if (InspectorPanel.Visibility != Visibility.Visible)
        {
            return;
        }

        var slideAnim = new System.Windows.Media.Animation.DoubleAnimation(InspectorTransform.X, 380.0, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn }
        };
        var fadeAnim = new System.Windows.Media.Animation.DoubleAnimation(InspectorScrim.Opacity, 0.0, TimeSpan.FromMilliseconds(150));

        slideAnim.Completed += (_, _) =>
        {
            InspectorPanel.Visibility = Visibility.Collapsed;
            InspectorScrim.Visibility = Visibility.Collapsed;
            InspectorBody.Children.Clear();
        };

        InspectorTransform.BeginAnimation(TranslateTransform.XProperty, slideAnim);
        InspectorScrim.BeginAnimation(OpacityProperty, fadeAnim);
    }

    /// <summary>Identity of a row across refreshes: the module's title, or its first cell.</summary>
    private static string DetailKey(ScreenRow row) =>
        row.Title ?? (row.Cells.Count > 0 ? row.Cells[0] : string.Empty);

    /// <summary>Re-opens the drawer on the same row after an action re-rendered the page.</summary>
    private void RestoreDetail(ScreenModel model)
    {
        if (_detailKey is null)
        {
            CloseInspector();
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
