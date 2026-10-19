using MamCom.DataBuilder.Schemas;
using System.Text.Json;
using System.Text.Json.Nodes;
using MamCom.Core.Cooking;
using MamCom.Core.Json;
using MamCom.Core.Models;
using MamCom.Core.Nutrition;
using MamCom.Core.Portions;
using MamCom.Core.Spinning;

namespace MamCom.DataBuilder.Tests.Schemas;

/// <summary>
/// The schemas and the Core types describe the same files twice. These tests fail when one changes without the other:
/// an enum gaining a value, a record gaining a field, a field becoming optional.
/// </summary>
public class SchemaCoreAgreementTests
{
    private static JsonNode Schema(string name) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(DataFiles.Root, "schemas", name + SchemaSet.FileSuffix)))!;

    /// <summary>The node at a JSON Pointer such as <c>/$defs/image/properties</c>.</summary>
    private static JsonNode At(string schema, string pointer)
    {
        var node = Schema(schema);
        foreach (var segment in pointer.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            node = node[segment] ?? throw new InvalidOperationException($"{schema}.schema.json has nothing at {pointer} (stopped at '{segment}').");
        }

        return node;
    }

    private static string[] Strings(JsonNode array) => array.AsArray().Select(n => n!.GetValue<string>()).Order(StringComparer.Ordinal).ToArray();

    private static string[] Keys(JsonNode obj) => obj.AsObject().Select(p => p.Key).Order(StringComparer.Ordinal).ToArray();

    /// <summary>The names the JSON uses for an enum's values, from the same converter the files are read with.</summary>
    private static string[] NamesOf<T>() where T : struct, Enum =>
        Enum.GetValues<T>()
            .Select(value => JsonSerializer.Serialize(value, CoreJson.Options).Trim('"'))
            .Order(StringComparer.Ordinal)
            .ToArray();

    // ---- enums -------------------------------------------------------------------------------------------

    [Fact]
    public void Enum_values_in_the_ingredient_schema_match_the_Core_enums()
    {
        Strings(At("ingredient", "/properties/aisle/enum")).ShouldBe(NamesOf<Aisle>());
    }

    [Fact]
    public void Enum_values_in_the_dish_schema_match_the_Core_enums()
    {
        Strings(At("dish", "/properties/slot/enum")).ShouldBe(NamesOf<DishSlot>());
        Strings(At("dish", "/properties/spicy/enum")).ShouldBe(NamesOf<SpicyLevel>());
        Strings(At("dish", "/properties/cookingMethod/enum")).ShouldBe(NamesOf<CookingMethod>());
        Strings(At("dish", "/properties/status/enum")).ShouldBe(NamesOf<DishStatus>());
        Strings(At("dish", "/properties/regions/items/enum")).ShouldBe(NamesOf<Region>());
        Strings(At("dish", "/$defs/dishIngredient/properties/scale/enum")).ShouldBe(NamesOf<ScaleKind>());
        Strings(At("dish", "/$defs/provenance/properties/draftedBy/enum")).ShouldBe(NamesOf<DraftAuthor>());
    }

    [Fact]
    public void Region_keys_in_the_ingredient_schema_are_the_regions_plus_default()
    {
        string[] expected = [.. NamesOf<Region>(), RegionKeys.Default];
        Array.Sort(expected, StringComparer.Ordinal);

        Keys(At("ingredient", "/$defs/regionNames/properties")).ShouldBe(expected);
        Keys(At("ingredient", "/$defs/regionMarketUnits/properties")).ShouldBe(expected);
    }

    [Fact]
    public void Age_group_and_bowl_size_keys_in_the_portion_schema_match_the_Core_enums()
    {
        Keys(At("portion-defaults", "/$defs/perAgeGroup/properties")).ShouldBe(NamesOf<AgeGroup>());
        Keys(At("portion-defaults", "/$defs/bowls/properties")).ShouldBe(NamesOf<AgeGroup>());
        Keys(At("portion-defaults", "/properties/gramsRawRicePerBowl/properties")).ShouldBe(NamesOf<BowlSize>());
    }

    [Fact]
    public void Dish_status_order_is_lifecycle_order_so_the_schema_rules_can_be_read_in_it()
    {
        // The three lifecycle rules of dish.schema.json name draft/validated, reviewed and cooktested.
        NamesOf<DishStatus>().ShouldBe(["cooktested", "draft", "reviewed", "validated"]);
        ((int)DishStatus.Draft).ShouldBeLessThan((int)DishStatus.Validated);
        ((int)DishStatus.Validated).ShouldBeLessThan((int)DishStatus.Reviewed);
        ((int)DishStatus.Reviewed).ShouldBeLessThan((int)DishStatus.CookTested);
    }

    // ---- record fields ---------------------------------------------------------------------------------------

    public static TheoryData<string, string, string> Records => new()
    {
        { "ingredient", "", nameof(Ingredient) },
        { "ingredient", "/$defs/marketUnit", nameof(MarketUnit) },
        { "ingredient", "/$defs/nutrition", nameof(NutritionFacts) },
        { "ingredient", "/$defs/nutritionSource", nameof(NutritionSource) },
        { "ingredient", "/$defs/image", nameof(ImageRef) },
        { "dish", "", nameof(Dish) },
        { "dish", "/$defs/dishIngredient", nameof(DishIngredient) },
        { "dish", "/$defs/step", nameof(Step) },
        { "dish", "/$defs/provenance", nameof(Provenance) },
        { "dish", "/$defs/image", nameof(ImageRef) },
        { "portion-defaults", "", nameof(PortionConfig) },
        { "portion-defaults", "/$defs/perAgeGroup", nameof(AgeGroupValues) },
        { "portion-defaults", "/properties/gramsRawRicePerBowl", nameof(BowlSizeValues) },
        { "spinner-defaults", "", nameof(SpinnerConfig) },
        { "spinner-defaults", "/properties/recency", nameof(RecencyConfig) },
        { "spinner-defaults", "/properties/preference", nameof(PreferenceWeights) },
        { "nutrition-defaults", "", nameof(NutritionConfig) },
        { "nutrition-defaults", "/properties/kcalPerGram", nameof(MacroValues) },
        { "nutrition-defaults", "/properties/recommendedEnergyShare", nameof(RecommendedShares) },
        { "nutrition-defaults", "/$defs/shareRange", nameof(ShareRange) },
        { "cooking-defaults", "", nameof(CookingConfig) },
    };

    private static Type RecordType(string name) => typeof(Ingredient).Assembly.GetExportedTypes().Single(t => t.Name == name);

    [Theory]
    [MemberData(nameof(Records))]
    public void A_schema_object_lists_exactly_the_fields_of_its_record(string schema, string pointer, string recordName)
    {
        var constructor = RecordType(recordName).GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
        var parameters = constructor.GetParameters();
        var node = At(schema, pointer);

        string Json(string name) => JsonNamingPolicy.CamelCase.ConvertName(name);

        Keys(node["properties"]!).ShouldBe(parameters.Select(p => Json(p.Name!)).Order(StringComparer.Ordinal).ToArray(), $"{schema}{pointer} properties");

        // A field the file may leave out is one the record has a default for (JSON reading requires the others).
        Strings(node["required"]!).ShouldBe(
            parameters.Where(p => !p.HasDefaultValue).Select(p => Json(p.Name!)).Order(StringComparer.Ordinal).ToArray(),
            $"{schema}{pointer} required");

        node["additionalProperties"]!.GetValue<bool>().ShouldBeFalse($"{schema}{pointer} must reject unknown fields");
    }

    [Fact]
    public void The_dish_and_ingredient_image_definitions_are_the_same_text()
    {
        // Each schema stands alone, so the shared definitions are copies; keep them identical.
        At("ingredient", "/$defs/image").ToJsonString().ShouldBe(At("dish", "/$defs/image").ToJsonString());
        At("ingredient", "/$defs/id").ToJsonString().ShouldBe(At("dish", "/$defs/id").ToJsonString());
    }
}
