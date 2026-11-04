using SimT.Contracts;
using SimT.Contracts.Data;
using SimT.Contracts.Logging;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Military;

internal sealed class MilitaryService : IMilitaryCommand
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
        ILog log)
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

        // Run full multi-round tactical duel simulation
        var duel = TacticalDuelEngine.SimulateDuel(_attributes, npc, playerBranch);

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

        _log.Info($"Arena duel against {npc.NameVi} ({TroopBranchHelper.NameVi(duel.OpponentBranch)}): won={duel.Won}, rounds={duel.Rounds}, prize={duel.GoldReward}.");
        return duel;
    }

    public string AssaultStronghold(long strongholdId)
    {
        var stronghold = _db.QueryOne(
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

        if (stronghold.LastClearedDate is not null)
        {
            var daysSince = _clock.Today.DayIndex - stronghold.LastClearedDate.Value.DayIndex;
            if (daysSince < 30)
            {
                var daysRemaining = 30 - daysSince;
                return $"Sào huyệt {stronghold.NameVi} vừa bị tiễu phạt cách đây {daysSince} ngày, thổ phỉ tan tác chưa kịp tụ tập lại (cần {daysRemaining} ngày nữa mới xuất hiện).";
            }
        }

        if (_attributes.Vitals.CurrentStamina < 15 || _attributes.Vitals.Fatigue >= 95)
        {
            return $"Thể lực đã cạn kiệt (mệt mỏi {_attributes.Vitals.Fatigue}/100). Không đủ sức dẫn binh tiến đánh sào huyệt hiểm trở. Hãy tĩnh dưỡng nghỉ ngơi!";
        }

        _attributes.ApplyFatigue(25);
        _clock.Advance(1);

        var today = _clock.Today;
        var formattedDate = GameDateCodec.Format(today);

        var pAttr = _attributes.Attributes;
        var playerPower = pAttr.War + (pAttr.Con / 3) + (pAttr.Ldr / 3);
        var requiredPower = stronghold.DangerLevel * 14;

        var won = playerPower >= requiredPower;
        if (!won)
        {
            _attributes.ApplyFatigue(10);
            return $"Thất bại! Thế lực {stronghold.NameVi} quá đông đảo (Độ nguy hiểm cấp {stronghold.DangerLevel}). Quân ta lực bất tòng tâm đành phải tạm lui binh.";
        }

        var item = _items.FindByCode(stronghold.ItemRewardCode);
        var currentCity = _player.CurrentCityId;

        _db.Transaction(() =>
        {
            _wallet.Pay(stronghold.LootGold, "military.loot", $"Chiến lợi phẩm tiễu phạt {stronghold.NameVi}", $"assault:{strongholdId}:{formattedDate}");

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

        var itemName = item?.NameVi ?? "Quân lương";
        _log.Info($"Assaulted stronghold {stronghold.NameVi}: victory, loot={stronghold.LootGold}, item={itemName}.");
        return $"Đại Thắng! Tiễu trừ {stronghold.NameVi}, thu hồi {stronghold.LootGold} Quan và 2 {itemName} nạp vào hành trang.";
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
}
