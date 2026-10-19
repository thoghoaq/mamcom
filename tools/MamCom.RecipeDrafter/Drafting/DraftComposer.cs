using System.Text.Json;
using System.Text.Json.Nodes;
using MamCom.Core.Json;
using MamCom.Core.Models;
using MamCom.DataBuilder.Loading;
using MamCom.RecipeDrafter.Plan;

namespace MamCom.RecipeDrafter.Drafting;

/// <summary>
/// Turns what the model wrote into a dish file. What the owner decided (name, slot, regions) and what only a person
/// may decide (status, review marks) come from here, never from the model: a draft is always <c>draft</c>, by an LLM,
/// with no review, no cook test and no image. The result is read back through the Core types, so a value the Core
/// would refuse (an unknown spicy level) is refused here too.
/// </summary>
public static class DraftComposer
{
    /// <exception cref="FormatException">The recipe holds a value the dish model does not accept; the message says which.</exception>
    public static Dish Compose(DishRequest request, LlmRecipe recipe)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(recipe);

        var dish = new JsonObject
        {
            ["id"] = request.Id,
            ["name"] = request.Name,
            ["slot"] = JsonName(request.Slot),
            ["regions"] = new JsonArray(request.Regions.Select(r => (JsonNode?)JsonValue.Create(JsonName(r))).ToArray()),
            ["seasons"] = new JsonArray(recipe.Seasons.Select(s => (JsonNode?)JsonValue.Create(s)).ToArray()),
            ["spicy"] = recipe.Spicy,
            ["cookingMethod"] = recipe.CookingMethod,
            ["proteinSource"] = recipe.ProteinSource,
            ["serveHot"] = recipe.ServeHot,
            ["ingredients"] = new JsonArray(recipe.Ingredients.Select(Line).ToArray()),
            ["steps"] = new JsonArray(recipe.Steps.Select((step, index) => Step(step, index + 1)).ToArray()),
            ["status"] = "draft",
            ["provenance"] = new JsonObject
            {
                ["draftedBy"] = "llm",
                ["reviewedBy"] = null,
                ["reviewedAt"] = null,
                ["cookTestedAt"] = null,
                ["measuredTotalMinutes"] = null,
            },
            ["image"] = new JsonObject
            {
                ["thumb"] = null,
                ["full"] = null,
                ["source"] = null,
                ["license"] = null,
                ["isIllustration"] = false,
            },
        };

        try
        {
            return JsonSerializer.Deserialize<Dish>(dish.ToJsonString(), CoreJson.Options)
                ?? throw new FormatException("The dish came out empty.");
        }
        catch (JsonException ex)
        {
            throw new FormatException(ex.Message, ex);
        }
    }

    /// <summary>The file text of a dish: the same canonical format the other dish files have.</summary>
    public static string Serialize(Dish dish) => DishFile.Serialize(dish);

    private static JsonNode Line(LlmIngredient line) => new JsonObject
    {
        ["ingredientId"] = line.IngredientId,
        ["gramsEdiblePerServing"] = line.GramsEdiblePerServing,
        ["scale"] = line.Scale,
        ["consumedRatio"] = 1,
        ["note"] = NullIfBlank(line.Note),
    };

    private static JsonNode Step(LlmStep step, int order) => new JsonObject
    {
        ["order"] = order,
        ["text"] = step.Text,
        ["activeMinutes"] = step.ActiveMinutes,
        ["passiveMinutes"] = step.PassiveMinutes,
        ["ingredientIds"] = new JsonArray(step.IngredientIds.Select(i => (JsonNode?)JsonValue.Create(i)).ToArray()),
    };

    private static string? NullIfBlank(string text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static string JsonName<T>(T value) where T : struct, Enum =>
        JsonSerializer.Serialize(value, CoreJson.Options).Trim('"');
}
