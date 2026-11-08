using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SimT.Contracts;
using SimT.Contracts.Ledger;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;
using SimT.Contracts.Ui;

namespace SimT.Host.Wpf;

public partial class MainWindow
{
    private void RegisterModalPresenters()
    {
        _kernel.Ui.RegisterModalPresenter("talk:npc:", commandId =>
        {
            var uri = CommandUri.Parse(commandId);
            if (uri.TryGetParamLong(out var talkNpcId) || uri.TryGetTargetLong(out talkNpcId))
            {
                if (!TryOpenTalkScene(talkNpcId))
                {
                    MeetNpcFallback(talkNpcId);
                }
            }
        });

        _kernel.Ui.RegisterModalPresenter("npc:modal:", commandId =>
        {
            var uri = CommandUri.Parse(commandId);
            if (uri.TryGetParamLong(out var npcId) || uri.TryGetTargetLong(out npcId))
            {
                TryOpenNpcModal(npcId);
            }
        });

        _kernel.Ui.RegisterModalPresenter("duel:", commandId =>
        {
            var uri = CommandUri.Parse(commandId);
            if (uri.TryGetTargetLong(out var duelNpcId) || uri.TryGetParamLong(out duelNpcId))
            {
                TryOpenDuelModal(duelNpcId);
            }
        });

        _kernel.Ui.RegisterModalPresenter("letter:read:", commandId =>
        {
            var uri = CommandUri.Parse(commandId);
            if (uri.TryGetParamLong(out var letterId) || uri.TryGetTargetLong(out letterId))
            {
                TryOpenLetterModal(letterId);
            }
        });

        _kernel.Ui.RegisterModalPresenter("item:modal:", commandId =>
        {
            var uri = CommandUri.Parse(commandId);
            var raw = uri.Parameter ?? uri.Target ?? string.Empty;
            if (long.TryParse(raw, out var itemId))
            {
                TryOpenItemDetailModal(itemId);
            }
            else if (!string.IsNullOrEmpty(raw))
            {
                TryOpenItemDetailModalByCode(raw);
            }
        });

        _kernel.Ui.RegisterModalPresenter("stronghold:inspect:", commandId =>
        {
            var uri = CommandUri.Parse(commandId);
            if (uri.TryGetParamLong(out var shId) || uri.TryGetTargetLong(out shId))
            {
                TryOpenStrongholdDetailModal(shId);
            }
        });

        _kernel.Ui.RegisterModalPresenter("stronghold:assault:", commandId =>
        {
            var uri = CommandUri.Parse(commandId);
            if (uri.TryGetParamLong(out var shId) || uri.TryGetTargetLong(out shId))
            {
                TryOpenStrongholdDetailModal(shId);
            }
        });

        _kernel.Ui.RegisterModalPresenter("stronghold:modal:", commandId =>
        {
            var uri = CommandUri.Parse(commandId);
            if (uri.TryGetParamLong(out var shId) || uri.TryGetTargetLong(out shId))
            {
                TryOpenStrongholdDetailModal(shId);
            }
        });

        _kernel.Ui.RegisterModalPresenter("inspect:", commandId =>
        {
            var uri = CommandUri.Parse(commandId);
            if (uri.TryGetParamLong(out var citId) || uri.TryGetTargetLong(out citId))
            {
                TryOpenCitadelDetailModal(citId);
            }
        });

        _kernel.Ui.RegisterModalPresenter("citadel:inspect:", commandId =>
        {
            var uri = CommandUri.Parse(commandId);
            if (uri.TryGetParamLong(out var citId) || uri.TryGetTargetLong(out citId))
            {
                TryOpenCitadelDetailModal(citId);
            }
        });

        _kernel.Ui.RegisterModalPresenter("citadel:modal:", commandId =>
        {
            var uri = CommandUri.Parse(commandId);
            if (uri.TryGetParamLong(out var citId) || uri.TryGetTargetLong(out citId))
            {
                TryOpenCitadelDetailModal(citId);
            }
        });

        _kernel.Ui.RegisterModalPresenter("faction:inspect:", commandId =>
        {
            var uri = CommandUri.Parse(commandId);
            if (uri.TryGetParamLong(out var facId) || uri.TryGetTargetLong(out facId))
            {
                TryOpenFactionDetailModal(facId);
            }
        });

        _kernel.Ui.RegisterModalPresenter("faction:modal:", commandId =>
        {
            var uri = CommandUri.Parse(commandId);
            if (uri.TryGetParamLong(out var facId) || uri.TryGetTargetLong(out facId))
            {
                TryOpenFactionDetailModal(facId);
            }
        });

        _kernel.Ui.RegisterModalPresenter("route:inspect:", commandId =>
        {
            var raw = commandId.Substring("route:inspect:".Length);
            var parts = raw.Split(':');
            var fromId = parts.Length > 0 && long.TryParse(parts[0], out var f) ? f : 1;
            var toId = parts.Length > 1 && long.TryParse(parts[1], out var t) ? t : 2;
            TryOpenCaravanRouteDetailModal(fromId, toId);
        });

        _kernel.Ui.RegisterModalPresenter("plot:inspect:", commandId =>
        {
            var uri = CommandUri.Parse(commandId);
            if (uri.TryGetParamLong(out var plotId) || uri.TryGetTargetLong(out plotId))
            {
                TryOpenFarmsteadDetailModal(plotId);
            }
        });

        _kernel.Ui.RegisterModalPresenter("farm:inspect:", commandId =>
        {
            var uri = CommandUri.Parse(commandId);
            if (uri.TryGetParamLong(out var plotId) || uri.TryGetTargetLong(out plotId))
            {
                TryOpenFarmsteadDetailModal(plotId);
            }
        });

        _kernel.Ui.RegisterModalPresenter("history:view:", commandId =>
        {
            var code = commandId["history:view:".Length..].Trim();
            if (!string.IsNullOrEmpty(code))
            {
                TryOpenHistoricalEventScene(code, isReplay: true);
            }
        });
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

        var bagItemList = new List<ProtagonistBagItem>();
        if (_kernel.Capabilities.TryGet<IBag>(CapabilityIds.PlayerBag, out var bag) && bag is not null &&
            _kernel.Capabilities.TryGet<IItemCatalog>(CapabilityIds.ItemCatalog, out var catalog) && catalog is not null)
        {
            _kernel.Capabilities.TryGet<IItemHubService>(CapabilityIds.ItemHub, out var hub);
            foreach (var item in catalog.All)
            {
                var qty = bag.Quantity(playerState.CurrentCityId, item.Id);
                if (qty > 0)
                {
                    var entry = hub?.FindEntry(item.Id);
                    var icon = entry?.VisualIcon ?? "📦";
                    var subCat = entry?.SubCategoryVi ?? SimT.Contracts.Scenes.RoleTitleVi.FormatItemCategory(item.Category);
                    var summary = entry?.LoreDescriptionVi ?? item.NameVi;
                    bagItemList.Add(new ProtagonistBagItem(
                        item.Id,
                        item.Code,
                        item.NameVi,
                        subCat,
                        icon,
                        qty,
                        item.BasePrice,
                        item.Weight,
                        item.ShelfLifeDays,
                        summary));
                }
            }
        }

        Func<IReadOnlyList<ProtagonistBagItem>> reloadBag = () =>
        {
            var list = new List<ProtagonistBagItem>();
            if (_kernel.Capabilities.TryGet<IBag>(CapabilityIds.PlayerBag, out var b) && b is not null &&
                _kernel.Capabilities.TryGet<IItemCatalog>(CapabilityIds.ItemCatalog, out var c) && c is not null)
            {
                _kernel.Capabilities.TryGet<IItemHubService>(CapabilityIds.ItemHub, out var h);
                foreach (var item in c.All)
                {
                    var qty = b.Quantity(playerState.CurrentCityId, item.Id);
                    if (qty > 0)
                    {
                        var entry = h?.FindEntry(item.Id);
                        var icon = entry?.VisualIcon ?? "📦";
                        var subCat = entry?.SubCategoryVi ?? SimT.Contracts.Scenes.RoleTitleVi.FormatItemCategory(item.Category);
                        var summary = entry?.LoreDescriptionVi ?? item.NameVi;
                        list.Add(new ProtagonistBagItem(
                            item.Id,
                            item.Code,
                            item.NameVi,
                            subCat,
                            icon,
                            qty,
                            item.BasePrice,
                            item.Weight,
                            item.ShelfLifeDays,
                            summary));
                    }
                }
            }
            return list;
        };

        _kernel.Capabilities.TryGet<IMarketWarehouse>(CapabilityIds.MarketWarehouse, out var whProfile);
        var curWhInfo = whProfile?.Info(playerState.CurrentCityId);

        Func<long, int, (bool, string)> quickStore = (itId, q) =>
        {
            if (whProfile is null) return (false, "Chưa kích hoạt kho bãi.");
            var ok = whProfile.Store(playerState.CurrentCityId, itId, q, out var msg);
            if (ok)
            {
                StatusText.Text = msg;
                InvalidateBadges(); RefreshBadges();
                RenderSelected();
            }
            return (ok, msg);
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
            npcNameLookup,
            bagItemList,
            itemId => TryOpenItemDetailModal(itemId),
            reloadBag,
            curWhInfo,
            quickStore);

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
        if (npc is null || !registry.IsPresent(npcId))
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
        if (TryOpenTalkScene(npcId))
        {
            return;
        }
        MeetNpcFallback(npcId);
    }

