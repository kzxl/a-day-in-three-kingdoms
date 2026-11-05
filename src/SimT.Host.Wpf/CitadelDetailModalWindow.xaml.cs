using System.Windows;
using System.Windows.Media;
using SimT.Contracts.Playing;
using SimT.Core.Kernel;

namespace SimT.Host.Wpf;

public sealed record ResidingNpcItemVm(
    long Id,
    string NameVi,
    string Role,
    string AvatarChar,
    string WarText);

public sealed record SublocationItemVm(
    string NameVi,
    string CategoryVi,
    string Glyph);

public partial class CitadelDetailModalWindow : HubModalWindowBase
{
    private readonly SimKernel _kernel;
    private readonly long _citadelId;

    public CitadelDetailModalWindow(SimKernel kernel, long citadelId)
    {
        InitializeComponent();

        _kernel = kernel;
        _citadelId = citadelId;

        PopulateCitadelHub();
    }

    private void PopulateCitadelHub()
    {
        if (!_kernel.Capabilities.TryGet<IWorldMap>(CapabilityIds.WorldMap, out var map) || map is null)
        {
            return;
        }

        var citadel = map.Find(_citadelId) ?? (map.Citadels.Count > 0 ? map.Citadels[0] : null);
        if (citadel == null) return;

        var isPlayerHere = false;
        if (_kernel.Capabilities.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var playerState) && playerState is not null)
        {
            isPlayerHere = playerState.CurrentCityId == citadel.Id;
        }

        // 1. Hub Header
        var regionName = citadel.Region switch
        {
            "trung_nguyen" => "Trung Nguyên Trực Lệ",
            "tay_bac" => "Tây Bắc Lương Châu",
            "ha_bac" => "Hà Bắc Ký U Tịnh",
            "giang_dong" => "Giang Đông Ngô Việt",
            "kinh_so" => "Kinh Sở Cửu Quận",
            "ba_thuc" => "Ba Thục Ích Châu",
            _ => "Trung Nguyên Châu Quận"
        };

        var order = 80;
        var defense = 7;
        string? factionName = "Triều Đình Hán Thất";
        string? governorName = "Thái Thú Triều Đình";

        if (_kernel.Capabilities.TryGet<IPoliticalCourt>(CapabilityIds.CourtOffice, out var court) && court is not null)
        {
            var gov = court.Governances.FirstOrDefault(g => g.CitadelId == citadel.Id);
            if (gov != null)
            {
                order = Math.Max(20, 100 - gov.UnrestLevel);
                defense = gov.DefenseStrength;
                var fac = court.Factions.FirstOrDefault(f => f.Id == gov.FactionId);
                if (fac != null) factionName = fac.NameVi;
            }
        }

        HubHeader.Configure(
            glyph: "🏯",
            category: $"CHÂU QUẬN · {regionName.ToUpperInvariant()}",
            title: citadel.NameVi,
            subtitle: $"📍 Vị thế: Cố đô thiên hạ · Trực thuộc {regionName}",
            rightBadgeTitle: $"★ TRỊ AN {order}% · THỦ CẤP {defense}",
            rightBadgeSub: isPlayerHere ? "● Bạn đang ngụ tại thành" : "○ Phương xa",
            accentColor: Color.FromRgb(0xF5, 0xD0, 0x61));

        // 2. Demographics & Metrics
        GovernorNameText.Text = governorName;
        FactionAffiliationText.Text = $"Thuộc Thế Lực: {factionName}";
        PopulationText.Text = $"{citadel.Id * 15000 + 85000:N0}";
        PublicOrderText.Text = $"{order}%";
        DefenseText.Text = $"Cấp {defense}";
        GranaryText.Text = order >= 70 ? "Dồi Dào" : (order >= 50 ? "Bình Thường" : "Thiếu Hụt");
        CitadelBioText.Text = $"Thành trì {citadel.NameVi} thuộc vùng đất {regionName}, nơi đô hội sầm uất với nhiều danh môn vọng tộc và thương nhân hội tụ.";

        // 3. Bandit Strongholds surrounding citadel
        if (_kernel.Capabilities.TryGet<IMilitaryCommand>(CapabilityIds.MilitaryCommand, out var military) && military is not null)
        {
            var localStrongholds = military.StrongholdsAt(citadel.Id);
            StrongholdCountText.Text = $"{localStrongholds.Count} Cứ Điểm";
            if (localStrongholds.Count > 0)
            {
                var names = string.Join(", ", localStrongholds.Select(s => $"{s.NameVi} (Cấp {s.DangerLevel})"));
                StrongholdSummaryText.Text = names;
            }
            else
            {
                StrongholdSummaryText.Text = "Khu vực ngoại thành thanh bình, chưa phát hiện cứ điểm thảo khấu nguy hiểm.";
            }
        }

        // 4. Residing NPCs
        if (_kernel.Capabilities.TryGet<INpcRegistry>(CapabilityIds.NpcRegistry, out var registry) && registry is not null)
        {
            var npcs = registry.FindByCitadel(citadel.Id);
            NpcCountHeader.Text = $"{npcs.Count} Nhân vật";
            ResidingNpcList.ItemsSource = npcs.Select(n => new ResidingNpcItemVm(
                n.Id,
                n.NameVi,
                n.Role,
                !string.IsNullOrEmpty(n.NameVi) ? n.NameVi[..1] : "武",
                $"⚔ {n.Attributes.War}")).ToList();
        }

        // 5. Sublocations
        var sublocs = map.Sublocations(citadel.Id);
        SublocationsList.ItemsSource = sublocs.Select(s => new SublocationItemVm(
            s.NameVi,
            s.Kind switch
            {
                "urban" => "Nội Thị",
                "water" => "Bến Thuyền",
                "rural" => "Ngoại Ô",
                _ => "Khu Vực"
            },
            s.Kind == "water" ? "⛵" : (s.Kind == "rural" ? "🌾" : "🏮"))).ToList();

        // 6. Action button presence
        LocationPresenceText.Text = isPlayerHere
            ? "📍 Bạn đang có mặt tại thành trì này"
            : "🗺️ Thành trì phương xa (Hãy dùng dịch chuyển trên bản đồ để đến)";
        LocationPresenceText.Foreground = (Brush)(isPlayerHere ? FindResource("Gain") : FindResource("Muted"));

        StrollCityBtn.IsEnabled = isPlayerHere;
        StrollCityBtn.ToolTip = isPlayerHere
            ? "Dạo bước ngắm cảnh phố phường thành trì này"
            : "Bạn không có mặt tại thành trì này!";
    }

    private void StrollCity_Click(object sender, RoutedEventArgs e)
    {
        if (_kernel.Capabilities.TryGet<ICityStroll>(CapabilityIds.CityStroll, out var stroll) && stroll is not null)
        {
            var strollModal = new CityStrollModalWindow(_kernel, stroll, _citadelId) { Owner = this };
            strollModal.StrollStateChanged += () =>
            {
                NotifyHubStateChanged();
                PopulateCitadelHub();
            };
            strollModal.ShowDialog();
            NotifyHubStateChanged();
            PopulateCitadelHub();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
