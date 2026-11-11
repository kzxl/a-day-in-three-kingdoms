using SimT.Contracts.Data;
using SimT.Contracts.Identity;
using SimT.Contracts.Logging;
using SimT.Contracts.Modules;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Society;

internal sealed partial class SocietyService
{
    // ------------------------------------------------------------- AI Merchant Rivalry

    private bool? _hasRivalTable;

    private bool HasRivalTable() =>
        _hasRivalTable ??= _db.ScalarLong("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='so_merchant_rival'") > 0;

    private void EnsureMerchantRivalsInitialized()
    {
        if (!HasRivalTable()) return;

        var count = _db.ScalarLong("SELECT COUNT(*) FROM so_merchant_rival");
        if (count > 0) return;

        var formattedDate = GameDateCodec.Format(_clock.Today);

        var presets = new (long NpcId, string Enterprise, int Tier, long Capital, int Carts, long GoodId, long Target)[]
        {
            (1, "Lạc Dương Tế Thế Dược Phòng", 2, 2400, 2, 7, 2),
            (2, "Tây Vực Khoáng Thương Hội", 2, 2100, 2, 5, 1),
            (7, "Trung Sơn Trương Thị Mã Phô", 3, 4800, 3, 5, 3),
            (8, "U Châu Mộc Nghệ Thương Bang", 2, 2600, 2, 6, 3),
            (10, "Từ Châu Mi Thị Cẩm Phô", 4, 12500, 5, 4, 1),
            (115, "Ký Châu Chân Thị Lương Thương", 4, 14000, 5, 1, 1),
            (121, "Tây Lương Tuấn Mã Thương Hội", 2, 3100, 2, 5, 14),
            (127, "Nam Hải Hương Dược Hành", 3, 5200, 3, 14, 13)
        };

        var presetMap = presets.ToDictionary(p => p.NpcId);
        var allActivities = AllActivities();
        var npcsById = _registry.All.ToDictionary(n => n.Id);

        _db.Transaction(() =>
        {
            foreach (var (npcId, enterprise, tier, capital, carts, goodId, target) in presets)
            {
                if (!npcsById.TryGetValue(npcId, out var npc)) continue;

                _db.Execute(
                    """
                    INSERT OR IGNORE INTO so_merchant_rival (
                        npc_id, enterprise_name, merchant_tier, capital_quan, fleet_carts, primary_good_id,
                        headquarters_citadel_id, target_citadel_id, reputation, market_influence_permille,
                        monthly_profit, total_trade_volume, last_action_desc, updated_date)
                    VALUES (@npcId, @enterprise, @tier, @capital, @carts, @goodId, @hq, @target, @rep, @inf, 0, 0, @desc, @date)
                    """,
                    new DbArgs()
                        .Add("@npcId", npcId)
                        .Add("@enterprise", enterprise)
                        .Add("@tier", tier)
                        .Add("@capital", capital)
                        .Add("@carts", carts)
                        .Add("@goodId", goodId)
                        .Add("@hq", npc.CitadelId)
                        .Add("@target", target)
                        .Add("@rep", tier * 25)
                        .Add("@inf", tier * 180)
                        .Add("@desc", $"Thiết lập đại bản doanh tại {_map.Find(npc.CitadelId)?.NameVi ?? "Bản địa"}")
                        .Add("@date", formattedDate));
            }

            foreach (var act in allActivities)
            {
                if (act.FocusPath != "merchant" || presetMap.ContainsKey(act.NpcId)) continue;
                if (!npcsById.TryGetValue(act.NpcId, out var npc)) continue;

                var starterGood = (npc.Id % 8) + 1;
                var starterTier = act.CurrentWealth >= 3500 ? 3 : (act.CurrentWealth >= 1000 ? 2 : 1);
                var carts = starterTier;
                var enterpriseName = $"{npc.NameVi.Split(' ')[0]} Ký Thương Hành";
                var neighbours = _map.RouteNeighbours(act.CurrentCitadelId);
                var targetCit = neighbours.Count > 0 ? neighbours[0] : act.CurrentCitadelId;

                _db.Execute(
                    """
                    INSERT OR IGNORE INTO so_merchant_rival (
                        npc_id, enterprise_name, merchant_tier, capital_quan, fleet_carts, primary_good_id,
                        headquarters_citadel_id, target_citadel_id, reputation, market_influence_permille,
                        monthly_profit, total_trade_volume, last_action_desc, updated_date)
                    VALUES (@npcId, @enterprise, @tier, @capital, @carts, @goodId, @hq, @target, @rep, @inf, 0, 0, @desc, @date)
                    """,
                    new DbArgs()
                        .Add("@npcId", act.NpcId)
                        .Add("@enterprise", enterpriseName)
                        .Add("@tier", starterTier)
                        .Add("@capital", Math.Max(800, act.CurrentWealth))
                        .Add("@carts", carts)
                        .Add("@goodId", starterGood)
                        .Add("@hq", act.CurrentCitadelId)
                        .Add("@target", targetCit)
                        .Add("@rep", starterTier * 20)
                        .Add("@inf", starterTier * 120)
                        .Add("@desc", "Khai thương lập nghiệp")
                        .Add("@date", formattedDate));
            }
        });
    }

