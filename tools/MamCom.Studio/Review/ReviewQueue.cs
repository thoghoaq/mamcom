namespace MamCom.Studio.Review;

/// <summary>Where a person stands in a list of things to check: what comes after this one, and how many are still waiting.</summary>
/// <param name="Next">The next one still waiting after the current one, going round to the start of the list; null when none is left.</param>
/// <param name="Waiting">How many are still waiting, the current one included when it is waiting.</param>
public sealed record ReviewQueue<T>(T? Next, int Waiting)
    where T : class
{
    /// <summary>
    /// Walks <paramref name="ordered"/> (in the order the list page shows it) from just after the current one and round, so a person
    /// who works down the list never meets one twice, and one who skipped some comes back to them at the end.
    /// </summary>
    public static ReviewQueue<T> Of(IReadOnlyList<T> ordered, Func<T, bool> isCurrent, Func<T, bool> isWaiting)
    {
        ArgumentNullException.ThrowIfNull(ordered);
        ArgumentNullException.ThrowIfNull(isCurrent);
        ArgumentNullException.ThrowIfNull(isWaiting);

        var here = -1;
        for (var i = 0; i < ordered.Count; i++)
        {
            if (isCurrent(ordered[i]))
            {
                here = i;
                break;
            }
        }

        T? next = null;
        for (var step = 1; step <= ordered.Count && next is null; step++)
        {
            var item = ordered[(here + step + ordered.Count) % ordered.Count];
            if (!isCurrent(item) && isWaiting(item))
            {
                next = item;
            }
        }

        return new ReviewQueue<T>(next, ordered.Count(isWaiting));
    }
}

/// <summary>
/// A line to show on the next page after Studio moved on by itself ("Đã duyệt X."), since the page it was said on is gone.
/// One per browser tab (scoped to the circuit), taken once.
/// </summary>
public sealed class ReviewFlash
{
    private (string Message, string Tone)? _line;

    public void Set(string message, string tone) => _line = (message, tone);

    public (string Message, string Tone)? Take()
    {
        var line = _line;
        _line = null;
        return line;
    }
}
