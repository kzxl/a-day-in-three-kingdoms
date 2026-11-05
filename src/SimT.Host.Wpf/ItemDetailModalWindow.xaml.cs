using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using SimT.Contracts.Playing;

namespace SimT.Host.Wpf;

public partial class ItemDetailModalWindow : Window
{
    private readonly ItemHubEntry _entry;
    private int _bagQuantity;
    private int _warehouseQuantity;
    private WarehouseInfo? _warehouseInfo;
    private readonly Func<int, (bool ok, string msg)>? _onStore;
    private readonly Func<int, (bool ok, string msg)>? _onTake;
    private readonly Func<(bool ok, string msg)>? _onRent;
    private readonly Action? _onChanged;

    public ItemDetailModalWindow(
        ItemHubEntry entry,
        int bagQuantity = 0,
        int warehouseQuantity = 0,
        WarehouseInfo? warehouseInfo = null,
        Func<int, (bool ok, string msg)>? onStore = null,
        Func<int, (bool ok, string msg)>? onTake = null,
        Func<(bool ok, string msg)>? onRent = null,
        Action? onChanged = null)
    {
        InitializeComponent();
        DarkTitleBar.Apply(this);

        _entry = entry;
        _bagQuantity = Math.Max(0, bagQuantity);
        _warehouseQuantity = Math.Max(0, warehouseQuantity);
        _warehouseInfo = warehouseInfo;
        _onStore = onStore;
        _onTake = onTake;
        _onRent = onRent;
        _onChanged = onChanged;

        IconGlyphText.Text = entry.VisualIcon;
        CategoryTagSmall.Text = entry.Item.Category.ToUpperInvariant();

        NameViText.Text = entry.Item.NameVi;
        NameEnText.Text = entry.Item.NameEn.ToUpperInvariant();
        CodeBadgeText.Text = $"#{entry.Item.Id} · {entry.Item.Code}";

        SubCategoryText.Text = $"📂 {entry.SubCategoryVi}";
        PriceTagText.Text = $"💰 Giá gốc: {entry.Item.BasePrice} Quan";
        ShelfLifeTagText.Text = entry.Item.ShelfLifeDays > 0 ? $"⏳ Hạn dùng: {entry.Item.ShelfLifeDays} ngày" : "⏳ Hạn dùng: Vĩnh cửu";
        WeightTagText.Text = $"⚖️ Nặng: {entry.Item.Weight} Cân";

        AsciiIllustrationText.Text = entry.IllustrationArt.Trim();
        LoreDescriptionText.Text = entry.LoreDescriptionVi;

        DetailedStatsList.ItemsSource = entry.DetailedStats.ToList();

        SourcingText.Text = entry.SourcingVi;
        UsageText.Text = entry.UsageVi;
        UpgradeRoadmapText.Text = entry.UpgradeRoadmapVi;

        UpdateTransferUi();
    }

    private void UpdateTransferUi()
    {
        BagQuantityText.Text = $"{_bagQuantity} đơn vị";
        AvailableInBagText.Text = $"Có: {_bagQuantity}";

        if (_warehouseInfo is null)
        {
            WarehouseQuantityText.Text = "Chưa thuê";
            WarehouseCapacitySummaryText.Text = "Phí 120q/tháng";
            WarehouseStatusBadge.Text = "Chưa thuê kho (Phí 120 Quan/tháng)";

            UnrentedWarehousePanel.Visibility = Visibility.Visible;
            RentedWarehousePanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            WarehouseQuantityText.Text = $"{_warehouseQuantity} đơn vị";
            WarehouseCapacitySummaryText.Text = $"Kho: {_warehouseInfo.Used}/{_warehouseInfo.Capacity}";
            var availableSpace = Math.Max(0, _warehouseInfo.Capacity - _warehouseInfo.Used);
            WarehouseStatusBadge.Text = $"Sức chứa: {_warehouseInfo.Used}/{_warehouseInfo.Capacity} (Còn trống: {availableSpace})";

            UnrentedWarehousePanel.Visibility = Visibility.Collapsed;
            RentedWarehousePanel.Visibility = Visibility.Visible;
            AvailableInWarehouseText.Text = $"Kho: {_warehouseQuantity}";

            // Enable / Disable buttons based on capacity and held amounts
            var canStore = _bagQuantity > 0 && availableSpace > 0;
            StoreOneButton.IsEnabled = canStore;
            StoreFiveButton.IsEnabled = canStore;
            StoreAllButton.IsEnabled = canStore;

            var canTake = _warehouseQuantity > 0;
            TakeOneButton.IsEnabled = canTake;
            TakeFiveButton.IsEnabled = canTake;
            TakeAllButton.IsEnabled = canTake;
        }
    }

