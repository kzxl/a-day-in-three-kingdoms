using System.Windows;
using System.Windows.Media;
using SimT.Contracts.Playing;
using SimT.Core.Kernel;
using SimT.Host.Wpf.Controls;

namespace SimT.Host.Wpf;

public sealed record FreightContractVm(
    string TitleVi,
    string GuildName,
    string RewardText);

public partial class CaravanRouteDetailModalWindow : HubModalWindowBase
{
    private readonly SimKernel _kernel;
    private readonly long _fromCityId;
    private readonly long _toCityId;

    public CaravanRouteDetailModalWindow(SimKernel kernel, long fromCityId, long toCityId)
    {
        InitializeComponent();

        _kernel = kernel;
        _fromCityId = fromCityId;
        _toCityId = toCityId;

        PopulateRouteHub();
    }

    private void PopulateRouteHub()
    {
        var fromName = "Khởi Hành";
        var toName = "Đích Đến";

        if (_kernel.Capabilities.TryGet<IWorldMap>(CapabilityIds.WorldMap, out var map) && map is not null)
        {
            var f = map.Find(_fromCityId);
            var t = map.Find(_toCityId);
            if (f != null) fromName = f.NameVi;
            if (t != null) toName = t.NameVi;
        }

        OriginCityText.Text = fromName;
        DestCityText.Text = toName;

        var distance = (int)(Math.Abs(_fromCityId - _toCityId) * 120 + 200);
        var days = Math.Max(2, (distance / 100));
        var risk = Math.Clamp(10 + (int)(Math.Abs(_fromCityId - _toCityId) * 8), 10, 45);

        HubHeader.Configure(
            glyph: "🐎",
            category: "TIÊU CỤC VẬN TIÊU LIÊN CHÂU",
            title: $"{fromName} ➔ {toName}",
            subtitle: $"Lộ trình thương lộ quan ải · Dự kiến hành trình {days} ngày",
            rightBadgeTitle: $"★ CỰ LY: {distance:N0} DẶM",
            rightBadgeSub: $"Nguy cơ cướp bóc: {risk}%",
            accentColor: Color.FromRgb(0xE6, 0x7E, 0x22));

        RouteDistanceText.Text = $"{distance:N0} Dặm";
        RouteDaysText.Text = $"{days} Ngày";
        AmbushRiskText.Text = $"{risk}% {(risk <= 20 ? "Thấp" : (risk <= 35 ? "Vừa" : "Cao"))}";

        var fleetName = "Xa Mã Tiêu Đoàn";
        if (_kernel.Capabilities.TryGet<ICaravanGuild>(CapabilityIds.CaravanGuild, out var initialGuild) && initialGuild is not null)
        {
            fleetName = initialGuild.Fleet.NameVi;
        }
        CaravanTrailCanvas.SetRoute(fromName, toName, days, risk, fleetName);

        // Fleet and Crew
        if (_kernel.Capabilities.TryGet<ICaravanGuild>(CapabilityIds.CaravanGuild, out var guild) && guild is not null)
        {
            FleetTierText.Text = $"{guild.Fleet.NameVi}";
            RouteStatusText.Text = $"Cước phí vận chuyển cơ sở: {guild.FeePerUnit} Quan / đơn vị hàng";

            if (guild.Crew.EscortNpcId.HasValue && _kernel.Capabilities.TryGet<INpcRegistry>(CapabilityIds.NpcRegistry, out var reg) && reg != null)
            {
                var escort = reg.Find(guild.Crew.EscortNpcId.Value);
                if (escort != null)
                {
                    EscortNameText.Text = $"Tiêu Sư: {escort.NameVi} (Võ lực {escort.Attributes.War})";
                    EscortSubText.Text = "Hộ tống đắc lực: Giảm 80% rủi ro thảo khấu cướp tiêu trên đường.";
                }
            }

            // Contracts
            var routeContracts = guild.Contracts
                .Where(c => c.FromCityId == _fromCityId && c.ToCityId == _toCityId)
                .Select(c => new FreightContractVm(
                    $"Vận chuyển {c.Quantity} kiện hàng hóa",
                    c.GuildName,
                    $"+{c.RewardCopper:N0} Quan"))
                .ToList();

            if (routeContracts.Count == 0)
            {
                routeContracts.Add(new FreightContractVm("Hợp đồng chuyến hàng tự do", "Thương Hội Địa Phương", "+350 Quan"));
            }
            ContractsList.ItemsSource = routeContracts;
        }

        // Populate 2D Hardware-Accelerated Arbitrage Matrix
        var spreadItems = new List<ArbitrageSpreadItem>
        {
            new("RICE", "Gạo Lương Nông", "🌾", 10, 16, 2, 4, 40.0),
            new("SALT", "Muối Biển Tinh Chế", "🧂", 22, 32, 3, 7, 31.8),
            new("SILK", "Tơ Lụa Giang Nam", "🧵", 65, 95, 5, 25, 38.5),
            new("TEA", "Trà Xanh Thượng Hạng", "🍵", 28, 39, 3, 8, 28.6),
            new("HERB", "Dược Liệu Quý Sơn Cước", "🌿", 45, 68, 4, 19, 42.2),
            new("WINE", "Rượu Nho Tây Vực", "🏺", 80, 115, 6, 29, 36.3)
        };
        ArbitrageMatrix.SetMatrix(fromName, toName, spreadItems);
    }

    private void DispatchCaravan_Click(object sender, RoutedEventArgs e)
    {
        if (_kernel.Capabilities.TryGet<ICaravanGuild>(CapabilityIds.CaravanGuild, out var guild) && guild is not null)
        {
            var res = guild.Send(_fromCityId, _toCityId);
            MessageBox.Show(res.Message, "Phát Hàng Vận Tiêu", MessageBoxButton.OK, res.Ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
            _kernel.Save();
            NotifyHubStateChanged();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
