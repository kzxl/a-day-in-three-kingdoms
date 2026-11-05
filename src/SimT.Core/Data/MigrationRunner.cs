using System.Reflection;
using SimT.Contracts;
using SimT.Contracts.Data;

namespace SimT.Core.Data;

/// <summary>
/// Applies <c>migrations/*.sql</c> embedded in each module assembly, in ordinal filename
/// order, recording what ran in <c>core_migration</c>. A module can only migrate what it
/// owns because only its own assembly resources are read.
/// </summary>
public sealed class MigrationRunner
{
    private const string Marker = ".migrations.";

    private readonly IDbSession _db;

    public MigrationRunner(IDbSession db) => _db = db;

    public int Apply(Assembly assembly, string moduleId)
    {
        EnsureMigrationTable();
        var pending = Pending(assembly, moduleId);

        foreach (var (resourceName, id) in pending)
        {
            var sql = ReadResource(assembly, resourceName);
            _db.Transaction(() =>
            {
                ExecuteMigrationSql(sql);
                _db.Execute(
                    "INSERT INTO core_migration(module_id, migration_id, applied_at) VALUES(@module, @id, @at)",
                    new DbArgs()
                        .Add("@module", moduleId)
                        .Add("@id", id)
                        .Add("@at", DateTime.UtcNow.ToString("O")));
            });
        }

        // Align migration module ownership when modules are consolidated
        var resources = ListResources(assembly);
        foreach (var res in resources)
        {
            _db.Execute(
                "UPDATE OR IGNORE core_migration SET module_id=@module WHERE migration_id=@id AND module_id != @module",
                new DbArgs().Add("@module", moduleId).Add("@id", res.Id));
        }

        return pending.Count;
    }

    private void ExecuteMigrationSql(string sql)
    {
        try
        {
            _db.Execute(sql);
        }
        catch (SimException ex) when (ex.Message.Contains("duplicate column name", StringComparison.OrdinalIgnoreCase))
        {
            // When existing database schemas already contain columns added by incremental migrations,
            // execute statements individually and bypass already-existing column additions.
            var statements = sql.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var stmt in statements)
            {
                if (string.IsNullOrWhiteSpace(stmt)) continue;
                try
                {
                    _db.Execute(stmt);
                }
                catch (SimException stmtEx) when (stmtEx.Message.Contains("duplicate column name", StringComparison.OrdinalIgnoreCase))
                {
                    // Column already present in existing database, safe to proceed
                }
            }
        }
    }

    /// <summary>
    /// Migrations that have not run yet for this module. Used before applying anything, so the
    /// kernel knows whether it must take a backup first.
    /// </summary>
    public IReadOnlyList<(string ResourceName, string Id)> Pending(Assembly assembly, string moduleId)
    {
        if (!HasMigrationTable())
        {
            return ListResources(assembly);
        }

        var applied = new HashSet<string>(
            _db.Query("SELECT migration_id FROM core_migration", row => row.GetString(0)),
            StringComparer.Ordinal);

        return ListResources(assembly)
            .Where(item => !applied.Contains(item.Id))
            .ToList();
    }

    public bool HasMigrationTable() =>
        _db.ScalarLong("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='core_migration'") > 0;

    public static IReadOnlyList<(string ResourceName, string Id)> ListResources(Assembly assembly) =>
        assembly.GetManifestResourceNames()
            .Where(name => name.Contains(Marker, StringComparison.Ordinal)
                           && name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .Select(name =>
            {
                var start = name.LastIndexOf(Marker, StringComparison.Ordinal) + Marker.Length;
                return (ResourceName: name, Id: name[start..^".sql".Length]);
            })
            .OrderBy(item => item.Id, StringComparer.Ordinal)
            .ToList();

    private void EnsureMigrationTable()
    {
        _db.Execute(
            """
            CREATE TABLE IF NOT EXISTS core_migration (
                module_id    TEXT NOT NULL,
                migration_id TEXT NOT NULL,
                applied_at   TEXT NOT NULL,
                PRIMARY KEY (module_id, migration_id)
            );
            """);
    }

    private static string ReadResource(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName)
                           ?? throw new SimException("migration.missing", $"Resource '{resourceName}' vanished from {assembly.GetName().Name}.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
