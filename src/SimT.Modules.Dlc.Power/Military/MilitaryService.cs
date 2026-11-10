using SimT.Contracts;
using SimT.Contracts.Data;
using SimT.Contracts.Logging;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Power;

internal sealed partial class MilitaryService : IMilitaryCommand
{
    private readonly IDbSession _db;
    private readonly IGameClock _clock;
    private readonly IWallet _wallet;
    private readonly IBag _bag;
    private readonly INpcRegistry _registry;
    private readonly IWorldMap _map;
    private readonly IPlayerState _player;
    private readonly IPlayerAttributes _attributes;
    private readonly IItemCatalog _items;
    private readonly ILog _log;
    private readonly IPlayerEquipment? _equipment;
    private readonly SimT.Contracts.Modules.ICapabilityBroker? _capabilities;

    public MilitaryService(
        IDbSession db,
        IGameClock clock,
        IWallet wallet,
        IBag bag,
        INpcRegistry registry,
        IWorldMap map,
        IPlayerState player,
        IPlayerAttributes attributes,
        IItemCatalog items,
        ILog log,
        IPlayerEquipment? equipment = null,
        SimT.Contracts.Modules.ICapabilityBroker? capabilities = null)
    {
        _db = db;
        _clock = clock;
        _wallet = wallet;
        _bag = bag;
        _registry = registry;
        _map = map;
        _player = player;
        _attributes = attributes;
        _items = items;
        _log = log;
        _equipment = equipment;
        _capabilities = capabilities;
    }

    public int GetCitadelSecurity(long citadelId)
    {
        var strongholds = StrongholdsAt(citadelId);
        if (strongholds.Count == 0)
        {
            return 85;
        }

        var baseSecurity = 50;
        var today = _clock.Today.DayIndex;

        foreach (var sh in strongholds)
        {
            if (sh.LastClearedDate.HasValue)
            {
                var daysSince = today - sh.LastClearedDate.Value.DayIndex;
                if (daysSince <= 90)
                {
                    baseSecurity += 15;
                }
            }
            else if (sh.DangerLevel >= 3)
            {
                baseSecurity -= 10;
            }
        }

        var govRow = _db.QueryOne(
            "SELECT defense_strength FROM po_city_governance WHERE citadel_id = @citId",
            r => new { Defense = r.GetInt32(0) },
            DbArgs.One("@citId", citadelId));
        if (govRow != null)
        {
            if (govRow.Defense < 40)
            {
                baseSecurity -= 15;
            }
            else if (govRow.Defense >= 80)
            {
                baseSecurity += 5;
            }
        }

        return Math.Clamp(baseSecurity, 10, 100);
    }

    public IReadOnlyList<StrongholdDef> StrongholdsAt(long citadelId)
    {
        return _db.Query(
            "SELECT id, citadel_id, name_vi, bandit_faction, danger_level, loot_gold, item_reward_code, cleared_count, last_cleared_date " +
            "FROM ml_stronghold WHERE citadel_id = @citId ORDER BY danger_level ASC",
            record => new StrongholdDef(
                record.GetInt64(0),
                record.GetInt64(1),
                record.GetString(2),
                record.GetString(3),
                record.GetInt32(4),
                record.GetInt32(5),
                record.GetString(6),
                record.GetInt32(7),
                record.IsDbNull(8) ? null : GameDateCodec.Parse(record.GetString(8)),
                TacticalDuelEngine.ResolveStrongholdBranch(record.GetInt64(0), record.GetString(3), record.GetString(2))),
            DbArgs.One("@citId", citadelId));
    }

    public IReadOnlyList<StrongholdDef> AllStrongholds()
    {
        return _db.Query(
            "SELECT id, citadel_id, name_vi, bandit_faction, danger_level, loot_gold, item_reward_code, cleared_count, last_cleared_date " +
            "FROM ml_stronghold ORDER BY citadel_id ASC, danger_level ASC",
            record => new StrongholdDef(
                record.GetInt64(0),
                record.GetInt64(1),
                record.GetString(2),
                record.GetString(3),
                record.GetInt32(4),
                record.GetInt32(5),
                record.GetString(6),
                record.GetInt32(7),
                record.IsDbNull(8) ? null : GameDateCodec.Parse(record.GetString(8)),
                TacticalDuelEngine.ResolveStrongholdBranch(record.GetInt64(0), record.GetString(3), record.GetString(2))));
    }

