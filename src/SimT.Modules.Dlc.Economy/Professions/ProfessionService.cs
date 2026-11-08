using SimT.Contracts;
using SimT.Contracts.Data;
using SimT.Contracts.Ledger;
using SimT.Contracts.Logging;
using SimT.Contracts.Modules;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Economy;

/// <summary>
/// Day labour, trades, equipment crafting, workshop automation, and guild commissions.
/// Working a trade raises the skill tier, unlocking recipes and boosting output.
/// </summary>
internal sealed partial class ProfessionService : IProfessionGuild, IPlayerEquipment
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
    private readonly IPlayerState? _player;
    private readonly ICapabilityBroker? _broker;

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
        ISocietyChronicle? chronicle = null,
        IPlayerState? player = null,
        ICapabilityBroker? broker = null)
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
        _player = player;
        _broker = broker;
        Trades = LoadTrades(db);
    }

    private ISocietyChronicle? GetChronicle()
    {
        if (_chronicle != null)
        {
            return _chronicle;
        }

        if (_broker != null && _broker.TryGet<ISocietyChronicle>(CapabilityIds.SocietyChronicle, out var sc))
        {
            return sc;
        }

        return null;
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

    private static EquipmentDef MapEquipment(DbRecord row) =>
        new(
            row.GetInt64(0),
            row.GetString(1),
            row.GetInt64(2),
            row.GetInt64(3),
            row.GetInt32(4),
            row.GetInt32(5),
            row.GetString(6),
            row.GetString(7),
            row.GetInt32(8),
            row.GetInt32(9),
            row.IsDbNull(10) ? null : row.GetInt64(10),
            row.IsDbNull(11) ? null : row.GetString(11),
            row.IsDbNull(12) ? null : row.GetInt64(12),
            row.IsDbNull(13) ? null : row.GetInt64(13),
            row.GetInt32(14),
            row.GetInt32(15),
            row.IsDbNull(16) ? null : row.GetString(16),
            row.IsDbNull(17) ? null : row.GetString(17));

    public IReadOnlyList<EquipmentDef> Equipments(long citadelId) =>
        _db.Query(
            """
            SELECT id, name_vi, item_id, citadel_id, durability, max_durability, quality_rank, bonus_stat, bonus_value, COALESCE(temper_level, 0),
                   creator_id, creator_name_vi, forged_citadel_id, forged_day_index, COALESCE(fame_score, 0), COALESCE(kill_count, 0), legend_narrative, lot_code
            FROM pf_equipment
            WHERE citadel_id = @citadel
            ORDER BY id ASC
            """,
            MapEquipment,
            DbArgs.One("@citadel", citadelId));

    public EquipmentDef? FindEquipment(long equipmentId) =>
        _db.QueryOne(
            """
            SELECT id, name_vi, item_id, citadel_id, durability, max_durability, quality_rank, bonus_stat, bonus_value, COALESCE(temper_level, 0),
                   creator_id, creator_name_vi, forged_citadel_id, forged_day_index, COALESCE(fame_score, 0), COALESCE(kill_count, 0), legend_narrative, lot_code
            FROM pf_equipment
            WHERE id = @id
            """,
            MapEquipment,
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
        var activeTool = GetSlot("TOOL")?.Equipment;
        var toolBonus = activeTool is not null && activeTool.Durability > 0 ? 1 : 0;
        var quantity = trade.BaseQuantity + tierBefore + perkBonus + toolBonus;
        var daysCost = HasPerk("nhat_tam_nhi_dung") ? Math.Max(1, trade.DaysPerBatch - 1) : trade.DaysPerBatch;
        var tierAfter = 0;

        _db.Transaction(() =>
        {
            _wallet.Charge(trade.ToolFee, JournalCodes.ToolFee, $"Dụng cụ nghề {trade.NameVi}");
            _clock.Advance(daysCost);
            _bag.Add(cityId, trade.OutputItemId, quantity);
            DegradeEquipped("TOOL", 2);

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

    public long CurrentDayIndex => _clock.Today.DayIndex;

    public string GetRegionalNodeSummary(long cityId, string nodeType)
    {
        if (_broker is not null && _broker.TryGet<IValueChainService>(CapabilityIds.ValueChain, out var vc) && vc is not null)
        {
            var node = vc.GetResourceNodes(cityId).FirstOrDefault();
            if (node is not null)
            {
                var pItem = _items.Find(node.PrimaryItemId)?.NameVi ?? $"#{node.PrimaryItemId}";
                var sItem = node.SecondaryItemId.HasValue ? _items.Find(node.SecondaryItemId.Value)?.NameVi : null;
                var itemsLabel = sItem is not null ? $"{pItem} / {sItem}" : pItem;
                return $"{node.NodeNameVi} (Đặc sản: {itemsLabel})";
            }
        }
        return nodeType == "MINE_NODE" ? "Mỏ khoáng lộ thiên quanh thành" : "Rừng sâu & núi cao quanh thành";
    }

    public WorkResult WorkBatch(long cityId, long tradeId, int batches = 3)
    {
        batches = Math.Clamp(batches, 1, 5);
        var trade = Trades.FirstOrDefault(candidate => candidate.Id == tradeId);
        if (trade is null)
        {
            return WorkResult.Failure("Nghề này không tồn tại.");
        }

        var output = _items.Find(trade.OutputItemId);
        var tierBefore = Tier(tradeId);
        var qtyBefore = _bag.Quantity(cityId, trade.OutputItemId);
        var dayBefore = _clock.Today.DayIndex;
        var completed = 0;
        string? stopReason = null;

        for (var i = 0; i < batches; i++)
        {
            var res = Work(cityId, tradeId);
            if (!res.Ok)
            {
                stopReason = res.Message;
                break;
            }
            completed++;
        }

        if (completed == 0)
        {
            return WorkResult.Failure(stopReason ?? "Không thể thực hiện đợt lao tác.");
        }

        var totalQty = (int)Math.Max(0, _bag.Quantity(cityId, trade.OutputItemId) - qtyBefore);
        var totalDays = (int)Math.Max(0, _clock.Today.DayIndex - dayBefore);
        var tierAfter = Tier(tradeId);
        var tierNote = tierAfter > tierBefore ? $", thăng lên '{GetCraftsmanRank(tradeId)}' (bậc {tierAfter})" : string.Empty;
        return WorkResult.Success(
            $"Hoàn thành đợt {completed} lượt {trade.NameVi} ({totalDays} ngày): +{totalQty} {output?.NameVi ?? trade.NameVi} (tổng phí {completed * trade.ToolFee} Quan){tierNote}.",
            totalQty,
            totalDays);
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

            var practice = PracticeCount(recipe.TradeId) + 2;
            var tierAfter = Math.Min(MaxTier, practice / PracticesPerTier);
            _db.Execute(
                """
                INSERT INTO pf_skill(trade_id, practice_count, tier) VALUES(@trade, @practice, @tier)
                ON CONFLICT(trade_id) DO UPDATE SET practice_count = excluded.practice_count, tier = excluded.tier
                """,
                new DbArgs().Add("@trade", recipe.TradeId).Add("@practice", practice).Add("@tier", tierAfter));

            _attributes?.AddExp("CRA", 25);

            _attributes?.PracticeSkill("master_forging", 20);
            var masterForgingLvl = _attributes?.GetEquippedSkillLevel("master_forging") ?? 0;

            var playerTier = Tier(recipe.TradeId);
            var seed = (int)(recipe.Id * 37 + playerTier * 19 + _clock.Today.DayIndex);
            var rng = new Random(seed);
            var perkAlchDiscount = HasPerk("dan_dao_thong_than") ? 0.10 : 0.0;
            var skillQualityBoost = masterForgingLvl * 0.025;
            var roll = Math.Max(0.0, rng.NextDouble() - furnaceBonus - perkAlchDiscount - skillQualityBoost);

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
            if (masterForgingLvl > 0)
            {
                maxDura += masterForgingLvl * 3;
            }

            var bonusStat = recipe.TradeId switch
            {
                4 => "WAR",
                5 => "HEA",
                7 => "DIP",
                9 => "COM",
                16 => "COM",
                3 => recipe.NameVi.Contains("Nỗ") || recipe.NameVi.Contains("Cung") ? "WAR" : "COM",
                _ => "POL"
            };

            var creatorId = _player?.PlayerId ?? 0;
            var creatorName = !string.IsNullOrWhiteSpace(_player?.Name) ? _player.Name : "Đại Sư Lữ Khách";
            var forgedDay = _clock.Today.DayIndex;
            var initialFame = qualityRank == "LEGENDARY" ? 40 : (qualityRank == "MASTER" ? 25 : (qualityRank == "FINE" ? 10 : 0));
            if (HasPerk("grandmaster_seal"))
            {
                initialFame += 30;
            }

            _db.Execute(
                """
                INSERT INTO pf_equipment(name_vi, item_id, citadel_id, durability, max_durability, quality_rank, bonus_stat, bonus_value, creator_id, creator_name_vi, forged_citadel_id, forged_day_index, fame_score, kill_count)
                VALUES(@name, @item, @citadel, @dura, @dura, @quality, @stat, @bonus, @creatorId, @creatorName, @forgedCit, @forgedDay, @fame, 0)
                """,
                new DbArgs()
                    .Add("@name", recipe.NameVi)
                    .Add("@item", recipe.OutputItemId)
                    .Add("@citadel", cityId)
                    .Add("@dura", maxDura)
                    .Add("@quality", qualityRank)
                    .Add("@stat", bonusStat)
                    .Add("@bonus", bonusValue)
                    .Add("@creatorId", creatorId)
                    .Add("@creatorName", creatorName)
                    .Add("@forgedCit", cityId)
                    .Add("@forgedDay", forgedDay)
                    .Add("@fame", initialFame));

            var insertedId = _db.ScalarLong("SELECT last_insert_rowid()");
            var lotCode = $"LOT-{_clock.Today.Year}{_clock.Today.Month:D2}-C{cityId:D2}-{insertedId:D4}";
            _db.Execute(
                "UPDATE pf_equipment SET lot_code = @lot WHERE id = @id",
                new DbArgs().Add("@lot", lotCode).Add("@id", insertedId));
        });

        return WorkResult.Success(
            $"Chế tác thành công {recipe.NameVi}! Thu được {recipe.OutputQuantity} {outputName} sau {recipe.CraftDays} ngày.",
            recipe.OutputQuantity,
            recipe.CraftDays);
    }

    public long CalculateRepairCost(long cityId, EquipmentDef equip)
    {
        var missing = Math.Max(0, equip.MaxDurability - equip.Durability);
        if (missing == 0) return 0;

        var rankMult = equip.QualityRank switch
        {
            "LEGENDARY" => 4,
            "MASTER" => 3,
            "FINE" => 2,
            _ => 1
        };

        var rawCost = Math.Max(15, (missing * rankMult) / 3 + equip.TemperLevel * 4);
        var ws = GetWorkshop(cityId);
        if (ws is not null && ws.IsActive)
        {
            rawCost = Math.Max(6, (rawCost * 60) / 100);
        }

        if (HasPerk("eternal_edge"))
        {
            rawCost = Math.Max(6, (rawCost * 75) / 100);
        }

        var maintLvl = _attributes?.GetEquippedSkillLevel("weapon_maintenance") ?? 0;
        if (maintLvl > 0)
        {
            var discountPct = Math.Min(50, maintLvl * 5);
            rawCost = Math.Max(5, (rawCost * (100 - discountPct)) / 100);
        }

        return rawCost;
    }

    public WorkResult Repair(long cityId, long equipmentId)
    {
        var equip = Equipments(cityId).FirstOrDefault(e => e.Id == equipmentId) ?? FindEquipment(equipmentId);
        if (equip is null)
        {
            return WorkResult.Failure("Không tìm thấy trang bị.");
        }

        if (equip.Durability >= equip.MaxDurability)
        {
            return WorkResult.Failure("Trang bị này còn nguyên vẹn, không cần tu sửa.");
        }

        var baseCost = CalculateRepairCost(cityId, equip);
        var slotType = ResolveSlotForEquipment(equip);
        var stdMatId = slotType switch
        {
            "ARMOR" => _bag.Quantity(cityId, 27) > 0 ? 27L : 11L,
            "TOOL" => 6L,
            _ => 5L
        };

        var hasBlackIron = slotType is "WEAPON" or "ARMOR" && _bag.Quantity(cityId, 25) > 0;
        var hasStdMat = _bag.Quantity(cityId, stdMatId) > 0;
        var finalCost = hasStdMat ? Math.Max(5, baseCost / 2) : baseCost;

        if (_wallet.Liquid < finalCost)
        {
            return WorkResult.Failure($"Cần {finalCost} Quan tiền công tu sửa, chỉ có {_wallet.Liquid}.");
        }

        var newMaxDura = equip.MaxDurability;
        var bonusNote = string.Empty;

        _db.Transaction(() =>
        {
            _wallet.Charge(finalCost, JournalCodes.ToolFee, $"Tu sửa {equip.NameVi}");

            if (hasBlackIron)
            {
                _bag.Remove(cityId, 25, 1);
                newMaxDura += 5;
                bonusNote = " [Đại tu bằng Huyền Thiết: Độ bền tối đa vĩnh viễn +5!]";
            }
            else if (hasStdMat)
            {
                _bag.Remove(cityId, stdMatId, 1);
                var matName = _items.Find(stdMatId)?.NameVi ?? "vật liệu";
                bonusNote = $" (Dùng 1 {matName} tự có, giảm 50% tiền công thợ)";
            }

            _db.Execute(
                "UPDATE pf_equipment SET durability = @maxDura, max_durability = @maxDura WHERE id = @id",
                new DbArgs().Add("@maxDura", newMaxDura).Add("@id", equipmentId));

            _attributes?.PracticeSkill("weapon_maintenance", 25);
        });

        return WorkResult.Success(
            $"Đã tu sửa {equip.NameVi} phục hồi toàn bộ độ bền {newMaxDura}/{newMaxDura} (tốn {finalCost} Quan).{bonusNote}",
            1,
            0);
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

        var furnaceLvl = _attributes?.GetEquippedSkillLevel("furnace_mastery") ?? 0;
        var nextLevel = eq.TemperLevel + 1;
        var reqSat = nextLevel * 2;
        var reqThan = nextLevel * 1;
        var fee = furnaceLvl > 0 ? (nextLevel * 20) : (nextLevel * 25);

        var reqCra = Math.Max(15, 20 + nextLevel * 10 - furnaceLvl * 2);
        var reqWar = Math.Max(15, 20 + nextLevel * 10 - furnaceLvl * 2);

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

        var satInBag = _bag.Quantity(citadelId, 5);
        if (satInBag < reqSat)
            return WorkResult.Failure($"Thiếu quặng sắt tôi luyện: Cần {reqSat} Sắt (hiện có {satInBag}).");

        var thanInBag = _bag.Quantity(citadelId, 15);
        if (thanInBag < reqThan)
            return WorkResult.Failure($"Thiếu than đá hỏa hầu: Cần {reqThan} Than Đá (hiện có {thanInBag}).");

        _bag.Remove(citadelId, 5, reqSat);
        _bag.Remove(citadelId, 15, reqThan);
        _wallet.Charge(fee, JournalCodes.ToolFee, $"Tôi luyện khí cụ {eq.NameVi} lên +{nextLevel}");

        var newBonus = eq.BonusValue + 1;
        var newMaxDur = eq.MaxDurability + 15 + (furnaceLvl > 0 ? 5 : 0);

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
            _attributes.PracticeSkill("furnace_mastery", 25);
            _attributes.ApplyFatigue(15);
            _attributes.AddExp("CRA", 25);
            _attributes.AddExp("WAR", 15);
        }

        var milestoneMsg = FormatSkillProgressMessage("Chế Tác", practice);
        _log.Info($"dlc.professions: tempered equipment {eq.Id} to +{nextLevel}");
        return WorkResult.Success($"Tôi luyện thành công {eq.NameVi} lên cấp +{nextLevel}! Chỉ số tăng lên +{newBonus} {eq.BonusStat}, độ bền tối đa đạt {newMaxDur}.{milestoneMsg}", 1, 1);
    }

    public static string FormatQualityRankVi(string? rank) => rank switch
    {
        "LEGENDARY" => "Cực Phẩm ★★★",
        "MASTER" => "Thượng Phẩm ★★",
        "FINE" => "Trung Phẩm ★",
        "COMMON" => "Hạ Phẩm",
        _ => rank ?? "—"
    };

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

        var famePremium = equipment.FameScore * 2;
        var rawValue = (basePrice * qualityMultiplier * temperBonus * duraRatio) + famePremium + 15;
        if (HasPerk("grandmaster_seal"))
        {
            rawValue *= 1.25;
        }
        var appraisalLvl = _attributes?.GetEquippedSkillLevel("jade_appraisal") ?? 0;
        if (appraisalLvl > 0)
        {
            rawValue *= (1.0 + appraisalLvl * 0.05);
        }
        return Math.Max(10, (long)rawValue);
    }

    public WorkResult SellEquipment(long citadelId, long equipmentId)
    {
        var eq = FindEquipment(equipmentId);
        if (eq is null)
            return WorkResult.Failure("Không tìm thấy khí cụ để bán.");

        if (eq.CitadelId != citadelId)
            return WorkResult.Failure("Khí cụ không có ở thành này.");

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
            _attributes?.PracticeSkill("jade_appraisal", 20);
        });

        _log.Info($"dlc.professions: sold equipment {eq.NameVi} (#{equipmentId}) at citadel {citadelId} for {price} copper");
        return WorkResult.Success($"Đã bán {eq.NameVi} cho phường rèn thương nhân, thu được {price} Quan tiền đồng.", 1, 0);
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
