using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SimT.Contracts.Playing;

namespace SimT.Host.Wpf;

public partial class NpcDetailWindow : Window
{
    private long _npcId;
    private NpcDef _npc = null!;
    private string _citadelName = "Ẩn sĩ";
    private long _npcCitadelId;
    private long _playerCityId;
    private bool _hasMet;
    private NpcStatDelta? _deltas;
    private NpcActivityDef? _activity;
    private RelationshipDef? _relationship;
    private RetainerDef? _retainer;
    private int _playerFatigue;
    private int _playerStamina = 100;
    private bool _isProtagonist;
    private readonly CharacterAttributeExps? _exps;

    public Action<long>? OnChallengeDuel { get; set; }
    public Action<long>? OnMeetNpc { get; set; }

    public NpcDetailWindow(
        NpcDef npc,
        string citadelName,
        long npcCitadelId,
        long playerCityId,
        bool hasMet,
        NpcStatDelta? deltas = null,
        NpcActivityDef? activity = null,
        RelationshipDef? relationship = null,
        RetainerDef? retainer = null,
        int playerFatigue = 0,
        int playerStamina = 100,
        CharacterAttributeExps? exps = null)
    {
        InitializeComponent();
        DarkTitleBar.Apply(this);

        _npc = npc;
        _npcId = npc.Id;
        _citadelName = citadelName;
        _npcCitadelId = npcCitadelId;
        _playerCityId = playerCityId;
        _hasMet = hasMet;
        _deltas = deltas;
        _activity = activity;
        _relationship = relationship;
        _retainer = retainer;
        _playerFatigue = playerFatigue;
        _playerStamina = playerStamina;
        _isProtagonist = (npc.Id == 0 || npc.Code == "protagonist" || npc.Role == "Lữ Khách");
        _exps = exps;

        Populate();
    }

    public static void ShowNpc(
        Window owner,
        NpcDef npc,
        string citadelName,
        long npcCitadelId,
        long playerCityId,
        bool hasMet,
        NpcStatDelta? deltas = null,
        NpcActivityDef? activity = null,
        RelationshipDef? relationship = null,
        RetainerDef? retainer = null,
        int playerFatigue = 0,
        int playerStamina = 100,
        Action<long>? onChallengeDuel = null,
        Action<long>? onMeetNpc = null,
        CharacterAttributeExps? exps = null)
    {
        var window = new NpcDetailWindow(
            npc,
            citadelName,
            npcCitadelId,
            playerCityId,
            hasMet,
            deltas,
            activity,
            relationship,
            retainer,
            playerFatigue,
            playerStamina,
            exps)
        {
            Owner = owner,
            OnChallengeDuel = onChallengeDuel,
            OnMeetNpc = onMeetNpc
        };

        window.ConfigureFooterActions();
        window.ShowDialog();
    }

    public static void ShowNpc(
        Window owner,
        NpcDef npc,
        string citadelName,
        NpcStatDelta? deltas = null,
        NpcActivityDef? activity = null,
        RelationshipDef? relationship = null,
        RetainerDef? retainer = null,
        Action<long>? onChallengeDuel = null,
        CharacterAttributeExps? exps = null)
    {
        ShowNpc(
            owner,
            npc,
            citadelName,
            npc.CitadelId,
            npc.CitadelId,
            hasMet: relationship is not null || onChallengeDuel is null,
            deltas,
            activity,
            relationship,
            retainer,
            playerFatigue: 0,
            playerStamina: 100,
            onChallengeDuel,
            onMeetNpc: null,
            exps: exps);
    }

