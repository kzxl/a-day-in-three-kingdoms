using SimT.Contracts;
using SimT.Contracts.Data;
using SimT.Contracts.Ledger;
using SimT.Contracts.Logging;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Economy;

/// <summary>
/// Day labour, trades, equipment crafting, workshop automation, and guild commissions.
/// Working a trade raises the skill tier, unlocking recipes and boosting output.
/// </summary>
internal sealed class ProfessionService : IProfessionGuild, IPlayerEquipment
{
    public const int PracticesPerTier = 4;
    public const int MaxTier = 5;
    public const long WorkshopOpenCost = 120;
    public const long WorkshopBaseRent = 25;
    public const long ArtisanHireCost = 20;
    public const long ArtisanMonthlySalary = 15;

    private readonly IDbSession _db;
    private readonly IGameClock _clock;
    private readonly IWallet _wallet;
    private readonly IBag _bag;
    private readonly IItemCatalog _items;
    private readonly ILog _log;
    private readonly IPlayerAttributes? _attributes;
    private readonly IWorldMap? _map;
    private readonly INpcRegistry? _npcs;
    private readonly ISocialNetwork? _social;
    private readonly ISocietyChronicle? _chronicle;

    public ProfessionService(
        IDbSession db,
        IGameClock clock,
        IWallet wallet,
        IBag bag,
        IItemCatalog items,
        ILog log,
        IPlayerAttributes? attributes = null,
        IWorldMap? map = null,
        INpcRegistry? npcs = null,
        ISocialNetwork? social = null,
        ISocietyChronicle? chronicle = null)
    {
        _db = db;
        _clock = clock;
        _wallet = wallet;
        _bag = bag;
        _items = items;
        _log = log;
        _attributes = attributes;
        _map = map;
        _npcs = npcs;
        _social = social;
        _chronicle = chronicle;
        Trades = LoadTrades(db);
    }

    public IReadOnlyList<TradeDefinition> Trades { get; }

    public IReadOnlyList<RecipeDef> Recipes =>
        _db.Query(
            """
            SELECT id, trade_id, name_vi, required_tier, material_item_id, material_quantity, secondary_item_id, secondary_quantity, output_item_id, output_quantity, craft_days, craft_fee
            FROM pf_recipe
            ORDER BY id ASC
            """,
            row => new RecipeDef(
                row.GetInt64(0),
                row.GetInt64(1),
                row.GetString(2),
                row.GetInt32(3),
                row.GetInt64(4),
                row.GetInt32(5),
                row.IsDbNull(6) ? null : row.GetInt64(6),
                row.GetInt32(7),
                row.GetInt64(8),
                row.GetInt32(9),
                row.GetInt32(10),
                row.GetInt64(11)));

    public IReadOnlyList<EquipmentDef> Equipments(long citadelId) =>
        _db.Query(
            """
            SELECT id, name_vi, item_id, citadel_id, durability, max_durability, quality_rank, bonus_stat, bonus_value, COALESCE(temper_level, 0)
            FROM pf_equipment
            WHERE citadel_id = @citadel
            ORDER BY id ASC
            """,
            row => new EquipmentDef(
                row.GetInt64(0),
                row.GetString(1),
                row.GetInt64(2),
                row.GetInt64(3),
                row.GetInt32(4),
                row.GetInt32(5),
                row.GetString(6),
                row.GetString(7),
                row.GetInt32(8),
                row.GetInt32(9)),
            DbArgs.One("@citadel", citadelId));

    public EquipmentDef? FindEquipment(long equipmentId) =>
        _db.QueryOne(
            """
            SELECT id, name_vi, item_id, citadel_id, durability, max_durability, quality_rank, bonus_stat, bonus_value, COALESCE(temper_level, 0)
            FROM pf_equipment
            WHERE id = @id
            """,
            row => new EquipmentDef(
                row.GetInt64(0),
                row.GetString(1),
                row.GetInt64(2),
                row.GetInt64(3),
                row.GetInt32(4),
                row.GetInt32(5),
                row.GetString(6),
                row.GetString(7),
                row.GetInt32(8),
                row.GetInt32(9)),
            DbArgs.One("@id", equipmentId));

    public void ValidateCatalog()
    {
        foreach (var trade in Trades)
        {
            if (_items.Find(trade.OutputItemId) is null)
            {
                throw new SimException(
                    "professions.output_missing",
                    $"Trade '{trade.Code}' yields item {trade.OutputItemId}, which is not in the catalog.");
            }
        }
    }

    public int Tier(long tradeId) =>
        (int)_db.ScalarLong("SELECT COALESCE(tier, 0) FROM pf_skill WHERE trade_id=@trade", DbArgs.One("@trade", tradeId));

    public int PracticeCount(long tradeId) =>
        (int)_db.ScalarLong("SELECT COALESCE(practice_count, 0) FROM pf_skill WHERE trade_id=@trade", DbArgs.One("@trade", tradeId));

    public string GetCraftsmanRank(long tradeId) => Tier(tradeId) switch
    {
        0 => "Học Đồ",
        1 => "Thợ Phụ",
        2 => "Thợ Nghề",
        3 => "Thợ Cả",
        4 => "Đại Sư",
        _ => "Tông Sư"
    };

    public int TotalGuildFame =>
        (int)_db.ScalarLong("SELECT COALESCE((SELECT total_fame FROM pf_guild_fame WHERE id = 1), 0)");

    public WorkResult Work(long cityId, long tradeId)
    {
        var trade = Trades.FirstOrDefault(candidate => candidate.Id == tradeId);
        if (trade is null)
        {
            return WorkResult.Failure("Nghề này không tồn tại.");
        }

        if (_wallet.Liquid < trade.ToolFee)
        {
            return WorkResult.Failure($"Cần {trade.ToolFee} Quan tiền dụng cụ, chỉ có {_wallet.Liquid}.");
        }

        var output = _items.Find(trade.OutputItemId);
        if (output is null)
        {
            throw new SimException("professions.output_missing", $"Trade '{trade.Code}' has no catalog entry.");
        }

        var tierBefore = Tier(tradeId);
        var perkBonus = HasPerk("xao_doat_thien_cong") ? 1 : 0;
        var quantity = trade.BaseQuantity + tierBefore + perkBonus;
        var daysCost = HasPerk("nhat_tam_nhi_dung") ? Math.Max(1, trade.DaysPerBatch - 1) : trade.DaysPerBatch;
        var tierAfter = 0;

        _db.Transaction(() =>
        {
            _wallet.Charge(trade.ToolFee, JournalCodes.ToolFee, $"Dụng cụ nghề {trade.NameVi}");
            _clock.Advance(daysCost);
            _bag.Add(cityId, trade.OutputItemId, quantity);

            var practice = PracticeCount(tradeId) + 1;
            tierAfter = Math.Min(MaxTier, practice / PracticesPerTier);

            _db.Execute(
                """
                INSERT INTO pf_skill(trade_id, practice_count, tier) VALUES(@trade, @practice, @tier)
                ON CONFLICT(trade_id) DO UPDATE SET practice_count = excluded.practice_count, tier = excluded.tier
                """,
                new DbArgs().Add("@trade", tradeId).Add("@practice", practice).Add("@tier", tierAfter));

            _attributes?.AddExp("CRA", 15);
            var skillToTrain = trade.Code switch
            {
                "nong_phu" or "canh_nong" => "nong_canh",
                "tho_moc" => "moc_nghe",
                "luyen_kim" or "duc_dong" => "che_tac",
                "tho_may" or "tho_det" or "det_gam" => "det_may",
                "hai_tra" or "tam_tang" => "nong_canh",
                "ngu_phu" => "dieu_ngu",
                "hai_thuoc" or "tam_tram" => "tam_duoc",
                "khai_khoang" or "lam_muoi" => "khai_khoang",
                "nau_ruou" => "u_men",
                _ => "che_tac"
            };
            _attributes?.PracticeSkill(skillToTrain, 20);
        });

        _log.Info($"worked {trade.Code}: +{quantity} {output.Code}, tier {tierBefore} -> {tierAfter}");

        var tierNote = tierAfter > tierBefore ? $", tay nghề thăng lên '{GetCraftsmanRank(tradeId)}' (bậc {tierAfter})" : string.Empty;
        return WorkResult.Success(
            $"{trade.DaysPerBatch} ngày làm {trade.NameVi}: +{quantity} {output.NameVi} (phí {trade.ToolFee} Quan){tierNote}.",
            quantity,
            trade.DaysPerBatch);
    }

