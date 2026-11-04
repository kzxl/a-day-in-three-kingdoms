using SimT.Contracts;
using SimT.Contracts.Data;
using SimT.Contracts.Ledger;
using SimT.Contracts.Logging;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Professions;

/// <summary>
/// Day labour, trades, equipment crafting, and tool maintenance.
/// Working a trade raises the skill tier, unlocking recipes and boosting output.
/// </summary>
internal sealed class ProfessionService : IProfessionGuild
{
    public const int PracticesPerTier = 4;
    public const int MaxTier = 5;

    private readonly IDbSession _db;
    private readonly IGameClock _clock;
    private readonly IWallet _wallet;
    private readonly IBag _bag;
    private readonly IItemCatalog _items;
    private readonly ILog _log;

    public ProfessionService(IDbSession db, IGameClock clock, IWallet wallet, IBag bag, IItemCatalog items, ILog log)
    {
        _db = db;
        _clock = clock;
        _wallet = wallet;
        _bag = bag;
        _items = items;
        _log = log;
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
            SELECT id, name_vi, item_id, citadel_id, durability, max_durability, quality_rank, bonus_stat, bonus_value
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
                row.GetInt32(8)),
            DbArgs.One("@citadel", citadelId));

    /// <summary>Boot-time validation: every output must exist in the shared catalog.</summary>
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
        var quantity = trade.BaseQuantity + tierBefore;
        var tierAfter = 0;

        _db.Transaction(() =>
        {
            _wallet.Charge(trade.ToolFee, JournalCodes.ToolFee, $"Dụng cụ nghề {trade.NameVi}");
            _clock.Advance(trade.DaysPerBatch);
            _bag.Add(cityId, trade.OutputItemId, quantity);

            var practice = PracticeCount(tradeId) + 1;
            tierAfter = Math.Min(MaxTier, practice / PracticesPerTier);

            _db.Execute(
                """
                INSERT INTO pf_skill(trade_id, practice_count, tier) VALUES(@trade, @practice, @tier)
                ON CONFLICT(trade_id) DO UPDATE SET practice_count = excluded.practice_count, tier = excluded.tier
                """,
                new DbArgs().Add("@trade", tradeId).Add("@practice", practice).Add("@tier", tierAfter));
        });

        _log.Info($"worked {trade.Code}: +{quantity} {output.Code}, tier {tierBefore} -> {tierAfter}");

        var tierNote = tierAfter > tierBefore ? $", tay nghề lên bậc {tierAfter}" : string.Empty;
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

        _db.Transaction(() =>
        {
            _wallet.Charge(recipe.CraftFee, JournalCodes.ToolFee, $"Chi phí chế tác {recipe.NameVi}");
            _clock.Advance(recipe.CraftDays);

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

            // Create piece of equipment in citadel
            _db.Execute(
                """
                INSERT INTO pf_equipment(name_vi, item_id, citadel_id, durability, max_durability, quality_rank, bonus_stat, bonus_value)
                VALUES(@name, @item, @citadel, 100, 100, 'FINE', 'CRA', 3)
                """,
                new DbArgs()
                    .Add("@name", recipe.NameVi)
                    .Add("@item", recipe.OutputItemId)
                    .Add("@citadel", cityId));
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
            return WorkResult.Failure("Không tìm thấy trang bị tại thành này.");
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

        return WorkResult.Success($"Đã tu sửa {equip.NameVi} phục hồi toàn bộ độ bền {equip.MaxDurability}/100.", 1, 0);
    }

    private static IReadOnlyList<TradeDefinition> LoadTrades(IDbSession db) =>
        db.Query(
            """
            SELECT id, code, name_vi, name_en, output_item_id, days_per_batch, base_quantity, tool_fee
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
                row.GetInt64(7)));
}
