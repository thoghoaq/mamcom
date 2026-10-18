namespace MamCom.Core;

/// <summary>
/// SplitMix64 generator. It is implemented here, not taken from <see cref="Random"/>, so a seed yields the
/// same sequence on every runtime version and platform (browser WebAssembly included).
/// </summary>
public sealed class SeededRandomSource(ulong seed) : IRandomSource
{
    private ulong _state = seed;

    public ulong NextUInt64()
    {
        _state += 0x9E3779B97F4A7C15UL;
        var z = _state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));

    public int NextInt(int maxExclusive)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxExclusive);

        var bound = (ulong)maxExclusive;
        // Rejecting the first (2^64 mod bound) values removes modulo bias.
        var threshold = (0UL - bound) % bound;
        while (true)
        {
            var value = NextUInt64();
            if (value >= threshold)
            {
                return (int)(value % bound);
            }
        }
    }
}
