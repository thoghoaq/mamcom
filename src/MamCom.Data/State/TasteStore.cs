using MamCom.Core.Taste;

namespace MamCom.Data.State;

/// <summary>What the household said about dishes (SPEC §5.4, §8.1-6): the ones they liked or did not like after a meal, and the ones they blocked.</summary>
public interface ITasteStore
{
    /// <returns>The profile that was saved, or the empty profile when none was or it cannot be used.</returns>
    ValueTask<TasteProfile> LoadAsync();

    /// <returns>True when it is kept for next time; false when it could only be kept for as long as the page lives.</returns>
    ValueTask<bool> SaveAsync(TasteProfile profile);
}

/// <summary><see cref="ITasteStore"/> over <see cref="IUserStateStore"/>, under the key <c>taste.v1</c>.</summary>
public sealed class TasteStore(IUserStateStore state) : ITasteStore
{
    public const string Key = "taste.v1";

    public async ValueTask<TasteProfile> LoadAsync() => TasteDesk.Open(await state.GetAsync<TasteProfile>(Key));

    public ValueTask<bool> SaveAsync(TasteProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return state.SetAsync(Key, profile);
    }
}