    public IReadOnlyList<SupplyRequisitionDef> ActiveRequisitions(long citadelId)
    {
        return _db.Query(
            "SELECT id, citadel_id, target_item_code, target_item_name, quantity_needed, reward_per_unit, deadline_date, fulfilled " +
            "FROM ml_supply_requisition WHERE citadel_id = @citId AND fulfilled = 0 ORDER BY id ASC",
            record => new SupplyRequisitionDef(
                record.GetInt64(0),
                record.GetInt64(1),
                record.GetString(2),
                record.GetString(3),
                record.GetInt32(4),
                record.GetInt32(5),
                GameDateCodec.Parse(record.GetString(6)),
                record.GetInt32(7) == 1),
            DbArgs.One("@citId", citadelId));
    }

    public TroopBranch ResolveNpcBranch(long npcId)
    {
        var npc = _registry.Find(npcId)
                  ?? throw new SimException("military.npc_not_found", $"Không tìm thấy võ sĩ mã {npcId}.");
        return TacticalDuelEngine.ResolveNpcBranch(npc);
    }

    public DuelResult ChallengeArena(long npcId) => ChallengeArena(npcId, TroopBranch.Infantry);

    public DuelResult ChallengeArena(long npcId, TroopBranch playerBranch)
    {
        var npc = _registry.Find(npcId)
                  ?? throw new SimException("military.npc_not_found", $"Không tìm thấy võ sĩ mã {npcId}.");

        if (_attributes.Vitals.CurrentStamina < 10 || _attributes.Vitals.Fatigue >= 95)
        {
            return new DuelResult(
                false,
                0,
                0,
                npc.NameVi,
                $"Thể lực đã cạn kiệt (mệt mỏi {_attributes.Vitals.Fatigue}/100). Hãy bấm 'Nghỉ ngơi 💤' hoặc 'Ngày tiếp ▸' để hồi phục thể lực trước khi lên đài!",
                playerBranch,
                TacticalDuelEngine.ResolveNpcBranch(npc),
                100,
                100,
                0,
                100,
                Array.Empty<DuelRoundLog>(),
                !string.IsNullOrEmpty(npc.NameVi) ? npc.NameVi[..1] : "武",
                npc.Role,
                npc.Attributes.War);
        }

        _attributes.ApplyFatigue(18);
        _clock.Advance(1);

        var today = _clock.Today;
        var formattedDate = GameDateCodec.Format(today);

        IReadOnlyList<NpcTraitDef>? opponentTraits = null;
        ISocialNetwork? social = null;
        if (_capabilities is not null)
        {
            _capabilities.TryGet(CapabilityIds.SocialNetwork, out social);
            if (social is not null)
                opponentTraits = social.GetTraits(npcId);
        }

        // Run full multi-round tactical duel simulation
        var destinyDamage = 0;
        var hasWarlord = false;
        if (_capabilities?.TryGet<IPlayerDestiny>(CapabilityIds.PlayerDestiny, out var destiny) == true && destiny is not null)
        {
            destinyDamage = destiny.GetModifier("duel_damage_bonus");
            hasWarlord = destiny.HasKeystone("binhgia_keystone_warlord");
        }
        var duel = TacticalDuelEngine.SimulateDuel(_attributes, npc, playerBranch, opponentTraits: opponentTraits, destinyDamageBonus: destinyDamage, hasWarlordKeystone: hasWarlord);

        if (duel.Won && social is not null)
        {
            var meVoBonus = social.RecordDuelVictoryBonus(npcId);
            if (meVoBonus > 0)
            {
                duel = duel with
                {
                    CombatSummaryVi = $"{duel.CombatSummaryVi} Đặc tính [Mê võ] khiến {npc.NameVi} nể phục bản lĩnh của bạn (+{meVoBonus} Hảo cảm)."
                };
            }
        }

        _db.Transaction(() =>
        {
            if (duel.Won && duel.GoldReward > 0)
            {
                _wallet.Pay(duel.GoldReward, "military.arena", $"Thưởng quán quân lôi đài đả bại {npc.NameVi}", $"duel:{npcId}:{formattedDate}");
                _attributes.AddExp("WAR", 30);
                _attributes.AddExp("LDR", 15);
            }
            else
            {
                _attributes.AddExp("WAR", 15);
            }

            _db.Execute(
                "INSERT INTO ml_duel_log (date, challenger_name, opponent_npc_id, opponent_name, citadel_id, winner, rounds, log_details, reward_gold) " +
                "VALUES (@date, 'Lữ Khách', @npcId, @npcName, @citId, @winner, @rounds, @details, @prize)",
                new DbArgs()
                    .Add("@date", formattedDate)
                    .Add("@npcId", npcId)
                    .Add("@npcName", npc.NameVi)
                    .Add("@citId", _player.CurrentCityId)
                    .Add("@winner", duel.Won ? "player" : "npc")
                    .Add("@rounds", duel.Rounds)
                    .Add("@details", duel.CombatSummaryVi)
                    .Add("@prize", duel.GoldReward));
        });

        _equipment?.DegradeEquipped("WEAPON", 3);
        _equipment?.DegradeEquipped("ARMOR", 3);

        _log.Info($"Arena duel against {npc.NameVi} ({TroopBranchHelper.NameVi(duel.OpponentBranch)}): won={duel.Won}, rounds={duel.Rounds}, prize={duel.GoldReward}.");
        return duel;
    }

