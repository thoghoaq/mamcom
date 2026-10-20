using MamCom.Core.Cooking;

namespace MamCom.Data.State;

/// <summary>The cooking mode as it was left (SPEC §8.1-5): the step on the screen and the timers that were started.</summary>
public interface ICookingStore
{
    /// <returns>The state that was saved, or null when none was or it cannot be read. Whether it is for the tray on the screen is for <c>CookingDesk.Open</c> to say.</returns>
    ValueTask<CookingState?> LoadAsync();

    /// <returns>True when it is kept for next time; false when it could only be kept for as long as the page lives.</returns>
    ValueTask<bool> SaveAsync(CookingState state);
}

/// <summary><see cref="ICookingStore"/> over <see cref="IUserStateStore"/>, under the key <c>cooking.v1</c>.</summary>
public sealed class CookingStore(IUserStateStore state) : ICookingStore
{
    public const string Key = "cooking.v1";

    public ValueTask<CookingState?> LoadAsync() => state.GetAsync<CookingState>(Key);

    public ValueTask<bool> SaveAsync(CookingState value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return state.SetAsync(Key, value);
    }
}
