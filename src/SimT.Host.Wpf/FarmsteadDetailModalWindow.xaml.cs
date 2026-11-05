using System.Windows;
using System.Windows.Media;
using SimT.Contracts.Playing;
using SimT.Core.Kernel;

namespace SimT.Host.Wpf;

public partial class FarmsteadDetailModalWindow : HubModalWindowBase
{
    private readonly SimKernel _kernel;
    private readonly long _plotId;

    public FarmsteadDetailModalWindow(SimKernel kernel, long plotId)
    {
        InitializeComponent();

        _kernel = kernel;
        _plotId = plotId;

        PopulateFarmHub();
    }

    private void PopulateFarmHub()
    {
        if (!_kernel.Capabilities.TryGet<ICultivationEstate>(CapabilityIds.CultivationEstate, out var estate) || estate is null)
        {
            return;
        }

        var plot = estate.Plots.FirstOrDefault(p => p.Id == _plotId)
                   ?? (estate.Plots.Count > 0 ? estate.Plots[0] : null);
        if (plot == null) return;

        var citadelName = "Châu Quận";
        if (_kernel.Capabilities.TryGet<IWorldMap>(CapabilityIds.WorldMap, out var map) && map is not null)
        {
            var cit = map.Find(plot.CitadelId);
            if (cit != null) citadelName = cit.NameVi;
        }

        // 1. Hub Header
        HubHeader.Configure(
            glyph: "🌾",
            category: "ĐIỀN TRANG KHAI HOANG CANH TÁC",
            title: $"Thửa Ruộng #{plot.Id} · {citadelName}",
            subtitle: $"Địa phận ngoại ô {citadelName} · Tiền thuê: {plot.MonthlyRent} Quan/tháng",
            rightBadgeTitle: $"★ DIỆN TÍCH: {plot.AreaMu} MẪU",
            rightBadgeSub: plot.Rented ? "● Đang canh tác" : "○ Đất hoang",
            accentColor: Color.FromRgb(0x5A, 0xC0, 0x8A));

        // 2. Metrics & Soil
        PlotLocationText.Text = $"Vị trí: Ngoại Ô {citadelName}";
        PlotTenancyText.Text = plot.Rented ? "Trạng thái: Đã thuê canh tác" : "Trạng thái: Đất nhàn rỗi";
        PlotTenancyText.Foreground = (Brush)FindResource(plot.Rented ? "Gain" : "Muted");

        SoilFertilityText.Text = $"{plot.Soil} / 100";
        IrrigationLevelText.Text = $"Cấp {plot.IrrigationLevel} / 3";
        PlotAreaText.Text = $"{plot.AreaMu} Mẫu";
        MonthlyRentText.Text = $"{plot.MonthlyRent} Quan/tháng";

        // 3. Facilities
        var facilities = estate.GetFacilities(plot.CitadelId);
        FacilitiesList.ItemsSource = facilities;

        // 4. Crop Progress
        var remainingDays = (plot.CropStatus == "growing")
            ? (int)Math.Max(1L, (plot.ReadyDayIndex ?? _kernel.Today.DayIndex) - _kernel.Today.DayIndex)
            : 0;
        var cropDisplayName = plot.CropStatus == "ready" ? "Lúa Vàng Trĩu Hạt" : (plot.CropStatus == "growing" ? "Lúa Nước Kinh Sở" : "Chưa Gieo Trồng");
        CropGrowthVisualizer.SetStatus(
            cropName: cropDisplayName,
            cropStatus: plot.CropStatus?.ToUpperInvariant(),
            remainingDays: remainingDays,
            totalDays: 30,
            soilFertility: plot.Soil,
            irrigationLevel: plot.IrrigationLevel);

        if (plot.CropStatus == "growing")
        {
            CropNameText.Text = "Lúa Nước Đang Lớn";
            CropStatusBadgeText.Text = "Đang Phát Triển";
            CropProgressBar.Value = 65;
            var remaining = Math.Max(1, (plot.ReadyDayIndex ?? _kernel.Today.DayIndex) - _kernel.Today.DayIndex);
            HarvestDaysText.Text = $"⏳ Dự kiến thu hoạch sau: {remaining} ngày";
            EstimatedYieldText.Text = $"Sản lượng ước tính: ~{plot.YieldQuantity ?? 30} Thạch";

            HarvestBtn.IsEnabled = false;
            HarvestBtn.ToolTip = "Cây trồng chưa đến kỳ thu hoạch!";
            IrrigateBtn.IsEnabled = true;
            FertilizeBtn.IsEnabled = true;
        }
        else if (plot.CropStatus == "ready")
        {
            CropNameText.Text = "Lúa Vàng Trĩu Hạt";
            CropStatusBadgeText.Text = "ĐÃ ĐẾN MÙA GẶT";
            CropStatusBadge.Background = (Brush)FindResource("Gain");
            CropStatusBadgeText.Foreground = (Brush)FindResource("Text");
            CropProgressBar.Value = 100;
            HarvestDaysText.Text = "✨ Đã chín rộ! Có thể thu hoạch ngay";
            EstimatedYieldText.Text = $"Sản lượng: {plot.YieldQuantity ?? 40} Thạch";

            HarvestBtn.IsEnabled = true;
            HarvestBtn.ToolTip = "Tiến hành thu hoạch mùa vụ!";
            IrrigateBtn.IsEnabled = false;
            FertilizeBtn.IsEnabled = false;
        }
        else
        {
            CropNameText.Text = "Đất Đang Trống";
            CropStatusBadgeText.Text = "Chưa Gieo Hạt";
            CropProgressBar.Value = 0;
            HarvestDaysText.Text = "Hãy chọn hạt giống để gieo trồng";
            EstimatedYieldText.Text = "Sản lượng: 0";

            HarvestBtn.IsEnabled = false;
            IrrigateBtn.IsEnabled = false;
            FertilizeBtn.IsEnabled = false;
        }

        // 5. Supply Inventory
        var supplies = estate.GetSupplyInventory(plot.CitadelId);
        SeedsStockText.Text = $"🌾 Lúa: {supplies.RiceSeeds} | Trà: {supplies.TeaSeeds}";
        FertilizerStockText.Text = $"🌱 Phân hữu cơ: {supplies.FertilizerCount} bao";
        ToolsStockText.Text = $"⚒ Cuốc: {(supplies.HasHoe ? "Có" : "Thiếu")} | Liềm: {(supplies.HasSickle ? "Có" : "Thiếu")}";
        WaterScoopStockText.Text = $"💧 Gầu tát nước: {(supplies.HasWateringScoop ? "Có" : "Thiếu")}";
    }

