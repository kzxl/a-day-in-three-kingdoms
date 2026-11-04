using SimT.Contracts.Data;
using SimT.Contracts.Identity;
using SimT.Contracts.Ledger;
using SimT.Contracts.Logging;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;
using SimT.Contracts;

namespace SimT.Modules.Dlc.Caravan;

/// <summary>
/// Freight, trade routes, retainers escort, and logistics contracts.
/// Goods leave the current city's bag, travel for the road's duration and land in the destination city's bag.
/// Every unit of freight is paid for, so shipping is never a free teleport.
/// </summary>
internal sealed class CaravanService : ICaravanGuild
{
    public const long DefaultFreightPerUnit = 8;

    private readonly IDbSession _db;
    private readonly IGameClock _clock;
    private readonly IWallet _wallet;
    private readonly IBag _bag;
    private readonly IWorldMap _map;
    private readonly IItemCatalog _items;
    private readonly IPlayerState _player;
    private readonly IIdGenerator _ids;
    private readonly ILog _log;
    private readonly IActionTime _actionTime;
    private readonly long _freightPerUnit;

    public CaravanService(
        IDbSession db,
        IGameClock clock,
        IWallet wallet,
        IBag bag,
        IWorldMap map,
        IItemCatalog items,
        IPlayerState player,
        IIdGenerator ids,
        ILog log,
        IActionTime actionTime,
        long freightPerUnit = DefaultFreightPerUnit)
    {
        _db = db;
        _clock = clock;
        _wallet = wallet;
        _bag = bag;
        _map = map;
        _items = items;
        _player = player;
        _ids = ids;
        _log = log;
        _actionTime = actionTime;
        _freightPerUnit = freightPerUnit > 0
            ? freightPerUnit
            : throw new SimException("caravan.config_invalid", $"freightPerUnit must be positive, got {freightPerUnit}.");
    }

    public long FeePerUnit => _freightPerUnit;

    public IReadOnlyList<CaravanRunSummary> Runs =>
        _db.Query(
            """
            SELECT r.id, r.from_city, r.to_city, r.depart_day, r.arrive_day, r.status,
                   COALESCE((SELECT SUM(quantity) FROM cv_cargo c WHERE c.run_id = r.id), 0),
                   COALESCE((SELECT SUM(lost) FROM cv_cargo c WHERE c.run_id = r.id), 0),
                   r.escort_npc_id,
                   r.ambush_loss
            FROM cv_run r
            ORDER BY r.id DESC
            LIMIT 20
            """,
            row => new CaravanRunSummary(
                row.GetInt64(0),
                row.GetInt64(1),
                row.GetInt64(2),
                row.GetInt32(3),
                row.GetInt32(4),
                row.GetString(5),
                row.GetInt32(6),
                row.GetInt32(7),
                row.IsDbNull(8) ? null : row.GetInt64(8),
                row.GetInt32(9)));

    public IReadOnlyList<FreightContractDef> Contracts =>
        _db.Query(
            """
            SELECT id, guild_name, from_city, to_city, item_id, quantity, reward_copper, deadline_day, status, accepted_day, completed_day
            FROM cv_contract
            ORDER BY id ASC
            """,
            row => new FreightContractDef(
                row.GetInt64(0),
                row.GetString(1),
                row.GetInt64(2),
                row.GetInt64(3),
                row.GetInt64(4),
                row.GetInt32(5),
                row.GetInt32(6),
                row.GetInt32(7),
                row.GetString(8),
                row.IsDbNull(9) ? null : row.GetInt32(9),
                row.IsDbNull(10) ? null : row.GetInt32(10)));

    public int UnitsInTransit =>
        (int)_db.ScalarLong(
            "SELECT COALESCE(SUM(c.quantity), 0) FROM cv_cargo c JOIN cv_run r ON r.id = c.run_id WHERE r.status = 'RUNNING'");

    public CaravanResult Send(long fromCity, long toCity) =>
        SendWithEscort(fromCity, toCity, null);