    public WorkResult Craft(long cityId, long recipeId)
    {
        var recipe = Recipes.FirstOrDefault(r => r.Id == recipeId);
        if (recipe is null)
        {
            return WorkResult.Failure("Bản vẽ chế tác không tồn tại.");
        }

        var currentTier = Tier(recipe.TradeId);
        if (currentTier < recipe.RequiredTier)
        {
            return WorkResult.Failure($"Chưa đủ tay nghề để chế tác (cần bậc {recipe.RequiredTier}, hiện có bậc {currentTier}).");
        }

        if (_wallet.Liquid < recipe.CraftFee)
        {
            return WorkResult.Failure($"Cần {recipe.CraftFee} Quan chi phí chế tác, chỉ có {_wallet.Liquid}.");
        }

        var matQty = _bag.Quantity(cityId, recipe.MaterialItemId);
        if (matQty < recipe.MaterialQuantity)
        {
            var matName = _items.Find(recipe.MaterialItemId)?.NameVi ?? "nguyên liệu";
            return WorkResult.Failure($"Thiếu nguyên liệu chính: cần {recipe.MaterialQuantity} {matName}, chỉ có {matQty}.");
        }

        if (recipe.SecondaryItemId.HasValue && recipe.SecondaryQuantity > 0)
        {
            var secQty = _bag.Quantity(cityId, recipe.SecondaryItemId.Value);
            if (secQty < recipe.SecondaryQuantity)
            {
                var secName = _items.Find(recipe.SecondaryItemId.Value)?.NameVi ?? "nguyên liệu phụ";
                return WorkResult.Failure($"Thiếu nguyên liệu phụ: cần {recipe.SecondaryQuantity} {secName}, chỉ có {secQty}.");
            }
        }

        var output = _items.Find(recipe.OutputItemId);
        var outputName = output?.NameVi ?? recipe.NameVi;

        // Workshop furnace quality boost
        var ws = GetWorkshop(cityId);
        var furnaceBonus = ws != null && ws.IsActive ? ws.FurnaceLevel * 0.05 : 0.0;

        _db.Transaction(() =>
        {
            _wallet.Charge(recipe.CraftFee, JournalCodes.ToolFee, $"Chi phí chế tác {recipe.NameVi}");
            var craftDays = HasPerk("nhat_tam_nhi_dung") ? Math.Max(1, recipe.CraftDays - 1) : recipe.CraftDays;
            _clock.Advance(craftDays);

            _bag.Remove(cityId, recipe.MaterialItemId, recipe.MaterialQuantity);
            if (recipe.SecondaryItemId.HasValue && recipe.SecondaryQuantity > 0)
            {
                _bag.Remove(cityId, recipe.SecondaryItemId.Value, recipe.SecondaryQuantity);
            }

            _bag.Add(cityId, recipe.OutputItemId, recipe.OutputQuantity);

            // Grant practice
            var practice = PracticeCount(recipe.TradeId) + 2;
            var tierAfter = Math.Min(MaxTier, practice / PracticesPerTier);
            _db.Execute(
                """
                INSERT INTO pf_skill(trade_id, practice_count, tier) VALUES(@trade, @practice, @tier)
                ON CONFLICT(trade_id) DO UPDATE SET practice_count = excluded.practice_count, tier = excluded.tier
                """,
                new DbArgs().Add("@trade", recipe.TradeId).Add("@practice", practice).Add("@tier", tierAfter));

            _attributes?.AddExp("CRA", 25);

            var playerTier = Tier(recipe.TradeId);
            var seed = (int)(recipe.Id * 37 + playerTier * 19 + _clock.Today.DayIndex);
            var rng = new Random(seed);
            var perkAlchDiscount = HasPerk("dan_dao_thong_than") ? 0.10 : 0.0;
            var roll = Math.Max(0.0, rng.NextDouble() - furnaceBonus - perkAlchDiscount);

            string qualityRank;
            int bonusValue;
            int maxDura;

            if (playerTier >= 4)
            {
                if (roll < 0.35)
                {
                    qualityRank = "LEGENDARY";
                    bonusValue = 10;
                    maxDura = 150;
                }
                else if (roll < 0.75)
                {
                    qualityRank = "MASTER";
                    bonusValue = 6;
                    maxDura = 125;
                }
                else
                {
                    qualityRank = "FINE";
                    bonusValue = 4;
                    maxDura = 100;
                }
            }
            else if (playerTier >= 2)
            {
                if (roll < 0.15)
                {
                    qualityRank = "LEGENDARY";
                    bonusValue = 8;
                    maxDura = 140;
                }
                else if (roll < 0.50)
                {
                    qualityRank = "MASTER";
                    bonusValue = 5;
                    maxDura = 120;
                }
                else if (roll < 0.85)
                {
                    qualityRank = "FINE";
                    bonusValue = 3;
                    maxDura = 100;
                }
                else
                {
                    qualityRank = "COMMON";
                    bonusValue = 2;
                    maxDura = 100;
                }
            }
            else
            {
                if (roll < 0.20)
                {
                    qualityRank = "MASTER";
                    bonusValue = 4;
                    maxDura = 110;
                }
                else if (roll < 0.70)
                {
                    qualityRank = "FINE";
                    bonusValue = 3;
                    maxDura = 100;
                }
                else
                {
                    qualityRank = "COMMON";
                    bonusValue = 2;
                    maxDura = 100;
                }
            }

            if (HasPerk("thiet_cot_bach_luyen"))
            {
                maxDura += 25;
            }

            var bonusStat = recipe.TradeId switch
            {
                4 => "WAR", // Luyện kim / Rèn
                5 => "HEA", // Dược sư
                7 => "DIP", // Nấu rượu
                9 => "COM", // Dệt gấm
                16 => "COM", // Đúc đồng
                3 => recipe.NameVi.Contains("Nỗ") || recipe.NameVi.Contains("Cung") ? "WAR" : "COM",
                _ => "POL"  // Nông phu, Ngư dân
            };

            _db.Execute(
                """
                INSERT INTO pf_equipment(name_vi, item_id, citadel_id, durability, max_durability, quality_rank, bonus_stat, bonus_value)
                VALUES(@name, @item, @citadel, @dura, @dura, @quality, @stat, @bonus)
                """,
                new DbArgs()
                    .Add("@name", recipe.NameVi)
                    .Add("@item", recipe.OutputItemId)
                    .Add("@citadel", cityId)
                    .Add("@dura", maxDura)
                    .Add("@quality", qualityRank)
                    .Add("@stat", bonusStat)
                    .Add("@bonus", bonusValue));
        });

        return WorkResult.Success(
            $"Chế tác thành công {recipe.NameVi}! Thu được {recipe.OutputQuantity} {outputName} sau {recipe.CraftDays} ngày.",
            recipe.OutputQuantity,
            recipe.CraftDays);
    }

