using SimT.Contracts;
using SimT.Contracts.Time;
using SimT.Core.Events;
using SimT.Core.Time;

namespace SimT.Core.Tests;

/// <summary>
/// Time is the currency of every action: the policy is data, the charge moves the calendar, and a
/// failed action takes its days back with it.
/// </summary>
public sealed class ActionTimeTests : IDisposable
{
    private readonly string _path;
    private readonly Data.SqliteSession _db;
    private readonly EventPublisher _events;

    public ActionTimeTests()
    {
        _path = Path.Combine(Path.GetTempPath(), "simt-action-time", Guid.NewGuid().ToString("N") + ".db");
        _db = Data.SqliteSession.Open(_path);
        new Data.MigrationRunner(_db).Apply(typeof(Kernel.SimKernel).Assembly, "core");
        _events = new EventPublisher();
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
    public void The_shipped_table_prices_the_known_actions()
    {
        var policy = ActionCostPolicy.Load(Path.Combine(FindRepoRoot(), "assets", "config", "action-costs.json"));

        Assert.Equal(2, policy.Of(ActionCodes.TradeBuy).Days);
        Assert.Equal(2, policy.Of(ActionCodes.TradeSell).Days);
        Assert.Equal(1, policy.Of(ActionCodes.CaravanSend).Days);
        Assert.Equal(2, policy.Of(ActionCodes.FarmHarvest).Days);
        Assert.Equal(0, policy.Of(ActionCodes.WarehouseMove).Days);
    }

    [Fact]
    public void An_unknown_action_costs_the_default()
    {
        var policy = ActionCostPolicy.Load(Path.Combine(FindRepoRoot(), "assets", "config", "action-costs.json"));

        Assert.Equal(policy.Fallback, policy.Of("dlc.unknown"));
        Assert.Equal(1, policy.Of("dlc.unknown").Days);
    }

    [Fact]
    public void A_bad_cost_table_is_rejected()
    {
        var file = Path.Combine(Path.GetTempPath(), "bad-costs-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(file, """{ "default": { "days": 1 }, "actions": { "x": { "days": 99 } } }""");

        try
        {
            var error = Assert.Throws<SimException>(() => ActionCostPolicy.Load(file));
            Assert.Equal("config.invalid_value", error.Code);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void Charging_an_action_advances_exactly_its_days()
    {
        var clock = new SimClock(_db, _events);
        var time = new ActionTime(clock, ShippedPolicy(), _events);
        var before = clock.Today;

        time.Charge(ActionCodes.TradeBuy);

        Assert.Equal(before.AddDays(2), clock.Today);
    }

    [Fact]
    public void A_free_action_does_not_move_the_clock()
    {
        var clock = new SimClock(_db, _events);
        var policy = ActionCostPolicy.Load(Path.Combine(FindRepoRoot(), "assets", "config", "action-costs.json"));
        var time = new ActionTime(clock, policy, _events);
        var before = clock.Today;

        time.Charge(ActionCodes.WarehouseMove);

        Assert.Equal(before, clock.Today);
    }

    [Fact]
    public void A_charge_rolls_back_when_the_action_fails()
    {
        var clock = new SimClock(_db, _events);
        var time = new ActionTime(clock, ActionCostPolicy.Default(), _events);
        var before = clock.Today;

        Assert.Throws<InvalidOperationException>(() => _db.Transaction(() =>
        {
            time.Charge(ActionCodes.TradeBuy);
            throw new InvalidOperationException("trade failed");
        }));

        // The in-memory clock moved, but the save must not have: a failed action costs nothing.
        var reloaded = new SimClock(_db, _events);
        Assert.Equal(before, reloaded.Today);
    }

    [Fact]
    public void A_charge_is_announced_for_the_report()
    {
        var clock = new SimClock(_db, _events);
        var time = new ActionTime(clock, ShippedPolicy(), _events);
        ActionTimeCharged? charged = null;
        _events.Subscribe<ActionTimeCharged>(evt => charged = evt);

        time.Charge(ActionCodes.TradeBuy);

        Assert.NotNull(charged);
        Assert.Equal(ActionCodes.TradeBuy, charged.Value.ActionCode);
        Assert.Equal(2, charged.Value.Cost.Days);
    }

    private static ActionCostPolicy ShippedPolicy() =>
        ActionCostPolicy.Load(Path.Combine(FindRepoRoot(), "assets", "config", "action-costs.json"));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SimT.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("SimT.sln not found above the test output directory.");
    }
}
