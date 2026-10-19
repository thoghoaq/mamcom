using System.Text.Json.Nodes;

namespace MamCom.RecipeDrafter.Drafting;

/// <summary>
/// The JSON Schema the model's answer must follow (structured outputs). The ingredient ids, season ids and protein
/// sources are enums built from the data, so the model cannot write an id that does not exist; there is no field for a
/// nutrition number, so it cannot write one. The finer rules (grams above zero, minutes) are checked afterwards by the
/// dish schema, where a failure can be reported instead of refusing the whole answer.
/// </summary>
public static class RecipeSchema
{
    public static readonly string[] Scales = ["linear", "sublinear", "integer", "fixed"];

    public static readonly string[] CookingMethods = ["luoc", "hap", "nau", "kho", "xao", "chien", "nuong", "tron"];

    public static readonly string[] SpicyLevels = ["none", "optional", "required"];

    public static string Build(IEnumerable<string> ingredientIds, IEnumerable<string> seasonIds, IEnumerable<string> proteinSources)
    {
        var ids = ingredientIds.Order(StringComparer.Ordinal).ToArray();
        if (ids.Length == 0)
        {
            throw new ArgumentException("There must be at least one ingredient to draft from.", nameof(ingredientIds));
        }

        JsonObject Strings(string description, IEnumerable<string>? allowed = null)
        {
            var node = new JsonObject { ["type"] = "string", ["description"] = description };
            if (allowed is not null)
            {
                node["enum"] = new JsonArray(allowed.Select(v => (JsonNode?)JsonValue.Create(v)).ToArray());
            }

            return node;
        }

        JsonObject ArrayOf(JsonObject items, string description) => new() { ["type"] = "array", ["description"] = description, ["items"] = items };

        JsonObject Object(JsonObject properties) => new()
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["required"] = new JsonArray(properties.Select(p => (JsonNode?)JsonValue.Create(p.Key)).ToArray()),
            ["properties"] = properties,
        };

        var ingredient = Object(new JsonObject
        {
            ["ingredientId"] = Strings("Id of an ingredient from the list.", ids),
            ["gramsEdiblePerServing"] = new JsonObject { ["type"] = "number", ["description"] = "Edible grams for ONE serving." },
            ["scale"] = Strings("How the amount grows with the number of servings.", Scales),
            ["note"] = Strings("Short preparation note; empty when there is none."),
        });

        var step = Object(new JsonObject
        {
            ["text"] = Strings("One short imperative sentence in Vietnamese."),
            ["activeMinutes"] = new JsonObject { ["type"] = "integer", ["description"] = "Minutes the cook's hands are needed." },
            ["passiveMinutes"] = new JsonObject { ["type"] = "integer", ["description"] = "Minutes that run unattended." },
            ["ingredientIds"] = ArrayOf(Strings("Id of an ingredient used in this step.", ids), "Ingredients used in this step."),
        });

        var missing = Object(new JsonObject
        {
            ["name"] = Strings("Vietnamese name of an ingredient that is not in the list."),
            ["gramsEdiblePerServing"] = new JsonObject { ["type"] = "number", ["description"] = "Edible grams for ONE serving." },
            ["note"] = Strings("Short note; empty when there is none."),
        });

        var recipe = Object(new JsonObject
        {
            ["seasons"] = ArrayOf(Strings("A season id, or all.", seasonIds), "Seasons the dish suits; just all when it suits every season."),
            ["spicy"] = Strings("How spicy the dish is.", SpicyLevels),
            ["cookingMethod"] = Strings("The main cooking method.", CookingMethods),
            ["proteinSource"] = Strings("The main protein source, none or chay.", proteinSources),
            ["serveHot"] = new JsonObject { ["type"] = "boolean", ["description"] = "True when the dish must come straight off the heat." },
            ["ingredients"] = ArrayOf(ingredient, "The recipe lines for one serving, from the ingredient list only."),
            ["steps"] = ArrayOf(step, "The steps in order."),
            ["missingIngredients"] = ArrayOf(missing, "Ingredients the recipe needs that the list does not have."),
        });

        return recipe.ToJsonString();
    }
}