    public WorkResult Repair(long cityId, long equipmentId)
    {
        var equip = Equipments(cityId).FirstOrDefault(e => e.Id == equipmentId);
        if (equip is null)
        {
            equip = FindEquipment(equipmentId);
        }

        if (equip is null)
        {
            return WorkResult.Failure("Không tìm thấy trang bị.");
        }

        if (equip.Durability >= equip.MaxDurability)
        {
            return WorkResult.Failure("Trang bị này còn nguyên vẹn, không cần tu sửa.");
        }

        const int repairCost = 15;
        if (_wallet.Liquid < repairCost)
        {
            return WorkResult.Failure($"Cần {repairCost} Quan tiền công tu sửa, chỉ có {_wallet.Liquid}.");
        }

        _db.Transaction(() =>
        {
            _wallet.Charge(repairCost, JournalCodes.ToolFee, $"Tu sửa {equip.NameVi}");
            _db.Execute(
                "UPDATE pf_equipment SET durability = max_durability WHERE id = @id",
                DbArgs.One("@id", equipmentId));
        });

        return WorkResult.Success($"Đã tu sửa {equip.NameVi} phục hồi toàn bộ độ bền {equip.MaxDurability}/{equip.MaxDurability}.", 1, 0);
    }

    // =========================================================================
    // WORKSHOP AUTOMATION & ATELIER MANAGEMENT
    // =========================================================================

    public WorkshopDef? GetWorkshop(long citadelId) =>
        _db.QueryOne(
            """
            SELECT id, citadel_id, trade_id, name_vi, monthly_rent, furnace_level, hired_artisans, is_active, created_day_index
            FROM pf_workshop
            WHERE citadel_id = @citadel
            """,
            row => new WorkshopDef(
                row.GetInt64(0),
                row.GetInt64(1),
                row.GetInt64(2),
                row.GetString(3),
                row.GetInt64(4),
                row.GetInt32(5),
                row.GetInt32(6),
                row.GetInt64(7) != 0,
                row.GetInt32(8)),
            DbArgs.One("@citadel", citadelId));

    public IReadOnlyList<WorkshopDef> AllWorkshops() =>
        _db.Query(
            """
            SELECT id, citadel_id, trade_id, name_vi, monthly_rent, furnace_level, hired_artisans, is_active, created_day_index
            FROM pf_workshop
            ORDER BY citadel_id ASC
            """,
            row => new WorkshopDef(
                row.GetInt64(0),
                row.GetInt64(1),
                row.GetInt64(2),
                row.GetString(3),
                row.GetInt64(4),
                row.GetInt32(5),
                row.GetInt32(6),
                row.GetInt64(7) != 0,
                row.GetInt32(8)));

    public WorkResult OpenWorkshop(long citadelId, long tradeId)
    {
        var existing = GetWorkshop(citadelId);
        if (existing is not null)
        {
            return WorkResult.Failure("Đã có xưởng chế tác tại thành trì này.");
        }

        var trade = Trades.FirstOrDefault(t => t.Id == tradeId);
        if (trade is null)
        {
            return WorkResult.Failure("Nghề nghiệp không hợp lệ.");
        }

        if (_wallet.Liquid < WorkshopOpenCost)
        {
            return WorkResult.Failure($"Cần {WorkshopOpenCost} Quan tiền thuê mặt bằng mở xưởng, chỉ có {_wallet.Liquid}.");
        }

        var workshopName = $"Phường Nghề {trade.NameVi}";
        var day = _clock.Today.DayIndex;

        _db.Transaction(() =>
        {
            _wallet.Charge(WorkshopOpenCost, JournalCodes.ToolFee, $"Mở {workshopName}");
            _db.Execute(
                """
                INSERT INTO pf_workshop(citadel_id, trade_id, name_vi, monthly_rent, furnace_level, hired_artisans, is_active, created_day_index)
                VALUES(@citadel, @trade, @name, @rent, 1, 0, 1, @day)
                """,
                new DbArgs()
                    .Add("@citadel", citadelId)
                    .Add("@trade", tradeId)
                    .Add("@name", workshopName)
                    .Add("@rent", WorkshopBaseRent)
                    .Add("@day", day));
        });

        return WorkResult.Success($"Mở thành công {workshopName} tại thành! Tiền thuê cơ sở {WorkshopBaseRent} Quan/tháng.", 1, 0);
    }

    public WorkResult HireArtisan(long citadelId)
    {
        var ws = GetWorkshop(citadelId);
        if (ws is null)
        {
            return WorkResult.Failure("Chưa có xưởng chế tác tại thành này.");
        }

        if (ws.HiredArtisans >= 5)
        {
            return WorkResult.Failure("Xưởng đã đạt giới hạn thợ phụ tối đa (5 người).");
        }

        if (_wallet.Liquid < ArtisanHireCost)
        {
            return WorkResult.Failure($"Cần {ArtisanHireCost} Quan tiền chiêu mộ thợ phụ, chỉ có {_wallet.Liquid}.");
        }

        _db.Transaction(() =>
        {
            _wallet.Charge(ArtisanHireCost, JournalCodes.ToolFee, "Chiêu mộ thợ phụ gia công");
            _db.Execute(
                "UPDATE pf_workshop SET hired_artisans = hired_artisans + 1 WHERE citadel_id = @citadel",
                DbArgs.One("@citadel", citadelId));
        });

        return WorkResult.Success($"Đã tuyển thêm 1 thợ phụ (hiện có {ws.HiredArtisans + 1} người). Lương tháng {ArtisanMonthlySalary} Quan/người.", 1, 0);
    }

    public WorkResult DismissArtisan(long citadelId)
    {
        var ws = GetWorkshop(citadelId);
        if (ws is null || ws.HiredArtisans <= 0)
        {
            return WorkResult.Failure("Không có thợ phụ nào để cho nghỉ việc.");
        }

        _db.Transaction(() =>
        {
            _db.Execute(
                "UPDATE pf_workshop SET hired_artisans = hired_artisans - 1 WHERE citadel_id = @citadel",
                DbArgs.One("@citadel", citadelId));
        });

        return WorkResult.Success($"Đã cho 1 thợ phụ thôi việc. Còn lại {ws.HiredArtisans - 1} người.", 1, 0);
    }

    public WorkResult UpgradeFurnace(long citadelId)
    {
        var ws = GetWorkshop(citadelId);
        if (ws is null)
        {
            return WorkResult.Failure("Chưa có xưởng chế tác tại thành này.");
        }

        if (ws.FurnaceLevel >= 5)
        {
            return WorkResult.Failure("Lò rèn/cơ sở vật chất xưởng đã đạt cấp tối đa (cấp 5).");
        }

        var cost = ws.FurnaceLevel * 80;
        if (_wallet.Liquid < cost)
        {
            return WorkResult.Failure($"Cần {cost} Quan để nâng cấp cơ sở xưởng, chỉ có {_wallet.Liquid}.");
        }

        _db.Transaction(() =>
        {
            _wallet.Charge(cost, JournalCodes.ToolFee, "Nâng cấp cơ sở vật chất xưởng");
            _db.Execute(
                "UPDATE pf_workshop SET furnace_level = furnace_level + 1 WHERE citadel_id = @citadel",
                DbArgs.One("@citadel", citadelId));
        });

        return WorkResult.Success($"Nâng cấp cơ sở xưởng lên cấp {ws.FurnaceLevel + 1}! Tăng mạnh tỷ lệ tạo tác Thần Binh/Tuyệt Phẩm.", 1, 0);
    }

    // =========================================================================
    // GUILD COMMISSIONS & CRAFTING ORDERS
    // =========================================================================

    public IReadOnlyList<CraftOrderDef> AvailableOrders(long citadelId) =>
        _db.Query(
            """
            SELECT id, client_name_vi, client_type, citadel_id, target_item_id, quantity_needed, reward_copper, fame_reward, deadline_day, status, accepted_day
            FROM pf_order
            WHERE citadel_id = @citadel AND status = 'AVAILABLE' AND deadline_day >= @today
            ORDER BY id ASC
            """,
            row => new CraftOrderDef(
                row.GetInt64(0),
                row.GetString(1),
                row.GetString(2),
                row.GetInt64(3),
                row.GetInt64(4),
                row.GetInt32(5),
                row.GetInt32(6),
                row.GetInt32(7),
                row.GetInt32(8),
                row.GetString(9),
                row.IsDbNull(10) ? null : row.GetInt32(10)),
            new DbArgs().Add("@citadel", citadelId).Add("@today", _clock.Today.DayIndex));