    private void ExecuteRivalMerchantAction(NpcDef npc, NpcActivityDef act, MerchantRivalDef rival, string citName, string date, long tick, IRng rng)
    {
        var roll = rng.Next(4);
        string headline, detail, impact, actionText;
        int deltaCom = 1, deltaLog = 0, repGain = 0;
        long goldDelta = 0;

        switch (roll)
        {
            case 0:
                headline = $"{rival.EnterpriseName} ({npc.NameVi}) điều phối {rival.FleetCarts} cỗ xe thâu tóm {rival.PrimaryGoodNameVi} tại {citName}";
                detail = $"{rival.EnterpriseName} xuất ngân lượng lớn thâu gom toàn bộ nguồn hàng {rival.PrimaryGoodNameVi} chất lượng cao, chuẩn bị xuất khẩu liên châu.";
                goldDelta = rival.MerchantTier * 280;
                deltaLog = 1;
                impact = $"+1 Thương Mại, +1 Hậu Cần, +{goldDelta} Quan";
                actionText = $"Áp tải đoàn xe {rival.PrimaryGoodNameVi} xuất kho";
                break;
            case 1:
                headline = $"{rival.EnterpriseName} mở rộng mạng lưới phân phối tại {citName}";
                detail = $"{npc.NameVi} thiết lập thêm chi điếm mới, tuyển mộ chưởng quỹ và tăng cường thế lực cạnh tranh tại khu thương thị sầm uất.";
                repGain = rival.MerchantTier * 5;
                goldDelta = -180;
                impact = $"+1 Thương Mại, +{repGain} Uy danh";
                actionText = "Khai trương chi điếm mới";
                break;
            case 2:
                headline = $"{rival.EnterpriseName} quyên góp quân lương trợ cấp thành phòng {citName}";
                detail = $"{npc.NameVi} xuất ngân khoản tài trợ quan quân và tuần cảnh nhằm củng cố quan hệ ngoại giao cùng nha môn bản hạt.";
                repGain = 12;
                goldDelta = -200;
                impact = "+12 Uy danh thương trường";
                actionText = "Giao thiệp cùng nha môn châu quận";
                break;
            default:
                headline = $"{rival.EnterpriseName} thắng lớn trong đợt đấu giá kỳ trân dị bảo tại {citName}";
                detail = $"{npc.NameVi} khéo léo chớp lấy thời cơ ép giá đối thủ cạnh tranh, thâu tóm lô hàng đặc sản với giá hời.";
                goldDelta = rival.MerchantTier * 320;
                repGain = 6;
                impact = $"+1 Thương Mại, +{goldDelta} Quan";
                actionText = "Kiểm đếm lợi nhuận phiên chợ";
                break;
        }

        ApplyNpcGrowth(npc.Id, date, 0, 0, 0, 0, 0, deltaCom, deltaLog, 0);
        UpdateActivity(npc.Id, date, actionText, (int)Math.Clamp(goldDelta, -10000, 10000), repGain, act.CurrentWealth);
        LogEvent(date, npc.Id, npc.NameVi, act.CurrentCitadelId, citName, "commerce", headline, detail, impact, tick);
    }