    private void Irrigate_Click(object sender, RoutedEventArgs e)
    {
        if (_kernel.Capabilities.TryGet<ICultivationEstate>(CapabilityIds.CultivationEstate, out var estate) && estate is not null)
        {
            var res = estate.Irrigate(_plotId, _kernel.Today.DayIndex);
            MessageBox.Show(res.Message, "Tưới Tiêu", MessageBoxButton.OK, res.Ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
            _kernel.Save();
            PopulateFarmHub();
            NotifyHubStateChanged();
        }
    }

    private void Fertilize_Click(object sender, RoutedEventArgs e)
    {
        if (_kernel.Capabilities.TryGet<ICultivationEstate>(CapabilityIds.CultivationEstate, out var estate) && estate is not null)
        {
            var res = estate.Fertilise(_plotId, _kernel.Today.DayIndex);
            MessageBox.Show(res.Message, "Bón Phân", MessageBoxButton.OK, res.Ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
            _kernel.Save();
            PopulateFarmHub();
            NotifyHubStateChanged();
        }
    }

    private void Harvest_Click(object sender, RoutedEventArgs e)
    {
        if (_kernel.Capabilities.TryGet<ICultivationEstate>(CapabilityIds.CultivationEstate, out var estate) && estate is not null)
        {
            var res = estate.Harvest(_plotId, _kernel.Today.DayIndex);
            MessageBox.Show(res.Message, "Thu Hoạch Vụ Mùa", MessageBoxButton.OK, res.Ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
            _kernel.Save();
            PopulateFarmHub();
            NotifyHubStateChanged();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
