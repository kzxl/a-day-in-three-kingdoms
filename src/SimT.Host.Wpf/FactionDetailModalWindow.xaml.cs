using System.Windows;
using System.Windows.Media;
using SimT.Contracts.Playing;
using SimT.Core.Kernel;
using SimT.Host.Wpf.Controls;

namespace SimT.Host.Wpf;

public sealed record FactionTerritoryVm(
    string NameVi,
    string RegionVi,
    string DefenseText);

public sealed record FactionOfficerVm(
    long Id,
    string NameVi,
    string Role,
    string AvatarChar,
    string WarText);

public partial class FactionDetailModalWindow : HubModalWindowBase
{
    private readonly SimKernel _kernel;
    private readonly long _factionId;

    public FactionDetailModalWindow(SimKernel kernel, long factionId)
    {
        InitializeComponent();

        _kernel = kernel;
        _factionId = factionId;

        PopulateFactionHub();
    }

    private void PopulateFactionHub()
    {
        if (!_kernel.Capabilities.TryGet<IPoliticalCourt>(CapabilityIds.CourtOffice, out var court) || court is null)
        {
            return;
        }

        var faction = court.Factions.FirstOrDefault(f => f.Id == _factionId)
                      ?? (court.Factions.Count > 0 ? court.Factions[0] : null);
        if (faction == null) return;

        // 1. Resolve Color & Header
        Color factionColor;
        try
        {
            factionColor = (Color)ColorConverter.ConvertFromString(faction.ColorHex);
        }
        catch
        {
            factionColor = Color.FromRgb(0x4A, 0x90, 0xE2);
        }

        var capitalName = "Lạc Dương";
        if (_kernel.Capabilities.TryGet<IWorldMap>(CapabilityIds.WorldMap, out var map) && map is not null)
        {
            var cit = map.Find(faction.CapitalCityId);
            if (cit != null) capitalName = cit.NameVi;
        }

        HubHeader.Configure(
            glyph: "🚩",
            category: "THẾ LỰC CHƯ HẦU TAM QUỐC",
            title: faction.NameVi,
            subtitle: $"📍 Đô thành: {capitalName} · Tôn chỉ: {faction.IdealVi}",
            rightBadgeTitle: $"★ QUÂN LỰC: {faction.TroopCount:N0}",
            rightBadgeSub: $"Ngân khố: {faction.TreasuryCopper:N0} Quan",
            accentColor: factionColor);

        // 2. Leader
        var leaderName = "Minh Chủ Chư Hầu";
        var leaderAvatar = "主";
        NpcDef? leader = null;
        if (_kernel.Capabilities.TryGet<INpcRegistry>(CapabilityIds.NpcRegistry, out var registry) && registry is not null)
        {
            leader = registry.Find(faction.LeaderNpcId);
            if (leader != null)
            {
                leaderName = leader.NameVi;
                leaderAvatar = !string.IsNullOrEmpty(leader.NameVi) ? leader.NameVi[..1] : "主";
            }
        }

        LeaderAvatarText.Text = leaderAvatar;
        LeaderAvatarBox.BorderBrush = new SolidColorBrush(factionColor);
        LeaderNameText.Text = leaderName;
        FactionIdealText.Text = $"Lý Tưởng: {faction.IdealVi}";

        if (leader != null)
        {
            WarlordRadarChart.SetCoreStats(leader.Attributes);
        }
        else
        {
            WarlordRadarChart.SetCoreStats(new CharacterAttributes(
                War: 80, Ldr: 85, Int: 88, Pol: 82, Dip: 78, Com: 70, Log: 80, Cra: 65,
                Con: 80, Wil: 85, Prc: 75, Lck: 80));
        }

        FactionTroopsText.Text = $"{faction.TroopCount:N0}";
        FactionTreasuryText.Text = $"{faction.TreasuryCopper:N0} Quan";
        CapitalCityText.Text = capitalName;

        // 3. Controlled Territories
        var territories = new List<FactionTerritoryVm>();
        var governances = court.Governances.Where(g => g.FactionId == faction.Id).ToList();
        TerritoryCountText.Text = $"{governances.Count} Châu Quận";

        if (map != null)
        {
            var allegiances = new List<GameFactionInfluenceMapControl.CitadelAllegiance>();
            var allCitadels = map.Citadels;
            foreach (var c in allCitadels)
            {
                var gov = court.Governances.FirstOrDefault(g => g.CitadelId == c.Id);
                var fac = gov != null ? court.Factions.FirstOrDefault(f => f.Id == gov.FactionId) : null;
                var facColor = fac?.ColorHex ?? "#64748B";
                var isCap = faction.CapitalCityId == c.Id;

                var eco = map.GetEcosystem(c.Id);
                var normX = Math.Clamp((eco.MapX - 120.0) / 530.0, 0.05, 0.95);
                var normY = Math.Clamp((eco.MapY - 60.0) / 440.0, 0.05, 0.95);

                allegiances.Add(new GameFactionInfluenceMapControl.CitadelAllegiance(
                    c.Id,
                    c.NameVi,
                    c.Region,
                    normX,
                    normY,
                    gov?.FactionId ?? 0,
                    facColor,
                    isCap));
            }

            FactionInfluenceMap.SetData(allegiances, faction.Id, faction.ColorHex);

            foreach (var g in governances)
            {
                var cit = map.Find(g.CitadelId);
                if (cit != null)
                {
                    territories.Add(new FactionTerritoryVm(
                        cit.NameVi,
                        FormatRegion(cit.Region),
                        $"Phòng Thủ Cấp {g.DefenseStrength}"));
                }
            }
        }
        TerritoriesList.ItemsSource = territories;

        // 4. Officers List
        if (registry != null)
        {
            var factionNpcs = registry.All.Where(n => governances.Any(g => g.CitadelId == n.CitadelId)).Take(8).ToList();
            OfficersList.ItemsSource = factionNpcs.Select(n => new FactionOfficerVm(
                n.Id,
                n.NameVi,
                n.Role,
                !string.IsNullOrEmpty(n.NameVi) ? n.NameVi[..1] : "将",
                $"⚔ {n.Attributes.War}")).ToList();
        }

        // 5. Player Allegiance
        var isPlayerJoined = court.AllegianceFactionId == faction.Id;
        AllegianceStatusText.Text = isPlayerJoined
            ? "★ Bạn là bộ hạ trung thành của thế lực này"
            : (court.AllegianceFactionId > 0 ? "○ Bạn đang phụng sự phe phái khác" : "○ Bạn đang là hiệp khách tự do");
        AllegianceStatusText.Foreground = (Brush)(isPlayerJoined ? FindResource("Gain") : FindResource("Muted"));

        JoinFactionBtn.Content = isPlayerJoined ? "↩ Rời Bỏ Phe Phái" : "🤝 Gia Nhập Phe Phái";
        JoinFactionBtn.Style = (Style)FindResource(isPlayerJoined ? "GhostButton" : "PrimaryButton");
    }

    private static string FormatRegion(string r) => r switch
    {
        "trung_nguyen" => "Trung Nguyên",
        "tay_bac" => "Tây Bắc",
        "ha_bac" => "Hà Bắc",
        "giang_dong" => "Giang Đông",
        "kinh_so" => "Kinh Sở",
        "ba_thuc" => "Ba Thục",
        _ => "Châu Quận"
    };

    private void JoinFaction_Click(object sender, RoutedEventArgs e)
    {
        if (_kernel.Capabilities.TryGet<IPoliticalCourt>(CapabilityIds.CourtOffice, out var court) && court is not null)
        {
            if (court.AllegianceFactionId == _factionId)
            {
                var res = court.LeaveFaction();
                MessageBox.Show(res.Message, "Rời Phe Phái", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                var res = court.JoinFaction(_factionId);
                MessageBox.Show(res.Message, "Gia Nhập Phe Phái", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            _kernel.Save();
            PopulateFactionHub();
            NotifyHubStateChanged();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
