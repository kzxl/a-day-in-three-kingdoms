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
