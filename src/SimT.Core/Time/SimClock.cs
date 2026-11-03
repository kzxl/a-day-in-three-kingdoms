using SimT.Contracts;
using SimT.Contracts.Data;
using SimT.Contracts.Events;
using SimT.Contracts.Time;

namespace SimT.Core.Time;

/// <summary>
/// K2 — the only mover of time. Persists the current date in <c>core_state</c> and publishes
/// exactly one <see cref="MonthClosed"/> per boundary crossed.
/// </summary>
public sealed class SimClock : IGameClock
{
    private const string DateKey = "date";

    private readonly IDbSession _db;
    private readonly IEventPublisher _events;
    private GameDate _today;

    public SimClock(IDbSession db, IEventPublisher events)
    {
        _db = db;
        _events = events;

        var stored = db.ScalarString("SELECT value FROM core_state WHERE key=@key", DbArgs.One("@key", DateKey));
        _today = stored is null ? GameDate.Start : Parse(stored);
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
            if (_today.Day == 1)
            {
                _events.Publish(new MonthClosed(_today, previous));
            }
        }

        Persist();
        return _today;
    }

    public void Persist()
    {
        _db.Execute(
            "INSERT INTO core_state(key, value) VALUES(@key, @value) ON CONFLICT(key) DO UPDATE SET value=excluded.value",
            new DbArgs().Add("@key", DateKey).Add("@value", _today.ToString()));
    }

    private static GameDate Parse(string stored)
    {
        var parts = stored.Split('-');
        if (parts.Length != 3
            || !int.TryParse(parts[0], out var year)
            || !int.TryParse(parts[1], out var month)
            || !int.TryParse(parts[2], out var day))
        {
            throw new SimException("time.corrupt", $"Stored date '{stored}' cannot be parsed.");
        }

        return new GameDate(year, month, day);
    }
}
