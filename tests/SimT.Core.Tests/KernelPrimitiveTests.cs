using SimT.Contracts;
using SimT.Contracts.Data;
using SimT.Contracts.Ledger;
using SimT.Contracts.Time;
using SimT.Core.Data;
using SimT.Core.Events;
using SimT.Core.Identity;
using SimT.Core.Ledger;
using SimT.Core.Time;

namespace SimT.Core.Tests;

public sealed class KernelPrimitiveTests : IDisposable
{
    private readonly string _path;
    private readonly SqliteSession _db;

    public KernelPrimitiveTests()
    {
        _path = Path.Combine(Path.GetTempPath(), "simt-core-tests", Guid.NewGuid().ToString("N") + ".db");
        _db = SqliteSession.Open(_path);
        new MigrationRunner(_db).Apply(typeof(SimT.Core.Kernel.SimKernel).Assembly, "core");
    }

    public void Dispose()
    {
        _db.Dispose();
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }

    [Fact]
    public void GameDate_round_trips_through_day_index()
    {
        foreach (var index in new long[] { 0, 1, 29, 30, 359, 360, 721 })
        {
            Assert.Equal(index, GameDate.FromDayIndex(index).DayIndex);
        }
    }

    [Fact]
    public void GameDate_crosses_month_and_year_boundaries()
    {
        var end = new GameDate(1, 12, 30);
        var next = end.AddDays(1);

        Assert.Equal(new GameDate(2, 1, 1), next);
        Assert.True(next.IsMonthStart);
        Assert.True(next.IsYearStart);
    }

    [Fact]
    public void Clock_publishes_one_boundary_per_month_crossed()
    {
        var events = new EventPublisher();
        var closed = new List<MonthClosed>();
        events.Subscribe<MonthClosed>(closed.Add);

        var clock = new SimClock(_db, events);
        clock.Advance(61);

        Assert.Equal(2, closed.Count);
        Assert.Equal(new GameDate(1, 2, 1), closed[0].Date);
        Assert.Equal(new GameDate(1, 3, 1), closed[1].Date);
    }

    [Fact]
    public void Ledger_posts_and_keeps_balance_consistent()
    {
        var ledger = new SqliteLedger(_db);
        var date = GameDate.Start;

        ledger.Post(date, new LedgerEntry(JournalCodes.StartingCash, LedgerDirection.In, 1000, "start"));
        ledger.Post(date, new LedgerEntry(JournalCodes.MarketBuy, LedgerDirection.Out, 250, "buy"));

        Assert.Equal(750, ledger.Balance);
        Assert.Equal(750, ledger.Liquid);
        Assert.Equal(0, ledger.Safe);

        ledger.Move(date, 200, toSafe: true, JournalCodes.SafeDeposit, "vault");

        Assert.Equal(750, ledger.Balance);
        Assert.Equal(550, ledger.Liquid);
        Assert.Equal(200, ledger.Safe);
    }

    [Fact]
    public void Ledger_rejects_non_positive_amounts()
    {
        var ledger = new SqliteLedger(_db);

        var error = Assert.Throws<SimException>(() =>
            ledger.Post(GameDate.Start, new LedgerEntry(JournalCodes.MarketBuy, LedgerDirection.Out, 0, "nothing")));

        Assert.Equal("ledger.invalid_amount", error.Code);
    }

    [Fact]
    public void Ids_are_sequential_and_deterministic()
    {
        var ids = new SequenceIdGenerator(_db);

        Assert.Equal(1, ids.Next("market"));
        Assert.Equal(2, ids.Next("market"));
        Assert.Equal(1, ids.Next("lineage"));
        Assert.Equal("MK000003", ids.NextId("MK", "market"));
    }

    [Fact]
    public void Rng_reproduces_the_same_stream_from_the_same_seed()
    {
        var left = new SplitMix64Rng(12345);
        var right = new SplitMix64Rng(12345);

        for (var i = 0; i < 100; i++)
        {
            Assert.Equal(left.Next(0, 1000), right.Next(0, 1000));
        }
    }

