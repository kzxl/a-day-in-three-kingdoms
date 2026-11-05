using System.Windows.Input;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SimT.Contracts.Playing;
using SimT.Host.Wpf.Controls;

namespace SimT.Host.Wpf;

public partial class NpcDetailWindow
{
    private void PopulateInnateStats()
    {
        InnateStatsGrid.Children.Clear();
        var a = _npc.Attributes;
        var sameCity = (_npcCitadelId == _playerCityId);
        var canInspect = _hasMet || _isProtagonist || sameCity;

        if (!canInspect)
        {
            UnmetInnateNotice.Visibility = Visibility.Visible;
            AddMaskedStatItem(InnateStatsGrid, "🩸 CON · Căn Cốt", "Cơ mật bản thể (Cần diện kiến)");
            AddMaskedStatItem(InnateStatsGrid, "🧘 WIL · Định Lực", "Ý chí chưa từng bộc lộ");
            AddMaskedStatItem(InnateStatsGrid, "👁️ PRC · Tuệ Nhãn", "Cơ biến chưa thể kiểm chứng");
            AddMaskedStatItem(InnateStatsGrid, "✨ LCK · Khí Vận", "Phúc duyên trời ban bí mật");

            ProtagonistVitalsPanel.Visibility = Visibility.Collapsed;
            NpcLifestylePanel.Visibility = Visibility.Visible;
            LifestyleWealthText.Text = "Kín đáo (Chưa rõ)";
            LifestyleReputationText.Text = _activity is not null ? $"★ Uy danh {_activity.Reputation}" : "Danh sĩ Tam Quốc";
            LifestyleActionText.Text = "Hành tung bất định ở phương xa";
            PersonalityDetailText.Text = "Khí chất bình ổn, chưa bộc lộ dã tâm hay khuynh hướng đặc biệt.";
            return;
        }

        UnmetInnateNotice.Visibility = Visibility.Collapsed;
        var isFullyRevealed = _hasMet || _isProtagonist || (sameCity && _playerPrc >= 85);
        AddStatItem(InnateStatsGrid, "🩸 CON · Căn Cốt", a.Con, 0, "Thể phách sinh mệnh, quyết định HP & Thể lực tối đa", isFullyRevealed, _exps?.Con);
        AddStatItem(InnateStatsGrid, "🧘 WIL · Định Lực", a.Wil, 0, "Ý chí kiên định, chống hoảng loạn, giảm stress & phục hồi thể lực", isFullyRevealed, _exps?.Wil);
        AddStatItem(InnateStatsGrid, "👁️ PRC · Tuệ Nhãn", a.Prc, 0, "Cơ biến mẫn tiệp, phát giác phục kích & giám định thương phẩm", isFullyRevealed, _exps?.Prc);
        AddStatItem(InnateStatsGrid, "✨ LCK · Khí Vận", a.Lck, 0, "Phúc duyên trời ban, bạo kích kỳ ngộ thương trường & bảo mệnh", isFullyRevealed, _exps?.Lck);

        if (_isProtagonist)
        {
            ProtagonistVitalsPanel.Visibility = Visibility.Visible;
            NpcLifestylePanel.Visibility = Visibility.Collapsed;

            if (_protagonistExtra is not null)
            {
                var v = _protagonistExtra.Vitals;
                var s = _protagonistExtra.Social;

                // HP
                ProtagonistHpValText.Text = $"{v.Health}/{v.MaxHp}";
                var hpRatio = v.MaxHp > 0 ? (double)v.Health / v.MaxHp : 1.0;
                var hpBrush = (Brush)(hpRatio > 0.4 ? FindResource("Gain") : FindResource("Loss"));
                ProtagonistHpGauge.SetProgress(v.Health, v.MaxHp, hpBrush);

                // Stamina
                ProtagonistStaminaValText.Text = $"{v.CurrentStamina}/{v.MaxStamina}";
                ProtagonistStaminaGauge.SetProgress(v.CurrentStamina, v.MaxStamina, new SolidColorBrush(Color.FromRgb(0x4A, 0x90, 0xE2)));

                // Fatigue
                ProtagonistFatigueValText.Text = $"{v.Fatigue}%";
                var fatBrush = (Brush)(v.Fatigue >= 70 ? FindResource("Loss") : FindResource("Accent"));
                ProtagonistFatigueGauge.SetProgress(v.Fatigue, 100.0, fatBrush);

                // Stress
                ProtagonistStressValText.Text = $"{v.Stress}%";
                var stressBrush = (Brush)(v.Stress >= 70 ? FindResource("Loss") : FindResource("Muted"));
                ProtagonistStressGauge.SetProgress(v.Stress, 100.0, stressBrush);

                // Holdings & Economics
                ProtagonistLiquidGoldText.Text = $"{_protagonistExtra.LiquidGold:N0} Quan";
                ProtagonistPortfolioText.Text = $"{_protagonistExtra.PortfolioValue:N0} Quan";

                var fame = s.Fame;
                var fameTitle = fame switch
                {
                    >= 5000 => "Thiên Hạ Vô Song",
                    >= 2500 => "Thiên Hạ Tri Danh",
                    >= 1200 => "Danh Chấn Hoa Hạ",
                    >= 600 => "Danh Chấn Một Vùng",
                    >= 300 => "Châu Huyện Chi Danh",
                    >= 100 => "Hào Kiệt Mới Nổi",
                    _ => "Vô Danh Tiểu Tốt"
                };
                ProtagonistFameBadgeText.Text = $"★ {fame:N0} ({fameTitle})";
            }
        }
        else
        {
            ProtagonistVitalsPanel.Visibility = Visibility.Collapsed;
            NpcLifestylePanel.Visibility = Visibility.Visible;

            LifestyleWealthText.Text = _activity is not null ? $"💰 {_activity.CurrentWealth:N0} Quan" : "Gia cảnh bình ổn";
            var repVal = _activity?.Reputation ?? Math.Clamp((int)((_npc.Attributes.War + _npc.Attributes.Ldr + _npc.Attributes.Int + _npc.Attributes.Pol + _npc.Attributes.Dip + _npc.Attributes.Com) * 0.8), 50, 950);
            var repTitle = repVal switch
            {
                >= 1200 => "Danh Chấn Hoa Hạ",
                >= 600 => "Danh Chấn Một Vùng",
                >= 300 => "Châu Huyện Chi Danh",
                >= 100 => "Hào Kiệt Mới Nổi",
                _ => "Vô Danh Tiểu Tốt"
            };
            LifestyleReputationText.Text = $"★ Uy danh {repVal:N0} ({repTitle})";
            LifestyleActionText.Text = _activity is not null ? $"⚡ {_activity.CurrentAction}" : "Tu tâm dưỡng tính";
            PersonalityDetailText.Text = string.IsNullOrWhiteSpace(_npc.Personality)
                ? "Khí chất bình ổn, chưa bộc lộ dã tâm hay khuynh hướng đặc biệt."
                : _npc.Personality;
        }
    }

