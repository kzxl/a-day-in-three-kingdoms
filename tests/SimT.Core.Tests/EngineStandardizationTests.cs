using SimT.Contracts.Audio;
using SimT.Contracts.Commands;
using SimT.Contracts.Data;
using SimT.Contracts.Localization;
using SimT.Contracts.Simulation;
using SimT.Contracts.Time;
using SimT.Contracts.Ui;
using SimT.Core.Commands;
using SimT.Core.Data;
using SimT.Core.Kernel;
using SimT.Core.Localization;
using SimT.Core.Modules;
using SimT.Core.Simulation;

namespace SimT.Core.Tests;

public sealed class EngineStandardizationTests
{
    [Fact]
    public void Audio_null_engine_operates_safely_without_exceptions()
    {
        var audio = NullAudioEngine.Instance;
        Assert.NotNull(audio);

        // Safe no-op calls
        audio.PlaySfx("sfx.coin", 1.2f, 0.8f);
        audio.PlayMusic("bgm.theme", loop: true, fadeDurationSeconds: 0.5f);
        audio.StopMusic(0.2f);
        audio.SetVolume(AudioBus.Master, 0.75f);
        Assert.Equal(1.0f, audio.GetVolume(AudioBus.Master));
    }

    [Fact]
    public void UiRegistry_modal_presentation_protocol_routes_correctly()
    {
        var ui = new UiRegistry();
        var captured = new List<string>();

        ui.RegisterModalPresenter("npc:modal:", cmd => captured.Add($"npc:{cmd}"));
        ui.RegisterModalPresenter("duel:", cmd => captured.Add($"duel:{cmd}"));

        Assert.True(ui.TryPresentModal("npc:modal:42"));
        Assert.True(ui.TryPresentModal("duel:99"));
        Assert.False(ui.TryPresentModal("unknown:command:1"));

        Assert.Equal(["npc:npc:modal:42", "duel:duel:99"], captured);
    }

    [Fact]
    public void Command_dispatcher_validates_and_executes_structured_commands()
    {
        var dispatcher = new CommandDispatcher();
        var dummyContext = new DummyCommandContext();

        var testCmd = new DummyIncrementCommand();
        dispatcher.Register(testCmd);

        Assert.True(dispatcher.TryGet("test.inc", out var found));
        Assert.NotNull(found);

        var preview = found.Preview(dummyContext);
        Assert.Equal("Adds 1 to counter", preview.Summary);
        Assert.Equal(1, preview.CostDays);

        // Execute valid
        var result1 = dispatcher.Dispatch("test.inc", dummyContext);
        Assert.True(result1.Success);
        Assert.Equal("Counter incremented to 1", result1.Message);

        // Validation failure on invalid context
        dummyContext.Allowed = false;
        var result2 = dispatcher.Dispatch("test.inc", dummyContext);
        Assert.False(result2.Success);
        Assert.Equal("Context disabled", result2.Message);
    }

    [Fact]
    public void Localization_service_switches_locale_and_interpolates_keys()
    {
        var loc = new LocalizationService("vi-VN");
        loc.RegisterStrings("vi-VN", new Dictionary<string, string>
        {
            ["game.greeting"] = "Xin chào {0}!",
            ["game.gold"] = "Ngân lượng: {0} Quan"
        });
        loc.RegisterStrings("en-US", new Dictionary<string, string>
        {
            ["game.greeting"] = "Greetings {0}!",
            ["game.gold"] = "Gold: {0} Quan"
        });

        Assert.Equal("Xin chào Lữ Khách!", loc.Get("game.greeting", "Lữ Khách"));
        Assert.Equal("Ngân lượng: 500 Quan", loc.Get("game.gold", 500));

        loc.SetLocale("en-US");
        Assert.Equal("Greetings Traveler!", loc.Get("game.greeting", "Traveler"));

        // Fallback to key if unknown
        Assert.Equal("unknown.token", loc.Get("unknown.token"));
    }

