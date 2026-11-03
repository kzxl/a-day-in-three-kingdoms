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
                _db.Execute(sql);
                _db.Execute(
                    "INSERT INTO core_migration(module_id, migration_id, applied_at) VALUES(@module, @id, @at)",
                    new DbArgs()
                        .Add("@module", moduleId)
                        .Add("@id", id)
                        .Add("@at", DateTime.UtcNow.ToString("O")));
            });
        }

        return pending.Count;
    }

    /// <summary>
    /// Migrations that have not run yet for this module. Used before applying anything, so the
    /// kernel knows whether it must take a backup first.
    /// </summary>
    public IReadOnlyList<(string ResourceName, string Id)> Pending(Assembly assembly, string moduleId)
    {
        var applied = HasMigrationTable()
            ? new HashSet<string>(
                _db.Query(
                    "SELECT migration_id FROM core_migration WHERE module_id=@module",
                    row => row.GetString(0),
                    DbArgs.One("@module", moduleId)),
                StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);

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