    private void Populate()
    {
        Title = $"{_npc.NameVi} — Hồ Sơ Nhân Vật";

        // Avatar char: first letter of Han name or Vietnamese name
        var avatarChar = !string.IsNullOrWhiteSpace(_npc.NameHan) && _npc.NameHan.Length > 0
            ? _npc.NameHan[0].ToString()
            : _npc.NameVi.Length > 0 ? _npc.NameVi[0].ToString() : "人";
        AvatarCharText.Text = avatarChar;

        RoleBadgeSmall.Text = DetermineRoleTag(_npc.Role);

        NameText.Text = _npc.NameVi;
        HanNameText.Text = string.IsNullOrWhiteSpace(_npc.NameHan) ? "" : _npc.NameHan;
        StyleNameText.Text = string.IsNullOrWhiteSpace(_npc.StyleNameVi) ? "" : $"Tự: {_npc.StyleNameVi}";

        RoleText.Text = _npc.Role;
        CitadelText.Text = $"Ngụ tại: {_citadelName}";

        var sameCity = (_npcCitadelId == _playerCityId);
        UpdatePills(sameCity);

        PersonalitySummaryText.Text = string.IsNullOrWhiteSpace(_npc.Personality)
            ? "Khí chất bình ổn, phong thái điềm đạm."
            : $"Khí chất: {_npc.Personality}";

        // Calculate total stats
        var a = _npc.Attributes;
        var bonusTotal = _deltas?.TotalBonus ?? 0;
        var total = a.War + a.Ldr + a.Int + a.Pol + a.Dip + a.Com + a.Log + a.Cra + a.Con + a.Wil + a.Prc + a.Lck + bonusTotal;

        if (_hasMet || _isProtagonist)
        {
            TotalStatsLabel.Text = "TỔNG ĐIỂM";
            TotalStatsText.Text = total.ToString();
            TotalStatsMaxText.Text = "/ 1200";
        }
        else
        {
            TotalStatsLabel.Text = "ƯỚC LƯỢNG";
            var estimate = Math.Clamp((total / 50) * 50, 400, 1150);
            TotalStatsText.Text = $"~{estimate}";
            TotalStatsMaxText.Text = "danh tiếng";
        }

        // TAB 1: Radar Chart & Core Stats
        StatRadarChart.SetCoreStats(a, _deltas);
        ArchetypeTagText.Text = StatRadarChart.ArchetypeTitle;

        if (!_hasMet && !_isProtagonist)
        {
            UnmetStatsNotice.Visibility = Visibility.Visible;
        }
        else
        {
            UnmetStatsNotice.Visibility = Visibility.Collapsed;
        }

        CoreStatsGrid.Children.Clear();
        AddStatItem(CoreStatsGrid, "WAR · Võ Lực", a.War, _deltas?.WarDelta ?? 0, "Khả năng chiến đấu, trảm tướng phá trận", _hasMet || _isProtagonist, _exps?.War);
        AddStatItem(CoreStatsGrid, "LDR · Thống Soái", a.Ldr, _deltas?.LdrDelta ?? 0, "Khả năng chỉ huy quân đội, giữ vững đội hình", _hasMet || _isProtagonist, _exps?.Ldr);
        AddStatItem(CoreStatsGrid, "INT · Trí Tuệ", a.Int, _deltas?.IntDelta ?? 0, "Mưu lược quân sự, bày binh bố trận", _hasMet || _isProtagonist, _exps?.Int);
        AddStatItem(CoreStatsGrid, "POL · Chính Trị", a.Pol, _deltas?.PolDelta ?? 0, "Quản lý kinh bang tế thế, phát triển thành thị", _hasMet || _isProtagonist, _exps?.Pol);
        AddStatItem(CoreStatsGrid, "DIP · Ngoại Giao", a.Dip, _deltas?.DipDelta ?? 0, "Du thuyết liên minh, thu phục lòng người", _hasMet || _isProtagonist, _exps?.Dip);
        AddStatItem(CoreStatsGrid, "COM · Thương Mại", a.Com, _deltas?.ComDelta ?? 0, "Đàm phán giá cả, kinh doanh sinh lời", _hasMet || _isProtagonist, _exps?.Com);
        AddStatItem(CoreStatsGrid, "LOG · Hậu Cần", a.Log, _deltas?.LogDelta ?? 0, "Vận tải lương thảo, quản lý kho tàng", _hasMet || _isProtagonist, _exps?.Log);
        AddStatItem(CoreStatsGrid, "CRA · Kỹ Nghệ", a.Cra, _deltas?.CraDelta ?? 0, "Chế tạo khí giới, xây đắp công trình", _hasMet || _isProtagonist, _exps?.Cra);

        // TAB 2: Innate Vitals & Lifestyle
        PopulateInnateStats();

        // TAB 3: Skills & Feats
        SkillsListPanel.Children.Clear();
        SkillCountText.Text = _npc.Skills.Count > 0 ? $"{_npc.Skills.Count} tuyệt kỹ" : "Chưa khai mở";

        if (_npc.Skills.Count == 0)
        {
            SkillsListPanel.Children.Add(new TextBlock
            {
                Text = "Nhân sĩ này chưa bộc lộ đặc kỹ danh tướng riêng biệt.",
                Style = (Style)FindResource("Caption"),
                Foreground = (Brush)FindResource("Muted"),
                Margin = new Thickness(4, 2, 4, 4)
            });
        }
        else
        {
            foreach (var skill in _npc.Skills)
            {
                SkillsListPanel.Children.Add(BuildSkillCard(skill));
            }
        }

        // TAB 4: Relationship & Biography
        PopulateRelationship();

        BioText.Text = string.IsNullOrWhiteSpace(_npc.BioVi)
            ? "Nhân sĩ danh tiếng thời Tam Quốc, hành trạng được dân gian ca tụng."
            : _npc.BioVi;
    }

