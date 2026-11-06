using SimT.Contracts.Time;

namespace SimT.Contracts.Data;

/// <summary>
/// Lightweight metadata header attached to an individual save slot.
/// Read instantly without mounting or opening SQLite databases.
/// </summary>
public sealed record SaveSlotMetadata(
    int SlotId,
    string SlotName,
    GameDate InGameDate,
    string PlayerName,
    long CurrentCash,
    long LocationId,
    string LocationName,
    DateTime SavedAtUtc,
    string EngineVersion,
    string? ThumbnailBase64 = null);

/// <summary>
/// Multi-slot save game manager supporting quick save, autosave,
/// metadata pre-inspection, and atomic snapshot restores.
/// </summary>
public interface ISlotSaveManager
{
    IReadOnlyList<SaveSlotMetadata> ListSlots();
    SaveSlotMetadata? GetSlotMetadata(int slotId);
    void SaveToSlot(int slotId, string slotName, SaveSlotMetadata metadata);
    bool LoadFromSlot(int slotId);
    void DeleteSlot(int slotId);

    void QuickSave(SaveSlotMetadata metadata);
    bool QuickLoad();
}
