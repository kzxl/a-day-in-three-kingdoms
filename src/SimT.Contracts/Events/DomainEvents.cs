using SimT.Contracts.Time;

namespace SimT.Contracts.Events;

/// <summary>
/// Common marker contract for in-process reactive domain events.
/// </summary>
public interface IDomainEvent
{
    GameDate Date { get; }
}

/// <summary>
/// Emitted when the player relocates or travels between citadels.
/// </summary>
public readonly record struct PlayerRelocatedEvent(
    long PlayerId,
    long FromCityId,
    long ToCityId,
    GameDate Date) : IDomainEvent;

/// <summary>
/// Emitted when a market transaction is executed (wholesale or stall trade).
/// </summary>
public readonly record struct MarketTradeExecutedEvent(
    long CityId,
    long ItemId,
    int Quantity,
    long PricePerUnit,
    bool IsBuy,
    GameDate Date) : IDomainEvent;

/// <summary>
/// Emitted when an arena or martial duel finishes.
/// </summary>
public readonly record struct DuelCompletedEvent(
    long NpcId,
    bool PlayerWon,
    string CitadelName,
    GameDate Date) : IDomainEvent;

/// <summary>
/// Emitted when an agricultural plot completes harvest.
/// </summary>
public readonly record struct HarvestCompletedEvent(
    long CityId,
    int PlotId,
    long ItemId,
    int Yield,
    GameDate Date) : IDomainEvent;

/// <summary>
/// Emitted when a karma or causality event chain reaches fruition.
/// </summary>
public readonly record struct KarmaEventTriggeredEvent(
    string ChainId,
    string TitleVi,
    string OutcomeSummary,
    GameDate Date) : IDomainEvent;

/// <summary>
/// Emitted when player fame or reputation standing changes.
/// </summary>
public readonly record struct PlayerReputationChangedEvent(
    long OldFame,
    long NewFame,
    string TitleVi,
    GameDate Date) : IDomainEvent;

/// <summary>
/// Emitted when a market is cornered via economic warfare.
/// </summary>
public readonly record struct MarketCorneredEvent(
    long CitadelId,
    long ItemId,
    int Severity,
    GameDate Date) : IDomainEvent;

/// <summary>
/// Emitted when contraband or emergency supplies are successfully smuggled into a besieged citadel.
/// </summary>
public readonly record struct ContrabandDeliveredEvent(
    long CitadelId,
    long ItemId,
    int Quantity,
    GameDate Date) : IDomainEvent;
