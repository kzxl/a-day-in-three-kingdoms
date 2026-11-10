using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SimT.Contracts.Ui;

namespace SimT.Host.Wpf.Controls;

/// <summary>
/// Thematic Caravan Dispatch Board for logistics and trading expeditions.
/// Supports 3 distinct visual modes:
/// 1. Crew Triptych (Tam Trụ Tiêu Cục): 3 dedicated officer cards for Escort, Accountant, Navigator.
/// 2. Route Dispatch Deck: Animated caravan trail canvas, route selector pills, and dispatch commands.
/// 3. Contract Board: Interactive commercial transport commission cards with seals and rewards.
/// </summary>
public sealed class GameCaravanDispatchControl : Border
{
    private static readonly Brush PanelBg = Freeze(new SolidColorBrush(Color.FromRgb(0x18, 0x12, 0x0E)));
    private static readonly Brush CardBg = Freeze(new SolidColorBrush(Color.FromRgb(0x21, 0x18, 0x12)));
    private static readonly Brush CardBorder = Freeze(new SolidColorBrush(Color.FromRgb(0x3A, 0x2C, 0x20)));
    private static readonly Brush AccentGold = Freeze(new SolidColorBrush(Color.FromRgb(0xC4, 0xA0, 0x62)));
    private static readonly Brush JadeGreen = Freeze(new SolidColorBrush(Color.FromRgb(0x3F, 0x6B, 0x55)));
    private static readonly Brush CinnabarRed = Freeze(new SolidColorBrush(Color.FromRgb(0xA8, 0x32, 0x1F)));
    private static readonly Brush TextMain = Freeze(new SolidColorBrush(Color.FromRgb(0xF3, 0xE6, 0xCC)));
    private static readonly Brush TextMuted = Freeze(new SolidColorBrush(Color.FromRgb(0x8F, 0x7F, 0x62)));
    private static readonly FontFamily MonoFont = new("Consolas");

    private readonly ScreenSection _section;
    private readonly Action<string>? _commandInvoker;
    private readonly Action<ScreenSection, ScreenRow>? _rowInspector;
    private readonly string _currentCityName;

    public GameCaravanDispatchControl(
        ScreenSection section,
        Action<string>? commandInvoker,
        Action<ScreenSection, ScreenRow>? rowInspector = null,
        string currentCityName = "Bản Thành")
    {
        _section = section;
        _commandInvoker = commandInvoker;
        _rowInspector = rowInspector;
        _currentCityName = currentCityName;

        Background = PanelBg;
        BorderBrush = CardBorder;
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(6);
        Padding = new Thickness(12);
        Margin = new Thickness(0, 4, 0, 8);
        SnapsToDevicePixels = true;

        if (section.Heading.Contains("Nhân Sự", StringComparison.OrdinalIgnoreCase))
        {
            Child = BuildCrewTriptych();
        }
        else if (section.Heading.Contains("Hợp Đồng", StringComparison.OrdinalIgnoreCase))
        {
            Child = BuildContractBoard();
        }
        else if (section.Heading.Contains("Lũng Đoạn", StringComparison.OrdinalIgnoreCase))
        {
            Child = BuildCorneredMarketBoard();
        }
        else if (section.Heading.Contains("Buôn Lậu", StringComparison.OrdinalIgnoreCase))
        {
            Child = BuildSmugglingConvoyBoard();
        }
        else if (section.Heading.Contains("Nhật Ký Áp Tải", StringComparison.OrdinalIgnoreCase))
        {
            Child = BuildSmugglingLogBoard();
        }
        else
        {
            Child = BuildRouteDispatchDeck();
        }
    }

