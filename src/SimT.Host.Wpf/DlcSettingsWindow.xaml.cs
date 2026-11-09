using SimT.Contracts.Localization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SimT.Contracts.Modules;
using SimT.Core.Kernel;
using SimT.Host.Wpf.Controls;

namespace SimT.Host.Wpf;

public partial class DlcSettingsWindow : Window
{
    private readonly SimKernel _kernel;

    private static readonly Dictionary<string, (string NameVi, string Glyph, string Desc)> KnownModules = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dlc.economy"] = ("Thương Đạo & Bách Nghiệp", "🪙", "Đại trụ cột kinh tế: Điền trang nông sản, bách nghệ chế tác, giao thương thị trường và thương đoàn viễn chinh."),
        ["dlc.market"] = ("Thương Nghiệp & Thị Trường", "🎪", "Hệ thống mua sỉ/bán sỉ, quản lý kho bãi lưu trữ hàng hóa và mở quầy bán lẻ tư nhân."),
        ["dlc.caravan"] = ("Thương Đoàn Viễn Chinh", "⇢", "Tổ chức thương đội vận tải hàng hóa liên thành, thuê tiêu sư hộ vệ và nhận hợp đồng giao thương."),
        ["dlc.professions"] = ("Bách Nghệ & Chế Tác", "⚒", "Hệ thống 5 nghề thủ công (Rèn, Mộc, Y, Bói, Tửu) với kỹ năng chế tác thần binh và thương phẩm."),
        ["dlc.cultivation"] = ("Điền Trang & Nông Nghiệp", "❖", "Khai hoang điền sản, mua bán ruộng đất, gieo trồng nông sản/dược liệu và thu hoạch theo mùa vụ."),
        ["dlc.character"] = ("Nhân Duyên & Gia Tộc", "👥", "Đại trụ cột nhân vật: Mạng lưới hảo cảm, kết nghĩa Đào Viên, đối thoại tương tác và hôn sự gia tộc."),
        ["dlc.lineage"] = ("Gia Tộc & Huyết Thống", "⚭", "Kết tóc phu thê, sinh con dưỡng cái, giáo dục gia phong và lập người kế vị cơ nghiệp."),
        ["dlc.power"] = ("Vương Quyền & Binh Nghiệp", "⚔", "Đại trụ cột quyền lực: Cửu phẩm trung chính, bổ nhiệm thái thú, thao lược binh cơ và tiễu trừ thổ phỉ."),
        ["dlc.politics"] = ("Quan Trường & Triều Đình", "⌂", "Hệ thống Cửu phẩm trung chính, phái hệ triều đình, tiến cử thăng quan và bổ nhiệm thái thú."),
        ["dlc.society"] = ("Thế Thái & Nhân Văn", "📜", "Mô phỏng 127 danh nhân NPC có đời sống tự trị, ghi chép biên niên sử thế thái và tin tức giang hồ."),
        ["dlc.social"] = ("Quan Hệ & Môn Hạ", "🤝", "Mạng lưới hảo cảm, kết nghĩa kim lan Đào Viên, chiêu mộ gia thần môn hạ và nhật ký tương tác."),
        ["dlc.military"] = ("Binh Bị & Võ Đài", "⚔", "Võ đài so tài võ nghệ danh tướng, tiễu trừ 17 cứ điểm sơn tặc và thao luyện nghĩa dũng quân."),
        ["base.content"] = ("Kho Dữ Liệu & Danh Mục", "📦", "Danh mục vật phẩm, chỉ số cơ bản và hằng số kinh tế toàn trò chơi."),
        ["base.world"] = ("Bản Đồ Thiên Hạ & Thành Trì", "🗺️", "Mạng lưới 15 đại thành trì, lộ trình giao thông liên châu và các công trình địa phương."),
        ["base.player"] = ("Nhân Vật Chính & Thuộc Tính", "👤", "Hồ sơ 12 thuộc tính, kinh nghiệm EXP, chỉ số sinh mệnh và túi đồ cá nhân."),
        ["base.npc"] = ("Danh Bạ Nhân Sĩ Tam Quốc", "📜", "Hồ sơ 127 nhân vật lịch sử, phân bố cư ngụ và trạng thái sinh hoạt.")
    };

    public DlcSettingsWindow(SimKernel kernel)
    {
        _kernel = kernel;
        InitializeComponent();
        DarkTitleBar.Apply(this);
        InitializeAudioOptions();

        PopulateDlcList();
        PopulateEngineInfo();
        InitializeLanguageOptions();
        InitializeGameplayOptions();
        UpdateLocalizedTexts();
    }

    private void PopulateDlcList()
    {
        DlcListPanel.Children.Clear();
        var dlcCount = 0;
        var totalModules = _kernel.Modules.Count;

        foreach (var ctx in _kernel.Modules)
        {
            var m = ctx.Manifest;
            var isDlc = string.Equals(m.Kind, "dlc", StringComparison.OrdinalIgnoreCase);
            if (isDlc)
            {
                dlcCount++;
            }

            var (nameVi, glyph, desc) = KnownModules.TryGetValue(m.Id, out var meta)
                ? meta
                : (m.Id, "•", "Mô-đun mở rộng độc lập.");

            var card = new GameDlcCard();
            var isEnabled = _kernel.Product.Modules.Contains(m.Id, StringComparer.Ordinal);
            card.Configure(m, isEnabled, nameVi, glyph, desc);
            DlcListPanel.Children.Add(card);
        }

        DlcSummaryText.Text = $"Đã nạp: {totalModules} Modules ({dlcCount} DLCs kích hoạt) · Bản quyền Engine v1.0.0-rc1";
    }

    private void PopulateEngineInfo()
    {
        var p = _kernel.Product;
        EngineInfoText.Text =
            $"• Sản Phẩm (Product): {p.Name} (Mã: {p.Id})\n" +
            $"• Seed Thế Giới: {p.Seed}\n" +
            $"• Ngày Khởi Nghiệp: {p.StartDate}\n" +
            $"• Thành Xuất Phát: {p.StartLocationCode} · Tiền khởi điểm: {p.StartingCash:N0} Quan\n" +
            $"• Tổng Số Invariants Giám Sát: {_kernel.Invariants.Count} quy chuẩn toàn vẹn\n" +
            $"• Kiến Trúc Giao Tiếp: Zero-Coupling qua ICapabilityBroker";
    }

    private void RunInvariants_Click(object sender, RoutedEventArgs e)
    {
        var failures = _kernel.RunInvariants();
        InvariantResultsPanel.Children.Clear();

        if (failures.Count == 0)
        {
            InvariantStatusText.Text = $"✓ HỢP LỆ HOÀN TOÀN: {_kernel.Invariants.Count}/{_kernel.Invariants.Count} Invariants đều ĐẠT CHUẨN.";
            InvariantStatusText.Foreground = (Brush)FindResource("Gain");

            var okBorder = new Border
            {
                Background = (Brush)FindResource("Surface"),
                BorderBrush = (Brush)FindResource("Gain"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 4, 0, 0)
            };
            okBorder.Child = new TextBlock
            {
                Text = "Toàn bộ cơ sở dữ liệu SQLite, sổ cái tiền tệ, kho hàng và vị trí thực thể đều nhất quán 100%. Không có bất kỳ lỗi tham nhũng nào.",
                Style = (Style)FindResource("Caption"),
                Foreground = (Brush)FindResource("Gain")
            };
            InvariantResultsPanel.Children.Add(okBorder);
        }
        else
        {
            InvariantStatusText.Text = $"⚠️ CẢNH BÁO: Phát hiện {failures.Count} bất thường trong dữ liệu!";
            InvariantStatusText.Foreground = (Brush)FindResource("Loss");

            foreach (var fail in failures)
            {
                var failText = new TextBlock
                {
                    Text = $"❌ [{fail.InvariantId}] {fail.Message}",
                    Style = (Style)FindResource("Caption"),
                    Foreground = (Brush)FindResource("Loss"),
                    Margin = new Thickness(0, 2, 0, 2)
                };
                InvariantResultsPanel.Children.Add(failText);
            }
        }
    }

    private void TabDlc_Click(object sender, RoutedEventArgs e)
    {
        TabDlcPanel.Visibility = Visibility.Visible;
        TabDiagnosticsPanel.Visibility = Visibility.Collapsed;
        TabOptionsPanel.Visibility = Visibility.Collapsed;

        TabDlcBtn.FontWeight = FontWeights.Bold;
        TabDiagnosticsBtn.FontWeight = FontWeights.Normal;
        TabOptionsBtn.FontWeight = FontWeights.Normal;
    }

    private void TabDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        TabDlcPanel.Visibility = Visibility.Collapsed;
        TabDiagnosticsPanel.Visibility = Visibility.Visible;
        TabOptionsPanel.Visibility = Visibility.Collapsed;

        TabDlcBtn.FontWeight = FontWeights.Normal;
        TabDiagnosticsBtn.FontWeight = FontWeights.Bold;
        TabOptionsBtn.FontWeight = FontWeights.Normal;
    }

    private void TabOptions_Click(object sender, RoutedEventArgs e)
    {
        TabDlcPanel.Visibility = Visibility.Collapsed;
        TabDiagnosticsPanel.Visibility = Visibility.Collapsed;
        TabOptionsPanel.Visibility = Visibility.Visible;

        TabDlcBtn.FontWeight = FontWeights.Normal;
        TabDiagnosticsBtn.FontWeight = FontWeights.Normal;
        TabOptionsBtn.FontWeight = FontWeights.Bold;
    }

    private bool _languageOptionsReady;

    public sealed record LanguageItem(string Code, string DisplayName)
    {
        public override string ToString() => DisplayName;
    }

    private void InitializeLanguageOptions()
    {
        var avail = _kernel.Localization.GetAvailableLanguages();
        var items = new List<LanguageItem>();

        var supported = new (string Code, string FallbackName, string Flag)[]
        {
            ("vi", "Tiếng Việt", "🇻🇳"),
            ("en", "English", "🇬🇧"),
            ("zh", "简体中文", "🇨🇳")
        };

        foreach (var (code, fallbackName, flag) in supported)
        {
            var match = avail.FirstOrDefault(a => a.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
            var name = match != null ? $"{flag} {match.DisplayName}" : $"{flag} {fallbackName}";
            items.Add(new LanguageItem(code, name));
        }

        ComboLanguage.ItemsSource = items;
        ComboLanguage.SelectedValuePath = nameof(LanguageItem.Code);
        ComboLanguage.SelectedValue = _kernel.Localization.CurrentLocale;
        if (ComboLanguage.SelectedItem == null && items.Count > 0)
        {
            ComboLanguage.SelectedIndex = 0;
        }

        var secondaryItems = items.ToList();
        ComboSecondaryLanguage.ItemsSource = secondaryItems;
        ComboSecondaryLanguage.SelectedValuePath = nameof(LanguageItem.Code);
        ComboSecondaryLanguage.SelectedValue = _kernel.Localization.SecondaryLocale;
        if (ComboSecondaryLanguage.SelectedItem == null && secondaryItems.Count > 1)
        {
            ComboSecondaryLanguage.SelectedValue = "zh";
        }

        OptBilingualMode.IsChecked = _kernel.Localization.IsBilingualMode;
        SecondaryLangRow.Visibility = _kernel.Localization.IsBilingualMode ? Visibility.Visible : Visibility.Collapsed;

        _languageOptionsReady = true;
    }

    private void InitializeGameplayOptions()
    {
        var prefs = GamePreferences.Instance;
        OptAutoSave.IsChecked = prefs.AutoSave;
        OptConfirmAdvance.IsChecked = prefs.ConfirmAdvance;
        OptShowAlerts.IsChecked = prefs.ShowAlerts;
    }

    private void ComboLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_languageOptionsReady) return;
        if (ComboLanguage.SelectedItem is LanguageItem item)
        {
            _kernel.Localization.SetLocale(item.Code);
            GamePreferences.Instance.Language = item.Code;
            GamePreferences.Instance.Save();
            UpdateLocalizedTexts();
        }
    }

    private void ComboSecondaryLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_languageOptionsReady) return;
        if (ComboSecondaryLanguage.SelectedItem is LanguageItem item)
        {
            _kernel.Localization.SecondaryLocale = item.Code;
            GamePreferences.Instance.SecondaryLanguage = item.Code;
            GamePreferences.Instance.Save();
        }
    }

    private void OptBilingual_Click(object sender, RoutedEventArgs e)
    {
        var enabled = OptBilingualMode.IsChecked == true;
        _kernel.Localization.IsBilingualMode = enabled;
        SecondaryLangRow.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        GamePreferences.Instance.BilingualMode = enabled;
        GamePreferences.Instance.Save();
    }

    private void OptPreference_Click(object sender, RoutedEventArgs e)
    {
        var prefs = GamePreferences.Instance;
        prefs.AutoSave = OptAutoSave.IsChecked == true;
        prefs.ConfirmAdvance = OptConfirmAdvance.IsChecked == true;
        prefs.ShowAlerts = OptShowAlerts.IsChecked == true;
        prefs.Save();
    }

    private void UpdateLocalizedTexts()
    {
        var loc = _kernel.Localization;
        Title = loc.Get("settings.title");
        SettingsTitleText.Text = loc.Get("settings.title");
        SettingsSubtitleText.Text = loc.Get("settings.subtitle");
        TabDlcBtn.Content = loc.Get("settings.tab_dlc");
        TabDiagnosticsBtn.Content = loc.Get("settings.tab_diagnostics");
        TabOptionsBtn.Content = loc.Get("settings.tab_options");
        OptHeaderLanguage.Text = loc.Get("settings.section_language");
        OptPrimaryLangLabel.Text = loc.Get("settings.primary_language");
        OptBilingualMode.Content = loc.Get("settings.bilingual_mode");
        OptSecondaryLangLabel.Text = loc.Get("settings.secondary_language");
        OptHeaderGameplay.Text = loc.Get("settings.section_gameplay");
        OptAutoSave.Content = loc.Get("settings.opt_autosave");
        OptConfirmAdvance.Content = loc.Get("settings.opt_confirm_advance");
        OptShowAlerts.Content = loc.Get("settings.opt_show_alerts");
        OptHeaderAudio.Text = loc.Get("settings.section_audio");
        AudioMasterLabel.Text = loc.Get("settings.audio_master");
        AudioMusicLabel.Text = loc.Get("settings.audio_music");
        AudioSfxLabel.Text = loc.Get("settings.audio_sfx");
        AudioAmbienceLabel.Text = loc.Get("settings.audio_ambience");
        EscHintText.Text = loc.Get("settings.esc_hint");
        CloseButton.Content = loc.Get("common.close");
    }

    private void Close_Click(object sender, RoutedEventArgs e)
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
