namespace SimT.Contracts.Time;

/// <summary>
/// The only way the simulation moves forward. There is no <c>DateTime.Now</c> in domain
/// code: callers ask the clock, the clock persists the date.
/// </summary>
public interface IGameClock
{
    GameDate Today { get; }

    /// <summary>
    /// Advances the campaign by <paramref name="days"/> and publishes a
    /// <see cref="MonthClosed"/> event for every month boundary crossed.
    /// </summary>
    GameDate Advance(int days);
}

/// <summary>Raised once per month boundary, in calendar order.</summary>
public readonly record struct MonthClosed(GameDate Date, GameDate PreviousDate);

/// <summary>
/// Raised for every single day, before any boundary event. Time-driven modules (caravans in
/// transit, crops, construction) listen here instead of polling the clock.
/// </summary>
public readonly record struct DayAdvanced(GameDate Date);
