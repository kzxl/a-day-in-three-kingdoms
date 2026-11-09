namespace SimT.Contracts.Identity;

/// <summary>
/// Ergonomic extension methods for <see cref="IRng"/>.
/// </summary>
public static class RngExtensions
{
    /// <summary>
    /// Returns a non-negative random integer that is less than the specified maximum.
    /// </summary>
    public static int Next(this IRng rng, int maxExclusive) => rng.Next(0, maxExclusive);
}
