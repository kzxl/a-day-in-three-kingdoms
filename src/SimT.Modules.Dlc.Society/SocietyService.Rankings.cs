using SimT.Contracts.Data;
using SimT.Contracts.Ledger;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Society;

internal sealed partial class SocietyService
{
    public IReadOnlyList<RealmRankingBoard> GetAllRealmRankings(int top = 15)
    {
        return
        [
            GetRealmRanking(RealmRankingCategory.Wealth, top),
            GetRealmRanking(RealmRankingCategory.Fame, top),
            GetRealmRanking(RealmRankingCategory.Martial, top),
            GetRealmRanking(RealmRankingCategory.Wisdom, top),
            GetRealmRanking(RealmRankingCategory.Influence, top)
        ];
    }

    public RealmRankingBoard GetRealmRanking(RealmRankingCategory category, int top = 25)
    {
        // Phase 1 (DOD Bulk Ingest): Single pass over batch-loaded maps (Zero N+1 SQL queries)
        var activities = AllActivities().ToDictionary(a => a.NpcId);
        var deltas = LoadAllStatDeltasMap();
        var rivals = GetAllRivals().ToDictionary(r => r.NpcId);
        var citadels = _map.Citadels.ToDictionary(c => c.Id, c => c.NameVi);

        IWallet? wallet = null;
        IBag? bag = null;
        IMarketWarehouse? wh = null;
        ICultivationEstate? estate = null;
        IItemCatalog? itemCatalog = null;
        IPlayerAttributes? playerAttrs = null;
        IPlayerReputation? playerRep = null;
        IPoliticalCourt? court = null;
        ISocialNetwork? social = null;
        ICaravanGuild? caravan = null;

        if (_broker is not null)
        {
            _broker.TryGet(CapabilityIds.PlayerWallet, out wallet);
            _broker.TryGet(CapabilityIds.PlayerBag, out bag);
            _broker.TryGet(CapabilityIds.MarketWarehouse, out wh);
            _broker.TryGet(CapabilityIds.CultivationEstate, out estate);
            _broker.TryGet(CapabilityIds.ItemCatalog, out itemCatalog);
            _broker.TryGet(CapabilityIds.PlayerAttributes, out playerAttrs);
            _broker.TryGet(CapabilityIds.PlayerReputation, out playerRep);
            _broker.TryGet(CapabilityIds.CourtOffice, out court);
            _broker.TryGet(CapabilityIds.SocialNetwork, out social);
            _broker.TryGet(CapabilityIds.CaravanGuild, out caravan);
        }

        var factionCitadelCounts = new Dictionary<long, int>();
        if (court is not null)
        {
            foreach (var g in court.Governances)
            {
                if (g.FactionId > 0)
                {
                    factionCitadelCounts.TryGetValue(g.FactionId, out var cnt);
                    factionCitadelCounts[g.FactionId] = cnt + 1;
                }
            }
        }

        var (boardTitle, scoreLabel, scoreUnit, advice) = GetCategoryMetadata(category);
        var rawEntries = new List<RealmRankingEntry>(_registry.All.Count + 1);

        // 1. Score Player
        var playerCityName = citadels.TryGetValue(_player.CurrentCityId, out var pCit) ? pCit : "Trung Nguyên";
        var pAttr = playerAttrs?.Attributes ?? new CharacterAttributes(50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50);
        var pStanding = playerRep?.Standing;
        var pFame = pStanding?.Fame ?? (playerAttrs?.Social.Fame ?? 100);
        var pTitle = pStanding?.TitleVi ?? "Lữ Khách Khởi Nghiệp";

        var (playerScore, playerTier, playerBreakdown) = ComputePlayerPillarScore(
            category, pAttr, playerAttrs, pFame, wallet, bag, wh, estate, itemCatalog, court, social, caravan);

        rawEntries.Add(new RealmRankingEntry(
            Rank: 0,
            EntityId: _player.PlayerId,
            NameVi: _player.Name,
            IsPlayer: true,
            RoleOrTitleVi: pTitle,
            CitadelNameVi: playerCityName,
            Score: playerScore,
            ScoreUnitVi: scoreUnit,
            TierTitleVi: playerTier,
            SpecialtyOrBreakdownVi: playerBreakdown));

        // 2. Score all living NPCs in memory
        foreach (var npc in _registry.All)
        {
            activities.TryGetValue(npc.Id, out var act);
            if (npc.IsDeceasedFlag || act?.IsDeceased == true) continue;

            deltas.TryGetValue(npc.Id, out var delta);
            rivals.TryGetValue(npc.Id, out var rival);

            var citId = act?.CurrentCitadelId ?? npc.CitadelId;
            var citName = citadels.TryGetValue(citId, out var cName) ? cName : "Châu quận";

            var (score, tierTitle, breakdown) = ComputeNpcPillarScore(
                category, npc, act, delta, rival, factionCitadelCounts);

            rawEntries.Add(new RealmRankingEntry(
                Rank: 0,
                EntityId: npc.Id,
                NameVi: npc.NameVi,
                IsPlayer: false,
                RoleOrTitleVi: npc.StyleNameVi is { Length: > 0 } style ? $"{npc.Role} ({style})" : npc.Role,
                CitadelNameVi: citName,
                Score: score,
                ScoreUnitVi: scoreUnit,
                TierTitleVi: tierTitle,
                SpecialtyOrBreakdownVi: breakdown));
        }

        // 3. Deterministic Sort (ties favor established NPCs over player so player must earn overtakes)
        rawEntries.Sort((a, b) =>
        {
            var cmp = b.Score.CompareTo(a.Score);
            if (cmp != 0) return cmp;
            if (a.IsPlayer != b.IsPlayer) return a.IsPlayer ? 1 : -1;
            return a.EntityId.CompareTo(b.EntityId);
        });

        var ranked = new List<RealmRankingEntry>(rawEntries.Count);
        RealmRankingEntry? playerEntry = null;
        for (var i = 0; i < rawEntries.Count; i++)
        {
            var entry = rawEntries[i] with { Rank = i + 1 };
            ranked.Add(entry);
            if (entry.IsPlayer) playerEntry = entry;
        }

        playerEntry ??= ranked[0];

        // 4. Compute Next Rival & Milestone Benchmark ("Mục Tiêu Phấn Đấu")
        RealmRankingEntry? nextRival = playerEntry.Rank > 1 ? ranked[playerEntry.Rank - 2] : null;
        var gapToNext = nextRival is not null ? Math.Max(1L, (nextRival.Score - playerEntry.Score) + 1L) : 0L;

        RealmRankingEntry? milestoneTarget = null;
        if (playerEntry.Rank > 1)
        {
            int[] milestones = [100, 50, 25, 10, 3, 1];
            var targetRank = milestones.FirstOrDefault(m => m < playerEntry.Rank && m <= ranked.Count);
            if (targetRank <= 0) targetRank = 1;
            milestoneTarget = ranked[targetRank - 1];
        }

        // 5. Build Top Display Window (anchoring player at bottom if outside top N)
        var safeTop = Math.Max(3, top);
        List<RealmRankingEntry> topSlice;
        if (ranked.Count <= safeTop || playerEntry.Rank <= safeTop)
        {
            topSlice = ranked.Take(safeTop).ToList();
        }
        else
        {
            topSlice = ranked.Take(safeTop - 1).ToList();
            topSlice.Add(playerEntry);
        }

        return new RealmRankingBoard(
            Category: category,
            TitleVi: boardTitle,
            ScoreLabelVi: scoreLabel,
            TopEntries: topSlice,
            PlayerEntry: playerEntry,
            NextRivalToOvertake: nextRival,
            GapToNextRival: gapToNext,
            MilestoneTarget: milestoneTarget,
            ProgressionAdviceVi: advice);
    }

