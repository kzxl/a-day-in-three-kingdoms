using SimT.Contracts.Data;
using SimT.Contracts.Diagnostics;
using SimT.Contracts.Logging;
using SimT.Contracts.Narrative;
using SimT.Contracts.Simulation;
using SimT.Contracts.Time;
using SimT.Core.Data;
using SimT.Core.Diagnostics;
using SimT.Core.Narrative;
using SimT.Core.Simulation;
using Xunit;

namespace SimT.Core.Tests;

public sealed class EngineEnhancementTests
{
    private sealed class TestLog : ILog
    {
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message, Exception? error = null) { }
    }

    private sealed class MockGameClock : IGameClock
    {
        public GameDate Today { get; private set; } = new(1, 1, 1);
        public int Turn => (Today.Year - 1) * 12 + Today.Month;
        public int EraEndYear => 10;
        public bool IsEraEnded => Today.Year >= EraEndYear;

        public GameDate Advance(int days = 1)
        {
            var dayIndex = Today.DayIndex + days;
            Today = GameDate.FromDayIndex(dayIndex);
            return Today;
        }

        public GameDate AdvanceToMonthStart()
        {
            return Advance(1);
        }
    }

    private sealed class DummyDbSession : IDbSession
    {
        public string DatabasePath => "dummy.db";
        public int Execute(string sql, DbArgs? args = null) => 0;
        public long ScalarLong(string sql, DbArgs? args = null) => 0;
        public double ScalarDouble(string sql, DbArgs? args = null) => 0.0;
        public string? ScalarString(string sql, DbArgs? args = null) => null;
        public List<T> Query<T>(string sql, Func<DbRecord, T> map, DbArgs? args = null) => new();
        public T? QueryOne<T>(string sql, Func<DbRecord, T> map, DbArgs? args = null) where T : class => null;
        public void Transaction(Action body) => body();
    }

    // ==========================================
    // 1. ASYNC SIMULATION WORKER TESTS
    // ==========================================

    [Fact]
    public async Task AsyncSimulation_AdvancesDaysAndReportsProgress()
    {
        var clock = new MockGameClock();
        var worker = new AsyncSimulationWorker(clock, new TestLog())
        {
            Speed = SimulationSpeed.Ultra
        };

        var progressCalls = new List<int>();
        var progress = new Progress<int>(v => progressCalls.Add(v));

        var result = await worker.AdvanceDaysAsync(5, progress);

        Assert.Equal(5, result.DaysAdvanced);
        Assert.False(result.PausedByCriticalCondition);
        Assert.Equal(new GameDate(1, 1, 6), clock.Today);
    }

    [Fact]
    public async Task AsyncSimulation_PausesOnCriticalCondition()
    {
        var clock = new MockGameClock();
        var worker = new AsyncSimulationWorker(clock, new TestLog())
        {
            Speed = SimulationSpeed.Ultra
        };

        worker.RegisterCriticalPauseCondition(date =>
        {
            if (date.Day >= 4)
            {
                return (true, "TargetDayReached");
            }
            return (false, string.Empty);
        });

        var result = await worker.AdvanceDaysAsync(10);

        Assert.True(result.PausedByCriticalCondition);
        Assert.Equal("TargetDayReached", result.PauseReason);
        Assert.Equal(new GameDate(1, 1, 4), clock.Today);
    }

    [Fact]
    public async Task AsyncSimulation_PausesOnSignal()
    {
        var clock = new MockGameClock();
        var worker = new AsyncSimulationWorker(clock, new TestLog())
        {
            Speed = SimulationSpeed.Fast
        };

        var task = worker.AdvanceDaysAsync(100);
        worker.SignalPause("UserRequestedStop");
        var result = await task;

        Assert.True(result.DaysAdvanced < 100);
    }

    // ==========================================
    // 2. ENGINE PROFILER TESTS
    // ==========================================

    [Fact]
    public void EngineProfiler_MeasuresAndCalculatesAverages()
    {
        var profiler = new EngineProfiler();

        using (profiler.Scope("Core", "TestOp"))
        {
            Thread.Sleep(5);
        }

        using (profiler.Scope("Core", "TestOp"))
        {
            Thread.Sleep(5);
        }

        var snapshot = profiler.GetSnapshot("Core", "TestOp");
        Assert.NotNull(snapshot);
        Assert.Equal(2, snapshot.CallCount);
        Assert.True(snapshot.TotalMilliseconds > 5.0);
        Assert.True(snapshot.MaxMilliseconds > 0);
        Assert.True(snapshot.AvgMilliseconds > 0);

        var all = profiler.GetSnapshots();
        Assert.Single(all);

        profiler.Reset();
        Assert.Empty(profiler.GetSnapshots());
    }

    // ==========================================
    // 3. MULTI-SLOT SAVE MANAGER TESTS
    // ==========================================

    [Fact]
    public void SlotSaveManager_SavesLoadsAndDeletesSlots()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "simt_slot_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var dbPath = Path.Combine(tempDir, "source.db");
        File.WriteAllText(dbPath, "MOCK_DB_CONTENT");

        try
        {
            var manager = new SlotSaveManager(new DummyDbSession(), dbPath, Path.Combine(tempDir, "saves"));

            var metadata = new SaveSlotMetadata(
                SlotId: 1,
                SlotName: "Hero Save",
                InGameDate: new GameDate(2, 5, 10),
                PlayerName: "Trader Joe",
                CurrentCash: 50000,
                LocationId: 101,
                LocationName: "Capital City",
                SavedAtUtc: DateTime.UtcNow,
                EngineVersion: "1.0");

            manager.SaveToSlot(1, "Hero Save", metadata);

            var loadedMeta = manager.GetSlotMetadata(1);
            Assert.NotNull(loadedMeta);
            Assert.Equal("Hero Save", loadedMeta.SlotName);
            Assert.Equal("Trader Joe", loadedMeta.PlayerName);
            Assert.Equal(50000, loadedMeta.CurrentCash);
            Assert.Equal(new GameDate(2, 5, 10), loadedMeta.InGameDate);

            var list = manager.ListSlots();
            Assert.Single(list);
            Assert.Equal(1, list[0].SlotId);

            // Test Quicksave
            manager.QuickSave(metadata with { SlotName = "Quicksave" });
            Assert.Equal(2, manager.ListSlots().Count);

            // Test restore
            File.WriteAllText(dbPath, "CORRUPTED");
            var loaded = manager.LoadFromSlot(1);
            Assert.True(loaded);
            Assert.Equal("MOCK_DB_CONTENT", File.ReadAllText(dbPath));

            // Test Delete
            manager.DeleteSlot(1);
            Assert.Null(manager.GetSlotMetadata(1));
            Assert.Single(manager.ListSlots()); // quicksave remains
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    // ==========================================
    // 4. EVENT DECK ENGINE TESTS
    // ==========================================

    [Fact]
    public void EventDeckEngine_EvaluatesConditionsAndDrawsCards()
    {
        var deck = new EventDeckEngine();

        var cardA = new NarrativeEventCard(
            EventId: "ev_gold_rush",
            TitleKey: "event.gold_rush.title",
            DescriptionKey: "event.gold_rush.desc",
            BaseWeight: 10,
            Conditions: new[]
            {
                new NarrativeCondition("Cash", ConditionOperator.GreaterThan, 1000),
                new NarrativeCondition("Region", ConditionOperator.Equals, "North")
            },
            ChoiceKeys: new[] { "choice_invest", "choice_ignore" });

        var cardB = new NarrativeEventCard(
            EventId: "ev_bad_weather",
            TitleKey: "event.bad_weather.title",
            DescriptionKey: "event.bad_weather.desc",
            BaseWeight: 20,
            Conditions: new[]
            {
                new NarrativeCondition("Season", ConditionOperator.Contains, "Storm")
            },
            ChoiceKeys: new[] { "choice_take_shelter" });

        deck.RegisterCard(cardA);
        deck.RegisterCard(cardB);

        var state1 = new Dictionary<string, object>
        {
            { "Cash", 5000 },
            { "Region", "North" },
            { "Season", "Mild" }
        };

        var eligible1 = deck.EvaluateEligibleCards(state1);
        Assert.Single(eligible1);
        Assert.Equal("ev_gold_rush", eligible1[0].EventId);

        var state2 = new Dictionary<string, object>
        {
            { "Cash", 500 },
            { "Region", "North" },
            { "Season", "Stormy" }
        };

        var eligible2 = deck.EvaluateEligibleCards(state2);
        Assert.Single(eligible2);
        Assert.Equal("ev_bad_weather", eligible2[0].EventId);

        // Both eligible
        var stateBoth = new Dictionary<string, object>
        {
            { "Cash", 5000 },
            { "Region", "North" },
            { "Season", "Stormy" }
        };

        var drawn = deck.DrawEvent(stateBoth, 42);
        Assert.NotNull(drawn);
        Assert.True(drawn.EventId is "ev_gold_rush" or "ev_bad_weather");
    }
}