    private void MeetNpcFallback(long npcId)
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
        if (npc is null || !registry.IsPresent(npcId))
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

        if (TryOpenDuelScene(npc, citadelName)) return;

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

    public void TryOpenItemDetailModal(long itemId, Window? owner = null)
    {
        if (!_kernel.Capabilities.TryGet<IItemHubService>(CapabilityIds.ItemHub, out var hub) || hub is null)
        {
            StatusText.Text = "Hệ thống Đồ Giám Kỳ Vật chưa khả dụng.";
            return;
        }

        var entry = hub.FindEntry(itemId);
        if (entry is null)
        {
            StatusText.Text = $"Không tìm thấy thông tin kỳ vật #{itemId}.";
            return;
        }

        var cityId = 1L;
        if (_kernel.Capabilities.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var player) && player is not null)
        {
            cityId = player.CurrentCityId;
        }

        var bagQty = 0;
        if (_kernel.Capabilities.TryGet<IBag>(CapabilityIds.PlayerBag, out var bag) && bag is not null)
        {
            bagQty = bag.Quantity(cityId, itemId);
        }

        _kernel.Capabilities.TryGet<IMarketWarehouse>(CapabilityIds.MarketWarehouse, out var warehouse);
        var whInfo = warehouse?.Info(cityId);
        var whQty = warehouse?.Stock(cityId).FirstOrDefault(s => s.ItemId == itemId)?.Quantity ?? 0;