    public void SimulateMerchantTurn(GameDate date)
    {
        var formattedDate = GameDateCodec.Format(date);
        SimulateMerchantTurnCore(date, formattedDate, date.DayIndex);
    }

    private void SimulateMerchantTurnCore(GameDate date, string formattedDate, long tick)
    {
        if (!HasRivalTable()) return;

        var rivals = GetAllRivals();
        if (rivals.Count == 0) return;

        IMarketTrade? marketTrade = null;
        IWallet? playerWallet = null;
        ISocialNetwork? socialNet = null;
        if (_broker != null)
        {
            _broker.TryGet(CapabilityIds.MarketTrade, out marketTrade);
            _broker.TryGet(CapabilityIds.PlayerWallet, out playerWallet);
            _broker.TryGet(CapabilityIds.SocialNetwork, out socialNet);
        }

        var playerRich = (playerWallet?.Balance ?? 0) >= 10000;
        var topRivalNpcId = rivals[0].NpcId;

        _db.Transaction(() =>
        {
            foreach (var rival in rivals)
            {
                var rng = CreateSubStreamRng(rival.NpcId, date.DayIndex, 0x50C1E71_003UL);

                var currentCitId = rival.CurrentCitadelId;
                var currentCit = _map.Find(currentCitId);
                var currentCitName = currentCit?.NameVi ?? "Bản xứ";

                // Scout target citadel along travel routes (Always consume RNG first to keep stream invariant!)
                var neighbours = _map.RouteNeighbours(currentCitId);
                var targetCitId = rival.TargetCitadelId;
                var rerollTarget = rng.Next(100) < 30;
                if (neighbours.Count > 0 && (!neighbours.Contains(targetCitId) || rerollTarget))
                {
                    targetCitId = neighbours.OrderByDescending(nid => _map.Find(nid)?.PriceFactorPermille ?? 1000).First();
                }

                // Anti-Snowballing Rivalry Pressure: When player amasses >= 10,000 Quan, the #1 Merchant Tycoon
                // actively targets the player's current market to contest commercial hegemony!
                var isCoalitionLeader = playerRich && rival.NpcId == topRivalNpcId;
                if (isCoalitionLeader)
                {
                    targetCitId = _player.CurrentCityId;
                }
                var targetCit = _map.Find(targetCitId);
                var targetCitName = targetCit?.NameVi ?? "Lân bang";

                var goodId = rival.PrimaryGoodId;
                var goodName = rival.PrimaryGoodNameVi;
                var basePrice = 30L;
                if (_broker != null && _broker.TryGet<IItemCatalog>(CapabilityIds.ItemCatalog, out var catalog) && catalog != null)
                {
                    var item = catalog.Find(goodId);
                    if (item != null) basePrice = item.BasePrice;
                }

                int buyQty = 0;
                int sellQty = 0;
                long grossProfit = 0;

                if (marketTrade != null)
                {
                    // 1. Sweep local stock via market capability
                    var stock = marketTrade.StallStock(currentCitId, goodId);
                    if (stock > 3)
                    {
                        var maxCapacity = rival.FleetCarts * 12;
                        var maxBudgetQty = (int)(rival.CapitalQuan * 35 / 100 / Math.Max(1, basePrice));
                        buyQty = Math.Clamp(stock / 2, 1, Math.Min(maxCapacity, Math.Max(1, maxBudgetQty)));

                        if (buyQty > 0)
                        {
                            marketTrade.AdjustStock(currentCitId, goodId, -buyQty);
                        }
                    }

                    // 2. Transport & sell to destination market
                    sellQty = Math.Max(buyQty, rival.FleetCarts * 8);
                    var markup = 1150 + rng.Next(250);
                    var grossRevenue = sellQty * basePrice * markup / 1000;
                    grossProfit = Math.Max(120, grossRevenue - (sellQty * basePrice));

                    var baseline = marketTrade.StallBaseline(targetCitId, goodId);
                    var destStock = marketTrade.StallStock(targetCitId, goodId);

                    if (destStock < baseline * 2)
                    {
                        var addQty = Math.Min(sellQty, (baseline * 2) - destStock);
                        if (addQty > 0)
                        {
                            marketTrade.AdjustStock(targetCitId, goodId, addQty);
                        }
                    }
                }
                else
                {
                    sellQty = rival.FleetCarts * 10;
                    grossProfit = rival.MerchantTier * 220 + rng.Next(150);
                }

                var newCapital = rival.CapitalQuan + grossProfit;
                var newTotalVolume = rival.TotalTradeVolume + sellQty;

                // Enterprise growth & Tier upgrades
                var newTier = rival.MerchantTier;
                var newCarts = rival.FleetCarts;
                var newReputation = rival.Reputation;
                var newInfluence = rival.MarketInfluencePermille;
                bool tierUpgraded = false;

                if (newCapital >= 30000 && newTier < 5)
                {
                    newTier = 5;
                    newCarts = Math.Max(newCarts + 2, 8);
                    newReputation += 25;
                    newInfluence = 1000;
                    tierUpgraded = true;
                }
                else if (newCapital >= 10000 && newTier < 4)
                {
                    newTier = 4;
                    newCarts = Math.Max(newCarts + 1, 5);
                    newReputation += 18;
                    newInfluence = 650;
                    tierUpgraded = true;
                }
                else if (newCapital >= 3500 && newTier < 3)
                {
                    newTier = 3;
                    newCarts = Math.Max(newCarts + 1, 3);
                    newReputation += 12;
                    newInfluence = 400;
                    tierUpgraded = true;
                }
                else if (newCapital >= 1000 && newTier < 2)
                {
                    newTier = 2;
                    newCarts = Math.Max(newCarts + 1, 2);
                    newReputation += 8;
                    newInfluence = 250;
                    tierUpgraded = true;
                }

                string actionDesc = buyQty > 0
                    ? $"Vét {buyQty} {goodName} tại {currentCitName}, áp tải sang {targetCitName} thu lời {grossProfit:N0} Quan."
                    : $"Vận chuyển {sellQty} {goodName} sang {targetCitName}, tích lũy thêm {grossProfit:N0} Quan vốn liếng.";

                _db.Execute(
                    """
                    UPDATE so_merchant_rival SET
                        merchant_tier = @tier,
                        capital_quan = @capital,
                        fleet_carts = @carts,
                        target_citadel_id = @targetCit,
                        reputation = @rep,
                        market_influence_permille = @inf,
                        monthly_profit = @profit,
                        total_trade_volume = @vol,
                        last_action_desc = @desc,
                        updated_date = @date
                    WHERE npc_id = @id
                    """,
                    new DbArgs()
                        .Add("@tier", newTier)
                        .Add("@capital", newCapital)
                        .Add("@carts", newCarts)
                        .Add("@targetCit", targetCitId)
                        .Add("@rep", newReputation)
                        .Add("@inf", newInfluence)
                        .Add("@profit", grossProfit)
                        .Add("@vol", newTotalVolume)
                        .Add("@desc", actionDesc)
                        .Add("@date", formattedDate)
                        .Add("@id", rival.NpcId));

                // Sync wealth into so_npc_activity
                _db.Execute(
                    "UPDATE so_npc_activity SET current_wealth = @w, current_action = @act, updated_date = @date WHERE npc_id = @id",
                    new DbArgs()
                        .Add("@w", newCapital)
                        .Add("@act", actionDesc)
                        .Add("@date", formattedDate)
                        .Add("@id", rival.NpcId));

                if (isCoalitionLeader)
                {
                    var playerCitId = _player.CurrentCityId;
                    var playerCitName = _map.Find(playerCitId)?.NameVi ?? currentCitName;
                    var headline = $"[Thương Chiến Chèn Ép] {rival.EnterpriseName} vây ép nguồn cung tại {playerCitName}!";
                    var detail = $"Thấy cơ nghiệp của bạn vượt mốc vạn kim, {rival.NpcNameVi} liên kết các đại thương hội dồn xe hàng về {playerCitName} nhằm kìm hãm thế độc tôn của bạn.";
                    LogEvent(formattedDate, rival.NpcId, rival.NpcNameVi, playerCitId, playerCitName, "commerce", headline, detail, "Áp lực liên minh phú thương", tick);
                    if (socialNet != null && socialNet.HasMet(rival.NpcId))
                    {
                        socialNet.RecordCooperation(rival.NpcId, -6, -4, $"Cạnh tranh khốc liệt: {rival.NpcNameVi} phát động thương chiến chèn ép thị phần của bạn tại {playerCitName}.");
                    }
                }
                else if (tierUpgraded)
                {
                    var title = MerchantRivalDef.TierToTitle(newTier);
                    var headline = $"{rival.EnterpriseName} thăng cấp lên [{title}]!";
                    var detail = $"Nhờ mạng lưới phân phối rộng khắp và tích lũy tư bản vượt trội ({newCapital:N0} Quan), {rival.NpcNameVi} đã nâng cấp quy mô thương hội, chiêu mộ thêm thương đoàn hộ tiêu tinh nhuệ.";
                    LogEvent(formattedDate, rival.NpcId, rival.NpcNameVi, currentCitId, currentCitName, "commerce", headline, detail, $"+{title}, +{newCarts} Xe Hàng", tick);
                }
                else if (buyQty >= 8 && currentCitId == _player.CurrentCityId)
                {
                    var headline = $"{rival.NpcNameVi} thâu tóm {buyQty} {goodName} tại {currentCitName}!";
                    var detail = $"Đoàn xe của {rival.EnterpriseName} vừa quét sạch nguồn cung {goodName} tại chợ {currentCitName}, khiến giá bán lẻ tại sạp tăng vọt.";
                    LogEvent(formattedDate, rival.NpcId, rival.NpcNameVi, currentCitId, currentCitName, "commerce", headline, detail, $"Thị trường {goodName} khan hiếm", tick);
                    if (socialNet != null && socialNet.HasMet(rival.NpcId))
                    {
                        socialNet.RecordCooperation(rival.NpcId, -3, -2, $"Tranh mua hàng hiếm: {rival.NpcNameVi} vét sạch {goodName} ngay trước mũi bạn tại {currentCitName}.");
                    }
                }
            }
        });
    }

