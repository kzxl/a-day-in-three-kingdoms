using SimT.Contracts.Data;
using SimT.Contracts.Identity;
using SimT.Contracts.Ledger;
using SimT.Contracts.Logging;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;
using SimT.Contracts;

namespace SimT.Modules.Dlc.Caravan;

/// <summary>
/// Freight and travel. Goods leave the current city's bag, travel for the road's duration and
/// land in the destination city's bag; the player must walk there to sell them. Every unit of
/// freight is paid for, so shipping is never a free teleport.
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
    private readonly IIdGenerator _ids;
    private readonly ILog _log;
    private readonly long _freightPerUnit;

    public CaravanService(
        IDbSession db,
        IGameClock clock,
        IWallet wallet,
        IBag bag,
        IWorldMap map,
        IItemCatalog items,
        IIdGenerator ids,
        ILog log,
        long freightPerUnit = DefaultFreightPerUnit)
    {
        _db = db;
        _clock = clock;
        _wallet = wallet;
        _bag = bag;
        _map = map;
        _items = items;
        _ids = ids;
        _log = log;
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
                   COALESCE((SELECT SUM(lost) FROM cv_cargo c WHERE c.run_id = r.id), 0)
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
                row.GetInt32(7)));

    public int UnitsInTransit =>
        (int)_db.ScalarLong(
            "SELECT COALESCE(SUM(c.quantity), 0) FROM cv_cargo c JOIN cv_run r ON r.id = c.run_id WHERE r.status = 'RUNNING'");

    public CaravanResult Send(long fromCity, long toCity)
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
                INSERT INTO cv_run(id, from_city, to_city, depart_day, arrive_day, fee_paid, status)
                VALUES(@id, @from, @to, @depart, @arrive, @fee, 'RUNNING')
                """,
                new DbArgs()
                    .Add("@id", runId)
                    .Add("@from", fromCity)
                    .Add("@to", toCity)
                    .Add("@depart", departDay)
                    .Add("@arrive", arriveDay)
                    .Add("@fee", fee));

            foreach (var row in rows)
            {
                _db.Execute(
                    "INSERT INTO cv_cargo(run_id, item_id, quantity) VALUES(@run, @item, @quantity)",
                    new DbArgs().Add("@run", runId).Add("@item", row.ItemId).Add("@quantity", row.Quantity));
            }
        });

        return CaravanResult.Success(
            $"Đoàn {units} đơn vị lên đường, tới sau {days} ngày (ngày {GameDate.FromDayIndex(arriveDay)}), cước {fee} Quan.");
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
        });

        return CaravanResult.Success($"Đi {days} ngày, tới nơi ngày {_clock.Today}.");
    }

    /// <summary>Arrival hook: runs on every day, marks a run arrived exactly once.</summary>
    public void OnDay(GameDate date)
    {
        var due = _db.Query(
            "SELECT id, to_city, depart_day, arrive_day FROM cv_run WHERE status = 'RUNNING' AND arrive_day <= @day ORDER BY id",
            row => (Id: row.GetInt64(0), ToCity: row.GetInt64(1), Depart: row.GetInt32(2), Arrive: row.GetInt32(3)),
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

public readonly record struct CaravanResult(bool Ok, string Message)
{
    public static CaravanResult Success(string message) => new(true, message);

    public static CaravanResult Failure(string message) => new(false, message);
}
