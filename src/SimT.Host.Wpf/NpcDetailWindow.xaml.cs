using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SimT.Contracts.Playing;

namespace SimT.Host.Wpf;

public sealed record ProtagonistExtraData(
    CharacterVitals Vitals,
    CharacterSocial Social,
    long LiquidGold,
    long PortfolioValue,
    IReadOnlyList<RetainerDef> Retainers,
    IReadOnlyList<SwornBrotherDef> SwornBrothers,
    bool IsMarried,
    string? SpouseName,
    int ChildCount,
    Func<long, string?>? NpcNameLookup = null);

public partial class NpcDetailWindow : Window
{
    private readonly long _npcId;
    private readonly NpcDef _npc;
    private readonly string _citadelName;
    private readonly long _npcCitadelId;
    private readonly long _playerCityId;
    private bool _hasMet;
    private readonly NpcStatDelta? _deltas;
    private readonly NpcActivityDef? _activity;
    private readonly RelationshipDef? _relationship;
    private readonly RetainerDef? _retainer;
    private readonly int _playerFatigue;
    private readonly int _playerStamina;
    private readonly bool _isProtagonist;
    private readonly CharacterAttributeExps? _exps;
    private readonly ProtagonistExtraData? _protagonistExtra;
    private readonly IReadOnlyList<MemoryEntry>? _memories;

    private string _selectedSkillCategory = "all";

    public Action<long>? OnChallengeDuel { get; set; }
    public Action<long>? OnMeetNpc { get; set; }

    public NpcDetailWindow(
        NpcDef npc,
        string citadelName,
        long npcCitadelId,
        long playerCityId,
        bool hasMet,
        NpcStatDelta? deltas = null,
        NpcActivityDef? activity = null,
        RelationshipDef? relationship = null,
        RetainerDef? retainer = null,
        int playerFatigue = 0,
        int playerStamina = 100,
        CharacterAttributeExps? exps = null,
        ProtagonistExtraData? protagonistExtra = null,
        IReadOnlyList<MemoryEntry>? memories = null)
    {
        InitializeComponent();
        DarkTitleBar.Apply(this);

        _npc = npc;
        _npcId = npc.Id;
        _citadelName = citadelName;
        _npcCitadelId = npcCitadelId;
        _playerCityId = playerCityId;
        _hasMet = hasMet;
        _deltas = deltas;
        _activity = activity;
        _relationship = relationship;
        _retainer = retainer;
        _playerFatigue = playerFatigue;
        _playerStamina = playerStamina;
        _isProtagonist = (npc.Id == 0 || npc.Code == "protagonist" || npc.Role == "Lữ Khách");
        _exps = exps;
        _protagonistExtra = protagonistExtra;
        _memories = memories;

        Populate();
    }

    public static void ShowNpc(
        Window owner,
        NpcDef npc,
        string citadelName,
        long npcCitadelId,
        long playerCityId,
        bool hasMet,
        NpcStatDelta? deltas = null,
        NpcActivityDef? activity = null,
        RelationshipDef? relationship = null,
        RetainerDef? retainer = null,
        int playerFatigue = 0,
        int playerStamina = 100,
        Action<long>? onChallengeDuel = null,
        Action<long>? onMeetNpc = null,
        CharacterAttributeExps? exps = null,
        ProtagonistExtraData? protagonistExtra = null,
        IReadOnlyList<MemoryEntry>? memories = null)
    {
        var window = new NpcDetailWindow(
            npc,
            citadelName,
            npcCitadelId,
            playerCityId,
            hasMet,
            deltas,
            activity,
            relationship,
            retainer,
            playerFatigue,
            playerStamina,
            exps,
            protagonistExtra,
            memories)
        {
            Owner = owner,
            OnChallengeDuel = onChallengeDuel,
            OnMeetNpc = onMeetNpc
        };

        window.ConfigureFooterActions();
        window.ShowDialog();
    }

