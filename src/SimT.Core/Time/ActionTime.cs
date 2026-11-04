using SimT.Contracts.Events;
using SimT.Contracts.Time;

namespace SimT.Core.Time;

/// <summary>
/// K2 — the single place an action spends calendar time. Modules ask for a charge instead of
/// advancing the clock themselves, so every action's cost stays in one table and every charge is
/// announced for the report.
/// </summary>
public sealed class ActionTime : IActionTime
{
    private readonly IGameClock _clock;
    private readonly IActionCostPolicy _policy;
    private readonly IEventPublisher _events;

    public ActionTime(IGameClock clock, IActionCostPolicy policy, IEventPublisher events)
    {
        _clock = clock;
        _policy = policy;
        _events = events;
    }

    public ActionTimeQuote Quote(string actionCode) => new(actionCode, _policy.Of(actionCode));

    public void Charge(string actionCode)
    {
        var cost = _policy.Of(actionCode);
        if (cost.IsFree)
        {
            return;
        }

        var date = _clock.Advance(cost.Days);
        _events.Publish(new ActionTimeCharged(actionCode, cost, date));
    }
}
