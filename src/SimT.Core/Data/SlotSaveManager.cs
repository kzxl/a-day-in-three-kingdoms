using System.Text.Json;
using SimT.Contracts;
using SimT.Contracts.Data;

namespace SimT.Core.Data;

/// <summary>
/// Sovereign multi-slot save game manager.
/// Manages campaign persistence via atomic SQLite snapshot files and isolated JSON metadata.
/// </summary>
public sealed class SlotSaveManager : ISlotSaveManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly IDbSession _db;
    private readonly string _databasePath;
    private readonly string _savesDirectory;

    public SlotSaveManager(IDbSession db, string databasePath, string savesDirectory)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _databasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));
        _savesDirectory = savesDirectory ?? throw new ArgumentNullException(nameof(savesDirectory));
    }

    public IReadOnlyList<SaveSlotMetadata> ListSlots()
    {
        if (!Directory.Exists(_savesDirectory))
        {
            return Array.Empty<SaveSlotMetadata>();
        }

        var slotDirs = Directory.GetDirectories(_savesDirectory, "slot_*");
        var list = new List<SaveSlotMetadata>();

        foreach (var dir in slotDirs)
        {
            var metaFile = Path.Combine(dir, "metadata.json");
            if (File.Exists(metaFile))
            {
                try
                {
                    var json = File.ReadAllText(metaFile);
                    var metadata = JsonSerializer.Deserialize<SaveSlotMetadata>(json, JsonOptions);
                    if (metadata != null)
                    {
                        list.Add(metadata);
                    }
                }
                catch
                {
                    // Skip corrupt slot metadata without crashing the listing
                }
            }
        }

        list.Sort((a, b) => b.SavedAtUtc.CompareTo(a.SavedAtUtc));
        return list;
    }

    public SaveSlotMetadata? GetSlotMetadata(int slotId)
    {
        var metaFile = Path.Combine(GetSlotDirectory(slotId), "metadata.json");
        if (!File.Exists(metaFile))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(metaFile);
            return JsonSerializer.Deserialize<SaveSlotMetadata>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public void SaveToSlot(int slotId, string slotName, SaveSlotMetadata metadata)
    {
        if (!File.Exists(_databasePath))
        {
            throw new SimException("save.db_not_found", $"Current database file not found: {_databasePath}");
        }

        var slotDir = GetSlotDirectory(slotId);
        Directory.CreateDirectory(slotDir);

        // Checkpoint WAL to flush state into the main file
        _db.Execute("PRAGMA wal_checkpoint(TRUNCATE);");

        var targetDb = Path.Combine(slotDir, "campaign.db");
        File.Copy(_databasePath, targetDb, overwrite: true);

        var normalizedMetadata = metadata with
        {
            SlotId = slotId,
            SlotName = slotName,
            SavedAtUtc = DateTime.UtcNow
        };

        var metaFile = Path.Combine(slotDir, "metadata.json");
        var json = JsonSerializer.Serialize(normalizedMetadata, JsonOptions);
        File.WriteAllText(metaFile, json);
    }

    public bool LoadFromSlot(int slotId)
    {
        var slotDir = GetSlotDirectory(slotId);
        var sourceDb = Path.Combine(slotDir, "campaign.db");

        if (!File.Exists(sourceDb))
        {
            return false;
        }

        _db.Execute("PRAGMA wal_checkpoint(TRUNCATE);");
        File.Copy(sourceDb, _databasePath, overwrite: true);
        return true;
    }

    public void DeleteSlot(int slotId)
    {
        var slotDir = GetSlotDirectory(slotId);
        if (Directory.Exists(slotDir))
        {
            Directory.Delete(slotDir, recursive: true);
        }
    }

    public void QuickSave(SaveSlotMetadata metadata)
    {
        SaveToSlot(0, "QuickSave", metadata);
    }

    public bool QuickLoad()
    {
        return LoadFromSlot(0);
    }

    private string GetSlotDirectory(int slotId) =>
        Path.Combine(_savesDirectory, $"slot_{slotId}");
}