    private void PopulateSkills()
    {
        SkillsListPanel.Children.Clear();

        var skills = _npc.Skills;
        var filtered = _selectedSkillCategory == "all"
            ? skills
            : skills.Where(s => s.Category.Equals(_selectedSkillCategory, StringComparison.OrdinalIgnoreCase)).ToList();

        SkillCountText.Text = $"{filtered.Count}/{skills.Count} tuyệt kỹ";

        ProtagonistMentorshipTip.Visibility = _isProtagonist ? Visibility.Visible : Visibility.Collapsed;

        if (filtered.Count == 0)
        {
            var msg = _selectedSkillCategory == "all"
                ? "Nhân sĩ này chưa bộc lộ đặc kỹ danh tướng riêng biệt."
                : "Chưa có tuyệt kỹ nào thuộc phân loại này.";

            SkillsListPanel.Children.Add(new TextBlock
            {
                Text = msg,
                Style = (Style)FindResource("Caption"),
                Foreground = (Brush)FindResource("Muted"),
                Margin = new Thickness(4, 6, 4, 6)
            });
            return;
        }

        foreach (var skill in filtered)
        {
            SkillsListPanel.Children.Add(new GameSkillCard(skill));
        }
    }

    private void SkillFilter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string cat)
        {
            _selectedSkillCategory = cat;

            // Highlight buttons
            SkillFilterAll.BorderBrush = (Brush)FindResource(cat == "all" ? "Accent" : "Line");
            SkillFilterMartial.BorderBrush = (Brush)FindResource(cat == "martial" ? "Accent" : "Line");
            SkillFilterStrategist.BorderBrush = (Brush)FindResource(cat == "strategist" ? "Accent" : "Line");
            SkillFilterGovernance.BorderBrush = (Brush)FindResource(cat == "governance" ? "Accent" : "Line");
            SkillFilterCommerce.BorderBrush = (Brush)FindResource(cat == "commerce" ? "Accent" : "Line");
            SkillFilterSpecial.BorderBrush = (Brush)FindResource(cat == "special" ? "Accent" : "Line");

            PopulateSkills();
        }
    }

    private void PopulateRelationship()
    {
        if (_isProtagonist)
        {
            ProtagonistSocialPanel.Visibility = Visibility.Visible;
            NpcSocialPanel.Visibility = Visibility.Collapsed;

            // Sworn Brothers
            SwornBrotherListPanel.Children.Clear();
            var brothers = _protagonistExtra?.SwornBrothers ?? Array.Empty<SwornBrotherDef>();
            SwornBrotherCountText.Text = brothers.Count > 0 ? $"{brothers.Count} Huynh đệ" : "Chưa kết nghĩa";

            if (brothers.Count == 0)
            {
                SwornBrotherListPanel.Children.Add(new TextBlock
                {
                    Text = "Chưa kết nghĩa đào viên cùng ai. Hãy đàm đạo, tặng quà với nhân sĩ có chí hướng để thề ước kim lan vào sinh ra tử!",
                    Style = (Style)FindResource("Caption"),
                    Foreground = (Brush)FindResource("Muted"),
                    Margin = new Thickness(4, 4, 4, 4)
                });
            }
            else
            {
                foreach (var b in brothers)
                {
                    var name = _protagonistExtra?.NpcNameLookup?.Invoke(b.NpcId) ?? $"Huynh đệ #{b.NpcId}";
                    var card = new GameBondCard();
                    card.ConfigureSwornBrother(name, b.BondTitle, b.OathDate.ToString(), b.AssistCount);
                    SwornBrotherListPanel.Children.Add(card);
                }
            }

            // Retainers
            RetainerListPanel.Children.Clear();
            var retainers = _protagonistExtra?.Retainers ?? Array.Empty<RetainerDef>();
            RetainerCountText.Text = retainers.Count > 0 ? $"{retainers.Count} Môn hạ" : "Chưa có môn hạ";

            if (retainers.Count == 0)
            {
                RetainerListPanel.Children.Add(new TextBlock
                {
                    Text = "Chưa có gia thần phục vụ. Hãy nâng hảo cảm với nhân sĩ lên mức thâm giao (>= 35) để chiêu mộ về trướng hạ!",
                    Style = (Style)FindResource("Caption"),
                    Foreground = (Brush)FindResource("Muted"),
                    Margin = new Thickness(4, 4, 4, 4)
                });
            }
            else
            {
                foreach (var r in retainers)
                {
                    var name = _protagonistExtra?.NpcNameLookup?.Invoke(r.NpcId) ?? $"Môn hạ #{r.NpcId}";
                    var card = new GameBondCard();
                    card.ConfigureRetainer(name, r.RoleAssigned, r.MonthlyStipend, r.Loyalty, r.RecruitedDate.ToString());
                    RetainerListPanel.Children.Add(card);
                }
            }

            // Family
            if (_protagonistExtra is not null && _protagonistExtra.IsMarried)
            {
                LineageFamilyStatusText.Text = $"💍 Đã kết tóc se duyên cùng phu thê [{_protagonistExtra.SpouseName ?? "Hiền thê"}]. Con cháu thừa tự: {_protagonistExtra.ChildCount} người.";
            }
            else
            {
                LineageFamilyStatusText.Text = "Chưa lập gia thất. Lữ khách độc hành bốn bể, một lòng bôn ba gây dựng sự nghiệp.";
            }

            return;
        }

        // NPC View
        ProtagonistSocialPanel.Visibility = Visibility.Collapsed;
        NpcSocialPanel.Visibility = Visibility.Visible;

        if (_hasMet)
        {
            var aff = _relationship?.Affinity ?? 0;
            var affSign = aff >= 0 ? "+" : "";
            KizunaAffinityDetail.Text = $"♥ {affSign}{aff}/100 ({_relationship?.Impression ?? "Sơ giao"})";

            AffinityBarText.Text = $"Hảo cảm {affSign}{aff} · Tin cậy {_relationship?.Trust ?? 50}/100";
            var affBrush = new SolidColorBrush(aff >= 0 ? Color.FromRgb(0xFF, 0x6B, 0x8B) : Color.FromRgb(0x8A, 0x97, 0xA8));
            AffinityGauge.SetProgress(aff + 100, 200.0, affBrush);

            if (_retainer is not null)
            {
                KizunaRetainerDetail.Text = $"🛡️ {_retainer.RoleAssigned} (Trung thành {_retainer.Loyalty}/100)";
            }
            else
            {
                KizunaRetainerDetail.Text = aff >= 35 ? "🤝 Có thể chiêu mộ" : "Chưa chiêu mộ";
            }

            var gifts = _relationship?.GiftCount ?? 0;
            KizunaMemoryText.Text = gifts > 0
                ? $"Đã kết giao tương thức. Đã biếu tặng {gifts} món quà phẩm."
                : "Đã kết giao tương thức qua đàm đạo.";

            // Memories Log
            MemoriesListPanel.Children.Clear();
            if (_memories is not null && _memories.Count > 0)
            {
                MemoriesLogBorder.Visibility = Visibility.Visible;
                foreach (var m in _memories)
                {
                    var card = new GameBondCard();
                    card.ConfigureMemory(m.Date.ToString(), m.Topic, m.AffinityDelta, m.SummaryVi);
                    MemoriesListPanel.Children.Add(card);
                }
            }
            else
            {
                MemoriesLogBorder.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            KizunaAffinityDetail.Text = "Chưa từng diện kiến (0/100 Hảo cảm)";
            KizunaRetainerDetail.Text = "Chưa thể chiêu mộ";
            AffinityBarText.Text = "Chưa có dữ liệu";
            AffinityGauge.SetProgress(0, 100.0, (Brush)FindResource("Line"));
            KizunaMemoryText.Text = "Chưa từng gặp gỡ hay trò chuyện. Hãy bấm 'Bái Phỏng' để bắt đầu kết giao!";
            MemoriesLogBorder.Visibility = Visibility.Collapsed;
        }
    }

    public void ConfigureFooterActions()
    {
        if (_isProtagonist)
        {
            MeetNpcButton.Visibility = Visibility.Collapsed;
            DuelChallengeButton.Visibility = Visibility.Collapsed;
            return;
        }

        var sameCity = (_npcCitadelId == _playerCityId);

        // Meet / Visit Button
        if (_hasMet)
        {
            MeetNpcButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            MeetNpcButton.Visibility = Visibility.Visible;
            if (sameCity)
            {
                MeetNpcButton.IsEnabled = true;
                MeetNpcButton.ToolTip = $"Bái phỏng đàm đạo cùng {_npc.NameVi} tại thành này.";
            }
            else
            {
                MeetNpcButton.IsEnabled = false;
                MeetNpcButton.ToolTip = $"{_npc.NameVi} hiện đang ở {_citadelName}. Cần du hành đến đó để bái phỏng.";
            }
        }

        // Duel Button: Must be same city AND must have met AND have martial stats AND player not exhausted
        DuelChallengeButton.Visibility = Visibility.Visible;

        if (!sameCity)
        {
            DuelChallengeButton.IsEnabled = false;
            DuelChallengeButton.ToolTip = $"Không thể so tài: {_npc.NameVi} hiện đang ở {_citadelName}, không cùng thành trì với bạn.";
        }
        else if (!_hasMet)
        {
            DuelChallengeButton.IsEnabled = false;
            DuelChallengeButton.ToolTip = $"Chưa thể so tài: Bạn chưa từng diện kiến {_npc.NameVi}. Hãy bấm '💬 Bái Phỏng' để làm quen trước!";
        }
        else if (_npc.Attributes.War < 30)
        {
            DuelChallengeButton.IsEnabled = false;
            DuelChallengeButton.ToolTip = $"{_npc.NameVi} theo đường văn nhã, không thông thạo đao kiếm (Võ lực < 30).";
        }
        else if (_playerFatigue >= 95 || _playerStamina < 10)
        {
            DuelChallengeButton.IsEnabled = false;
            DuelChallengeButton.ToolTip = $"Thể lực cạn kiệt (mệt mỏi {_playerFatigue}/100). Hãy nghỉ ngơi trước khi lên võ đài!";
        }
        else
        {
            DuelChallengeButton.IsEnabled = true;
            DuelChallengeButton.ToolTip = $"Khiêu chiến {_npc.NameVi} lên võ đài so tài võ nghệ!";
        }
    }


    private static void AddStatItem(
        Panel container,
        string label,
        int baseValue,
        int bonus,
        string tip,
        bool verified,
        int? exp = null)
    {
        var card = new GameStatCard();
        card.Configure(label, baseValue, bonus, tip, verified, exp);
        container.Children.Add(card);
    }

    private static void AddMaskedStatItem(Panel container, string label, string tip)
    {
        var card = new GameStatCard();
        card.ConfigureMasked(label, tip);
        container.Children.Add(card);
    }

    private static (int cp, string tier) CalculateCombatPower(CharacterAttributes a)
    {
        // Combat Power formula: WAR*30 + LDR*22 + INT*18 + CON*20 + WIL*15 + PRC*12 + CRA*8 + LCK*10
        var cp = (a.War * 30) + (a.Ldr * 22) + (a.Int * 18) + (a.Con * 20) + (a.Wil * 15) + (a.Prc * 12) + (a.Cra * 8) + (a.Lck * 10);
        var tier = cp switch
        {
            >= 10000 => "Thiên Hạ Vô Song 👑",
            >= 8500 => "Danh Tướng Tuyệt Luân ⭐",
            >= 7000 => "Nhất Phương Hào Kiệt ⚔️",
            >= 5500 => "Dũng Khí Đáng Khen 🛡️",
            >= 4000 => "Tầm Thường Sĩ Tốt 🏹",
            _ => "Sơ Xuất Mao Lư 🌿"
        };
        return (cp, tier);
    }

    private static string DetermineRoleTag(string role)
    {
        var r = role.ToLowerInvariant();
        if (r.Contains("tướng") || r.Contains("quân") || r.Contains("soái")) return "VÕ";
        if (r.Contains("sư") || r.Contains("mưu") || r.Contains("học")) return "VĂN";
        if (r.Contains("thương") || r.Contains("buôn") || r.Contains("bản")) return "THƯƠNG";
        if (r.Contains("chủ") || r.Contains("vương") || r.Contains("đế")) return "CHÚA";
        return "SĨ";
    }

    private void PopulateInventory()
    {
        InventoryListPanel.Children.Clear();

        if (!_isProtagonist)
        {
            InventoryTabItem.Visibility = Visibility.Collapsed;
            return;
        }

        InventoryTabItem.Visibility = Visibility.Visible;
        var bagItems = _protagonistExtra?.BagItems;

        if (bagItems is null || bagItems.Count == 0)
        {
            InventoryItemCountText.Text = "HÀNH TRANG THỰC TẾ (0 MẶT HÀNG)";
            InventoryValuationText.Text = "Trị giá: 0 Quan";
            var whText = _protagonistExtra?.WarehouseInfo is not null ? $"🏪 Kho: {_protagonistExtra.WarehouseInfo.Used}/{_protagonistExtra.WarehouseInfo.Capacity}" : "🏪 Kho: Chưa thuê";
        InventoryCityLabel.Text = $"📍 Tại: {_citadelName} · {whText}";

            var empty = new GameEmptyState(
                "Hành Trang Trống Không",
                "Trong túi đồ hiện không có vật phẩm hay nông sản nào tại thành này. Hãy ghé qua chợ, thương khố hoặc làm nhiệm vụ để mua bán.",
                "🎒");
            empty.Margin = new Thickness(0, 20, 0, 0);
            InventoryListPanel.Children.Add(empty);
            return;
        }

        var totalQty = bagItems.Sum(i => i.Quantity);
        var totalVal = bagItems.Sum(i => i.Quantity * i.UnitPrice);
        InventoryItemCountText.Text = $"HÀNH TRANG THỰC TẾ ({bagItems.Count} MẶT HÀNG · {totalQty} ĐƠN VỊ)";
        InventoryValuationText.Text = $"Trị giá: {totalVal:N0} Quan";
        InventoryCityLabel.Text = $"📍 Tại: {_citadelName}";

        foreach (var item in bagItems)
        {
            var card = CreateInventoryItemCard(item);
            InventoryListPanel.Children.Add(card);
        }
    }

    private FrameworkElement CreateInventoryItemCard(ProtagonistBagItem item)
    {
        var border = new Border
        {
            Background = (Brush)FindResource("Surface"),
            BorderBrush = (Brush)FindResource("Line"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 0, 0, 8),
            Cursor = Cursors.Hand,
            ToolTip = "Nhấp chuột để xem chi tiết kỳ vật, thông số & đồ giám"
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(165) });

        // 1. Icon Box
        var iconBorder = new Border
        {
            Width = 42,
            Height = 42,
            Background = (Brush)FindResource("SurfaceAlt"),
            BorderBrush = (Brush)FindResource("AccentSoft"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };
        iconBorder.Child = new TextBlock
        {
            Text = item.VisualIcon,
            FontSize = 22,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(iconBorder, 0);
        grid.Children.Add(iconBorder);

        // 2. Info stack
        var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 8, 0) };
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
        titleRow.Children.Add(new TextBlock
        {
            Text = item.NameVi,
            FontWeight = FontWeights.Bold,
            FontSize = 13.5,
            Foreground = (Brush)FindResource("Text")
        });
        titleRow.Children.Add(new Border
        {
            Background = (Brush)FindResource("SurfaceAlt"),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(5, 1, 5, 1),
            Margin = new Thickness(8, 0, 0, 0),
            Child = new TextBlock
            {
                Text = item.CategoryVi,
                FontSize = 10,
                Foreground = (Brush)FindResource("Accent")
            }
        });
        infoStack.Children.Add(titleRow);

        var subText = new TextBlock
        {
            Text = $"Đơn giá: {item.UnitPrice:N0} Quan  ·  Khối lượng: {item.WeightKg} Cân  ·  Hạn dùng: {(item.ShelfLifeDays > 0 ? $"{item.ShelfLifeDays}N" : "Vĩnh cửu")}",
            FontSize = 11,
            Foreground = (Brush)FindResource("Muted"),
            Margin = new Thickness(0, 3, 0, 0)
        };
        infoStack.Children.Add(subText);

        Grid.SetColumn(infoStack, 1);
        grid.Children.Add(infoStack);

        // 3. Right Action / Valuation Box
        var rightStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
        rightStack.Children.Add(new TextBlock
        {
            Text = $"x{item.Quantity}",
            FontWeight = FontWeights.Bold,
            FontSize = 14,
            Foreground = (Brush)FindResource("Text"),
            HorizontalAlignment = HorizontalAlignment.Right
        });

        var detailBtn = new Button
        {
            Content = "🔍 Xem Chi Tiết",
            Style = (Style)FindResource("ChipButton"),
            Margin = new Thickness(0, 4, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        detailBtn.Click += (s, e) =>
        {
            e.Handled = true;
            _protagonistExtra?.OnInspectItem?.Invoke(item.ItemId);
        };
        rightStack.Children.Add(detailBtn);

        Grid.SetColumn(rightStack, 2);
        grid.Children.Add(rightStack);

        border.Child = grid;

        // Hover effect
        border.MouseEnter += (s, e) => border.BorderBrush = (Brush)FindResource("Accent");
        border.MouseLeave += (s, e) => border.BorderBrush = (Brush)FindResource("Line");

        // Click on the entire card also opens detail
        border.MouseLeftButtonUp += (s, e) =>
        {
            _protagonistExtra?.OnInspectItem?.Invoke(item.ItemId);
        };

        return border;
    }
}