    public IReadOnlyList<MerchantRivalDef> GetAllRivals()
    {
        if (!HasRivalTable()) return Array.Empty<MerchantRivalDef>();

        var sql = """
            SELECT r.npc_id, r.enterprise_name, r.merchant_tier,
                   r.capital_quan, r.fleet_carts, r.primary_good_id, r.headquarters_citadel_id,
                   r.target_citadel_id, r.reputation, r.market_influence_permille, r.monthly_profit,
                   r.total_trade_volume, r.last_action_desc, r.updated_date,
                   COALESCE(a.current_citadel_id, r.headquarters_citadel_id)
            FROM so_merchant_rival r
            LEFT JOIN so_npc_activity a ON a.npc_id = r.npc_id
            ORDER BY r.capital_quan DESC
            """;

        return _db.Query(sql, row =>
        {
            var npcId = row.GetInt64(0);
            var enterprise = row.GetString(1);
            var tier = row.GetInt32(2);
            var capital = row.GetInt64(3);
            var carts = row.GetInt32(4);
            var goodId = row.GetInt64(5);
            var hqId = row.GetInt64(6);
            var targetId = row.GetInt64(7);
            var rep = row.GetInt32(8);
            var inf = row.GetInt32(9);
            var profit = row.GetInt64(10);
            var vol = row.GetInt64(11);
            var desc = row.GetString(12);
            var updatedStr = row.GetString(13);
            var currentCitId = row.GetInt64(14);

            var npcName = _registry.Find(npcId)?.NameVi ?? "Thương Nhân";
            var goodName = ResolveGoodName(goodId);
            var hqName = _map.Find(hqId)?.NameVi ?? "Bản xứ";
            var currentCitName = _map.Find(currentCitId)?.NameVi ?? hqName;
            var targetName = _map.Find(targetId)?.NameVi ?? "Lân bang";

            return new MerchantRivalDef(
                npcId,
                npcName,
                enterprise,
                tier,
                MerchantRivalDef.TierToTitle(tier),
                capital,
                carts,
                goodId,
                goodName,
                hqId,
                hqName,
                currentCitId,
                currentCitName,
                targetId,
                targetName,
                rep,
                inf,
                profit,
                vol,
                desc,
                GameDateCodec.Parse(updatedStr));
        });
    }