    #region 1. Crew Triptych (Tam Trụ Tiêu Cục)
    private UIElement BuildCrewTriptych()
    {
        var root = new StackPanel();

        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
        header.Children.Add(new TextBlock
        {
            Text = "TAM TRỤ TIÊU CỤC · ĐỘI NGŨ CHỦ CHỐT THƯƠNG ĐOÀN",
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = AccentGold,
            VerticalAlignment = VerticalAlignment.Center
        });
        root.Children.Add(header);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        for (var i = 0; i < Math.Min(3, _section.Rows.Count); i++)
        {
            var row = _section.Rows[i];
            var colIndex = i;

            var card = BuildCrewCard(row, colIndex switch
            {
                0 => ("TIÊU ĐẦU 🛡️", "Áp tải, đẩy lùi cướp bóc", AccentGold),
                1 => ("CHƯỞNG QUỸ 💰", "Sổ sách, đàm phán cước", JadeGreen),
                _ => ("HOA TIÊU 🧭", "Địa hình, rút ngắn lộ trình", CinnabarRed)
            });

            Grid.SetColumn(card, colIndex);
            grid.Children.Add(card);
        }

        root.Children.Add(grid);
        return root;
    }

    private Border BuildCrewCard(ScreenRow row, (string RoleTitle, string Duty, Brush Accent) meta)
    {
        var card = new Border
        {
            Background = CardBg,
            BorderBrush = CardBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12),
            Margin = new Thickness(4)
        };

        var stack = new StackPanel();

        // Role title
        var roleText = new TextBlock
        {
            Text = meta.RoleTitle,
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = meta.Accent
        };
        stack.Children.Add(roleText);

        var dutyText = new TextBlock
        {
            Text = meta.Duty,
            FontSize = 9.5,
            Foreground = TextMuted,
            Margin = new Thickness(0, 2, 0, 8)
        };
        stack.Children.Add(dutyText);

        // Officer name & stat
        var officerName = row.Cells.Count > 1 ? row.Cells[1] : "(Trống)";
        var statText = row.Cells.Count > 2 ? row.Cells[2] : "—";
        var isAssigned = !string.Equals(officerName, "(Trống)", StringComparison.OrdinalIgnoreCase);

        var nameBox = new Border
        {
            Background = PanelBg,
            BorderBrush = isAssigned ? meta.Accent : CardBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(0, 0, 0, 8)
        };

        var nameStack = new DockPanel();
        nameStack.Children.Add(new TextBlock
        {
            Text = officerName,
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = isAssigned ? TextMain : TextMuted,
            VerticalAlignment = VerticalAlignment.Center
        });

        var statBadge = new TextBlock
        {
            Text = statText,
            FontFamily = MonoFont,
            FontSize = 10.5,
            FontWeight = FontWeights.Bold,
            Foreground = meta.Accent,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(statBadge, Dock.Right);
        nameStack.Children.Add(statBadge);
        nameBox.Child = nameStack;
        stack.Children.Add(nameBox);

        // Perk / Skill description
        var perkText = row.Cells.Count > 3 ? row.Cells[3] : string.Empty;
        var perkBlock = new TextBlock
        {
            Text = perkText,
            FontSize = 10,
            Foreground = isAssigned ? TextMain : TextMuted,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 36,
            Margin = new Thickness(0, 0, 0, 10)
        };
        stack.Children.Add(perkBlock);

        // Action Command buttons
        if (row.Commands.Count > 0)
        {
            foreach (var cmd in row.Commands)
            {
                var isDismiss = cmd.Id.StartsWith("unassign", StringComparison.OrdinalIgnoreCase);
                var btn = new GameButton(
                    cmd.Label,
                    isDismiss ? GameButtonVariant.Danger : GameButtonVariant.Primary,
                    isCompact: true)
                {
                    Margin = new Thickness(0, 2, 0, 2),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                btn.Click += (_, _) => _commandInvoker?.Invoke(cmd.Id);
                stack.Children.Add(btn);
            }
        }
        else
        {
            var noCandidate = new TextBlock
            {
                Text = "Chưa có danh sĩ phù hợp tại thành",
                FontSize = 9.5,
                Foreground = TextMuted,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 6, 0, 0)
            };
            stack.Children.Add(noCandidate);
        }

        card.Child = stack;
        return card;
    }
    #endregion