    public IReadOnlyList<CraftOrderDef> ActiveOrders() =>
        _db.Query(
            """
            SELECT id, client_name_vi, client_type, citadel_id, target_item_id, quantity_needed, reward_copper, fame_reward, deadline_day, status, accepted_day
            FROM pf_order
            WHERE status = 'ACCEPTED' AND deadline_day >= @today
            ORDER BY deadline_day ASC
            """,
            row => new CraftOrderDef(
                row.GetInt64(0),
                row.GetString(1),
                row.GetString(2),
                row.GetInt64(3),
                row.GetInt64(4),
                row.GetInt32(5),
                row.GetInt32(6),
                row.GetInt32(7),
                row.GetInt32(8),
                row.GetString(9),
                row.IsDbNull(10) ? null : row.GetInt32(10)),
            DbArgs.One("@today", _clock.Today.DayIndex));

    private sealed record OrderAcceptInfo(long Id, string Status, string Name);

    public WorkResult AcceptOrder(long orderId)
    {
        var order = _db.QueryOne(
            "SELECT id, status, client_name_vi FROM pf_order WHERE id = @id",
            row => new OrderAcceptInfo(row.GetInt64(0), row.GetString(1), row.GetString(2)),
            DbArgs.One("@id", orderId));

        if (order is null)
        {
            return WorkResult.Failure("Không tìm thấy đơn đặt hàng.");
        }

        if (order.Status != "AVAILABLE")
        {
            return WorkResult.Failure("Đơn hàng này không ở trạng thái có thể tiếp nhận.");
        }

        _db.Transaction(() =>
        {
            _db.Execute(
                "UPDATE pf_order SET status = 'ACCEPTED', accepted_day = @day WHERE id = @id",
                new DbArgs().Add("@day", _clock.Today.DayIndex).Add("@id", orderId));
        });

        return WorkResult.Success($"Đã tiếp nhận đơn đặt hàng từ {order.Name}.", 1, 0);
    }

    public WorkResult FulfillOrder(long citadelId, long orderId)
    {
        var order = _db.QueryOne(
            """
            SELECT id, client_name_vi, target_item_id, quantity_needed, reward_copper, fame_reward, status, citadel_id
            FROM pf_order
            WHERE id = @id
            """,
            row => new
            {
                Id = row.GetInt64(0),
                ClientName = row.GetString(1),
                TargetItemId = row.GetInt64(2),
                QuantityNeeded = row.GetInt32(3),
                RewardCopper = row.GetInt32(4),
                FameReward = row.GetInt32(5),
                Status = row.GetString(6),
                CitadelId = row.GetInt64(7)
            },
            DbArgs.One("@id", orderId));

        if (order is null)
        {
            return WorkResult.Failure("Không tìm thấy đơn hàng.");
        }

        if (order.Status != "ACCEPTED" && order.Status != "AVAILABLE")
        {
            return WorkResult.Failure("Đơn hàng này đã hoàn thành hoặc hết hạn.");
        }

        var currentQty = _bag.Quantity(citadelId, order.TargetItemId);
        if (currentQty < order.QuantityNeeded)
        {
            var itemName = _items.Find(order.TargetItemId)?.NameVi ?? "vật phẩm";
            return WorkResult.Failure($"Chưa đủ số lượng giao nộp: cần {order.QuantityNeeded} {itemName}, chỉ có {currentQty} trong tay nải tại thành này.");
        }

        var rewardCopper = order.RewardCopper;
        if (HasPerk("thuong_than_dieu_toan"))
        {
            rewardCopper += (int)(rewardCopper * 0.25);
        }

        _db.Transaction(() =>
        {
            _bag.Remove(citadelId, order.TargetItemId, order.QuantityNeeded);
            _wallet.Pay(rewardCopper, JournalCodes.CourtStipend, $"Thù lao đơn đặt hàng {order.ClientName}");

            _db.Execute(
                "UPDATE pf_guild_fame SET total_fame = total_fame + @fame WHERE id = 1",
                DbArgs.One("@fame", order.FameReward));

            _db.Execute(
                "UPDATE pf_order SET status = 'COMPLETED' WHERE id = @id",
                DbArgs.One("@id", orderId));

            _attributes?.AddExp("CRA", 30);
        });

        return WorkResult.Success(
            $"Giao nộp thành công cho {order.ClientName}! Nhận {rewardCopper} Quan và +{order.FameReward} Danh vọng phường nghề.",
            order.QuantityNeeded,
            0);
    }

    // =========================================================================
    // PLAYER EQUIPMENT & COMBAT SLOTS
    // =========================================================================

    public IReadOnlyList<EquippedSlotDef> EquippedSlots => Slots;

    public IReadOnlyList<EquippedSlotDef> Slots =>
        _db.Query(
            """
            SELECT s.slot_code, s.equipment_id, e.name_vi, e.item_id, e.citadel_id, e.durability, e.max_durability, e.quality_rank, e.bonus_stat, e.bonus_value, COALESCE(e.temper_level, 0)
            FROM pf_equipped_slot s
            LEFT JOIN pf_equipment e ON e.id = s.equipment_id
            ORDER BY s.slot_code DESC
            """,
            row =>
            {
                var slotCode = row.GetString(0);
                var slotName = slotCode switch
                {
                    "WEAPON" => "Binh Khí",
                    "ARMOR" => "Hộ Giáp",
                    _ => "Khí Cụ"
                };

                EquipmentDef? equip = null;
                if (!row.IsDbNull(1))
                {
                    equip = new EquipmentDef(
                        row.GetInt64(1),
                        row.GetString(2),
                        row.GetInt64(3),
                        row.GetInt64(4),
                        row.GetInt32(5),
                        row.GetInt32(6),
                        row.GetString(7),
                        row.GetString(8),
                        row.GetInt32(9),
                        row.GetInt32(10));
                }

                return new EquippedSlotDef(slotCode, slotName, row.IsDbNull(1) ? null : row.GetInt64(1), equip);
            });

    public EquippedSlotDef? GetSlot(string slotCode) =>
        Slots.FirstOrDefault(s => s.SlotCode.Equals(slotCode, StringComparison.OrdinalIgnoreCase));

    public int GetTotalBonus(string statCode)
    {
        var total = 0;
        foreach (var slot in Slots)
        {
            if (slot.Equipment is not null && slot.Equipment.Durability > 0)
            {
                if (slot.Equipment.BonusStat.Equals(statCode, StringComparison.OrdinalIgnoreCase))
                {
                    total += slot.Equipment.BonusValue;
                }
            }
        }
        return total;
    }

    public int GetEquippedStatBonus(string statCode) => GetTotalBonus(statCode);

    public WorkResult Equip(long equipmentId)
    {
        var equip = FindEquipment(equipmentId);
        if (equip is null)
        {
            return WorkResult.Failure("Không tìm thấy trang bị này.");
        }

        var slotCode = ResolveSlotForEquipment(equip);
        var slotName = slotCode switch
        {
            "WEAPON" => "Binh Khí",
            "ARMOR" => "Hộ Giáp",
            _ => "Khí Cụ"
        };

        _db.Transaction(() =>
        {
            _db.Execute(
                """
                INSERT INTO pf_equipped_slot(slot_code, equipment_id, equipped_at_day)
                VALUES(@slot, @id, @day)
                ON CONFLICT(slot_code) DO UPDATE SET equipment_id = excluded.equipment_id, equipped_at_day = excluded.equipped_at_day
                """,
                new DbArgs().Add("@slot", slotCode).Add("@id", equipmentId).Add("@day", _clock.Today.DayIndex));
        });

        return WorkResult.Success($"Đã trang bị {equip.NameVi} vào vị trí {slotName} (+{equip.BonusValue} {equip.BonusStat}).", 1, 0);
    }

