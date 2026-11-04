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
    private bool _isAdvancing;

    private async void Advance_Click(object sender, RoutedEventArgs e) => await AdvanceAsync(1);

    private async void Rest_Click(object sender, RoutedEventArgs e)
    {
        if (_isAdvancing) return;

        if (_kernel.Capabilities.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var attrs) && attrs is not null)
        {
            attrs.Rest(1);
        }

        await AdvanceAsync(1, "Đã nghỉ ngơi tĩnh dưỡng 1 ngày: Thể lực hồi phục trọn vẹn, tinh thần sảng khoái.");
    }

    private async void AdvanceWeek_Click(object sender, RoutedEventArgs e) => await AdvanceAsync(7);

    public void AdvanceSync(int days)
    {
        var from = _kernel.Today;
        _kernel.AdvanceDays(days);
        _kernel.Save();
        UpdateHeader();
        RenderSelected();
        ShowReport(from, _kernel.Today);
    }

    private async Task AdvanceAsync(int days, string? customStatus = null)
    {
        if (_isAdvancing) return;
        _isAdvancing = true;

        try
        {
            SetAdvancingState(true);
            var from = _kernel.Today;

            for (var step = 1; step <= days; step++)
            {
                var prev = _kernel.Today;
                _kernel.AdvanceDays(1);
                var curr = _kernel.Today;

                await CalendarBar.StepToDateAsync(prev, curr, step, days);
                UpdateHeaderDateOnly(curr);
            }

            _kernel.Save();
            UpdateHeader();
            RenderSelected();
            ShowReport(from, _kernel.Today);
            StatusText.Text = customStatus ?? $"Đã sang {GameDateCodec.DescribeVi(_kernel.Today)} (+{days} ngày).";
        }
        catch (Exception error)
        {
            StatusText.Text = Describe(error);
        }
        finally
        {
            SetAdvancingState(false);
            _isAdvancing = false;
        }
    }

    private void SetAdvancingState(bool busy)
    {
        AdvanceButton.IsEnabled = !busy;
        RestButton.IsEnabled = !busy;
        AdvanceWeekButton.IsEnabled = !busy;
        Cursor = busy ? System.Windows.Input.Cursors.Wait : null;
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

    private void RegisterModalPresenters()
    {
        _kernel.Ui.RegisterModalPresenter("npc:modal:", commandId =>
        {
            if (long.TryParse(commandId["npc:modal:".Length..], out var npcId))
            {
                TryOpenNpcModal(npcId);
            }
        });

        _kernel.Ui.RegisterModalPresenter("duel:", commandId =>
        {
            if (long.TryParse(commandId["duel:".Length..], out var duelNpcId))
            {
                TryOpenDuelModal(duelNpcId);
            }
        });

        _kernel.Ui.RegisterModalPresenter("letter:read:", commandId =>
        {
            if (long.TryParse(commandId["letter:read:".Length..], out var letterId))
            {
                TryOpenLetterModal(letterId);
            }
        });
    }

    private async void RowCommand_Click(object sender, RoutedEventArgs e)
    {
        if (_isAdvancing) return;

        if (sender is not Button { Tag: string commandId })
        {
            return;
        }

        if (_kernel.Ui.TryPresentModal(commandId))
        {
            return;
        }

        var screenId = ScreenForCommand();
        if (!_kernel.TryGetCommandHandler(screenId, out var handler) || handler is null)
        {
            StatusText.Text = $"Màn hình '{screenId}' không nhận lệnh.";
            return;
        }

        var fromDate = _kernel.Today;
        string? resultMsg = null;
        try
        {
            resultMsg = handler.Execute(commandId) ?? "Xong.";
            StatusText.Text = resultMsg;
        }
        catch (Exception error)
        {
            StatusText.Text = Describe(error);
        }

        var toDate = _kernel.Today;
        if (toDate != fromDate)
        {
            _isAdvancing = true;
            SetAdvancingState(true);
            try
            {
                var daysDiff = (toDate.Year - fromDate.Year) * 360 + (toDate.Month - fromDate.Month) * 30 + (toDate.Day - fromDate.Day);
                await CalendarBar.AnimateCatchUpAsync(fromDate, toDate, $"Hoàn thành công việc (+{daysDiff} ngày)");
            }
            finally
            {
                SetAdvancingState(false);
                _isAdvancing = false;
            }
        }

        _kernel.Save();
        UpdateHeader();
        RenderSelected();
    }

    private string ScreenForCommand() =>
        NavList.SelectedItem is NavEntry entry ? entry.ScreenId : _screenId;

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new DlcSettingsWindow(_kernel) { Owner = this };
        settingsWindow.ShowDialog();
    }

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
            foreach (var prog in attributes.SkillProgresses)
            {
                if (skillLookup.TryGetValue(prog.SkillCode, out var sDef))
                {
                    var slotNote = prog.IsEquipped ? $" [Ô {prog.EquippedSlot}]" : "";
                    var enhancedSkill = sDef with
                    {
                        NameVi = $"{sDef.NameVi} Lv.{prog.Level}{slotNote}",
                        EffectSummary = $"{sDef.EffectSummary} (Thuần Thục: {(prog.Level >= 5 ? "Tối Đa" : $"{prog.Proficiency}/{prog.MaxProficiency}")})"
                    };
                    learnedSkills.Add(enhancedSkill);
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

        long liquidGold = 0;
        if (_kernel.Capabilities.TryGet<IWallet>(CapabilityIds.PlayerWallet, out var wallet) && wallet is not null)
        {
            liquidGold = wallet.Liquid;
        }

        long portfolioVal = 0;
        if (_kernel.Capabilities.TryGet<IMarketPortfolio>(CapabilityIds.MarketPortfolio, out var portfolio) && portfolio is not null)
        {
            portfolioVal = portfolio.InventoryValue();
        }

        IReadOnlyList<RetainerDef> retainers = Array.Empty<RetainerDef>();
        IReadOnlyList<SwornBrotherDef> swornBrothers = Array.Empty<SwornBrotherDef>();
        if (_kernel.Capabilities.TryGet<ISocialNetwork>(CapabilityIds.SocialNetwork, out var social) && social is not null)
        {
            retainers = social.GetRetainers();
            swornBrothers = social.GetSwornBrothers();
        }

        bool isMarried = false;
        string? spouseName = null;
        int childCount = 0;
        if (_kernel.Capabilities.TryGet<ILineageHouse>(CapabilityIds.LineageHouse, out var lineage) && lineage is not null)
        {
            isMarried = lineage.IsMarried;
            spouseName = lineage.SpouseName;
            childCount = lineage.ChildCount;
        }

        Func<long, string?> npcNameLookup = id =>
        {
            if (_kernel.Capabilities.TryGet<INpcRegistry>(CapabilityIds.NpcRegistry, out var nr) && nr is not null)
            {
                return nr.Find(id)?.NameVi;
            }
            return null;
        };

        var extraData = new ProtagonistExtraData(
            v,
            s,
            liquidGold,
            portfolioVal,
            retainers,
            swornBrothers,
            isMarried,
            spouseName,
            childCount,
            npcNameLookup);

        NpcDetailWindow.ShowNpc(
            this,
            protagonistDef,
            citadelName,
            npcCitadelId: playerState.CurrentCityId,
            playerCityId: playerState.CurrentCityId,
            hasMet: true,
            deltas: null,
            activity: null,
            relationship: null,
            retainer: null,
            playerFatigue: v.Fatigue,
            playerStamina: v.CurrentStamina,
            onChallengeDuel: null,
            onMeetNpc: null,
            exps: attributes.Exps,
            protagonistExtra: extraData);
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
        IReadOnlyList<MemoryEntry>? memories = null;
        bool hasMet = false;
        if (_kernel.Capabilities.TryGet<ISocialNetwork>(CapabilityIds.SocialNetwork, out var social) && social is not null)
        {
            hasMet = social.HasMet(npcId);
            relationship = hasMet ? social.GetRelationship(npcId) : null;
            retainer = social.GetRetainer(npcId);
            if (hasMet)
            {
                memories = social.GetMemories(npcId, 10);
            }
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

        var playerCityId = 1L;
        var playerFatigue = 0;
        var playerStamina = 100;
        var playerPrc = 50;
        var hasBaLac = false;
        var hasNguyetDan = false;
        var hasVongKhi = false;
        if (_kernel.Capabilities.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var playerState) && playerState is not null)
        {
            playerCityId = playerState.CurrentCityId;
        }
        if (_kernel.Capabilities.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var playerAttrs) && playerAttrs is not null)
        {
            playerFatigue = playerAttrs.Vitals.Fatigue;
            playerStamina = playerAttrs.Vitals.CurrentStamina;
            playerPrc = playerAttrs.EffectivePrc;
            hasBaLac = playerAttrs.IsSkillEquipped("ba_lac_than_nhan");
            hasNguyetDan = playerAttrs.IsSkillEquipped("nguyet_dan_giam_dinh");
            hasVongKhi = playerAttrs.IsSkillEquipped("vong_khi_thien_co");
        }

        NpcDetailWindow.ShowNpc(
            this,
            npc,
            citadelName,
            currentCitId,
            playerCityId,
            hasMet,
            deltas,
            activity,
            relationship,
            retainer,
            playerFatigue,
            playerStamina,
            id => TryOpenDuelModal(id),
            id => MeetNpcFromModal(id),
            memories: memories,
            playerPrc: playerPrc,
            hasBaLac: hasBaLac,
            hasNguyetDan: hasNguyetDan,
            hasVongKhi: hasVongKhi);

        StatusText.Text = $"Đang xem hồ sơ: {npc.NameVi} ({npc.StyleNameVi ?? npc.Role}).";
    }

    private void MeetNpcFromModal(long npcId)
    {
        if (_kernel.Capabilities.TryGet<ISocialNetwork>(CapabilityIds.SocialNetwork, out var social) && social is not null)
        {
            try
            {
                var res = social.ChatWithNpc(npcId);
                StatusText.Text = res;

                if (_kernel.Capabilities.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var attrs) && attrs is not null)
                {
                    attrs.AddExp("PRC", 5);
                }

                _kernel.Save();
                UpdateHeader();
                RenderSelected();
            }
            catch (Exception ex)
            {
                StatusText.Text = ex.Message;
            }
        }
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

        // 1. Same Citadel Check
        var currentCitId = npc.CitadelId;
        if (_kernel.Capabilities.TryGet<ISocietyChronicle>(CapabilityIds.SocietyChronicle, out var society) && society is not null)
        {
            var act = society.GetNpcActivity(npcId);
            if (act is not null)
            {
                currentCitId = act.CurrentCitadelId;
            }
        }

        if (currentCitId != playerState.CurrentCityId)
        {
            var targetCit = worldMap.Find(currentCitId);
            var targetCitName = targetCit?.NameVi ?? "châu quận khác";
            StatusText.Text = $"Không thể so tài: {npc.NameVi} hiện đang ở {targetCitName}, không ở cùng thành trì với bạn.";
            return;
        }

        // 2. Acquaintance Check (hasMet)
        bool hasMet = false;
        if (_kernel.Capabilities.TryGet<ISocialNetwork>(CapabilityIds.SocialNetwork, out var social) && social is not null)
        {
            hasMet = social.HasMet(npcId);
        }
        if (!hasMet)
        {
            StatusText.Text = $"Bạn chưa từng diện kiến {npc.NameVi}. Hãy đến gặp gỡ, đàm đạo trước khi hẹn ngày so tài võ nghệ.";
            return;
        }

        // 3. Martial Capability Check
        if (npc.Attributes.War < 30)
        {
            StatusText.Text = $"{npc.NameVi} là văn nhân/danh sĩ không theo nghiệp đao kiếm (Võ lực < 30), từ chối thượng đài.";
            return;
        }

        // 4. Vitals Check
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

    private void TryOpenLetterModal(long letterId)
    {
        if (!_kernel.Capabilities.TryGet<ISocialNetwork>(CapabilityIds.SocialNetwork, out var social) || social is null)
        {
            StatusText.Text = "Hệ thống nhân duyên chưa sẵn sàng.";
            return;
        }

        var letter = social.GetLetter(letterId);
        if (letter is null)
        {
            StatusText.Text = $"Không tìm thấy phong thư mã {letterId}.";
            return;
        }

        var citadelName = "Phương xa";
        if (_kernel.Capabilities.TryGet<IWorldMap>(CapabilityIds.WorldMap, out var map) && map is not null)
        {
            var cit = map.Find(letter.SenderCitadelId);
            if (cit is not null)
            {
                citadelName = cit.NameVi;
            }
        }

        var win = new LetterDetailWindow(letter, citadelName, social, onStateChanged: () =>
        {
            _kernel.Save();
            UpdateHeader();
            RenderSelected();
        })
        {
            Owner = this
        };

        win.ShowDialog();

        _kernel.Save();
        UpdateHeader();
        RenderSelected();
    }
}
