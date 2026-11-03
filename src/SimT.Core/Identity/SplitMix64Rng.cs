using SimT.Contracts.Identity;

namespace SimT.Core.Identity;

/// <summary>
/// K4 — SplitMix64. Small, fast, fully deterministic and easy to persist as one 64-bit
/// state, which is all a save needs to resume the stream.
/// </summary>
public sealed class SplitMix64Rng : IRng
{
    private const ulong Gamma = 0x9E3779B97F4A7C15UL;

    private ulong _state;

    public SplitMix64Rng(ulong seed) => _state = seed == 0 ? 0x0123456789ABCDEFUL : seed;

    public ulong State => _state;

    public void Reseed(ulong state) => _state = state == 0 ? 0x0123456789ABCDEFUL : state;

    public ulong NextULong()
    {
        _state += Gamma;
        var z = _state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    public int Next(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
        {
            return minInclusive;
        }

        var span = (ulong)(long)(maxExclusive - minInclusive);
        return (int)(minInclusive + (long)(NextULong() % span));
    }

    public double NextDouble() => (NextULong() >> 11) * (1.0 / (1UL << 53));
}
