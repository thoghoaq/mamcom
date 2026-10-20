using System.Text.Json.Serialization;
using MamCom.Core.Models;

namespace MamCom.Core.Taste;

/// <summary>
/// What this household said about dishes (SPEC §5.2, §5.4, §8.1-6): which ones they liked or did not like after a meal, and which ones they
/// never want to see again. The spinner reads it: a blocked dish is never drawn, a liked one is drawn twice as often, a disliked one a fifth as often.
/// </summary>
/// <param name="Preferences">Liked and disliked dishes by id. A dish that is neutral is not in it: the same as never rated.</param>
/// <param name="BlockedDishIds">Dishes that are never drawn, in the order they were blocked.</param>
public sealed record TasteProfile(IReadOnlyDictionary<string, DishPreference> Preferences, IReadOnlyList<string> BlockedDishIds)
{
    public static TasteProfile Empty { get; } = new(new Dictionary<string, DishPreference>(), []);

    /// <summary>False for a profile that was edited by hand or came from another shape: no blank id, no unknown preference, no dish blocked twice.</summary>
    [JsonIgnore]
    public bool IsUsable =>
        Preferences is not null
        && BlockedDishIds is not null
        && Preferences.All(pair => !string.IsNullOrWhiteSpace(pair.Key) && Enum.IsDefined(pair.Value))
        && BlockedDishIds.All(id => !string.IsNullOrWhiteSpace(id))
        && BlockedDishIds.Distinct(StringComparer.Ordinal).Count() == BlockedDishIds.Count;
}

/// <summary>The taste of the household as functions from one profile to the next, as the other desks do for their screens.</summary>
public static class TasteDesk
{
    /// <summary>The profile to start with: what was saved if it can be used, else none.</summary>
    public static TasteProfile Open(TasteProfile? saved) => saved is { IsUsable: true } ? saved : TasteProfile.Empty;

    public static DishPreference PreferenceOf(TasteProfile profile, string dishId)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return profile.Preferences.GetValueOrDefault(dishId, DishPreference.Neutral);
    }

    public static bool IsBlocked(TasteProfile profile, string dishId)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return profile.BlockedDishIds.Contains(dishId, StringComparer.Ordinal);
    }

    /// <summary>Records what the household said about each of these dishes. Neutral takes the dish out of the list, so "bình thường" is the same as never asked.</summary>
    /// <exception cref="ArgumentException">An id is blank.</exception>
    public static TasteProfile Rate(TasteProfile profile, IEnumerable<string> dishIds, DishPreference preference)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(dishIds);
        if (!Enum.IsDefined(preference))
        {
            throw new ArgumentOutOfRangeException(nameof(preference), preference, null);
        }

        var next = new Dictionary<string, DishPreference>(profile.Preferences, StringComparer.Ordinal);
        foreach (var id in dishIds)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(id);

            if (preference == DishPreference.Neutral)
            {
                next.Remove(id);
            }
            else
            {
                next[id] = preference;
            }
        }

        return profile with { Preferences = next };
    }

    /// <summary>What the household said about all of these dishes, if it is one thing; null when they differ or there is no dish. The one tap of the whole meal shows it.</summary>
    public static DishPreference? CommonPreference(TasteProfile profile, IReadOnlyCollection<string> dishIds)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(dishIds);

        var preferences = dishIds.Select(id => PreferenceOf(profile, id)).Distinct().ToArray();
        return preferences.Length == 1 ? preferences[0] : null;
    }

    /// <summary>Never draws this dish again. A dish that is blocked already is left as it is.</summary>
    /// <exception cref="ArgumentException">The id is blank.</exception>
    public static TasteProfile Block(TasteProfile profile, string dishId)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(dishId);

        return IsBlocked(profile, dishId) ? profile : profile with { BlockedDishIds = [.. profile.BlockedDishIds, dishId] };
    }

    public static TasteProfile Unblock(TasteProfile profile, string dishId)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return profile with { BlockedDishIds = [.. profile.BlockedDishIds.Where(id => !string.Equals(id, dishId, StringComparison.Ordinal))] };
    }

    /// <summary>The blocked ids as the set the spinner takes (<see cref="Spinning.SpinContext.BlockedDishIds"/>).</summary>
    public static IReadOnlySet<string> BlockedSet(TasteProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return new HashSet<string>(profile.BlockedDishIds, StringComparer.Ordinal);
    }
}
