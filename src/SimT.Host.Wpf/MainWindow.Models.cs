using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using SimT.Contracts.Ui;

namespace SimT.Host.Wpf;

public partial class MainWindow
{
    private sealed class NavGroupVm : INotifyPropertyChanged
    {
        private bool _isExpanded;
        private string _badge = string.Empty;
        private Visibility _badgeVisibility = Visibility.Collapsed;

        public NavGroupVm(string name, string sealHan, string hint, bool isExpanded = false)
        {
            Name = name;
            SealHan = sealHan;
            Hint = hint;
            _isExpanded = isExpanded;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Name { get; }

        public string SealHan { get; }

        public string Hint { get; }

        public List<NavEntry> Entries { get; } = [];

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded == value)
                {
                    return;
                }

                _isExpanded = value;
                Notify(nameof(IsExpanded));
                Notify(nameof(ChevronText));
                foreach (var entry in Entries)
                {
                    entry.NotifyGroupExpandedChanged();
                }
            }
        }

        public string ChevronText => _isExpanded ? "▾" : "▸";

        public string Badge => _badge;

        public Visibility BadgeVisibility => _badgeVisibility;

        public void RefreshGroupBadge()
        {
            var total = Entries.Sum(e => e.BadgeCount);
            var nextBadge = total > 0 ? total.ToString(Vietnamese) : string.Empty;
            var nextVis = total > 0 ? Visibility.Visible : Visibility.Collapsed;

            if (_badge != nextBadge)
            {
                _badge = nextBadge;
                Notify(nameof(Badge));
            }

            if (_badgeVisibility != nextVis)
            {
                _badgeVisibility = nextVis;
                Notify(nameof(BadgeVisibility));
            }
        }

        private void Notify(string property) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
    }

    private sealed class NavEntry : INotifyPropertyChanged
    {
        private int _badgeCount;
        private string _badge = string.Empty;
        private Visibility _badgeVisibility = Visibility.Collapsed;

        public NavEntry(
            string screenId,
            string title,
            string description,
            string sealHan,
            NavGroupVm groupInfo,
            string iconPathData)
        {
            ScreenId = screenId;
            Title = title;
            Description = description;
            SealHan = sealHan;
            Group = groupInfo.Name;
            GroupInfo = groupInfo;
            var geom = Geometry.Parse(iconPathData);
            geom.Freeze();
            IconGeometry = geom;
            groupInfo.Entries.Add(this);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string ScreenId { get; }

        public string Title { get; }

        public string Description { get; }

        public string SealHan { get; }

        public string Group { get; }

        public NavGroupVm GroupInfo { get; }

        public Geometry IconGeometry { get; }

        public int BadgeCount => _badgeCount;

        public Visibility ItemVisibility => GroupInfo.IsExpanded ? Visibility.Visible : Visibility.Collapsed;

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
            _badgeCount = count;
            Badge = count > 0 ? $"{count.ToString(Vietnamese)} việc" : string.Empty;
            BadgeVisibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        public void NotifyGroupExpandedChanged() =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ItemVisibility)));

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
        public RowVm(ScreenSection section, ScreenRow row, MainWindow? _ = null)
        {
            Section = section;
            Row = row;
            Cells = row.Cells.Select(CellVm.Create).ToArray();
            Chips = row.Commands
                .Where(command => !command.RequiresQuantity)
                .Select(command => new ChipVm(command))
                .ToList();
        }

        public ScreenSection Section { get; }

        public ScreenRow Row { get; }

        public CellVm[] Cells { get; }

        public IReadOnlyList<ChipVm> Chips { get; }
    }

    public sealed record ChipVm(ScreenCommand Command)
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