    [Fact]
    public void Checkpoint_manager_creates_and_prunes_snapshots()
    {
        var dir = Path.Combine(Path.GetTempPath(), "simt-checkpoint-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var dbPath = Path.Combine(dir, "campaign.db");
        var checkpointDir = Path.Combine(dir, "checkpoints");

        try
        {
            using var db = SqliteSession.Open(dbPath);
            db.Execute("CREATE TABLE core_test(id INTEGER PRIMARY KEY, val TEXT);");
            db.Execute("INSERT INTO core_test(id, val) VALUES(1, 'v1');");

            var manager = new CheckpointManager(db, dbPath, checkpointDir);

            var cp1 = manager.CreateCheckpoint("before_battle");
            Assert.True(File.Exists(cp1.Path));
            Assert.Equal("before_battle", cp1.Label);

            db.Execute("UPDATE core_test SET val='v2' WHERE id=1;");
            var cp2 = manager.CreateCheckpoint("after_battle");

            var list = manager.ListCheckpoints();
            Assert.Equal(2, list.Count);

            // Validate snapshots are independent point-in-time states
            using (var snap1 = SqliteSession.Open(cp1.Path))
            {
                Assert.Equal("v1", snap1.ScalarString("SELECT val FROM core_test WHERE id=1"));
            }
            using (var snap2 = SqliteSession.Open(cp2.Path))
            {
                Assert.Equal("v2", snap2.ScalarString("SELECT val FROM core_test WHERE id=1"));
            }

            // Pruning test
            manager.PruneCheckpoints(keep: 1);
            var remaining = manager.ListCheckpoints();
            Assert.Single(remaining);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Simulation_runner_advances_in_chunks_and_reports_progress()
    {
        var dir = Path.Combine(Path.GetTempPath(), "simt-sim-runner-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var dbPath = Path.Combine(dir, "campaign.db");
        var modulesDir = Path.Combine(dir, "Modules");
        Directory.CreateDirectory(modulesDir);

        var productPath = Path.Combine(dir, "product.json");
        File.WriteAllText(productPath, System.Text.Json.JsonSerializer.Serialize(new
        {
            id = "test-empty",
            name = "Empty product",
            modules = Array.Empty<string>(),
            seed = 42,
            startLocation = "nowhere",
            startingCash = 0
        }));

        var constantsPath = Path.Combine(FindRepoRoot(), "assets", "config", "game-constants.json");

        try
        {
            using var kernel = SimKernel.Open(new KernelOptions(
                DatabasePath: dbPath,
                ModulesDirectory: modulesDir,
                ProductPath: productPath,
                ConstantsPath: constantsPath));

            var startDate = kernel.Today;
            var progressReports = new List<SimProgress>();
            var progress = new Progress<SimProgress>(p => progressReports.Add(p));

            var endDate = await kernel.Simulation.AdvanceAsync(totalDays: 5, chunkSize: 2, progress: progress);

            Assert.Equal(startDate.AddDays(5), endDate);
            Assert.Equal(endDate, kernel.Today);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

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

    private sealed class DummyCommandContext : ICommandContext
    {
        public string ActorId => "player";
        public bool Allowed { get; set; } = true;

        public T GetCapability<T>(string capabilityId) where T : class => throw new NotImplementedException();
        public bool TryGetCapability<T>(string capabilityId, out T? capability) where T : class
        {
            capability = null;
            return false;
        }
    }

    private sealed class DummyIncrementCommand : IStructuredCommand
    {
        private int _count;

        public string CommandId => "test.inc";

        public CommandValidation CanExecute(ICommandContext context)
        {
            if (context is DummyCommandContext dummy && !dummy.Allowed)
            {
                return CommandValidation.Refuse("Context disabled");
            }
            return CommandValidation.Allowed;
        }

        public CommandPreview Preview(ICommandContext context) =>
            new("Adds 1 to counter", CostDays: 1, CostAmount: 0);

        public CommandResult Execute(ICommandContext context)
        {
            _count++;
            return CommandResult.Ok($"Counter incremented to {_count}");
        }
    }
}
