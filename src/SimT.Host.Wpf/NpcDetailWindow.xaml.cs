using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SimT.Contracts.Playing;
using SimT.Host.Wpf.Controls;

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

        var canInspect = _hasMet || _isProtagonist || sameCity;

        if (canInspect)
        {
            TotalStatsLabel.Text = "TỔNG ĐIỂM";
            TotalStatsText.Text = total.ToString();
            TotalStatsMaxText.Text = "/ 1200";
            UnmetStatsNotice.Visibility = Visibility.Collapsed;
        }
        else
        {
            TotalStatsLabel.Text = "ƯỚC LƯỢNG";
            var estimate = Math.Clamp((total / 50) * 50, 400, 1150);
            TotalStatsText.Text = $"~{estimate}";
            TotalStatsMaxText.Text = "danh tiếng";
            UnmetStatsNotice.Visibility = Visibility.Visible;
        }

        // TAB 1: Radar Chart, 4-Branch Summary & Core Stats
        StatRadarChart.SetCoreStats(a, _deltas);
        ArchetypeTagText.Text = StatRadarChart.ArchetypeTitle;

        BranchMartialText.Text = $"{a.War + a.Ldr}";
        BranchStrategyText.Text = $"{a.Int + a.Pol}";
        BranchCommerceText.Text = $"{a.Com + a.Log}";
        BranchCraftText.Text = $"{a.Dip + a.Cra}";

        CoreStatsGrid.Children.Clear();
        AddStatItem(CoreStatsGrid, "⚔️ WAR · Võ Lực", a.War, _deltas?.WarDelta ?? 0, "Sát thương chiến trận, trảm tướng lôi đài & bạo kích võ công", canInspect, _exps?.War);
        AddStatItem(CoreStatsGrid, "🛡️ LDR · Thống Soái", a.Ldr, _deltas?.LdrDelta ?? 0, "Khả năng chỉ huy quân đoàn, giữ vững đội hình & sĩ khí", canInspect, _exps?.Ldr);
        AddStatItem(CoreStatsGrid, "🧠 INT · Trí Tuệ", a.Int, _deltas?.IntDelta ?? 0, "Binh pháp mưu lược, thi triển & hóa giải kế sách quân sự", canInspect, _exps?.Int);
        AddStatItem(CoreStatsGrid, "📜 POL · Chính Trị", a.Pol, _deltas?.PolDelta ?? 0, "Kinh bang tế thế, khai hoang, thu thuế & tuần tra trị an", canInspect, _exps?.Pol);
        AddStatItem(CoreStatsGrid, "🗣️ DIP · Ngoại Giao", a.Dip, _deltas?.DipDelta ?? 0, "Du thuyết liên minh, thu phục lòng người & chiêu mộ hiền tài", canInspect, _exps?.Dip);
        AddStatItem(CoreStatsGrid, "💰 COM · Thương Mại", a.Com, _deltas?.ComDelta ?? 0, "Mặc cả giá buôn, mở rộng thương đoàn & sinh lời giao thương", canInspect, _exps?.Com);
        AddStatItem(CoreStatsGrid, "📦 LOG · Hậu Cần", a.Log, _deltas?.LogDelta ?? 0, "Vận tải lương thảo, quản lý kho tàng & hành quân viễn chinh", canInspect, _exps?.Log);
        AddStatItem(CoreStatsGrid, "🔨 CRA · Kỹ Nghệ", a.Cra, _deltas?.CraDelta ?? 0, "Rèn đúc thần binh khí giới, xây dựng thành lũy & công trình", canInspect, _exps?.Cra);

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
}
