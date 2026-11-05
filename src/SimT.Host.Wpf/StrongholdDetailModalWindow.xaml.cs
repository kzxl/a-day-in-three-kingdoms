using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;
using SimT.Core.Kernel;

namespace SimT.Host.Wpf;

public partial class StrongholdDetailModalWindow : HubModalWindowBase
{
    private readonly SimKernel _kernel;
    private readonly IMilitaryCommand _military;
    private readonly long _strongholdId;
    private readonly string _citadelName;
    private StrongholdDef _stronghold;
    private BanditChiefDef _chief;
    private TroopBranch _selectedPlayerBranch = TroopBranch.Infantry;

    public StrongholdDetailModalWindow(
        SimKernel kernel,
        IMilitaryCommand military,
        long strongholdId,
        string citadelName)
    {
        InitializeComponent();

        _kernel = kernel;
        _military = military;
        _strongholdId = strongholdId;
        _citadelName = citadelName;

        _stronghold = _military.AllStrongholds().FirstOrDefault(s => s.Id == strongholdId)
                      ?? throw new ArgumentException($"Stronghold {strongholdId} not found.");
        _chief = _military.GetStrongholdChief(strongholdId);

        // Auto-select countering branch if possible
        _selectedPlayerBranch = _chief.Branch switch
        {
            TroopBranch.Infantry => TroopBranch.Cavalry,
            TroopBranch.Cavalry => TroopBranch.Archers,
            TroopBranch.Archers => TroopBranch.Infantry,
            _ => TroopBranch.Infantry
        };

        PopulateHub();
    }

    private void PopulateHub()
    {
        // 1. Configure GameHubHeader
        var accentColor = _stronghold.BanditFaction switch
        {
            "khan_vang" => Color.FromRgb(0xF5, 0xD0, 0x61), // Gold
            "hac_son" => Color.FromRgb(0x4A, 0x90, 0xE2),   // Sky blue / Indigo
            "bach_ba" => Color.FromRgb(0x5A, 0xC0, 0x8A),   // Jade green
            _ => Color.FromRgb(0xE6, 0x7E, 0x22)            // Outlaw orange
        };

        HubHeader.Configure(
            glyph: "⛰️",
            category: $"SÀO HUYỆT · {_chief.FactionVi.ToUpperInvariant()}",
            title: _stronghold.NameVi,
            subtitle: $"📍 Châu Quận {_citadelName} · Địa thế {_chief.TerrainNameVi}",
            rightBadgeTitle: $"★ NGUY HIỂM CẤP {_stronghold.DangerLevel} / 10",
            rightBadgeSub: $"Quân số: {_chief.TroopCount:N0} thảo khấu",
            accentColor: accentColor);

        // 2. Chief Profile
        ChiefAvatarText.Text = _chief.AvatarChar;
        ChiefAvatarBox.BorderBrush = new SolidColorBrush(accentColor);
        ChiefTitleText.Text = _chief.TitleVi;
        ChiefNameText.Text = _chief.NameVi;
        ChiefBranchText.Text = $"{TroopBranchHelper.Icon(_chief.Branch)} {TroopBranchHelper.NameVi(_chief.Branch)}";
        ChiefFactionText.Text = _chief.FactionVi;

        ChiefWarText.Text = _chief.War.ToString();
        ChiefLdrText.Text = _chief.Ldr.ToString();
        ChiefIntText.Text = _chief.Int.ToString();

        BanditTroopCountText.Text = $"{_chief.TroopCount:N0} Quân Sĩ";
        TerrainNameText.Text = _chief.TerrainNameVi;
        DangerLevelText.Text = $"Cấp {_stronghold.DangerLevel} / 10 (Thảo Khấu Thiện Chiến)";
        ChiefLoreText.Text = _chief.DescriptionVi;

        // 3. Loot & Rewards
        LootGoldText.Text = $"+{_stronghold.LootGold:N0} Quan";

        var rewardItemName = "Quân Lương";
        if (_kernel.Capabilities.TryGet<IItemCatalog>(CapabilityIds.ItemCatalog, out var catalog) && catalog is not null)
        {
            var item = catalog.FindByCode(_stronghold.ItemRewardCode);
            if (item != null) rewardItemName = item.NameVi;
        }
        ItemRewardText.Text = $"2 {rewardItemName}";
        FameRewardText.Text = $"+{_stronghold.DangerLevel * 15} Danh · +5 Đức";

        // 4. Historical Clearance Status & Cooldown
        var isUnderCooldown = false;
        if (_stronghold.LastClearedDate is not null)
        {
            var daysSince = _kernel.Today.DayIndex - _stronghold.LastClearedDate.Value.DayIndex;
            if (daysSince < 30)
            {
                isUnderCooldown = true;
                var remaining = 30 - daysSince;
                StatusBadgeIcon.Text = "🛡️";
                StatusBadgeTitle.Text = "Sào Huyệt Đã Bị Bình Định";
                StatusBadgeTitle.Foreground = (Brush)FindResource("Gain");
                StatusBadgeSub.Text = $"Đã tiễu phạt {_stronghold.ClearedCount} lần · Cần {remaining} ngày nữa tặc đồ mới phục hồi";
                AssaultExpeditionBtn.IsEnabled = false;
                AssaultExpeditionBtn.ToolTip = $"Sào huyệt chưa phục hồi lại lực lượng (còn {remaining} ngày).";
            }
        }

        if (!isUnderCooldown)
        {
            StatusBadgeIcon.Text = "⚠️";
            StatusBadgeTitle.Text = "Đang Hoạt Động Cướp Phá";
            StatusBadgeTitle.Foreground = (Brush)FindResource("Loss");
            StatusBadgeSub.Text = $"Đã tiễu phạt {_stronghold.ClearedCount} lần · Sẵn sàng xuất chinh bình tặc";
            AssaultExpeditionBtn.IsEnabled = true;
            AssaultExpeditionBtn.ToolTip = "Tiến hành xuất binh tiễu trừ trại chủ";
        }

        // 5. Player Led Troops Strength
        if (_kernel.Capabilities.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var attrs) && attrs is not null)
        {
            var p = attrs.Attributes;
            var playerTroops = Math.Max(600, (p.Ldr * 22) + (p.Con * 10));
            PlayerTroopStrengthText.Text = $"Quân Lực Phe Ta: {playerTroops:N0} Quân";
        }