    public static void ShowNpc(
        Window owner,
        NpcDef npc,
        string citadelName,
        NpcStatDelta? deltas = null,
        NpcActivityDef? activity = null,
        RelationshipDef? relationship = null,
        RetainerDef? retainer = null,
        Action<long>? onChallengeDuel = null,
        CharacterAttributeExps? exps = null,
        ProtagonistExtraData? protagonistExtra = null,
        IReadOnlyList<MemoryEntry>? memories = null)
    {
        ShowNpc(
            owner,
            npc,
            citadelName,
            npc.CitadelId,
            npc.CitadelId,
            hasMet: relationship is not null || onChallengeDuel is null,
            deltas,
            activity,
            relationship,
            retainer,
            playerFatigue: 0,
            playerStamina: 100,
            onChallengeDuel,
            onMeetNpc: null,
            exps: exps,
            protagonistExtra: protagonistExtra,
            memories: memories);
    }

    private void Populate()
    {
        Title = $"{_npc.NameVi} — Hồ Sơ Nhân Vật";

        // Avatar char: first letter of Han name or Vietnamese name
        var avatarChar = !string.IsNullOrWhiteSpace(_npc.NameHan) && _npc.NameHan.Length > 0
            ? _npc.NameHan[0].ToString()
            : _npc.NameVi.Length > 0 ? _npc.NameVi[0].ToString() : "人";
        AvatarCharText.Text = avatarChar;

        RoleBadgeSmall.Text = DetermineRoleTag(_npc.Role);

        NameText.Text = _npc.NameVi;
        HanNameText.Text = string.IsNullOrWhiteSpace(_npc.NameHan) ? "" : _npc.NameHan;
        StyleNameText.Text = string.IsNullOrWhiteSpace(_npc.StyleNameVi) ? "" : $"Tự: {_npc.StyleNameVi}";

        RoleText.Text = _npc.Role;
        CitadelText.Text = $"Ngụ tại: {_citadelName}";

        var sameCity = (_npcCitadelId == _playerCityId);
        UpdatePills(sameCity);

        PersonalitySummaryText.Text = string.IsNullOrWhiteSpace(_npc.Personality)
            ? "Khí chất bình ổn, phong thái điềm đạm."
            : $"Khí chất: {_npc.Personality}";

        // Calculate total stats and combat power
        var a = _npc.Attributes;
        var bonusTotal = _deltas?.TotalBonus ?? 0;
        var total = a.War + a.Ldr + a.Int + a.Pol + a.Dip + a.Com + a.Log + a.Cra + a.Con + a.Wil + a.Prc + a.Lck + bonusTotal;

        var (cp, cpTier) = CalculateCombatPower(a);
        CombatPowerText.Text = cp.ToString("N0");
        CombatTierText.Text = cpTier;

        if (_isProtagonist)
        {
            HeaderQuickVitalsPanel.Visibility = Visibility.Visible;
            if (_protagonistExtra is not null)
            {
                var v = _protagonistExtra.Vitals;
                var s = _protagonistExtra.Social;
                HeaderHpText.Text = $"❤️ HP {v.Health}/{v.MaxHp}";
                HeaderStaminaText.Text = $"⚡ Thể Lực {v.CurrentStamina}/{v.MaxStamina}";
                HeaderFameText.Text = $"★ Uy Danh {s.Fame}";
                HeaderAlignmentText.Text = $"⚖️ Đức {s.Virtue} · Tín {s.Credibility}";
            }
        }
        else
        {
            HeaderQuickVitalsPanel.Visibility = Visibility.Collapsed;
        }

        if (_hasMet || _isProtagonist)
        {
            TotalStatsLabel.Text = "TỔNG ĐIỂM";
            TotalStatsText.Text = total.ToString();
            TotalStatsMaxText.Text = "/ 1200";
        }
        else
        {
            TotalStatsLabel.Text = "ƯỚC LƯỢNG";
            var estimate = Math.Clamp((total / 50) * 50, 400, 1150);
            TotalStatsText.Text = $"~{estimate}";
            TotalStatsMaxText.Text = "danh tiếng";
        }

        // TAB 1: Radar Chart, 4-Branch Summary & Core Stats
        StatRadarChart.SetCoreStats(a, _deltas);
        ArchetypeTagText.Text = StatRadarChart.ArchetypeTitle;

        BranchMartialText.Text = $"{a.War + a.Ldr}";
        BranchStrategyText.Text = $"{a.Int + a.Pol}";
        BranchCommerceText.Text = $"{a.Com + a.Log}";
        BranchCraftText.Text = $"{a.Dip + a.Cra}";

        if (!_hasMet && !_isProtagonist)
        {
            UnmetStatsNotice.Visibility = Visibility.Visible;
        }
        else
        {
            UnmetStatsNotice.Visibility = Visibility.Collapsed;
        }

        CoreStatsGrid.Children.Clear();
        AddStatItem(CoreStatsGrid, "⚔️ WAR · Võ Lực", a.War, _deltas?.WarDelta ?? 0, "Sát thương chiến trận, trảm tướng lôi đài & bạo kích võ công", _hasMet || _isProtagonist, _exps?.War);
        AddStatItem(CoreStatsGrid, "🛡️ LDR · Thống Soái", a.Ldr, _deltas?.LdrDelta ?? 0, "Khả năng chỉ huy quân đoàn, giữ vững đội hình & sĩ khí", _hasMet || _isProtagonist, _exps?.Ldr);
        AddStatItem(CoreStatsGrid, "🧠 INT · Trí Tuệ", a.Int, _deltas?.IntDelta ?? 0, "Binh pháp mưu lược, thi triển & hóa giải kế sách quân sự", _hasMet || _isProtagonist, _exps?.Int);
        AddStatItem(CoreStatsGrid, "📜 POL · Chính Trị", a.Pol, _deltas?.PolDelta ?? 0, "Kinh bang tế thế, khai hoang, thu thuế & tuần tra trị an", _hasMet || _isProtagonist, _exps?.Pol);
        AddStatItem(CoreStatsGrid, "🗣️ DIP · Ngoại Giao", a.Dip, _deltas?.DipDelta ?? 0, "Du thuyết liên minh, thu phục lòng người & chiêu mộ hiền tài", _hasMet || _isProtagonist, _exps?.Dip);
        AddStatItem(CoreStatsGrid, "💰 COM · Thương Mại", a.Com, _deltas?.ComDelta ?? 0, "Mặc cả giá buôn, mở rộng thương đoàn & sinh lời giao thương", _hasMet || _isProtagonist, _exps?.Com);
        AddStatItem(CoreStatsGrid, "📦 LOG · Hậu Cần", a.Log, _deltas?.LogDelta ?? 0, "Vận tải lương thảo, quản lý kho tàng & hành quân viễn chinh", _hasMet || _isProtagonist, _exps?.Log);
        AddStatItem(CoreStatsGrid, "🔨 CRA · Kỹ Nghệ", a.Cra, _deltas?.CraDelta ?? 0, "Rèn đúc thần binh khí giới, xây dựng thành lũy & công trình", _hasMet || _isProtagonist, _exps?.Cra);

        // TAB 2: Innate Vitals & Lifestyle
        PopulateInnateStats();

        // TAB 3: Skills & Feats
        PopulateSkills();

        // TAB 4: Relationship & Biography
        PopulateRelationship();

        BioText.Text = string.IsNullOrWhiteSpace(_npc.BioVi)
            ? "Nhân sĩ danh tiếng thời Tam Quốc, hành trạng được dân gian ca tụng."
            : _npc.BioVi;
    }

