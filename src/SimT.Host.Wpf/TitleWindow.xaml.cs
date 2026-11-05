using System.IO;
using System.Windows;
using System.Windows.Controls;
using SimT.Contracts;
using SimT.Contracts.Products;

namespace SimT.Host.Wpf;

/// <summary>Campaign picker. Each row is one independent save directory.</summary>
public partial class TitleWindow : Window
{
    private readonly SaveManager _saves;

    public TitleWindow(SaveManager saves)
    {
        InitializeComponent();
        _saves = saves;
        DarkTitleBar.Apply(this);
        VersionText.Text = $"SimT {App.Version()} · lưu tại {saves.Root}";
        Reload();

        Loaded += (_, _) =>
        {
            var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(260));
            var scaleIn = new System.Windows.Media.Animation.DoubleAnimation(0.97, 1.0, TimeSpan.FromMilliseconds(260))
            {
                EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };
            var slideIn = new System.Windows.Media.Animation.DoubleAnimation(8.0, 0.0, TimeSpan.FromMilliseconds(260))
            {
                EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };

            TitleRoot.BeginAnimation(OpacityProperty, fadeIn);
            TitleScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleIn);
            TitleScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleIn);
            TitleTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, slideIn);
        };
    }

    private void Reload()
    {
        var items = _saves.List()
            .Select(meta => new SaveItem(meta))
            .ToList();

        SaveList.ItemsSource = items;
        EmptyText.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        if (items.Count > 0)
        {
            SaveList.SelectedIndex = 0;
        }
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        var name = string.IsNullOrWhiteSpace(NameBox.Text) ? "Lữ Khách" : NameBox.Text.Trim();
        try
        {
            var meta = _saves.Create(name);
            Enter(meta);
        }
        catch (SimException error)
        {
            Fail(error);
        }
    }

    private void Load_Click(object sender, RoutedEventArgs e)
    {
        if (SaveList.SelectedItem is SaveItem selected)
        {
            Enter(selected.Meta);
        }
    }

    private void Load_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (SaveList.SelectedItem is SaveItem selected)
        {
            Enter(selected.Meta);
        }
    }

    private void Duplicate_Click(object sender, RoutedEventArgs e)
    {
        if (SaveList.SelectedItem is not SaveItem selected)
        {
            return;
        }

        try
        {
            _saves.Duplicate(selected.Meta);
            Reload();
        }
        catch (IOException error)
        {
            Fail(error.Message);
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (SaveList.SelectedItem is not SaveItem selected)
        {
            return;
        }

        var confirm = MessageBox.Show(this, $"Xóa vĩnh viễn ván \"{selected.Name}\"?", "Xác nhận",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        _saves.Delete(selected.Meta.CampaignId);
        Reload();
    }

    private void DlcManager_Click(object sender, RoutedEventArgs e)
    {
        if (SaveList.SelectedItem is SaveItem selected)
        {
            try
            {
                using var kernel = _saves.Open(selected.Meta);
                var dlcWin = new DlcSettingsWindow(kernel) { Owner = this };
                dlcWin.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể nạp thông tin DLC: {ex.Message}", "Quản Lý DLC", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        else
        {
            MessageBox.Show("Vui lòng chọn một ván chơi trong danh sách để quản lý và kiểm tra DLC của ván đó.", "Quản Lý DLC", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void Enter(CampaignMeta meta)
    {
        try
        {
            var kernel = _saves.Open(meta);
            var window = new MainWindow(kernel, _saves, meta);
            Application.Current.MainWindow = window;
            window.Show();
            Close();
        }
        catch (SimException error)
        {
            Fail(error);
        }
    }

    private void Fail(SimException error) =>
        MessageBox.Show(this, $"[{error.Code}] {error.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);

    private void Fail(string message) =>
        MessageBox.Show(this, message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);

    private sealed class SaveItem
    {
        public SaveItem(CampaignMeta meta)
        {
            Meta = meta;
            Name = meta.Name;
            Summary = $"Ngày {meta.GameDate} · lưu lúc {meta.LastPlayedUtc[..Math.Min(16, meta.LastPlayedUtc.Length)]} · {meta.CampaignId[..8]}";
        }

        public CampaignMeta Meta { get; }

        public string Name { get; }

        public string Summary { get; }
    }
}
