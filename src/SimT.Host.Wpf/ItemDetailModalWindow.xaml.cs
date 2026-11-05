using System.Windows;
using System.Windows.Input;
using SimT.Contracts.Playing;

namespace SimT.Host.Wpf;

public partial class ItemDetailModalWindow : Window
{
    public ItemDetailModalWindow(ItemHubEntry entry, int bagQuantity = 0)
    {
        InitializeComponent();
        DarkTitleBar.Apply(this);

        IconGlyphText.Text = entry.VisualIcon;
        CategoryTagSmall.Text = entry.Item.Category.ToUpperInvariant();

        NameViText.Text = entry.Item.NameVi;
        NameEnText.Text = entry.Item.NameEn.ToUpperInvariant();
        CodeBadgeText.Text = $"#{entry.Item.Id} · {entry.Item.Code}";

        SubCategoryText.Text = $"📂 {entry.SubCategoryVi}";
        PriceTagText.Text = $"💰 Giá gốc: {entry.Item.BasePrice} Quan";
        ShelfLifeTagText.Text = entry.Item.ShelfLifeDays > 0 ? $"⏳ Hạn dùng: {entry.Item.ShelfLifeDays} ngày" : "⏳ Hạn dùng: Vĩnh cửu";
        WeightTagText.Text = $"⚖️ Nặng: {entry.Item.Weight} Cân";

        BagQuantityText.Text = bagQuantity > 0 ? $"{bagQuantity} đơn vị" : "0 đơn vị";

        AsciiIllustrationText.Text = entry.IllustrationArt.Trim();
        LoreDescriptionText.Text = entry.LoreDescriptionVi;

        DetailedStatsList.ItemsSource = entry.DetailedStats.ToList();

        SourcingText.Text = entry.SourcingVi;
        UsageText.Text = entry.UsageVi;
        UpgradeRoadmapText.Text = entry.UpgradeRoadmapVi;
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