    private void UpdatePills(bool sameCity)
    {
        if (_isProtagonist)
        {
            LocationPillText.Text = "ĐỒNG THÀNH";
            LocationPillText.Foreground = (Brush)FindResource("Gain");
            LocationPillBorder.BorderBrush = (Brush)FindResource("Gain");
            LocationPillBorder.ToolTip = "Đang ở thành trì hiện tại.";

            AcquaintancePillText.Text = "BẢN THÂN";
            AcquaintancePillText.Foreground = (Brush)FindResource("Accent");
            AcquaintancePillBorder.BorderBrush = (Brush)FindResource("Accent");
            AcquaintancePillBorder.ToolTip = "Hồ sơ chính nhân vật của bạn.";
            return;
        }

        // Location Pill
        if (sameCity)
        {
            LocationPillText.Text = "ĐỒNG THÀNH";
            LocationPillText.Foreground = (Brush)FindResource("Gain");
            LocationPillBorder.BorderBrush = (Brush)FindResource("Gain");
            LocationPillBorder.ToolTip = "Đang ở cùng thành trì với bạn.";
        }
        else
        {
            LocationPillText.Text = "PHƯƠNG XA";
            LocationPillText.Foreground = (Brush)FindResource("Muted");
            LocationPillBorder.BorderBrush = (Brush)FindResource("Line");
            LocationPillBorder.ToolTip = $"Đang ở {_citadelName}, không ở cùng thành trì với bạn.";
        }

        // Acquaintance Pill
        if (_hasMet)
        {
            AcquaintancePillText.Text = "ĐÃ DIỆN KIẾN";
            AcquaintancePillText.Foreground = (Brush)FindResource("Accent");
            AcquaintancePillBorder.BorderBrush = (Brush)FindResource("Accent");
            AcquaintancePillBorder.ToolTip = "Bạn đã từng gặp gỡ, đàm đạo với nhân sĩ này.";
        }
        else
        {
            AcquaintancePillText.Text = "CHƯA GẶP MẶT ❓";
            AcquaintancePillText.Foreground = (Brush)FindResource("Faint");
            AcquaintancePillBorder.BorderBrush = (Brush)FindResource("Line");
            AcquaintancePillBorder.ToolTip = "Bạn chưa từng gặp gỡ nhân sĩ này. Bấm 'Bái Phỏng' để làm quen.";
        }
    }