    public CaravanResult SendWithEscort(long fromCity, long toCity, long? escortNpcId)
    {
        var days = _map.TravelDays(fromCity, toCity);
        if (days <= 0)
        {
            return CaravanResult.Failure("Không có đường thương giữa hai thành này.");
        }

        var rows = _bag.Rows(fromCity).Where(row => row.Quantity > 0).ToList();
        if (rows.Count == 0)
        {
            return CaravanResult.Failure("Túi trống, không có gì để gửi.");
        }

        var units = rows.Sum(row => row.Quantity);
        var fee = units * _freightPerUnit;
        if (_wallet.Liquid < fee)
        {
            return CaravanResult.Failure($"Cần {fee} Quan cước, chỉ có {_wallet.Liquid}.");
        }

        var departDay = _clock.Today.DayIndex;
        var arriveDay = departDay + days;

        _db.Transaction(() =>
        {
            _actionTime.Charge(ActionCodes.CaravanSend);
            _wallet.Charge(fee, JournalCodes.CaravanFreight, $"Cước {units} đơn vị về thành {toCity}");

            foreach (var row in rows)
            {
                if (!_bag.Remove(fromCity, row.ItemId, row.Quantity))
                {
                    throw new SimException("caravan.bag_race", "Bag changed while loading the caravan; rolled back.");
                }
            }

            var runId = _ids.Next("caravan.run");
            _db.Execute(
                """
                INSERT INTO cv_run(id, from_city, to_city, depart_day, arrive_day, fee_paid, status, escort_npc_id, ambush_loss)
                VALUES(@id, @from, @to, @depart, @arrive, @fee, 'RUNNING', @escort, 0)
                """,
                new DbArgs()
                    .Add("@id", runId)
                    .Add("@from", fromCity)
                    .Add("@to", toCity)
                    .Add("@depart", departDay)
                    .Add("@arrive", arriveDay)
                    .Add("@fee", fee)
                    .Add("@escort", (object?)escortNpcId ?? DBNull.Value));

            foreach (var row in rows)
            {
                _db.Execute(
                    "INSERT INTO cv_cargo(run_id, item_id, quantity) VALUES(@run, @item, @quantity)",
                    new DbArgs().Add("@run", runId).Add("@item", row.ItemId).Add("@quantity", row.Quantity));
            }
        });

        var escortText = escortNpcId.HasValue ? " (có tiêu đầu hộ vệ)" : string.Empty;
        return CaravanResult.Success(
            $"Đoàn {units} đơn vị lên đường{escortText}, tới sau {days} ngày (ngày {GameDate.FromDayIndex(arriveDay)}), cước {fee} Quan.");
    }

    public CaravanResult Travel(long fromCity, long toCity, IPlayerState player)
    {
        var days = _map.TravelDays(fromCity, toCity);
        if (days <= 0)
        {
            return CaravanResult.Failure("Không có đường đi giữa hai thành này.");
        }

        _db.Transaction(() =>
        {
            _clock.Advance(days);
            player.MoveTo(toCity, _clock.Today);

            // Escort active freight contracts cargo from starting city to destination city
            var activeContracts = _db.Query(
                "SELECT id, item_id, quantity FROM cv_contract WHERE status = 'IN_TRANSIT' AND from_city = @from AND to_city = @to",
                row => (Id: row.GetInt64(0), ItemId: row.GetInt64(1), Quantity: row.GetInt32(2)),
                new DbArgs().Add("@from", fromCity).Add("@to", toCity));

            foreach (var c in activeContracts)
            {
                if (_bag.Quantity(fromCity, c.ItemId) >= c.Quantity)
                {
                    _bag.Remove(fromCity, c.ItemId, c.Quantity);
                    _bag.Add(toCity, c.ItemId, c.Quantity);
                }
            }
        });

        return CaravanResult.Success($"Đi {days} ngày, tới nơi ngày {_clock.Today}.");
    }

