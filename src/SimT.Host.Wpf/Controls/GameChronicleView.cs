using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;
using SimT.Core.Kernel;

namespace SimT.Host.Wpf.Controls;

/// <summary>
/// Historical Chronicle & Events View (Thiên Hạ Biên Niên Sử).
/// Implements Elegy of the Vast Land aesthetic: Painterly Historical Illustration,
/// dark historical strategy + parchment feel, and deep interactive causality decisions.
/// </summary>
public sealed partial class GameChronicleView : Border
{
    private static readonly Brush CanvasDarkBg = Freeze(new SolidColorBrush(Color.FromRgb(0x13, 0x0E, 0x0A)));
    private static readonly Brush ParchmentCardBg = Freeze(new SolidColorBrush(Color.FromRgb(0x1E, 0x16, 0x11)));
    private static readonly Brush ParchmentCardHover = Freeze(new SolidColorBrush(Color.FromRgb(0x28, 0x1D, 0x15)));
    private static readonly Brush ParchmentSelectedBg = Freeze(new SolidColorBrush(Color.FromRgb(0x2C, 0x20, 0x17)));
    private static readonly Brush BorderBronze = Freeze(new SolidColorBrush(Color.FromRgb(0x4E, 0x3A, 0x26)));
    private static readonly Brush BorderGold = Freeze(new SolidColorBrush(Color.FromRgb(0x8C, 0x6E, 0x3C)));
    private static readonly Brush GoldAccent = Freeze(new SolidColorBrush(Color.FromRgb(0xC4, 0xA0, 0x62)));
    private static readonly Brush TextIvory = Freeze(new SolidColorBrush(Color.FromRgb(0xF3, 0xE6, 0xCC)));
    private static readonly Brush TextParchmentMuted = Freeze(new SolidColorBrush(Color.FromRgb(0xB8, 0xA6, 0x88)));
    private static readonly Brush CinnabarSeal = Freeze(new SolidColorBrush(Color.FromRgb(0xA8, 0x32, 0x1F)));
    private static readonly Brush SuccessGreen = Freeze(new SolidColorBrush(Color.FromRgb(0x34, 0xD3, 0x99)));

    // Internal model for unified historical event stream
    private sealed class ChronicleEventModel
    {
        public bool IsMuted { get; init; }
        public long Id { get; init; }
        public long? SeedId { get; init; }
        public GameDate Date { get; init; }
        public string CitadelName { get; init; } = string.Empty;
        public string CategoryKey { get; init; } = "general";
        public string CategoryNameVi { get; init; } = "Thời Cuộc";
        public string Headline { get; init; } = string.Empty;
        public string Narrative { get; init; } = string.Empty;
        public string? StatImpact { get; init; }
        public bool CanIntervene { get; init; }
        public IReadOnlyList<KarmaPromptOption>? Options { get; init; }
        public bool IsResolved { get; init; }
        public string? OutcomeVi { get; init; }
        public string? HistoricalEventCode { get; init; }
    }

    private SimKernel? _kernel;
    private string _currentFilter = "all";
    private readonly List<ChronicleEventModel> _allEvents = new();
    private ChronicleEventModel? _selectedEvent;

    // UI elements
    private readonly WrapPanel _filterBar;
    private readonly StackPanel _eventListStack;
    private readonly Border _detailContainer;
    private readonly ScrollViewer _listScroller;

    public event Action? EventInteracted;
    public event Action<string>? HistoricalEventReplayRequested;