    public MerchantRivalDef? GetRival(long npcId)
    {
        if (!HasRivalTable()) return null;

        var sql = """
            SELECT r.npc_id, r.enterprise_name, r.merchant_tier,
                   r.capital_quan, r.fleet_carts, r.primary_good_id, r.headquarters_citadel_id,
                   r.target_citadel_id, r.reputation, r.market_influence_permille, r.monthly_profit,
                   r.total_trade_volume, r.last_action_desc, r.updated_date,
                   COALESCE(a.current_citadel_id, r.headquarters_citadel_id)
            FROM so_merchant_rival r
            LEFT JOIN so_npc_activity a ON a.npc_id = r.npc_id
            WHERE r.npc_id = @id
            """;

        return _db.QueryOne(sql, row =>
        {
            var id = row.GetInt64(0);
            var enterprise = row.GetString(1);
            var tier = row.GetInt32(2);
            var capital = row.GetInt64(3);
            var carts = row.GetInt32(4);
            var goodId = row.GetInt64(5);
            var hqId = row.GetInt64(6);
            var targetId = row.GetInt64(7);
            var rep = row.GetInt32(8);
            var inf = row.GetInt32(9);
            var profit = row.GetInt64(10);
            var vol = row.GetInt64(11);
            var desc = row.GetString(12);
            var updatedStr = row.GetString(13);
            var currentCitId = row.GetInt64(14);

            var npcName = _registry.Find(id)?.NameVi ?? "Thương Nhân";
            var goodName = ResolveGoodName(goodId);
            var hqName = _map.Find(hqId)?.NameVi ?? "Bản xứ";
            var currentCitName = _map.Find(currentCitId)?.NameVi ?? hqName;
            var targetName = _map.Find(targetId)?.NameVi ?? "Lân bang";

            return new MerchantRivalDef(
                id,
                npcName,
                enterprise,
                tier,
                MerchantRivalDef.TierToTitle(tier),
                capital,
                carts,
                goodId,
                goodName,
                hqId,
                hqName,
                currentCitId,
                currentCitName,
                targetId,
                targetName,
                rep,
                inf,
                profit,
                vol,
                desc,
                GameDateCodec.Parse(updatedStr));
        }, DbArgs.One("@id", npcId));
    }

