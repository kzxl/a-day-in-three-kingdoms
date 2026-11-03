using System.Text.Json;
using SimT.Contracts.Data;
using SimT.Core.Data;
using SimT.Core.Kernel;

namespace SimT.Core.Tests;

/// <summary>
/// A migration is the only operation that can leave a save unreadable, so it must be preceded
/// by a snapshot — and only then, because copying on every open would be noise.
/// </summary>
public sealed class BackupTests : IDisposable
{
    private readonly string _workspace;
    private readonly string _modules;
    private readonly string _product;
    private readonly string _constants;
    private readonly string _backups;

    public BackupTests()
    {
        _workspace = Path.Combine(Path.GetTempPath(), "simt-backup", Guid.NewGuid().ToString("N"));
        _modules = Path.Combine(_workspace, "Modules");
        _backups = Path.Combine(_workspace, "backups");
        Directory.CreateDirectory(_modules);

        _product = Path.Combine(_workspace, "product.json");
        File.WriteAllText(_product, JsonSerializer.Serialize(new
        {
            id = "test-empty",
            name = "Empty product",
            modules = Array.Empty<string>(),
            seed = 3,
            startLocation = "nowhere",
            startingCash = 0
        }));

        _constants = Path.Combine(FindRepoRoot(), "assets", "config", "game-constants.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_workspace))
        {
            Directory.Delete(_workspace, recursive: true);
        }
    }

    [Fact]
    public void A_fresh_save_is_not_backed_up()
    {
        using (var kernel = Boot())
        {
            kernel.Save();
        }

        Assert.False(Directory.Exists(_backups) && Directory.GetFiles(_backups).Length > 0);
    }

    [Fact]
    public void Reopening_a_fully_migrated_save_creates_no_backup()
    {
        using (var first = Boot())
        {
            first.Save();
        }

        using (var second = Boot())
        {
            second.Save();
        }

        Assert.False(Directory.Exists(_backups) && Directory.GetFiles(_backups).Length > 0);
    }

    [Fact]
    public void A_pending_migration_takes_one_snapshot_before_it_runs()
    {
        using (var kernel = Boot())
        {
            kernel.Save();
        }

        // Forget that the core schema ran; the next open must back up, then re-apply it.
        using (var db = SqliteSession.Open(DatabasePath))
        {
            db.Execute("DELETE FROM core_migration WHERE migration_id = '0001-core'");
        }

        using (var kernel = Boot())
        {
            kernel.Save();
        }

        var snapshots = Directory.GetFiles(_backups, "campaign-*.db");
        Assert.Single(snapshots);
        Assert.True(new FileInfo(snapshots[0]).Length > 0);
    }

    [Fact]
    public void Pruning_keeps_only_the_newest_snapshots()
    {
        Directory.CreateDirectory(_backups);
        for (var index = 1; index <= 8; index++)
        {
            File.WriteAllText(Path.Combine(_backups, $"campaign-2026010{index}-000000-000.db"), "x");
        }

        DatabaseBackup.Prune(_backups, keep: 5);

        var remaining = Directory.GetFiles(_backups, "campaign-*.db");
        Assert.Equal(5, remaining.Length);
        Assert.DoesNotContain(remaining, path => path.EndsWith("campaign-20260101-000000-000.db", StringComparison.Ordinal));
    }

    private string DatabasePath => Path.Combine(_workspace, "campaign.db");

    private SimKernel Boot() => SimKernel.Open(new KernelOptions(
        DatabasePath: DatabasePath,
        ModulesDirectory: _modules,
        ProductPath: _product,
        ConstantsPath: _constants,
        BackupDirectory: _backups));

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
