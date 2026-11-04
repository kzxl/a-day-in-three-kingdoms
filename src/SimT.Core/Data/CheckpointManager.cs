using SimT.Contracts;
using SimT.Contracts.Data;

namespace SimT.Core.Data;

/// <summary>
/// Manages campaign checkpoints and quicksaves.
/// Flushes the SQLite WAL before taking a point-in-time snapshot.
/// </summary>
public sealed class CheckpointManager : ICheckpointManager
{
    private readonly IDbSession _db;
    private readonly string _databasePath;
    private readonly string _checkpointDirectory;

    public CheckpointManager(IDbSession db, string databasePath, string checkpointDirectory)
    {
        _db = db;
        _databasePath = databasePath;
        _checkpointDirectory = checkpointDirectory;
    }

    public CheckpointInfo CreateCheckpoint(string label)
    {
        if (!File.Exists(_databasePath))
        {
            throw new SimException("checkpoint.not_found", $"Database file not found: {_databasePath}");
        }

        Directory.CreateDirectory(_checkpointDirectory);

        // Fold WAL into the main file before copying
        _db.Execute("PRAGMA wal_checkpoint(TRUNCATE);");

        var cleanLabel = string.Concat(label.Split(Path.GetInvalidFileNameChars())).Replace(' ', '_');
        if (string.IsNullOrWhiteSpace(cleanLabel))
        {
            cleanLabel = "snapshot";
        }

        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var filename = $"checkpoint-{cleanLabel}-{stamp}.db";
        var target = Path.Combine(_checkpointDirectory, filename);

        File.Copy(_databasePath, target, overwrite: true);

        var fileInfo = new FileInfo(target);
        return new CheckpointInfo(target, cleanLabel, fileInfo.CreationTimeUtc.ToString("O"), fileInfo.Length);
    }

    public IReadOnlyList<CheckpointInfo> ListCheckpoints()
    {
        if (!Directory.Exists(_checkpointDirectory))
        {
            return Array.Empty<CheckpointInfo>();
        }

        var files = Directory.GetFiles(_checkpointDirectory, "checkpoint-*.db");
        var list = new List<CheckpointInfo>(files.Length);

        foreach (var file in files)
        {
            var info = new FileInfo(file);
            var name = Path.GetFileNameWithoutExtension(file);
            var parts = name.Split('-');
            var label = parts.Length >= 2 ? parts[1] : "checkpoint";
            list.Add(new CheckpointInfo(file, label, info.CreationTimeUtc.ToString("O"), info.Length));
        }

        list.Sort((a, b) => string.CompareOrdinal(b.CreatedUtc, a.CreatedUtc));
        return list;
    }

    public void RestoreCheckpoint(string checkpointPath)
    {
        if (!File.Exists(checkpointPath))
        {
            throw new SimException("checkpoint.restore_missing", $"Checkpoint not found: {checkpointPath}");
        }

        _db.Execute("PRAGMA wal_checkpoint(TRUNCATE);");
        File.Copy(checkpointPath, _databasePath, overwrite: true);
    }

    public void PruneCheckpoints(int keep = 10)
    {
        if (!Directory.Exists(_checkpointDirectory))
        {
            return;
        }

        var files = Directory.GetFiles(_checkpointDirectory, "checkpoint-*.db");
        Array.Sort(files, StringComparer.Ordinal);

        for (var i = 0; i < files.Length - keep; i++)
        {
            try
            {
                File.Delete(files[i]);
            }
            catch
            {
                // Best-effort cleanup
            }
        }
    }
}
