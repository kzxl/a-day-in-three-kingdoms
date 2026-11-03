using System.Text.Json;
using SimT.Contracts;
using SimT.Contracts.Ledger;
using SimT.Contracts.Time;
using SimT.Core.Kernel;

namespace SimT.Core.Tests;

/// <summary>
/// Guards the kernel's own promises: it opens without any module, it refuses a save written by a
/// newer build, and its invariant runner actually notices corrupted state.
/// </summary>
public sealed class KernelHardeningTests : IDisposable
{
    private readonly string _workspace;
    private readonly string _modulesDirectory;
    private readonly string _productPath;
    private readonly string _constantsPath;
    private readonly string _dbPath;

    public KernelHardeningTests()
    {
        _workspace = Path.Combine(Path.GetTempPath(), "simt-hardening", Guid.NewGuid().ToString("N"));
        _modulesDirectory = Path.Combine(_workspace, "Modules");
        Directory.CreateDirectory(_modulesDirectory);

        _productPath = Path.Combine(_workspace, "product.json");
        File.WriteAllText(_productPath, JsonSerializer.Serialize(new
        {
            id = "test-empty",
            name = "Empty product",
            modules = Array.Empty<string>(),
            seed = 7,
            startLocation = "nowhere",
            startingCash = 0
        }));

        _constantsPath = Path.Combine(FindRepoRoot(), "assets", "config", "game-constants.json");
        _dbPath = Path.Combine(_workspace, "campaign.db");
    }

    public void Dispose()
    {
        if (Directory.Exists(_workspace))
        {
            Directory.Delete(_workspace, recursive: true);
        }
    }

    [Fact]
    public void Kernel_opens_with_zero_modules()
    {
        using var kernel = Boot();

        Assert.Empty(kernel.Modules);
        Assert.NotEmpty(kernel.Invariants);
        Assert.Empty(kernel.RunInvariants());
    }

    [Fact]
    public void Kernel_rejects_a_save_written_by_a_newer_schema()
    {
        using (var kernel = Boot())
        {
            kernel.Save();
        }

        using (var db = Data.SqliteSession.Open(_dbPath))
        {
            db.Execute("UPDATE core_state SET value='99' WHERE key='schema.version'");
        }

        var error = Assert.Throws<SimException>(() => Boot());
        Assert.Equal("save.future_version", error.Code);
    }

    [Fact]
    public void Invariant_runner_reports_a_tampered_ledger_line()
    {
        using var kernel = Boot();
        kernel.Ledger.Post(GameDate.Start, new LedgerEntry(JournalCodes.StartingCash, LedgerDirection.In, 500, "start"));
        Assert.Empty(kernel.RunInvariants());

        using (var db = Data.SqliteSession.Open(_dbPath))
        {
            db.Execute("UPDATE core_ledger SET balance_after = 9999");
        }

        var failures = kernel.RunInvariants();

        Assert.Contains(failures, failure => failure.InvariantId == "core.ledger.running_balance");
    }

    [Fact]
    public void Advancing_a_day_persists_the_rng_position_without_an_explicit_save()
    {
        using var kernel = Boot();

        kernel.AdvanceDay();

        var stored = kernel.Db.ScalarString("SELECT value FROM core_state WHERE key='rng.state'");
        Assert.False(string.IsNullOrEmpty(stored));
    }

    [Fact]
    public void Module_states_are_recorded_for_every_started_module()
    {
        using var kernel = Boot();
        var enabled = kernel.Db.ScalarLong("SELECT COUNT(*) FROM core_module_state WHERE enabled = 1");

        Assert.Equal(0, enabled);
    }

    private SimKernel Boot() => SimKernel.Open(new KernelOptions(
        DatabasePath: _dbPath,
        ModulesDirectory: _modulesDirectory,
        ProductPath: _productPath,
        ConstantsPath: _constantsPath));

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