    public IReadOnlyList<MerchantLeaderboardEntry> GetLeaderboard()
    {
        var rivals = GetAllRivals();

        long playerCash = 0;
        if (_broker != null && _broker.TryGet<IWallet>(CapabilityIds.PlayerWallet, out var wallet) && wallet != null)
        {
            playerCash = wallet.Liquid;
        }

        long playerBagValue = 0;
        IItemCatalog? itemCatalog = null;
        if (_broker != null && _broker.TryGet<IItemCatalog>(CapabilityIds.ItemCatalog, out var cat))
        {
            itemCatalog = cat;
        }

        if (_broker != null && _broker.TryGet<IBag>(CapabilityIds.PlayerBag, out var bag) && bag != null)
        {
            foreach (var c in _map.Citadels)
            {
                var rows = bag.Rows(c.Id);
                for (int i = 0; i < rows.Count; i++)
                {
                    var price = itemCatalog?.Find(rows[i].ItemId)?.BasePrice ?? 20;
                    playerBagValue += rows[i].Quantity * price;
                }
            }
        }

        long playerWhValue = 0;
        if (_broker != null && _broker.TryGet<IMarketWarehouse>(CapabilityIds.MarketWarehouse, out var wh) && wh != null)
        {
            foreach (var c in _map.Citadels)
            {
                var stock = wh.Stock(c.Id);
                for (int i = 0; i < stock.Count; i++)
                {
                    var price = itemCatalog?.Find(stock[i].ItemId)?.BasePrice ?? 20;
                    playerWhValue += stock[i].Quantity * price;
                }
            }
        }

        long playerEstateValue = 0;
        if (_broker != null && _broker.TryGet<ICultivationEstate>(CapabilityIds.CultivationEstate, out var estate) && estate != null)
        {
            playerEstateValue = estate.Plots.Count * 500 + estate.Facilities.Sum(f => f.Tier * 800);
        }

        var totalPlayerNetWorth = playerCash + playerBagValue + playerWhValue + playerEstateValue;
        var playerTier = totalPlayerNetWorth >= 30000 ? 5
                       : totalPlayerNetWorth >= 10000 ? 4
                       : totalPlayerNetWorth >= 3500 ? 3
                       : totalPlayerNetWorth >= 1000 ? 2
                       : 1;

        var currentCit = _map.Find(_player.CurrentCityId);
        var playerBase = currentCit?.NameVi ?? "Kinh Kỳ";

        var entries = new List<(long EntityId, string Name, bool IsPlayer, string Enterprise, int Tier, string TierTitle, long NetWorth, string BaseCitadel, string Specialty, string Status)>();

        entries.Add((
            _player.PlayerId,
            _player.Name,
            true,
            $"Gia Tộc {_player.Name}",
            playerTier,
            MerchantRivalDef.TierToTitle(playerTier),
            totalPlayerNetWorth,
            playerBase,
            "Đa Ngành Toàn Diện",
            $"Tiền mặt {playerCash:N0} · Kho {playerBagValue + playerWhValue:N0} · Điền trang {playerEstateValue:N0}"));

        foreach (var r in rivals)
        {
            var rivalNetWorth = r.CapitalQuan + (r.FleetCarts * 500) + (r.Reputation * 20);
            entries.Add((
                r.NpcId,
                r.NpcNameVi,
                false,
                r.EnterpriseName,
                r.MerchantTier,
                r.TierTitleVi,
                rivalNetWorth,
                r.HeadquartersNameVi,
                r.PrimaryGoodNameVi,
                r.LastActionDesc));
        }

        var sorted = entries.OrderByDescending(e => e.NetWorth).ToList();
        var result = new List<MerchantLeaderboardEntry>(sorted.Count);
        for (int i = 0; i < sorted.Count; i++)
        {
            var s = sorted[i];
            result.Add(new MerchantLeaderboardEntry(
                i + 1,
                s.EntityId,
                s.Name,
                s.IsPlayer,
                s.Enterprise,
                s.Tier,
                s.TierTitle,
                s.NetWorth,
                s.BaseCitadel,
                s.Specialty,
                s.Status));
        }

        return result;
    }

    private string ResolveGoodName(long goodId)
    {
        if (_broker != null && _broker.TryGet<IItemCatalog>(CapabilityIds.ItemCatalog, out var catalog) && catalog != null)
        {
            var item = catalog.Find(goodId);
            if (item != null) return item.NameVi;
        }

        return goodId switch
        {
            1 => "Gạo",
            2 => "Muối",
            3 => "Trà",
            4 => "Lụa",
            5 => "Sắt",
            6 => "Gỗ",
            7 => "Thuốc",
            8 => "Cá khô",
            9 => "Lúa mì",
            10 => "Rượu nếp",
            11 => "Vải thô",
            12 => "Gấm Thục",
            13 => "Nhân sâm",
            14 => "Trầm hương",
            15 => "Than đá",
            16 => "Đồng thau",
            _ => "Hàng hóa"
        };
    }

}
