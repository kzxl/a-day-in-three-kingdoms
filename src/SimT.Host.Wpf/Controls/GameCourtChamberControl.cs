using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SimT.Contracts.Ui;

namespace SimT.Host.Wpf.Controls;

/// <summary>
/// Imperial Court Chamber control visualizing the Nine-Rank Official Hierarchy (Cửu Phẩm Quan Chế),
/// Feudal Warlord Factions, and Provincial Regional Governance policies.
/// </summary>
public sealed class GameCourtChamberControl : Grid
{
    private static readonly Brush PanelBg = Freeze(new SolidColorBrush(Color.FromRgb(0x16, 0x13, 0x11)));
    private static readonly Brush PanelBorder = Freeze(new SolidColorBrush(Color.FromRgb(0x3E, 0x2A, 0x20)));
    private static readonly Brush CardBg = Freeze(new SolidColorBrush(Color.FromRgb(0x1E, 0x17, 0x13)));
    private static readonly Brush CardHoverBg = Freeze(new SolidColorBrush(Color.FromRgb(0x28, 0x20, 0x18)));
    private static readonly Brush ActiveOfficeBg = Freeze(new SolidColorBrush(Color.FromRgb(0x33, 0x26, 0x18)));
    private static readonly Brush GoldBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xD4, 0xAF, 0x37)));
    private static readonly Brush GoldDimBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x8A, 0x72, 0x35)));
    private static readonly Brush TextPrimaryBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xF0, 0xE4, 0xCE)));
    private static readonly Brush TextMutedBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x9E, 0x8F, 0x7C)));
    private static readonly Brush TextSuccessBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x5E, 0xA8, 0x72)));
    private static readonly Brush TextWarningBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xDF, 0x8A, 0x48)));

    private readonly ScreenSection _section;
    private readonly Action<string> _onCommand;
    private readonly Action<ScreenSection, ScreenRow>? _onInspect;

    public GameCourtChamberControl(
        ScreenSection section,
        Action<string> onCommand,
        Action<ScreenSection, ScreenRow>? onInspect = null)
    {
        _section = section;
        _onCommand = onCommand;
        _onInspect = onInspect;

        Margin = new Thickness(0, 4, 0, 4);

        var heading = section.Heading;
        if (heading.Contains("Quan Chế", StringComparison.OrdinalIgnoreCase) ||
            heading.Contains("Chức vụ", StringComparison.OrdinalIgnoreCase))
        {
            BuildNineRankOfficesView();
        }
        else if (heading.Contains("Thế Lực", StringComparison.OrdinalIgnoreCase) ||
                 heading.Contains("Chư Hầu", StringComparison.OrdinalIgnoreCase))
        {
            BuildFeudalFactionsView();
        }
        else
        {
            BuildRegionalGovernanceView();
        }
    }

    #region Nine-Rank Imperial Offices (Cửu Phẩm Quan Chế)

    private void BuildNineRankOfficesView()
    {
        var mainStack = new StackPanel();
        Children.Add(mainStack);

        var banner = new Border
        {
            Background = Freeze(new SolidColorBrush(Color.FromArgb(0x40, 0x8A, 0x72, 0x35))),
            BorderBrush = GoldDimBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 0, 10)
        };
        var bannerDock = new DockPanel();
        var bannerTitle = new TextBlock
        {
            Text = "📜 CỬU PHẨM QUAN CHẾ TRIỀU ĐÌNH — PHẨM HÀM & BỔNG LỘC",
            FontWeight = FontWeights.Bold,
            FontSize = 13,
            Foreground = GoldBrush
        };
        DockPanel.SetDock(bannerTitle, Dock.Left);
        bannerDock.Children.Add(bannerTitle);

        var subText = new TextBlock
        {
            Text = "Tích lũy uy tín phụng sự xã tắc để thăng phẩm",
            FontSize = 11,
            Foreground = TextMutedBrush,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        DockPanel.SetDock(subText, Dock.Right);
        bannerDock.Children.Add(subText);
        banner.Child = bannerDock;
        mainStack.Children.Add(banner);

        var wrap = new WrapPanel { Orientation = Orientation.Horizontal };
        mainStack.Children.Add(wrap);

        foreach (var row in _section.Rows)
        {
            wrap.Children.Add(BuildOfficeCard(row));
        }
    }

    private UIElement BuildOfficeCard(ScreenRow row)
    {
        var isHolding = row.Cells.Count > 4 && row.Cells[4].Contains("Đang tại chức");
        var isEligible = row.Cells.Count > 4 && row.Cells[4].Contains("Đủ điều kiện");

        var card = new Border
        {
            Background = isHolding ? ActiveOfficeBg : CardBg,
            BorderBrush = isHolding ? GoldBrush : (isEligible ? TextSuccessBrush : PanelBorder),
            BorderThickness = new Thickness(isHolding ? 1.8 : 1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12),
            Margin = new Thickness(6),
            Width = 270,
            Cursor = Cursors.Hand
        };

        var stack = new StackPanel();
        card.Child = stack;

        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        var title = row.Cells.Count > 0 ? row.Cells[0] : (row.Title ?? "Chức Quan");
        var nameBlock = new TextBlock
        {
            Text = title,
            FontWeight = FontWeights.Bold,
            FontSize = 13.5,
            Foreground = isHolding ? GoldBrush : TextPrimaryBrush,
            MaxWidth = 175,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        DockPanel.SetDock(nameBlock, Dock.Left);
        header.Children.Add(nameBlock);

        var rank = row.Cells.Count > 1 ? row.Cells[1] : "Phẩm 9";
        var rankBadge = new GameBadge(rank, isHolding ? BadgeVariant.Gain : BadgeVariant.Default);
        DockPanel.SetDock(rankBadge, Dock.Right);
        header.Children.Add(rankBadge);
        stack.Children.Add(header);

        var stipend = row.Cells.Count > 2 ? row.Cells[2] : "0 Quan";
        var repReq = row.Cells.Count > 3 ? row.Cells[3] : "0";
        var status = row.Cells.Count > 4 ? row.Cells[4] : "—";

        var infoBox = new Border
        {
            Background = Freeze(new SolidColorBrush(Color.FromArgb(0x30, 0x12, 0x0E, 0x0A))),
            BorderBrush = PanelBorder,
            BorderThickness = new Thickness(0.8),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(0, 0, 0, 8)
        };
        var s = new StackPanel();
        s.Children.Add(new TextBlock
        {
            Text = $"💰 Bổng lộc: {stipend} / tháng",
            FontSize = 11,
            Foreground = GoldBrush
        });
        s.Children.Add(new TextBlock
        {
            Text = $"🎖️ Uy tín cần: {repReq} điểm",
            FontSize = 10.5,
            Foreground = TextMutedBrush,
            Margin = new Thickness(0, 2, 0, 0)
        });
        infoBox.Child = s;
        stack.Children.Add(infoBox);

        var statusBlock = new TextBlock
        {
            Text = isHolding ? "👑 Đang đảm nhiệm ấn tín" : status,
            FontSize = 11,
            FontWeight = isHolding ? FontWeights.Bold : FontWeights.Normal,
            Foreground = isHolding ? GoldBrush : (isEligible ? TextSuccessBrush : TextMutedBrush),
            Margin = new Thickness(0, 0, 0, 6)
        };
        stack.Children.Add(statusBlock);

        if (row.Commands.Count > 0)
        {
            var cmd = row.Commands[0];
            var btn = new GameButton(cmd.Label, GameButtonVariant.Primary, isCompact: true)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            var cid = cmd.Id;
            btn.Click += (_, _) => _onCommand(cid);
            stack.Children.Add(btn);
        }

        card.MouseEnter += (_, _) => { if (!isHolding) card.Background = CardHoverBg; };
        card.MouseLeave += (_, _) => { if (!isHolding) card.Background = CardBg; };
        card.MouseLeftButtonUp += (_, _) => _onInspect?.Invoke(_section, row);

        return card;
    }

    #endregion

    #region Feudal Factions (Thế Lực Chư Hầu Tam Quốc)

    private void BuildFeudalFactionsView()
    {
        var mainStack = new StackPanel();
        Children.Add(mainStack);

        var wrap = new WrapPanel { Orientation = Orientation.Horizontal };
        mainStack.Children.Add(wrap);

        foreach (var row in _section.Rows)
        {
            wrap.Children.Add(BuildFactionCard(row));
        }
    }

    private UIElement BuildFactionCard(ScreenRow row)
    {
        var isAllegiant = row.Cells.Count > 5 && row.Cells[5].Contains("ĐANG PHỤNG SỰ");

        var card = new Border
        {
            Background = isAllegiant ? ActiveOfficeBg : CardBg,
            BorderBrush = isAllegiant ? GoldBrush : PanelBorder,
            BorderThickness = new Thickness(isAllegiant ? 1.8 : 1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12),
            Margin = new Thickness(6),
            Width = 280,
            Cursor = Cursors.Hand
        };

        var stack = new StackPanel();
        card.Child = stack;

        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        var name = row.Cells.Count > 0 ? row.Cells[0] : (row.Title ?? "Chư Hầu");
        var titleBlock = new TextBlock
        {
            Text = $"🏛️ {name}",
            FontWeight = FontWeights.Bold,
            FontSize = 13.5,
            Foreground = isAllegiant ? GoldBrush : TextPrimaryBrush,
            MaxWidth = 180,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        DockPanel.SetDock(titleBlock, Dock.Left);
        header.Children.Add(titleBlock);

        var capital = row.Cells.Count > 1 ? row.Cells[1] : "Bản doanh";
        var capBadge = new GameBadge(capital, BadgeVariant.Default);
        DockPanel.SetDock(capBadge, Dock.Right);
        header.Children.Add(capBadge);
        stack.Children.Add(header);

        var ideal = row.Cells.Count > 2 ? row.Cells[2] : "Bá nghiệp";
        var troops = row.Cells.Count > 3 ? row.Cells[3] : "—";
        var treasury = row.Cells.Count > 4 ? row.Cells[4] : "—";

        var s = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
        s.Children.Add(new TextBlock
        {
            Text = $"Lý tưởng: \"{ideal}\"",
            FontSize = 11,
            FontStyle = FontStyles.Italic,
            Foreground = TextMutedBrush,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        s.Children.Add(new TextBlock
        {
            Text = $"⚔️ Binh lực: {troops} · 💰 Quốc khố: {treasury}",
            FontSize = 11,
            Foreground = GoldDimBrush,
            Margin = new Thickness(0, 3, 0, 0)
        });
        stack.Children.Add(s);

        if (row.Commands.Count > 0)
        {
            var btnPanel = new WrapPanel { Orientation = Orientation.Horizontal };
            foreach (var cmd in row.Commands)
            {
                var isPrimary = cmd.Id.StartsWith("join_faction:", StringComparison.Ordinal);
                var btn = new GameButton(cmd.Label, isPrimary ? GameButtonVariant.Primary : GameButtonVariant.Secondary, isCompact: true)
                {
                    Margin = new Thickness(0, 0, 4, 4)
                };
                var cid = cmd.Id;
                btn.Click += (_, _) => _onCommand(cid);
                btnPanel.Children.Add(btn);
            }
            stack.Children.Add(btnPanel);
        }

        card.MouseEnter += (_, _) => { if (!isAllegiant) card.Background = CardHoverBg; };
        card.MouseLeave += (_, _) => { if (!isAllegiant) card.Background = CardBg; };
        card.MouseLeftButtonUp += (_, _) => _onInspect?.Invoke(_section, row);

        return card;
    }

    #endregion

    #region Regional Governance (Chính Sự Châu Quận)

    private void BuildRegionalGovernanceView()
    {
        var mainStack = new StackPanel();
        Children.Add(mainStack);

        var wrap = new WrapPanel { Orientation = Orientation.Horizontal };
        mainStack.Children.Add(wrap);

        foreach (var row in _section.Rows)
        {
            var card = new Border
            {
                Background = CardBg,
                BorderBrush = PanelBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(12),
                Margin = new Thickness(6),
                Width = 280,
                Cursor = Cursors.Hand
            };
            var s = new StackPanel();
            card.Child = s;

            var title = row.Cells.Count > 0 ? row.Cells[0] : (row.Title ?? "Thành Trì");
            s.Children.Add(new TextBlock
            {
                Text = $"🏰 {title}",
                FontWeight = FontWeights.Bold,
                FontSize = 13.5,
                Foreground = TextPrimaryBrush,
                Margin = new Thickness(0, 0, 0, 6)
            });

            var faction = row.Cells.Count > 1 ? row.Cells[1] : "—";
            var unrest = row.Cells.Count > 2 ? row.Cells[2] : "0";
            var defense = row.Cells.Count > 3 ? row.Cells[3] : "0";

            s.Children.Add(new TextBlock
            {
                Text = $"Trấn thủ: {faction}",
                FontSize = 11,
                Foreground = GoldBrush
            });
            s.Children.Add(new TextBlock
            {
                Text = $"Bất ổn: {unrest} · Phòng thủ: {defense}",
                FontSize = 10.5,
                Foreground = unrest.Contains("Loạn") ? TextWarningBrush : TextMutedBrush,
                Margin = new Thickness(0, 2, 0, 6)
            });

            if (row.Commands.Count > 0)
            {
                var bp = new WrapPanel();
                foreach (var cmd in row.Commands)
                {
                    var btn = new GameButton(cmd.Label, cmd.Kind == CommandKind.Primary ? GameButtonVariant.Primary : GameButtonVariant.Secondary, isCompact: true)
                    {
                        Margin = new Thickness(0, 0, 4, 4)
                    };
                    var cid = cmd.Id;
                    btn.Click += (_, _) => _onCommand(cid);
                    bp.Children.Add(btn);
                }
                s.Children.Add(bp);
            }

            card.MouseEnter += (_, _) => card.Background = CardHoverBg;
            card.MouseLeave += (_, _) => card.Background = CardBg;
            card.MouseLeftButtonUp += (_, _) => _onInspect?.Invoke(_section, row);

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
