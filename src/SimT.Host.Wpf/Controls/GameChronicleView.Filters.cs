using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SimT.Contracts;
using SimT.Contracts.Playing;

namespace SimT.Host.Wpf.Controls;

public sealed partial class GameChronicleView
{
    private string _searchQuery = string.Empty;
    private TextBox? _searchBox;
    private TextBlock? _resultCountText;

    private readonly (string Key, string Label, string Icon)[] _macroFilters =
    [
        ("all", "Toàn Bộ", "📜"),
        ("military", "Quân Sự", "⚔️"),
        ("economy", "Kinh Tế", "💰"),
        ("social", "Nhân Vật", "👥"),
        ("local", "Tại Chỗ", "📍"),
        ("actionable", "Quyết Sách", "⚡")
    ];

    private void BuildFilterBarUi()
    {
        _filterBar.Children.Clear();

        var container = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 0, 0, 4) };

        // 1. Row of Macro Channels with Dynamic Counters
        var tabsRow = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };

        var playerCityId = 1L;
        if (_kernel != null && _kernel.Capabilities.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var player) && player != null)
        {
            playerCityId = player.CurrentCityId;
        }

        foreach (var (key, label, icon) in _macroFilters)
        {
            var count = GetFilterCount(key, playerCityId);
            var isActive = key == _currentFilter;

            var btn = new Button
            {
                Content = $"{icon} {label} ({count})",
                Tag = key,
                Margin = new Thickness(0, 0, 8, 4),
                Padding = new Thickness(12, 5, 12, 5),
                FontSize = 11.5,
                FontWeight = isActive ? FontWeights.Bold : FontWeights.Normal,
                Cursor = System.Windows.Input.Cursors.Hand,
                Background = isActive ? CinnabarSeal : ParchmentCardBg,
                Foreground = isActive ? TextIvory : TextParchmentMuted,
                BorderBrush = isActive ? GoldAccent : BorderBronze,
                BorderThickness = new Thickness(1)
            };

            btn.Click += (s, _) =>
            {
                if (s is Button b && b.Tag is string newKey)
                {
                    _currentFilter = newKey;
                    BuildFilterBarUi();
                    RenderList();
                }
            };

            tabsRow.Children.Add(btn);
        }

        container.Children.Add(tabsRow);

        // 2. Search & Result Metrics Row
        var searchRow = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };

        // Result count indicator on the right
        _resultCountText = new TextBlock
        {
            Text = $"Hiển thị {_allEvents.Count} / {_allEvents.Count} phong vân biến cố",
            FontSize = 11,
            Foreground = TextParchmentMuted,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0)
        };
        DockPanel.SetDock(_resultCountText, Dock.Right);
        searchRow.Children.Add(_resultCountText);

        // Search Box with Icon on the left
        var searchBoxBorder = new Border
        {
            Background = ParchmentCardBg,
            BorderBrush = BorderBronze,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2, 6, 2),
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var searchStack = new StackPanel { Orientation = Orientation.Horizontal };
        searchStack.Children.Add(new TextBlock
        {
            Text = "🔍",
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0)
        });

        _searchBox = new TextBox
        {
            Width = 240,
            FontSize = 11.5,
            Background = Brushes.Transparent,
            Foreground = TextIvory,
            BorderThickness = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
            Text = _searchQuery
        };
        _searchBox.TextChanged += (_, _) =>
        {
            _searchQuery = _searchBox.Text;
            RenderList();
        };
        searchStack.Children.Add(_searchBox);

        // Clear button (✕)
        var clearBtn = new Button
        {
            Content = "✕",
            Background = Brushes.Transparent,
            Foreground = TextParchmentMuted,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(4, 0, 4, 0),
            FontSize = 10,
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = "Xóa từ khóa tìm kiếm"
        };
        clearBtn.Click += (_, _) =>
        {
            if (_searchBox != null)
            {
                _searchBox.Text = string.Empty;
            }
        };
        searchStack.Children.Add(clearBtn);

        searchBoxBorder.Child = searchStack;
        searchRow.Children.Add(searchBoxBorder);

        container.Children.Add(searchRow);
        _filterBar.Children.Add(container);
    }

    private int GetFilterCount(string filterKey, long playerCityId)
    {
        return _allEvents.Count(e => MatchFilter(e, filterKey, string.Empty, playerCityId));
    }

    private bool MatchFilter(ChronicleEventModel e, string filter, string query, long playerCityId)
    {
        // 1. Text Search Filter
        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim().ToLowerInvariant();
            var matchText = e.Headline.ToLowerInvariant().Contains(q) ||
                            e.Narrative.ToLowerInvariant().Contains(q) ||
                            e.CitadelName.ToLowerInvariant().Contains(q) ||
                            e.CategoryNameVi.ToLowerInvariant().Contains(q);
            if (!matchText) return false;
        }

        // 2. Channel & Category Filter
        return filter switch
        {
            "all" => true,
            "actionable" => e.CanIntervene,
            "local" => IsLocalCitadelEvent(e, playerCityId),
            "military" => e.CategoryKey is "history" or "turmoil" or "faction" or "martial",
            "economy" => e.CategoryKey is "bankruptcy" or "store" or "rumors" or "commerce",
            "social" => e.CategoryKey is "marriage" or "bond" or "appointment" or "relationship" or "scholarly" or "governance",
            _ => e.CategoryKey == filter
        };
    }

    private bool IsLocalCitadelEvent(ChronicleEventModel e, long playerCityId)
    {
        if (string.IsNullOrWhiteSpace(e.CitadelName) || _kernel == null) return false;
        if (_kernel.Capabilities.TryGet<IWorldMap>(CapabilityIds.WorldMap, out var map) && map != null)
        {
            var cit = map.Find(playerCityId);
            return cit != null && string.Equals(cit.NameVi, e.CitadelName, StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    private IReadOnlyList<ChronicleEventModel> GetFilteredEvents()
    {
        var playerCityId = 1L;
        if (_kernel != null && _kernel.Capabilities.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var player) && player != null)
        {
            playerCityId = player.CurrentCityId;
        }

        return _allEvents
            .Where(e => MatchFilter(e, _currentFilter, _searchQuery, playerCityId))
            .ToList();
    }
}