    private void PopulateInnateStats()
    {
        InnateStatsGrid.Children.Clear();
        var a = _npc.Attributes;

        if (!_hasMet && !_isProtagonist)
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
        AddStatItem(InnateStatsGrid, "🩸 CON · Căn Cốt", a.Con, 0, "Thể phách sinh mệnh, quyết định HP & Thể lực tối đa", true, _exps?.Con);
        AddStatItem(InnateStatsGrid, "🧘 WIL · Định Lực", a.Wil, 0, "Ý chí kiên định, chống hoảng loạn, giảm stress & phục hồi thể lực", true, _exps?.Wil);
        AddStatItem(InnateStatsGrid, "👁️ PRC · Tuệ Nhãn", a.Prc, 0, "Cơ biến mẫn tiệp, phát giác phục kích & giám định thương phẩm", true, _exps?.Prc);
        AddStatItem(InnateStatsGrid, "✨ LCK · Khí Vận", a.Lck, 0, "Phúc duyên trời ban, bạo kích kỳ ngộ thương trường & bảo mệnh", true, _exps?.Lck);

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
                RenderGauge(ProtagonistHpBarGrid, hpRatio, hpBrush);

                // Stamina
                ProtagonistStaminaValText.Text = $"{v.CurrentStamina}/{v.MaxStamina}";
                var stRatio = v.MaxStamina > 0 ? (double)v.CurrentStamina / v.MaxStamina : 1.0;
                RenderGauge(ProtagonistStaminaBarGrid, stRatio, new SolidColorBrush(Color.FromRgb(0x4A, 0x90, 0xE2)));

                // Fatigue
                ProtagonistFatigueValText.Text = $"{v.Fatigue}%";
                var fatRatio = v.Fatigue / 100.0;
                var fatBrush = (Brush)(v.Fatigue >= 70 ? FindResource("Loss") : FindResource("Accent"));
                RenderGauge(ProtagonistFatigueBarGrid, fatRatio, fatBrush);

                // Stress
                ProtagonistStressValText.Text = $"{v.Stress}%";
                var stValRatio = v.Stress / 100.0;
                var stressBrush = (Brush)(v.Stress >= 70 ? FindResource("Loss") : FindResource("Muted"));
                RenderGauge(ProtagonistStressBarGrid, stValRatio, stressBrush);

                // Holdings & Economics
                ProtagonistLiquidGoldText.Text = $"{_protagonistExtra.LiquidGold:N0} Quan";
                ProtagonistPortfolioText.Text = $"{_protagonistExtra.PortfolioValue:N0} Quan";

                var fame = s.Fame;
                var fameTitle = fame switch
                {
                    >= 1000 => "Thiên Hạ Vô Song",
                    >= 500 => "Danh Chấn Hoa Hạ",
                    >= 250 => "Châu Huyện Chi Danh",
                    >= 100 => "Hào Kiệt Mới Nổi",
                    _ => "Vô Danh Tiểu Tốt"
                };
                ProtagonistFameBadgeText.Text = $"★ {fame} ({fameTitle})";
            }
        }
        else
        {
            ProtagonistVitalsPanel.Visibility = Visibility.Collapsed;
            NpcLifestylePanel.Visibility = Visibility.Visible;

            LifestyleWealthText.Text = _activity is not null ? $"💰 {_activity.CurrentWealth:N0} Quan" : "Gia cảnh bình ổn";
            LifestyleReputationText.Text = _activity is not null ? $"★ Uy danh {_activity.Reputation}" : "★ Uy danh 50";
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
            SkillsListPanel.Children.Add(BuildSkillCard(skill));
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
                    var card = new Border
                    {
                        Background = (Brush)FindResource("SurfaceAlt"),
                        BorderBrush = (Brush)FindResource("Line"),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(6),
                        Padding = new Thickness(10, 6, 10, 6),
                        Margin = new Thickness(0, 0, 0, 6)
                    };
                    var dock = new DockPanel();
                    var right = new TextBlock
                    {
                        Text = $"⚔️ Trợ chiến: {b.AssistCount} trận",
                        Style = (Style)FindResource("Caption"),
                        Foreground = (Brush)FindResource("Accent"),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    DockPanel.SetDock(right, Dock.Right);
                    dock.Children.Add(right);

                    var info = new StackPanel();
                    info.Children.Add(new TextBlock
                    {
                        Text = $"🍑 {name} ({b.BondTitle})",
                        FontWeight = FontWeights.Bold,
                        Foreground = (Brush)FindResource("Text")
                    });
                    info.Children.Add(new TextBlock
                    {
                        Text = $"Thề ước: {b.OathDate}",
                        Style = (Style)FindResource("Caption"),
                        Foreground = (Brush)FindResource("Faint")
                    });
                    dock.Children.Add(info);
                    card.Child = dock;
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
                    var card = new Border
                    {
                        Background = (Brush)FindResource("SurfaceAlt"),
                        BorderBrush = (Brush)FindResource("Line"),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(6),
                        Padding = new Thickness(10, 6, 10, 6),
                        Margin = new Thickness(0, 0, 0, 6)
                    };
                    var dock = new DockPanel();
                    var right = new TextBlock
                    {
                        Text = $"Trung thành: {r.Loyalty}/100",
                        Style = (Style)FindResource("Caption"),
                        Foreground = (Brush)FindResource(r.Loyalty >= 80 ? "Gain" : "Accent"),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    DockPanel.SetDock(right, Dock.Right);
                    dock.Children.Add(right);

                    var info = new StackPanel();
                    info.Children.Add(new TextBlock
                    {
                        Text = $"🛡️ {name} · {r.RoleAssigned}",
                        FontWeight = FontWeights.Bold,
                        Foreground = (Brush)FindResource("Text")
                    });
                    info.Children.Add(new TextBlock
                    {
                        Text = $"Bổng lộc: {r.MonthlyStipend:N0} Quan/tháng · Gia nhập: {r.RecruitedDate}",
                        Style = (Style)FindResource("Caption"),
                        Foreground = (Brush)FindResource("Faint")
                    });
                    dock.Children.Add(info);
                    card.Child = dock;
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
            var affRatio = Math.Clamp((aff + 100) / 200.0, 0.05, 1.0);
            var affBrush = new SolidColorBrush(aff >= 0 ? Color.FromRgb(0xFF, 0x6B, 0x8B) : Color.FromRgb(0x8A, 0x97, 0xA8));
            RenderGauge(AffinityBarGrid, affRatio, affBrush);

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
                    var memBorder = new Border
                    {
                        Background = (Brush)FindResource("SurfaceAlt"),
                        BorderBrush = (Brush)FindResource("Line"),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(8, 4, 8, 4),
                        Margin = new Thickness(0, 0, 0, 4)
                    };
                    var dock = new DockPanel();
                    var deltaText = new TextBlock
                    {
                        Text = m.AffinityDelta != 0 ? $"{(m.AffinityDelta > 0 ? "+" : "")}{m.AffinityDelta} ♥" : "",
                        Style = (Style)FindResource("Caption"),
                        Foreground = (Brush)FindResource(m.AffinityDelta >= 0 ? "Gain" : "Loss"),
                        FontWeight = FontWeights.Bold,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    DockPanel.SetDock(deltaText, Dock.Right);
                    dock.Children.Add(deltaText);

                    var topicIcon = m.Topic switch
                    {
                        "chat" => "💬 Đàm đạo",
                        "gift" => "🎁 Tặng quà",
                        "recruit" => "🤝 Chiêu mộ",
                        _ => "⚡ Tương tác"
                    };
                    var desc = new TextBlock
                    {
                        Text = $"[{m.Date}] {topicIcon}: {m.SummaryVi}",
                        Style = (Style)FindResource("Caption"),
                        Foreground = (Brush)FindResource("Text")
                    };
                    dock.Children.Add(desc);
                    memBorder.Child = dock;
                    MemoriesListPanel.Children.Add(memBorder);
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
            RenderGauge(AffinityBarGrid, 0.5, (Brush)FindResource("Line"));
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

    private void MeetNpc_Click(object sender, RoutedEventArgs e)
    {
        OnMeetNpc?.Invoke(_npcId);
        _hasMet = true;
        UpdateAfterMeeting();
    }

    private void UpdateAfterMeeting()
    {
        UpdatePills(sameCity: true);
        PopulateInnateStats();
        PopulateRelationship();
        ConfigureFooterActions();

        // Update Total Stats text
        var a = _npc.Attributes;
        var bonusTotal = _deltas?.TotalBonus ?? 0;
        var total = a.War + a.Ldr + a.Int + a.Pol + a.Dip + a.Com + a.Log + a.Cra + a.Con + a.Wil + a.Prc + a.Lck + bonusTotal;
        TotalStatsLabel.Text = "TỔNG ĐIỂM";
        TotalStatsText.Text = total.ToString();
        TotalStatsMaxText.Text = "/ 1200";

        UnmetStatsNotice.Visibility = Visibility.Collapsed;
    }

    private void DuelChallenge_Click(object sender, RoutedEventArgs e)
    {
        var id = _npcId;
        Close();
        OnChallengeDuel?.Invoke(id);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private UIElement BuildSkillCard(NpcSkillDef skill)
    {
        var card = new Border
        {
            Background = (Brush)FindResource("SurfaceAlt"),
            BorderBrush = (Brush)FindResource("Line"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 0, 0, 8)
        };

        var stack = new StackPanel();
        var headerDock = new DockPanel();

        var badgeStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(badgeStack, Dock.Right);

        var stars = new string('★', Math.Clamp(skill.Tier, 1, 5));
        badgeStack.Children.Add(new TextBlock
        {
            Text = stars,
            Foreground = (Brush)FindResource("Gain"),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        });

        var (catName, catBrush) = skill.Category switch
        {
            "martial" => ("Võ Đạo", (Brush)new SolidColorBrush(Color.FromRgb(0xE0, 0x6C, 0x75))),
            "strategist" => ("Mưu Lược", (Brush)new SolidColorBrush(Color.FromRgb(0x4D, 0x96, 0xFF))),
            "governance" => ("Trị Quốc", (Brush)new SolidColorBrush(Color.FromRgb(0x5A, 0xC0, 0x8A))),
            "commerce" => ("Thương Mại", (Brush)FindResource("Accent")),
            _ => ("Kỳ Tài", (Brush)new SolidColorBrush(Color.FromRgb(0xBD, 0x93, 0xF9)))
        };

        var catBorder = new Border
        {
            Background = (Brush)FindResource("Surface"),
            BorderBrush = (Brush)FindResource("Line"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2, 6, 2)
        };
        catBorder.Child = new TextBlock
        {
            Text = catName,
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            Foreground = catBrush
        };
        badgeStack.Children.Add(catBorder);

        headerDock.Children.Add(badgeStack);

        headerDock.Children.Add(new TextBlock
        {
            Text = skill.NameVi,
            FontSize = 13.5,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("Text")
        });

        stack.Children.Add(headerDock);

        stack.Children.Add(new TextBlock
        {
            Text = skill.Description,
            Style = (Style)FindResource("Caption"),
            Foreground = (Brush)FindResource("Muted"),
            Margin = new Thickness(0, 4, 0, 2),
            TextWrapping = TextWrapping.Wrap
        });

        var effectBorder = new Border
        {
            Background = (Brush)FindResource("AccentSoft"),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(6, 2, 6, 2),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 4, 0, 0)
        };
        effectBorder.Child = new TextBlock
        {
            Text = $"⚡ Hiệu Quả: {skill.EffectSummary}",
            FontSize = 10.5,
            FontWeight = FontWeights.Medium,
            Foreground = (Brush)FindResource("Accent")
        };
        stack.Children.Add(effectBorder);

        card.Child = stack;
        return card;
    }

    private void AddStatItem(
        Panel container,
        string label,
        int baseValue,
        int bonus,
        string tip,
        bool verified,
        int? exp = null)
    {
        var effective = baseValue + bonus;
        var rank = effective switch
        {
            >= 95 => "S+",
            >= 90 => "S",
            >= 80 => "A",
            >= 70 => "B",
            >= 60 => "C",
            _ => "D"
        };

        var barBrush = (Brush)(effective >= 90
            ? FindResource("Accent")
            : effective >= 80
                ? FindResource("Gain")
                : effective >= 70
                    ? new SolidColorBrush(Color.FromRgb(0x4A, 0x90, 0xE2))
                    : FindResource("Muted"));

        var expBrush = (Brush)FindResource("Accent");

        // Rich dark-themed ToolTip description
        var tooltipBuilder = new System.Text.StringBuilder();
        tooltipBuilder.AppendLine(label);
        tooltipBuilder.AppendLine(tip);
        if (verified)
        {
            tooltipBuilder.AppendLine();
            tooltipBuilder.Append($"📊 Chỉ số: {effective} điểm (Bậc {rank})");
            if (bonus > 0)
            {
                tooltipBuilder.Append($" [Cơ bản: {baseValue}, Ngoại vật/Khí vận: +{bonus}]");
            }
            tooltipBuilder.AppendLine();
        }
        if (exp.HasValue)
        {
            var curExp = Math.Clamp(exp.Value, 0, 99);
            var needed = 100 - curExp;
            tooltipBuilder.AppendLine($"⭐ Kinh nghiệm chỉ số: {curExp}/100 EXP");
            tooltipBuilder.Append($"⚡ Tích lũy thêm {needed} EXP để thăng cấp lên {effective + 1}.");
        }

        var card = new Border
        {
            Background = (Brush)FindResource("SurfaceAlt"),
            BorderBrush = (Brush)FindResource("Line"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(4, 4, 4, 4),
            ToolTip = tooltipBuilder.ToString().TrimEnd()
        };

        var stack = new StackPanel();
        var line = new DockPanel();

        var valueStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(valueStack, Dock.Right);

        var displayVal = verified ? effective.ToString() : (effective >= 80 ? $"~{(effective / 5) * 5}" : "???");

        var valueText = new TextBlock
        {
            Text = displayVal,
            Style = (Style)FindResource("Number"),
            FontWeight = FontWeights.Bold,
            Foreground = barBrush,
            FontSize = 14
        };
        valueStack.Children.Add(valueText);

        if (bonus > 0 && verified)
        {
            var bonusText = new TextBlock
            {
                Text = $" (+{bonus})",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("Gain"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(3, 0, 0, 0)
            };
            valueStack.Children.Add(bonusText);
        }

        line.Children.Add(valueStack);

        var rankBorder = new Border
        {
            Background = (Brush)FindResource("Surface"),
            BorderBrush = (Brush)FindResource("Line"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(5, 1, 5, 1),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(rankBorder, Dock.Right);
        rankBorder.Child = new TextBlock
        {
            Text = verified ? rank : (effective >= 80 ? rank : "?"),
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            Foreground = barBrush
        };
        line.Children.Add(rankBorder);

        var labelText = new TextBlock
        {
            Text = label,
            Style = (Style)FindResource("Caption"),
            Foreground = (Brush)FindResource("Text"),
            VerticalAlignment = VerticalAlignment.Center
        };
        line.Children.Add(labelText);
        stack.Children.Add(line);

        if (exp.HasValue)
        {
            // EXP Sub-header: "EXP CHỈ SỐ" & "xx/100"
            var curExp = Math.Clamp(exp.Value, 0, 99);
            var expLine = new DockPanel { Margin = new Thickness(0, 5, 0, 2) };

            var expValText = new TextBlock
            {
                Text = $"{curExp}/100",
                Style = (Style)FindResource("Number"),
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = expBrush
            };
            DockPanel.SetDock(expValText, Dock.Right);
            expLine.Children.Add(expValText);

            var expLabelText = new TextBlock
            {
                Text = "EXP CHỈ SỐ",
                Style = (Style)FindResource("Overline"),
                FontSize = 9.5,
                Foreground = (Brush)FindResource("Faint"),
                VerticalAlignment = VerticalAlignment.Center
            };
            expLine.Children.Add(expLabelText);
            stack.Children.Add(expLine);

            // EXP Progress Track & Fill
            var track = new Border
            {
                Background = (Brush)FindResource("Surface"),
                BorderBrush = (Brush)FindResource("Line"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                Height = 6,
                Margin = new Thickness(0, 1, 0, 0)
            };

            var fillGrid = new Grid();
            var expRatio = Math.Clamp(curExp / 100.0, 0.0, 1.0);
            if (expRatio > 0.001)
            {
                fillGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(expRatio, GridUnitType.Star) });
                fillGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0 - expRatio, GridUnitType.Star) });

                var bar = new Border
                {
                    Background = expBrush,
                    CornerRadius = new CornerRadius(1)
                };
                Grid.SetColumn(bar, 0);
                fillGrid.Children.Add(bar);
            }
            track.Child = fillGrid;
            stack.Children.Add(track);
        }
        else
        {
            // NPC Stat Capacity Track & Fill
            var track = new Border
            {
                Background = (Brush)FindResource("Surface"),
                BorderBrush = (Brush)FindResource("Line"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                Height = 6,
                Margin = new Thickness(0, 6, 0, 0)
            };

            var fillGrid = new Grid();
            var ratio = Math.Clamp(effective / 120.0, 0.05, 1.0);
            fillGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ratio, GridUnitType.Star) });
            fillGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0 - ratio, GridUnitType.Star) });

            var bar = new Border
            {
                Background = barBrush,
                CornerRadius = new CornerRadius(1)
            };
            Grid.SetColumn(bar, 0);
            fillGrid.Children.Add(bar);

            track.Child = fillGrid;
            stack.Children.Add(track);
        }

        card.Child = stack;
        container.Children.Add(card);
    }

    private void AddMaskedStatItem(Panel container, string label, string tip)
    {
        var card = new Border
        {
            Background = (Brush)FindResource("SurfaceAlt"),
            BorderBrush = (Brush)FindResource("Line"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(4, 4, 4, 4),
            ToolTip = tip
        };

        var stack = new StackPanel();
        var line = new DockPanel();

        var valueText = new TextBlock
        {
            Text = "???",
            Style = (Style)FindResource("Number"),
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("Faint"),
            FontSize = 14
        };
        DockPanel.SetDock(valueText, Dock.Right);
        line.Children.Add(valueText);

        var rankBorder = new Border
        {
            Background = (Brush)FindResource("Surface"),
            BorderBrush = (Brush)FindResource("Line"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(5, 1, 5, 1),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(rankBorder, Dock.Right);
        rankBorder.Child = new TextBlock
        {
            Text = "🔒",
            FontSize = 10,
            Foreground = (Brush)FindResource("Faint")
        };
        line.Children.Add(rankBorder);

        var labelText = new TextBlock
        {
            Text = label,
            Style = (Style)FindResource("Caption"),
            Foreground = (Brush)FindResource("Muted"),
            VerticalAlignment = VerticalAlignment.Center
        };
        line.Children.Add(labelText);
        stack.Children.Add(line);

        var track = new Border
        {
            Background = (Brush)FindResource("Surface"),
            BorderBrush = (Brush)FindResource("Line"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2),
            Height = 6,
            Margin = new Thickness(0, 6, 0, 0),
            Opacity = 0.3
        };
        stack.Children.Add(track);

        card.Child = stack;
        container.Children.Add(card);
    }

    private static void RenderGauge(Grid grid, double ratio, Brush brush)
    {
        grid.Children.Clear();
        grid.ColumnDefinitions.Clear();

        var clamped = Math.Clamp(ratio, 0.0, 1.0);
        if (clamped < 0.005)
        {
            return;
        }

        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(clamped, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0 - clamped, GridUnitType.Star) });

        var bar = new Border
        {
            Background = brush,
            CornerRadius = new CornerRadius(1)
        };
        Grid.SetColumn(bar, 0);
        grid.Children.Add(bar);
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
}
