namespace MamCom.Data.Storage;

/// <summary>
/// Small strings kept on the device under a key: in a browser, localStorage. What the app remembers (the family, the last meals,
/// the version of the data it holds) goes through this, so a test can stand in for the browser.
/// </summary>
public interface IKeyValueStorage
{
    /// <summary>
    /// False when the browser keeps nothing between visits (a private window, blocked site data). Values then live only as long
    /// as the page, and the app should say so.
    /// </summary>
    bool IsPersistent { get; }

    /// <returns>The value, or null when there is none.</returns>
    ValueTask<string?> GetAsync(string key);

    /// <returns>True when the value is kept for next time; false when it could only be kept for as long as the page lives.</returns>
    ValueTask<bool> SetAsync(string key, string value);

    ValueTask RemoveAsync(string key);

    /// <summary>
    /// Asks the browser not to throw the data away when it is short of space or, on some phones, after a week unused. Best effort.
    /// </summary>
    /// <returns>True when the browser agreed.</returns>
    ValueTask<bool> RequestPersistenceAsync();
}

/// <summary>Keeps everything in memory. The stand-in for the browser in tests, and what the app falls back to when the browser keeps nothing.</summary>
public sealed class MemoryKeyValueStorage : IKeyValueStorage
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

    public bool IsPersistent => false;

    public ValueTask<string?> GetAsync(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        return ValueTask.FromResult(_values.GetValueOrDefault(key));
    }

    public ValueTask<bool> SetAsync(string key, string value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        _values[key] = value;
        return ValueTask.FromResult(false);
    }

    public ValueTask RemoveAsync(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        _values.Remove(key);
        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> RequestPersistenceAsync() => ValueTask.FromResult(false);
}
