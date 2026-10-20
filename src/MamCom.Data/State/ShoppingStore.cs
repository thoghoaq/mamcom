using MamCom.Core.Shopping;

namespace MamCom.Data.State;

/// <summary>The shopping screen as it was left (SPEC §8.1-4): which items are ticked off for which tray, and the pantry switch.</summary>
public interface IShoppingStore
{
    /// <returns>The state that was saved, or null when none was or it cannot be read. Whether it is for the tray on the screen is for <c>ShoppingDesk.Open</c> to say.</returns>
    ValueTask<ShoppingState?> LoadAsync();

    /// <returns>True when it is kept for next time; false when it could only be kept for as long as the page lives.</returns>
    ValueTask<bool> SaveAsync(ShoppingState state);
}

/// <summary><see cref="IShoppingStore"/> over <see cref="IUserStateStore"/>, under the key <c>shopping.v1</c>.</summary>
public sealed class ShoppingStore(IUserStateStore state) : IShoppingStore
{
    public const string Key = "shopping.v1";

    public ValueTask<ShoppingState?> LoadAsync() => state.GetAsync<ShoppingState>(Key);

    public ValueTask<bool> SaveAsync(ShoppingState value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return state.SetAsync(Key, value);
    }
}
