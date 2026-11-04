using SimT.Contracts;
using SimT.Contracts.Data;
using SimT.Contracts.Identity;
using SimT.Contracts.Ledger;
using SimT.Contracts.Logging;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Cultivation;

/// <summary>
/// Rented land. Rent is charged on the month boundary, sowing costs seed money, the crop only
/// becomes harvestable on its ready day, and every harvest wears the soil out — so a plot is a
/// long-term commitment, not a button that prints grain.
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
    private readonly IActionTime _actionTime;
    private readonly ILog _log;

    public FarmService(
        IDbSession db,
        IGameClock clock,
        IWallet wallet,
        IBag bag,
        IItemCatalog items,
        IIdGenerator ids,
        IActionTime actionTime,
        ILog log)
    {
        _db = db;
        _clock = clock;
        _wallet = wallet;
        _bag = bag;
        _items = items;
        _ids = ids;
        _actionTime = actionTime;
        _log = log;
    }

    public IReadOnlyList<PlotView> Plots =>
        _db.Query(
            """
            SELECT p.id, p.citadel_id, p.area_mu, p.monthly_rent, p.rented, p.soil,
                   c.status, c.ready_day, c.yield_qty
            FROM fm_plot p
            LEFT JOIN fm_crop c ON c.plot_id = p.id AND c.status IN ('GROWING', 'READY')
            ORDER BY p.citadel_id, p.id
            """,
            row =>
            {
                var live = row.GetStringOrEmpty(6) is { Length: > 0 };
                return new PlotView(
                    row.GetInt64(0),
                    row.GetInt64(1),
                    row.GetInt32(2),
                    row.GetInt64(3),
                    row.GetInt64(4) != 0,
                    row.GetInt32(5),
                    live ? row.GetString(6) : null,
                    live ? row.GetInt32(7) : null,
                    live ? row.GetInt32(8) : null);
            });

    public long MonthlyRentTotal =>
        _db.ScalarLong("SELECT COALESCE(SUM(monthly_rent), 0) FROM fm_plot WHERE rented = 1");

    public int GrowingCount => (int)_db.ScalarLong("SELECT COUNT(*) FROM fm_crop WHERE status = 'GROWING'");

    public int ReadyCount => (int)_db.ScalarLong("SELECT COUNT(*) FROM fm_crop WHERE status = 'READY'");

    /// <summary>Boot-time validation: every crop must exist in the shared catalog.</summary>
    public void ValidateCrops()
    {
        foreach (var crop in CropSpec.All)
        {
            if (_items.Find(crop.OutputItemId) is null)
            {
                throw new SimException(
                    "cultivation.crop_missing",
                    $"Crop '{crop.Code}' yields item {crop.OutputItemId}, which is not in the catalog.");
            }
        }
    }

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
                InsertPlot(citadel.Id, areaMu: 2, rent: 60);
                InsertPlot(citadel.Id, areaMu: 3, rent: 90);
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
            _actionTime.Charge(ActionCodes.FarmRent);
            _wallet.Charge(plot.MonthlyRent, JournalCodes.FarmRent, $"Thuê ruộng khu {plot.Id} tháng đầu");
            _db.Execute("UPDATE fm_plot SET rented = 1 WHERE id=@id", DbArgs.One("@id", plotId));
        });

        return FarmResult.Success($"Thuê khu {plot.Id} ({plot.AreaMu} mẫu), {plot.MonthlyRent} Quan/tháng.");
    }

    public FarmResult Plant(long plotId, long cityId, string? cropCode)
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

        var crop = CropSpec.Find(cropCode);
        if (crop is null)
        {
            return FarmResult.Failure($"Không có giống '{cropCode}'.");
        }

        var seedCost = SeedCostPerMu * plot.AreaMu;
        if (_wallet.Liquid < seedCost)
        {
            return FarmResult.Failure($"Cần {seedCost} Quan tiền giống, chỉ có {_wallet.Liquid}.");
        }

        var name = _items.Find(crop.OutputItemId)?.NameVi ?? crop.Code;
        var growth = 0;
        var yieldQuantity = 0;

        // The two days spent sowing are part of the crop's schedule: charge first, then read the
        // calendar, or the harvest lands two days earlier than the player was told.
        _db.Transaction(() =>
        {
            _actionTime.Charge(ActionCodes.FarmPlant);

            var month = _clock.Today.Month;
            growth = CropCalendar.GrowthDays(crop, month);
            yieldQuantity = CropCalendar.Yield(plot.AreaMu, crop, month, plot.Soil);
            var plantedDay = _clock.Today.DayIndex;

            _wallet.Charge(seedCost, JournalCodes.FarmSeed, $"Giống {name} cho khu {plot.Id}");
            _db.Execute(
                """
                INSERT INTO fm_crop(id, plot_id, item_id, planted_day, ready_day, yield_qty, status)
                VALUES(@id, @plot, @item, @planted, @ready, @yield, 'GROWING')
                """,
                new DbArgs()
                    .Add("@id", _ids.Next("cultivation.crop"))
                    .Add("@plot", plotId)
                    .Add("@item", crop.OutputItemId)
                    .Add("@planted", plantedDay)
                    .Add("@ready", plantedDay + growth)
                    .Add("@yield", yieldQuantity));
        });

        return FarmResult.Success(
            $"Gieo {name} trên {plot.AreaMu} mẫu: {growth} ngày nữa chín, dự thu {yieldQuantity} (giống {seedCost} Quan).");
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
        var itemId = CropSpec.All.FirstOrDefault(spec =>
            _db.ScalarLong(
                "SELECT COUNT(*) FROM fm_crop WHERE plot_id=@plot AND status='READY' AND item_id=@item",
                new DbArgs().Add("@plot", plotId).Add("@item", spec.OutputItemId)) > 0)?.OutputItemId;

        if (itemId is null)
        {
            throw new SimException("cultivation.crop_unknown", $"Ready crop on plot {plotId} has no known item.");
        }

        var cropId = _db.ScalarLong(
            "SELECT id FROM fm_crop WHERE plot_id=@plot AND status = 'READY' ORDER BY id LIMIT 1",
            DbArgs.One("@plot", plotId));

        var soilAfter = Math.Max(0, plot.Soil - CropCalendar.SoilCostPerHarvest);

        _db.Transaction(() =>
        {
            _actionTime.Charge(ActionCodes.FarmHarvest);
            var updated = _db.Execute(
                "UPDATE fm_crop SET status = 'HARVESTED' WHERE id=@id AND status = 'READY'",
                DbArgs.One("@id", cropId));

            if (updated == 0)
            {
                throw new SimException("cultivation.crop_race", "Crop changed while harvesting; rolled back.");
            }

            _db.Execute("UPDATE fm_plot SET soil=@soil WHERE id=@id", new DbArgs().Add("@soil", soilAfter).Add("@id", plotId));
            _bag.Add(cityId, itemId.Value, yieldQuantity);
        });

        _log.Info($"harvested plot {plotId}: +{yieldQuantity}, soil {plot.Soil} -> {soilAfter}");
        return FarmResult.Success($"Gặt được {yieldQuantity}, đất còn {soilAfter}/100.");
    }

    public FarmResult Fertilise(long plotId, long cityId)
    {
        var plot = FindPlot(plotId);
        if (plot is null)
        {
            return FarmResult.Failure("Không có khu ruộng này.");
        }

        if (plot.CitadelId != cityId)
        {
            return FarmResult.Failure("Phải ở đúng thành mới bón được.");
        }

        if (!plot.Rented)
        {
            return FarmResult.Failure("Chưa thuê khu ruộng này.");
        }

        if (plot.Soil >= CropCalendar.SoilCeiling)
        {
            return FarmResult.Failure("Đất còn tốt, chưa cần bón.");
        }

        if (_wallet.Liquid < CropCalendar.FertiliseCost)
        {
            return FarmResult.Failure($"Cần {CropCalendar.FertiliseCost} Quan phân bón, chỉ có {_wallet.Liquid}.");
        }

        var soilAfter = Math.Min(CropCalendar.SoilCeiling, plot.Soil + CropCalendar.FertiliseGain);

        _db.Transaction(() =>
        {
            _actionTime.Charge(ActionCodes.FarmFertilise);
            _wallet.Charge(CropCalendar.FertiliseCost, JournalCodes.FarmSeed, $"Phân bón khu {plot.Id}");
            _db.Execute("UPDATE fm_plot SET soil=@soil WHERE id=@id", new DbArgs().Add("@soil", soilAfter).Add("@id", plotId));
        });

        return FarmResult.Success($"Bón phân: đất {plot.Soil} -> {soilAfter}/100 ({CropCalendar.FertiliseCost} Quan).");
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

            _wallet.Charge(
                plot.Rent,
                JournalCodes.FarmRent,
                $"Tiền ruộng khu {plot.Id} tháng {date.Month:00}/{date.Year}",
                $"cultivation.rent:{plot.Id}:{date.DayIndex}");
        }
    }

    /// <summary>Soil recovers a little each month on rented land — fallow is not wasted time.</summary>
    public void RecoverSoil(GameDate date)
    {
        _db.Execute(
            """
            UPDATE fm_plot SET soil = MIN(@ceiling, soil + @gain)
            WHERE rented = 1 AND soil < @ceiling
            """,
            new DbArgs().Add("@ceiling", CropCalendar.SoilCeiling).Add("@gain", CropCalendar.SoilRecoveryPerMonth));
    }

    private void InsertPlot(long citadelId, int areaMu, long rent) =>
        _db.Execute(
            "INSERT INTO fm_plot(id, citadel_id, area_mu, monthly_rent, rented, soil) VALUES(@id, @city, @area, @rent, 0, 100)",
            new DbArgs()
                .Add("@id", _ids.Next("cultivation.plot"))
                .Add("@city", citadelId)
                .Add("@area", areaMu)
                .Add("@rent", rent));

    private PlotView? FindPlot(long plotId) => Plots.FirstOrDefault(plot => plot.Id == plotId);
}

public readonly record struct FarmResult(bool Ok, string Message)
{
    public static FarmResult Success(string message) => new(true, message);

    public static FarmResult Failure(string message) => new(false, message);
}
