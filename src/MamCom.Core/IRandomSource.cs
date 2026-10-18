namespace MamCom.Core;

/// <summary>All randomness in Core goes through this interface so results can be reproduced from a seed.</summary>
public interface IRandomSource
{
    /// <summary>Uniform value in [0, 1).</summary>
    double NextDouble();

    /// <summary>Uniform integer in [0, <paramref name="maxExclusive"/>). <paramref name="maxExclusive"/> must be positive.</summary>
    int NextInt(int maxExclusive);
}
