namespace SimT.Contracts.Data;

/// <summary>
/// Metadata describing an immutable state checkpoint of a campaign.
/// </summary>
public sealed record CheckpointInfo(
    string Path,
    string Label,
    string CreatedUtc,
    long SizeBytes);

/// <summary>
/// Campaign checkpoint and quicksave subsystem contract.
/// Enables deterministic point-in-time snapshots and rollbacks without corrupting the active save.
/// </summary>
public interface ICheckpointManager
{
    CheckpointInfo CreateCheckpoint(string label);

    IReadOnlyList<CheckpointInfo> ListCheckpoints();

    void RestoreCheckpoint(string checkpointPath);

    void PruneCheckpoints(int keep = 10);
}