        Func<int, (bool, string)> onStore = qty =>
        {
            if (warehouse is null) return (false, "Chưa kích hoạt tính năng kho bãi.");
            var ok = warehouse.Store(cityId, itemId, qty, out var msg);
            if (ok)
            {
                StatusText.Text = msg;
                InvalidateBadges(); RefreshBadges();
                RenderSelected();
            }
            return (ok, msg);
        };

        Func<int, (bool, string)> onTake = qty =>
        {
            if (warehouse is null) return (false, "Chưa kích hoạt tính năng kho bãi.");
            var ok = warehouse.Take(cityId, itemId, qty, out var msg);
            if (ok)
            {
                StatusText.Text = msg;
                InvalidateBadges(); RefreshBadges();
                RenderSelected();
            }
            return (ok, msg);
        };

        Func<(bool, string)> onRent = () =>
        {
            if (warehouse is null) return (false, "Chưa kích hoạt tính năng kho bãi.");
            var ok = warehouse.Rent(cityId, out var msg);
            if (ok)
            {
                StatusText.Text = msg;
                InvalidateBadges(); RefreshBadges();
                RenderSelected();
            }
            return (ok, msg);
        };

        Action onChanged = () =>
        {
            InvalidateBadges(); RefreshBadges();
            RenderSelected();
        };

