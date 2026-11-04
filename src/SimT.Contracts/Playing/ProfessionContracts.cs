namespace SimT.Contracts.Playing;

/// <summary>Represents a workshop owned/rented by the player in a citadel.</summary>
public sealed record WorkshopDef(
    long Id,
    long CitadelId,
    long TradeId,
    string NameVi,
    long MonthlyRent,
    int FurnaceLevel,
    int HiredArtisans,
    bool IsActive,
    int CreatedDayIndex);

/// <summary>Crafting commission or order from factions, garrisons, or merchants.</summary>
public sealed record CraftOrderDef(
    long Id,
    string ClientNameVi,
    string ClientType,
    long CitadelId,
    long TargetItemId,
    int QuantityNeeded,
    int RewardCopper,
    int FameReward,
    int DeadlineDay,
    string Status,
    int? AcceptedDay = null);

/// <summary>Equipment slot on the protagonist (Weapon, Armor, Tool).</summary>
public sealed record EquippedSlotDef(
    string SlotCode, // "WEAPON", "ARMOR", "TOOL"
    string SlotNameVi,
    long? EquipmentId,
    EquipmentDef? Equipment);

/// <summary>Player equipment management interface, provided as <c>player.equipment</c>.</summary>
public interface IPlayerEquipment
{
    IReadOnlyList<EquippedSlotDef> Slots { get; }

    EquippedSlotDef? GetSlot(string slotCode);

    int GetTotalBonus(string statCode);

    WorkResult Equip(long equipmentId);

    WorkResult Unequip(string slotCode);

    void DegradeEquipped(string slotCode, int loss);
}

/// <summary>Read surface of <c>dlc.professions</c>, provided as the <c>professions.guild</c> capability.</summary>
public interface IProfessionGuild
{
    IReadOnlyList<TradeDefinition> Trades { get; }

    IReadOnlyList<RecipeDef> Recipes { get; }

    IReadOnlyList<EquipmentDef> Equipments(long citadelId);

    int Tier(long tradeId);

    int PracticeCount(long tradeId);

    string GetCraftsmanRank(long tradeId);

    int TotalGuildFame { get; }

    WorkResult Work(long cityId, long tradeId);

    WorkResult Craft(long cityId, long recipeId);

    WorkResult Repair(long cityId, long equipmentId);

    // Workshop features
    WorkshopDef? GetWorkshop(long citadelId);

    IReadOnlyList<WorkshopDef> AllWorkshops();

    WorkResult OpenWorkshop(long citadelId, long tradeId);

    WorkResult HireArtisan(long citadelId);

    WorkResult DismissArtisan(long citadelId);

    WorkResult UpgradeFurnace(long citadelId);

    // Crafting orders
    IReadOnlyList<CraftOrderDef> AvailableOrders(long citadelId);

    IReadOnlyList<CraftOrderDef> ActiveOrders();

    WorkResult AcceptOrder(long orderId);

    WorkResult FulfillOrder(long citadelId, long orderId);

    // Equipment features
    IReadOnlyList<EquippedSlotDef> EquippedSlots { get; }

    WorkResult Equip(long equipmentId);

    WorkResult Unequip(string slotCode);

    int GetEquippedStatBonus(string statCode);
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
