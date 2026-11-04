using SimT.Contracts.Time;

namespace SimT.Contracts.Playing;

/// <summary>
/// Cross-module data contracts. A DLC must never reference another module's assembly, so the
/// shapes exchanged through capabilities live here, next to the capability ids.
/// </summary>
public static class CapabilityIds
{
    public const string PlayerWallet = "player.wallet";
    public const string PlayerBag = "player.bag";
    public const string PlayerState = "player.state";
    public const string PlayerAttributes = "player.attributes";
    public const string ItemCatalog = "content.items";
    public const string WorldMap = "world.map";
    public const string MarketTrade = "market.trade";
    public const string MarketWarehouse = "market.warehouse";
    public const string MarketPortfolio = "market.portfolio";
    public const string PlayerStall = "market.player_stall";
    public const string CaravanGuild = "caravan.guild";
    public const string ProfessionGuild = "professions.guild";
    public const string LineageHouse = "lineage.house";
    public const string CultivationEstate = "cultivation.estate";
    public const string CourtOffice = "politics.court";
    public const string NpcRegistry = "npc.registry";
}

/// <summary>Cash access for modules that must charge or pay the player.</summary>
public interface IWallet
{
    long Balance { get; }

    long Liquid { get; }

    /// <summary>
    /// Charges the player; throws when short. Posts through the kernel ledger. An idempotency
    /// key makes a repeated call (retry, replayed month) a no-op instead of a second charge.
    /// </summary>
    void Charge(long amount, string journalCode, string description, string? idempotencyKey = null);

    void Pay(long amount, string journalCode, string description, string? idempotencyKey = null);
}

public sealed record ItemDef(
    long Id,
    string Code,
    string NameVi,
    string NameEn,
    string Category,
    long BasePrice,
    int ShelfLifeDays,
    int Weight);

/// <summary>Regional price factor in permille: 1000 = baseline, 1120 = 12% dearer.</summary>
public sealed record CitadelDef(
    long Id,
    string Code,
    string NameVi,
    string NameEn,
    string Region,
    int PriceFactorPermille);

/// <summary>Sublocation / Point of Interest owned by a citadel.</summary>
public sealed record SublocationDef(
    long Id,
    long CitadelId,
    string Code,
    string NameVi,
    string Kind);

/// <summary>Canonical 12 RPG attributes: 8 Action Capacities + 4 Innate Vitals & Destiny.</summary>
public sealed record CharacterAttributes(
    int War,
    int Ldr,
    int Int,
    int Pol,
    int Dip,
    int Com,
    int Log,
    int Cra,
    int Con,
    int Wil,
    int Prc,
    int Lck);

/// <summary>Dynamic vitals profile: health, fatigue, morale, stress, stamina.</summary>
public sealed record CharacterVitals(
    int Health,
    int MaxHp,
    int Fatigue,
    int Morale,
    int Stress,
    int CurrentStamina,
    int MaxStamina);

/// <summary>Social standing & moral alignment.</summary>
public sealed record CharacterSocial(
    int Fame,
    int Virtue,
    int Credibility);

/// <summary>Full demographic and canonical attributes of an NPC owned by <c>base.npc</c>.</summary>
public sealed record NpcDef(
    long Id,
    string Code,
    string NameVi,
    string? NameHan,
    string? StyleNameVi,
    string Role,
    long CitadelId,
    CharacterAttributes Attributes,
    string? Personality,
    string? BioVi);

public sealed record BagRow(long CityId, long ItemId, int Quantity);

/// <summary>Read-only catalog owned by <c>base.content</c>.</summary>
public interface IItemCatalog
{
    IReadOnlyList<ItemDef> All { get; }

    ItemDef? Find(long itemId);

    ItemDef? FindByCode(string code);
}

/// <summary>Read-only map owned by <c>base.world</c>.</summary>
public interface IWorldMap
{
    IReadOnlyList<CitadelDef> Citadels { get; }

    CitadelDef? Find(long citadelId);

    IReadOnlyList<long> RouteNeighbours(long citadelId);

    /// <summary>Travel time in days, or 0 when there is no road between the two settlements.</summary>
    int TravelDays(long fromCitadelId, long toCitadelId);

    /// <summary>Sub-locations / POIs inside or around the citadel.</summary>
    IReadOnlyList<SublocationDef> Sublocations(long citadelId);
}

/// <summary>Read-only NPC registry owned by <c>base.npc</c>.</summary>
public interface INpcRegistry
{
    IReadOnlyList<NpcDef> All { get; }

    NpcDef? Find(long npcId);

    NpcDef? FindByCode(string code);

    IReadOnlyList<NpcDef> FindByCitadel(long citadelId);
}

/// <summary>Per-city inventory owned by <c>base.player</c>.</summary>
public interface IBag
{
    int Quantity(long cityId, long itemId);

    int TotalQuantity(long cityId);

    void Add(long cityId, long itemId, int quantity);

    bool Remove(long cityId, long itemId, int quantity);

    IReadOnlyList<BagRow> Rows(long cityId);
}

public interface IPlayerState
{
    long PlayerId { get; }

    string Name { get; }

    long CurrentCityId { get; }

    void MoveTo(long cityId, GameDate date);
}

/// <summary>RPG character stats and vitals owned by <c>base.player</c>.</summary>
public interface IPlayerAttributes
{
    CharacterAttributes Attributes { get; }

    CharacterVitals Vitals { get; }

    CharacterSocial Social { get; }

    void AddExp(string attributeCode, int amount);

    void ApplyFatigue(int amount);

    void Rest(int days);
}