        var activeOwner = owner ?? Application.Current.Windows.OfType<Window>().LastOrDefault(w => w.IsActive) ?? this;
        var win = new ItemDetailModalWindow(entry, bagQty, whQty, whInfo, onStore, onTake, onRent, onChanged)
        {
            Owner = activeOwner
        };
        win.ShowDialog();
    }

    public void TryOpenItemDetailModalByCode(string code)
    {
        if (_kernel.Capabilities.TryGet<IItemHubService>(CapabilityIds.ItemHub, out var hub) && hub is not null)
        {
            var entry = hub.FindEntryByCode(code);
            if (entry is not null)
            {
                TryOpenItemDetailModal(entry.Item.Id);
            }
        }
    }



    public void TryOpenStrongholdDetailModal(long strongholdId)
    {
        if (!_kernel.Capabilities.TryGet<IMilitaryCommand>(CapabilityIds.MilitaryCommand, out var military) || military is null ||
            !_kernel.Capabilities.TryGet<IWorldMap>(CapabilityIds.WorldMap, out var worldMap) || worldMap is null ||
            !_kernel.Capabilities.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var playerState) || playerState is null)
        {
            StatusText.Text = "Hệ thống quân bị tiễu phạt chưa sẵn sàng.";
            return;
        }

        var citadel = worldMap.Find(playerState.CurrentCityId);
        var citadelName = citadel?.NameVi ?? "Châu quận";

        var modal = new StrongholdDetailModalWindow(
            _kernel,
            military,
            strongholdId,
            citadelName)
        {
            Owner = this
        };

        modal.HubStateChanged += () =>
        {
            _kernel.Save();
            UpdateHeader();
            RenderSelected();
        };

        modal.ShowDialog();

        _kernel.Save();
        UpdateHeader();
        RenderSelected();
    }



    public void TryOpenCitadelDetailModal(long citadelId)
    {
        var modal = new CitadelDetailModalWindow(_kernel, citadelId)
        {
            Owner = this
        };
        modal.HubStateChanged += () =>
        {
            _kernel.Save();
            UpdateHeader();
            RenderSelected();
        };
        modal.ShowDialog();
        _kernel.Save();
        UpdateHeader();
        RenderSelected();
    }

    public void TryOpenFactionDetailModal(long factionId)
    {
        var modal = new FactionDetailModalWindow(_kernel, factionId)
        {
            Owner = this
        };
        modal.HubStateChanged += () =>
        {
            _kernel.Save();
            UpdateHeader();
            RenderSelected();
        };
        modal.ShowDialog();
        _kernel.Save();
        UpdateHeader();
        RenderSelected();
    }

    public void TryOpenCaravanRouteDetailModal(long fromCityId, long toCityId)
    {
        var modal = new CaravanRouteDetailModalWindow(_kernel, fromCityId, toCityId)
        {
            Owner = this
        };
        modal.HubStateChanged += () =>
        {
            _kernel.Save();
            UpdateHeader();
            RenderSelected();
        };
        modal.ShowDialog();
        _kernel.Save();
        UpdateHeader();
        RenderSelected();
    }

    public void TryOpenFarmsteadDetailModal(long plotId)
    {
        var modal = new FarmsteadDetailModalWindow(_kernel, plotId)
        {
            Owner = this
        };
        modal.HubStateChanged += () =>
        {
            _kernel.Save();
            UpdateHeader();
            RenderSelected();
        };
        modal.ShowDialog();
        _kernel.Save();
        UpdateHeader();
        RenderSelected();
    }

}