    public CaravanResult AcceptContract(long contractId)
    {
        var contract = _db.QueryOne(
            "SELECT id, guild_name, from_city, to_city, item_id, quantity, reward_copper, deadline_day, status, accepted_day, completed_day FROM cv_contract WHERE id = @id",
            row => new FreightContractDef(
                row.GetInt64(0),
                row.GetString(1),
                row.GetInt64(2),
                row.GetInt64(3),
                row.GetInt64(4),
                row.GetInt32(5),
                row.GetInt32(6),
                row.GetInt32(7),
                row.GetString(8),
                row.IsDbNull(9) ? null : row.GetInt32(9),
                row.IsDbNull(10) ? null : row.GetInt32(10)
            ),
            DbArgs.One("@id", contractId));

        if (contract is null)
        {
            return CaravanResult.Failure("Hợp đồng không tồn tại.");
        }

        if (contract.Status != "AVAILABLE")
        {
            return CaravanResult.Failure("Hợp đồng này không còn khả dụng hoặc đã được tiếp nhận.");
        }

        if (_player.CurrentCityId != contract.FromCityId)
        {
            var fromName = _map.Find(contract.FromCityId)?.NameVi ?? contract.FromCityId.ToString();
            return CaravanResult.Failure($"Hợp đồng này yêu cầu tiếp nhận tại {fromName}.");
        }

        _db.Transaction(() =>
        {
            _db.Execute(
                "UPDATE cv_contract SET status = 'IN_TRANSIT', accepted_day = @day WHERE id = @id",
                new DbArgs().Add("@id", contract.Id).Add("@day", _clock.Today.DayIndex));

            _bag.Add(contract.FromCityId, contract.ItemId, contract.Quantity);
        });

        var destName = _map.Find(contract.ToCityId)?.NameVi ?? contract.ToCityId.ToString();
        var itemName = _items.Find(contract.ItemId)?.NameVi ?? $"Vật phẩm #{contract.ItemId}";
        return CaravanResult.Success(
            $"Đã nhận ủy thác từ {contract.GuildName}. Vận chuyển {contract.Quantity} {itemName} tới thành {destName} trước ngày {GameDate.FromDayIndex(contract.DeadlineDayIndex)} (thù lao {contract.RewardCopper} Quan).");
    }

    public CaravanResult DeliverContract(long contractId)
    {
        var contract = _db.QueryOne(
            "SELECT id, guild_name, from_city, to_city, item_id, quantity, reward_copper, deadline_day, status, accepted_day, completed_day FROM cv_contract WHERE id = @id",
            row => new FreightContractDef(
                row.GetInt64(0),
                row.GetString(1),
                row.GetInt64(2),
                row.GetInt64(3),
                row.GetInt64(4),
                row.GetInt32(5),
                row.GetInt32(6),
                row.GetInt32(7),
                row.GetString(8),
                row.IsDbNull(9) ? null : row.GetInt32(9),
                row.IsDbNull(10) ? null : row.GetInt32(10)
            ),
            DbArgs.One("@id", contractId));

        if (contract is null)
        {
            return CaravanResult.Failure("Hợp đồng không tồn tại.");
        }

        if (contract.Status != "IN_TRANSIT")
        {
            return CaravanResult.Failure("Hợp đồng này chưa được nhận hoặc đã hoàn tất.");
        }

        if (_player.CurrentCityId != contract.ToCityId)
        {
            var destName = _map.Find(contract.ToCityId)?.NameVi ?? contract.ToCityId.ToString();
            return CaravanResult.Failure($"Chưa tới đích để giao hàng. Điểm bàn giao là {destName}.");
        }

        var availableInBag = _bag.Quantity(contract.ToCityId, contract.ItemId);
        if (availableInBag < contract.Quantity)
        {
            var itemName = _items.Find(contract.ItemId)?.NameVi ?? "hàng hóa";
            return CaravanResult.Failure($"Không đủ số lượng {itemName} trong túi để bàn giao (cần {contract.Quantity}, hiện có {availableInBag}).");
        }

        var onTime = _clock.Today.DayIndex <= contract.DeadlineDayIndex;
        var payout = onTime ? contract.RewardCopper : Math.Max(10, contract.RewardCopper / 2);

        _db.Transaction(() =>
        {
            if (!_bag.Remove(contract.ToCityId, contract.ItemId, contract.Quantity))
            {
                throw new SimException("caravan.deliver_bag", "Failed to remove contract goods from bag.");
            }

            _wallet.Pay(payout, JournalCodes.CaravanReward, $"Thù lao vận tiêu hợp đồng {contract.Id} ({contract.GuildName})");

            _db.Execute(
                "UPDATE cv_contract SET status = 'DELIVERED', completed_day = @day WHERE id = @id",
                new DbArgs().Add("@id", contract.Id).Add("@day", _clock.Today.DayIndex));
        });

        return onTime
            ? CaravanResult.Success($"Giao tiêu thành công cho {contract.GuildName}! Nhận đủ {payout} Quan thù lao đúng hạn.")
            : CaravanResult.Success($"Giao tiêu trễ hạn cho {contract.GuildName}! Bị trừ 50% tiền cước, nhận {payout} Quan.");
    }