    public BanditChiefDef GetStrongholdChief(long strongholdId)
    {
        var sh = GetStronghold(strongholdId);
        return BanditExpeditionEngine.ResolveChief(sh);
    }

    private StrongholdDef GetStronghold(long strongholdId)
    {
        return _db.QueryOne(
            "SELECT id, citadel_id, name_vi, bandit_faction, danger_level, loot_gold, item_reward_code, cleared_count, last_cleared_date " +
            "FROM ml_stronghold WHERE id = @id",
            record => new StrongholdDef(
                record.GetInt64(0),
                record.GetInt64(1),
                record.GetString(2),
                record.GetString(3),
                record.GetInt32(4),
                record.GetInt32(5),
                record.GetString(6),
                record.GetInt32(7),
                record.IsDbNull(8) ? null : GameDateCodec.Parse(record.GetString(8)),
                TacticalDuelEngine.ResolveStrongholdBranch(record.GetInt64(0), record.GetString(3), record.GetString(2))),
            DbArgs.One("@id", strongholdId))
            ?? throw new SimException("military.stronghold_not_found", $"Không tìm thấy sào huyệt mã {strongholdId}.");
    }

    public BanditExpeditionResult SimulateBanditExpedition(long strongholdId, TroopBranch playerBranch)
    {
        var stronghold = GetStronghold(strongholdId);
        var item = _items.FindByCode(stronghold.ItemRewardCode);
        var itemName = item?.NameVi ?? "Quân lương";
        return BanditExpeditionEngine.SimulateExpedition(_attributes, stronghold, playerBranch, itemName);
    }

