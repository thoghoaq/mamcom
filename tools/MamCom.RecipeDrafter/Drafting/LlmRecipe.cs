using System.Text.Json;
using System.Text.Json.Serialization;

namespace MamCom.RecipeDrafter.Drafting;

/// <summary>One ingredient line as the model writes it. <c>Note</c> is empty when there is nothing to say.</summary>
public sealed record LlmIngredient(string IngredientId, double GramsEdiblePerServing, string Scale, string Note);

public sealed record LlmStep(string Text, int ActiveMinutes, int PassiveMinutes, IReadOnlyList<string> IngredientIds);

/// <summary>An ingredient the recipe needs that the ingredient list does not have.</summary>
public sealed record LlmMissingIngredient(string Name, double GramsEdiblePerServing, string Note);

/// <summary>
/// What the model decides about a dish. The name, slot and regions are the owner's and the status is never the model's,
/// so none of them is here: the program fills those in.
/// </summary>
public sealed record LlmRecipe(
    IReadOnlyList<string> Seasons,
    string Spicy,
    string CookingMethod,
    string ProteinSource,
    bool ServeHot,
    IReadOnlyList<LlmIngredient> Ingredients,
    IReadOnlyList<LlmStep> Steps,
    IReadOnlyList<LlmMissingIngredient> MissingIngredients)
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        AllowDuplicateProperties = false,
    };

    /// <exception cref="JsonException">Not JSON, a member missing, an unknown member, or a value of the wrong type.</exception>
    public static LlmRecipe Parse(string json) =>
        JsonSerializer.Deserialize<LlmRecipe>(json, ReadOptions) ?? throw new JsonException("The answer must be an object, not null.");
}
