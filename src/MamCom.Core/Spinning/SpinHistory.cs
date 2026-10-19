using MamCom.Core.Models;

namespace MamCom.Core.Spinning;

/// <summary>The most recent spins, newest first, for the "previous tray" button (SPEC §5.5).</summary>
public sealed record SpinHistory(IReadOnlyList<SpinResult> Items)
{
    public static SpinHistory Empty { get; } = new([]);

    /// <summary>A copy with <paramref name="result"/> at the front, cut to <paramref name="capacity"/> entries.</summary>
    public SpinHistory Push(SpinResult result, int capacity)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);

        return new SpinHistory([result, .. Items.Take(capacity - 1)]);
    }
}