    [Fact]
    public void Clock_publishes_each_boundary_only_once_even_if_the_date_is_rewound()
    {
        var events = new EventPublisher();
        var closed = 0;
        events.Subscribe<MonthClosed>(_ => closed++);

        var clock = new SimClock(_db, events);
        clock.Advance(31);
        Assert.Equal(1, closed);

        // Simulate a replayed save: the stored date goes back, the period log does not.
        _db.Execute("UPDATE core_state SET value='0001-01-01' WHERE key='date'");
        var rewound = new SimClock(_db, events);
        rewound.Advance(31);

        Assert.Equal(1, closed);
    }

    [Fact]
    public void Ledger_refuses_to_overdraw_the_cash_account()
    {
        var ledger = new SqliteLedger(_db);
        ledger.Post(GameDate.Start, new LedgerEntry(JournalCodes.StartingCash, LedgerDirection.In, 100, "start"));

        var error = Assert.Throws<SimException>(() =>
            ledger.Post(GameDate.Start, new LedgerEntry(JournalCodes.MarketBuy, LedgerDirection.Out, 101, "too much")));

        Assert.Equal("ledger.overdraft", error.Code);
        Assert.Equal(100, ledger.Balance);
    }

    [Fact]
    public void An_overdraft_inside_a_transaction_rolls_the_whole_thing_back()
    {
        var ledger = new SqliteLedger(_db);
        ledger.Post(GameDate.Start, new LedgerEntry(JournalCodes.StartingCash, LedgerDirection.In, 50, "start"));

        Assert.Throws<SimException>(() => _db.Transaction(() =>
        {
            ledger.Post(GameDate.Start, new LedgerEntry(JournalCodes.MarketSell, LedgerDirection.In, 10, "in"));
            ledger.Post(GameDate.Start, new LedgerEntry(JournalCodes.MarketBuy, LedgerDirection.Out, 999, "overdraw"));
        }));

        Assert.Equal(50, ledger.Balance);
    }

    [Fact]
    public void Driver_errors_surface_as_contract_errors()
    {
        _db.Execute("INSERT INTO core_state(key, value) VALUES('dup', 1)");

        var error = Assert.Throws<SimException>(() =>
            _db.Execute("INSERT INTO core_state(key, value) VALUES('dup', 2)"));

        Assert.Equal("db.sqlite", error.Code);
        Assert.IsType<Microsoft.Data.Sqlite.SqliteException>(error.InnerException);
    }

    [Fact]
    public void Posting_the_same_idempotency_key_twice_writes_one_line()
    {
        var ledger = new SqliteLedger(_db);
        ledger.Post(GameDate.Start, new LedgerEntry(JournalCodes.StartingCash, LedgerDirection.In, 500, "start"));

        var first = ledger.Post(
            GameDate.Start,
            new LedgerEntry(JournalCodes.FarmRent, LedgerDirection.Out, 60, "rent", "rent:plot:1"));
        var second = ledger.Post(
            GameDate.Start,
            new LedgerEntry(JournalCodes.FarmRent, LedgerDirection.Out, 60, "rent", "rent:plot:1"));

        Assert.Equal(first, second);
        Assert.Equal(440, ledger.Balance);
        Assert.Single(ledger.Recent(20), line => line.JournalCode == JournalCodes.FarmRent);
    }

    [Fact]
    public void Different_keys_post_separately()
    {
        var ledger = new SqliteLedger(_db);
        ledger.Post(GameDate.Start, new LedgerEntry(JournalCodes.StartingCash, LedgerDirection.In, 500, "start"));

        ledger.Post(GameDate.Start, new LedgerEntry(JournalCodes.FarmRent, LedgerDirection.Out, 60, "rent", "rent:1"));
        ledger.Post(GameDate.Start, new LedgerEntry(JournalCodes.FarmRent, LedgerDirection.Out, 60, "rent", "rent:2"));

        Assert.Equal(380, ledger.Balance);
    }

    [Fact]
    public void Transaction_rolls_back_every_statement_on_failure()
    {
        var ledger = new SqliteLedger(_db);

        Assert.Throws<InvalidOperationException>(() => _db.Transaction(() =>
        {
            ledger.Post(GameDate.Start, new LedgerEntry(JournalCodes.StartingCash, LedgerDirection.In, 500, "in"));
            throw new InvalidOperationException("boom");
        }));

        Assert.Equal(0, ledger.Balance);
    }
}
