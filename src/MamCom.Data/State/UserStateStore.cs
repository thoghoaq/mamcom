using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using MamCom.Core.Json;
using MamCom.Data.Storage;

namespace MamCom.Data.State;

/// <summary>
/// <see cref="IUserStateStore"/> over an <see cref="IKeyValueStorage"/>. Every value is JSON in the shape of Core's own types, under
/// <c>mamcom.state.&lt;key&gt;</c>. A list of the keys is kept too, because a browser cannot be asked which keys are ours, and
/// "delete my data" must remove those and nothing else (not, for example, the version of the data bundle the app holds).
/// </summary>
public sealed partial class UserStateStore(IKeyValueStorage storage) : IUserStateStore
{
    public const string Prefix = "mamcom.state.";

    private const string IndexKey = Prefix + "@keys";

    public bool IsPersistent => storage.IsPersistent;

    public async ValueTask<T?> GetAsync<T>(string key)
        where T : class
    {
        CheckKey(key);

        var json = await storage.GetAsync(Prefix + key).ConfigureAwait(false);
        if (json is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(json, TypeInfo<T>());
        }
        catch (JsonException)
        {
            return null;   // a half-written or hand-edited value
        }
    }

    public async ValueTask<bool> SetAsync<T>(string key, T value)
        where T : class
    {
        CheckKey(key);
        ArgumentNullException.ThrowIfNull(value);

        var saved = await storage.SetAsync(Prefix + key, JsonSerializer.Serialize(value, TypeInfo<T>())).ConfigureAwait(false);

        var keys = await ReadIndexAsync().ConfigureAwait(false);
        if (!keys.Contains(key, StringComparer.Ordinal))
        {
            saved &= await storage.SetAsync(IndexKey, JsonSerializer.Serialize(keys.Append(key).Order(StringComparer.Ordinal).ToArray(), CoreJsonContext.Default.StringArray)).ConfigureAwait(false);
        }

        return saved;
    }

    public async ValueTask RemoveAsync(string key)
    {
        CheckKey(key);

        await storage.RemoveAsync(Prefix + key).ConfigureAwait(false);

        var keys = await ReadIndexAsync().ConfigureAwait(false);
        if (keys.Contains(key, StringComparer.Ordinal))
        {
            await WriteIndexAsync([.. keys.Where(k => k != key)]).ConfigureAwait(false);
        }
    }

    public async ValueTask ClearAsync()
    {
        foreach (var key in await ReadIndexAsync().ConfigureAwait(false))
        {
            await storage.RemoveAsync(Prefix + key).ConfigureAwait(false);
        }

        await storage.RemoveAsync(IndexKey).ConfigureAwait(false);
    }

    private async ValueTask<string[]> ReadIndexAsync()
    {
        var json = await storage.GetAsync(IndexKey).ConfigureAwait(false);
        if (json is null)
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize(json, CoreJsonContext.Default.StringArray) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private async ValueTask WriteIndexAsync(string[] keys)
    {
        if (keys.Length == 0)
        {
            await storage.RemoveAsync(IndexKey).ConfigureAwait(false);
            return;
        }

        await storage.SetAsync(IndexKey, JsonSerializer.Serialize(keys, CoreJsonContext.Default.StringArray)).ConfigureAwait(false);
    }

    /// <exception cref="NotSupportedException">Core's JSON context does not know the type: add it there, so it also works when the app is trimmed.</exception>
    private static JsonTypeInfo<T> TypeInfo<T>() => (JsonTypeInfo<T>)CoreJson.Options.GetTypeInfo(typeof(T));

    private static void CheckKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (!ValidKey().IsMatch(key))
        {
            throw new ArgumentException($"'{key}' is not a key: use lower case letters, digits, dots, dashes and underscores, not starting with a symbol.", nameof(key));
        }
    }

    [GeneratedRegex(@"^[a-z0-9][a-z0-9._-]*\z")]
    private static partial Regex ValidKey();
}
