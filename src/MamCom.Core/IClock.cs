namespace MamCom.Core;

/// <summary>Source of "now" and "today" for Core; nothing in Core reads the system clock directly.</summary>
public interface IClock
{
    DateTimeOffset Now { get; }

    /// <summary>The local calendar date of <see cref="Now"/>.</summary>
    DateOnly Today { get; }
}
