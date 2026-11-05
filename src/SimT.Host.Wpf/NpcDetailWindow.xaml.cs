using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SimT.Contracts.Playing;
using SimT.Host.Wpf.Controls;

namespace SimT.Host.Wpf;

public sealed record ProtagonistBagItem(
    long ItemId,
    string Code,
    string NameVi,
    string CategoryVi,
    string VisualIcon,
    int Quantity,
    long UnitPrice,
    int WeightKg,
    int ShelfLifeDays,
    string Summary);

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
    Func<long, string?>? NpcNameLookup = null,
    IReadOnlyList<ProtagonistBagItem>? BagItems = null,
    Action<long>? OnInspectItem = null,
    Func<IReadOnlyList<ProtagonistBagItem>>? ReloadBagItems = null,
    WarehouseInfo? WarehouseInfo = null,
    Func<long, int, (bool ok, string msg)>? OnQuickStore = null,
    Func<(bool ok, string msg)>? OnRentWarehouse = null);

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
    private readonly int _playerPrc;
    private readonly bool _hasBaLac;
    private readonly bool _hasNguyetDan;
    private readonly bool _hasVongKhi;

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
        IReadOnlyList<MemoryEntry>? memories = null,
        int playerPrc = 50,
        bool hasBaLac = false,
        bool hasNguyetDan = false,
        bool hasVongKhi = false)
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
        _playerPrc = playerPrc;
        _hasBaLac = hasBaLac;
        _hasNguyetDan = hasNguyetDan;
        _hasVongKhi = hasVongKhi;

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
        IReadOnlyList<MemoryEntry>? memories = null,
        int playerPrc = 50,
        bool hasBaLac = false,
        bool hasNguyetDan = false,
        bool hasVongKhi = false)
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
            memories,
            playerPrc,
            hasBaLac,
            hasNguyetDan,
            hasVongKhi)
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

        RoleText.Text = SimT.Contracts.Scenes.RoleTitleVi.Format(_npc.Role);
        CitadelText.Text = $"Ngụ tại: {_citadelName}";

        var sameCity = (_npcCitadelId == _playerCityId);
        UpdatePills(sameCity);

        PersonalitySummaryText.Text = $"Khí chất: {SimT.Contracts.Scenes.RoleTitleVi.FormatPersonality(_npc.Personality)}";

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

        var isFullyRevealed = _hasMet || _isProtagonist || (sameCity && _playerPrc >= 85) || _hasVongKhi;
        var isBehaviorRevealed = isFullyRevealed || (sameCity && (_playerPrc >= 70 || _hasBaLac)) || _hasBaLac;

        if (_isProtagonist || _hasMet)
        {
            UnmetStatsNotice.Visibility = Visibility.Collapsed;
        }
        else
        {
            UnmetStatsNotice.Visibility = Visibility.Visible;
            if (_hasVongKhi)
            {
                UnmetStatsNoticeText.Text = $"👁️ THIÊN NHÃN TOÀN TRI [VỌNG KHÍ THIÊN CƠ]: Tuyệt kỹ tối thượng nhìn thấu toàn diện! Toàn bộ 12 chỉ số và căn cốt bản thể của {_npc.NameVi} đã giải mã 100%.";
            }
            else if (_hasBaLac)
            {
                UnmetStatsNoticeText.Text = $"👁️ BÁ LẠC THẦN NHÃN [TUYỆT KỸ TRANG BỊ]: Nhìn thấu tài năng quân sự võ nghiệp! 8 chỉ số hành vi cốt lõi của {_npc.NameVi} đã được giải mã 100%.";
            }
            else if (_hasNguyetDan)
            {
                UnmetStatsNoticeText.Text = $"👁️ NGUYỆT ĐÁN GIÁM ĐỊNH [TUYỆT KỸ TRANG BỊ]: Thấu suốt tâm tính và tiểu sử! Tính cách ẩn và lai lịch sâu kín đã mở khóa hoàn toàn.";
            }
            else if (_playerPrc >= 85)
            {
                UnmetStatsNoticeText.Text = $"👁️ TUỆ NHÃN NHƯ ĐUỐC (PRC {_playerPrc}): Tuệ nhãn tuyệt đỉnh thấu triệt chân tướng! Toàn bộ 12 chỉ số của {_npc.NameVi} đã được giải mã chính xác 100%.";
            }
            else if (_playerPrc >= 70)
            {
                UnmetStatsNoticeText.Text = $"👁️ TUỆ NHÃN THÔNG TUỆ (PRC {_playerPrc}): Tuệ nhãn thức anh hùng! 8 chỉ số hành vi cốt lõi đã được nhìn thấu tường tận. Chỉ số căn cốt ước lượng với độ chuẩn xác cao.";
            }
            else if (_playerPrc >= 50)
            {
                UnmetStatsNoticeText.Text = $"👁️ TUỆ NHÃN MẪN TIỆP (PRC {_playerPrc}): Quan sát khí độ, ước lượng năng lực thực tế trong biên độ hẹp (±5 điểm). Bái phỏng để giải mã hoàn toàn.";
            }
            else
            {
                UnmetStatsNoticeText.Text = $"👁️ TUỆ NHÃN SƠ KHAI (PRC {_playerPrc}): Chỉ số hiển thị ước lượng đại khái (±10 điểm). Cần bái phỏng hoặc trau dồi Tuệ Nhãn để nhìn thấu.";
            }
        }

        if (isFullyRevealed)
        {
            TotalStatsLabel.Text = "TỔNG ĐIỂM";
            TotalStatsText.Text = total.ToString();
            TotalStatsMaxText.Text = "/ 1200";
        }
        else if (isBehaviorRevealed)
        {
            TotalStatsLabel.Text = "TỔNG ĐIỂM (TUỆ NHÃN)";
            TotalStatsText.Text = $"~{total / 10 * 10}";
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

        CoreStatsGrid.Children.Clear();
        AddStatItem(CoreStatsGrid, "⚔️ WAR · Võ Lực", a.War, _deltas?.WarDelta ?? 0, "Sát thương chiến trận, trảm tướng lôi đài & bạo kích võ công", isBehaviorRevealed, _exps?.War);
        AddStatItem(CoreStatsGrid, "🛡️ LDR · Thống Soái", a.Ldr, _deltas?.LdrDelta ?? 0, "Khả năng chỉ huy quân đoàn, giữ vững đội hình & sĩ khí", isBehaviorRevealed, _exps?.Ldr);
        AddStatItem(CoreStatsGrid, "🧠 INT · Trí Tuệ", a.Int, _deltas?.IntDelta ?? 0, "Binh pháp mưu lược, thi triển & hóa giải kế sách quân sự", isBehaviorRevealed, _exps?.Int);
        AddStatItem(CoreStatsGrid, "📜 POL · Chính Trị", a.Pol, _deltas?.PolDelta ?? 0, "Kinh bang tế thế, khai hoang, thu thuế & tuần tra trị an", isBehaviorRevealed, _exps?.Pol);
        AddStatItem(CoreStatsGrid, "🗣️ DIP · Ngoại Giao", a.Dip, _deltas?.DipDelta ?? 0, "Du thuyết liên minh, thu phục lòng người & chiêu mộ hiền tài", isBehaviorRevealed, _exps?.Dip);
        AddStatItem(CoreStatsGrid, "💰 COM · Thương Mại", a.Com, _deltas?.ComDelta ?? 0, "Mặc cả giá buôn, mở rộng thương đoàn & sinh lời giao thương", isBehaviorRevealed, _exps?.Com);
        AddStatItem(CoreStatsGrid, "📦 LOG · Hậu Cần", a.Log, _deltas?.LogDelta ?? 0, "Vận tải lương thảo, quản lý kho tàng & hành quân viễn chinh", isBehaviorRevealed, _exps?.Log);
        AddStatItem(CoreStatsGrid, "🔨 CRA · Kỹ Nghệ", a.Cra, _deltas?.CraDelta ?? 0, "Rèn đúc thần binh khí giới, xây dựng thành lũy & công trình", isBehaviorRevealed, _exps?.Cra);

        // TAB 2: Innate Vitals & Lifestyle
        PopulateInnateStats();

        // TAB 3: Skills & Feats
        PopulateSkills();

        // TAB 4: Relationship & Biography
        PopulateRelationship();

        // TAB 5: Inventory & Items
        PopulateInventory();

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

    private void MeetNpc_Click(object sender, RoutedEventArgs e)
    {
        var id = _npcId;
        Close();
        OnMeetNpc?.Invoke(id);
        _hasMet = true;
        UpdateAfterMeeting();
    }

    private void UpdateAfterMeeting()
    {
        UpdatePills(sameCity: true);
        PopulateInnateStats();
        PopulateRelationship();
        ConfigureFooterActions();

        var a = _npc.Attributes;
        CoreStatsGrid.Children.Clear();
        AddStatItem(CoreStatsGrid, "⚔️ WAR · Võ Lực", a.War, _deltas?.WarDelta ?? 0, "Sát thương chiến trận, trảm tướng lôi đài & bạo kích võ công", true, _exps?.War);
        AddStatItem(CoreStatsGrid, "🛡️ LDR · Thống Soái", a.Ldr, _deltas?.LdrDelta ?? 0, "Khả năng chỉ huy quân đoàn, giữ vững đội hình & sĩ khí", true, _exps?.Ldr);
        AddStatItem(CoreStatsGrid, "🧠 INT · Trí Tuệ", a.Int, _deltas?.IntDelta ?? 0, "Binh pháp mưu lược, thi triển & hóa giải kế sách quân sự", true, _exps?.Int);
        AddStatItem(CoreStatsGrid, "📜 POL · Chính Trị", a.Pol, _deltas?.PolDelta ?? 0, "Kinh bang tế thế, khai hoang, thu thuế & tuần tra trị an", true, _exps?.Pol);
        AddStatItem(CoreStatsGrid, "🗣️ DIP · Ngoại Giao", a.Dip, _deltas?.DipDelta ?? 0, "Du thuyết liên minh, thu phục lòng người & chiêu mộ hiền tài", true, _exps?.Dip);
        AddStatItem(CoreStatsGrid, "💰 COM · Thương Mại", a.Com, _deltas?.ComDelta ?? 0, "Mặc cả giá buôn, mở rộng thương đoàn & sinh lời giao thương", true, _exps?.Com);
        AddStatItem(CoreStatsGrid, "📦 LOG · Hậu Cần", a.Log, _deltas?.LogDelta ?? 0, "Vận tải lương thảo, quản lý kho tàng & hành quân viễn chinh", true, _exps?.Log);
        AddStatItem(CoreStatsGrid, "🔨 CRA · Kỹ Nghệ", a.Cra, _deltas?.CraDelta ?? 0, "Rèn đúc thần binh khí giới, xây dựng thành lũy & công trình", true, _exps?.Cra);

        // Update Total Stats text
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

}
