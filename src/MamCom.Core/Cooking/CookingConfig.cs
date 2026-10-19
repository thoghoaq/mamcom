using System.Text.Json;
using MamCom.Core.Json;

namespace MamCom.Core.Cooking;

/// <summary>The cooking constants of SPEC §5.6, loaded from <c>data/config/cooking-defaults.json</c>.</summary>
/// <param name="RiceActiveMinutes">Hands-on minutes to wash the rice and start the cooker.</param>
/// <param name="RicePassiveMinutes">Minutes the rice cooker runs on its own.</param>
/// <param name="RiceStepText">The one instruction of the rice pseudo-dish, as shown in cooking mode.</param>
public sealed record CookingConfig(int RiceActiveMinutes, int RicePassiveMinutes, string RiceStepText)
{
    /// <summary>Reads the content of <c>cooking-defaults.json</c>. Does not validate values; see <see cref="Validate"/>.</summary>
    public static CookingConfig Parse(string json) =>
        JsonSerializer.Deserialize(json, CoreJsonContext.Default.CookingConfig)
        ?? throw new JsonException("cooking-defaults.json must be an object, not null.");

    /// <exception cref="ArgumentException">A value is outside the range the scheduler needs.</exception>
    public void Validate()
    {
        Require(RiceActiveMinutes >= 0, nameof(RiceActiveMinutes), "must not be negative");
        Require(RicePassiveMinutes >= 0, nameof(RicePassiveMinutes), "must not be negative");
        Require(!string.IsNullOrWhiteSpace(RiceStepText), nameof(RiceStepText), "must not be empty");
    }

    private static void Require(bool condition, string name, string reason)
    {
        if (!condition)
        {
            throw new ArgumentException($"Cooking config {name} {reason}.", name);
        }
    }
}
