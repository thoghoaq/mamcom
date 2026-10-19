using System.Text.Json;
using System.Text.Json.Nodes;
using MamCom.Core.Json;
using MamCom.Core.Models;
using MamCom.DataBuilder.Schemas;
using MamCom.RecipeDrafter.Drafting;

namespace MamCom.RecipeDrafter.Tests;

/// <summary>The schema is what keeps the model inside the data: ids that exist, no nutrition, nothing the program does not read.</summary>
public class RecipeSchemaTests
{
    private static readonly string[] Ingredients = ["thit-ba-chi", "gao", "ca-loc"];
    private static readonly string[] Seasons = ["summer", "winter", "all"];
    private static readonly string[] Proteins = ["heo", "ca", "none", "chay"];

    private static JsonObject Build() => JsonNode.Parse(RecipeSchema.Build(Ingredients, Seasons, Proteins))!.AsObject();

    private static string[] EnumValues(JsonNode node) => [.. node["enum"]!.AsArray().Select(n => n!.GetValue<string>())];

    private static string[] NamesOf<T>() where T : struct, Enum =>
        [.. Enum.GetValues<T>().Select(v => JsonSerializer.Serialize(v, CoreJson.Options).Trim('"')).Order(StringComparer.Ordinal)];

    // ---- shape ---------------------------------------------------------------------------------------------------

    [Fact]
    public void The_answer_has_the_members_of_the_recipe_record_and_no_others()
    {
        var schema = Build();

        var members = typeof(LlmRecipe).GetConstructors().Single().GetParameters().Select(p => JsonNamingPolicy.CamelCase.ConvertName(p.Name!)).Order(StringComparer.Ordinal).ToArray();

        schema["properties"]!.AsObject().Select(p => p.Key).Order(StringComparer.Ordinal).ShouldBe(members);
        schema["required"]!.AsArray().Select(n => n!.GetValue<string>()).Order(StringComparer.Ordinal).ShouldBe(members);
    }

    [Fact]
    public void Every_object_in_the_schema_refuses_extra_members_and_requires_all_its_own()
    {
        var objects = new List<JsonObject>();
        void Walk(JsonNode? node)
        {
            switch (node)
            {
                case JsonObject o:
                    if (o["type"]?.GetValue<string>() == "object")
                    {
                        objects.Add(o);
                    }

                    foreach (var child in o.Select(p => p.Value))
                    {
                        Walk(child);
                    }

                    break;
                case JsonArray a:
                    foreach (var child in a)
                    {
                        Walk(child);
                    }

                    break;
            }
        }

        Walk(Build());

        objects.Count.ShouldBe(4);   // the answer, an ingredient line, a step, a missing ingredient
        foreach (var o in objects)
        {
            o["additionalProperties"]!.GetValue<bool>().ShouldBeFalse();
            o["required"]!.AsArray().Select(n => n!.GetValue<string>()).Order().ShouldBe(o["properties"]!.AsObject().Select(p => p.Key).Order());
        }
    }

    [Fact]
    public void Ingredient_ids_in_lines_and_steps_are_limited_to_the_ingredient_list()
    {
        var schema = Build();

        var line = schema["properties"]!["ingredients"]!["items"]!["properties"]!["ingredientId"]!;
        var step = schema["properties"]!["steps"]!["items"]!["properties"]!["ingredientIds"]!["items"]!;

        EnumValues(line).ShouldBe(["ca-loc", "gao", "thit-ba-chi"]);
        EnumValues(step).ShouldBe(["ca-loc", "gao", "thit-ba-chi"]);
    }

    [Fact]
    public void Seasons_and_protein_sources_are_limited_to_the_ones_given()
    {
        var schema = Build();

        EnumValues(schema["properties"]!["seasons"]!["items"]!).ShouldBe(Seasons);
        EnumValues(schema["properties"]!["proteinSource"]!).ShouldBe(Proteins);
    }

    [Fact]
    public void The_fixed_choices_are_the_ones_the_dish_model_accepts()
    {
        var properties = Build()["properties"]!;

        EnumValues(properties["spicy"]!).Order(StringComparer.Ordinal).ShouldBe(NamesOf<SpicyLevel>());
        EnumValues(properties["cookingMethod"]!).Order(StringComparer.Ordinal).ShouldBe(NamesOf<CookingMethod>());
        EnumValues(properties["ingredients"]!["items"]!["properties"]!["scale"]!).Order(StringComparer.Ordinal).ShouldBe(NamesOf<ScaleKind>());
    }

