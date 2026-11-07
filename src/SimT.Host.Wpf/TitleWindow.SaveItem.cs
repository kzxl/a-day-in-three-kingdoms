using System.Globalization;
using System.IO;
using System.Windows;
using Microsoft.Data.Sqlite;
using SimT.Contracts.Products;
using SimT.Host.Wpf.Controls;

namespace SimT.Host.Wpf;

public partial class TitleWindow
{
    private void Duplicate_Click(object sender, RoutedEventArgs e)
    {
        if (_isEntering || SaveList.SelectedItem is not SaveItem selected)
            return;

        try
        {
            var copy = _saves.Duplicate(selected.Meta);
            Reload();

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
        if (_isEntering || SaveList.SelectedItem is not SaveItem selected)
            return;

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
        if (_isEntering)
            return;

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
        if (_isEntering)
            return;

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
        if (!_isEntering)
            Reload();
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        if (!_isEntering)
            Close();
    }

    private static SaveItem LoadSaveItem(CampaignMeta meta, string savesRoot)
    {
        var name = meta.Name;
        var gameDate = meta.GameDate;
        var formattedDate = FormatGameDateVi(gameDate);
        var lastPlayedText = FormatRelativeOrLocal(meta.LastPlayedUtc);
        var campaignIdShort = meta.CampaignId.Length >= 8 ? meta.CampaignId[..8] : meta.CampaignId;

        var cityName = "Lạc Dương";
        var cityCode = "lac_duong";
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

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT p.name, c.name_vi, c.region, c.code FROM pl_player p LEFT JOIN wd_citadel c ON p.city_id = c.id WHERE p.id = 1 LIMIT 1;";
                    using var r = cmd.ExecuteReader();
                    if (r.Read())
                    {
                        if (!r.IsDBNull(0) && !string.IsNullOrWhiteSpace(r.GetString(0)))
                            name = r.GetString(0);
                        if (!r.IsDBNull(1))
                            cityName = r.GetString(1);
                        if (!r.IsDBNull(2))
                            regionName = FormatRegionName(r.GetString(2));
                        if (!r.IsDBNull(3) && !string.IsNullOrWhiteSpace(r.GetString(3)))
                            cityCode = r.GetString(3);
                    }
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT balance_after FROM core_ledger WHERE account = 'player.wallet' ORDER BY id DESC LIMIT 1;";
                    var val = cmd.ExecuteScalar();
                    if (val is long l) wallet = l;
                    else if (val is int i) wallet = i;
                }

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
            cityCode,
            regionName,
            $"{wallet:N0} quan",
            wallet,
            fame,
            fameTitle,
            virtue,
            credibility);
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

    private static string FormatRegionName(string region) =>
        SimT.Contracts.Scenes.RoleTitleVi.FormatRegion(region);

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
            string cityCode,
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
            CityCode = cityCode;
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
        public string CityCode { get; }
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