    public WorkResult Unequip(string slotCode)
    {
        var slot = GetSlot(slotCode);
        if (slot is null || slot.EquipmentId is null)
        {
            return WorkResult.Failure("Vị trí này hiện đang để trống.");
        }

        _db.Transaction(() =>
        {
            _db.Execute(
                "UPDATE pf_equipped_slot SET equipment_id = NULL WHERE slot_code = @slot",
                DbArgs.One("@slot", slot.SlotCode));
        });

        return WorkResult.Success($"Đã gỡ trang bị ở vị trí {slot.SlotNameVi}.", 1, 0);
    }

    public void DegradeEquipped(string slotCode, int loss)
    {
        if (loss <= 0) return;
        var slot = GetSlot(slotCode);
        if (slot is not null && slot.EquipmentId.HasValue)
        {
            _db.Execute(
                "UPDATE pf_equipment SET durability = MAX(0, durability - @loss) WHERE id = @id",
                new DbArgs().Add("@loss", loss).Add("@id", slot.EquipmentId.Value));
        }
    }

    private static string ResolveSlotForEquipment(EquipmentDef equip)
    {
        if (equip.BonusStat == "WAR")
        {
            var name = equip.NameVi.ToLowerInvariant();
            if (name.Contains("giáp") || name.Contains("tháp") || name.Contains("khôi"))
            {
                return "ARMOR";
            }
            return "WEAPON";
        }
        return "TOOL";
    }

    // =========================================================================
    // MONTHLY TICK (WORKSHOP RENT & ARTISAN LABOUR)
    // =========================================================================

    public void OnMonth(GameDate date)
    {
        var workshops = AllWorkshops();
        foreach (var ws in workshops)
        {
            if (!ws.IsActive) continue;

            // Workshop rent
            if (_wallet.Liquid >= ws.MonthlyRent)
            {
                _wallet.Charge(ws.MonthlyRent, JournalCodes.ToolFee, $"Tiền thuê {ws.NameVi} tháng {date.Month:00}/{date.Year}");
            }
            else
            {
                _log.Warn($"Không đủ tiền trả thuê xưởng {ws.NameVi} tháng {date}.");
            }

            // Artisan salaries & production
            if (ws.HiredArtisans > 0)
            {
                var totalSalary = ws.HiredArtisans * ArtisanMonthlySalary;
                if (_wallet.Liquid >= totalSalary)
                {
                    _wallet.Charge(totalSalary, JournalCodes.ToolFee, $"Lương thợ phụ {ws.NameVi} ({ws.HiredArtisans} người)");

                    var trade = Trades.FirstOrDefault(t => t.Id == ws.TradeId);
                    if (trade is not null)
                    {
                        var yieldPerWorker = trade.BaseQuantity;
                        var totalYield = ws.HiredArtisans * yieldPerWorker;
                        _bag.Add(ws.CitadelId, trade.OutputItemId, totalYield);

                        var currentPractice = PracticeCount(trade.Id);
                        var newPractice = currentPractice + ws.HiredArtisans;
                        var newTier = Math.Min(MaxTier, newPractice / PracticesPerTier);

                        _db.Execute(
                            """
                            INSERT INTO pf_skill(trade_id, practice_count, tier) VALUES(@trade, @practice, @tier)
                            ON CONFLICT(trade_id) DO UPDATE SET practice_count = excluded.practice_count, tier = excluded.tier
                            """,
                            new DbArgs().Add("@trade", trade.Id).Add("@practice", newPractice).Add("@tier", newTier));

                        _log.Info($"Workshop {ws.NameVi} produced +{totalYield} items via {ws.HiredArtisans} artisans.");
                    }
                }
                else
                {
                    _log.Warn($"Không đủ trả lương {totalSalary} Quan cho thợ phụ {ws.NameVi}.");
                }
            }
        }

        // Expire overdue orders
        _db.Execute(
            "UPDATE pf_order SET status = 'EXPIRED' WHERE deadline_day < @day AND status IN ('AVAILABLE', 'ACCEPTED')",
            DbArgs.One("@day", date.DayIndex));

        EnsureNpcOrders(date);
    }

    // =========================================================================
    // MASTERY PERKS & TALENTS
    // =========================================================================

    public IReadOnlyList<MasteryPerkDef> Perks =>
        _db.Query(
            """
            SELECT id, code, name_vi, description_vi, required_fame, is_unlocked
            FROM pf_mastery_perk
            ORDER BY required_fame ASC
            """,
            row => new MasteryPerkDef(
                row.GetInt64(0),
                row.GetString(1),
                row.GetString(2),
                row.GetString(3),
                row.GetInt32(4),
                row.GetInt64(5) != 0));

    public bool HasPerk(string code) =>
        _db.ScalarLong("SELECT COUNT(*) FROM pf_mastery_perk WHERE code = @code AND is_unlocked = 1", DbArgs.One("@code", code)) > 0;

    public WorkResult UnlockPerk(long perkId)
    {
        var perk = Perks.FirstOrDefault(p => p.Id == perkId);
        if (perk is null)
        {
            return WorkResult.Failure("Không tìm thấy bí kỹ này.");
        }

        if (perk.IsUnlocked)
        {
            return WorkResult.Failure($"Bí kỹ '{perk.NameVi}' đã được giác ngộ.");
        }

        if (TotalGuildFame < perk.RequiredFame)
        {
            return WorkResult.Failure($"Chưa đủ danh vọng phường nghề (cần {perk.RequiredFame} điểm, hiện có {TotalGuildFame}).");
        }

        _db.Transaction(() =>
        {
            _db.Execute(
                "UPDATE pf_mastery_perk SET is_unlocked = 1 WHERE id = @id",
                DbArgs.One("@id", perkId));
        });

        return WorkResult.Success($"Giác ngộ thành công bí kỹ '{perk.NameVi}'! {perk.DescriptionVi}", 1, 0);
    }

    // =========================================================================
    // DEEP RPG LIFE SKILLS (COOKING, FISHING, FORAGING, TEMPERING)
    // =========================================================================

    public IReadOnlyList<DishRecipeDef> Dishes =>
        _db.Query(
            """
            SELECT d.id, d.code, d.name_vi, d.description_vi, d.primary_item_id, d.primary_qty,
                   d.secondary_item_id, d.secondary_qty, d.fatigue_relief, d.stress_relief, d.health_gain,
                   d.cook_fee, COALESCE(inv.quantity, 0)
            FROM pf_dish d
            LEFT JOIN pf_dish_inventory inv ON inv.dish_id = d.id
            ORDER BY d.id ASC
            """,
            row => new DishRecipeDef(
                row.GetInt64(0),
                row.GetString(1),
                row.GetString(2),
                row.GetString(3),
                row.GetInt64(4),
                row.GetInt32(5),
                row.IsDbNull(6) ? null : row.GetInt64(6),
                row.GetInt32(7),
                row.GetInt32(8),
                row.GetInt32(9),
                row.GetInt32(10),
                row.GetInt64(11),
                row.GetInt32(12)));

