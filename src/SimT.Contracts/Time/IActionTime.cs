namespace SimT.Contracts.Time;

/// <summary>
/// K2 — the one gate every meaningful action passes through. A module must not call
/// <c>IGameClock.Advance</c> for its own action: it asks <see cref="Charge"/>, which looks the
/// cost up in the policy and moves the calendar. The charge belongs inside the caller's
/// transaction, so a failed action takes its days back with it.
/// </summary>
public interface IActionTime
{
    /// <summary>What the action would cost, without spending anything.</summary>
    ActionTimeQuote Quote(string actionCode);

    /// <summary>Spends the action's days.</summary>
    void Charge(string actionCode);
}

/// <summary>Read side of the policy, so a screen can show "2 ngày" before the player commits.</summary>
public interface IActionCostPolicy
{
    ActionCost Of(string actionCode);

    IReadOnlyDictionary<string, ActionCost> All { get; }
}

/// <summary>Raised after an action's time cost was charged, so the shell can report it.</summary>
public readonly record struct ActionTimeCharged(string ActionCode, ActionCost Cost, GameDate Date);
