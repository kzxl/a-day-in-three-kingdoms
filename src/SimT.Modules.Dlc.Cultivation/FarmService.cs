using SimT.Contracts;
using SimT.Contracts.Data;
using SimT.Contracts.Identity;
using SimT.Contracts.Ledger;
using SimT.Contracts.Logging;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Cultivation;

/// <summary>
/// Seasonal arithmetic. Growth and yield are integer functions of the month, so a replay is
/// identical everywhere and the crop calendar is data a designer can read at a glance.
/// </summary>
internal static class CropCalendar
{
    public const int BaseYieldPerMu = 6;

    public static int GrowthDays(int month) => month switch
    {
        <= 3 => 20,
        <= 6 => 18,
        <= 9 => 22,
        _ => 30
    };

    /// <summary>Permille applied to the base yield: winter is poor, autumn is generous.</summary>
    public static int YieldPermille(int month) => month switch
    {
        <= 3 => 1000,
        <= 6 => 1100,
        <= 9 => 1200,
        _ => 600
    };

    public static int Yield(int areaMu, int month) =>
        (int)(((long)areaMu * BaseYieldPerMu * YieldPermille(month)) + 500) / 1000;
}

/// <summary>
/// Rented land. Rent is charged on the month boundary, sowing costs seed money, and a crop only
/// becomes harvestable on its ready day — the plot is a time and cash commitment, not a button.
/// </summary>
internal sealed class FarmService : ICultivationEstate
{
    public const long SeedCostPerMu = 5;

    private readonly IDbSession _db;
    private readonly IGameClock _clock;
    private readonly IWallet _wallet;
    private readonly IBag _bag;
    private readonly IItemCatalog _items;
    private readonly IIdGenerator _ids;
    private readonly ILog _log;
    private readonly long _grainItemId;

    public FarmService(
        IDbSession db,
        IGameClock clock,
        IWallet wallet,
        IBag bag,
        IItemCatalog items,
        IIdGenerator ids,
        ILog log)
    {
        _db = db;
        _clock = clock;
        _wallet = wallet;
        _bag = bag;
        _items = items;
        _ids = ids;
        _log = log;

        _grainItemId = items.FindByCode("gao")?.Id
                      ?? throw new SimException("cultivation.grain_missing", "The catalog has no 'gao' item to harvest.");
    }

    public IReadOnlyList<PlotView> Plots =>
        _db.Query(
            """
            SELECT p.id, p.citadel_id, p.area_mu, p.monthly_rent, p.rented,
                   c.status, c.ready_day, c.yield_qty
            FROM fm_plot p
            LEFT JOIN fm_crop c ON c.plot_id = p.id AND c.status IN ('GROWING', 'READY')
            ORDER BY p.citadel_id, p.id
            """,
            row => new PlotView(
                row.GetInt64(0),
                row.GetInt64(1),
                row.GetInt32(2),
                row.GetInt64(3),
                row.GetInt64(4) != 0,
                row.GetStringOrEmpty(5) is { Length: > 0 } status ? status : null,
                row.GetStringOrEmpty(5) is { Length: > 0 } ? row.GetInt32(6) : null,
                row.GetStringOrEmpty(5) is { Length: > 0 } ? row.GetInt32(7) : null));

    public long MonthlyRentTotal =>
        _db.ScalarLong("SELECT COALESCE(SUM(monthly_rent), 0) FROM fm_plot WHERE rented = 1");

    public int GrowingCount => (int)_db.ScalarLong("SELECT COUNT(*) FROM fm_crop WHERE status = 'GROWING'");

    public int ReadyCount => (int)_db.ScalarLong("SELECT COUNT(*) FROM fm_crop WHERE status = 'READY'");

    /// <summary>Seeds two plots per settlement the first time the module runs.</summary>
    public void EnsurePlots(IWorldMap map)
    {
        if (_db.ScalarLong("SELECT COUNT(*) FROM fm_plot") > 0)
        {
            return;
        }

        _db.Transaction(() =>
        {
            foreach (var citadel in map.Citadels)
            {
                _db.Execute(
                    "INSERT INTO fm_plot(id, citadel_id, area_mu, monthly_rent, rented) VALUES(@id, @city, 2, 60, 0)",
                    new DbArgs().Add("@id", _ids.Next("cultivation.plot")).Add("@city", citadel.Id));
                _db.Execute(
                    "INSERT INTO fm_plot(id, citadel_id, area_mu, monthly_rent, rented) VALUES(@id, @city, 3, 90, 0)",
                    new DbArgs().Add("@id", _ids.Next("cultivation.plot")).Add("@city", citadel.Id));
            }
        });
    }

    public FarmResult Rent(long plotId, long cityId)
    {
        var plot = FindPlot(plotId);
        if (plot is null)
        {
            return FarmResult.Failure("Không có khu ruộng này.");
        }

        if (plot.CitadelId != cityId)
        {
            return FarmResult.Failure("Phải ở đúng thành mới thuê được ruộng.");
        }

        if (plot.Rented)
        {
            return FarmResult.Failure("Khu ruộng đã thuê rồi.");
        }

        if (_wallet.Liquid < plot.MonthlyRent)
        {
            return FarmResult.Failure($"Cần {plot.MonthlyRent} Quan thuê ruộng, chỉ có {_wallet.Liquid}.");
        }

        _db.Transaction(() =>
        {
            _wallet.Charge(plot.MonthlyRent, JournalCodes.FarmRent, $"Thuê ruộng khu {plot.Id} tháng đầu");
            _db.Execute("UPDATE fm_plot SET rented = 1 WHERE id=@id", DbArgs.One("@id", plotId));
        });

        return FarmResult.Success($"Thuê khu {plot.Id} ({plot.AreaMu} mẫu), {plot.MonthlyRent} Quan/tháng.");
    }