    public WorkResult Cook(long citadelId, long dishId)
    {
        var dish = Dishes.FirstOrDefault(d => d.Id == dishId);
        if (dish is null)
            return WorkResult.Failure("Không tìm thấy thực đơn món ăn.");

        if (_wallet.Liquid < dish.CookFee)
            return WorkResult.Failure($"Không đủ tiền mua gia vị, củi lửa ({dish.CookFee} Quan).");

        var primaryItem = _items.Find(dish.PrimaryItemId);
        if (primaryItem is null)
            return WorkResult.Failure("Vật phẩm nguyên liệu không tồn tại.");

        var hasPrimary = _bag.Quantity(citadelId, dish.PrimaryItemId);
        if (hasPrimary < dish.PrimaryQuantity)
            return WorkResult.Failure($"Thiếu nguyên liệu chính: Cần {dish.PrimaryQuantity} {primaryItem.NameVi} (hiện có {hasPrimary}).");

        if (dish.SecondaryItemId.HasValue && dish.SecondaryQuantity > 0)
        {
            var secondaryItem = _items.Find(dish.SecondaryItemId.Value);
            var hasSecondary = _bag.Quantity(citadelId, dish.SecondaryItemId.Value);
            if (hasSecondary < dish.SecondaryQuantity)
                return WorkResult.Failure($"Thiếu nguyên liệu phụ: Cần {dish.SecondaryQuantity} {secondaryItem?.NameVi ?? "phụ gia"} (hiện có {hasSecondary}).");
        }

        // Deduct materials & fee
        _bag.Remove(citadelId, dish.PrimaryItemId, dish.PrimaryQuantity);
        if (dish.SecondaryItemId.HasValue && dish.SecondaryQuantity > 0)
        {
            _bag.Remove(citadelId, dish.SecondaryItemId.Value, dish.SecondaryQuantity);
        }

        _wallet.Charge(dish.CookFee, JournalCodes.ToolFee, $"Nấu món {dish.NameVi}");

        // Add to dish inventory
        _db.Execute(
            """
            INSERT INTO pf_dish_inventory (dish_id, quantity)
            VALUES (@id, 1)
            ON CONFLICT(dish_id) DO UPDATE SET quantity = quantity + 1
            """,
            DbArgs.One("@id", dish.Id));

        (bool Awakened, bool LeveledUp, int Level) practice = (false, false, 1);
        if (_attributes is not null)
        {
            practice = _attributes.PracticeSkill("am_thuc", 25);
            _attributes.AddExp("CRA", 15);
            _attributes.AddExp("INT", 10);
        }

        var milestoneMsg = FormatSkillProgressMessage("Ẩm Thực", practice);
        _log.Info($"dlc.professions: cooked {dish.Code}: {dish.NameVi}");

        return WorkResult.Success($"Nấu thành công 1 phần {dish.NameVi} thơm ngon!{milestoneMsg}", 1, 0);
    }

    public WorkResult Eat(long dishId)
    {
        var dish = Dishes.FirstOrDefault(d => d.Id == dishId);
        if (dish is null)
            return WorkResult.Failure("Không tìm thấy món ăn.");

        if (dish.InInventory <= 0)
            return WorkResult.Failure($"Không còn {dish.NameVi} trong giỏ ẩm thực để dùng bữa.");

        _db.Execute(
            """
            UPDATE pf_dish_inventory
            SET quantity = quantity - 1
            WHERE dish_id = @id AND quantity > 0
            """,
            DbArgs.One("@id", dish.Id));

        _attributes?.RecoverVitals(dish.FatigueRelief, dish.StressRelief, dish.HealthGain);
        _log.Info($"dlc.professions: consumed {dish.Code}: -{dish.FatigueRelief} fatigue, -{dish.StressRelief} stress, +{dish.HealthGain} hp");

        return WorkResult.Success(
            $"Thưởng thức {dish.NameVi}: Giảm {dish.FatigueRelief} Mệt mỏi, giảm {dish.StressRelief} Căng thẳng, hồi phục {dish.HealthGain} Khí huyết.",
            1,
            0);
    }

    public WorkResult Fish(long citadelId)
    {
        _clock.Advance(1);
        _attributes?.ApplyFatigue(10);

        var practice = _attributes?.PracticeSkill("dieu_ngu", 25) ?? (false, false, 0);
        var skillLevel = practice.Level;
        var attrs = _attributes?.Attributes;
        var wil = attrs?.Wil ?? 50;
        var prc = attrs?.Prc ?? 50;
        var lck = attrs?.Lck ?? 50;

        var region = _map?.Find(citadelId)?.Region ?? "trung_tho";
        var isWaterRegion = region is "giang_dong" or "duong_chau" or "kinh_chau";

        var catchCount = (practice.Level > 0 ? 2 : 1) + (skillLevel >= 3 ? 1 : 0) + (skillLevel >= 6 ? 1 : 0) + (skillLevel >= 9 ? 1 : 0);
        if (wil >= 60 || prc >= 60) catchCount += 1;
        if (isWaterRegion) catchCount += 1;

        _bag.Add(citadelId, 8, catchCount); // 8: ca_kho

        _attributes?.RecoverVitals(fatigueRelief: 0, stressRelief: 15, healthGain: 0);
        _attributes?.AddExp("WIL", 15);
        _attributes?.AddExp("PRC", 10);

        var extraMsg = string.Empty;
        if (isWaterRegion && (skillLevel >= 3 || lck >= 60))
        {
            _wallet.Pay(50, "PF.SALVAGE", "Bắt được Ba Ba Vàng / Trai ngọc Trường Giang");
            extraMsg = " Vớt được Ba Ba Vàng quý hiếm bán được 50 Quan!";
        }
        else if (skillLevel >= 4 || lck >= 65)
        {
            _wallet.Pay(80, "PF.SALVAGE", "Vớt được rương cổ tích dưới lòng sông");
            extraMsg = " Cơ duyên vớt được Rương Cổ Tích dưới đáy sông (+80 Quan)!";
        }

        var milestoneMsg = FormatSkillProgressMessage("Điêu Ngư", practice);
        var regionalFlavor = isWaterRegion ? "Lưu vực Trường Giang thủy triều mênh mông" : "Bến đò sông Hoàng Hà";
        _log.Info($"dlc.professions: fished at citadel {citadelId} ({region}): +{catchCount} fish");
        return WorkResult.Success($"1 ngày buông cần tại {regionalFlavor}: Câu được {catchCount} Cá tươi/khô, tâm trí thư thái (giảm 15 Căng thẳng).{extraMsg}{milestoneMsg}", catchCount, 1);
    }

    public WorkResult Forage(long citadelId)
    {
        _clock.Advance(1);
        _attributes?.ApplyFatigue(15);

        var practice = _attributes?.PracticeSkill("tam_duoc", 25) ?? (false, false, 0);
        var skillLevel = practice.Level;
        var attrs = _attributes?.Attributes;
        var prc = attrs?.Prc ?? 50;
        var @int = attrs?.Int ?? 50;

        var region = _map?.Find(citadelId)?.Region ?? "trung_tho";
        var herbCount = (practice.Level > 0 ? 2 : 1) + (skillLevel >= 3 ? 1 : 0) + (skillLevel >= 6 ? 1 : 0) + (skillLevel >= 9 ? 1 : 0);
        if (prc >= 60) herbCount += 1;
        _bag.Add(citadelId, 7, herbCount); // 7: thuoc

        _attributes?.AddExp("PRC", 20);
        _attributes?.AddExp("INT", 10);

        var extraMsg = string.Empty;
        if (region == "u_chau" && (skillLevel >= 2 || prc >= 60))
        {
            _bag.Add(citadelId, 13, 1); // 13: sam (Nhân sâm Liêu Đông)
            extraMsg = " Vùng lạnh phương Bắc đào được 1 củ Nhân Sâm Liêu Đông quý giá!";
        }
        else if (region == "ich_chau" && (skillLevel >= 2 || prc >= 60))
        {
            _bag.Add(citadelId, 14, 1); // 14: tram_huong (Trầm hương Thục sơn)
            extraMsg = " Trong thung lũng sương mù Ba Thục tìm thấy 1 khúc Trầm Hương lâu năm!";
        }
        else if (prc >= 65 || @int >= 65)
        {
            _bag.Add(citadelId, 13, 1);
            extraMsg = " Tinh mắt phát hiện thêm 1 củ Nhân Sâm quý hiếm!";
        }

        var milestoneMsg = FormatSkillProgressMessage("Tầm Dược", practice);
        _log.Info($"dlc.professions: foraged at citadel {citadelId} ({region}): +{herbCount} herbs");
        return WorkResult.Success($"1 ngày vượt suối trèo non tầm dược: Thu hái được {herbCount} Thảo dược quý.{extraMsg}{milestoneMsg}", herbCount, 1);
    }

