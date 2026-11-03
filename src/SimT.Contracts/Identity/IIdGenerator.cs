namespace SimT.Contracts.Identity;

/// <summary>
/// Deterministic identifier source backed by a per-campaign sequence table. Modules never
/// call <c>Guid.NewGuid()</c> for domain rows: same seed + same actions must give the same ids.
/// </summary>
public interface IIdGenerator
{
    long Next(string scope);

    string NextId(string prefix, string scope);
}

/// <summary>
/// Seeded random source. <see cref="System.Random"/> without an explicit seed is banned in
/// module code; a replay must reproduce every roll.
/// </summary>
public interface IRng
{
    ulong NextULong();

    int Next(int minInclusive, int maxExclusive);

    double NextDouble();

    /// <summary>Current internal state, persisted in the save so a reload resumes the same stream.</summary>
    ulong State { get; }

    void Reseed(ulong state);
}