    public FarmResult Plant(long plotId, long cityId)
    {
        var plot = FindPlot(plotId);
        if (plot is null)
        {
            return FarmResult.Failure("Không có khu ruộng này.");
        }

        if (plot.CitadelId != cityId)
        {
            return FarmResult.Failure("Phải ở đúng thành mới gieo được.");
        }

        if (!plot.Rented)
        {
            return FarmResult.Failure("Chưa thuê khu ruộng này.");
        }

        if (plot.CropStatus is not null)
        {
            return FarmResult.Failure("Khu ruộng đang có cây, phải gặt xong đã.");
        }

        var seedCost = SeedCostPerMu * plot.AreaMu;
        if (_wallet.Liquid < seedCost)
        {
            return FarmResult.Failure($"Cần {seedCost} Quan tiền giống, chỉ có {_wallet.Liquid}.");
        }

        var month = _clock.Today.Month;
        var growth = CropCalendar.GrowthDays(month);
        var yieldQuantity = CropCalendar.Yield(plot.AreaMu, month);
        var plantedDay = _clock.Today.DayIndex;

        _db.Transaction(() =>
        {
            _wallet.Charge(seedCost, JournalCodes.FarmSeed, $"Giống vụ cho khu {plot.Id}");
            _db.Execute(
                """
                INSERT INTO fm_crop(id, plot_id, item_id, planted_day, ready_day, yield_qty, status)
                VALUES(@id, @plot, @item, @planted, @ready, @yield, 'GROWING')
                """,
                new DbArgs()
                    .Add("@id", _ids.Next("cultivation.crop"))
                    .Add("@plot", plotId)
                    .Add("@item", _grainItemId)
                    .Add("@planted", plantedDay)
                    .Add("@ready", plantedDay + growth)
                    .Add("@yield", yieldQuantity));
        });

        return FarmResult.Success(
            $"Gieo {plot.AreaMu} mẫu, {growth} ngày nữa chín, dự thu {yieldQuantity} gạo (giống {seedCost} Quan).");
    }

    public FarmResult Harvest(long plotId, long cityId)
    {
        var plot = FindPlot(plotId);
        if (plot is null)
        {
            return FarmResult.Failure("Không có khu ruộng này.");
        }

        if (plot.CitadelId != cityId)
        {
            return FarmResult.Failure("Phải ở đúng thành mới gặt được.");
        }

        if (plot.CropStatus != "READY")
        {
            return FarmResult.Failure(plot.CropStatus is null ? "Khu ruộng đang trống." : "Lúa chưa chín.");
        }

        var yieldQuantity = plot.YieldQuantity ?? 0;
        var cropId = _db.ScalarLong(
            "SELECT id FROM fm_crop WHERE plot_id=@plot AND status = 'READY' ORDER BY id LIMIT 1",
            DbArgs.One("@plot", plotId));

        _db.Transaction(() =>
        {
            var updated = _db.Execute(
                "UPDATE fm_crop SET status = 'HARVESTED' WHERE id=@id AND status = 'READY'",
                DbArgs.One("@id", cropId));

            if (updated == 0)
            {
                throw new SimException("cultivation.crop_race", "Crop changed while harvesting; rolled back.");
            }

            _bag.Add(cityId, _grainItemId, yieldQuantity);
        });

        _log.Info($"harvested plot {plotId}: +{yieldQuantity} grain");
        return FarmResult.Success($"Gặt được {yieldQuantity} gạo.");
    }

    /// <summary>Daily flip: a crop becomes harvestable on its ready day, exactly once.</summary>
    public void OnDay(GameDate date)
    {
        _db.Execute(
            "UPDATE fm_crop SET status = 'READY' WHERE status = 'GROWING' AND ready_day <= @day",
            DbArgs.One("@day", date.DayIndex));
    }

    /// <summary>Monthly rent for every rented plot. Unaffordable months are logged, never free.</summary>
    public void ChargeMonthlyRent(GameDate date)
    {
        var plots = _db.Query(
            "SELECT id, monthly_rent FROM fm_plot WHERE rented = 1 ORDER BY id",
            row => (Id: row.GetInt64(0), Rent: row.GetInt64(1)));

        foreach (var plot in plots)
        {
            if (_wallet.Liquid < plot.Rent)
            {
                _log.Warn($"Chưa trả được tiền ruộng {plot.Rent} Quan (khu {plot.Id}) ngày {date}.");
                continue;
            }

            _wallet.Charge(plot.Rent, JournalCodes.FarmRent, $"Tiền ruộng khu {plot.Id} tháng {date.Month:00}/{date.Year}");
        }
    }

    private PlotView? FindPlot(long plotId) => Plots.FirstOrDefault(plot => plot.Id == plotId);
}

public readonly record struct FarmResult(bool Ok, string Message)
{
    public static FarmResult Success(string message) => new(true, message);

    public static FarmResult Failure(string message) => new(false, message);
}