    private void StoreOne_Click(object sender, RoutedEventArgs e) => ExecuteStore(1);

    private void StoreFive_Click(object sender, RoutedEventArgs e)
    {
        if (_warehouseInfo is null) return;
        var maxPossible = Math.Min(5, Math.Min(_bagQuantity, _warehouseInfo.Capacity - _warehouseInfo.Used));
        ExecuteStore(Math.Max(1, maxPossible));
    }

    private void StoreAll_Click(object sender, RoutedEventArgs e)
    {
        if (_warehouseInfo is null) return;
        var maxPossible = Math.Min(_bagQuantity, _warehouseInfo.Capacity - _warehouseInfo.Used);
        ExecuteStore(Math.Max(1, maxPossible));
    }

    private void TakeOne_Click(object sender, RoutedEventArgs e) => ExecuteTake(1);

    private void TakeFive_Click(object sender, RoutedEventArgs e)
    {
        var maxPossible = Math.Min(5, _warehouseQuantity);
        ExecuteTake(Math.Max(1, maxPossible));
    }

    private void TakeAll_Click(object sender, RoutedEventArgs e)
    {
        ExecuteTake(_warehouseQuantity);
    }

    private void ExecuteStore(int quantity)
    {
        if (_onStore is null)
        {
            ShowFeedback("Thao tác gửi kho không khả dụng.", isSuccess: false);
            return;
        }

        if (quantity <= 0 || _bagQuantity <= 0)
        {
            ShowFeedback("Không có đủ hàng trong túi để gửi.", isSuccess: false);
            return;
        }

        var (ok, msg) = _onStore(quantity);
        ShowFeedback(msg, ok);

        if (ok)
        {
            _bagQuantity = Math.Max(0, _bagQuantity - quantity);
            _warehouseQuantity += quantity;
            if (_warehouseInfo is not null)
            {
                _warehouseInfo = _warehouseInfo with { Used = _warehouseInfo.Used + quantity };
            }

            UpdateTransferUi();
            _onChanged?.Invoke();
        }
    }

    private void ExecuteTake(int quantity)
    {
        if (_onTake is null)
        {
            ShowFeedback("Thao tác lấy hàng không khả dụng.", isSuccess: false);
            return;
        }

        if (quantity <= 0 || _warehouseQuantity <= 0)
        {
            ShowFeedback("Kho không có đủ hàng để lấy.", isSuccess: false);
            return;
        }

        var (ok, msg) = _onTake(quantity);
        ShowFeedback(msg, ok);

        if (ok)
        {
            _bagQuantity += quantity;
            _warehouseQuantity = Math.Max(0, _warehouseQuantity - quantity);
            if (_warehouseInfo is not null)
            {
                _warehouseInfo = _warehouseInfo with { Used = Math.Max(0, _warehouseInfo.Used - quantity) };
            }

            UpdateTransferUi();
            _onChanged?.Invoke();
        }
    }

    private void RentWarehouse_Click(object sender, RoutedEventArgs e)
    {
        if (_onRent is null)
        {
            ShowFeedback("Thao tác thuê kho không khả dụng.", isSuccess: false);
            return;
        }

        var (ok, msg) = _onRent();
        ShowFeedback(msg, ok);

        if (ok)
        {
            _warehouseInfo = new WarehouseInfo(_entry.Item.Id, 50, 120, 0);
            UpdateTransferUi();
            _onChanged?.Invoke();
        }
    }

    private void ShowFeedback(string message, bool isSuccess)
    {
        TransferFeedbackText.Text = message;
        TransferFeedbackText.Foreground = isSuccess
            ? (Brush)FindResource("Accent")
            : (Brush)FindResource("Loss");
        TransferFeedbackBorder.BorderBrush = isSuccess
            ? (Brush)FindResource("Accent")
            : (Brush)FindResource("Loss");
        TransferFeedbackBorder.Visibility = Visibility.Visible;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }
}
