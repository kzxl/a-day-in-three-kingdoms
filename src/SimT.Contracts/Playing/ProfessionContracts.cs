namespace SimT.Contracts.Playing;

/// <summary>Read surface of <c>dlc.professions</c>, provided as the <c>professions.guild</c> capability.</summary>
public interface IProfessionGuild
{
    IReadOnlyList<TradeDefinition> Trades { get; }

    IReadOnlyList<RecipeDef> Recipes { get; }

    IReadOnlyList<EquipmentDef> Equipments(long citadelId);

    int Tier(long tradeId);

    int PracticeCount(long tradeId);

    WorkResult Work(long cityId, long tradeId);

    WorkResult Craft(long cityId, long recipeId);

    WorkResult Repair(long cityId, long equipmentId);
}

public sealed record TradeDefinition(
    long Id,
    string Code,
    string NameVi,
    string NameEn,
    long OutputItemId,
    int DaysPerBatch,
    int BaseQuantity,
    long ToolFee);

public sealed record RecipeDef(
    long Id,
    long TradeId,
    string NameVi,
    int RequiredTier,
    long MaterialItemId,
    int MaterialQuantity,
    long? SecondaryItemId,
    int SecondaryQuantity,
    long OutputItemId,
    int OutputQuantity,
    int CraftDays,
    long CraftFee);

public sealed record EquipmentDef(
    long Id,
    string NameVi,
    long ItemId,
    long CitadelId,
    int Durability,
    int MaxDurability,
    string QualityRank,
    string BonusStat,
    int BonusValue);

public readonly record struct WorkResult(bool Ok, string Message, int Quantity, int Days)
{
    public static WorkResult Success(string message, int quantity, int days) => new(true, message, quantity, days);

    public static WorkResult Failure(string message) => new(false, message, 0, 0);
}
