using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SimT.Contracts.Ui;

namespace SimT.Host.Wpf.Controls;

/// <summary>
/// Immersive Tactical War Camp & Martial Arena control for military expeditions,
/// duels, bandit stronghold assaults, and supply logistics.
/// </summary>
public sealed class GameTacticalWarCampControl : Grid
{
    private static readonly Brush PanelBg = Freeze(new SolidColorBrush(Color.FromRgb(0x16, 0x13, 0x11)));
    private static readonly Brush PanelBorder = Freeze(new SolidColorBrush(Color.FromRgb(0x3E, 0x2A, 0x20)));
    private static readonly Brush CardBg = Freeze(new SolidColorBrush(Color.FromRgb(0x1E, 0x17, 0x13)));
    private static readonly Brush CardHoverBg = Freeze(new SolidColorBrush(Color.FromRgb(0x2A, 0x20, 0x19)));
    private static readonly Brush GoldBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xD4, 0xAF, 0x37)));
    private static readonly Brush CrimsonBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xB8, 0x38, 0x2B)));
    private static readonly Brush CrimsonBorder = Freeze(new SolidColorBrush(Color.FromRgb(0x73, 0x24, 0x1B)));
    private static readonly Brush TextPrimaryBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xF0, 0xE4, 0xCE)));
    private static readonly Brush TextMutedBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x9E, 0x8F, 0x7C)));
    private static readonly Brush TextWarningBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xDF, 0x8A, 0x48)));
    private static readonly Brush TextSuccessBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x5E, 0xA8, 0x72)));

    private readonly ScreenSection _section;
    private readonly Action<string> _onCommand;
    private readonly Action<ScreenSection, ScreenRow>? _onInspect;

    public GameTacticalWarCampControl(
        ScreenSection section,
        Action<string> onCommand,
        Action<ScreenSection, ScreenRow>? onInspect = null)
    {
        _section = section;
        _onCommand = onCommand;
        _onInspect = onInspect;

        Margin = new Thickness(0, 4, 0, 4);

        var heading = section.Heading;
        if (heading.Contains("Lôi Đài", StringComparison.OrdinalIgnoreCase) ||
            heading.Contains("Tỷ Võ", StringComparison.OrdinalIgnoreCase))
        {
            BuildMartialArenaView();
        }
        else if (heading.Contains("Cứ Điểm", StringComparison.OrdinalIgnoreCase) ||
                 heading.Contains("Sơn Tặc", StringComparison.OrdinalIgnoreCase))
        {
            BuildStrongholdAssaultView();
        }
        else
        {
            BuildMilitarySupplyView();
        }
    }

    #region Martial Arena (Lôi Đài Tỷ Võ)

    private void BuildMartialArenaView()
    {
        var mainStack = new StackPanel();
        Children.Add(mainStack);

        // Header Strip
        var banner = new Border
        {
            Background = Freeze(new SolidColorBrush(Color.FromArgb(0x40, 0x73, 0x24, 0x1B))),
            BorderBrush = CrimsonBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 0, 12)
        };
        var bannerDock = new DockPanel();
        var bannerTitle = new TextBlock
        {
            Text = "⚔️ THAO TRƯỜNG TỶ VÕ & CHIÊU MỘ DANH TƯỚNG",
            FontWeight = FontWeights.Bold,
            FontSize = 13,
            Foreground = GoldBrush
        };
        DockPanel.SetDock(bannerTitle, Dock.Left);
        bannerDock.Children.Add(bannerTitle);

        var counterPill = new GameBadge($"{_section.Rows.Count} Hảo Thủ", BadgeVariant.Gain);
        DockPanel.SetDock(counterPill, Dock.Right);
        bannerDock.Children.Add(counterPill);
        banner.Child = bannerDock;
        mainStack.Children.Add(banner);

        // Warrior Grid
        var wrap = new WrapPanel { Orientation = Orientation.Horizontal };
        mainStack.Children.Add(wrap);

        foreach (var row in _section.Rows)
        {
            wrap.Children.Add(BuildWarriorCard(row));
        }

        if (_section.Rows.Count == 0)
        {
            mainStack.Children.Add(new TextBlock
            {
                Text = "Hiện không có võ tướng nào lập đài tỷ võ tại thành này.",
                Foreground = TextMutedBrush,
                FontStyle = FontStyles.Italic,
                Margin = new Thickness(12)
            });
        }
    }

    private UIElement BuildWarriorCard(ScreenRow row)
    {
        var card = new Border
        {
            Background = CardBg,
            BorderBrush = PanelBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12),
            Margin = new Thickness(6),
            Width = 275,
            Cursor = Cursors.Hand
        };

        var stack = new StackPanel();
        card.Child = stack;

        // Header: Name & Role
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        var name = row.Cells.Count > 0 ? row.Cells[0] : (row.Title ?? "Võ Tướng");
        var nameBlock = new TextBlock
        {
            Text = name,
            FontWeight = FontWeights.Bold,
            FontSize = 13.5,
            Foreground = TextPrimaryBrush,
            MaxWidth = 180,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        DockPanel.SetDock(nameBlock, Dock.Left);
        header.Children.Add(nameBlock);

        var role = row.Cells.Count > 1 ? row.Cells[1] : "Hiệp khách";
        var roleBadge = new GameBadge(role, BadgeVariant.Default);
        DockPanel.SetDock(roleBadge, Dock.Right);
        header.Children.Add(roleBadge);
        stack.Children.Add(header);

        // Branch & Troop Affinity
        var branch = row.Cells.Count > 2 ? row.Cells[2] : "Bộ Binh";
        var branchBox = new Border
        {
            Background = Freeze(new SolidColorBrush(Color.FromArgb(0x30, 0x12, 0x0E, 0x0A))),
            BorderBrush = PanelBorder,
            BorderThickness = new Thickness(0.8),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 4, 6, 4),
            Margin = new Thickness(0, 0, 0, 8)
        };
        var branchText = new TextBlock
        {
            Text = $"Binh chủng: {branch}",
            FontSize = 11,
            Foreground = GoldBrush,
            FontWeight = FontWeights.SemiBold
        };
        branchBox.Child = branchText;
        stack.Children.Add(branchBox);

        // Key Attributes Strip: WAR / CON / WIL
        var war = row.Cells.Count > 3 ? row.Cells[3] : "—";
        var con = row.Cells.Count > 4 ? row.Cells[4] : "—";
        var wil = row.Cells.Count > 5 ? row.Cells[5] : "—";

        var attrGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        attrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        attrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        attrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var warPill = BuildAttrPill("Võ Lực", war, CrimsonBrush);
        Grid.SetColumn(warPill, 0);
        attrGrid.Children.Add(warPill);

        var conPill = BuildAttrPill("Căn Cốt", con, TextPrimaryBrush);
        Grid.SetColumn(conPill, 1);
        attrGrid.Children.Add(conPill);

        var wilPill = BuildAttrPill("Định Lực", wil, TextMutedBrush);
        Grid.SetColumn(wilPill, 2);
        attrGrid.Children.Add(wilPill);
        stack.Children.Add(attrGrid);

        // Buttons: Duel & Modal Profile
        if (row.Commands.Count > 0)
        {
            var btnPanel = new DockPanel { Margin = new Thickness(0, 4, 0, 0) };
            var duelCmd = row.Commands.FirstOrDefault(c => c.Id.StartsWith("duel:", StringComparison.Ordinal));
            var modalCmd = row.Commands.FirstOrDefault(c => c.Id.StartsWith("npc:", StringComparison.Ordinal));

            if (duelCmd is not null)
            {
                var duelBtn = new GameButton(
                    duelCmd.Label,
                    GameButtonVariant.Primary,
                    isCompact: true)
                {
                    Margin = new Thickness(0, 0, 6, 0)
                };
                duelBtn.Click += (_, _) => _onCommand(duelCmd.Id);
                DockPanel.SetDock(duelBtn, Dock.Left);
                btnPanel.Children.Add(duelBtn);
            }

            if (modalCmd is not null)
            {
                var profileBtn = new GameButton(
                    modalCmd.Label,
                    GameButtonVariant.Secondary,
                    isCompact: true);
                profileBtn.Click += (_, _) => _onCommand(modalCmd.Id);
                DockPanel.SetDock(profileBtn, Dock.Right);
                btnPanel.Children.Add(profileBtn);
            }
            stack.Children.Add(btnPanel);
        }

        card.MouseEnter += (_, _) => card.Background = CardHoverBg;
        card.MouseLeave += (_, _) => card.Background = CardBg;
        card.MouseLeftButtonUp += (_, _) => _onInspect?.Invoke(_section, row);

        return card;
    }

    private static UIElement BuildAttrPill(string label, string val, Brush valBrush)
    {
        var border = new Border
        {
            Background = Freeze(new SolidColorBrush(Color.FromArgb(0x40, 0x11, 0x0E, 0x0A))),
            BorderBrush = PanelBorder,
            BorderThickness = new Thickness(0.8),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(4, 3, 4, 3),
            Margin = new Thickness(2)
        };
        var s = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        s.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 9.5,
            Foreground = TextMutedBrush,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        s.Children.Add(new TextBlock
        {
            Text = val,
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = valBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 1, 0, 0)
        });
        border.Child = s;
        return border;
    }

    #endregion

    #region Stronghold Assault View (Cứ Điểm Ngoại Thành)

    private void BuildStrongholdAssaultView()
    {
        var mainStack = new StackPanel();
        Children.Add(mainStack);

        var wrap = new WrapPanel { Orientation = Orientation.Horizontal };
        mainStack.Children.Add(wrap);

        foreach (var row in _section.Rows)
        {
            wrap.Children.Add(BuildStrongholdCard(row));
        }

        if (_section.Rows.Count == 0)
        {
            mainStack.Children.Add(new TextBlock
            {
                Text = "Khu vực phụ cận thái bình, không phát hiện tung tích thổ phỉ hay cứ điểm địch.",
                Foreground = TextSuccessBrush,
                FontStyle = FontStyles.Italic,
                Margin = new Thickness(12)
            });
        }
    }

    private UIElement BuildStrongholdCard(ScreenRow row)
    {
        var card = new Border
        {
            Background = CardBg,
            BorderBrush = CrimsonBorder,
            BorderThickness = new Thickness(1.2),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14),
            Margin = new Thickness(8),
            Width = 310,
            Cursor = Cursors.Hand
        };

        var stack = new StackPanel();
        card.Child = stack;

        // Title & Bandit Faction
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        var name = row.Cells.Count > 0 ? row.Cells[0] : (row.Title ?? "Cứ Điểm");
        var titleBlock = new TextBlock
        {
            Text = $"⛰️ {name}",
            FontWeight = FontWeights.Bold,
            FontSize = 14,
            Foreground = TextPrimaryBrush,
            MaxWidth = 180,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        DockPanel.SetDock(titleBlock, Dock.Left);
        header.Children.Add(titleBlock);

        var faction = row.Cells.Count > 1 ? row.Cells[1] : "Thổ phỉ";
        var factionBadge = new GameBadge(faction, BadgeVariant.Loss);
        DockPanel.SetDock(factionBadge, Dock.Right);
        header.Children.Add(factionBadge);
        stack.Children.Add(header);

        // Danger Stars & Troop Branch
        var danger = row.Cells.Count > 3 ? row.Cells[3] : "★";
        var branch = row.Cells.Count > 2 ? row.Cells[2] : "Bộ Binh";
        var subLine = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        var dangerBlock = new TextBlock
        {
            Text = danger,
            FontWeight = FontWeights.Bold,
            FontSize = 12,
            Foreground = TextWarningBrush
        };
        DockPanel.SetDock(dangerBlock, Dock.Left);
        subLine.Children.Add(dangerBlock);

        var branchBlock = new TextBlock
        {
            Text = branch,
            FontSize = 11,
            Foreground = TextMutedBrush,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        DockPanel.SetDock(branchBlock, Dock.Right);
        subLine.Children.Add(branchBlock);
        stack.Children.Add(subLine);

        // Spoils of War (Loot & Items)
        var lootGold = row.Cells.Count > 4 ? row.Cells[4] : "0 Quan";
        var lootItem = row.Cells.Count > 5 ? row.Cells[5] : "—";
        var cleared = row.Cells.Count > 6 ? row.Cells[6] : "0 lần";

        var lootBox = new Border
        {
            Background = Freeze(new SolidColorBrush(Color.FromArgb(0x40, 0x12, 0x0E, 0x0A))),
            BorderBrush = PanelBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(0, 0, 0, 10)
        };
        var lootStack = new StackPanel();
        lootStack.Children.Add(new TextBlock
        {
            Text = $"💰 Chiến lợi phẩm: {lootGold} + {lootItem}",
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = GoldBrush
        });
        lootStack.Children.Add(new TextBlock
        {
            Text = $"🚩 Đã tiễu phạt: {cleared}",
            FontSize = 10.5,
            Foreground = TextMutedBrush,
            Margin = new Thickness(0, 2, 0, 0)
        });
        lootBox.Child = lootStack;
        stack.Children.Add(lootBox);

        // Action Buttons: Assault (Danger) & Recon (Secondary)
        if (row.Commands.Count > 0)
        {
            var btnPanel = new DockPanel();
            var assaultCmd = row.Commands.FirstOrDefault(c => c.Id.Contains("assault"));
            var reconCmd = row.Commands.FirstOrDefault(c => c.Id.Contains("inspect"));

            if (assaultCmd is not null)
            {
                var assaultBtn = new GameButton(
                    assaultCmd.Label,
                    GameButtonVariant.Danger,
                    isCompact: true)
                {
                    FontSize = 12.5,
                    FontWeight = FontWeights.Bold
                };
                assaultBtn.Click += (_, _) => _onCommand(assaultCmd.Id);
                DockPanel.SetDock(assaultBtn, Dock.Left);
                btnPanel.Children.Add(assaultBtn);
            }

            if (reconCmd is not null)
            {
                var reconBtn = new GameButton(
                    reconCmd.Label,
                    GameButtonVariant.Secondary,
                    isCompact: true);
                reconBtn.Click += (_, _) => _onCommand(reconCmd.Id);
                DockPanel.SetDock(reconBtn, Dock.Right);
                btnPanel.Children.Add(reconBtn);
            }
            stack.Children.Add(btnPanel);
        }

        card.MouseEnter += (_, _) => card.Background = CardHoverBg;
        card.MouseLeave += (_, _) => card.Background = CardBg;
        card.MouseLeftButtonUp += (_, _) => _onInspect?.Invoke(_section, row);

        return card;
    }

    #endregion

    #region Military Supply Logistics (Lệnh Tiếp Tế Quân Lương)

    private void BuildMilitarySupplyView()
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

            var title = row.Title ?? (row.Cells.Count > 0 ? row.Cells[0] : "Quân Nhu");
            s.Children.Add(new TextBlock
            {
                Text = $"🌾 {title}",
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                Foreground = TextPrimaryBrush,
                Margin = new Thickness(0, 0, 0, 6)
            });

            var need = row.Cells.Count > 1 ? row.Cells[1] : "—";
            var stock = row.Cells.Count > 2 ? row.Cells[2] : "0";
            var reward = row.Cells.Count > 3 ? row.Cells[3] : "—";
            var deadline = row.Cells.Count > 4 ? row.Cells[4] : "—";

            s.Children.Add(new TextBlock
            {
                Text = $"Đòi hỏi: {need} (Trong kho: {stock})",
                FontSize = 11,
                Foreground = stock.StartsWith("0") ? TextWarningBrush : TextSuccessBrush
            });
            s.Children.Add(new TextBlock
            {
                Text = $"Thù lao: {reward} · Hạn: {deadline}",
                FontSize = 10.5,
                Foreground = GoldBrush,
                Margin = new Thickness(0, 2, 0, 6)
            });

            if (row.Commands.Count > 0)
            {
                var cmd = row.Commands[0];
                var btn = new GameButton(cmd.Label, cmd.Kind == CommandKind.Primary ? GameButtonVariant.Primary : GameButtonVariant.Secondary, isCompact: true)
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                var cid = cmd.Id;
                btn.Click += (_, _) => _onCommand(cid);
                s.Children.Add(btn);
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
