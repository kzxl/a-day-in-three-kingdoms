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
