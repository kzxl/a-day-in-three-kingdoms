using System.IO;
using System.Windows;
using SimT.Contracts;
using SimT.Contracts.Products;
using SimT.Host.Wpf.Controls;

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
        VersionText.Text = $"Một Ngày Ở Tam Quốc {App.Version()} · lưu tại {saves.Root}";
        Reload();
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

        if (!GameConfirmDialog.Ask(
                this,
                "Xác nhận xóa ván chơi",
                $"Xóa vĩnh viễn ván \"{selected.Name}\"? Dữ liệu ván chơi này sẽ không thể khôi phục.",
                "Xóa vĩnh viễn",
                "Giữ lại",
                isDanger: true))
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
                GameConfirmDialog.Alert(this, "Quản Lý DLC", $"Không thể nạp thông tin DLC: {ex.Message}", isError: true);
            }
        }
        else
        {
            GameConfirmDialog.Alert(this, "Quản Lý DLC", "Vui lòng chọn một ván chơi trong danh sách để quản lý và kiểm tra DLC của ván đó.");
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
        GameConfirmDialog.Alert(this, "Lỗi", $"[{error.Code}] {error.Message}", isError: true);

    private void Fail(string message) =>
        GameConfirmDialog.Alert(this, "Lỗi", message, isError: true);

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
