using SimT.Contracts.Data;

namespace SimT.Core.Data;

/// <summary>
/// Copies the save file before a migration runs, because a migration is the one operation that
/// can leave a campaign unreadable. The WAL is checkpointed first so the copied file contains
/// every committed row.
/// </summary>
public static class DatabaseBackup
{
    public const int DefaultKeep = 5;

    public static string? Create(IDbSession db, string databasePath, string backupDirectory, int keep = DefaultKeep)
    {
        if (!File.Exists(databasePath))
        {
            return null;
        }

        Directory.CreateDirectory(backupDirectory);

        // Single-process kernel: checkpointing folds the WAL into the main file so a plain copy
        // is a complete snapshot.
        db.Execute("PRAGMA wal_checkpoint(TRUNCATE);");

        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
        var target = Path.Combine(backupDirectory, $"campaign-{stamp}.db");
        File.Copy(databasePath, target, overwrite: true);

        Prune(backupDirectory, keep);
        return target;
    }

    /// <summary>Keeps the newest <paramref name="keep"/> snapshots and deletes the rest.</summary>
    public static void Prune(string backupDirectory, int keep)
    {
        var files = Directory.GetFiles(backupDirectory, "campaign-*.db");
        Array.Sort(files, StringComparer.Ordinal);

        for (var index = 0; index < files.Length - keep; index++)
        {
            File.Delete(files[index]);
        }
    }
}