    public StrategicRivalryActionResult HostGrandSymposium()
    {
        const long cost = 500;
        if (_broker is null || !_broker.TryGet<IWallet>(CapabilityIds.PlayerWallet, out var wallet) || wallet is null)
        {
            return new StrategicRivalryActionResult(false, "Thiếu ngân khố", "Không thể truy cập ngân khố người chơi.", 0, 0, 0);
        }

        if (wallet.Balance < cost)
        {
            return new StrategicRivalryActionResult(
                false,
                "Ngân lượng chưa đủ",
                $"Cần {cost:N0} Quan để mở Đại Hội Luận Đạo Cửu Châu (Hiện có: {wallet.Balance:N0} Quan).",
                0, 0, 0);
        }

        var cityId = _player.CurrentCityId;
        var cityName = _map.Find(cityId)?.NameVi ?? "Bản thành";
        wallet.Charge(cost, JournalCodes.SocietySymposium, $"Mở Đại Hội Luận Đạo & Chiêu Hiền tại {cityName}");

        const int fameGain = 80;
        const int virtueGain = 8;

        if (_broker.TryGet<IPlayerReputation>(CapabilityIds.PlayerReputation, out var rep) && rep is not null)
        {
            var regionCode = _map.Find(cityId)?.Region;
            rep.GainFame(
                fameGain,
                "social",
                "Đại Hội Luận Đạo Cửu Châu",
                $"Xuất {cost:N0} Quan mở tiệc trà luận bàn thiên hạ đại thế tại {cityName}, sĩ phu bốn phương nức lòng ca ngợi.",
                regionCode,
                virtueDelta: virtueGain,
                credDelta: 5);
        }

        if (_broker.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var attrs) && attrs is not null)
        {
            attrs.AddExp("INT", 25);
            attrs.AddExp("DIP", 25);
        }