    #region 2. Route Dispatch Deck
    private UIElement BuildRouteDispatchDeck()
    {
        var root = new StackPanel();

        // 1. Trail Route Canvas
        var trailCanvas = new GameCaravanTrailControl
        {
            Height = 110,
            Margin = new Thickness(0, 0, 0, 10)
        };
        root.Children.Add(trailCanvas);

        // 2. Route Selector Header
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        header.Children.Add(new TextBlock
        {
            Text = "LỘ TRÌNH VẬN TẢI & THƯƠNG ĐOÀN KHỞI HÀNH",
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = AccentGold,
            VerticalAlignment = VerticalAlignment.Center
        });
        root.Children.Add(header);

        // 3. Dynamic Selection Container
        var deckGrid = new Grid();
        deckGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        deckGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(320, GridUnitType.Pixel) });

        var routesScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 240,
            Margin = new Thickness(0, 0, 10, 0)
        };
        var routesStack = new StackPanel();
        routesScroll.Content = routesStack;
        Grid.SetColumn(routesScroll, 0);
        deckGrid.Children.Add(routesScroll);

        var actionBox = new Border
        {
            Background = CardBg,
            BorderBrush = CardBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12)
        };
        var actionStack = new StackPanel();
        actionBox.Child = actionStack;
        Grid.SetColumn(actionBox, 1);
        deckGrid.Children.Add(actionBox);

        root.Children.Add(deckGrid);

        ScreenRow? selectedRouteRow = _section.Rows.Count > 0 ? _section.Rows[0] : null;
        var routeButtons = new List<(ScreenRow Row, Border ButtonWrapper)>();

        void UpdateSelectedRoute(ScreenRow targetRow)
        {
            selectedRouteRow = targetRow;
            var destName = targetRow.Cells.Count > 0 ? targetRow.Cells[0] : "Lân Thành";
            var parenIdx = destName.IndexOf('(');
            if (parenIdx > 0) destName = destName[..parenIdx].Trim();

            var days = 3;
            if (targetRow.Cells.Count > 1 && int.TryParse(targetRow.Cells[1], out var d)) days = d;

            var fee = 10;
            if (targetRow.Cells.Count > 2 && int.TryParse(targetRow.Cells[2], out var f)) fee = f;

            var held = 0;
            if (targetRow.Cells.Count > 3 && int.TryParse(targetRow.Cells[3], out var h)) held = h;

            trailCanvas.SetRoute(_currentCityName, destName, days, Math.Min(35, days * 7), "Xa Mã Tiêu Đoàn");

            foreach (var item in routeButtons)
            {
                var isSel = ReferenceEquals(item.Row, targetRow);
                item.ButtonWrapper.BorderBrush = isSel ? AccentGold : CardBorder;
                item.ButtonWrapper.Background = isSel
                    ? Freeze(new SolidColorBrush(Color.FromArgb(0x35, 0xC4, 0xA0, 0x62)))
                    : CardBg;
            }

            // Render Action Box
            actionStack.Children.Clear();

            var destTitle = new TextBlock
            {
                Text = $"{_currentCityName} ➔ {destName}",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = TextMain,
                Margin = new Thickness(0, 0, 0, 6)
            };
            actionStack.Children.Add(destTitle);

            var metaStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            metaStack.Children.Add(new GameBadge($"{days} ngày đường", BadgeVariant.Default));
            metaStack.Children.Add(new Border { Width = 6 });
            metaStack.Children.Add(new GameBadge($"Cước {fee}q/đv", BadgeVariant.Gain));
            actionStack.Children.Add(metaStack);

            var bagInfo = new TextBlock
            {
                Text = $"Hàng trong túi có thể gửi: {held} đơn vị",
                FontSize = 10.5,
                Foreground = TextMuted,
                Margin = new Thickness(0, 0, 0, 10)
            };
            actionStack.Children.Add(bagInfo);

            foreach (var cmd in targetRow.Commands)
            {
                var isPrimary = cmd.Kind == CommandKind.Primary || cmd.Id.StartsWith("send_escort", StringComparison.OrdinalIgnoreCase);
                var btn = new GameButton(
                    cmd.Label,
                    isPrimary ? GameButtonVariant.Primary : GameButtonVariant.Secondary,
                    isCompact: true)
                {
                    Margin = new Thickness(0, 0, 0, 6),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                btn.Click += (_, _) => _commandInvoker?.Invoke(cmd.Id);
                actionStack.Children.Add(btn);
            }

            _rowInspector?.Invoke(_section, targetRow);
        }

        // Build route list items
        foreach (var r in _section.Rows)
        {
            var rName = r.Cells.Count > 0 ? r.Cells[0] : "Lân Thành";
            var rDays = r.Cells.Count > 1 ? r.Cells[1] : "3";
            var rFee = r.Cells.Count > 2 ? r.Cells[2] : "10";

            var rItem = new Border
            {
                Background = CardBg,
                BorderBrush = CardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 6),
                Cursor = Cursors.Hand
            };

            var rDock = new DockPanel();
            rDock.Children.Add(new TextBlock
            {
                Text = rName,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = TextMain,
                VerticalAlignment = VerticalAlignment.Center
            });

            var rightTag = new TextBlock
            {
                Text = $"{rDays} ngày · {rFee}q/đv",
                FontFamily = MonoFont,
                FontSize = 11,
                Foreground = AccentGold,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(rightTag, Dock.Right);
            rDock.Children.Add(rightTag);
            rItem.Child = rDock;

            var captured = r;
            rItem.MouseLeftButtonUp += (_, _) => UpdateSelectedRoute(captured);

            routeButtons.Add((r, rItem));
            routesStack.Children.Add(rItem);
        }

        if (selectedRouteRow is not null)
        {
            UpdateSelectedRoute(selectedRouteRow);
        }

        return root;
    }
    #endregion

    #region 3. Contract Board
    private UIElement BuildContractBoard()
    {
        var root = new StackPanel();

        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
        header.Children.Add(new TextBlock
        {
            Text = "HỢP ĐỒNG ÁP TIÊU & ỦY THÁC VẬN CHUYỂN",
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = AccentGold,
            VerticalAlignment = VerticalAlignment.Center
        });
        root.Children.Add(header);

        var wrap = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var row in _section.Rows)
        {
            var guild = row.Cells.Count > 0 ? row.Cells[0] : "Hội Quán";
            var from = row.Cells.Count > 1 ? row.Cells[1] : "Nơi Nhận";
            var to = row.Cells.Count > 2 ? row.Cells[2] : "Nơi Giao";
            var item = row.Cells.Count > 3 ? row.Cells[3] : "Hàng Hóa";
            var reward = row.Cells.Count > 4 ? row.Cells[4] : "0";
            var deadline = row.Cells.Count > 5 ? row.Cells[5] : "—";
            var status = row.Cells.Count > 6 ? row.Cells[6] : "Chờ nhận";

            var card = new Border
            {
                Width = 280,
                Background = CardBg,
                BorderBrush = CardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 10, 10)
            };

            var stack = new StackPanel();

            var topRow = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            topRow.Children.Add(new TextBlock
            {
                Text = guild,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = AccentGold,
                VerticalAlignment = VerticalAlignment.Center
            });

            var statusBadge = new GameBadge(status, status.Contains("Đang", StringComparison.Ordinal) ? BadgeVariant.Warning : BadgeVariant.Gain);
            DockPanel.SetDock(statusBadge, Dock.Right);
            topRow.Children.Add(statusBadge);
            stack.Children.Add(topRow);

            var routeText = new TextBlock
            {
                Text = $"{from} ➔ {to}",
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = TextMain,
                Margin = new Thickness(0, 0, 0, 4)
            };
            stack.Children.Add(routeText);

            var itemText = new TextBlock
            {
                Text = $"Vật phẩm: {item} · Hạn chót: {deadline}",
                FontSize = 10,
                Foreground = TextMuted,
                Margin = new Thickness(0, 0, 0, 8)
            };
            stack.Children.Add(itemText);

            var rewardBox = new Border
            {
                Background = PanelBg,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 0, 8)
            };
            var rwDock = new DockPanel();
            rwDock.Children.Add(new TextBlock { Text = "Thù lao áp tiêu:", FontSize = 10, Foreground = TextMuted });
            var rwVal = new TextBlock
            {
                Text = $"{reward} quan",
                FontFamily = MonoFont,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = JadeGreen,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            DockPanel.SetDock(rwVal, Dock.Right);
            rwDock.Children.Add(rwVal);
            rewardBox.Child = rwDock;
            stack.Children.Add(rewardBox);

            foreach (var cmd in row.Commands)
            {
                var btn = new GameButton(cmd.Label, GameButtonVariant.Primary, isCompact: true)
                {
                    Margin = new Thickness(0, 2, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                btn.Click += (_, _) => _commandInvoker?.Invoke(cmd.Id);
                stack.Children.Add(btn);
            }

            card.Child = stack;
            wrap.Children.Add(card);
        }

        root.Children.Add(wrap);
        return root;
    }
    #endregion

    #region 4. Economic Warfare Boards (Thương Chiến Tam Quốc: Lũng Đoạn & Buôn Lậu)

    private UIElement BuildCorneredMarketBoard()
    {
        var root = new StackPanel();
        var wrap = new WrapPanel { Orientation = Orientation.Horizontal };

        foreach (var row in _section.Rows)
        {
            var city = row.Cells.Count > 0 ? row.Cells[0] : "Thành Trấn";
            var item = row.Cells.Count > 1 ? row.Cells[1] : "Mặt Hàng";
            var qty = row.Cells.Count > 2 ? row.Cells[2] : "0 kiện";
            var severity = row.Cells.Count > 3 ? row.Cells[3] : "⚠️ Độc quyền";
            var impact = row.Cells.Count > 4 ? row.Cells[4] : string.Empty;

            var card = new Border
            {
                Width = 285,
                Background = CardBg,
                BorderBrush = AccentGold,
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 10, 10),
                Cursor = Cursors.Hand
            };

            var sp = new StackPanel();
            var topDock = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            topDock.Children.Add(new TextBlock
            {
                Text = $"⚔️ {item} ({city})",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = AccentGold,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            var multBadge = new GameBadge("Giá x2.2", BadgeVariant.Gain);
            DockPanel.SetDock(multBadge, Dock.Right);
            topDock.Children.Add(multBadge);
            sp.Children.Add(topDock);

            sp.Children.Add(new TextBlock
            {
                Text = $"Quy mô gom vét: {qty} · {severity}",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = TextMain,
                Margin = new Thickness(0, 0, 0, 6)
            });

            sp.Children.Add(new TextBlock
            {
                Text = impact,
                FontSize = 10.5,
                Foreground = TextMuted,
                TextWrapping = TextWrapping.Wrap
            });

            card.Child = sp;
            card.MouseLeftButtonUp += (_, _) => _rowInspector?.Invoke(_section, row);
            wrap.Children.Add(card);
        }

        root.Children.Add(wrap);
        return root;
    }

    private UIElement BuildSmugglingConvoyBoard()
    {
        var root = new StackPanel();
        var wrap = new WrapPanel { Orientation = Orientation.Horizontal };

        foreach (var row in _section.Rows)
        {
            var dest = row.Cells.Count > 0 ? row.Cells[0] : "Tiền Tuyến";
            var item = row.Cells.Count > 1 ? row.Cells[1] : "Quân Nhu";
            var priceSpan = row.Cells.Count > 2 ? row.Cells[2] : "—";
            var profitMargin = row.Cells.Count > 3 ? row.Cells[3] : "+250%";
            var riskText = row.Cells.Count > 4 ? row.Cells[4] : "35%";
            var statusText = row.Cells.Count > 5 ? row.Cells[5] : string.Empty;
            var canSmuggle = row.Commands.Count > 0;

            var digits = new string(riskText.TakeWhile(char.IsDigit).ToArray());
            var riskPct = int.TryParse(digits, out var rVal) ? Math.Clamp(rVal, 5, 95) : 35;

            var card = new Border
            {
                Width = 290,
                Background = CardBg,
                BorderBrush = canSmuggle ? CinnabarRed : CardBorder,
                BorderThickness = new Thickness(canSmuggle ? 1.5 : 1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 10, 10),
                Cursor = Cursors.Hand
            };

            var sp = new StackPanel();
            var topDock = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            topDock.Children.Add(new TextBlock
            {
                Text = $"{dest} · {item}",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = TextMain,
                MaxWidth = 185,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            var profBadge = new GameBadge($"Lời {profitMargin}", BadgeVariant.Gain);
            DockPanel.SetDock(profBadge, Dock.Right);
            topDock.Children.Add(profBadge);
            sp.Children.Add(topDock);

            sp.Children.Add(new TextBlock
            {
                Text = $"Chênh lệch chợ đen: {priceSpan}",
                FontFamily = MonoFont,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = AccentGold,
                Margin = new Thickness(0, 0, 0, 6)
            });

            // Patrol Interception Risk Gauge
            var riskLabelDock = new DockPanel { Margin = new Thickness(0, 0, 0, 3) };
            riskLabelDock.Children.Add(new TextBlock
            {
                Text = "Rủi ro tuần binh chặn bắt:",
                FontSize = 10,
                Foreground = TextMuted
            });
            var riskRight = new TextBlock
            {
                Text = riskText,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = CinnabarRed,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            DockPanel.SetDock(riskRight, Dock.Right);
            riskLabelDock.Children.Add(riskRight);
            sp.Children.Add(riskLabelDock);

            var riskBar = new Grid { Height = 6, Margin = new Thickness(0, 0, 0, 8) };
            riskBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(riskPct, GridUnitType.Star) });
            riskBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100 - riskPct, GridUnitType.Star) });
            var rTrack = new Border { Background = PanelBg, CornerRadius = new CornerRadius(3) };
            Grid.SetColumnSpan(rTrack, 2);
            riskBar.Children.Add(rTrack);
            var rFill = new Border { Background = CinnabarRed, CornerRadius = new CornerRadius(3) };
            Grid.SetColumn(rFill, 0);
            riskBar.Children.Add(rFill);
            sp.Children.Add(riskBar);

            sp.Children.Add(new TextBlock
            {
                Text = statusText,
                FontSize = 10.5,
                Foreground = canSmuggle ? JadeGreen : TextMuted,
                Margin = new Thickness(0, 0, 0, 8)
            });

            if (canSmuggle)
            {
                foreach (var cmd in row.Commands)
                {
                    var btn = new GameButton(cmd.Label, GameButtonVariant.Primary, isCompact: true)
                    {
                        HorizontalAlignment = HorizontalAlignment.Stretch
                    };
                    var cid = cmd.Id;
                    btn.Click += (_, _) => _commandInvoker?.Invoke(cid);
                    sp.Children.Add(btn);
                }
            }

            card.Child = sp;
            card.MouseLeftButtonUp += (_, _) => _rowInspector?.Invoke(_section, row);
            wrap.Children.Add(card);
        }

        root.Children.Add(wrap);
        return root;
    }

    private UIElement BuildSmugglingLogBoard()
    {
        var root = new StackPanel();
        foreach (var row in _section.Rows)
        {
            var day = row.Cells.Count > 0 ? row.Cells[0] : "—";
            var route = row.Cells.Count > 1 ? row.Cells[1] : "—";
            var cargo = row.Cells.Count > 2 ? row.Cells[2] : "—";
            var outcome = row.Cells.Count > 3 ? row.Cells[3] : "—";
            var narrative = row.Cells.Count > 4 ? row.Cells[4] : string.Empty;
            var isCaught = outcome.Contains("❌");

            var card = new Border
            {
                Background = CardBg,
                BorderBrush = isCaught ? CinnabarRed : JadeGreen,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 6)
            };

            var sp = new StackPanel();
            var topDock = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
            topDock.Children.Add(new TextBlock
            {
                Text = $"{day} · {route} ({cargo})",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = TextMain
            });
            var badge = new GameBadge(outcome, isCaught ? BadgeVariant.Warning : BadgeVariant.Gain);
            DockPanel.SetDock(badge, Dock.Right);
            topDock.Children.Add(badge);
            sp.Children.Add(topDock);

            sp.Children.Add(new TextBlock
            {
                Text = narrative,
                FontSize = 11,
                Foreground = TextMuted,
                TextWrapping = TextWrapping.Wrap
            });

            card.Child = sp;
            root.Children.Add(card);
        }
        return root;
    }

    #endregion

    private static Brush Freeze(SolidColorBrush b)
    {
        b.Freeze();
        return b;
    }
}