    private void UpdatePills(bool sameCity)
    {
        // Location Pill
        if (sameCity)
        {
            LocationPillText.Text = "ĐỒNG THÀNH";
            LocationPillText.Foreground = (Brush)FindResource("Gain");
            LocationPillBorder.BorderBrush = (Brush)FindResource("Gain");
            LocationPillBorder.ToolTip = "Đang ở cùng thành trì với bạn.";
        }
        else
        {
            LocationPillText.Text = "PHƯƠNG XA";
            LocationPillText.Foreground = (Brush)FindResource("Muted");
            LocationPillBorder.BorderBrush = (Brush)FindResource("Line");
            LocationPillBorder.ToolTip = $"Đang ở {_citadelName}, không ở cùng thành trì với bạn.";
        }

        // Acquaintance Pill
        if (_isProtagonist)
        {
            AcquaintancePillText.Text = "BẢN THÂN";
            AcquaintancePillText.Foreground = (Brush)FindResource("Accent");
            AcquaintancePillBorder.BorderBrush = (Brush)FindResource("Accent");
        }
        else if (_hasMet)
        {
            AcquaintancePillText.Text = "ĐÃ DIỆN KIẾN";
            AcquaintancePillText.Foreground = (Brush)FindResource("Accent");
            AcquaintancePillBorder.BorderBrush = (Brush)FindResource("Accent");
            AcquaintancePillBorder.ToolTip = "Bạn đã từng gặp gỡ, đàm đạo với nhân sĩ này.";
        }
        else
        {
            AcquaintancePillText.Text = "CHƯA GẶP MẶT ❓";
            AcquaintancePillText.Foreground = (Brush)FindResource("Faint");
            AcquaintancePillBorder.BorderBrush = (Brush)FindResource("Line");
            AcquaintancePillBorder.ToolTip = "Bạn chưa từng gặp gỡ nhân sĩ này. Bấm 'Bái Phỏng' để làm quen.";
        }
    }

    private void PopulateInnateStats()
    {
        InnateStatsGrid.Children.Clear();
        var a = _npc.Attributes;

        if (!_hasMet && !_isProtagonist)
        {
            UnmetInnateNotice.Visibility = Visibility.Visible;
            AddMaskedStatItem(InnateStatsGrid, "CON · Căn Cốt", "Cơ mật bản thể (Cần diện kiến)");
            AddMaskedStatItem(InnateStatsGrid, "WIL · Định Lực", "Ý chí chưa từng bộc lộ");
            AddMaskedStatItem(InnateStatsGrid, "PRC · Tuệ Nhãn", "Cơ biến chưa thể kiểm chứng");
            AddMaskedStatItem(InnateStatsGrid, "LCK · Khí Vận", "Phúc duyên trời ban bí mật");

            LifestyleWealthText.Text = "Kín đáo (Chưa rõ)";
            LifestyleReputationText.Text = _activity is not null ? $"★ Uy danh {_activity.Reputation}" : "Danh sĩ Tam Quốc";
            LifestyleActionText.Text = "Hành tung bất định ở phương xa";
        }
        else
        {
            UnmetInnateNotice.Visibility = Visibility.Collapsed;
            AddStatItem(InnateStatsGrid, "CON · Căn Cốt", a.Con, 0, "Thể phách, quyết định sinh lực & thể lực tối đa", true, _exps?.Con);
            AddStatItem(InnateStatsGrid, "WIL · Định Lực", a.Wil, 0, "Ý chí kiên định, chống hoảng loạn & giảm stress", true, _exps?.Wil);
            AddStatItem(InnateStatsGrid, "PRC · Tuệ Nhãn", a.Prc, 0, "Cơ biến, giám định hàng hóa & phát giác phục kích", true, _exps?.Prc);
            AddStatItem(InnateStatsGrid, "LCK · Khí Vận", a.Lck, 0, "Phúc duyên trời ban, bạo kích kỳ ngộ thương trường", true, _exps?.Lck);

            LifestyleWealthText.Text = _activity is not null ? $"💰 {_activity.CurrentWealth:N0} Quan" : "Gia cảnh bình ổn";
            LifestyleReputationText.Text = _activity is not null ? $"★ Uy danh {_activity.Reputation}" : "★ Uy danh 50";
            LifestyleActionText.Text = _activity is not null ? $"⚡ {_activity.CurrentAction}" : "Tu tâm dưỡng tính";
        }

        PersonalityDetailText.Text = string.IsNullOrWhiteSpace(_npc.Personality)
            ? "Khí chất bình ổn, chưa bộc lộ dã tâm hay khuynh hướng đặc biệt."
            : _npc.Personality;
    }

