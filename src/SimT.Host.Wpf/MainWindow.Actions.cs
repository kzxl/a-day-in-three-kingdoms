using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SimT.Contracts;
using SimT.Contracts.Ledger;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Host.Wpf;

public partial class MainWindow
{
    private void Advance_Click(object sender, RoutedEventArgs e) => Advance(1);

    private void Rest_Click(object sender, RoutedEventArgs e)
    {
        if (_kernel.Capabilities.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var attrs) && attrs is not null)
        {
            attrs.Rest(1);
        }

        Advance(1);
        StatusText.Text = "Đã nghỉ ngơi tĩnh dưỡng 1 ngày: Thể lực hồi phục trọn vẹn, tinh thần sảng khoái.";
    }

    private void AdvanceWeek_Click(object sender, RoutedEventArgs e) => Advance(7);

    private void Advance(int days)
    {
        try
        {
            var from = _kernel.Today;
            _kernel.AdvanceDays(days);
            _kernel.Save();
            UpdateHeader();
            RenderSelected();
            ShowReport(from, _kernel.Today);
            StatusText.Text = $"Đã sang {GameDateCodec.DescribeVi(_kernel.Today)}.";
        }
        catch (Exception error)
        {
            StatusText.Text = Describe(error);
        }
    }

    /// <summary>
    /// What the days just spent actually did: money in, money out, and the charges the month
    /// close took. The old turn report in one card.
    /// </summary>
    private void ShowReport(GameDate from, GameDate to)
    {
        var lines = _kernel.Ledger.Query(from, to, limit: 500);
        var income = lines.Where(line => line.Direction == LedgerDirection.In).Sum(line => line.Amount);
        var spend = lines.Where(line => line.Direction == LedgerDirection.Out).Sum(line => line.Amount);
        var charges = lines
            .Where(line => line.Direction == LedgerDirection.Out)
            .GroupBy(line => line.JournalCode)
            .OrderByDescending(group => group.Sum(line => line.Amount))
            .Take(3)
            .Select(group => $"{group.Key} {group.Sum(line => line.Amount).ToString("N0", Vietnamese)}");

        var days = (int)(to.DayIndex - from.DayIndex);
        var heading = $"BÁO CÁO · {GameDateCodec.ShortVi(from)} → {GameDateCodec.ShortVi(to)} ({days} ngày)";
        var summary = $"thu {income.ToString("N0", Vietnamese)} · chi {spend.ToString("N0", Vietnamese)} · "
                      + $"ròng {(income - spend).ToString("N0", Vietnamese)} quan";

        var card = new StackPanel();
        card.Children.Add(new TextBlock { Text = heading, Style = (Style)FindResource("Overline") });
        card.Children.Add(new TextBlock { Text = summary, Margin = new Thickness(0, 4, 0, 0) });
        if (charges.Any())
        {
            card.Children.Add(new TextBlock
            {
                Text = "khoản lớn nhất: " + string.Join(" · ", charges),
                Style = (Style)FindResource("Caption"),
                Foreground = (Brush)FindResource("Faint"),
                Margin = new Thickness(0, 3, 0, 0)
            });
        }

        ContentPanel.Children.Insert(0, new Border
        {
            Background = (Brush)FindResource("SurfaceAlt"),
            BorderBrush = (Brush)FindResource("Line"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 10, 14, 10),
            Margin = new Thickness(0, 0, 0, 14),
            Child = card
        });
    }

    private void RowCommand_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string commandId })
        {
            return;
        }

        if (commandId.StartsWith("npc:modal:") && long.TryParse(commandId["npc:modal:".Length..], out var npcId))
        {
            TryOpenNpcModal(npcId);
            return;
        }

        if (commandId.StartsWith("duel:") && long.TryParse(commandId["duel:".Length..], out var duelNpcId))
        {
            TryOpenDuelModal(duelNpcId);
            return;
        }

        var screenId = ScreenForCommand();
        if (!_kernel.TryGetCommandHandler(screenId, out var handler) || handler is null)
        {
            StatusText.Text = $"Màn hình '{screenId}' không nhận lệnh.";
            return;
        }

        try
        {
            StatusText.Text = handler.Execute(commandId) ?? "Xong.";
        }
        catch (Exception error)
        {
            StatusText.Text = Describe(error);
        }

        _kernel.Save();
        UpdateHeader();
        RenderSelected();
    }

    private string ScreenForCommand() =>
        NavList.SelectedItem is NavEntry entry ? entry.ScreenId : _screenId;

    private void PlayerCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        OpenPlayerProfileModal();
    }

    private void OpenPlayerProfileModal()
    {
        if (!_kernel.Capabilities.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var playerState) || playerState is null ||
            !_kernel.Capabilities.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var attributes) || attributes is null)
        {
            StatusText.Text = "Hồ sơ nhân vật chưa sẵn sàng.";
            return;
        }

        var citadelName = "Lạc Dương";
        if (_kernel.Capabilities.TryGet<IWorldMap>(CapabilityIds.WorldMap, out var map) && map is not null)
        {
            var cit = map.Find(playerState.CurrentCityId);
            if (cit is not null)
            {
                citadelName = $"{cit.NameVi} ({cit.Region})";
            }
        }

        var v = attributes.Vitals;
        var s = attributes.Social;

        var learnedSkills = new List<NpcSkillDef>();
        if (_kernel.Capabilities.TryGet<INpcRegistry>(CapabilityIds.NpcRegistry, out var reg) && reg is not null)
        {
            var skillLookup = reg.All.SelectMany(n => n.Skills).DistinctBy(sk => sk.Code).ToDictionary(sk => sk.Code, StringComparer.OrdinalIgnoreCase);
            foreach (var code in attributes.LearnedSkills)
            {
                if (skillLookup.TryGetValue(code, out var sDef))
                {
                    learnedSkills.Add(sDef);
                }
            }
        }

        var protagonistDef = new NpcDef(
            0,
            "protagonist",
            playerState.Name,
            "主角",
            "Vô Danh",
            "Thương Nhân",
            playerState.CurrentCityId,
            attributes.Attributes,
            "Chí tại bốn phương, cơ biến mẫn tiệp, khéo xoay sở thương trường.",
            $"Khởi nghiệp thời loạn thế Tam Quốc. Hiện ngụ cư tại {citadelName}. " +
            $"Thể lực hiện tại: {v.CurrentStamina}/{v.MaxStamina} (Mệt mỏi: {v.Fatigue}%, Áp lực: {v.Stress}%). " +
            $"Danh vọng thiên hạ: {s.Fame} điểm, Đức độ: {s.Virtue} điểm, Tín nghĩa: {s.Credibility} điểm.",
            learnedSkills);

        NpcDetailWindow.ShowNpc(this, protagonistDef, citadelName);
        StatusText.Text = $"Đã mở hồ sơ nhân vật chính: {playerState.Name}.";
    }

    private void TryOpenNpcModal(long npcId)
    {
        if (!_kernel.Capabilities.TryGet<INpcRegistry>(CapabilityIds.NpcRegistry, out var registry) || registry is null)
        {
            StatusText.Text = "Hệ thống nhân sĩ chưa sẵn sàng.";
            return;
        }

        var npc = registry.Find(npcId);
        if (npc is null)
        {
            StatusText.Text = $"Không tìm thấy nhân sĩ mã {npcId}.";
            return;
        }

        NpcActivityDef? activity = null;
        NpcStatDelta? deltas = null;
        var currentCitId = npc.CitadelId;

        if (_kernel.Capabilities.TryGet<ISocietyChronicle>(CapabilityIds.SocietyChronicle, out var society) && society is not null)
        {
            activity = society.GetNpcActivity(npcId);
            deltas = society.GetStatDeltas(npcId);
            if (activity is not null)
            {
                currentCitId = activity.CurrentCitadelId;
            }
        }

        RelationshipDef? relationship = null;
        RetainerDef? retainer = null;
        if (_kernel.Capabilities.TryGet<ISocialNetwork>(CapabilityIds.SocialNetwork, out var social) && social is not null)
        {
            relationship = social.GetRelationship(npcId);
            retainer = social.GetRetainer(npcId);
        }

        var citadelName = "Ẩn sĩ";
        if (_kernel.Capabilities.TryGet<IWorldMap>(CapabilityIds.WorldMap, out var map) && map is not null)
        {
            var cit = map.Find(currentCitId);
            if (cit is not null)
            {
                citadelName = cit.NameVi;
            }
        }

        NpcDetailWindow.ShowNpc(this, npc, citadelName, deltas, activity, relationship, retainer, id => TryOpenDuelModal(id));
        StatusText.Text = $"Đang xem hồ sơ: {npc.NameVi} ({npc.StyleNameVi ?? npc.Role}).";
    }

    private void TryOpenDuelModal(long npcId)
    {
        if (!_kernel.Capabilities.TryGet<IMilitaryCommand>(CapabilityIds.MilitaryCommand, out var military) || military is null ||
            !_kernel.Capabilities.TryGet<INpcRegistry>(CapabilityIds.NpcRegistry, out var registry) || registry is null ||
            !_kernel.Capabilities.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var attributes) || attributes is null ||
            !_kernel.Capabilities.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var playerState) || playerState is null ||
            !_kernel.Capabilities.TryGet<IWallet>(CapabilityIds.PlayerWallet, out var wallet) || wallet is null ||
            !_kernel.Capabilities.TryGet<IWorldMap>(CapabilityIds.WorldMap, out var worldMap) || worldMap is null)
        {
            StatusText.Text = "Hệ thống quân cơ tỷ võ chưa sẵn sàng.";
            return;
        }

        var npc = registry.Find(npcId);
        if (npc is null)
        {
            StatusText.Text = $"Không tìm thấy võ sĩ mã {npcId}.";
            return;
        }

        if (attributes.Vitals.CurrentStamina < 10 || attributes.Vitals.Fatigue >= 95)
        {
            StatusText.Text = $"Thể lực đã cạn kiệt (mệt mỏi {attributes.Vitals.Fatigue}/100). Hãy nghỉ ngơi trước khi lên đài!";
            return;
        }

        var citadel = worldMap.Find(playerState.CurrentCityId);
        var citadelName = citadel?.NameVi ?? "Châu quận";

        var duelWindow = new DuelBattleWindow(
            military,
            registry,
            attributes,
            playerState,
            wallet,
            npcId,
            citadelName)
        {
            Owner = this
        };

        duelWindow.ShowDialog();

        _kernel.Save();
        UpdateHeader();
        RenderSelected();
        StatusText.Text = $"Trận tỷ võ với {npc.NameVi} đã hoàn tất.";
    }
}
