using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using SimT.Contracts.Ui;

namespace SimT.Host.Wpf;

public partial class MainWindow
{
    private sealed class NavEntry : INotifyPropertyChanged
    {
        private string _badge = string.Empty;
        private Visibility _badgeVisibility = Visibility.Collapsed;

        public NavEntry(string screenId, string title, string group, string iconPathData, bool showGroupHeader = false)
        {
            ScreenId = screenId;
            Title = title;
            Group = group;
            IconGeometry = Geometry.Parse(iconPathData);
            GroupHeaderVisibility = showGroupHeader ? Visibility.Visible : Visibility.Collapsed;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string ScreenId { get; }

        public string Title { get; }

        public string Group { get; }

        public Geometry IconGeometry { get; }

        public Visibility GroupHeaderVisibility { get; }

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
    public sealed class RowVm
    {
        public RowVm(ScreenSection section, ScreenRow row, MainWindow owner)
        {
            Section = section;
            Row = row;
            Cells = row.Cells.Select(CellVm.Create).ToArray();
            Chips = row.Commands
                .Where(command => !command.RequiresQuantity)
                .Select(command => new ChipVm(
                    command,
                    (Style)owner.FindResource(command.Kind == CommandKind.Danger ? "DangerButton" : "ChipButton")))
                .ToList();
        }

        public ScreenSection Section { get; }

        public ScreenRow Row { get; }

        public CellVm[] Cells { get; }

        public IReadOnlyList<ChipVm> Chips { get; }
    }

    public sealed record ChipVm(ScreenCommand Command, Style Style)
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
                AlertLevel.Critical => "#A8321F",
                AlertLevel.Warning => "#C4A062",
                _ => "#8F7F62"
            };
        }

        public string Message { get; }

        public string? ScreenId { get; }

        public string Accent { get; }
    }
}
