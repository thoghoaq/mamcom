using Microsoft.JSInterop;

namespace MamCom.Data.Storage;

/// <summary>
/// <see cref="IKeyValueStorage"/> over the browser's localStorage. The browser may refuse it (a private window, blocked site data,
/// a full quota). Then the value is kept in memory for the life of the page and <see cref="SetAsync"/> says so, instead of the app
/// failing: the person can still use it, and the app can tell them nothing will be there next time.
/// </summary>
public sealed class LocalStorageKeyValueStorage(IJSRuntime js) : IKeyValueStorage
{
    private const string ProbeKey = "mamcom.probe";

    /// <summary>What could not be written to localStorage. It wins over localStorage on read, so the page sees its own latest value.</summary>
    private readonly Dictionary<string, string> _unsaved = new(StringComparer.Ordinal);

    private bool? _available;

    public bool IsPersistent => _available == true && _unsaved.Count == 0;

    public async ValueTask<string?> GetAsync(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (_unsaved.TryGetValue(key, out var value))
        {
            return value;
        }

        if (!await IsAvailableAsync().ConfigureAwait(false))
        {
            return null;
        }

        try
        {
            return await js.InvokeAsync<string?>("localStorage.getItem", key).ConfigureAwait(false);
        }
        catch (JSException)
        {
            return null;
        }
    }

    public async ValueTask<bool> SetAsync(string key, string value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        if (await IsAvailableAsync().ConfigureAwait(false))
        {
            try
            {
                await js.InvokeVoidAsync("localStorage.setItem", key, value).ConfigureAwait(false);
                _unsaved.Remove(key);
                return true;
            }
            catch (JSException)
            {
                // Out of space, or the browser changed its mind: keep it for this visit.
            }
        }

        _unsaved[key] = value;
        return false;
    }

    public async ValueTask RemoveAsync(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        _unsaved.Remove(key);
        if (!await IsAvailableAsync().ConfigureAwait(false))
        {
            return;
        }

        try
        {
            await js.InvokeVoidAsync("localStorage.removeItem", key).ConfigureAwait(false);
        }
        catch (JSException)
        {
            // Nothing to remove if it cannot be read.
        }
    }

    public async ValueTask<bool> RequestPersistenceAsync()
    {
        try
        {
            return await js.InvokeAsync<bool>("navigator.storage.persist").ConfigureAwait(false);
        }
        catch (JSException)
        {
            return false;   // an old browser with no storage manager
        }
    }

    /// <summary>Writes and removes a probe once: some browsers have localStorage and still refuse every write.</summary>
    private async ValueTask<bool> IsAvailableAsync()
    {
        if (_available is { } known)
        {
            return known;
        }

        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", ProbeKey, "1").ConfigureAwait(false);
            await js.InvokeVoidAsync("localStorage.removeItem", ProbeKey).ConfigureAwait(false);
            _available = true;
        }
        catch (JSException)
        {
            _available = false;
        }

        return _available.Value;
    }
}