    public GameChronicleView()
    {
        Background = CanvasDarkBg;
        BorderThickness = new Thickness(0);

        var rootGrid = new Grid();
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header & Filter
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 2-Column Master-Detail

        // Row 0: Parchment Header & Filter Tabs
        var topSection = new StackPanel { Margin = new Thickness(16, 12, 16, 10) };

        var headerRow = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        var titleStack = new StackPanel();

        var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
        var seal = new Border
        {
            Background = CinnabarSeal,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2, 6, 2),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        seal.Child = new TextBlock
        {
            Text = "史",
            FontFamily = new FontFamily("KaiTi, STKaiti, SimSun, Georgia"),
            FontWeight = FontWeights.Bold,
            FontSize = 14,
            Foreground = TextIvory
        };
        titleRow.Children.Add(seal);

        titleRow.Children.Add(new TextBlock
        {
            Text = "THIÊN HẠ BIÊN NIÊN SỬ · PHONG VÂN KỶ YẾU",
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            FontFamily = new FontFamily("KaiTi, STKaiti, SimSun, Georgia"),
            Foreground = GoldAccent,
            VerticalAlignment = VerticalAlignment.Center
        });
        titleStack.Children.Add(titleRow);

        titleStack.Children.Add(new TextBlock
        {
            Text = "Ghi chép hưng phế của thế gian, biến thiên của nhật nguyệt, và từng quyết sách chuyển dịch càn khôn của bậc hào kiệt.",
            FontSize = 11,
            Foreground = TextParchmentMuted,
            Margin = new Thickness(0, 3, 0, 0)
        });
        headerRow.Children.Add(titleStack);
        topSection.Children.Add(headerRow);

        // Filter Bar (Macro Channels + Search)
        _filterBar = new WrapPanel { Orientation = Orientation.Horizontal };
        BuildFilterBarUi();
        topSection.Children.Add(_filterBar);

        Grid.SetRow(topSection, 0);
        rootGrid.Children.Add(topSection);

        // Row 1: 2-Column Master-Detail View
        var bodyGrid = new Grid { Margin = new Thickness(16, 0, 16, 16) };
        bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(420) });
        bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Left Deck: Scrollable Event Cards List
        _listScroller = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        _eventListStack = new StackPanel();
        _listScroller.Content = _eventListStack;
        Grid.SetColumn(_listScroller, 0);
        bodyGrid.Children.Add(_listScroller);

        // Right Deck: Historical Detail & Interactive Decision Pad
        _detailContainer = new Border
        {
            Background = ParchmentCardBg,
            BorderBrush = BorderBronze,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(18, 16, 18, 16)
        };
        Grid.SetColumn(_detailContainer, 2);
        bodyGrid.Children.Add(_detailContainer);

        Grid.SetRow(bodyGrid, 1);
        rootGrid.Children.Add(bodyGrid);

        Child = rootGrid;
    }

    public void Bind(SimKernel kernel)
    {
        _kernel = kernel;
        LoadEvents();
        BuildFilterBarUi();
        RenderList();
    }

    private void BuildFilterButtons() => BuildFilterBarUi();

    private void LoadEvents()
    {
        _allEvents.Clear();
        if (_kernel is null) return;

        var playerCityId = 1L;
        if (_kernel.Capabilities.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var player) && player is not null)
        {
            playerCityId = player.CurrentCityId;
        }

        var today = _kernel.Today;

        // 1. Load pending actionable events from Karma
        if (_kernel.Capabilities.TryGet<ISocietyChronicle>(CapabilityIds.SocietyChronicle, out var society) && society is not null)
        {
            var pending = society.Karma.CheckPendingEvents(playerCityId, (int)today.DayIndex);
            foreach (var p in pending)
            {
                var catKey = MapChainCodeToCategory(p.ChainCode);
                _allEvents.Add(new ChronicleEventModel
                {
                    Id = p.SeedId,
                    SeedId = p.SeedId,
                    Date = today,
                    CitadelName = p.CitadelNameVi,
                    CategoryKey = catKey,
                    CategoryNameVi = FormatCategoryName(catKey),
                    Headline = p.TitleVi,
                    Narrative = p.NarrativeVi,
                    CanIntervene = true,
                    Options = p.Options,
                    IsResolved = false
                });
            }

            // Load resolved karma seeds for completed history
            var seeds = society.Karma.GetAllSeeds().Where(s => s.IsResolved).Take(15);
            foreach (var s in seeds)
            {
                var catKey = MapChainCodeToCategory(s.ChainCode);
                _allEvents.Add(new ChronicleEventModel
                {
                    Id = s.Id + 100000,
                    SeedId = s.Id,
                    Date = s.CreatedDate,
                    CitadelName = s.CitadelNameVi,
                    CategoryKey = catKey,
                    CategoryNameVi = FormatCategoryName(catKey),
                    Headline = s.TitleVi,
                    Narrative = s.LastOutcomeVi,
                    CanIntervene = false,
                    IsResolved = true,
                    OutcomeVi = s.LastOutcomeVi
                });
            }

            // 2. Load standard living world logs from Chronicle
            var logs = society.RecentEvents(limit: 45);
            foreach (var l in logs)
            {
                var catKey = DetectCategoryFromHeadline(l.HeadlineVi, l.EventKind);
                _allEvents.Add(new ChronicleEventModel
                {
                    Id = l.Id,
                    Date = l.Date,
                    CitadelName = l.CitadelNameVi,
                    CategoryKey = catKey,
                    CategoryNameVi = FormatCategoryName(catKey),
                    Headline = l.HeadlineVi,
                    Narrative = l.DetailVi,
                    StatImpact = l.StatImpact,
                    CanIntervene = false,
                    IsResolved = true,
                    HistoricalEventCode = ExtractHistoricalCode(l.StatImpact)
                });
            }

            LoadHistoricalEvents(society);
        }

        LoadRumorEvents();

        // Sort events: actionable first, then descending by date
        _allEvents.Sort((a, b) =>
        {
            if (a.CanIntervene && !b.CanIntervene) return -1;
            if (!a.CanIntervene && b.CanIntervene) return 1;
            return b.Date.DayIndex.CompareTo(a.Date.DayIndex);
        });

        if (_allEvents.Count > 0 && (_selectedEvent is null || !_allEvents.Contains(_selectedEvent)))
        {
            _selectedEvent = _allEvents[0];
        }
    }

    private void RenderList()
    {
        _eventListStack.Children.Clear();

        var filtered = GetFilteredEvents();
        if (_resultCountText != null)
        {
            _resultCountText.Text = $"Hiển thị {filtered.Count} / {_allEvents.Count} phong vân biến cố";
        }

        if (filtered.Count == 0)
        {
            _eventListStack.Children.Add(new Border
            {
                Background = ParchmentCardBg,
                BorderBrush = BorderBronze,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(16),
                Child = new TextBlock
                {
                    Text = "Chưa có sự kiện nào được ghi nhận trong chuyên mục này.",
                    Foreground = TextParchmentMuted,
                    FontSize = 12
                }
            });
            RenderDetail(null);
            return;
        }

        if (_selectedEvent is null || !filtered.Contains(_selectedEvent))
        {
            _selectedEvent = filtered[0];
        }

        foreach (var ev in filtered)
        {
            _eventListStack.Children.Add(BuildEventCard(ev));
        }

        RenderDetail(_selectedEvent);
    }

    private UIElement BuildEventCard(ChronicleEventModel ev)
    {
        var isSelected = ev == _selectedEvent;

        var card = new Border
        {
            Opacity = ev.IsMuted ? 0.45 : 1.0,
            Background = isSelected ? ParchmentSelectedBg : ParchmentCardBg,
            BorderBrush = isSelected ? GoldAccent : BorderBronze,
            BorderThickness = new Thickness(isSelected ? 1.5 : 1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 0, 6, 8),
            Cursor = System.Windows.Input.Cursors.Hand
        };

        var dock = new DockPanel();

        // Right Actionable Badge
        if (ev.CanIntervene)
        {
            var actBadge = new GameBadge("⚡ Quyết Sách", BadgeVariant.Gold)
            {
                Size = BadgeSize.Small,
                Margin = new Thickness(6, 0, 0, 0)
            };
            DockPanel.SetDock(actBadge, Dock.Right);
            dock.Children.Add(actBadge);
        }

        var stack = new StackPanel();

        // Date & Category & Citadel Row
        var metaRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 3) };
        metaRow.Children.Add(new TextBlock
        {
            Text = $"[{GameDateCodec.ShortVi(ev.Date)}]",
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = GoldAccent,
            Margin = new Thickness(0, 0, 6, 0)
        });

        var catBadge = new GameBadge(ev.CategoryNameVi, GetBadgeVariantForCategory(ev.CategoryKey))
        {
            Size = BadgeSize.Small,
            Margin = new Thickness(0, 0, 6, 0)
        };
        metaRow.Children.Add(catBadge);

        if (!string.IsNullOrWhiteSpace(ev.CitadelName))
        {
            metaRow.Children.Add(new TextBlock
            {
                Text = $"· {ev.CitadelName}",
                FontSize = 11,
                Foreground = TextParchmentMuted
            });
        }
        stack.Children.Add(metaRow);

        // Headline
        stack.Children.Add(new TextBlock
        {
            Text = ev.Headline,
            FontWeight = isSelected ? FontWeights.Bold : FontWeights.SemiBold,
            FontSize = 12.5,
            Foreground = isSelected ? TextIvory : TextParchmentMuted,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 2)
        });

        // 1-line narrative snippet
        var snippet = ev.Narrative.Length > 65 ? ev.Narrative[..65] + "…" : ev.Narrative;
        stack.Children.Add(new TextBlock
        {
            Text = snippet,
            FontSize = 11,
            Foreground = Freeze(new SolidColorBrush(Color.FromRgb(0x8C, 0x7E, 0x68))),
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        dock.Children.Add(stack);
        card.Child = dock;

        card.MouseEnter += (_, _) =>
        {
            if (ev != _selectedEvent) card.Background = ParchmentCardHover;
        };
        card.MouseLeave += (_, _) =>
        {
            if (ev != _selectedEvent) card.Background = ParchmentCardBg;
        };
        card.MouseLeftButtonDown += (_, _) =>
        {
            _selectedEvent = ev;
            RenderList();
        };

        return card;
    }

    // =========================================================================
    // Right Detail View: Painterly Illustration & Decision Pad
    // =========================================================================
    private void RenderDetail(ChronicleEventModel? ev)
    {
        if (ev is null)
        {
            _detailContainer.Child = new TextBlock
            {
                Text = "Hãy chọn một sự kiện trong niên biểu để xem tường tận.",
                Foreground = TextParchmentMuted,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            return;
        }

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };

        var root = new StackPanel();

        // 1. Painterly Contextual Illustration Canvas
        root.Children.Add(BuildPainterlyIllustration(ev.CategoryKey, ev.CitadelName));

        // 2. Chronicle Metadata & Title
        var headerPanel = new StackPanel { Margin = new Thickness(0, 14, 0, 8) };

        var metaRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
        metaRow.Children.Add(new TextBlock
        {
            Text = $"KỶ NIÊN HIỆU: [{GameDateCodec.ShortVi(ev.Date)}] · {ev.CitadelName.ToUpperInvariant()}",
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = GoldAccent,
            Margin = new Thickness(0, 0, 8, 0)
        });
        metaRow.Children.Add(new GameBadge(ev.CategoryNameVi, GetBadgeVariantForCategory(ev.CategoryKey)) { Size = BadgeSize.Small });
        headerPanel.Children.Add(metaRow);

        headerPanel.Children.Add(new TextBlock
        {
            Text = ev.Headline,
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            FontFamily = new FontFamily("KaiTi, STKaiti, SimSun, Georgia"),
            Foreground = TextIvory,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 6)
        });

        root.Children.Add(headerPanel);

        if (!string.IsNullOrWhiteSpace(ev.HistoricalEventCode))
        {
            root.Children.Add(BuildHistoricalReplayPad(ev.HistoricalEventCode));
        }

        // 3. Historical Chronicle Narrative Box
        var narrativeBox = new Border
        {
            Background = CanvasDarkBg,
            BorderBrush = BorderBronze,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14, 12, 14, 12),
            Margin = new Thickness(0, 0, 0, 14)
        };

        var narrativeStack = new StackPanel();
        narrativeStack.Children.Add(new TextBlock
        {
            Text = "📜 TRẦN THUẬT SỬ LIỆU:",
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = GoldAccent,
            Margin = new Thickness(0, 0, 0, 6)
        });

        narrativeStack.Children.Add(new TextBlock
        {
            Text = ev.Narrative,
            FontSize = 12.5,
            Foreground = TextIvory,
            LineHeight = 20,
            TextWrapping = TextWrapping.Wrap
        });

        if (!string.IsNullOrWhiteSpace(ev.StatImpact))
        {
            narrativeStack.Children.Add(new TextBlock
            {
                Text = $"⚡ Ảnh hưởng thế cục: {ev.StatImpact}",
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = SuccessGreen,
                Margin = new Thickness(0, 8, 0, 0)
            });
        }

        narrativeBox.Child = narrativeStack;
        root.Children.Add(narrativeBox);

        // 4. Interactive Decision Pad (Quyết Sách Tương Tác)
        if (ev.CanIntervene && ev.Options is { Count: > 0 } && ev.SeedId.HasValue)
        {
            root.Children.Add(BuildInteractiveDecisionPad(ev));
        }
        else if (ev.IsResolved && !string.IsNullOrWhiteSpace(ev.OutcomeVi))
        {
            var outcomeBox = new Border
            {
                Background = Freeze(new SolidColorBrush(Color.FromRgb(0x1C, 0x22, 0x1A))),
                BorderBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x3B, 0x6E, 0x48))),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var outStack = new StackPanel();
            outStack.Children.Add(new TextBlock
            {
                Text = "✔️ SỬ BÚT AN BÀI · QUYẾT SÁCH ĐÃ THỰC THI",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = SuccessGreen,
                Margin = new Thickness(0, 0, 0, 4)
            });
            outStack.Children.Add(new TextBlock
            {
                Text = ev.OutcomeVi,
                FontSize = 12,
                Foreground = TextIvory,
                TextWrapping = TextWrapping.Wrap
            });
            outcomeBox.Child = outStack;
            root.Children.Add(outcomeBox);
        }

        scroll.Content = root;
        _detailContainer.Child = scroll;
    }

    // =========================================================================
    // Painterly Historical Contextual Illustration
    // =========================================================================
    private UIElement BuildPainterlyIllustration(string categoryKey, string citadelName)
    {
        var frame = new Border
        {
            Height = 105,
            CornerRadius = new CornerRadius(6),
            BorderBrush = BorderGold,
            BorderThickness = new Thickness(1.5),
            ClipToBounds = true
        };

        var canvas = new Grid();

        // 1. Painterly Gradient Backdrop depending on event mood
        var (gradTop, gradBottom, sealChar, subTitle) = categoryKey switch
        {
            "history" => (Color.FromRgb(0x4A, 0x16, 0x10), Color.FromRgb(0x18, 0x0A, 0x06), "史變", "Đại Cuộc Sử Biến"),
            "turmoil" => (Color.FromRgb(0x42, 0x14, 0x0E), Color.FromRgb(0x1B, 0x0B, 0x07), "烽火", "Khói Lửa Binh Biến"),
            "bankruptcy" => (Color.FromRgb(0x28, 0x1A, 0x12), Color.FromRgb(0x12, 0x0C, 0x08), "虧損", "Thương Trường Sa Cơ"),
            "store" => (Color.FromRgb(0x36, 0x24, 0x12), Color.FromRgb(0x16, 0x10, 0x0A), "開市", "Khai Trương Đại Cát"),
            "marriage" => (Color.FromRgb(0x4A, 0x1A, 0x22), Color.FromRgb(0x1D, 0x0B, 0x10), "結髮", "Hồng Trang Kết Duyên"),
            "appointment" => (Color.FromRgb(0x2A, 0x1E, 0x36), Color.FromRgb(0x12, 0x0C, 0x18), "拜官", "Sắc Phong Trấn Nhậm"),
            "bond" => (Color.FromRgb(0x1E, 0x2C, 0x24), Color.FromRgb(0x0C, 0x16, 0x10), "知己", "Kim Lan Hội Ngộ"),
            "faction" => (Color.FromRgb(0x3C, 0x18, 0x10), Color.FromRgb(0x16, 0x0A, 0x06), "爭霸", "Chư Hầu Khởi Binh"),
            _ => (Color.FromRgb(0x2A, 0x1F, 0x16), Color.FromRgb(0x14, 0x0E, 0x0A), "春秋", "Thế Cuộc Phong Vân")
        };

        var gradBrush = new LinearGradientBrush
        {
            StartPoint = new Point(0.5, 0),
            EndPoint = new Point(0.5, 1),
            GradientStops = new GradientStopCollection
            {
                new(gradTop, 0.0),
                new(gradBottom, 1.0)
            }
        };
        canvas.Background = gradBrush;

        // 2. Artistic silhouette shapes / scenery simulation
        var decoCanvas = new Canvas();

        // Distant mountains silhouette
        var poly = new Polygon
        {
            Points = new PointCollection
            {
                new(0, 180), new(50, 120), new(120, 150), new(210, 90),
                new(320, 140), new(420, 80), new(500, 130), new(600, 100),
                new(700, 160), new(800, 110), new(900, 180)
            },
            Fill = Freeze(new SolidColorBrush(Color.FromArgb(0x40, 0x00, 0x00, 0x00)))
        };
        decoCanvas.Children.Add(poly);

        canvas.Children.Add(decoCanvas);

        // 3. Top-Right Imperial Cinnabar Seal
        var sealBox = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 12, 16, 0),
            Background = CinnabarSeal,
            BorderBrush = GoldAccent,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(6, 4, 6, 4)
        };
        sealBox.Child = new TextBlock
        {
            Text = sealChar,
            FontFamily = new FontFamily("KaiTi, STKaiti, SimSun, Georgia"),
            FontWeight = FontWeights.Bold,
            FontSize = 14,
            Foreground = TextIvory
        };
        canvas.Children.Add(sealBox);

        // 4. Bottom Scenery Label
        var labelBox = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(14, 0, 0, 12),
            Background = Freeze(new SolidColorBrush(Color.FromArgb(0xCC, 0x14, 0x0E, 0x0A))),
            BorderBrush = BorderBronze,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 4, 10, 4)
        };

        var labelStack = new StackPanel { Orientation = Orientation.Horizontal };
        labelStack.Children.Add(new TextBlock
        {
            Text = "🎨 MINH HỌA DÃ SỬ: ",
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            Foreground = GoldAccent
        });
        labelStack.Children.Add(new TextBlock
        {
            Text = $"{subTitle} tại {citadelName}",
            FontSize = 10,
            Foreground = TextIvory
        });
        labelBox.Child = labelStack;
        canvas.Children.Add(labelBox);

        frame.Child = canvas;
        return frame;
    }

    // =========================================================================
    // Interactive Decision Pad (Lựa chọn hành động can thiệp)
    // =========================================================================
    private UIElement BuildInteractiveDecisionPad(ChronicleEventModel ev)
    {
        var pad = new Border
        {
            Background = Freeze(new SolidColorBrush(Color.FromRgb(0x22, 0x18, 0x10))),
            BorderBrush = GoldAccent,
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14, 12, 14, 12),
            Margin = new Thickness(0, 4, 0, 10)
        };

        var stack = new StackPanel();

        var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        titleRow.Children.Add(new TextBlock
        {
            Text = "⚡ QUYẾT SÁCH TRƯỚC THỜI CUỘC · MỜI BẬC HÀO KIỆT ĐỊNH ĐOẠT:",
            FontSize = 11.5,
            FontWeight = FontWeights.Bold,
            Foreground = GoldAccent
        });
        stack.Children.Add(titleRow);

        foreach (var opt in ev.Options!)
        {
            var btnBorder = new Border
            {
                Background = CanvasDarkBg,
                BorderBrush = BorderBronze,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, 0, 8),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            var dock = new DockPanel();

            var pickBtn = new GameButton
            {
                Content = "Quyết Định",
                Variant = opt.KarmaAlignment == "virtue" ? GameButtonVariant.Primary : GameButtonVariant.Secondary,
                MinHeight = 28,
                Padding = new Thickness(10, 3, 10, 3),
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(pickBtn, Dock.Right);
            dock.Children.Add(pickBtn);

            var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };

            var labelRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 2) };
            labelRow.Children.Add(new TextBlock
            {
                Text = opt.LabelVi,
                FontWeight = FontWeights.Bold,
                FontSize = 12.5,
                Foreground = TextIvory
            });

            var alignBadge = opt.KarmaAlignment switch
            {
                "virtue" => new GameBadge("Nhân Nghĩa", BadgeVariant.Gain) { Size = BadgeSize.Small, Margin = new Thickness(6, 0, 0, 0) },
                "ruthless" => new GameBadge("Bá Đạo", BadgeVariant.Loss) { Size = BadgeSize.Small, Margin = new Thickness(6, 0, 0, 0) },
                _ => new GameBadge("Trung Lập", BadgeVariant.Default) { Size = BadgeSize.Small, Margin = new Thickness(6, 0, 0, 0) }
            };
            labelRow.Children.Add(alignBadge);
            info.Children.Add(labelRow);

            info.Children.Add(new TextBlock
            {
                Text = opt.DescriptionVi,
                FontSize = 11,
                Foreground = TextParchmentMuted,
                TextWrapping = TextWrapping.Wrap
            });

            dock.Children.Add(info);
            btnBorder.Child = dock;

            // Trigger resolution
            Action resolveAction = () =>
            {
                if (_kernel is null || !ev.SeedId.HasValue) return;

                if (_kernel.Capabilities.TryGet<ISocietyChronicle>(CapabilityIds.SocietyChronicle, out var society) && society is not null)
                {
                    var result = society.Karma.ResolveChoice(ev.SeedId.Value, opt.ChoiceKey);
                    if (result.Success)
                    {
                        // Apply money change
                        if (result.MoneyChange != 0 && _kernel.Capabilities.TryGet<IWallet>(CapabilityIds.PlayerWallet, out var wallet) && wallet is not null)
                        {
                            if (result.MoneyChange > 0)
                                wallet.Pay(result.MoneyChange, "society", "Quyết sách dã sử: " + result.HeadlineVi);
                            else
                                wallet.Charge(Math.Abs(result.MoneyChange), "society", "Quyết sách dã sử: " + result.HeadlineVi);
                        }

                        // Apply reputation change
                        if (result.FameChange != 0 && _kernel.Capabilities.TryGet<IPlayerReputation>(CapabilityIds.PlayerReputation, out var rep) && rep is not null)
                        {
                            rep.GainFame(result.FameChange, "society", "Quyết Sách Dã Sử", result.HeadlineVi);
                        }

                        _kernel.Save();
                        EventInteracted?.Invoke();
                        LoadEvents();
                        RenderList();
                    }
                }
            };

            pickBtn.Click += (_, _) => resolveAction();
            btnBorder.MouseLeftButtonDown += (_, _) => resolveAction();

            btnBorder.MouseEnter += (_, _) => btnBorder.Background = ParchmentCardHover;
            btnBorder.MouseLeave += (_, _) => btnBorder.Background = CanvasDarkBg;

            stack.Children.Add(btnBorder);
        }

        pad.Child = stack;
        return pad;
    }

    // =========================================================================
    // Category Mappers & Helpers
    // =========================================================================
    private static string MapChainCodeToCategory(string code) => code switch
    {
        "CH_NPC_MARRIAGE" => "marriage",
        "CH_MERCHANT_BANKRUPTCY" => "bankruptcy",
        "CH_STORE_OPENED" => "store",
        "CH_CITY_TURMOIL" => "turmoil",
        "CH_OFFICIAL_APPOINTED" => "appointment",
        "CH_PLAYER_BOND" => "bond",
        "CH_FACTION_EMERGED" => "faction",
        _ => "turmoil"
    };

    private static string DetectCategoryFromHeadline(string headline, string eventKind)
    {
        if (string.Equals(eventKind, "history", StringComparison.OrdinalIgnoreCase)) return "history";
        var lower = headline.ToLowerInvariant();
        if (lower.Contains("[sử biến]")) return "history";
        if (lower.Contains("kết hôn") || lower.Contains("thành hôn") || lower.Contains("hôn sự")) return "marriage";
        if (lower.Contains("phá sản") || lower.Contains("vỡ nợ") || lower.Contains("sa cơ")) return "bankruptcy";
        if (lower.Contains("khai trương") || lower.Contains("chi điếm") || lower.Contains("cửa hiệu")) return "store";
        if (lower.Contains("loạn") || lower.Contains("tiễu phỉ") || lower.Contains("nổi dậy") || lower.Contains("binh biến")) return "turmoil";
        if (lower.Contains("bổ nhiệm") || lower.Contains("thái thú") || lower.Contains("quan chức")) return "appointment";
        if (lower.Contains("kết giao") || lower.Contains("kết nghĩa") || lower.Contains("tri kỷ")) return "bond";
        if (lower.Contains("chư hầu") || lower.Contains("khởi binh") || lower.Contains("thế lực")) return "faction";

        return eventKind switch
        {
            "history" => "history",
            "martial" => "turmoil",
            "commerce" => "store",
            "governance" => "appointment",
            "scholarly" or "relationship" => "bond",
            _ => "general"
        };
    }

    private static string FormatCategoryName(string catKey) => catKey switch
    {
        "history" => "Sử biến",
        "turmoil" => "Loạn Lạc",
        "bankruptcy" => "Phá Sản",
        "store" => "Cửa Hiệu",
        "marriage" => "Hôn Sự",
        "appointment" => "Bổ Nhiệm",
        "bond" => "Kết Giao",
        "faction" => "Thế Lực",
        _ => "Thời Cuộc"
    };

    private static BadgeVariant GetBadgeVariantForCategory(string catKey) => catKey switch
    {
        "history" => BadgeVariant.Gold,
        "turmoil" => BadgeVariant.Loss,
        "bankruptcy" => BadgeVariant.Warning,
        "store" => BadgeVariant.Gain,
        "marriage" => BadgeVariant.Rose,
        "appointment" => BadgeVariant.Purple,
        "bond" => BadgeVariant.Gold,
        "faction" => BadgeVariant.Info,
        _ => BadgeVariant.Default
    };

    private static Brush Freeze(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }
}
