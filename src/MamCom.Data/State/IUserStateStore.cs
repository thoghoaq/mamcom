namespace MamCom.Data.State;

/// <summary>
/// What the app remembers about this household between visits: who is in the family, the last meals, which dishes were liked
/// (CLAUDE.md: kept on the device, never sent anywhere). Each thing has a key, and a type that Core's JSON context knows.
/// A key carries the version of its shape (<c>household.v1</c>), so a later shape is a new key and the old one can be read and
/// converted instead of being misread.
/// </summary>
public interface IUserStateStore
{
    /// <summary>False when the browser keeps nothing between visits; what was saved is lost when the page closes.</summary>
    bool IsPersistent { get; }

    /// <returns>The value, or null when it was never saved or cannot be read (it is not deleted: the caller decides).</returns>
    ValueTask<T?> GetAsync<T>(string key)
        where T : class;

    /// <returns>True when the value is kept for next time; false when it could only be kept for as long as the page lives.</returns>
    ValueTask<bool> SetAsync<T>(string key, T value)
        where T : class;

    ValueTask RemoveAsync(string key);

    /// <summary>Forgets everything this store saved (the "delete my data" of the settings), and nothing else on the device.</summary>
    ValueTask ClearAsync();
}