    private void PopulateRelationship()
    {
        if (_isProtagonist)
        {
            KizunaAffinityDetail.Text = "Lữ khách khởi nghiệp";
            KizunaRetainerDetail.Text = "Chúa công";
            KizunaMemoryText.Text = "Nhân vật chính dấn thân thời loạn thế Tam Quốc, gầy dựng sự nghiệp.";
            return;
        }

        if (_hasMet)
        {
            var affSign = (_relationship?.Affinity ?? 0) >= 0 ? "+" : "";
            KizunaAffinityDetail.Text = $"♥ {affSign}{_relationship?.Affinity ?? 0}/100 ({_relationship?.Impression ?? "Sơ giao"})";

            if (_retainer is not null)
            {
                KizunaRetainerDetail.Text = $"🛡️ {_retainer.RoleAssigned} (Trung thành {_retainer.Loyalty}/100)";
            }
            else
            {
                KizunaRetainerDetail.Text = (_relationship?.Affinity ?? 0) >= 35 ? "🤝 Có thể chiêu mộ" : "Chưa chiêu mộ";
            }

            var gifts = _relationship?.GiftCount ?? 0;
            KizunaMemoryText.Text = gifts > 0
                ? $"Đã kết giao tương thức. Đã biếu tặng {gifts} món quà phẩm."
                : "Đã kết giao tương thức qua đàm đạo.";
        }
        else
        {
            KizunaAffinityDetail.Text = "Chưa từng diện kiến (0/100 Hảo cảm)";
            KizunaRetainerDetail.Text = "Chưa thể chiêu mộ";
            KizunaMemoryText.Text = "Chưa từng gặp gỡ hay trò chuyện. Hãy bấm 'Bái Phỏng' để bắt đầu kết giao!";
        }
    }

    public void ConfigureFooterActions()
    {
        if (_isProtagonist)
        {
            MeetNpcButton.Visibility = Visibility.Collapsed;
            DuelChallengeButton.Visibility = Visibility.Collapsed;
            return;
        }

        var sameCity = (_npcCitadelId == _playerCityId);

        // Meet / Visit Button
        if (_hasMet)
        {
            MeetNpcButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            MeetNpcButton.Visibility = Visibility.Visible;
            if (sameCity)
            {
                MeetNpcButton.IsEnabled = true;
                MeetNpcButton.ToolTip = $"Bái phỏng đàm đạo cùng {_npc.NameVi} tại thành này.";
            }
            else
            {
                MeetNpcButton.IsEnabled = false;
                MeetNpcButton.ToolTip = $"{_npc.NameVi} hiện đang ở {_citadelName}. Cần du hành đến đó để bái phỏng.";
            }
        }

        // Duel Button: Must be same city AND must have met AND have martial stats AND player not exhausted
        DuelChallengeButton.Visibility = Visibility.Visible;

        if (!sameCity)
        {
            DuelChallengeButton.IsEnabled = false;
            DuelChallengeButton.ToolTip = $"Không thể so tài: {_npc.NameVi} hiện đang ở {_citadelName}, không cùng thành trì với bạn.";
        }
        else if (!_hasMet)
        {
            DuelChallengeButton.IsEnabled = false;
            DuelChallengeButton.ToolTip = $"Chưa thể so tài: Bạn chưa từng diện kiến {_npc.NameVi}. Hãy bấm '💬 Bái Phỏng' để làm quen trước!";
        }
        else if (_npc.Attributes.War < 30)
        {
            DuelChallengeButton.IsEnabled = false;
            DuelChallengeButton.ToolTip = $"{_npc.NameVi} theo đường văn nhã, không thông thạo đao kiếm (Võ lực < 30).";
        }
        else if (_playerFatigue >= 95 || _playerStamina < 10)
        {
            DuelChallengeButton.IsEnabled = false;
            DuelChallengeButton.ToolTip = $"Thể lực cạn kiệt (mệt mỏi {_playerFatigue}/100). Hãy nghỉ ngơi trước khi lên võ đài!";
        }
        else
        {
            DuelChallengeButton.IsEnabled = true;
            DuelChallengeButton.ToolTip = $"Khiêu chiến {_npc.NameVi} lên võ đài so tài võ nghệ!";
        }
    }