    [Fact]
    public void There_is_no_place_for_a_nutrition_number_a_status_or_an_image()
    {
        var names = new List<string>();
        void Collect(JsonNode? node)
        {
            switch (node)
            {
                case JsonObject o:
                    foreach (var (key, value) in o)
                    {
                        if (key == "properties")
                        {
                            names.AddRange(value!.AsObject().Select(p => p.Key));
                        }

                        Collect(value);
                    }

                    break;
                case JsonArray a:
                    foreach (var child in a)
                    {
                        Collect(child);
                    }

                    break;
            }
        }

        Collect(Build());

        var forbidden = new[] { "kcal", "proteinG", "fatG", "carbG", "fiberG", "sodiumMg", "nutrition", "status", "provenance", "image" };
        names.Intersect(forbidden).ShouldBeEmpty();

        // What the owner decides is not the model's either: the answer itself has no id, name, slot or regions
        // (a missing ingredient has a name of its own, which is why this looks at the top level only).
        Build()["properties"]!.AsObject().Select(p => p.Key).Intersect(["id", "name", "slot", "regions"]).ShouldBeEmpty();
    }

    [Fact]
    public void Ingredient_ids_are_sorted_so_the_schema_is_the_same_whatever_order_they_arrive_in()
    {
        RecipeSchema.Build(["b", "a", "c"], Seasons, Proteins).ShouldBe(RecipeSchema.Build(["c", "b", "a"], Seasons, Proteins));
    }

    [Fact]
    public void An_empty_ingredient_list_is_refused()
    {
        Should.Throw<ArgumentException>(() => RecipeSchema.Build([], Seasons, Proteins));
    }

    // ---- the schema as a validator ----------------------------------------------------------------------------------

    private static SchemaSet Validator()
    {
        var folder = Directory.CreateTempSubdirectory("mamcom-recipe-schema");
        File.WriteAllText(Path.Combine(folder.FullName, "recipe.schema.json"), RecipeSchema.Build(["ca-loc", "nuoc-mam", "duong", "hanh-tim", "hat-tieu", "ot", "dau-an"], ["all", "summer"], ["ca", "none", "chay"]));
        try
        {
            return SchemaSet.Load(folder.FullName);
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Fact]
    public void A_recipe_the_model_could_write_satisfies_the_schema()
    {
        Validator().Validate("recipe", Answers.Recipe().ToJsonString()).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("unknown ingredient in a line", "ingredients/0/ingredientId")]
    [InlineData("unknown ingredient in a step", "steps/0/ingredientIds/0")]
    [InlineData("unknown season", "seasons/0")]
    [InlineData("unknown protein", "proteinSource")]
    [InlineData("unknown spicy level", "spicy")]
    [InlineData("unknown cooking method", "cookingMethod")]
    [InlineData("unknown scale", "ingredients/0/scale")]
    [InlineData("a nutrition member", "")]
    [InlineData("a status", "")]
    [InlineData("a missing member", "")]
    public void The_schema_refuses_what_the_model_must_not_write(string what, string location)
    {
        var recipe = Answers.Recipe();
        switch (what)
        {
            case "unknown ingredient in a line": recipe["ingredients"]![0]!["ingredientId"] = "ca-chuoi"; break;
            case "unknown ingredient in a step": recipe["steps"]![0]!["ingredientIds"]![0] = "ca-chuoi"; break;
            case "unknown season": recipe["seasons"] = new JsonArray("monsoon"); break;
            case "unknown protein": recipe["proteinSource"] = "vit"; break;
            case "unknown spicy level": recipe["spicy"] = "hot"; break;
            case "unknown cooking method": recipe["cookingMethod"] = "fry"; break;
            case "unknown scale": recipe["ingredients"]![0]!["scale"] = "exponential"; break;
            case "a nutrition member": recipe["kcal"] = 400; break;
            case "a status": recipe["status"] = "reviewed"; break;
            default: recipe.Remove("serveHot"); break;
        }

        var issues = Validator().Validate("recipe", recipe.ToJsonString());

        issues.ShouldNotBeEmpty(what);
        if (location.Length > 0)
        {
            issues.ShouldContain(i => i.Location == "/" + location, what);
        }
    }
}