        int scholarsImpressed = 0;
        if (_broker.TryGet<ISocialNetwork>(CapabilityIds.SocialNetwork, out var social) && social is not null)
        {
            var localScholars = _registry.FindByCitadel(cityId)
                .Where(n => n.Attributes.Int >= 72 || n.Attributes.Pol >= 72)
                .Take(4)
                .ToList();

            foreach (var scholar in localScholars)
            {
                social.RecordMetNpc(scholar.Id, _clock.Today, cityId);
                social.RecordCooperation(scholar.Id, 6, 4, $"Cảm phục phong thái chiêu hiền đãi sĩ của bạn tại Đại Hội Luận Đạo {cityName}.");
                scholarsImpressed++;
            }
        }

        var formattedDate = GameDateCodec.Format(_clock.Today);
        var headline = $"{_player.Name} mở Đại Hội Luận Đạo tại {cityName}, danh chấn sĩ lâm!";
        var detail = $"Bỏ ra {cost:N0} Quan chiêu đãi hiền tài tại {cityName}: +{fameGain} Uy Danh, +{virtueGain} Nhân Đức, kết giao {scholarsImpressed} bậc danh sĩ.";
        LogEvent(formattedDate, 0, _player.Name, cityId, cityName, "scholarly", headline, detail, $"+{fameGain} Uy Danh, +{virtueGain} Đức", _clock.Today.DayIndex);