    public BanditExpeditionResult ExecuteBanditExpedition(long strongholdId, TroopBranch playerBranch)
    {
        var stronghold = GetStronghold(strongholdId);

        if (stronghold.LastClearedDate is not null)
        {
            var daysSince = _clock.Today.DayIndex - stronghold.LastClearedDate.Value.DayIndex;
            if (daysSince < 30)
            {
                var daysRemaining = 30 - daysSince;
                var chief = BanditExpeditionEngine.ResolveChief(stronghold);
                return new BanditExpeditionResult(
                    false,
                    0,
                    0,
                    stronghold.ItemRewardCode,
                    "",
                    0,
                    0,
                    0,
                    0,
                    chief.TroopCount,
                    chief.TroopCount,
                    chief,
                    stronghold,
                    Array.Empty<BanditBattleRoundLog>(),
                    $"Sào huyệt {stronghold.NameVi} vừa bị tiễu phạt cách đây {daysSince} ngày, thổ phỉ tan tác chưa kịp tụ tập lại (cần {daysRemaining} ngày nữa mới xuất hiện).");
            }
        }

        if (_attributes.Vitals.CurrentStamina < 15 || _attributes.Vitals.Fatigue >= 95)
        {
            var chief = BanditExpeditionEngine.ResolveChief(stronghold);
            return new BanditExpeditionResult(
                false,
                0,
                0,
                stronghold.ItemRewardCode,
                "",
                0,
                0,
                0,
                0,
                chief.TroopCount,
                chief.TroopCount,
                chief,
                stronghold,
                Array.Empty<BanditBattleRoundLog>(),
                $"Thể lực đã cạn kiệt (mệt mỏi {_attributes.Vitals.Fatigue}/100). Không đủ sức dẫn binh tiến đánh sào huyệt hiểm trở. Hãy tĩnh dưỡng nghỉ ngơi!");
        }

        _attributes.ApplyFatigue(25);
        _clock.Advance(1);

        var today = _clock.Today;
        var formattedDate = GameDateCodec.Format(today);

        var item = _items.FindByCode(stronghold.ItemRewardCode);
        var itemName = item?.NameVi ?? "Quân lương";
        var result = BanditExpeditionEngine.SimulateExpedition(_attributes, stronghold, playerBranch, itemName);

        if (result.Won)
        {
            var currentCity = _player.CurrentCityId;
            _db.Transaction(() =>
            {
                var actualLoot = (long)stronghold.LootGold;
                if (_capabilities?.TryGet<IPlayerDestiny>(CapabilityIds.PlayerDestiny, out var dst) == true && dst is not null)
                {
                    var bountyBonus = dst.GetModifier("bandit_bounty_bonus");
                    if (bountyBonus > 0)
                    {
                        actualLoot = (long)Math.Round(actualLoot * (1.0 + bountyBonus / 100.0));
                    }
                }
                _wallet.Pay(actualLoot, "military.loot", $"Chiến lợi phẩm tiễu phạt {stronghold.NameVi}", $"assault:{strongholdId}:{formattedDate}");

                if (item is not null)
                {
                    _bag.Add(currentCity, item.Id, 2);
                }

                _db.Execute(
                    "UPDATE ml_stronghold SET cleared_count = cleared_count + 1, last_cleared_date = @date WHERE id = @id",
                    new DbArgs().Add("@date", formattedDate).Add("@id", strongholdId));

                _attributes.AddExp("WAR", 30);
                _attributes.AddExp("LDR", 20);
                _attributes.GainFame(stronghold.DangerLevel * 15, $"Tiễu phạt sào huyệt {stronghold.NameVi}, an định bờ cõi.", virtueDelta: 5);
            });

            _equipment?.DegradeEquipped("WEAPON", 5);
            _equipment?.DegradeEquipped("ARMOR", 5);

            if (_equipment != null)
            {
                var weaponSlot = _equipment.GetSlot("WEAPON");
                if (weaponSlot?.EquipmentId != null)
                {
                    _equipment.RecordCombatFeat(
                        weaponSlot.EquipmentId.Value,
                        _player.Name,
                        $"tiễu phạt sào huyệt {stronghold.NameVi}, tiêu diệt thủ ác",
                        killDelta: stronghold.DangerLevel * 10,
                        fameDelta: stronghold.DangerLevel * 15);
                }
            }
            _log.Info($"Assaulted stronghold {stronghold.NameVi}: victory, loot={stronghold.LootGold}, item={itemName}.");
        }
        else
        {
            _attributes.ApplyFatigue(10);
            _log.Info($"Assaulted stronghold {stronghold.NameVi}: defeat.");
        }

        return result;
    }

    public string AssaultStronghold(long strongholdId)
    {
        var result = ExecuteBanditExpedition(strongholdId, TroopBranch.Infantry);
        return result.SummaryVi;
    }

    public string FulfillRequisition(long requisitionId)
    {
        var req = _db.QueryOne(
            "SELECT id, citadel_id, target_item_code, target_item_name, quantity_needed, reward_per_unit, deadline_date, fulfilled " +
            "FROM ml_supply_requisition WHERE id = @id",
            record => new SupplyRequisitionDef(
                record.GetInt64(0),
                record.GetInt64(1),
                record.GetString(2),
                record.GetString(3),
                record.GetInt32(4),
                record.GetInt32(5),
                GameDateCodec.Parse(record.GetString(6)),
                record.GetInt32(7) == 1),
            DbArgs.One("@id", requisitionId))
            ?? throw new SimException("military.requisition_not_found", $"Không tìm thấy lệnh điều động quân cơ mã {requisitionId}.");

        if (req.Fulfilled)
        {
            return "Lệnh quân cơ này đã được hoàn tất trước đó.";
        }

        var item = _items.FindByCode(req.TargetItemCode)
                   ?? throw new SimException("military.item_missing", $"Vật phẩm quân lương {req.TargetItemCode} không tồn tại.");

        var currentCity = _player.CurrentCityId;
        if (_bag.Quantity(currentCity, item.Id) < req.QuantityNeeded)
        {
            throw new SimException("military.insufficient_goods",
                $"Trong kho hành trang không đủ {req.QuantityNeeded} {req.TargetItemName} (hiện có {_bag.Quantity(currentCity, item.Id)}).");
        }

        var today = _clock.Today;
        var formattedDate = GameDateCodec.Format(today);
        var totalReward = req.QuantityNeeded * req.RewardPerUnit;

        _db.Transaction(() =>
        {
            if (!_bag.Remove(currentCity, item.Id, req.QuantityNeeded))
            {
                throw new SimException("military.deduct_failed", "Lỗi khấu trừ vật phẩm từ hành trang.");
            }

            _wallet.Pay(totalReward, "military.supply", $"Thù lao cung ứng quân nhu: {req.TargetItemName}", $"supply:{requisitionId}:{formattedDate}");

            _db.Execute(
                "UPDATE ml_supply_requisition SET fulfilled = 1 WHERE id = @id",
                DbArgs.One("@id", requisitionId));

            _attributes.AddExp("LOG", 20);
            _attributes.AddExp("DIP", 15);
            _attributes.GainFame(15, $"Cung ứng quân nhu {req.TargetItemName} cho nghĩa quân.", credDelta: 5);
        });

        _log.Info($"Fulfilled supply requisition {req.TargetItemName} x{req.QuantityNeeded} for {totalReward} Quan.");
        return $"Đã giao nộp {req.QuantityNeeded} {req.TargetItemName} cho quân cơ doanh, nhận thưởng hậu hĩ {totalReward} Quan!";
    }