    /// <summary>Arrival hook: runs on every day, marks a run arrived exactly once.</summary>
    public void OnDay(GameDate date)
    {
        var due = _db.Query(
            "SELECT id, to_city, depart_day, arrive_day, escort_npc_id FROM cv_run WHERE status = 'RUNNING' AND arrive_day <= @day ORDER BY id",
            row => (
                Id: row.GetInt64(0),
                ToCity: row.GetInt64(1),
                Depart: row.GetInt32(2),
                Arrive: row.GetInt32(3),
                EscortNpcId: row.IsDbNull(4) ? (long?)null : row.GetInt64(4)
            ),
            DbArgs.One("@day", date.DayIndex));

        foreach (var run in due)
        {
            var transitDays = Math.Max(1, run.Arrive - run.Depart);

            _db.Transaction(() =>
            {
                var moved = _db.Execute(
                    "UPDATE cv_run SET status = 'ARRIVED' WHERE id=@id AND status = 'RUNNING'",
                    DbArgs.One("@id", run.Id));

                if (moved == 0)
                {
                    return;
                }

                var cargo = _db.Query(
                    "SELECT item_id, quantity FROM cv_cargo WHERE run_id=@id ORDER BY item_id",
                    row => (ItemId: row.GetInt64(0), Quantity: row.GetInt32(1)),
                    DbArgs.One("@id", run.Id));

                foreach (var line in cargo)
                {
                    var lost = Spoilage(line.ItemId, line.Quantity, transitDays);
                    if (lost > 0)
                    {
                        _db.Execute(
                            "UPDATE cv_cargo SET lost=@lost WHERE run_id=@run AND item_id=@item",
                            new DbArgs().Add("@run", run.Id).Add("@item", line.ItemId).Add("@lost", lost));
                    }

                    var delivered = line.Quantity - lost;
                    if (delivered > 0)
                    {
                        _bag.Add(run.ToCity, line.ItemId, delivered);
                    }
                }
            });

            _log.Info($"caravan {run.Id} arrived at citadel {run.ToCity} on {date}");
        }
    }

    /// <summary>
    /// Freight loss: perishables lose transitDays / shelfLife of their quantity on the road,
    /// rounded down, and always keep at least one unit so a short hop never empties a crate.
    /// Durable goods (shelf life 0) arrive whole.
    /// </summary>
    private int Spoilage(long itemId, int quantity, int transitDays)
    {
        var item = _items.Find(itemId);
        if (item is null || item.ShelfLifeDays <= 0 || quantity <= 0)
        {
            return 0;
        }

        var lost = (int)((long)quantity * transitDays / item.ShelfLifeDays);
        return Math.Min(lost, quantity - 1);
    }
}
