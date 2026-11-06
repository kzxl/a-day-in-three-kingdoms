using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SimT.Contracts;
using SimT.Contracts.Products;
using SimT.Core.Kernel;
using SimT.Host.Wpf.Controls;
using SimT.Host.Wpf.Scenes;

namespace SimT.Host.Wpf;

/// <summary>
/// Atmospheric start screen and campaign picker for Một Ngày Ở Tam Quốc.
/// Features lacquer aesthetic, animated particles, real-time save dossier inspection,
/// and instant campaign onboarding.
/// </summary>
public partial class TitleWindow : Window
{
    private const string EmptyTitle = "Chưa Có Ván Lưu Nào";
    private const string EmptyDesc = "Hãy nhập tên thương hiệu phía trên và nhấn Khởi nghiệp để bắt đầu vận mệnh Tam Quốc!";

    private static BitmapSource? s_cachedBackground;

    private readonly SaveManager _saves;
    private bool _isEntering;
    private bool _allowClose;

    public TitleWindow(SaveManager saves)
    {
        InitializeComponent();
        _saves = saves;
        DarkTitleBar.Apply(this);

        VersionText.Text = $"Một Ngày Ở Tam Quốc {App.Version()} · Lưu trữ SQLite WAL độc lập · {saves.Root}";

        EmptyStateControl.Configure(EmptyTitle, EmptyDesc, "📜", "Khởi nghiệp ngay");
        EmptyStateControl.ActionClick += EmptyState_ActionClick;

        LoadBackgroundImage();
        Reload();

        Loaded += (_, _) =>
        {
            var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(320));
            var scaleIn = new System.Windows.Media.Animation.DoubleAnimation(0.97, 1.0, TimeSpan.FromMilliseconds(320))
            {
                EasingFunction = new System.Windows.Media.Animation.CubicEase
                {
                    EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
                }
            };
            var slideIn = new System.Windows.Media.Animation.DoubleAnimation(8.0, 0.0, TimeSpan.FromMilliseconds(320))
            {
                EasingFunction = new System.Windows.Media.Animation.CubicEase
                {
                    EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
                }
            };

            TitleRoot.BeginAnimation(OpacityProperty, fadeIn);
            TitleScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleIn);
            TitleScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleIn);
            TitleTranslate.BeginAnimation(TranslateTransform.YProperty, slideIn);

            NameBox.Focus();
            NameBox.SelectAll();

            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                if (IsLoaded && !_isEntering)
                    _ = SceneOverlayControl.PrewarmFromWindowAsync(this);
            }));
        };
    }

    internal static BitmapSource? GetSharedBackgroundImage()
    {
        if (s_cachedBackground is not null)
            return s_cachedBackground;

        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "assets", "images", "backgrounds", "luoyang_grand_market.jpg");
            if (!File.Exists(path))
            {
                path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "assets", "images", "backgrounds", "luoyang_grand_market.jpg"));
            }

            if (File.Exists(path))
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.DecodePixelWidth = 1040;
                bmp.UriSource = new Uri(path, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();
                s_cachedBackground = bmp;
            }
        }
        catch
        {
            // Silently fall back to lacquer solid backdrop
        }

        return s_cachedBackground;
    }

    private void LoadBackgroundImage()
    {
        var bmp = GetSharedBackgroundImage();
        if (bmp is not null)
            BgImage.Source = bmp;
    }

    private void Reload()
    {
        var rawList = _saves.List();
        var items = rawList
            .Select(meta => LoadSaveItem(meta, _saves.Root))
            .ToList();

        SaveList.ItemsSource = items;
        SaveCountText.Text = $"{items.Count} ván";

        if (items.Count == 0)
        {
            SaveList.Visibility = Visibility.Collapsed;
            EmptyStateControl.Visibility = Visibility.Visible;
            UpdateDossierView(null);
        }
        else
        {
            SaveList.Visibility = Visibility.Visible;
            EmptyStateControl.Visibility = Visibility.Collapsed;
            SaveList.SelectedIndex = 0;
            UpdateDossierView(items[0]);
        }
    }

    private void SaveList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateDossierView(SaveList.SelectedItem as SaveItem);
    }

    private void UpdateDossierView(SaveItem? item)
    {
        if (item != null)
        {
            DossierRoot.DataContext = item;
            DossierRoot.Visibility = Visibility.Visible;
            WelcomeRoot.Visibility = Visibility.Collapsed;
            PopulateDlcBadges(item.Meta.Modules);
        }
        else
        {
            DossierRoot.DataContext = null;
            DossierRoot.Visibility = Visibility.Collapsed;
            WelcomeRoot.Visibility = Visibility.Visible;
            DlcBadgesPanel.Children.Clear();
        }
    }

    private void PopulateDlcBadges(IReadOnlyList<string>? modules)
    {
        DlcBadgesPanel.Children.Clear();
        if (modules == null || modules.Count == 0)
        {
            DlcBadgesPanel.Children.Add(new GameBadge("Cơ bản", BadgeVariant.Default));
            return;
        }

        var dict = new Dictionary<string, (string Name, BadgeVariant Variant)>(StringComparer.OrdinalIgnoreCase)
        {
            ["dlc.economy"] = ("🪙 Kinh Tế & Thương Đội", BadgeVariant.Gold),
            ["dlc.character"] = ("👥 Nhân Sĩ & Hảo Cảm", BadgeVariant.Rose),
            ["dlc.power"] = ("⚔️ Triều Đình & Binh Quyền", BadgeVariant.Loss),
            ["dlc.society"] = ("🏮 Xã Hội & Giai Thoại", BadgeVariant.Purple),
            ["base.content"] = ("📦 Vật Phẩm", BadgeVariant.Default),
            ["base.world"] = ("🗺️ Thành Trì", BadgeVariant.Info),
            ["base.player"] = ("👤 Thương Gia", BadgeVariant.Gain),
            ["base.npc"] = ("📜 Quần Anh", BadgeVariant.Default)
        };

        foreach (var mod in modules)
        {
            var badge = dict.TryGetValue(mod, out var info)
                ? new GameBadge(info.Name, info.Variant) { Margin = new Thickness(0, 0, 6, 6) }
                : new GameBadge(mod, BadgeVariant.Default) { Margin = new Thickness(0, 0, 6, 6) };
            DlcBadgesPanel.Children.Add(badge);
        }
    }

    private async void New_Click(object sender, RoutedEventArgs e)
    {
        if (_isEntering)
            return;

        var name = string.IsNullOrWhiteSpace(NameBox.Text) ? "Lữ Khách" : NameBox.Text.Trim();
        await BeginEnteringAsync(isNewCampaign: true);

        SimKernel? kernel = null;
        try
        {
            var meta = await Task.Run(() => _saves.Create(name));
            kernel = await Task.Run(() => _saves.Open(meta));
            await HandOffToMainWindowAsync(kernel, meta);
        }
        catch (SimException error)
        {
            kernel?.Dispose();
            RestoreInteractiveState();
            Fail(error);
        }
        catch (Exception error)
        {
            kernel?.Dispose();
            RestoreInteractiveState();
            Fail(error.Message);
        }
    }

    private void EmptyState_ActionClick(object sender, RoutedEventArgs e) =>
        New_Click(sender, e);

    private void Load_Click(object sender, RoutedEventArgs e)
    {
        if (!_isEntering && SaveList.SelectedItem is SaveItem selected)
            _ = EnterAsync(selected.Meta);
    }

    private void Load_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_isEntering)
        {
            e.Handled = true;
            return;
        }

        if (SaveList.SelectedItem is SaveItem selected)
            _ = EnterAsync(selected.Meta);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_isEntering)
        {
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            if (NameBox.IsFocused)
            {
                New_Click(sender, e);
                e.Handled = true;
            }
            else if (SaveList.SelectedItem is SaveItem)
            {
                Load_Click(sender, e);
                e.Handled = true;
            }
        }
        else if (e.Key == Key.F5)
        {
            Reload();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_isEntering && !_allowClose)
        {
            e.Cancel = true;
            return;
        }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        SceneOverlayControl.ReleaseWarmController();
        base.OnClosed(e);
    }

    private async Task EnterAsync(CampaignMeta meta)
    {
        if (_isEntering)
            return;

        await BeginEnteringAsync(isNewCampaign: false);
        SimKernel? kernel = null;
        try
        {
            kernel = await Task.Run(() => _saves.Open(meta));
            await HandOffToMainWindowAsync(kernel, meta);
        }
        catch (SimException error)
        {
            kernel?.Dispose();
            RestoreInteractiveState();
            Fail(error);
        }
        catch (Exception error)
        {
            kernel?.Dispose();
            RestoreInteractiveState();
            Fail(error.Message);
        }
    }

    private async Task BeginEnteringAsync(bool isNewCampaign)
    {
        _isEntering = true;
        if (isNewCampaign)
        {
            NewCampaignButton.Label = "Đang vào...";
            EmptyStateControl.Configure(EmptyTitle, EmptyDesc, "📜", "Đang vào...");
        }
        else
        {
            EnterGameButton.Label = "Đang vào...";
            EnterGameButton.CostText = null;
        }

        TitleRoot.IsEnabled = false;
        Cursor = Cursors.Wait;
        UpdateLayout();
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
    }

    private async Task HandOffToMainWindowAsync(SimKernel kernel, CampaignMeta meta)
    {
        var window = new MainWindow(kernel, _saves, meta);
        Application.Current.MainWindow = window;
        window.Show();
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        _allowClose = true;
        Hide();
        _ = window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(Close));
    }

    private void RestoreInteractiveState()
    {
        _isEntering = false;
        NewCampaignButton.Label = "Khởi nghiệp";
        EnterGameButton.Label = "VÀO VÁN CHƠI";
        EnterGameButton.CostText = "Enter";
        EmptyStateControl.Configure(EmptyTitle, EmptyDesc, "📜", "Khởi nghiệp ngay");
        TitleRoot.IsEnabled = true;
        Cursor = null;
    }

    private void Fail(SimException error) =>
        GameConfirmDialog.Alert(this, "Lỗi", $"[{error.Code}] {error.Message}", isError: true);

    private void Fail(string message) =>
        GameConfirmDialog.Alert(this, "Lỗi", message, isError: true);
}