    private void MeetNpc_Click(object sender, RoutedEventArgs e)
    {
        OnMeetNpc?.Invoke(_npcId);
        _hasMet = true;
        UpdateAfterMeeting();
    }

    private void UpdateAfterMeeting()
    {
        UpdatePills(sameCity: true);
        PopulateInnateStats();
        PopulateRelationship();
        ConfigureFooterActions();

        // Update Total Stats text
        var a = _npc.Attributes;
        var bonusTotal = _deltas?.TotalBonus ?? 0;
        var total = a.War + a.Ldr + a.Int + a.Pol + a.Dip + a.Com + a.Log + a.Cra + a.Con + a.Wil + a.Prc + a.Lck + bonusTotal;
        TotalStatsLabel.Text = "TỔNG ĐIỂM";
        TotalStatsText.Text = total.ToString();
        TotalStatsMaxText.Text = "/ 1200";

        UnmetStatsNotice.Visibility = Visibility.Collapsed;
    }

    private void DuelChallenge_Click(object sender, RoutedEventArgs e)
    {
        var id = _npcId;
        Close();
        OnChallengeDuel?.Invoke(id);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private UIElement BuildSkillCard(NpcSkillDef skill)
    {
        var card = new Border
        {
            Background = (Brush)FindResource("SurfaceAlt"),
            BorderBrush = (Brush)FindResource("Line"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 0, 0, 8)
        };

        var stack = new StackPanel();
        var headerDock = new DockPanel();

        var badgeStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(badgeStack, Dock.Right);

        var stars = new string('★', Math.Clamp(skill.Tier, 1, 5));
        badgeStack.Children.Add(new TextBlock
        {
            Text = stars,
            Foreground = (Brush)FindResource("Gain"),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        });

        var catName = skill.Category switch
        {
            "martial" => "Võ Đạo",
            "strategist" => "Mưu Lược",
            "governance" => "Trị Quốc",
            "commerce" => "Thương Mại",
            _ => "Kỳ Tài"
        };
        var catBorder = new Border
        {
            Background = (Brush)FindResource("Surface"),
            BorderBrush = (Brush)FindResource("Line"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2, 6, 2)
        };
        catBorder.Child = new TextBlock
        {
            Text = catName,
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("Accent")
        };
        badgeStack.Children.Add(catBorder);

        headerDock.Children.Add(badgeStack);

        headerDock.Children.Add(new TextBlock
        {
            Text = skill.NameVi,
            FontSize = 13.5,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("Text")
        });

        stack.Children.Add(headerDock);

        stack.Children.Add(new TextBlock
        {
            Text = skill.Description,
            Style = (Style)FindResource("Caption"),
            Foreground = (Brush)FindResource("Muted"),
            Margin = new Thickness(0, 4, 0, 2),
            TextWrapping = TextWrapping.Wrap
        });

        var effectBorder = new Border
        {
            Background = (Brush)FindResource("AccentSoft"),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(6, 2, 6, 2),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 4, 0, 0)
        };
        effectBorder.Child = new TextBlock
        {
            Text = $"⚡ Hiệu Quả: {skill.EffectSummary}",
            FontSize = 10.5,
            FontWeight = FontWeights.Medium,
            Foreground = (Brush)FindResource("Accent")
        };
        stack.Children.Add(effectBorder);

        card.Child = stack;
        return card;
    }