    public WorkResult Hunt(long citadelId)
    {
        _clock.Advance(1);

        var practice = _attributes?.PracticeSkill("thu_liep", 25) ?? (false, false, 0);
        var skillLevel = practice.Level;
        var attrs = _attributes?.Attributes;
        var war = attrs?.War ?? 50;
        var prc = attrs?.Prc ?? 50;

        var region = _map?.Find(citadelId)?.Region ?? "trung_tho";
        var isWilderness = region is "u_chau" or "ich_chau";

        if (isWilderness && war < 35)
        {
            _attributes?.ApplyFatigue(25);
            _attributes?.RecoverVitals(fatigueRelief: 0, stressRelief: -10, healthGain: -20);
            _log.Info($"dlc.professions: hunted at citadel {citadelId} - wounded by wild beast");
            return WorkResult.Failure("Gặp phải đàn dã thú hung hãn trên núi! Võ lực không đủ trấn áp, bị thương tổn hại 20 Khí huyết, đành ôm vết thương lui về.");
        }

        _attributes?.ApplyFatigue(20);

        var meatCount = (practice.Level > 0 ? 2 : 1) + (skillLevel >= 3 ? 1 : 0) + (skillLevel >= 6 ? 1 : 0) + (skillLevel >= 9 ? 1 : 0) + (war >= 60 ? 1 : 0);
        _bag.Add(citadelId, 8, meatCount); // 8: ca_kho (lương thực thịt cá)

        var extraMsg = string.Empty;
        if (region == "u_chau")
        {
            _bag.Add(citadelId, 11, 1); // 11: vai
            if (skillLevel >= 4)
            {
                _bag.Add(citadelId, 12, 1); // 12: gam
                extraMsg = " Săn được Hươu tuyết phương Bắc, thu được da lông thú thượng phẩm (+1 Gấm)!";
            }
            else
            {
                extraMsg = " Thu được da lông hươu tuyết giữ ấm (+1 Vải thô).";
            }
        }
        else if (region == "ich_chau")
        {
            if (war >= 65)
            {
                _bag.Add(citadelId, 13, 1); // 13: sam
                _wallet.Pay(60, "PF.SALVAGE", "Tiền thưởng diệt trừ ác thú");
                extraMsg = " Bắn hạ Mãnh Hổ Ba Thục! Thu hoạch Mật Gấu đại bổ (+1 Nhân sâm) và nhận 60 Quan tiền thưởng trừ hại cho dân!";
            }
            else
            {
                extraMsg = " Bắn được Sơn dương núi đá, bổ sung lương thực cho thương đội.";
            }
        }
        else if (region is "giang_dong" or "duong_chau" or "kinh_chau")
        {
            _bag.Add(citadelId, 5, 1); // 5: sat
            extraMsg = " Bắt được cá sấu đầm lầy, vẩy cứng chắc như sắt giáp (+1 Sắt)!";
        }
        else
        {
            extraMsg = " Bắn trúng bầy lợn rừng châu thổ, thu hoạch thịt tươi dồi dào.";
        }

        _attributes?.AddExp("WAR", 20);
        _attributes?.AddExp("PRC", 15);
        _attributes?.AddExp("CON", 10);

        var milestoneMsg = FormatSkillProgressMessage("Thú Liệp", practice);
        _log.Info($"dlc.professions: hunted at citadel {citadelId} ({region}): +{meatCount} meat");
        return WorkResult.Success($"1 ngày giương cung săn bắn tại vùng {region}: Thu hoạch {meatCount} phần thịt thú săn.{extraMsg}{milestoneMsg}", meatCount, 1);
    }

    public WorkResult Temper(long citadelId, long equipmentId)
    {
        var eq = FindEquipment(equipmentId);
        if (eq is null)
            return WorkResult.Failure("Không tìm thấy khí cụ để tôi luyện.");

        if (eq.CitadelId != citadelId)
            return WorkResult.Failure("Khí cụ không có ở thành này.");

        if (eq.TemperLevel >= 5)
            return WorkResult.Failure("Khí cụ đã tôi luyện đạt mức tối đa (+5).");

        var nextLevel = eq.TemperLevel + 1;
        var reqSat = nextLevel * 2;
        var reqThan = nextLevel * 1;
        var fee = nextLevel * 25;

        var reqCra = 20 + nextLevel * 10;
        var reqWar = 20 + nextLevel * 10;

        var attrs = _attributes?.Attributes;
        if (attrs is not null)
        {
            if (attrs.Cra < reqCra || attrs.War < reqWar)
            {
                return WorkResult.Failure($"Không đủ công xảo hoặc võ lực để tôi luyện cấp +{nextLevel}: Cần CRA {reqCra} (hiện {attrs.Cra}) và WAR {reqWar} (hiện {attrs.War}).");
            }
        }

        if (_wallet.Liquid < fee)
            return WorkResult.Failure($"Không đủ lộ phí và lò than tôi luyện ({fee} Quan).");

        var satInBag = _bag.Quantity(citadelId, 5); // 5 = sat
        if (satInBag < reqSat)
            return WorkResult.Failure($"Thiếu quặng sắt tôi luyện: Cần {reqSat} Sắt (hiện có {satInBag}).");

        var thanInBag = _bag.Quantity(citadelId, 15); // 15 = than_da
        if (thanInBag < reqThan)
            return WorkResult.Failure($"Thiếu than đá hỏa hầu: Cần {reqThan} Than Đá (hiện có {thanInBag}).");

        // Deduct materials & fee
        _bag.Remove(citadelId, 5, reqSat);
        _bag.Remove(citadelId, 15, reqThan);
        _wallet.Charge(fee, JournalCodes.ToolFee, $"Tôi luyện khí cụ {eq.NameVi} lên +{nextLevel}");

        var newBonus = eq.BonusValue + 1;
        var newMaxDur = eq.MaxDurability + 15;

        _db.Execute(
            """
            UPDATE pf_equipment
            SET temper_level = @lvl,
                bonus_value = @bonus,
                max_durability = @maxDur,
                durability = @maxDur
            WHERE id = @id
            """,
            new DbArgs()
                .Add("@lvl", nextLevel)
                .Add("@bonus", newBonus)
                .Add("@maxDur", newMaxDur)
                .Add("@id", eq.Id));

        (bool Awakened, bool LeveledUp, int Level) practice = (false, false, 1);
        if (_attributes is not null)
        {
            practice = _attributes.PracticeSkill("che_tac", 25);
            _attributes.ApplyFatigue(15);
            _attributes.AddExp("CRA", 25);
            _attributes.AddExp("WAR", 15);
        }

        var milestoneMsg = FormatSkillProgressMessage("Chế Tác", practice);
        _log.Info($"dlc.professions: tempered equipment {eq.Id} to +{nextLevel}");
        return WorkResult.Success($"Tôi luyện thành công {eq.NameVi} lên cấp +{nextLevel}! Chỉ số tăng lên +{newBonus} {eq.BonusStat}, độ bền tối đa đạt {newMaxDur}.{milestoneMsg}", 1, 1);
    }

    public long CalculateEquipmentSellValue(EquipmentDef equipment)
    {
        var baseItem = _items.Find(equipment.ItemId);
        var basePrice = baseItem?.BasePrice ?? 50;

        var qualityMultiplier = equipment.QualityRank switch
        {
            "LEGENDARY" => 4.0,
            "MASTER" => 2.5,
            "FINE" => 1.5,
            _ => 1.0
        };

        var temperBonus = 1.0 + (equipment.TemperLevel * 0.25);
        var duraRatio = equipment.MaxDurability > 0
            ? (double)equipment.Durability / equipment.MaxDurability
            : 1.0;

        var value = (long)(basePrice * qualityMultiplier * temperBonus * duraRatio) + 15;
        return Math.Max(10, value);
    }