        return new StrategicRivalryActionResult(true, headline, detail, cost, fameGain, virtueGain);
    }

    public StrategicRivalryActionResult LaunchCommercialSuppression(long? targetRivalNpcId = null)
    {
        const long cost = 600;
        if (_broker is null || !_broker.TryGet<IWallet>(CapabilityIds.PlayerWallet, out var wallet) || wallet is null)
        {
            return new StrategicRivalryActionResult(false, "Thiếu ngân khố", "Không thể truy cập ngân khố người chơi.", 0, 0, 0);
        }

        if (wallet.Balance < cost)
        {
            return new StrategicRivalryActionResult(
                false,
                "Ngân lượng chưa đủ",
                $"Cần {cost:N0} Quan vốn dự trữ để phát động chiến dịch bán phá giá đè bẹp đối thủ (Hiện có: {wallet.Balance:N0} Quan).",
                0, 0, 0);
        }

        var allRivals = GetAllRivals();
        if (allRivals.Count == 0)
        {
            return new StrategicRivalryActionResult(false, "Không có đối thủ", "Chưa có thương hội đối thủ nào để phát động thương chiến.", 0, 0, 0);
        }

        var target = targetRivalNpcId.HasValue
            ? allRivals.FirstOrDefault(r => r.NpcId == targetRivalNpcId.Value) ?? allRivals[0]
            : allRivals.FirstOrDefault(r => r.CurrentCitadelId == _player.CurrentCityId) ?? allRivals[0];

        var cityId = _player.CurrentCityId;
        var cityName = _map.Find(cityId)?.NameVi ?? "Bản thành";

        wallet.Charge(cost, JournalCodes.SocietyTradeWar, $"Phát động thương chiến đè giá nhắm vào {target.EnterpriseName}");

        var capitalDrain = Math.Clamp(target.CapitalQuan / 4, 250L, 650L);
        var newRivalCapital = Math.Max(200L, target.CapitalQuan - capitalDrain);
        var newRivalInf = Math.Max(50, target.MarketInfluencePermille - 45);
        var formattedDate = GameDateCodec.Format(_clock.Today);

        _db.Transaction(() =>
        {
            _db.Execute(
                """
                UPDATE so_merchant_rival
                SET capital_quan = @cap,
                    market_influence_permille = @inf,
                    last_action_desc = @desc,
                    updated_date = @date
                WHERE npc_id = @id
                """,
                new DbArgs()
                    .Add("@cap", newRivalCapital)
                    .Add("@inf", newRivalInf)
                    .Add("@desc", $"Thiệt hại {capitalDrain:N0} Quan do đòn bán phá giá của {_player.Name}")
                    .Add("@date", formattedDate)
                    .Add("@id", target.NpcId));

            _db.Execute(
                "UPDATE so_npc_activity SET current_wealth = @cap, updated_date = @date WHERE npc_id = @id",
                new DbArgs().Add("@cap", newRivalCapital).Add("@date", formattedDate).Add("@id", target.NpcId));
        });

        const int fameGain = 55;
        const int virtueLoss = -8; // Orthogonal Trade-off: Hegemon dominance costs moral Virtue!

        if (_broker.TryGet<IPlayerReputation>(CapabilityIds.PlayerReputation, out var rep) && rep is not null)
        {
            var regionCode = _map.Find(cityId)?.Region;
            rep.GainFame(
                fameGain,
                "trade",
                "Thương Chiến Đoạt Thị Phần",
                $"Tung {cost:N0} Quan ép giá đè bẹp nguồn hàng của {target.EnterpriseName}, buộc {target.NpcNameVi} tổn thất {capitalDrain:N0} Quan tư bản.",
                regionCode,
                virtueDelta: virtueLoss,
                credDelta: -2);
        }

        if (_broker.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var attrs) && attrs is not null)
        {
            attrs.AddExp("COM", 35);
        }

        if (_broker.TryGet<ISocialNetwork>(CapabilityIds.SocialNetwork, out var social) && social is not null)
        {
            social.RecordMetNpc(target.NpcId, _clock.Today, cityId);
            social.RecordCooperation(
                target.NpcId,
                affinityDelta: -22,
                trustDelta: -15,
                summaryVi: $"Thương trường kết oán: Bạn tung {cost:N0} Quan phá giá khiến {target.EnterpriseName} tổn thất {capitalDrain:N0} Quan!");
        }

        var headline = $"[Thương Chiến] {_player.Name} tung {cost:N0} Quan đánh sập giá {target.PrimaryGoodNameVi}, chèn ép {target.EnterpriseName}!";
        var detail = $"{target.NpcNameVi} tổn thất {capitalDrain:N0} Quan vốn liếng & sụt giảm thị phần, nhưng thủ đoạn bá đạo khiến bạn giảm 8 Nhân Đức và kết mối thâm thù thương giới.";
        LogEvent(formattedDate, target.NpcId, target.NpcNameVi, cityId, cityName, "commerce", headline, detail, $"-{capitalDrain:N0}Q đối thủ, +{fameGain} Uy Danh, {virtueLoss} Đức", _clock.Today.DayIndex);

        return new StrategicRivalryActionResult(true, headline, detail, cost, fameGain, virtueLoss);
    }

    private static (string TitleVi, string ScoreLabelVi, string UnitVi, string AdviceVi) GetCategoryMetadata(RealmRankingCategory category) =>
        category switch
        {
            RealmRankingCategory.Wealth => (
                "💰 PHÚ GIẢ BẢNG · ĐỆ NHẤT TÀI PHÚ THIÊN HẠ",
                "Tổng Tài Sản",
                "Quan",
                "Buôn bán đặc sản chênh lệch giá, mở rộng Thương Đoàn, đầu tư Điền Trang hoặc phát động Thương Chiến để soán ngôi đại phú hào."),
            RealmRankingCategory.Fame => (
                "🌟 DANH VỌNG BẢNG · BẢNG PHONG THẦN UY DANH",
                "Uy Danh",
                "Điểm",
                "Phát chẩn cứu dân, mở Đại Hội Luận Đạo, hoàn thành tâm nguyện danh sĩ và lập chiến công tiễu phỉ để lưu danh sử sách."),
            RealmRankingCategory.Martial => (
                "⚔️ VÕ THẦN BẢNG · ĐỆ NHẤT DŨNG TƯỚNG TAM QUỐC",
                "Chiến Lực Võ Đạo",
                "Điểm",
                "Tỷ thí Lôi Đài, tiễu trừ sơn trại thảo khấu và bái sư tu luyện tuyệt kỹ hệ Võ Nghiệp để vượt qua hàng ngũ Ngũ Hổ Tướng."),
            RealmRankingCategory.Wisdom => (
                "📜 KỲ TÀI BẢNG · KINH BANG TẾ THẾ MƯU THẦN",
                "Chỉ Số Kỳ Tài",
                "Điểm",
                "Đàm đạo sách lược cùng bậc đại nho, tầm sư tìm Kỳ Thư ẩn sĩ, mở Đại Hội Luận Đạo và tham chính nha môn."),
            _ => (
                "👑 QUYỀN THẾ BẢNG · THỰC QUYỀN & THẾ LỰC CỬU CHÂU",
                "Điểm Quyền Thế",
                "Điểm",
                "Chiêu mộ gia thần Phẩm S/A, kết nghĩa Kim Lan, thăng tiến quan phẩm triều đình và mở rộng đoàn xe tiêu cục.")
        };

    private (long Score, string TierTitle, string Breakdown) ComputePlayerPillarScore(
        RealmRankingCategory category,
        CharacterAttributes pAttr,
        IPlayerAttributes? playerAttrs,
        int playerFame,
        IWallet? wallet,
        IBag? bag,
        IMarketWarehouse? wh,
        ICultivationEstate? estate,
        IItemCatalog? itemCatalog,
        IPoliticalCourt? court,
        ISocialNetwork? social,
        ICaravanGuild? caravan)
    {
        switch (category)
        {
            case RealmRankingCategory.Wealth:
            {
                long cash = wallet?.Balance ?? 0;
                long goodsVal = 0;
                if (bag is not null)
                {
                    foreach (var c in _map.Citadels)
                    {
                        var rows = bag.Rows(c.Id);
                        for (var i = 0; i < rows.Count; i++)
                            goodsVal += rows[i].Quantity * (itemCatalog?.Find(rows[i].ItemId)?.BasePrice ?? 20);
                    }
                }
                if (wh is not null)
                {
                    foreach (var c in _map.Citadels)
                    {
                        var stock = wh.Stock(c.Id);
                        for (var i = 0; i < stock.Count; i++)
                            goodsVal += stock[i].Quantity * (itemCatalog?.Find(stock[i].ItemId)?.BasePrice ?? 20);
                    }
                }
                long estateVal = estate is not null ? estate.Plots.Count * 500L + estate.Facilities.Sum(f => f.Tier * 800L) : 0L;
                var total = cash + goodsVal + estateVal;
                var tier = MerchantRivalDef.TierToTitle(total >= 30000 ? 5 : total >= 10000 ? 4 : total >= 3500 ? 3 : total >= 1000 ? 2 : 1);
                return (total, tier, $"Ngân quỹ {cash:N0} · Hàng hóa {goodsVal:N0} · Điền sản {estateVal:N0}");
            }

            case RealmRankingCategory.Fame:
            {
                var tier = FameTierTitle(playerFame);
                var virtue = playerAttrs?.Social.Virtue ?? 50;
                var cred = playerAttrs?.Social.Credibility ?? 50;
                return (playerFame, tier, $"Uy danh {playerFame:N0} · Nhân đức {virtue} · Tín nghĩa {cred}");
            }

            case RealmRankingCategory.Martial:
            {
                var skillBonus = (playerAttrs?.SkillProgresses.Count ?? 0) * 8;
                var score = pAttr.War * 2L + pAttr.Ldr + (pAttr.Con / 2) + skillBonus;
                return (score, MartialTierTitle(score), $"Võ {pAttr.War} · Thống {pAttr.Ldr} · Thể {pAttr.Con} · Võ kỹ +{skillBonus}");
            }

            case RealmRankingCategory.Wisdom:
            {
                var skillBonus = (playerAttrs?.SkillProgresses.Count ?? 0) * 8;
                var score = pAttr.Int * 2L + pAttr.Pol + (pAttr.Dip / 2) + skillBonus;
                return (score, WisdomTierTitle(score), $"Trí {pAttr.Int} · Chính {pAttr.Pol} · Giao {pAttr.Dip} · Tuyệt học +{skillBonus}");
            }

            default:
            {
                var officeRank = court is not null && court.OfficeId > 0
                    ? (court.Offices.FirstOrDefault(o => o.Id == court.OfficeId)?.Rank ?? 1)
                    : 0;
                var retainerCount = social?.GetRetainers().Count ?? 0;
                var swornCount = social?.GetSwornBrothers().Count ?? 0;
                var fleetBonus = (caravan?.Fleet.Tier ?? 0) * 35;
                var score = officeRank * 90L + retainerCount * 45L + swornCount * 60L + fleetBonus + (playerFame / 2) + ((pAttr.Ldr + pAttr.Pol + pAttr.Dip) / 2);
                return (score, InfluenceTierTitle(score), $"Quan phẩm {officeRank} · Gia thần {retainerCount} · Nghĩa đệ {swornCount} · Uy danh +{playerFame / 2}");
            }
        }
    }

    private static (long Score, string TierTitle, string Breakdown) ComputeNpcPillarScore(
        RealmRankingCategory category,
        NpcDef npc,
        NpcActivityDef? act,
        NpcStatDelta? delta,
        MerchantRivalDef? rival,
        Dictionary<long, int> factionCitadelCounts)
    {
        var a = npc.Attributes;
        var war = a.War + (delta?.WarDelta ?? 0);
        var ldr = a.Ldr + (delta?.LdrDelta ?? 0);
        var @int = a.Int + (delta?.IntDelta ?? 0);
        var pol = a.Pol + (delta?.PolDelta ?? 0);
        var dip = a.Dip + (delta?.DipDelta ?? 0);
        var rep = Math.Max(act?.Reputation ?? 50, ComputeInitialReputation(npc));

        switch (category)
        {
            case RealmRankingCategory.Wealth:
            {
                if (rival is not null)
                {
                    var netWorth = rival.CapitalQuan + (rival.FleetCarts * 500L) + (rival.Reputation * 20L);
                    return (netWorth, rival.TierTitleVi, $"{rival.EnterpriseName} · {rival.FleetCarts} Xe · Sở trường {rival.PrimaryGoodNameVi}");
                }

                var wealth = act?.CurrentWealth ?? ComputeInitialWealth(npc, DetermineFocusPath(a));
                var tier = MerchantRivalDef.TierToTitle(wealth >= 30000 ? 5 : wealth >= 10000 ? 4 : wealth >= 3500 ? 3 : wealth >= 1000 ? 2 : 1);
                return (wealth, tier, $"{act?.CurrentAction ?? npc.Role}");
            }

            case RealmRankingCategory.Fame:
            {
                return (rep, FameTierTitle(rep), act?.CurrentAction ?? npc.Role);
            }

            case RealmRankingCategory.Martial:
            {
                var martialSkills = npc.Skills.Where(s => s.Category == "martial").Sum(s => s.Tier * 6 + s.Level * 3);
                var score = war * 2L + ldr + (a.Con / 2) + martialSkills;
                var topSkill = npc.Skills.FirstOrDefault(s => s.Category == "martial")?.NameVi ?? "Binh pháp căn bản";
                return (score, MartialTierTitle(score), $"Võ {war} · Thống {ldr} · Tuyệt kỹ [{topSkill}]");
            }

            case RealmRankingCategory.Wisdom:
            {
                var sageSkills = npc.Skills.Where(s => s.Category is "strategist" or "governance").Sum(s => s.Tier * 6 + s.Level * 3);
                var score = @int * 2L + pol + (dip / 2) + sageSkills;
                var topSkill = npc.Skills.FirstOrDefault(s => s.Category is "strategist" or "governance")?.NameVi ?? "Kinh sử bách gia";
                return (score, WisdomTierTitle(score), $"Trí {@int} · Chính {pol} · Giao {dip} · [{topSkill}]");
            }

            default:
            {
                var role = npc.Role ?? string.Empty;
                long stature = 40;
                if (role.Contains("Hoàng Đế") || role.Contains("Thái Sư") || role.Contains("Tướng Quốc")) stature = 650;
                else if (role.Contains("Quân Chủ") || role.Contains("Minh Chủ") || role.Contains("Châu Mục")) stature = 480;
                else if (role.Contains("Thái Thú") || role.Contains("Thứ Sử") || role.Contains("Đại Tướng") || role.Contains("Giáo Tổ")) stature = 310;
                else if (rival is not null) stature = rival.MerchantTier * 75L + rival.FleetCarts * 25L;

                var factionBonus = act is not null && act.AllegianceFactionId > 0 && factionCitadelCounts.TryGetValue(act.AllegianceFactionId, out var cCount)
                    ? cCount * 25L
                    : 0L;

                var score = stature + factionBonus + (rep / 2) + ((ldr + pol + dip) / 2);
                return (score, InfluenceTierTitle(score), $"{npc.Role} · Uy danh {rep:N0}");
            }
        }
    }

    private static string FameTierTitle(long fame) => fame switch
    {
        >= 800 => "Thiên Hạ Chí Tôn",
        >= 550 => "Danh Chấn Hoa Hạ",
        >= 350 => "Châu Mục Đại Danh",
        >= 180 => "Quận Huyện Danh Sĩ",
        >= 100 => "Hào Kiệt Mới Nổi",
        _ => "Vô Danh Tiểu Tốt"
    };

    private static string MartialTierTitle(long score) => score switch
    {
        >= 330 => "Võ Thánh Vô Song",
        >= 295 => "Vạn Phu Mạc Địch",
        >= 255 => "Đương Thời Dũng Tướng",
        >= 210 => "Chiến Trường Kiện Tướng",
        _ => "Võ Môn Tân Tú"
    };

    private static string WisdomTierTitle(long score) => score switch
    {
        >= 350 => "Kinh Thiên Vĩ Địa",
        >= 310 => "Vương Tá Chi Tài",
        >= 265 => "Mưu Thần Sách Sĩ",
        >= 220 => "Châu Quận Hiền Tài",
        _ => "Thư Sinh Luận Đạo"
    };

    private static string InfluenceTierTitle(long score) => score switch
    {
        >= 950 => "Quyền Khuynh Thiên Hạ",
        >= 700 => "Bá Chủ Một Phương",
        >= 480 => "Trọng Thần Cương Khổn",
        >= 280 => "Thế Gia Hào Tộc",
        _ => "Giang Hồ Tự Lập"
    };
}