    public CitadelArmoryDef GetCitadelArmory(long citadelId)
    {
        var armory = _db.QueryOne(
            "SELECT citadel_id, weapons_stock, weapons_capacity, monthly_iron_in, monthly_arms_out, last_status_vi, updated_day_index " +
            "FROM ml_citadel_armory WHERE citadel_id = @citId",
            r => new CitadelArmoryDef(
                r.GetInt64(0),
                r.GetInt32(1),
                r.GetInt32(2),
                r.GetInt32(3),
                r.GetInt32(4),
                r.GetString(5),
                r.GetInt32(6)),
            DbArgs.One("@citId", citadelId));

        if (armory != null)
        {
            return armory;
        }

        return new CitadelArmoryDef(citadelId, 40, 100, 0, 0, "Binh giới sung túc", (int)_clock.Today.DayIndex);
    }

    public ArmorySimulationResult SimulateMonthlyArmoryAndProcurement(long citadelId, GameDate date)
    {
        var armory = GetCitadelArmory(citadelId);
        var currentWeapons = armory.WeaponsStock;
        var maxCapacity = armory.WeaponsCapacity;

        var ironConsumed = 0;
        var armsForged = 0;

        // 1. Autonomous City Forge: transforms raw iron & fuel into weapons
        IMarketTrade? trade = null;
        if (_capabilities != null && _capabilities.TryGet<IMarketTrade>(CapabilityIds.MarketTrade, out var t))
        {
            trade = t;
        }

        if (trade != null)
        {
            // Iron: item id 5 ('sat')
            // Coal: item id 15 ('than_da')
            // Timber: item id 6 ('go')
            var ironStock = trade.StallStock(citadelId, 5);
            var coalStock = trade.StallStock(citadelId, 15);
            var woodStock = trade.StallStock(citadelId, 6);

            var forgeCapacity = 8;
            var spaceAvailable = Math.Max(0, maxCapacity - currentWeapons);
            var maxArmsCanForge = Math.Min(forgeCapacity, spaceAvailable);

            if (maxArmsCanForge > 0 && ironStock >= 2)
            {
                var ironLimiter = ironStock / 2;
                var fuelLimiter = coalStock > 0 ? coalStock : (woodStock / 2);
                var possibleArms = Math.Min(maxArmsCanForge, Math.Min(ironLimiter, fuelLimiter));

                if (possibleArms > 0)
                {
                    armsForged = possibleArms;
                    ironConsumed = armsForged * 2;
                    trade.AdjustStock(citadelId, 5, -ironConsumed);

                    if (coalStock >= armsForged)
                    {
                        trade.AdjustStock(citadelId, 15, -armsForged);
                    }
                    else if (woodStock >= armsForged * 2)
                    {
                        trade.AdjustStock(citadelId, 6, -(armsForged * 2));
                    }

                    currentWeapons += armsForged;
                }
            }
        }

        // 2. Garrison Weapon Maintenance & Attrition
        var strongholds = StrongholdsAt(citadelId);
        var dangerExtra = strongholds.Count(s => s.DangerLevel >= 4 && !s.LastClearedDate.HasValue);
        var garrisonDemand = 6 + dangerExtra;

        var armsSupplied = Math.Min(garrisonDemand, currentWeapons);
        currentWeapons -= armsSupplied;
        var deficit = garrisonDemand - armsSupplied;

        // 3. Impact on Citadel Defense Strength in po_city_governance
        var oldDefense = 80;
        var govRow = _db.QueryOne(
            "SELECT defense_strength FROM po_city_governance WHERE citadel_id = @citId",
            r => new { Defense = r.GetInt32(0) },
            DbArgs.One("@citId", citadelId));

        if (govRow != null)
        {
            oldDefense = govRow.Defense;
        }

        int newDefense;
        string statusVi;

        if (deficit > 0)
        {
            // Severe deficit penalty: -8 defense per missing weapon unit
            newDefense = Math.Max(10, oldDefense - (deficit * 8));
            statusVi = $"Khủng hoảng quân giới: thiếu {deficit} binh khí! Phòng thủ suy giảm.";

            // Issue emergency military supply requisition for iron with 120 Quan bounty
            var existingReqCount = _db.ScalarLong(
                "SELECT COUNT(*) FROM ml_supply_requisition WHERE citadel_id = @citId AND target_item_code = 'sat' AND fulfilled = 0",
                DbArgs.One("@citId", citadelId));

            if (existingReqCount == 0)
            {
                var nextId = _db.ScalarLong("SELECT COALESCE(MAX(id), 0) + 1 FROM ml_supply_requisition");
                var deadline = GameDateCodec.Format(date.AddMonths(2));
                _db.Execute(
                    "INSERT INTO ml_supply_requisition (id, citadel_id, target_item_code, target_item_name, quantity_needed, reward_per_unit, deadline_date, fulfilled) " +
                    "VALUES (@id, @citId, 'sat', 'Quân Khẩn: Thiết Liệu Đúc Binh Khí', @qty, 120, @deadline, 0)",
                    new DbArgs().Add("@id", nextId).Add("@citId", citadelId).Add("@qty", deficit * 3).Add("@deadline", deadline));
            }
        }
        else
        {
            newDefense = Math.Min(95, oldDefense + 3);
            statusVi = "Binh giới sung túc, quân nhu nghiêm mật";
        }

        if (govRow != null)
        {
            _db.Execute(
                "UPDATE po_city_governance SET defense_strength = @def WHERE citadel_id = @citId",
                new DbArgs().Add("@def", newDefense).Add("@citId", citadelId));
        }

        // 4. Update ml_citadel_armory
        _db.Execute(
            """
            INSERT INTO ml_citadel_armory (citadel_id, weapons_stock, weapons_capacity, monthly_iron_in, monthly_arms_out, last_status_vi, updated_day_index)
            VALUES (@citId, @stock, @cap, @ironIn, @armsOut, @status, @day)
            ON CONFLICT(citadel_id) DO UPDATE SET
                weapons_stock = excluded.weapons_stock,
                monthly_iron_in = excluded.monthly_iron_in,
                monthly_arms_out = excluded.monthly_arms_out,
                last_status_vi = excluded.last_status_vi,
                updated_day_index = excluded.updated_day_index
            """,
            new DbArgs()
                .Add("@citId", citadelId)
                .Add("@stock", currentWeapons)
                .Add("@cap", maxCapacity)
                .Add("@ironIn", ironConsumed)
                .Add("@armsOut", armsSupplied)
                .Add("@status", statusVi)
                .Add("@day", (int)date.DayIndex));

        _log.Info($"[dlc.military] Armory at #{citadelId} on {date}: arms={currentWeapons}/{maxCapacity} (+{armsForged} forged, -{armsSupplied} garrison, def={newDefense})");

        return new ArmorySimulationResult(
            citadelId,
            armory.WeaponsStock,
            ironConsumed,
            armsForged,
            garrisonDemand,
            armsSupplied,
            deficit,
            currentWeapons,
            oldDefense,
            newDefense,
            statusVi);
    }

    public void ReinforceArmory(long citadelId, int weaponCount, string statusVi)
    {
        _db.Execute(
            """
            UPDATE ml_citadel_armory 
            SET weapons_stock = MIN(weapons_capacity, weapons_stock + @weapons),
                last_status_vi = @status
            WHERE citadel_id = @citadel
            """,
            new DbArgs().Add("@weapons", weaponCount).Add("@status", statusVi).Add("@citadel", citadelId));
    }
}
