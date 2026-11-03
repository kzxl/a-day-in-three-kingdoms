using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using SimT.Contracts;
using SimT.Contracts.Playing;
using SimT.Contracts.Products;
using SimT.Contracts.Ui;
using SimT.Core.Kernel;

namespace SimT.Host.Wpf;

/// <summary>
/// Generic shell. It renders whatever screens the enabled modules registered: a new DLC adds
/// a tab with zero changes here. That is the payoff of the module manifest design.
/// </summary>
public partial class MainWindow : Window
{
    private const string LedgerScreenId = "__ledger";

    private readonly SimKernel _kernel;
    private readonly SaveManager _saves;
    private readonly CampaignMeta _meta;
    private bool _loaded;

    public MainWindow(SimKernel kernel, SaveManager saves, CampaignMeta meta)
    {
        InitializeComponent();
        _kernel = kernel;
        _saves = saves;
        _meta = meta;

        CampaignText.Text = meta.Name;
        BuildTabs();
        UpdateHeader();
        _loaded = true;
    }

    private void BuildTabs()
    {
        Tabs.Items.Clear();
        foreach (var screen in _kernel.Ui.Screens)
        {
            Tabs.Items.Add(new TabItem { Header = screen.Title, Tag = screen.ScreenId });
        }

        Tabs.Items.Add(new TabItem { Header = "Sổ Cái", Tag = LedgerScreenId });
        Tabs.SelectedIndex = 0;
        RenderSelected();
    }

    private void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loaded)
        {
            RenderSelected();
        }
    }

    private void RenderSelected()
    {
        if (Tabs.SelectedItem is not TabItem { Tag: string screenId } tab)
        {
            return;
        }

        tab.Content = screenId == LedgerScreenId ? BuildLedger() : BuildScreen(screenId);
    }

    private FrameworkElement BuildScreen(string screenId)
    {
        var contribution = _kernel.Ui.Screens.FirstOrDefault(screen => screen.ScreenId == screenId);
        if (contribution is null)
        {
            return new TextBlock { Text = $"Màn hình '{screenId}' không còn được module nào cung cấp." };
        }

        var model = contribution.Render();
        var panel = new StackPanel { Margin = new Thickness(4, 10, 4, 10) };

        if (!string.IsNullOrWhiteSpace(model.StatusLine))
        {
            panel.Children.Add(new TextBlock
            {
                Text = model.StatusLine,
                Style = (Style)FindResource("Caption"),
                Margin = new Thickness(2, 0, 0, 4)
            });
        }

        if (model.Commands.Count > 0)
        {
            var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 8) };
            foreach (var command in model.Commands)
            {
                var button = new Button
                {
                    Content = command.Label,
                    Tag = command.Id,
                    Style = (Style)FindResource("AccentButton"),
                    Margin = new Thickness(0, 0, 8, 0)
                };
                button.Click += RowCommand_Click;
                bar.Children.Add(button);
            }

            panel.Children.Add(bar);
        }

        foreach (var section in model.Sections)
        {
            panel.Children.Add(new TextBlock
            {
                Text = section.Heading,
                Style = (Style)FindResource("SectionTitle")
            });
            panel.Children.Add(BuildGrid(section));
        }

        return new ScrollViewer
        {
            Content = panel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
    }

    private DataGrid BuildGrid(ScreenSection section)
    {
        var rows = section.Rows.Select(row => new RowVm(row)).ToList();
        var grid = new DataGrid
        {
            ItemsSource = rows,
            MaxHeight = 460,
            Margin = new Thickness(0, 0, 0, 6)
        };

        for (var index = 0; index < section.Columns.Count; index++)
        {
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = section.Columns[index],
                Binding = new Binding($"Cells[{index}]"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            });
        }

        if (section.Rows.Any(row => row.Commands.Count > 0))
        {
            grid.Columns.Add(new DataGridTemplateColumn
            {
                Header = string.Empty,
                CellTemplate = (DataTemplate)FindResource("ActionsCell"),
                Width = DataGridLength.Auto
            });
        }

        return grid;
    }

    private FrameworkElement BuildLedger()
    {
        var lines = _kernel.Ledger.Recent(300);
        var rows = lines
            .Select(line => new RowVm(new ScreenRow(
                [
                    line.Date.ToString(),
                    line.JournalCode,
                    line.Direction == SimT.Contracts.Ledger.LedgerDirection.In ? "thu" : "chi",
                    line.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    line.BalanceAfter.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    line.ModuleId,
                    line.Description
                ],
                Array.Empty<ScreenCommand>())))
            .ToList();

        var grid = new DataGrid { ItemsSource = rows, Margin = new Thickness(4, 10, 4, 4) };
        foreach (var (header, index) in new[] { ("Ngày", 0), ("Bút toán", 1), ("Loại", 2), ("Số tiền", 3), ("Số dư", 4), ("Nguồn", 5), ("Diễn giải", 6) })
        {
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = header,
                Binding = new Binding($"Cells[{index}]"),
                Width = index == 6 ? new DataGridLength(1, DataGridLengthUnitType.Star) : DataGridLength.Auto
            });
        }

        return grid;
    }

    private void Advance_Click(object sender, RoutedEventArgs e)
    {
        _kernel.AdvanceDay();
        _kernel.Save();
        UpdateHeader();
        RenderSelected();
        StatusText.Text = $"Đã sang ngày {_kernel.Today}.";
    }

    private void RowCommand_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string commandId } || Tabs.SelectedItem is not TabItem { Tag: string screenId })
        {
            return;
        }

        if (!_kernel.TryGetCommandHandler(screenId, out var handler) || handler is null)
        {
            StatusText.Text = $"Màn hình '{screenId}' không nhận lệnh.";
            return;
        }

        try
        {
            StatusText.Text = handler.Execute(commandId) ?? "Xong.";
        }
        catch (SimException error)
        {
            StatusText.Text = $"[{error.Code}] {error.Message}";
        }

        _kernel.Save();
        UpdateHeader();
        RenderSelected();
    }

    private void UpdateHeader()
    {
        DateText.Text = _kernel.Today.ToString();
        BalanceText.Text = _kernel.Ledger.Balance.ToString(System.Globalization.CultureInfo.InvariantCulture);

        if (_kernel.Capabilities.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var player) && player is not null)
        {
            if (_kernel.Capabilities.TryGet<IWorldMap>(CapabilityIds.WorldMap, out var map) && map is not null)
            {
                var city = map.Find(player.CurrentCityId);
                CityText.Text = city is null
                    ? player.Name
                    : $"{player.Name} · {city.NameVi} ({city.NameEn})";
            }
            else
            {
                CityText.Text = player.Name;
            }
        }
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        _kernel.Save();
        _saves.Touch(_meta, _kernel.Today.ToString());
        _kernel.Dispose();
    }

    /// <summary>
    /// Renders every registered tab once. Used by the headless smoke run so XAML and
    /// read-model failures surface in CI instead of on a player's screen.
    /// </summary>
    public int RenderAllTabs()
    {
        var rendered = 0;
        for (var index = 0; index < Tabs.Items.Count; index++)
        {
            Tabs.SelectedIndex = index;
            rendered++;
        }

        UpdateLayout();
        return rendered;
    }

    /// <summary>Row shape the DataGrid binds to: a string array plus optional buttons.</summary>
    private sealed class RowVm
    {
        public RowVm(ScreenRow row)
        {
            Cells = row.Cells.ToArray();
            Commands = row.Commands;
        }

        public string[] Cells { get; }

        public IReadOnlyList<ScreenCommand> Commands { get; }
    }
}