        // 6. Refresh Branch Selection & Matchup Callout
        UpdateBranchSelectionUi();
    }

    private void UpdateBranchSelectionUi()
    {
        var accentBrush = (Brush)FindResource("Accent");
        var lineBrush = (Brush)FindResource("Line");

        BranchInfantryBtn.BorderBrush = _selectedPlayerBranch == TroopBranch.Infantry ? accentBrush : lineBrush;
        BranchCavalryBtn.BorderBrush = _selectedPlayerBranch == TroopBranch.Cavalry ? accentBrush : lineBrush;
        BranchArchersBtn.BorderBrush = _selectedPlayerBranch == TroopBranch.Archers ? accentBrush : lineBrush;

        BranchInfantryBtn.FontWeight = _selectedPlayerBranch == TroopBranch.Infantry ? FontWeights.ExtraBold : FontWeights.Normal;
        BranchCavalryBtn.FontWeight = _selectedPlayerBranch == TroopBranch.Cavalry ? FontWeights.ExtraBold : FontWeights.Normal;
        BranchArchersBtn.FontWeight = _selectedPlayerBranch == TroopBranch.Archers ? FontWeights.ExtraBold : FontWeights.Normal;

        var countersBandit = TroopBranchHelper.Counters(_selectedPlayerBranch, _chief.Branch);
        var banditCountersPlayer = TroopBranchHelper.Counters(_chief.Branch, _selectedPlayerBranch);

        if (countersBandit)
        {
            AffinityMatchupHeading.Text = "★ THẾ TRẬN ÁP ĐẢO (KHẮC CHẾ ĐỐI PHƯƠNG)";
            AffinityMatchupHeading.Foreground = (Brush)FindResource("Gain");
            AffinityMatchupDetail.Text = $"{TroopBranchHelper.Icon(_selectedPlayerBranch)} {TroopBranchHelper.NameVi(_selectedPlayerBranch)} của ta KHẮC CHẾ {TroopBranchHelper.Icon(_chief.Branch)} {TroopBranchHelper.NameVi(_chief.Branch)} của {浪Name(_chief.NameVi)} (+25% Sát Thương Binh Lực, -15% Thương Vong)!";
            AffinityCalloutBorder.BorderBrush = (Brush)FindResource("Gain");
        }
        else if (banditCountersPlayer)
        {
            AffinityMatchupHeading.Text = "⚠️ BẤT LỢI ĐỊA HÌNH & BINH CHỦNG (BỊ KHẮC CHẾ)";
            AffinityMatchupHeading.Foreground = (Brush)FindResource("Loss");
            AffinityMatchupDetail.Text = $"{TroopBranchHelper.Icon(_selectedPlayerBranch)} {TroopBranchHelper.NameVi(_selectedPlayerBranch)} của ta BỊ KHẮC CHẾ bởi {TroopBranchHelper.Icon(_chief.Branch)} {TroopBranchHelper.NameVi(_chief.Branch)} của giặc (-20% Sát Thương, +20% Tổn Thất)! Nên đổi binh chủng thích hợp.";
            AffinityCalloutBorder.BorderBrush = (Brush)FindResource("Loss");
        }
        else
        {
            AffinityMatchupHeading.Text = "⚖ QUY LUẬT TƯƠNG KHẮC BINH CHỦNG (NGANG SỨC)";
            AffinityMatchupHeading.Foreground = (Brush)FindResource("Accent");
            AffinityMatchupDetail.Text = $"{TroopBranchHelper.Icon(_selectedPlayerBranch)} {TroopBranchHelper.NameVi(_selectedPlayerBranch)} ngang tài ngang sức cùng {TroopBranchHelper.Icon(_chief.Branch)} {TroopBranchHelper.NameVi(_chief.Branch)} giặc. Kỵ khắc Bộ · Bộ khắc Cung · Cung khắc Kỵ.";
            AffinityCalloutBorder.BorderBrush = (Brush)FindResource("Line");
        }
    }

    private static string 浪Name(string name) => string.IsNullOrEmpty(name) ? "Giặc" : name;

    private void Branch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag } && Enum.TryParse<TroopBranch>(tag, true, out var b))
        {
            _selectedPlayerBranch = b;
            UpdateBranchSelectionUi();
        }
    }

    private void AssaultExpedition_Click(object sender, RoutedEventArgs e)
    {
        // Open visual BanditBattleWindow!
        var battleWindow = new BanditBattleWindow(
            _kernel,
            _military,
            _strongholdId,
            _selectedPlayerBranch,
            _citadelName)
        {
            Owner = this
        };

        battleWindow.HubStateChanged += () =>
        {
            NotifyHubStateChanged();
        };

        battleWindow.ShowDialog();

        // Refresh our local stronghold dossier after combat
        _stronghold = _military.AllStrongholds().FirstOrDefault(s => s.Id == _strongholdId) ?? _stronghold;
        PopulateHub();
        NotifyHubStateChanged();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
