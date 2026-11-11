using System.Windows.Controls.Primitives;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SimT.Contracts.Ui;

namespace SimT.Host.Wpf.Controls;

/// <summary>
/// Immersive Artisan Workbench & Forge deck for professions, weapon crafting, tempering, and armory maintenance.
/// Displays an ancient anvil crucible, socketed materials, provenance seals, and equipment cards.
/// </summary>
public sealed class GameArtisanWorkbenchControl : Grid
{
    private static readonly Brush PanelBg = Freeze(new SolidColorBrush(Color.FromRgb(0x18, 0x14, 0x10)));
    private static readonly Brush PanelBorder = Freeze(new SolidColorBrush(Color.FromRgb(0x3E, 0x2F, 0x20)));
    private static readonly Brush AnvilBg = Freeze(new SolidColorBrush(Color.FromRgb(0x21, 0x19, 0x13)));
    private static readonly Brush CardBg = Freeze(new SolidColorBrush(Color.FromRgb(0x1D, 0x17, 0x12)));
    private static readonly Brush CardHoverBg = Freeze(new SolidColorBrush(Color.FromRgb(0x28, 0x20, 0x18)));
    private static readonly Brush CardSelectedBg = Freeze(new SolidColorBrush(Color.FromRgb(0x32, 0x25, 0x1A)));
    private static readonly Brush GoldBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xD4, 0xAF, 0x37)));
    private static readonly Brush GoldDimBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x8A, 0x72, 0x35)));
    private static readonly Brush TextMutedBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x9E, 0x8F, 0x7C)));
    private static readonly Brush TextPrimaryBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xF0, 0xE4, 0xCE)));
    private static readonly Brush TextWarningBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xDF, 0x8A, 0x48)));
    private static readonly Brush TextSuccessBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x5E, 0xA8, 0x72)));

    private readonly ScreenSection _section;
    private readonly Action<string> _onCommand;
    private readonly Action<ScreenSection, ScreenRow>? _onInspect;

    private ScreenRow? _selectedRecipeRow;
    private Border? _anvilPanelContainer;

    public GameArtisanWorkbenchControl(
        ScreenSection section,
        Action<string> onCommand,
        Action<ScreenSection, ScreenRow>? onInspect = null)
    {
        _section = section;
        _onCommand = onCommand;
        _onInspect = onInspect;

        Margin = new Thickness(0, 4, 0, 4);

        var heading = section.Heading;
        if (heading.Contains("Bản Vẽ", StringComparison.OrdinalIgnoreCase) ||
            heading.Contains("Chế Tác", StringComparison.OrdinalIgnoreCase))
        {
            BuildRecipeWorkbenchView();
        }
        else if (heading.Contains("Khí Cụ", StringComparison.OrdinalIgnoreCase) ||
                 heading.Contains("Trang Bị", StringComparison.OrdinalIgnoreCase))
        {
            BuildArmoryCatalogView();
        }
        else
        {
            BuildGenericWorkbenchCards();
        }
    }

    #region Recipe Workbench (Lò Rèn & Đe Chế Tác)

    private void BuildRecipeWorkbenchView()
    {
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(380) }); // Anvil Crucible
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });  // Gap
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Recipe Rack

        _selectedRecipeRow = _section.Rows.FirstOrDefault();

        // 1. Left Crucible & Anvil
        _anvilPanelContainer = new Border
        {
            Background = AnvilBg,
            BorderBrush = PanelBorder,
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(16)
        };
        Grid.SetColumn(_anvilPanelContainer, 0);
        Children.Add(_anvilPanelContainer);
        RefreshAnvilPanel();

        // 2. Right Recipe Catalog Rack
        var rackBorder = new Border
        {
            Background = PanelBg,
            BorderBrush = PanelBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12)
        };
        Grid.SetColumn(rackBorder, 2);
        Children.Add(rackBorder);

        var rackStack = new StackPanel();
        rackBorder.Child = rackStack;

        // Rack Header
        var rackHeader = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
        var rackTitle = new TextBlock
        {
            Text = $"📜 DANH SÁCH BẢN VẼ ({_section.Rows.Count})",
            FontWeight = FontWeights.Bold,
            FontSize = 13,
            Foreground = GoldBrush
        };
        DockPanel.SetDock(rackTitle, Dock.Left);
        rackHeader.Children.Add(rackTitle);

        var rackHint = new TextBlock
        {
            Text = "Chọn bản vẽ để đưa lên đe rèn",
            FontSize = 11,
            Foreground = TextMutedBrush,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        DockPanel.SetDock(rackHint, Dock.Right);
        rackHeader.Children.Add(rackHint);
        rackStack.Children.Add(rackHeader);

        // Recipe items
        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxHeight = 560
        };
        var recipeList = new StackPanel();
        scroll.Content = recipeList;
        rackStack.Children.Add(scroll);

        foreach (var row in _section.Rows)
        {
            recipeList.Children.Add(BuildRecipeRackItem(row));
        }
    }

    private UIElement BuildRecipeRackItem(ScreenRow row)
    {
        var isSelected = _selectedRecipeRow == row;
        var card = new Border
        {
            Background = isSelected ? CardSelectedBg : CardBg,
            BorderBrush = isSelected ? GoldBrush : PanelBorder,
            BorderThickness = new Thickness(isSelected ? 1.5 : 1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 0, 8),
            Cursor = Cursors.Hand
        };

        var rowGrid = new Grid();
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) }); // Icon slot
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) }); // Spacing
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Info
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Status / Craft button

        // Icon slot
        var slot = new GameItemSlot(
            row.Title ?? (row.Cells.Count > 0 ? row.Cells[0] : "Bản Vẽ"),
            quantity: 1,
            rarity: GameItemRarity.Fine)
        {
            IsSelected = isSelected
        };
        Grid.SetColumn(slot, 0);
        rowGrid.Children.Add(slot);

        // Info
        var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
        var titleBlock = new TextBlock
        {
            Text = row.Title ?? (row.Cells.Count > 0 ? row.Cells[0] : "Bản Vẽ"),
            FontWeight = FontWeights.Bold,
            FontSize = 13,
            Foreground = TextPrimaryBrush
        };
        titleRow.Children.Add(titleBlock);

        // Tier badge if present
        if (row.Cells.Count > 1)
        {
            var tierText = row.Cells[1];
            titleRow.Children.Add(new Border
            {
                Background = Freeze(new SolidColorBrush(Color.FromArgb(0x40, 0x8A, 0x72, 0x35))),
                BorderBrush = GoldDimBrush,
                BorderThickness = new Thickness(0.8),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(4, 1, 4, 1),
                Margin = new Thickness(8, 0, 0, 0),
                Child = new TextBlock { Text = tierText, FontSize = 10, Foreground = GoldBrush }
            });
        }
        infoStack.Children.Add(titleRow);

        // Material summary
        if (row.Cells.Count > 2)
        {
            var matText = row.Cells[2];
            var isMatReady = matText.Contains('✅');
            var matBlock = new TextBlock
            {
                Text = $"Nguyên liệu: {matText}",
                FontSize = 11,
                Foreground = isMatReady ? TextSuccessBrush : TextWarningBrush,
                Margin = new Thickness(0, 3, 0, 0)
            };
            infoStack.Children.Add(matBlock);
        }

        // Expected output & fee
        if (row.Cells.Count > 5)
        {
            var outText = row.Cells[5];
            var feeText = row.Cells.Count > 3 ? row.Cells[3] : string.Empty;
            var outBlock = new TextBlock
            {
                Text = $"Thành phẩm: {outText} · Phí: {feeText}",
                FontSize = 10.5,
                Foreground = TextMutedBrush,
                Margin = new Thickness(0, 2, 0, 0)
            };
            infoStack.Children.Add(outBlock);
        }

        Grid.SetColumn(infoStack, 2);
        rowGrid.Children.Add(infoStack);

        // Quick Craft Button or Badge
        var actionStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var craftCmd = row.Commands.FirstOrDefault(c => c.Id.StartsWith("craft:", StringComparison.Ordinal));
        if (craftCmd is not null)
        {
            var btn = new GameButton(
                "Rèn 🔨",
                GameButtonVariant.Primary,
                isCompact: true)
            {
                Margin = new Thickness(4, 0, 0, 0)
            };
            btn.Click += (_, _) => _onCommand(craftCmd.Id);
            actionStack.Children.Add(btn);
        }
        else
        {
            var bountyCmd = row.Commands.FirstOrDefault(c => c.Id.StartsWith("bounty:", StringComparison.Ordinal));
            if (bountyCmd is not null)
            {
                var btn = new GameButton(
                    "Treo Thưởng 📌",
                    GameButtonVariant.Secondary,
                    isCompact: true)
                {
                    Margin = new Thickness(4, 0, 0, 0)
                };
                btn.Click += (_, _) => _onCommand(bountyCmd.Id);
                actionStack.Children.Add(btn);
            }
        }
        Grid.SetColumn(actionStack, 3);
        rowGrid.Children.Add(actionStack);

        card.Child = rowGrid;

        card.MouseEnter += (_, _) =>
        {
            if (_selectedRecipeRow != row)
                card.Background = CardHoverBg;
        };
        card.MouseLeave += (_, _) =>
        {
            if (_selectedRecipeRow != row)
                card.Background = CardBg;
        };
        card.MouseLeftButtonUp += (_, _) =>
        {
            _selectedRecipeRow = row;
            RefreshRecipeRackSelection();
            RefreshAnvilPanel();
        };

        return card;
    }

    private void RefreshRecipeRackSelection()
    {
        if (Children.Count > 1 && Children[1] is Border rackBorder && rackBorder.Child is StackPanel rackStack)
        {
            var scroll = rackStack.Children.OfType<ScrollViewer>().FirstOrDefault();
            if (scroll?.Content is StackPanel recipeList)
            {
                for (var i = 0; i < recipeList.Children.Count && i < _section.Rows.Count; i++)
                {
                    if (recipeList.Children[i] is Border b)
                    {
                        var isSelected = _section.Rows[i] == _selectedRecipeRow;
                        b.Background = isSelected ? CardSelectedBg : CardBg;
                        b.BorderBrush = isSelected ? GoldBrush : PanelBorder;
                        b.BorderThickness = new Thickness(isSelected ? 1.5 : 1);
                    }
                }
            }
        }
    }

    private void RefreshAnvilPanel()
    {
        if (_anvilPanelContainer is null) return;

        var stack = new StackPanel();
        _anvilPanelContainer.Child = stack;

        var row = _selectedRecipeRow;
        if (row is null)
        {
            stack.Children.Add(new TextBlock
            {
                Text = "Chưa chọn bản vẽ nào để rèn.",
                Foreground = TextMutedBrush,
                FontStyle = FontStyles.Italic,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 40, 0, 0)
            });
            return;
        }

        // Header: Anvil Title & Crucible State
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 14) };
        var anvilTitle = new TextBlock
        {
            Text = "🔥 LÒ LUYỆN & ĐE RÈN",
            FontWeight = FontWeights.Bold,
            FontSize = 14,
            Foreground = GoldBrush
        };
        DockPanel.SetDock(anvilTitle, Dock.Left);
        header.Children.Add(anvilTitle);

        var qualityPredict = row.Details?.FirstOrDefault(d => d.Label.Contains("Phẩm Chất", StringComparison.OrdinalIgnoreCase))?.Value
                             ?? "Tiêu chuẩn";
        var qualityBadge = new GameBadge(qualityPredict, BadgeVariant.Gain);
        DockPanel.SetDock(qualityBadge, Dock.Right);
        header.Children.Add(qualityBadge);
        stack.Children.Add(header);

        // Crucible Crucible Target Slot Presentation
        var targetBox = new Border
        {
            Background = Freeze(new SolidColorBrush(Color.FromArgb(0x60, 0x12, 0x0E, 0x0A))),
            BorderBrush = GoldDimBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14),
            Margin = new Thickness(0, 0, 0, 14)
        };
        var targetStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        var targetSlot = new GameItemSlot(
            row.Title ?? "Bảo Khí",
            quantity: 1,
            rarity: GameItemRarity.Master)
        {
            IsSelected = true,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        targetStack.Children.Add(targetSlot);

        var targetTitle = new TextBlock
        {
            Text = row.Title ?? "Thành Phẩm",
            FontWeight = FontWeights.Bold,
            FontSize = 14,
            Foreground = TextPrimaryBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 2)
        };
        targetStack.Children.Add(targetTitle);

        var outputInfo = row.Cells.Count > 5 ? row.Cells[5] : (row.Details?.FirstOrDefault(d => d.Label == "Thành Phẩm")?.Value ?? string.Empty);
        var outputBlock = new TextBlock
        {
            Text = outputInfo,
            FontSize = 11.5,
            Foreground = GoldBrush,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        targetStack.Children.Add(outputBlock);
        targetBox.Child = targetStack;
        stack.Children.Add(targetBox);

        // Required Materials (Socket Slots)
        var matGroupTitle = new TextBlock
        {
            Text = "KHUÔN ĐÚC & NGUYÊN LIỆU ĐÒI HỎI",
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = TextMutedBrush,
            Margin = new Thickness(0, 4, 0, 6)
        };
        stack.Children.Add(matGroupTitle);

        var matMain = row.Details?.FirstOrDefault(d => d.Label.Contains("Nguyên Liệu Chính", StringComparison.OrdinalIgnoreCase))?.Value;
        var matSec = row.Details?.FirstOrDefault(d => d.Label.Contains("Nguyên Liệu Phụ", StringComparison.OrdinalIgnoreCase))?.Value;

        stack.Children.Add(BuildMaterialRow("Nguyên Liệu Chính ⚒️", matMain ?? (row.Cells.Count > 2 ? row.Cells[2] : "—")));
        if (!string.IsNullOrWhiteSpace(matSec) && !matSec.Contains("Không yêu cầu", StringComparison.OrdinalIgnoreCase))
        {
            stack.Children.Add(BuildMaterialRow("Phụ Liệu Socket 💎", matSec));
        }

        // Crafting Costs and Duration
        var fee = row.Cells.Count > 3 ? row.Cells[3] : "—";
        var days = row.Cells.Count > 4 ? row.Cells[4] : "—";
        var costGrid = new UniformGrid { Columns = 2, Margin = new Thickness(0, 10, 0, 14) };
        costGrid.Children.Add(BuildMetaPill("Phí Thuê Lò", fee, GoldBrush));
        costGrid.Children.Add(BuildMetaPill("Thời Gian", days, TextPrimaryBrush));
        stack.Children.Add(costGrid);

        // Big Action Button
        var craftCmd = row.Commands.FirstOrDefault(c => c.Id.StartsWith("craft:", StringComparison.Ordinal));
        if (craftCmd is not null)
        {
            var btn = new GameButton(
                "Rèn Thần Binh 🔨",
                GameButtonVariant.Primary)
            {
                FontSize = 13.5,
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(16, 10, 16, 10),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            btn.Click += (_, _) => _onCommand(craftCmd.Id);
            stack.Children.Add(btn);
        }
        else
        {
            var bountyCmd = row.Commands.FirstOrDefault(c => c.Id.StartsWith("bounty:", StringComparison.Ordinal));
            if (bountyCmd is not null)
            {
                var btn = new GameButton(
                    bountyCmd.Label,
                    GameButtonVariant.Secondary)
                {
                    FontSize = 12.5,
                    Padding = new Thickness(12, 8, 12, 8),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                btn.Click += (_, _) => _onCommand(bountyCmd.Id);
                stack.Children.Add(btn);
            }
            else
            {
                var warnText = new TextBlock
                {
                    Text = "⚠️ Chưa đủ điều kiện hoặc thiếu nguyên liệu rèn đúc.",
                    FontSize = 11.5,
                    Foreground = TextWarningBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 8, 0, 0)
                };
                stack.Children.Add(warnText);
            }
        }
    }

    private static UIElement BuildMaterialRow(string label, string desc)
    {
        var box = new Border
        {
            Background = CardBg,
            BorderBrush = PanelBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(0, 0, 0, 6)
        };
        var dock = new DockPanel();
        var lbl = new TextBlock
        {
            Text = label,
            FontSize = 11,
            Foreground = TextMutedBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(lbl, Dock.Left);
        dock.Children.Add(lbl);

        var isReady = desc.Contains('✅') || desc.Contains("Đủ", StringComparison.OrdinalIgnoreCase);
        var val = new TextBlock
        {
            Text = desc,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = isReady ? TextSuccessBrush : TextWarningBrush,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(val, Dock.Right);
        dock.Children.Add(val);
        box.Child = dock;
        return box;
    }

    private static UIElement BuildMetaPill(string label, string val, Brush valBrush)
    {
        var border = new Border
        {
            Background = CardBg,
            BorderBrush = PanelBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 4, 6, 4),
            Margin = new Thickness(2)
        };
        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        stack.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 10,
            Foreground = TextMutedBrush,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        stack.Children.Add(new TextBlock
        {
            Text = val,
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = valBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 0)
        });
        border.Child = stack;
        return border;
    }

    #endregion

    #region Armory Catalog View (Kho Khí Cụ & Trang Bị)

    private void BuildArmoryCatalogView()
    {
        var mainStack = new StackPanel();
        Children.Add(mainStack);

        var wrap = new WrapPanel { Orientation = Orientation.Horizontal };
        mainStack.Children.Add(wrap);

        foreach (var row in _section.Rows)
        {
            wrap.Children.Add(BuildEquipmentCard(row));
        }

        if (_section.Rows.Count == 0)
        {
            mainStack.Children.Add(new TextBlock
            {
                Text = "Hiện chưa có binh khí hay bảo giáp nào lưu trữ tại đây.",
                Foreground = TextMutedBrush,
                FontStyle = FontStyles.Italic,
                Margin = new Thickness(12)
            });
        }
    }

    private UIElement BuildEquipmentCard(ScreenRow row)
    {
        var card = new Border
        {
            Background = CardBg,
            BorderBrush = PanelBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(12),
            Margin = new Thickness(6),
            Width = 270,
            Cursor = Cursors.Hand
        };

        var stack = new StackPanel();
        card.Child = stack;

        // Header: Name & Quality Rank
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        var title = row.Title ?? (row.Cells.Count > 0 ? row.Cells[0] : "Bảo Khí");
        var nameBlock = new TextBlock
        {
            Text = title,
            FontWeight = FontWeights.Bold,
            FontSize = 12.5,
            Foreground = TextPrimaryBrush,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 170
        };
        DockPanel.SetDock(nameBlock, Dock.Left);
        header.Children.Add(nameBlock);

        var qualityRank = row.Cells.Count > 3 ? row.Cells[3] : "—";
        var qualityBadge = new GameBadge(qualityRank, BadgeVariant.Default);
        DockPanel.SetDock(qualityBadge, Dock.Right);
        header.Children.Add(qualityBadge);
        stack.Children.Add(header);

        // Equipment Slot Icon & Stat
        var slotRow = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        var isEquipped = title.Contains("[Đang mặc]");
        var slotIcon = new GameItemSlot(
            title,
            quantity: 1,
            rarity: qualityRank.Contains("Thần") || qualityRank.Contains("Cực") ? GameItemRarity.Legendary : (qualityRank.Contains("Thượng") ? GameItemRarity.Master : GameItemRarity.Fine))
        {
            IsSelected = isEquipped
        };
        DockPanel.SetDock(slotIcon, Dock.Left);
        slotRow.Children.Add(slotIcon);

        var statStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
        var statBonus = row.Cells.Count > 1 ? row.Cells[1] : "—";
        statStack.Children.Add(new TextBlock
        {
            Text = statBonus,
            FontWeight = FontWeights.Bold,
            FontSize = 13,
            Foreground = GoldBrush
        });

        // Durability
        var duraText = row.Cells.Count > 2 ? row.Cells[2] : "—";
        statStack.Children.Add(new TextBlock
        {
            Text = $"Độ bền: {duraText}",
            FontSize = 11,
            Foreground = duraText.Contains("Tổn hại") || duraText.Contains("Tàn tạ") ? TextWarningBrush : TextMutedBrush,
            Margin = new Thickness(0, 2, 0, 0)
        });
        DockPanel.SetDock(statStack, Dock.Left);
        slotRow.Children.Add(statStack);
        stack.Children.Add(slotRow);

        // Provenance & Legend seal if available
        var creator = row.Details?.FirstOrDefault(d => d.Label.Contains("Nghệ Nhân", StringComparison.OrdinalIgnoreCase))?.Value;
        if (!string.IsNullOrWhiteSpace(creator))
        {
            var provRow = new TextBlock
            {
                Text = $"👑 Tạo tác: {creator}",
                FontSize = 10.5,
                Foreground = TextMutedBrush,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 0, 4)
            };
            stack.Children.Add(provRow);
        }

        var feat = row.Details?.FirstOrDefault(d => d.Label.Contains("Chiến Tích", StringComparison.OrdinalIgnoreCase))?.Value;
        if (!string.IsNullOrWhiteSpace(feat) && !feat.Contains("Chưa nhuốm", StringComparison.OrdinalIgnoreCase))
        {
            var featBlock = new TextBlock
            {
                Text = $"⚔️ {feat}",
                FontSize = 10.5,
                Foreground = GoldDimBrush,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 0, 6)
            };
            stack.Children.Add(featBlock);
        }

        // Actions
        if (row.Commands.Count > 0)
        {
            var btnPanel = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
            foreach (var cmd in row.Commands)
            {
                var isPrimary = cmd.Kind == CommandKind.Primary || cmd.Id.StartsWith("equip:", StringComparison.Ordinal);
                var btn = new GameButton(
                    cmd.Label,
                    isPrimary ? GameButtonVariant.Primary : GameButtonVariant.Secondary,
                    isCompact: true)
                {
                    Margin = new Thickness(0, 0, 4, 4)
                };
                var cid = cmd.Id;
                btn.Click += (_, _) => _onCommand(cid);
                btnPanel.Children.Add(btn);
            }
            stack.Children.Add(btnPanel);
        }

        card.MouseEnter += (_, _) => card.Background = CardHoverBg;
        card.MouseLeave += (_, _) => card.Background = CardBg;
        if (row.Commands.Count == 0)
            card.MouseLeftButtonUp += (_, _) => _onInspect?.Invoke(_section, row);

        return card;
    }

    #endregion

    #region Generic Fallback Workbench Cards

    private void BuildGenericWorkbenchCards()
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
                Padding = new Thickness(12),
                Margin = new Thickness(6),
                Width = 260
            };
            var s = new StackPanel();
            card.Child = s;

            var title = row.Title ?? (row.Cells.Count > 0 ? row.Cells[0] : "Thao Tác");
            s.Children.Add(new TextBlock
            {
                Text = title,
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                Foreground = TextPrimaryBrush,
                Margin = new Thickness(0, 0, 0, 6)
            });

            for (var i = 1; i < row.Cells.Count; i++)
            {
                var col = i < _section.Columns.Count ? _section.Columns[i] : $"#{i}";
                s.Children.Add(new TextBlock
                {
                    Text = $"{col}: {row.Cells[i]}",
                    FontSize = 11,
                    Foreground = TextMutedBrush,
                    Margin = new Thickness(0, 1, 0, 1)
                });
            }

            if (row.Commands.Count > 0)
            {
                var bp = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
                foreach (var cmd in row.Commands)
                {
                    var b = new GameButton(cmd.Label, cmd.Kind == CommandKind.Primary ? GameButtonVariant.Primary : GameButtonVariant.Secondary, isCompact: true)
                    {
                        Margin = new Thickness(0, 0, 4, 4)
                    };
                    var cid = cmd.Id;
                    b.Click += (_, _) => _onCommand(cid);
                    bp.Children.Add(b);
                }
                s.Children.Add(bp);
            }
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
