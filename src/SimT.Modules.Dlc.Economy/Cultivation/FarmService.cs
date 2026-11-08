using SimT.Contracts;
using SimT.Contracts.Data;
using SimT.Contracts.Identity;
using SimT.Contracts.Ledger;
using SimT.Contracts.Logging;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Economy;

/// <summary>
/// Rented land, multi-crop agriculture, irrigation systems, and harvest records.
/// Rent is charged on the month boundary, sowing costs seed money, the crop only
/// becomes harvestable on its ready day, and every harvest wears the soil out.
/// </summary>
internal sealed class FarmService : ICultivationEstate
{
    public const long SeedCostPerMu = 5;
    public const long IrrigationCost = 80;

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
                   c.status, c.ready_day, c.yield_qty, p.irrigation_level, p.tenant_crop_code
            FROM fm_plot p
            LEFT JOIN fm_crop c ON c.plot_id = p.id AND c.status IN ('GROWING', 'READY')
            ORDER BY p.citadel_id, p.id
            """,
            row =>
            {
                var live = row.GetStringOrEmpty(6) is { Length: > 0 };
                var tenantCrop = row.GetStringOrEmpty(10);
                return new PlotView(
                    row.GetInt64(0),
                    row.GetInt64(1),
                    row.GetInt32(2),
                    row.GetInt64(3),
                    row.GetInt64(4) != 0,
                    row.GetInt32(5),
                    live ? row.GetString(6) : null,
                    live ? row.GetInt32(7) : null,
                    live ? row.GetInt32(8) : null,
                    row.GetInt32(9),
                    string.IsNullOrWhiteSpace(tenantCrop) ? null : tenantCrop);
            });

    public IBag Bag => _bag;

    public IReadOnlyList<HarvestLogDef> HarvestLogs =>
        _db.Query(
            """
            SELECT id, plot_id, citadel_id, crop_code, yield_qty, harvest_day, soil_after
            FROM fm_harvest_log
            ORDER BY id DESC
            LIMIT 20
            """,
            row => new HarvestLogDef(
                row.GetInt64(0),
                row.GetInt64(1),
                row.GetInt64(2),
                row.GetString(3),
                row.GetInt32(4),
                row.GetInt32(5),
                row.GetInt32(6)));

    public IReadOnlyList<EstateFacilityDef> Facilities =>
        _db.Query(
            """
            SELECT id, citadel_id, facility_type, tier, built_day
            FROM fm_facility
            ORDER BY citadel_id, facility_type
            """,
            row =>
            {
                var id = row.GetInt64(0);
                var cityId = row.GetInt64(1);
                var type = row.GetString(2);
                var tier = row.GetInt32(3);
                var built = row.GetInt32(4);
                var info = FacilitySpecs.GetTierInfo(type, tier);
                var nextCost = tier < 5 ? FacilitySpecs.GetTierInfo(type, tier + 1).Cost : 0;
                return new EstateFacilityDef(id, cityId, type, tier, built, info.Name, info.TierTitle, info.Effect, nextCost, tier < 5);
            });

    public IReadOnlyList<EstateFacilityDef> GetFacilities(long citadelId)
    {
        var existing = _db.Query(
            """
            SELECT id, citadel_id, facility_type, tier, built_day
            FROM fm_facility
            WHERE citadel_id = @city
            """,
            row => (
                Id: row.GetInt64(0),
                CityId: row.GetInt64(1),
                Type: row.GetString(2),
                Tier: row.GetInt32(3),
                Built: row.GetInt32(4)
            ),
            DbArgs.One("@city", citadelId)).ToDictionary(x => x.Type, StringComparer.OrdinalIgnoreCase);

        var list = new List<EstateFacilityDef>(FacilitySpecs.AllTypes.Length);
        foreach (var type in FacilitySpecs.AllTypes)
        {
            if (existing.TryGetValue(type, out var found))
            {
                var info = FacilitySpecs.GetTierInfo(type, found.Tier);
                var nextCost = found.Tier < 5 ? FacilitySpecs.GetTierInfo(type, found.Tier + 1).Cost : 0;
                list.Add(new EstateFacilityDef(found.Id, citadelId, type, found.Tier, found.Built, info.Name, info.TierTitle, info.Effect, nextCost, found.Tier < 5));
            }
            else
            {
                var info = FacilitySpecs.GetTierInfo(type, 1);
                list.Add(new EstateFacilityDef(0, citadelId, type, 0, 0, FacilitySpecs.TypeName(type), "Chưa xây dựng", "Chưa xây dựng công trình này", info.Cost, true));
            }
        }
        return list;
    }

    public int GetFacilityTier(long citadelId, string facilityType) =>
        (int)_db.ScalarLong(
            "SELECT COALESCE(tier, 0) FROM fm_facility WHERE citadel_id = @city AND facility_type = @type",
            new DbArgs().Add("@city", citadelId).Add("@type", facilityType.ToUpperInvariant()));

    public FarmResult UpgradeFacility(long citadelId, string facilityType)
    {
        var normalizedType = facilityType.ToUpperInvariant();
        if (!FacilitySpecs.AllTypes.Contains(normalizedType))
        {
            return FarmResult.Failure($"Loại công trình '{facilityType}' không tồn tại.");
        }

        var currentTier = GetFacilityTier(citadelId, normalizedType);
        if (currentTier >= 5)
        {
            return FarmResult.Failure("Công trình này đã đạt cấp tối đa (Cấp 5 - Hóa Cảnh).");
        }

        var nextTier = currentTier + 1;
        var info = FacilitySpecs.GetTierInfo(normalizedType, nextTier);
        if (_wallet.Liquid < info.Cost)
        {
            return FarmResult.Failure($"Cần {info.Cost} Quan để nâng cấp lên {info.TierTitle}, chỉ có {_wallet.Liquid}.");
        }

        _db.Transaction(() =>
        {
            _actionTime.Charge(ActionCodes.FarmRent);
            _wallet.Charge(info.Cost, JournalCodes.FarmRent, $"Nâng cấp {info.Name} ({info.TierTitle}) tại thành {citadelId}");

            if (currentTier == 0)
            {
                _db.Execute(
                    """
                    INSERT INTO fm_facility(id, citadel_id, facility_type, tier, built_day)
                    VALUES(@id, @city, @type, @tier, @day)
                    """,
                    new DbArgs()
                        .Add("@id", _ids.Next("cultivation.facility"))
                        .Add("@city", citadelId)
                        .Add("@type", normalizedType)
                        .Add("@tier", nextTier)
                        .Add("@day", _clock.Today.DayIndex));
            }
            else
            {
                _db.Execute(
                    "UPDATE fm_facility SET tier = @tier WHERE citadel_id = @city AND facility_type = @type",
                    new DbArgs().Add("@tier", nextTier).Add("@city", citadelId).Add("@type", normalizedType));
            }

            if (normalizedType == FacilitySpecs.Irrigation)
            {
                _db.Execute(
                    "UPDATE fm_plot SET soil = MIN(100, soil + 10) WHERE citadel_id = @city AND rented = 1",
                    DbArgs.One("@city", citadelId));
            }
        });

        _log.Info($"estate facility {normalizedType} at citadel {citadelId} upgraded to tier {nextTier} ({info.TierTitle})");
        return FarmResult.Success($"Nâng cấp thành công {info.Name} lên bậc {nextTier}/5 ({info.TierTitle}): {info.Effect} (Chi phí {info.Cost} Quan).");
    }

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

            if (_items.Find(crop.SeedItemId) is null)
            {
                throw new SimException(
                    "cultivation.seed_missing",
                    $"Crop '{crop.Code}' requires seed item {crop.SeedItemId}, which is not in the catalog.");
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
                InsertPlot(citadel.Id, areaMu: 2, rent: 25);
                InsertPlot(citadel.Id, areaMu: 3, rent: 35);
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

        // 1. Tool check: Hoe required to till and sow
        if (_bag.Quantity(cityId, FarmingSupplies.HoeItemId) <= 0)
        {
            return FarmResult.Failure("Thiếu nông cụ: Cần có [Cuốc Nông Nghiệp] trong hành trang để cày xới và gieo hạt! Hãy mua cuốc tại kho điền trang hoặc chợ.");
        }

        // 2. Seed check: required seeds = plot area in mu
        var requiredSeeds = plot.AreaMu;
        var currentSeeds = _bag.Quantity(cityId, crop.SeedItemId);
        if (currentSeeds < requiredSeeds)
        {
            return FarmResult.Failure($"Thiếu hạt giống: Cần {requiredSeeds} bao {crop.SeedNameVi} cho {plot.AreaMu} mẫu ruộng (trong hành trang chỉ có {currentSeeds}). Hãy mua thêm hạt giống trước!");
        }

        var name = _items.Find(crop.OutputItemId)?.NameVi ?? crop.Code;
        var growth = 0;
        var yieldQuantity = 0;
        var irrigationTier = GetFacilityTier(plot.CitadelId, FacilitySpecs.Irrigation);

        _db.Transaction(() =>
        {
            _actionTime.Charge(ActionCodes.FarmPlant);

            var month = _clock.Today.Month;
            growth = CropCalendar.GrowthDays(crop, month, irrigationTier);
            yieldQuantity = CropCalendar.Yield(plot.AreaMu, crop, month, plot.Soil, irrigationTier);
            var plantedDay = _clock.Today.DayIndex;

            _bag.Remove(cityId, crop.SeedItemId, requiredSeeds);
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
            $"Gieo {crop.SeedNameVi} trên {plot.AreaMu} mẫu (tiêu hao {requiredSeeds} bao giống): {growth} ngày nữa chín, dự thu {yieldQuantity} {name}.");
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

        // Tool check: Sickle required to harvest crops
        if (_bag.Quantity(cityId, FarmingSupplies.SickleItemId) <= 0)
        {
            return FarmResult.Failure("Thiếu nông cụ: Cần có [Liềm Thu Hoạch] trong hành trang để gặt lúa và thu hoạch nông sản! Hãy mua liềm tại kho điền trang hoặc chợ.");
        }

        var yieldQuantity = plot.YieldQuantity ?? 0;
        var cropSpec = CropSpec.All.FirstOrDefault(spec =>
            _db.ScalarLong(
                "SELECT COUNT(*) FROM fm_crop WHERE plot_id=@plot AND status='READY' AND item_id=@item",
                new DbArgs().Add("@plot", plotId).Add("@item", spec.OutputItemId)) > 0);

        if (cropSpec is null)
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
            _bag.Add(cityId, cropSpec.OutputItemId, yieldQuantity);

            // Log harvest history
            _db.Execute(
                """
                INSERT INTO fm_harvest_log(id, plot_id, citadel_id, crop_code, yield_qty, harvest_day, soil_after)
                VALUES(@id, @plot, @citadel, @crop, @yield, @day, @soil)
                """,
                new DbArgs()
                    .Add("@id", _ids.Next("cultivation.harvest_log"))
                    .Add("@plot", plotId)
                    .Add("@citadel", cityId)
                    .Add("@crop", cropSpec.Code)
                    .Add("@yield", yieldQuantity)
                    .Add("@day", _clock.Today.DayIndex)
                    .Add("@soil", soilAfter));
        });

        _log.Info($"harvested plot {plotId}: +{yieldQuantity}, soil {plot.Soil} -> {soilAfter}");
        return FarmResult.Success($"Gặt được {yieldQuantity} {cropSpec.Code}, đất còn {soilAfter}/100.");
    }


    public FarmResult HarvestAndStore(long plotId, long cityId, IMarketWarehouse? warehouse = null)
    {
        var plot = FindPlot(plotId);
        if (plot is null)
        {
            return FarmResult.Failure("Không có khu ruộng này.");
        }

        var cropSpec = CropSpec.All.FirstOrDefault(spec =>
            _db.ScalarLong(
                "SELECT COUNT(*) FROM fm_crop WHERE plot_id=@plot AND status='READY' AND item_id=@item",
                new DbArgs().Add("@plot", plotId).Add("@item", spec.OutputItemId)) > 0);

        var yieldQuantity = plot.YieldQuantity ?? 0;
        var harvestRes = Harvest(plotId, cityId);
        if (!harvestRes.Ok)
        {
            return harvestRes;
        }

        if (warehouse != null && cropSpec != null)
        {
            var warehouseInfo = warehouse.Info(cityId);
            if (warehouseInfo != null)
            {
                var stored = warehouse.Store(cityId, cropSpec.OutputItemId, yieldQuantity, out var storeMsg);
                if (stored)
                {
                    return FarmResult.Success($"{harvestRes.Message} Đã tự động cất {yieldQuantity} nông sản vào Kho Thành Thị ({warehouseInfo.Used + yieldQuantity}/{warehouseInfo.Capacity}).");
                }
                return FarmResult.Success($"{harvestRes.Message} Nhập kho thất bại ({storeMsg}), nông sản vẫn được giữ trong hành trang.");
            }
            return FarmResult.Success($"{harvestRes.Message} (Chưa thuê kho tại thành này nên nông sản được giữ trong hành trang).");
        }

        return harvestRes;
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

        // 1. Tool check: Hoe required to till and mix fertilizer
        if (_bag.Quantity(cityId, FarmingSupplies.HoeItemId) <= 0)
        {
            return FarmResult.Failure("Thiếu nông cụ: Cần có [Cuốc Nông Nghiệp] trong hành trang để xới đất bón phân!");
        }

        // 2. Fertilizer check: at least 1 bag of fertilizer
        var currentFertilizer = _bag.Quantity(cityId, FarmingSupplies.FertilizerItemId);
        if (currentFertilizer < 1)
        {
            return FarmResult.Failure("Thiếu phân bón: Cần ít nhất 1 bao [Phân Bón Nông Nghiệp] trong hành trang để bón cải tạo đất! Hãy mua phân bón trước.");
        }

        if (plot.Soil >= CropCalendar.SoilCeiling)
        {
            return FarmResult.Failure("Đất còn tốt, chưa cần bón.");
        }

        var soilAfter = Math.Min(CropCalendar.SoilCeiling, plot.Soil + CropCalendar.FertiliseGain);

        _db.Transaction(() =>
        {
            _actionTime.Charge(ActionCodes.FarmFertilise);
            _bag.Remove(cityId, FarmingSupplies.FertilizerItemId, 1);
            _db.Execute("UPDATE fm_plot SET soil=@soil WHERE id=@id", new DbArgs().Add("@soil", soilAfter).Add("@id", plotId));
        });

        return FarmResult.Success($"Bón phân thành công: tiêu hao 1 bao phân bón, độ phì đất {plot.Soil} -> {soilAfter}/100.");
    }

    public FarmResult Irrigate(long plotId, long cityId)
    {
        var plot = FindPlot(plotId);
        if (plot is null)
        {
            return FarmResult.Failure("Không có khu ruộng này.");
        }

        if (plot.CitadelId != cityId)
        {
            return FarmResult.Failure("Phải ở đúng thành mới đào mương được.");
        }

        if (!plot.Rented)
        {
            return FarmResult.Failure("Chưa thuê khu ruộng này.");
        }

        var currentIrrigation = (int)_db.ScalarLong("SELECT COALESCE(irrigation_level, 0) FROM fm_plot WHERE id=@id", DbArgs.One("@id", plotId));
        if (currentIrrigation >= 2)
        {
            return FarmResult.Failure("Hệ thống mương máng thủy lợi khu ruộng này đã hoàn thiện (cấp tối đa).");
        }

        // Tool check: Scoop or Hoe required
        if (_bag.Quantity(cityId, FarmingSupplies.ScoopItemId) <= 0 && _bag.Quantity(cityId, FarmingSupplies.HoeItemId) <= 0)
        {
            return FarmResult.Failure("Thiếu dụng cụ: Cần có [Gầu Mương Thủy Lợi] hoặc [Cuốc Nông Nghiệp] trong hành trang để thi công mương máng.");
        }

        if (_wallet.Liquid < IrrigationCost)
        {
            return FarmResult.Failure($"Cần {IrrigationCost} Quan chi phí xây dựng thủy lợi, chỉ có {_wallet.Liquid}.");
        }

        var newLevel = currentIrrigation + 1;
        var soilBonus = Math.Min(CropCalendar.SoilCeiling, plot.Soil + 10);

        _db.Transaction(() =>
        {
            _wallet.Charge(IrrigationCost, JournalCodes.FarmRent, $"Đào mương thủy lợi khu {plot.Id}");
            _db.Execute(
                "UPDATE fm_plot SET irrigation_level = @lvl, soil = @soil WHERE id = @id",
                new DbArgs().Add("@lvl", newLevel).Add("@soil", soilBonus).Add("@id", plotId));
        });

        return FarmResult.Success($"Đào mương thủy lợi thành công cho khu {plot.Id}! Nâng cấp lên bậc {newLevel}/2 (phí {IrrigationCost} Quan).");
    }

    public FarmResult BuySeed(long cityId, string cropCode, int quantity = 1)
    {
        if (quantity <= 0) return FarmResult.Failure("Số lượng mua phải lớn hơn 0.");
        var crop = CropSpec.Find(cropCode);
        if (crop is null) return FarmResult.Failure($"Không tìm thấy loại giống '{cropCode}'.");
        var seedItem = _items.Find(crop.SeedItemId);
        if (seedItem is null) return FarmResult.Failure("Vật phẩm hạt giống không tồn tại trong danh mục.");

        var totalCost = seedItem.BasePrice * quantity;
        if (_wallet.Liquid < totalCost)
        {
            return FarmResult.Failure($"Cần {totalCost} Quan để mua {quantity} bao {seedItem.NameVi}, trong tay chỉ có {_wallet.Liquid} Quan.");
        }

        _db.Transaction(() =>
        {
            _wallet.Charge(totalCost, JournalCodes.FarmSeed, $"Mua {quantity} bao {seedItem.NameVi} tại thành {cityId}");
            _bag.Add(cityId, seedItem.Id, quantity);
        });

        _log.Info($"purchased {quantity}x {seedItem.Code} for {totalCost} quan at city {cityId}");
        return FarmResult.Success($"Đã mua {quantity} bao {seedItem.NameVi} hết {totalCost} Quan (đã cất vào hành trang).");
    }

    public FarmResult BuyFertilizer(long cityId, int quantity = 1)
    {
        if (quantity <= 0) return FarmResult.Failure("Số lượng mua phải lớn hơn 0.");
        var fertItem = _items.Find(FarmingSupplies.FertilizerItemId);
        var basePrice = fertItem?.BasePrice ?? 15;
        var totalCost = basePrice * quantity;

        if (_wallet.Liquid < totalCost)
        {
            return FarmResult.Failure($"Cần {totalCost} Quan để mua {quantity} bao Phân Bón Nông Nghiệp, chỉ có {_wallet.Liquid} Quan.");
        }

        _db.Transaction(() =>
        {
            _wallet.Charge(totalCost, JournalCodes.FarmSeed, $"Mua {quantity} bao Phân Bón Nông Nghiệp tại thành {cityId}");
            _bag.Add(cityId, FarmingSupplies.FertilizerItemId, quantity);
        });

        _log.Info($"purchased {quantity}x fertilizer for {totalCost} quan at city {cityId}");
        return FarmResult.Success($"Đã mua {quantity} bao Phân Bón Nông Nghiệp hết {totalCost} Quan (đã cất vào hành trang).");
    }

    public FarmResult BuyTool(long cityId, string toolCode)
    {
        var normalized = toolCode.ToLowerInvariant().Trim();
        var (itemId, defaultCost, defaultName) = normalized switch
        {
            "cuoc_nong" or "hoe" => (FarmingSupplies.HoeItemId, 60L, FarmingSupplies.HoeNameVi),
            "liem_gat" or "sickle" => (FarmingSupplies.SickleItemId, 50L, FarmingSupplies.SickleNameVi),
            "binh_tuoi" or "scoop" => (FarmingSupplies.ScoopItemId, 70L, FarmingSupplies.ScoopNameVi),
            _ => (0L, 0L, "")
        };

        if (itemId == 0)
        {
            return FarmResult.Failure($"Không có nông cụ '{toolCode}'.");
        }

        var item = _items.Find(itemId);
        var cost = item?.BasePrice ?? defaultCost;
        var name = item?.NameVi ?? defaultName;

        if (_wallet.Liquid < cost)
        {
            return FarmResult.Failure($"Cần {cost} Quan để mua {name}, chỉ có {_wallet.Liquid} Quan.");
        }

        _db.Transaction(() =>
        {
            _wallet.Charge(cost, JournalCodes.FarmRent, $"Mua nông cụ {name} tại thành {cityId}");
            _bag.Add(cityId, itemId, 1);
        });

        _log.Info($"purchased tool {name} for {cost} quan at city {cityId}");
        return FarmResult.Success($"Đã mua thành công [{name}] với giá {cost} Quan (đã cất vào hành trang).");
    }

    public FarmSupplyInventory GetSupplyInventory(long cityId)
    {
        return new FarmSupplyInventory(
            RiceSeeds: _bag.Quantity(cityId, CropSpec.Rice.SeedItemId),
            TeaSeeds: _bag.Quantity(cityId, CropSpec.Tea.SeedItemId),
            MulberrySeedlings: _bag.Quantity(cityId, CropSpec.Mulberry.SeedItemId),
            HerbSeeds: _bag.Quantity(cityId, CropSpec.Herb.SeedItemId),
            FertilizerCount: _bag.Quantity(cityId, FarmingSupplies.FertilizerItemId),
            HasHoe: _bag.Quantity(cityId, FarmingSupplies.HoeItemId) > 0,
            HasSickle: _bag.Quantity(cityId, FarmingSupplies.SickleItemId) > 0,
            HasWateringScoop: _bag.Quantity(cityId, FarmingSupplies.ScoopItemId) > 0);
    }

    public FarmResult SetTenantCrop(long plotId, long cityId, string? cropCode)
    {
        var plot = FindPlot(plotId);
        if (plot is null)
        {
            return FarmResult.Failure("Không có khu ruộng này.");
        }

        if (plot.CitadelId != cityId)
        {
            return FarmResult.Failure("Phải ở đúng thành mới giao khoán tá điền được.");
        }

        if (!plot.Rented)
        {
            return FarmResult.Failure("Chưa thuê khu ruộng này.");
        }

        if (string.IsNullOrWhiteSpace(cropCode) ||
            cropCode.Equals("off", StringComparison.OrdinalIgnoreCase) ||
            cropCode.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            _db.Execute("UPDATE fm_plot SET tenant_crop_code = NULL WHERE id = @id", DbArgs.One("@id", plotId));
            return FarmResult.Success($"Đã thôi giao khoán tá điền tại khu ruộng {plotId}.");
        }

        var crop = CropSpec.Find(cropCode);
        if (crop is null)
        {
            return FarmResult.Failure($"Không có giống '{cropCode}' để giao tá điền.");
        }

        _db.Execute(
            "UPDATE fm_plot SET tenant_crop_code = @crop WHERE id = @id",
            new DbArgs().Add("@crop", crop.Code).Add("@id", plotId));

        if (plot.CropStatus is null)
        {
            TryTenantPlant(plot, crop, _clock.Today);
        }

        return FarmResult.Success(
            $"Tá điền đã nhận khoán tự canh [{crop.SeedNameVi}] tại khu {plotId} (Chủ điền hưởng 70% sản lượng, tự động gặt & gieo vụ mới).");
    }

    private void TryTenantPlant(PlotView plot, CropSpec crop, GameDate today)
    {
        var requiredSeeds = plot.AreaMu;
        var heldSeeds = _bag.Quantity(plot.CitadelId, crop.SeedItemId);
        if (heldSeeds >= requiredSeeds)
        {
            _bag.Remove(plot.CitadelId, crop.SeedItemId, requiredSeeds);
        }
        else
        {
            var seedItem = _items.Find(crop.SeedItemId);
            var unitPrice = seedItem?.BasePrice ?? SeedCostPerMu;
            var neededToBuy = requiredSeeds - heldSeeds;
            var buyCost = neededToBuy * unitPrice;
            if (_wallet.Liquid < buyCost)
            {
                return;
            }
            if (heldSeeds > 0)
            {
                _bag.Remove(plot.CitadelId, crop.SeedItemId, heldSeeds);
            }
            _wallet.Charge(buyCost, JournalCodes.FarmSeed, $"Tá điền mua giống {crop.SeedNameVi} khu {plot.Id}");
        }

        var irrigationTier = GetFacilityTier(plot.CitadelId, FacilitySpecs.Irrigation);
        var growth = CropCalendar.GrowthDays(crop, today.Month, irrigationTier);
        var yieldQuantity = CropCalendar.Yield(plot.AreaMu, crop, today.Month, plot.Soil, irrigationTier);

        _db.Execute(
            """
            INSERT INTO fm_crop(id, plot_id, item_id, planted_day, ready_day, yield_qty, status)
            VALUES(@id, @plot, @item, @planted, @ready, @yield, 'GROWING')
            """,
            new DbArgs()
                .Add("@id", _ids.Next("cultivation.crop"))
                .Add("@plot", plot.Id)
                .Add("@item", crop.OutputItemId)
                .Add("@planted", today.DayIndex)
                .Add("@ready", today.DayIndex + growth)
                .Add("@yield", yieldQuantity));
    }

    /// <summary>Daily flip: a crop becomes harvestable on its ready day, and tenant farmers auto-harvest/replant.</summary>
    public void OnDay(GameDate date)
    {
        _db.Execute(
            "UPDATE fm_crop SET status = 'READY' WHERE status = 'GROWING' AND ready_day <= @day",
            DbArgs.One("@day", date.DayIndex));

        var tenantPlots = Plots.Where(p => p.Rented && !string.IsNullOrWhiteSpace(p.TenantCropCode)).ToList();
        foreach (var plot in tenantPlots)
        {
            var cropSpec = CropSpec.Find(plot.TenantCropCode);
            if (cropSpec is null) continue;

            if (plot.CropStatus == "READY")
            {
                var cropId = _db.ScalarLong(
                    "SELECT id FROM fm_crop WHERE plot_id=@plot AND status = 'READY' ORDER BY id LIMIT 1",
                    DbArgs.One("@plot", plot.Id));
                if (cropId > 0)
                {
                    var fullYield = plot.YieldQuantity ?? 0;
                    var landownerShare = Math.Max(1, (fullYield * 70) / 100);
                    var soilAfter = Math.Max(0, plot.Soil - CropCalendar.SoilCostPerHarvest);

                    // Auto-fertilize if soil drops below 40 and fertilizer is available in bag
                    if (soilAfter < 40 && _bag.Quantity(plot.CitadelId, FarmingSupplies.FertilizerItemId) >= 1)
                    {
                        _bag.Remove(plot.CitadelId, FarmingSupplies.FertilizerItemId, 1);
                        soilAfter = Math.Min(CropCalendar.SoilCeiling, soilAfter + CropCalendar.FertiliseGain);
                    }

                    _db.Execute("UPDATE fm_crop SET status = 'HARVESTED' WHERE id = @id", DbArgs.One("@id", cropId));
                    _db.Execute("UPDATE fm_plot SET soil = @soil WHERE id = @id", new DbArgs().Add("@soil", soilAfter).Add("@id", plot.Id));
                    _bag.Add(plot.CitadelId, cropSpec.OutputItemId, landownerShare);

                    _db.Execute(
                        """
                        INSERT INTO fm_harvest_log(id, plot_id, citadel_id, crop_code, yield_qty, harvest_day, soil_after)
                        VALUES(@id, @plot, @citadel, @crop, @yield, @day, @soil)
                        """,
                        new DbArgs()
                            .Add("@id", _ids.Next("cultivation.harvest_log"))
                            .Add("@plot", plot.Id)
                            .Add("@citadel", plot.CitadelId)
                            .Add("@crop", cropSpec.Code)
                            .Add("@yield", landownerShare)
                            .Add("@day", date.DayIndex)
                            .Add("@soil", soilAfter));

                    var refreshed = FindPlot(plot.Id);
                    if (refreshed is not null && refreshed.CropStatus is null)
                    {
                        TryTenantPlant(refreshed, cropSpec, date);
                    }
                }
            }
            else if (plot.CropStatus is null)
            {
                TryTenantPlant(plot, cropSpec, date);
            }
        }
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

        // Passive stipend from BREWERY tier 5 (Quỳnh Tương Ngọc Lộ Cung)
        var breweryT5 = _db.ScalarLong("SELECT COUNT(*) FROM fm_facility WHERE facility_type = 'BREWERY' AND tier >= 5");
        if (breweryT5 > 0)
        {
            _wallet.Pay(
                100 * breweryT5,
                JournalCodes.FarmRent,
                "Bổng lộc danh tửu Quỳnh Tương Ngọc Lộ Cung",
                $"cultivation.brewery_stipend:{date.DayIndex}");
        }
    }

    /// <summary>Soil recovers a little each month on rented land — fallow is not wasted time.</summary>
    public void RecoverSoil(GameDate date)
    {
        _db.Execute(
            """
            UPDATE fm_plot SET soil = MIN(@ceiling, soil + @gain + (irrigation_level * 2))
            WHERE rented = 1 AND soil < @ceiling
            """,
            new DbArgs().Add("@ceiling", CropCalendar.SoilCeiling).Add("@gain", CropCalendar.SoilRecoveryPerMonth));

        // Additional recovery from IRRIGATION facilities in each citadel
        var irrigationTiers = _db.Query(
            "SELECT citadel_id, tier FROM fm_facility WHERE facility_type = 'IRRIGATION' AND tier > 0",
            row => (CityId: row.GetInt64(0), Tier: row.GetInt32(1)));

        foreach (var (cityId, tier) in irrigationTiers)
        {
            var extra = tier * 2;
            var minSoil = tier >= 5 ? 80 : 0;
            _db.Execute(
                """
                UPDATE fm_plot SET soil = MAX(@minSoil, MIN(@ceiling, soil + @extra))
                WHERE citadel_id = @city AND rented = 1
                """,
                new DbArgs()
                    .Add("@ceiling", CropCalendar.SoilCeiling)
                    .Add("@extra", extra)
                    .Add("@minSoil", minSoil)
                    .Add("@city", cityId));
        }
    }

    private void InsertPlot(long citadelId, int areaMu, long rent) =>
        _db.Execute(
            "INSERT INTO fm_plot(id, citadel_id, area_mu, monthly_rent, rented, soil, irrigation_level) VALUES(@id, @city, @area, @rent, 0, 100, 0)",
            new DbArgs()
                .Add("@id", _ids.Next("cultivation.plot"))
                .Add("@city", citadelId)
                .Add("@area", areaMu)
                .Add("@rent", rent));

    private PlotView? FindPlot(long plotId) => Plots.FirstOrDefault(plot => plot.Id == plotId);
}
