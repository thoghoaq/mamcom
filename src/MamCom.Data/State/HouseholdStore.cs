using MamCom.Core.Models;

namespace MamCom.Data.State;

/// <summary>Who is in the family: what the first screen makes (SPEC §8.1-1) and the settings change (SPEC §8.1-7).</summary>
public interface IHouseholdStore
{
    /// <returns>The household that was saved, or null when none was or it cannot be read. Whether it can be used is for <c>HouseholdEditor.IsUsable</c> to say: it needs the portion config.</returns>
    ValueTask<Household?> LoadAsync();

    /// <returns>True when it is kept for next time; false when it could only be kept for as long as the page lives.</returns>
    ValueTask<bool> SaveAsync(Household household);
}

/// <summary><see cref="IHouseholdStore"/> over <see cref="IUserStateStore"/>, under the key <c>household.v1</c>.</summary>
public sealed class HouseholdStore(IUserStateStore state) : IHouseholdStore
{
    public const string Key = "household.v1";

    public ValueTask<Household?> LoadAsync() => state.GetAsync<Household>(Key);

    public ValueTask<bool> SaveAsync(Household household)
    {
        ArgumentNullException.ThrowIfNull(household);

        return state.SetAsync(Key, household);
    }
}