    public WorkResult SellEquipment(long citadelId, long equipmentId)
    {
        var eq = FindEquipment(equipmentId);
        if (eq is null)
            return WorkResult.Failure("Không tìm thấy khí cụ để bán.");

        if (eq.CitadelId != citadelId)
            return WorkResult.Failure("Khí cụ không có ở thành này.");

        // If equipped, unequip first
        var slot = Slots.FirstOrDefault(s => s.EquipmentId == equipmentId);
        if (slot != null)
        {
            Unequip(slot.SlotCode);
        }

        var price = CalculateEquipmentSellValue(eq);

        _db.Transaction(() =>
        {
            _db.Execute(
                "DELETE FROM pf_equipment WHERE id = @id",
                DbArgs.One("@id", equipmentId));

            _wallet.Pay(price, JournalCodes.MarketSell, $"Bán khí cụ {eq.NameVi}");
        });

        _log.Info($"dlc.professions: sold equipment {eq.NameVi} (#{equipmentId}) at citadel {citadelId} for {price} copper");
        return WorkResult.Success($"Đã bán {eq.NameVi} cho phường rèn thương nhân, thu được {price} Quan tiền đồng.", 1, 0);
    }

    public long CalculateDishSellValue(DishRecipeDef dish) => dish.Code switch
    {
        "com_nam" => 35,
        "canh_ca" => 75,
        "thit_nuong" => 90,
        "ruou_am" => 80,
        "dan_duoc" => 180,
        _ => Math.Max(25, dish.CookFee * 3)
    };

    public WorkResult SellDish(long citadelId, long dishId, int quantity = 1)
    {
        if (quantity <= 0) quantity = 1;

        var dish = Dishes.FirstOrDefault(d => d.Id == dishId);
        if (dish is null)
            return WorkResult.Failure("Không tìm thấy món ăn này.");

        var count = _db.Query(
            "SELECT quantity FROM pf_dish_inventory WHERE dish_id = @id",
            row => row.GetInt32(0),
            DbArgs.One("@id", dishId)).FirstOrDefault();

        if (count < quantity)
            return WorkResult.Failure($"Trong kho chỉ còn {count} phần {dish.NameVi}, không đủ {quantity} phần để bán.");

        var unitPrice = CalculateDishSellValue(dish);
        var total = unitPrice * quantity;

        _db.Transaction(() =>
        {
            if (count == quantity)
            {
                _db.Execute("DELETE FROM pf_dish_inventory WHERE dish_id = @id", DbArgs.One("@id", dishId));
            }
            else
            {
                _db.Execute("UPDATE pf_dish_inventory SET quantity = quantity - @qty WHERE dish_id = @id",
                    new DbArgs().Add("@qty", quantity).Add("@id", dishId));
            }

            _wallet.Pay(total, JournalCodes.MarketSell, $"Bán {quantity} phần {dish.NameVi}");
        });

        _log.Info($"dlc.professions: sold {quantity} {dish.Code} for {total} copper");
        return WorkResult.Success($"Đã bán {quantity} phần {dish.NameVi} cho tửu lâu/khách quán, thu về {total} Quan.", quantity, 0);
    }

    private void EnsureNpcOrders(GameDate date)
    {
        var activeCount = _db.Query(
            "SELECT COUNT(*) FROM pf_order WHERE status IN ('AVAILABLE', 'ACCEPTED')",
            row => row.GetInt32(0)).FirstOrDefault();

        if (activeCount >= 6) return;

        var needed = 6 - activeCount;
        var npcs = _npcs?.All;
        if (npcs is null || npcs.Count == 0) return;

        var seed = (uint)(date.DayIndex * 179 + activeCount * 31);
        var rng = new Random((int)seed);

        for (int i = 0; i < needed; i++)
        {
            var npc = npcs[rng.Next(npcs.Count)];
            long targetItem;
            int qty;
            int reward;
            int fame;
            string clientType;

            var role = npc.Role;
            if (role.Contains("Tướng") || role.Contains("Võ") || role.Contains("Hiệp"))
            {
                clientType = "MILITARY";
                var roll = rng.Next(3);
                if (roll == 0) { targetItem = 5; qty = 6 + rng.Next(6); reward = qty * 45; fame = 15; }
                else if (roll == 1) { targetItem = 8; qty = 8 + rng.Next(10); reward = qty * 30; fame = 12; }
                else { targetItem = 11; qty = 5 + rng.Next(5); reward = qty * 28; fame = 10; }
            }
            else if (role.Contains("Y") || role.Contains("Dược") || npc.Skills.Any(s => s.Code == "tam_duoc"))
            {
                clientType = "COMMERCE";
                if (rng.Next(2) == 0) { targetItem = 7; qty = 3 + rng.Next(4); reward = qty * 150; fame = 20; }
                else { targetItem = 13; qty = 2 + rng.Next(3); reward = qty * 260; fame = 25; }
            }
            else if (role.Contains("Mưu") || role.Contains("Sĩ") || role.Contains("Chủ") || role.Contains("Đế") || role.Contains("Thái"))
            {
                clientType = "COURT";
                var roll = rng.Next(4);
                if (roll == 0) { targetItem = 3; qty = 5 + rng.Next(6); reward = qty * 50; fame = 15; }
                else if (roll == 1) { targetItem = 4; qty = 3 + rng.Next(4); reward = qty * 120; fame = 20; }
                else if (roll == 2) { targetItem = 12; qty = 2 + rng.Next(3); reward = qty * 190; fame = 25; }
                else { targetItem = 14; qty = 2 + rng.Next(2); reward = qty * 230; fame = 30; }
            }
            else
            {
                clientType = "NOBLE";
                var roll = rng.Next(3);
                if (roll == 0) { targetItem = 1; qty = 10 + rng.Next(15); reward = qty * 18; fame = 8; }
                else if (roll == 1) { targetItem = 6; qty = 6 + rng.Next(8); reward = qty * 25; fame = 10; }
                else { targetItem = 2; qty = 8 + rng.Next(10); reward = qty * 12; fame = 8; }
            }

            var deadline = date.DayIndex + 45 + rng.Next(30);

            _db.Execute(
                """
                INSERT INTO pf_order (client_name_vi, client_type, citadel_id, target_item_id, quantity_needed, reward_copper, fame_reward, deadline_day, status)
                VALUES (@client, @type, @citadel, @item, @qty, @reward, @fame, @deadline, 'AVAILABLE')
                """,
                new DbArgs()
                    .Add("@client", npc.NameVi)
                    .Add("@type", clientType)
                    .Add("@citadel", npc.CitadelId)
                    .Add("@item", targetItem)
                    .Add("@qty", qty)
                    .Add("@reward", reward)
                    .Add("@fame", fame)
                    .Add("@deadline", deadline));
        }
    }

    private static string FormatSkillProgressMessage(string skillNameVi, (bool Awakened, bool LeveledUp, int Level) progress)
    {
        if (progress.Awakened)
            return $" [LĨNH HỘI: Linh quang lóe sáng! Tự ngộ kỹ năng {skillNameVi} - Cấp 1 {SkillRankTitles.ForLevel(1)}!]";
        if (progress.LeveledUp)
            return $" [THĂNG CẤP: Kỹ năng {skillNameVi} đột phá Cấp {progress.Level} - {SkillRankTitles.ForLevel(progress.Level)}!]";
        if (progress.Level == 0)
            return $" (Thao tác sơ khởi còn vụng về, chưa thể lĩnh hội áo nghĩa {skillNameVi}. Hãy kiên trì rèn luyện!).";
        return string.Empty;
    }

    private static IReadOnlyList<TradeDefinition> LoadTrades(IDbSession db) =>
        db.Query(
            """
            SELECT id, code, name_vi, name_en, output_item_id, days_per_batch, base_quantity, tool_fee, COALESCE(sector, 'CRAFT')
            FROM pf_trade
            ORDER BY id
            """,
            row => new TradeDefinition(
                row.GetInt64(0),
                row.GetString(1),
                row.GetString(2),
                row.GetString(3),
                row.GetInt64(4),
                row.GetInt32(5),
                row.GetInt32(6),
                row.GetInt64(7),
                row.GetString(8)));
}
