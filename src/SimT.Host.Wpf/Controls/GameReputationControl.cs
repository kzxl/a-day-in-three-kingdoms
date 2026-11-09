using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SimT.Contracts.Ui;

namespace SimT.Host.Wpf.Controls;

/// <summary>
/// Reputation Hall of Fame Control visualizing the Nine-Tier Renown Ladder,
/// 5 Strategic Regions standing, and the Pan-China Hero Leaderboard (Bảng Phong Thần).
/// </summary>
public sealed class GameReputationControl : Grid
{
    private static readonly Brush PanelBg = Freeze(new SolidColorBrush(Color.FromRgb(0x16, 0x13, 0x11)));
    private static readonly Brush PanelBorder = Freeze(new SolidColorBrush(Color.FromRgb(0x3E, 0x2A, 0x20)));
    private static readonly Brush CardBg = Freeze(new SolidColorBrush(Color.FromRgb(0x1E, 0x17, 0x13)));
    private static readonly Brush CardHoverBg = Freeze(new SolidColorBrush(Color.FromRgb(0x28, 0x20, 0x18)));
    private static readonly Brush ActiveCardBg = Freeze(new SolidColorBrush(Color.FromRgb(0x34, 0x27, 0x18)));
    private static readonly Brush GoldBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xD4, 0xAF, 0x37)));
    private static readonly Brush GoldDimBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x8A, 0x72, 0x35)));
    private static readonly Brush GreenBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x5E, 0xA8, 0x72)));
    private static readonly Brush TextPrimaryBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xF0, 0xE4, 0xCE)));
    private static readonly Brush TextMutedBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x9E, 0x8F, 0x7C)));

    private readonly ScreenSection _section;
    private readonly Action<string> _onCommand;
    private readonly Action<ScreenSection, ScreenRow>? _onInspect;

    public GameReputationControl(
        ScreenSection section,
        Action<string> onCommand,
        Action<ScreenSection, ScreenRow>? onInspect = null)
    {
        _section = section;
        _onCommand = onCommand;
        _onInspect = onInspect;

        Margin = new Thickness(0, 4, 0, 4);

        var heading = section.Heading;
        if (heading.Contains("CẤP BẬC", StringComparison.OrdinalIgnoreCase) ||
            heading.Contains("ĐẶC QUYỀN", StringComparison.OrdinalIgnoreCase))
        {
            BuildTierLadderView();
        }
        else if (heading.Contains("KHU VỰC", StringComparison.OrdinalIgnoreCase))
        {
            BuildRegionalInfluenceView();
        }
        else if (heading.Contains("PHONG THẦN", StringComparison.OrdinalIgnoreCase) ||
                 heading.Contains("XẾP HẠNG", StringComparison.OrdinalIgnoreCase))
        {
            BuildLeaderboardView();
        }
        else
        {
            BuildDeedsLedgerView();
        }
    }

    #region Tier Ladder View (Cấp Bậc Danh Vọng)

    private void BuildTierLadderView()
    {
        var wrap = new WrapPanel { Orientation = Orientation.Horizontal };
        Children.Add(wrap);

        foreach (var row in _section.Rows)
        {
            var title = row.Cells.Count > 0 ? row.Cells[0] : (row.Title ?? "Tước Vị");
            var name = row.Cells.Count > 1 ? row.Cells[1] : "";
            var reqFame = row.Cells.Count > 2 ? row.Cells[2] : "0";
            var status = row.Cells.Count > 3 ? row.Cells[3] : "";
            var perk = row.Cells.Count > 4 ? row.Cells[4] : "";

            var isCurrent = status.Contains("Đang giữ") || status.Contains("👑") || title.StartsWith("★");
            var isUnlocked = isCurrent || status.Contains("Đã mở");

            var card = new Border
            {
                Background = isCurrent ? ActiveCardBg : CardBg,
                BorderBrush = isCurrent ? GoldBrush : (isUnlocked ? GreenBrush : PanelBorder),
                BorderThickness = new Thickness(isCurrent ? 1.8 : 1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Margin = new Thickness(6),
                Width = 320,
                Cursor = Cursors.Hand
            };

            var stack = new StackPanel();
            card.Child = stack;

            var header = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            var titleBlock = new TextBlock
            {
                Text = title,
                FontWeight = FontWeights.Bold,
                FontSize = 13.5,
                Foreground = isCurrent ? GoldBrush : TextPrimaryBrush,
                MaxWidth = 220,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            DockPanel.SetDock(titleBlock, Dock.Left);
            header.Children.Add(titleBlock);

            var statusBadge = new GameBadge(isCurrent ? "👑 Hiện Tại" : (isUnlocked ? "✅ Đã Đạt" : "🔒 Khóa"),
                isCurrent ? BadgeVariant.Gain : (isUnlocked ? BadgeVariant.Default : BadgeVariant.Loss));
            DockPanel.SetDock(statusBadge, Dock.Right);
            header.Children.Add(statusBadge);
            stack.Children.Add(header);

            if (!string.IsNullOrWhiteSpace(name))
            {
                stack.Children.Add(new TextBlock
                {
                    Text = $"Danh xưng: \"{name}\"",
                    FontSize = 11,
                    FontStyle = FontStyles.Italic,
                    Foreground = GoldDimBrush,
                    Margin = new Thickness(0, 0, 0, 4)
                });
            }

            stack.Children.Add(new TextBlock
            {
                Text = $"Uy danh cần: {reqFame} điểm",
                FontSize = 10.5,
                Foreground = TextMutedBrush,
                Margin = new Thickness(0, 0, 0, 4)
            });

            stack.Children.Add(new TextBlock
            {
                Text = perk,
                FontSize = 11,
                Foreground = TextPrimaryBrush,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 4)
            });

            if (!string.IsNullOrWhiteSpace(status))
            {
                stack.Children.Add(new TextBlock
                {
                    Text = status,
                    FontSize = 10.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = isCurrent ? GoldBrush : (isUnlocked ? GreenBrush : TextMutedBrush),
                    Margin = new Thickness(0, 4, 0, 0)
                });
            }

            card.MouseEnter += (_, _) => { if (!isCurrent) card.Background = CardHoverBg; };
            card.MouseLeave += (_, _) => { if (!isCurrent) card.Background = CardBg; };
            card.MouseLeftButtonUp += (_, _) => _onInspect?.Invoke(_section, row);

            wrap.Children.Add(card);
        }
    }

    #endregion

    #region Regional Influence View (5 Đại Khu Vực)

    private void BuildRegionalInfluenceView()
    {
        var wrap = new WrapPanel { Orientation = Orientation.Horizontal };
        Children.Add(wrap);

        foreach (var row in _section.Rows)
        {
            var card = new Border
            {
                Background = CardBg,
                BorderBrush = PanelBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Margin = new Thickness(6),
                Width = 270,
                Cursor = Cursors.Hand
            };

            var stack = new StackPanel();
            card.Child = stack;

            var region = row.Cells.Count > 0 ? row.Cells[0] : (row.Title ?? "Khu Vực");
            var fame = row.Cells.Count > 1 ? row.Cells[1] : "0";
            var tier = row.Cells.Count > 2 ? row.Cells[2] : "Vô danh";
            var summary = row.Cells.Count > 3 ? row.Cells[3] : "";

            var header = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            var regBlock = new TextBlock
            {
                Text = $"🗺️ {region}",
                FontWeight = FontWeights.Bold,
                FontSize = 13.5,
                Foreground = TextPrimaryBrush,
                MaxWidth = 175,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            DockPanel.SetDock(regBlock, Dock.Left);
            header.Children.Add(regBlock);

            var fameBadge = new GameBadge(fame, BadgeVariant.Default);
            DockPanel.SetDock(fameBadge, Dock.Right);
            header.Children.Add(fameBadge);
            stack.Children.Add(header);

            stack.Children.Add(new TextBlock
            {
                Text = $"Uy thế: {tier}",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = GoldBrush,
                Margin = new Thickness(0, 0, 0, 4)
            });

            stack.Children.Add(new TextBlock
            {
                Text = summary,
                FontSize = 10.5,
                Foreground = TextMutedBrush,
                TextWrapping = TextWrapping.Wrap
            });

            card.MouseEnter += (_, _) => card.Background = CardHoverBg;
            card.MouseLeave += (_, _) => card.Background = CardBg;
            card.MouseLeftButtonUp += (_, _) => _onInspect?.Invoke(_section, row);

            wrap.Children.Add(card);
        }
    }

    #endregion

    #region Leaderboard View (Bảng Phong Thần)

    private void BuildLeaderboardView()
    {
        var wrap = new WrapPanel { Orientation = Orientation.Horizontal };
        Children.Add(wrap);

        foreach (var row in _section.Rows)
        {
            var isPlayer = row.Cells.Count > 1 && row.Cells[1].Contains("BẢN THÂN");

            var card = new Border
            {
                Background = isPlayer ? ActiveCardBg : CardBg,
                BorderBrush = isPlayer ? GoldBrush : PanelBorder,
                BorderThickness = new Thickness(isPlayer ? 1.8 : 1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Margin = new Thickness(6),
                Width = 270,
                Cursor = Cursors.Hand
            };

            var stack = new StackPanel();
            card.Child = stack;

            var rank = row.Cells.Count > 0 ? row.Cells[0] : "#1";
            var name = row.Cells.Count > 1 ? row.Cells[1] : (row.Title ?? "Hào kiệt");
            var role = row.Cells.Count > 2 ? row.Cells[2] : "";
            var loc = row.Cells.Count > 3 ? row.Cells[3] : "";
            var fame = row.Cells.Count > 4 ? row.Cells[4] : "0";
            var renown = row.Cells.Count > 5 ? row.Cells[5] : "";

            var header = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            var rankBlock = new TextBlock
            {
                Text = rank,
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Foreground = rank.Contains("#1") || rank.Contains("#2") || rank.Contains("#3") ? GoldBrush : TextMutedBrush,
                Margin = new Thickness(0, 0, 8, 0)
            };
            DockPanel.SetDock(rankBlock, Dock.Left);
            header.Children.Add(rankBlock);

            var nameBlock = new TextBlock
            {
                Text = name,
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                Foreground = isPlayer ? GoldBrush : TextPrimaryBrush,
                MaxWidth = 160,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            DockPanel.SetDock(nameBlock, Dock.Left);
            header.Children.Add(nameBlock);

            var fameBadge = new GameBadge($"{fame} Điểm", isPlayer ? BadgeVariant.Gain : BadgeVariant.Default);
            DockPanel.SetDock(fameBadge, Dock.Right);
            header.Children.Add(fameBadge);
            stack.Children.Add(header);

            stack.Children.Add(new TextBlock
            {
                Text = $"{role} · {loc}",
                FontSize = 10.5,
                Foreground = TextMutedBrush,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 0, 2)
            });

            stack.Children.Add(new TextBlock
            {
                Text = $"Danh vị: {renown}",
                FontSize = 10.5,
                Foreground = GoldDimBrush
            });

            card.MouseEnter += (_, _) => { if (!isPlayer) card.Background = CardHoverBg; };
            card.MouseLeave += (_, _) => { if (!isPlayer) card.Background = CardBg; };
            card.MouseLeftButtonUp += (_, _) => _onInspect?.Invoke(_section, row);

            wrap.Children.Add(card);
        }
    }

    #endregion

    #region Deeds Ledger View (Nhật Ký Chiến Tích)

    private void BuildDeedsLedgerView()
    {
        var wrap = new WrapPanel { Orientation = Orientation.Horizontal };
        Children.Add(wrap);

        foreach (var row in _section.Rows)
        {
            var card = new Border
            {
                Background = CardBg,
                BorderBrush = PanelBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(10),
                Margin = new Thickness(4),
                Width = 275
            };

            var stack = new StackPanel();
            card.Child = stack;

            var date = row.Cells.Count > 0 ? row.Cells[0] : "";
            var cat = row.Cells.Count > 1 ? row.Cells[1] : "";
            var delta = row.Cells.Count > 2 ? row.Cells[2] : "";
            var title = row.Cells.Count > 3 ? row.Cells[3] : "";
            var desc = row.Cells.Count > 4 ? row.Cells[4] : "";

            var header = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
            var catBlock = new TextBlock
            {
                Text = $"📜 {title}",
                FontWeight = FontWeights.Bold,
                FontSize = 12.5,
                Foreground = TextPrimaryBrush,
                MaxWidth = 180,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            DockPanel.SetDock(catBlock, Dock.Left);
            header.Children.Add(catBlock);

            var deltaBadge = new GameBadge(delta, BadgeVariant.Gain);
            DockPanel.SetDock(deltaBadge, Dock.Right);
            header.Children.Add(deltaBadge);
            stack.Children.Add(header);

            stack.Children.Add(new TextBlock
            {
                Text = $"{date} · {cat}",
                FontSize = 10,
                Foreground = TextMutedBrush,
                Margin = new Thickness(0, 0, 0, 4)
            });

            stack.Children.Add(new TextBlock
            {
                Text = desc,
                FontSize = 10.5,
                Foreground = TextPrimaryBrush,
                TextWrapping = TextWrapping.Wrap
            });

            wrap.Children.Add(card);
        }
    }

    #endregion

    private static T Freeze<T>(T freezable) where T : Freezable
    {
        if (freezable.CanFreeze) freezable.Freeze();
        return freezable;
    }
}
