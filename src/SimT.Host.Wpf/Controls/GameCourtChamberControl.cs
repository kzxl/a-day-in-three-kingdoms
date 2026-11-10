using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
    private static readonly Brush TextDangerBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xD9, 0x53, 0x4F)));
    private static readonly Brush DangerBorderBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x9E, 0x35, 0x2A)));
    private static readonly Brush DangerBgBrush = Freeze(new SolidColorBrush(Color.FromArgb(0x38, 0x7A, 0x1C, 0x18)));
    private static readonly Brush TrackBgBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x2A, 0x1F, 0x16)));

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
        else if (heading.Contains("Phong Ấp", StringComparison.OrdinalIgnoreCase) ||
                 heading.Contains("Thái Thú", StringComparison.OrdinalIgnoreCase))
        {
            BuildGovernorFiefView();
        }
        else if (heading.Contains("Cây To Đón Gió", StringComparison.OrdinalIgnoreCase) ||
                 heading.Contains("Dòm Ngó", StringComparison.OrdinalIgnoreCase))
        {
            BuildWarlordScrutinyView();
        }
        else if (heading.Contains("Sóng Ngầm", StringComparison.OrdinalIgnoreCase) ||
                 heading.Contains("Tam Đại Thế Lực", StringComparison.OrdinalIgnoreCase))
        {
            BuildCourtFactionsTriadView();
        }
        else if (heading.Contains("Ngự Sử Đài", StringComparison.OrdinalIgnoreCase) ||
                 heading.Contains("Thanh Sát", StringComparison.OrdinalIgnoreCase))
        {
            BuildImperialInspectionView();
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

    #region Governor Fiefdom View (Thái Thú Phủ & Quản Trị Phong Ấp)

    private void BuildGovernorFiefView()
    {
        var mainStack = new StackPanel();
        Children.Add(mainStack);

        var banner = new Border
        {
            Background = Freeze(new SolidColorBrush(Color.FromArgb(0x40, 0xD4, 0xAF, 0x37))),
            BorderBrush = GoldBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 0, 10)
        };
        var bannerDock = new DockPanel();
        var bannerTitle = new TextBlock
        {
            Text = "🏛️ THÁI THÚ PHỦ · QUẢN TRỊ PHONG ẤP & TỰ ĐỘNG CHÁNH VỤ",
            FontWeight = FontWeights.Bold,
            FontSize = 13,
            Foreground = GoldBrush
        };
        DockPanel.SetDock(bannerTitle, Dock.Left);
        bannerDock.Children.Add(bannerTitle);

        var subText = new TextBlock
        {
            Text = "Ban bệ Trưởng Thừa (Văn) & Đô Úy (Võ) tự trị hàng tháng theo phương châm chánh sự",
            FontSize = 11,
            Foreground = TextMutedBrush,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        DockPanel.SetDock(subText, Dock.Right);
        bannerDock.Children.Add(subText);
        banner.Child = bannerDock;
        mainStack.Children.Add(banner);

        foreach (var row in _section.Rows)
        {
            var card = new Border
            {
                Background = ActiveOfficeBg,
                BorderBrush = GoldBrush,
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 12)
            };

            var root = new StackPanel();
            card.Child = root;

            var head = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
            var cityName = row.Cells.Count > 0 ? row.Cells[0] : "Phong Ấp";
            var cityTitle = new TextBlock
            {
                Text = cityName,
                FontWeight = FontWeights.Bold,
                FontSize = 15,
                Foreground = GoldBrush
            };
            DockPanel.SetDock(cityTitle, Dock.Left);
            head.Children.Add(cityTitle);

            var treasury = row.Cells.Count > 8 ? row.Cells[8] : row.Cells.Count > 5 ? row.Cells[5] : "500 Quan";
            var treasuryBadge = new GameBadge($"Ngân Khố Nha Môn: {treasury}", BadgeVariant.Gain);
            DockPanel.SetDock(treasuryBadge, Dock.Right);
            head.Children.Add(treasuryBadge);
            root.Children.Add(head);

            // Demographic & Garrison Bar
            if (row.Cells.Count > 1)
            {
                var demoPanel = new Border
                {
                    Background = Freeze(new SolidColorBrush(Color.FromArgb(0x35, 0x1A, 0x14, 0x10))),
                    BorderBrush = PanelBorder,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(10, 6, 10, 6),
                    Margin = new Thickness(0, 0, 0, 8)
                };
                var demoDock = new DockPanel();
                var demoText = new TextBlock
                {
                    Text = row.Cells[1],
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = TextPrimaryBrush
                };
                DockPanel.SetDock(demoText, Dock.Left);
                demoDock.Children.Add(demoText);

                if (row.Cells.Count > 7)
                {
                    var directiveBadge = new GameBadge(row.Cells[7], BadgeVariant.Warning);
                    DockPanel.SetDock(directiveBadge, Dock.Right);
                    demoDock.Children.Add(directiveBadge);
                }
                demoPanel.Child = demoDock;
                root.Children.Add(demoPanel);
            }

            // Aide Panel
            if (row.Cells.Count > 6)
            {
                var aidePanel = new Border
                {
                    Background = Freeze(new SolidColorBrush(Color.FromArgb(0x28, 0xD4, 0xAF, 0x37))),
                    BorderBrush = Freeze(new SolidColorBrush(Color.FromArgb(0x55, 0xD4, 0xAF, 0x37))),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(10, 6, 10, 6),
                    Margin = new Thickness(0, 0, 0, 10)
                };
                var aideSp = new StackPanel { Orientation = Orientation.Horizontal };
                aideSp.Children.Add(new TextBlock
                {
                    Text = "📜 Ban Bệ Phụ Tá: ",
                    FontSize = 11.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = GoldBrush,
                    Margin = new Thickness(0, 0, 8, 0)
                });
                aideSp.Children.Add(new TextBlock
                {
                    Text = row.Cells[6],
                    FontSize = 11.5,
                    Foreground = TextPrimaryBrush
                });
                aidePanel.Child = aideSp;
                root.Children.Add(aidePanel);
            }

            var grid = new UniformGrid { Columns = 4, Margin = new Thickness(0, 0, 0, 12) };

            var agri = row.Cells.Count > 2 ? row.Cells[2] : "50/100";
            var comm = row.Cells.Count > 3 ? row.Cells[3] : "50/100";
            var def = row.Cells.Count > 4 ? row.Cells[4] : "50/100";
            var ord = row.Cells.Count > 5 ? row.Cells[5] : "50/100";

            grid.Children.Add(BuildStatPill("🌾 Nông Khẩn", agri, TextSuccessBrush));
            grid.Children.Add(BuildStatPill("🏮 Thương Nghiệp", comm, GoldBrush));
            grid.Children.Add(BuildStatPill("🛡️ Thành Phòng", def, TextPrimaryBrush));
            grid.Children.Add(BuildStatPill("⚖️ Trị An Dân Tâm", ord, TextWarningBrush));
            root.Children.Add(grid);

            if (row.Commands.Count > 0)
            {
                var cmdPanel = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
                foreach (var cmd in row.Commands)
                {
                    var variant = cmd.Kind switch
                    {
                        CommandKind.Primary => GameButtonVariant.Primary,
                        CommandKind.Danger => GameButtonVariant.Danger,
                        _ => GameButtonVariant.Secondary
                    };

                    var btn = new GameButton(cmd.Label, variant, isCompact: false)
                    {
                        Margin = new Thickness(0, 0, 8, 8)
                    };
                    var cid = cmd.Id;
                    btn.Click += (_, _) => _onCommand(cid);
                    cmdPanel.Children.Add(btn);
                }
                root.Children.Add(cmdPanel);
            }

            mainStack.Children.Add(card);
        }
    }

    private static Border BuildStatPill(string label, string val, Brush accent)
    {
        var pill = new Border
        {
            Background = Freeze(new SolidColorBrush(Color.FromArgb(0x40, 0x1A, 0x14, 0x10))),
            BorderBrush = PanelBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(4)
        };
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 11,
            Foreground = TextMutedBrush,
            Margin = new Thickness(0, 0, 0, 4)
        });
        sp.Children.Add(new TextBlock
        {
            Text = val,
            FontSize = 13.5,
            FontWeight = FontWeights.Bold,
            Foreground = accent
        });
        pill.Child = sp;
        return pill;
    }

    #endregion

    #region Warlord Scrutiny & Extortion View (Cây To Đón Gió)

    private void BuildWarlordScrutinyView()
    {
        var mainStack = new StackPanel();
        Children.Add(mainStack);

        foreach (var row in _section.Rows)
        {
            var c0 = row.Cells.Count > 0 ? row.Cells[0] : string.Empty;
            var isDemand = c0.Contains("TRÁT LỆNH", StringComparison.OrdinalIgnoreCase);

            if (!isDemand)
            {
                var threatBadgeText = row.Cells.Count > 1 ? row.Cells[1] : "🟢 Bình An";
                var scoreText = row.Cells.Count > 2 ? row.Cells[2] : "0/100";
                var wealthText = row.Cells.Count > 3 ? row.Cells[3] : "—";
                var surchargeText = row.Cells.Count > 4 ? row.Cells[4] : "Không";
                var statusText = row.Cells.Count > 5 ? row.Cells[5] : "—";

                var scoreVal = ExtractLeadingInt(scoreText, 0);
                var isHighRisk = scoreVal >= 60 || statusText.Contains("Cấm vận");
                var barColor = scoreVal >= 85 ? TextDangerBrush : scoreVal >= 60 ? TextWarningBrush : scoreVal >= 40 ? GoldBrush : TextSuccessBrush;

                var card = new Border
                {
                    Background = isHighRisk ? DangerBgBrush : ActiveOfficeBg,
                    BorderBrush = isHighRisk ? DangerBorderBrush : GoldDimBrush,
                    BorderThickness = new Thickness(1.5),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(14),
                    Margin = new Thickness(0, 0, 0, 10)
                };

                var root = new StackPanel();
                card.Child = root;

                var head = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
                var titleBlock = new TextBlock
                {
                    Text = $"🦅 PHONG VŨ BIỂU QUÂN PHIỆT · {c0}",
                    FontWeight = FontWeights.Bold,
                    FontSize = 14,
                    Foreground = isHighRisk ? TextWarningBrush : GoldBrush
                };
                DockPanel.SetDock(titleBlock, Dock.Left);
                head.Children.Add(titleBlock);

                var badgeVariant = scoreVal >= 60 ? BadgeVariant.Warning : BadgeVariant.Gain;
                var badge = new GameBadge(threatBadgeText, badgeVariant);
                DockPanel.SetDock(badge, Dock.Right);
                head.Children.Add(badge);
                root.Children.Add(head);

                // Thermometer Gauge Bar (0..100)
                var gaugeLabelRow = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
                gaugeLabelRow.Children.Add(new TextBlock
                {
                    Text = "Nhiệt kế dòm ngó sản nghiệp (Cây To Đón Gió)",
                    FontSize = 11,
                    Foreground = TextMutedBrush
                });
                var scoreRight = new TextBlock
                {
                    Text = scoreText,
                    FontSize = 11.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = barColor,
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                DockPanel.SetDock(scoreRight, Dock.Right);
                gaugeLabelRow.Children.Add(scoreRight);
                root.Children.Add(gaugeLabelRow);

                var gaugeTrack = new Grid { Height = 10, Margin = new Thickness(0, 0, 0, 10) };
                gaugeTrack.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(1, scoreVal), GridUnitType.Star) });
                gaugeTrack.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(1, 100 - scoreVal), GridUnitType.Star) });
                var trackBg = new Border { Background = TrackBgBrush, CornerRadius = new CornerRadius(4) };
                SetColumnSpan(trackBg, 2);
                gaugeTrack.Children.Add(trackBg);
                var fillBar = new Border { Background = barColor, CornerRadius = new CornerRadius(4) };
                SetColumn(fillBar, 0);
                gaugeTrack.Children.Add(fillBar);
                root.Children.Add(gaugeTrack);

                var pillGrid = new UniformGrid { Columns = 3, Margin = new Thickness(0, 0, 0, 10) };
                pillGrid.Children.Add(BuildStatPill("💰 Tài Sản / Ngưỡng Bảo Hộ", wealthText, GoldBrush));
                pillGrid.Children.Add(BuildStatPill("⚖️ Phụ Thu Thuế Chợ", surchargeText, surchargeText != "Không" ? TextWarningBrush : TextSuccessBrush));
                pillGrid.Children.Add(BuildStatPill("📜 Tình Trạng Lãnh Địa", statusText, statusText.Contains("Cấm vận") ? TextDangerBrush : TextPrimaryBrush));
                root.Children.Add(pillGrid);

                if (row.Commands.Count > 0)
                {
                    var cmdWrap = new WrapPanel();
                    cmdWrap.Children.Add(new TextBlock
                    {
                        Text = "Tán tài tụ nghĩa (hạ nhiệt dòm ngó): ",
                        FontSize = 11.5,
                        Foreground = TextMutedBrush,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 8, 4)
                    });
                    foreach (var cmd in row.Commands)
                    {
                        var btn = new GameButton(cmd.Label, cmd.Kind == CommandKind.Primary ? GameButtonVariant.Primary : GameButtonVariant.Secondary, isCompact: true)
                        {
                            Margin = new Thickness(0, 0, 6, 4)
                        };
                        var cid = cmd.Id;
                        btn.Click += (_, _) => _onCommand(cid);
                        cmdWrap.Children.Add(btn);
                    }
                    root.Children.Add(cmdWrap);
                }

                mainStack.Children.Add(card);
            }
            else
            {
                // Extortion Demand Card (Trát Lệnh Quân Phiệt)
                var demTitle = row.Cells.Count > 1 ? row.Cells[1] : "Trát Trưng Thu Quân Nhu";
                var demAmount = row.Cells.Count > 2 ? row.Cells[2] : "0 Quan";
                var demDue = row.Cells.Count > 3 ? row.Cells[3] : "—";
                var demDesc = row.Cells.Count > 5 ? row.Cells[5] : string.Empty;

                var demCard = new Border
                {
                    Background = DangerBgBrush,
                    BorderBrush = DangerBorderBrush,
                    BorderThickness = new Thickness(1.5),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(14),
                    Margin = new Thickness(0, 0, 0, 8)
                };

                var sp = new StackPanel();
                demCard.Child = sp;

                var hDock = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
                hDock.Children.Add(new TextBlock
                {
                    Text = $"{c0} · {demTitle}",
                    FontWeight = FontWeights.Bold,
                    FontSize = 13.5,
                    Foreground = TextWarningBrush
                });
                var amtBadge = new GameBadge($"Đòi nộp: {demAmount} ({demDue})", BadgeVariant.Warning);
                DockPanel.SetDock(amtBadge, Dock.Right);
                hDock.Children.Add(amtBadge);
                sp.Children.Add(hDock);

                sp.Children.Add(new TextBlock
                {
                    Text = demDesc,
                    FontSize = 11.5,
                    Foreground = TextPrimaryBrush,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 8)
                });

                if (row.Commands.Count > 0)
                {
                    var bp = new WrapPanel();
                    foreach (var cmd in row.Commands)
                    {
                        var variant = cmd.Kind switch
                        {
                            CommandKind.Primary => GameButtonVariant.Primary,
                            CommandKind.Danger => GameButtonVariant.Danger,
                            _ => GameButtonVariant.Secondary
                        };
                        var btn = new GameButton(cmd.Label, variant, isCompact: true)
                        {
                            Margin = new Thickness(0, 0, 6, 4)
                        };
                        var cid = cmd.Id;
                        btn.Click += (_, _) => _onCommand(cid);
                        bp.Children.Add(btn);
                    }
                    sp.Children.Add(bp);
                }

                mainStack.Children.Add(demCard);
            }
        }
    }

    #endregion

    #region Court Factions Triad View (Sóng Ngầm Quan Trường)

    private void BuildCourtFactionsTriadView()
    {
        var mainStack = new StackPanel();
        Children.Add(mainStack);

        var wrap = new WrapPanel { Orientation = Orientation.Horizontal };
        mainStack.Children.Add(wrap);

        foreach (var row in _section.Rows)
        {
            var name = row.Cells.Count > 0 ? row.Cells[0] : "Thế Lực Triều Đình";
            var desc = row.Cells.Count > 1 ? row.Cells[1] : string.Empty;
            var scoreText = row.Cells.Count > 2 ? row.Cells[2] : "0 Điểm";
            var attitude = row.Cells.Count > 3 ? row.Cells[3] : "⚖️ Trung Lập";
            var score = ExtractSignedInt(scoreText, 0);

            var isFriendly = score >= 30;
            var isHostile = score <= -30;

            var card = new Border
            {
                Background = isFriendly ? ActiveOfficeBg : (isHostile ? DangerBgBrush : CardBg),
                BorderBrush = isFriendly ? GoldBrush : (isHostile ? DangerBorderBrush : PanelBorder),
                BorderThickness = new Thickness(isFriendly || isHostile ? 1.5 : 1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(14),
                Margin = new Thickness(6),
                Width = 300,
                Cursor = Cursors.Hand
            };

            var stack = new StackPanel();
            card.Child = stack;

            var head = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            head.Children.Add(new TextBlock
            {
                Text = $"🏛️ {name}",
                FontWeight = FontWeights.Bold,
                FontSize = 13.5,
                Foreground = isFriendly ? GoldBrush : (isHostile ? TextWarningBrush : TextPrimaryBrush),
                MaxWidth = 190,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            var badge = new GameBadge(scoreText, isFriendly ? BadgeVariant.Gain : (isHostile ? BadgeVariant.Warning : BadgeVariant.Default));
            DockPanel.SetDock(badge, Dock.Right);
            head.Children.Add(badge);
            stack.Children.Add(head);

            stack.Children.Add(new TextBlock
            {
                Text = attitude,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = isFriendly ? TextSuccessBrush : (isHostile ? TextDangerBrush : GoldDimBrush),
                Margin = new Thickness(0, 0, 0, 6)
            });

            // Bipolar Standing Bar (-100 .. 0 .. +100)
            var normalized = Math.Clamp((score + 100) / 2, 1, 99);
            var barGrid = new Grid { Height = 8, Margin = new Thickness(0, 0, 0, 8) };
            barGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(normalized, GridUnitType.Star) });
            barGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100 - normalized, GridUnitType.Star) });
            var track = new Border { Background = TrackBgBrush, CornerRadius = new CornerRadius(3) };
            SetColumnSpan(track, 2);
            barGrid.Children.Add(track);
            var fill = new Border
            {
                Background = score >= 0 ? TextSuccessBrush : TextDangerBrush,
                CornerRadius = new CornerRadius(3)
            };
            SetColumn(fill, 0);
            barGrid.Children.Add(fill);
            stack.Children.Add(barGrid);

            stack.Children.Add(new TextBlock
            {
                Text = desc,
                FontSize = 11,
                Foreground = TextMutedBrush,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            });

            if (row.Commands.Count > 0)
            {
                foreach (var cmd in row.Commands)
                {
                    var btn = new GameButton(cmd.Label, GameButtonVariant.Primary, isCompact: true)
                    {
                        HorizontalAlignment = HorizontalAlignment.Stretch
                    };
                    var cid = cmd.Id;
                    btn.Click += (_, _) => _onCommand(cid);
                    stack.Children.Add(btn);
                }
            }

            card.MouseLeftButtonUp += (_, _) => _onInspect?.Invoke(_section, row);
            wrap.Children.Add(card);
        }
    }

    #endregion

    #region Imperial Censorate Inspection View (Ngự Sử Đài Thanh Sát)

    private void BuildImperialInspectionView()
    {
        var mainStack = new StackPanel();
        Children.Add(mainStack);

        foreach (var row in _section.Rows)
        {
            var isCommandRow = row.Commands.Count > 0;
            var c0 = row.Cells.Count > 0 ? row.Cells[0] : "—";
            var censorName = row.Cells.Count > 1 ? row.Cells[1] : "Khâm Sai Đại Thần";
            var approach = row.Cells.Count > 2 ? row.Cells[2] : "—";
            var outcome = row.Cells.Count > 3 ? row.Cells[3] : "—";
            var delta = row.Cells.Count > 4 ? row.Cells[4] : "—";
            var narrative = row.Cells.Count > 5 ? row.Cells[5] : string.Empty;

            var isNegative = outcome.Contains("🔴") || outcome.Contains("🟠");
            var card = new Border
            {
                Background = isCommandRow ? ActiveOfficeBg : (isNegative ? DangerBgBrush : CardBg),
                BorderBrush = isCommandRow ? GoldBrush : (isNegative ? DangerBorderBrush : PanelBorder),
                BorderThickness = new Thickness(isCommandRow ? 1.5 : 1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 8),
                Cursor = Cursors.Hand
            };

            var sp = new StackPanel();
            card.Child = sp;

            var head = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            head.Children.Add(new TextBlock
            {
                Text = isCommandRow ? $"⚖️ LỆNH BÀI NGỰ SỬ ĐÀI · {censorName}" : $"📜 {c0} · {censorName} ({approach})",
                FontWeight = FontWeights.Bold,
                FontSize = 13.5,
                Foreground = isCommandRow ? GoldBrush : TextPrimaryBrush
            });

            var badgeVariant = outcome.Contains("🟢") ? BadgeVariant.Gain : (isNegative ? BadgeVariant.Warning : BadgeVariant.Default);
            var badge = new GameBadge(isCommandRow ? outcome : $"{outcome} ({delta})", badgeVariant);
            DockPanel.SetDock(badge, Dock.Right);
            head.Children.Add(badge);
            sp.Children.Add(head);

            sp.Children.Add(new TextBlock
            {
                Text = narrative,
                FontSize = 11.5,
                Foreground = isCommandRow ? TextMutedBrush : TextPrimaryBrush,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, isCommandRow ? 10 : 2)
            });

            if (isCommandRow)
            {
                var bp = new WrapPanel();
                foreach (var cmd in row.Commands)
                {
                    var btn = new GameButton(cmd.Label, cmd.Kind == CommandKind.Primary ? GameButtonVariant.Primary : GameButtonVariant.Secondary, isCompact: true)
                    {
                        Margin = new Thickness(0, 0, 8, 4)
                    };
                    var cid = cmd.Id;
                    btn.Click += (_, _) => _onCommand(cid);
                    bp.Children.Add(btn);
                }
                sp.Children.Add(bp);
            }

            card.MouseLeftButtonUp += (_, _) => _onInspect?.Invoke(_section, row);
            mainStack.Children.Add(card);
        }
    }

    private static int ExtractLeadingInt(string text, int fallback)
    {
        if (string.IsNullOrWhiteSpace(text)) return fallback;
        var slashIdx = text.IndexOf('/');
        var candidate = slashIdx > 0 ? text[..slashIdx] : text;
        var digits = new string(candidate.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var val) ? Math.Clamp(val, 0, 100) : fallback;
    }

    private static int ExtractSignedInt(string text, int fallback)
    {
        if (string.IsNullOrWhiteSpace(text)) return fallback;
        var token = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? text;
        return int.TryParse(token, out var val) ? Math.Clamp(val, -100, 100) : fallback;
    }

    #endregion

    private static T Freeze<T>(T freezable) where T : Freezable
    {
        if (freezable.CanFreeze) freezable.Freeze();
        return freezable;
    }
}
