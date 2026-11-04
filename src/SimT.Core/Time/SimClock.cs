using SimT.Contracts;
using SimT.Contracts.Data;
using SimT.Contracts.Events;
using SimT.Contracts.Time;

namespace SimT.Core.Time;

/// <summary>
/// K2 — the only mover of time. Persists the current date in <c>core_state</c> and publishes
/// exactly one <see cref="MonthClosed"/> per boundary, recorded in <c>core_period_log</c> so a
/// replayed or retried advance can never charge the same month twice.
/// </summary>
public sealed class SimClock : IGameClock
{
    private const string DateKey = "date";

    private readonly IDbSession _db;
    private readonly IEventPublisher _events;
    private GameDate _today;

    public SimClock(IDbSession db, IEventPublisher events, GameDate? campaignStart = null)
    {
        _db = db;
        _events = events;

        var stored = db.ScalarString("SELECT value FROM core_state WHERE key=@key", DbArgs.One("@key", DateKey));
        _today = stored is null
            ? campaignStart ?? GameDate.Start
            : GameDateCodec.Parse(stored);
    }

    public GameDate Today => _today;

    public GameDate Advance(int days)
    {
        if (days <= 0)
        {
            throw new SimException("time.invalid_advance", $"Advance requires at least one day, got {days}.");
        }

        for (var i = 0; i < days; i++)
        {
            var previous = _today;
            _today = _today.AddDays(1);
            _events.Publish(new DayAdvanced(_today));

            if (_today.Day == 1)
            {
                PublishOnce(new MonthClosed(_today, previous));
            }
        }

        Persist();
        return _today;
    }

    public void Persist()
    {
        _db.Execute(
            "INSERT INTO core_state(key, value) VALUES(@key, @value) ON CONFLICT(key) DO UPDATE SET value=excluded.value",
            new DbArgs().Add("@key", DateKey).Add("@value", GameDateCodec.Format(_today)));
    }

    /// <summary>
    /// Idempotent publication: the period key is written first, and the event only fires when
    /// that insert actually happened. Two advances across the same boundary emit once.
    /// </summary>
    private void PublishOnce(MonthClosed closed)
    {
        var inserted = _db.Execute(
            "INSERT OR IGNORE INTO core_period_log(period_key, published_at) VALUES(@key, @at)",
            new DbArgs()
                .Add("@key", "month:" + closed.Date.DayIndex)
                .Add("@at", DateTime.UtcNow.ToString("O")));

        if (inserted == 0)
        {
            return;
        }

        _events.Publish(closed);
    }

}
