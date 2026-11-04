using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SimT.Contracts.Ui;

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
}