    private void AddStatItem(
        Panel container,
        string label,
        int baseValue,
        int bonus,
        string tip,
        bool verified,
        int? exp = null)
    {
        var effective = baseValue + bonus;
        var rank = effective switch
        {
            >= 95 => "S+",
            >= 90 => "S",
            >= 80 => "A",
            >= 70 => "B",
            >= 60 => "C",
            _ => "D"
        };

        var barBrush = (Brush)(effective >= 90
            ? FindResource("Accent")
            : effective >= 80
                ? FindResource("Gain")
                : effective >= 70
                    ? new SolidColorBrush(Color.FromRgb(0x4A, 0x90, 0xE2))
                    : FindResource("Muted"));

        var expBrush = (Brush)FindResource("Accent");

        // Rich dark-themed ToolTip description
        var tooltipBuilder = new System.Text.StringBuilder();
        tooltipBuilder.AppendLine(label);
        tooltipBuilder.AppendLine(tip);
        if (verified)
        {
            tooltipBuilder.AppendLine();
            tooltipBuilder.Append($"📊 Chỉ số: {effective} điểm (Bậc {rank})");
            if (bonus > 0)
            {
                tooltipBuilder.Append($" [Cơ bản: {baseValue}, Ngoại vật/Khí vận: +{bonus}]");
            }
            tooltipBuilder.AppendLine();
        }
        if (exp.HasValue)
        {
            var curExp = Math.Clamp(exp.Value, 0, 99);
            var needed = 100 - curExp;
            tooltipBuilder.AppendLine($"⭐ Kinh nghiệm chỉ số: {curExp}/100 EXP");
            tooltipBuilder.Append($"⚡ Tích lũy thêm {needed} EXP để thăng cấp lên {effective + 1}.");
        }

        var card = new Border
        {
            Background = (Brush)FindResource("SurfaceAlt"),
            BorderBrush = (Brush)FindResource("Line"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(4, 4, 4, 4),
            ToolTip = tooltipBuilder.ToString().TrimEnd()
        };

        var stack = new StackPanel();
        var line = new DockPanel();

        var valueStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(valueStack, Dock.Right);

        var displayVal = verified ? effective.ToString() : (effective >= 80 ? $"~{(effective / 5) * 5}" : "???");

        var valueText = new TextBlock
        {
            Text = displayVal,
            Style = (Style)FindResource("Number"),
            FontWeight = FontWeights.Bold,
            Foreground = barBrush,
            FontSize = 14
        };
        valueStack.Children.Add(valueText);

        if (bonus > 0 && verified)
        {
            var bonusText = new TextBlock
            {
                Text = $" (+{bonus})",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("Gain"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(3, 0, 0, 0)
            };
            valueStack.Children.Add(bonusText);
        }

        line.Children.Add(valueStack);

        var rankBorder = new Border
        {
            Background = (Brush)FindResource("Surface"),
            BorderBrush = (Brush)FindResource("Line"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(5, 1, 5, 1),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(rankBorder, Dock.Right);
        rankBorder.Child = new TextBlock
        {
            Text = verified ? rank : (effective >= 80 ? rank : "?"),
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            Foreground = barBrush
        };
        line.Children.Add(rankBorder);

        var labelText = new TextBlock
        {
            Text = label,
            Style = (Style)FindResource("Caption"),
            Foreground = (Brush)FindResource("Text"),
            VerticalAlignment = VerticalAlignment.Center
        };
        line.Children.Add(labelText);
        stack.Children.Add(line);

        if (exp.HasValue)
        {
            // EXP Sub-header: "EXP CHỈ SỐ" & "xx/100"
            var curExp = Math.Clamp(exp.Value, 0, 99);
            var expLine = new DockPanel { Margin = new Thickness(0, 5, 0, 2) };

            var expValText = new TextBlock
            {
                Text = $"{curExp}/100",
                Style = (Style)FindResource("Number"),
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = expBrush
            };
            DockPanel.SetDock(expValText, Dock.Right);
            expLine.Children.Add(expValText);

            var expLabelText = new TextBlock
            {
                Text = "EXP CHỈ SỐ",
                Style = (Style)FindResource("Overline"),
                FontSize = 9.5,
                Foreground = (Brush)FindResource("Faint"),
                VerticalAlignment = VerticalAlignment.Center
            };
            expLine.Children.Add(expLabelText);
            stack.Children.Add(expLine);

            // EXP Progress Track & Fill
            var track = new Border
            {
                Background = (Brush)FindResource("Surface"),
                BorderBrush = (Brush)FindResource("Line"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                Height = 6,
                Margin = new Thickness(0, 1, 0, 0)
            };

            var fillGrid = new Grid();
            var expRatio = Math.Clamp(curExp / 100.0, 0.0, 1.0);
            if (expRatio > 0.001)
            {
                fillGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(expRatio, GridUnitType.Star) });
                fillGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0 - expRatio, GridUnitType.Star) });

                var bar = new Border
                {
                    Background = expBrush,
                    CornerRadius = new CornerRadius(1)
                };
                Grid.SetColumn(bar, 0);
                fillGrid.Children.Add(bar);
            }
            track.Child = fillGrid;
            stack.Children.Add(track);
        }
        else
        {
            // NPC Stat Capacity Track & Fill
            var track = new Border
            {
                Background = (Brush)FindResource("Surface"),
                BorderBrush = (Brush)FindResource("Line"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                Height = 6,
                Margin = new Thickness(0, 6, 0, 0)
            };

            var fillGrid = new Grid();
            var ratio = Math.Clamp(effective / 120.0, 0.05, 1.0);
            fillGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ratio, GridUnitType.Star) });
            fillGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0 - ratio, GridUnitType.Star) });

            var bar = new Border
            {
                Background = barBrush,
                CornerRadius = new CornerRadius(1)
            };
            Grid.SetColumn(bar, 0);
            fillGrid.Children.Add(bar);

            track.Child = fillGrid;
            stack.Children.Add(track);
        }

        card.Child = stack;
        container.Children.Add(card);
    }

    private void AddMaskedStatItem(Panel container, string label, string tip)
    {
        var card = new Border
        {
            Background = (Brush)FindResource("SurfaceAlt"),
            BorderBrush = (Brush)FindResource("Line"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(4, 4, 4, 4),
            ToolTip = tip
        };

        var stack = new StackPanel();
        var line = new DockPanel();

        var valueText = new TextBlock
        {
            Text = "???",
            Style = (Style)FindResource("Number"),
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("Faint"),
            FontSize = 14
        };
        DockPanel.SetDock(valueText, Dock.Right);
        line.Children.Add(valueText);

        var rankBorder = new Border
        {
            Background = (Brush)FindResource("Surface"),
            BorderBrush = (Brush)FindResource("Line"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(5, 1, 5, 1),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(rankBorder, Dock.Right);
        rankBorder.Child = new TextBlock
        {
            Text = "🔒",
            FontSize = 10,
            Foreground = (Brush)FindResource("Faint")
        };
        line.Children.Add(rankBorder);

        var labelText = new TextBlock
        {
            Text = label,
            Style = (Style)FindResource("Caption"),
            Foreground = (Brush)FindResource("Muted"),
            VerticalAlignment = VerticalAlignment.Center
        };
        line.Children.Add(labelText);
        stack.Children.Add(line);

        var track = new Border
        {
            Background = (Brush)FindResource("Surface"),
            BorderBrush = (Brush)FindResource("Line"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2),
            Height = 6,
            Margin = new Thickness(0, 6, 0, 0),
            Opacity = 0.3
        };
        stack.Children.Add(track);

        card.Child = stack;
        container.Children.Add(card);
    }

    private static string DetermineRoleTag(string role)
    {
        var r = role.ToLowerInvariant();
        if (r.Contains("tướng") || r.Contains("quân") || r.Contains("soái")) return "VÕ";
        if (r.Contains("sư") || r.Contains("mưu") || r.Contains("học")) return "VĂN";
        if (r.Contains("thương") || r.Contains("buôn") || r.Contains("bản")) return "THƯƠNG";
        if (r.Contains("chủ") || r.Contains("vương") || r.Contains("đế")) return "CHÚA";
        return "SĨ";
    }
}
