using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Data.Sqlite;
using SimT.Contracts;
using SimT.Contracts.Products;
using SimT.Host.Wpf.Controls;

namespace SimT.Host.Wpf;

/// <summary>
/// Atmospheric start screen and campaign picker for Một Ngày Ở Tam Quốc.
/// Features lacquer aesthetic, animated particles, real-time save dossier inspection,
/// and instant campaign onboarding.
/// </summary>
public partial class TitleWindow : Window
{
    private readonly SaveManager _saves;

    public TitleWindow(SaveManager saves)
    {
        InitializeComponent();
        _saves = saves;
        DarkTitleBar.Apply(this);

        VersionText.Text = $"Một Ngày Ở Tam Quốc {App.Version()} · Lưu trữ SQLite WAL độc lập · {saves.Root}";

        EmptyStateControl.Configure(
            "Chưa Có Ván Lưu Nào",
            "Hãy nhập tên thương hiệu phía trên và nhấn Khởi nghiệp để bắt đầu vận mệnh Tam Quốc!",
            "📜",
            "Khởi nghiệp ngay");
        EmptyStateControl.ActionClick += EmptyState_ActionClick;

        LoadBackgroundImage();
        Reload();

        Loaded += (_, _) =>
        {
            var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(320));
            var scaleIn = new System.Windows.Media.Animation.DoubleAnimation(0.97, 1.0, TimeSpan.FromMilliseconds(320))
            {
                EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };
            var slideIn = new System.Windows.Media.Animation.DoubleAnimation(8.0, 0.0, TimeSpan.FromMilliseconds(320))
            {
                EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };

            TitleRoot.BeginAnimation(OpacityProperty, fadeIn);
            TitleScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleIn);
            TitleScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleIn);
            TitleTranslate.BeginAnimation(TranslateTransform.YProperty, slideIn);

            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    private void LoadBackgroundImage()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "assets", "images", "backgrounds", "luoyang_grand_market.jpg");
            if (!File.Exists(path))
            {
                // Development fallback
                path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "assets", "images", "backgrounds", "luoyang_grand_market.jpg"));
            }

            if (File.Exists(path))
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(path, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();
                BgImage.Source = bmp;
            }
        }
        catch
        {
            // Silently fall back to lacquer solid backdrop
        }
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
            if (dict.TryGetValue(mod, out var info))
            {
                var badge = new GameBadge(info.Name, info.Variant)
                {
                    Margin = new Thickness(0, 0, 6, 6)
                };
                DlcBadgesPanel.Children.Add(badge);
            }
            else
            {
                var badge = new GameBadge(mod, BadgeVariant.Default)
                {
                    Margin = new Thickness(0, 0, 6, 6)
                };
                DlcBadgesPanel.Children.Add(badge);
            }
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

    private void EmptyState_ActionClick(object sender, RoutedEventArgs e)
    {
        New_Click(sender, e);
    }

    private void Load_Click(object sender, RoutedEventArgs e)
    {
        if (SaveList.SelectedItem is SaveItem selected)
        {
            Enter(selected.Meta);
        }
    }

    private void Load_DoubleClick(object sender, MouseButtonEventArgs e)
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
            var copy = _saves.Duplicate(selected.Meta);
            Reload();

            // Select duplicated item
            var found = SaveList.Items.OfType<SaveItem>().FirstOrDefault(x => x.Meta.CampaignId == copy.CampaignId);
            if (found != null)
            {
                SaveList.SelectedItem = found;
                SaveList.ScrollIntoView(found);
            }
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
                $"Bạn có chắc chắn muốn xóa vĩnh viễn ván \"{selected.Name}\"? Toàn bộ dữ liệu ngân lượng, sản nghiệp và hảo cảm của ván này sẽ mất vĩnh viễn.",
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

    private void OpenSaveFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var targetDir = _saves.Root;
            if (SaveList.SelectedItem is SaveItem selected)
            {
                var specific = Path.Combine(_saves.Root, selected.Meta.CampaignId);
                if (Directory.Exists(specific)) targetDir = specific;
            }

            if (Directory.Exists(targetDir))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = targetDir,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            Fail($"Không thể mở thư mục: {ex.Message}");
        }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        Reload();
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
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

    private static SaveItem LoadSaveItem(CampaignMeta meta, string savesRoot)
    {
        var name = meta.Name;
        var gameDate = meta.GameDate;
        var formattedDate = FormatGameDateVi(gameDate);
        var lastPlayedText = FormatRelativeOrLocal(meta.LastPlayedUtc);
        var campaignIdShort = meta.CampaignId.Length >= 8 ? meta.CampaignId[..8] : meta.CampaignId;

        var cityName = "Lạc Dương";
        var regionName = "Trung Nguyên";
        long wallet = 0;
        var fame = 100;
        var virtue = 50;
        var credibility = 50;

        var dbPath = Path.Combine(savesRoot, meta.CampaignId, "campaign.db");
        if (File.Exists(dbPath))
        {
            try
            {
                var cs = new SqliteConnectionStringBuilder
                {
                    DataSource = dbPath,
                    Mode = SqliteOpenMode.ReadOnly,
                    Pooling = false
                }.ToString();

                using var conn = new SqliteConnection(cs);
                conn.Open();

                // 1. City & player name
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT p.name, c.name_vi, c.region FROM pl_player p LEFT JOIN wd_citadel c ON p.city_id = c.id WHERE p.id = 1 LIMIT 1;";
                    using var r = cmd.ExecuteReader();
                    if (r.Read())
                    {
                        if (!r.IsDBNull(0) && !string.IsNullOrWhiteSpace(r.GetString(0)))
                            name = r.GetString(0);
                        if (!r.IsDBNull(1))
                            cityName = r.GetString(1);
                        if (!r.IsDBNull(2))
                            regionName = FormatRegionName(r.GetString(2));
                    }
                }

                // 2. Wallet balance
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT balance_after FROM core_ledger WHERE account = 'player.wallet' ORDER BY id DESC LIMIT 1;";
                    var val = cmd.ExecuteScalar();
                    if (val is long l) wallet = l;
                    else if (val is int i) wallet = i;
                }

                // 3. Social standing
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT fame, virtue, credibility FROM pl_social WHERE player_id = 1 LIMIT 1;";
                    using var r = cmd.ExecuteReader();
                    if (r.Read())
                    {
                        if (!r.IsDBNull(0)) fame = r.GetInt32(0);
                        if (!r.IsDBNull(1)) virtue = r.GetInt32(1);
                        if (!r.IsDBNull(2)) credibility = r.GetInt32(2);
                    }
                }
            }
            catch
            {
                // Fall back gracefully to index.json metadata
            }
        }

        var fameTitle = GetFameTitle(fame);

        return new SaveItem(
            meta,
            name,
            gameDate,
            formattedDate,
            lastPlayedText,
            campaignIdShort,
            cityName,
            regionName,
            $"{wallet:N0} quan",
            wallet,
            fame,
            fameTitle,
            virtue,
            credibility
        );
    }

    private static string FormatGameDateVi(string gameDate)
    {
        if (string.IsNullOrWhiteSpace(gameDate)) return "Năm 184";
        var parts = gameDate.Split('-');
        if (parts.Length == 3 && int.TryParse(parts[0], out var year) && int.TryParse(parts[1], out var month) && int.TryParse(parts[2], out var day))
        {
            var season = month switch
            {
                1 or 2 or 3 => "Xuân",
                4 or 5 or 6 => "Hạ",
                7 or 8 or 9 => "Thu",
                _ => "Đông"
            };
            return $"Năm {year} · Tháng {month} ({season}) · Ngày {day}";
        }
        return gameDate;
    }

    private static string FormatRelativeOrLocal(string utcString)
    {
        if (DateTime.TryParse(utcString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
        {
            return dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        }
        return utcString.Length > 16 ? utcString[..16].Replace('T', ' ') : utcString;
    }

    private static string FormatRegionName(string region) => region switch
    {
        "trung_nguyen" => "Trung Nguyên",
        "quan_trung" => "Quan Trung",
        "ha_bac" => "Hà Bắc",
        "giang_dong" => "Giang Đông",
        "kinh_chau" => "Kinh Châu",
        "ich_chau" => "Ích Châu",
        "u_chau" => "U Châu",
        "luong_chau" => "Lương Châu",
        _ => region
    };

    private static string GetFameTitle(int fame) => fame switch
    {
        < 80 => "Vô Danh Tiểu Tốt",
        < 150 => "Sơ Nhập Giang Hồ",
        < 250 => "Tiểu Hữu Danh Khí",
        < 500 => "Thương Nhân Nổi Danh",
        < 800 => "Cự Thương Một Cõi",
        _ => "Thiên Hạ Đệ Nhất Thương"
    };

    public sealed class SaveItem
    {
        public SaveItem(
            CampaignMeta meta,
            string name,
            string gameDate,
            string formattedDate,
            string lastPlayedText,
            string campaignIdShort,
            string cityName,
            string regionName,
            string walletText,
            long walletAmount,
            int fame,
            string fameTitle,
            int virtue,
            int credibility)
        {
            Meta = meta;
            Name = name;
            GameDate = gameDate;
            FormattedDate = formattedDate;
            LastPlayedText = lastPlayedText;
            CampaignIdShort = campaignIdShort;
            CityName = cityName;
            RegionName = regionName;
            WalletText = walletText;
            WalletAmount = walletAmount;
            Fame = fame;
            FameTitle = fameTitle;
            Virtue = virtue;
            Credibility = credibility;
        }

        public CampaignMeta Meta { get; }
        public string Name { get; }
        public string GameDate { get; }
        public string FormattedDate { get; }
        public string LastPlayedText { get; }
        public string CampaignIdShort { get; }
        public string CityName { get; }
        public string RegionName { get; }
        public string WalletText { get; }
        public long WalletAmount { get; }
        public int Fame { get; }
        public string FameText => $"{Fame:N0} Điểm";
        public string FameTitle { get; }
        public int Virtue { get; }
        public string VirtueText => $"{Virtue} / 100";
        public int Credibility { get; }
        public string CredibilityText => $"{Credibility} / 100";
        public string RegionDisplay => $"({RegionName})";
    }
}
