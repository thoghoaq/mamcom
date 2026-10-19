using System.Text.Json;
using System.Text.Json.Nodes;
using MamCom.Core.Models;
using MamCom.DataBuilder.Schemas;
using MamCom.FdcImporter.Suggestions;

namespace MamCom.FdcImporter.Tests;

/// <summary>The suggestion files are read by Studio, so their shape is a contract: the schema and the records must agree.</summary>
public class SuggestionSchemaTests
{
    private static readonly SchemaSet Schemas = SchemaSet.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas"));

    private static NutritionCandidate Candidate(int rank = 1) => new(
        rank, 168877, "Rice, white, long-grain, regular, raw, enriched", "SR Legacy", "Cereal Grains and Pasta",
        "https://fdc.nal.usda.gov/food-details/168877/nutrients",
        new NutritionFacts(365, 7.13, 0.66, 80, 1.3, 5), "Energy", [], "Lý do thử.");

    private static string Valid() => SuggestionFile.Serialize(new NutritionSuggestions("gao", "rice white raw", new DateOnly(2026, 10, 19), "usda-fdc", 2026, [Candidate(1), Candidate(2)]));

    private static JsonObject Broken(Action<JsonObject> change)
    {
        var root = JsonNode.Parse(Valid())!.AsObject();
        change(root);
        return root;
    }

    private static IReadOnlyList<SchemaIssue> Check(JsonObject content) => Schemas.Validate("nutrition-suggestion", content.ToJsonString());

    // ---- a real file is valid ----------------------------------------------------------------------------------------

    [Fact]
    public void A_file_the_importer_writes_is_valid()
    {
        Schemas.Validate("nutrition-suggestion", Valid()).ShouldBeEmpty();
    }

    [Fact]
    public void A_file_with_no_candidates_is_valid()
    {
        var text = SuggestionFile.Serialize(new NutritionSuggestions("gao", "rice", new DateOnly(2026, 10, 19), "usda-fdc", 0, []));

        Schemas.Validate("nutrition-suggestion", text).ShouldBeEmpty();
    }

    [Fact]
    public void A_candidate_with_unknown_nutrients_and_no_category_is_valid()
    {
        var candidate = Candidate() with
        {
            FoodCategory = null,
            KcalSource = null,
            Per100g = new NutritionFacts(null, null, null, null, null, null),
            Missing = ["kcal", "proteinG", "fatG", "carbG", "fiberG", "sodiumMg"],
        };
        var text = SuggestionFile.Serialize(new NutritionSuggestions("gao", "rice", new DateOnly(2026, 10, 19), "usda-fdc", 1, [candidate]));

        Schemas.Validate("nutrition-suggestion", text).ShouldBeEmpty();
    }

    [Fact]
    public async Task The_output_of_a_whole_run_validates()
    {
        using var data = new TempData();
        var fake = FakeFdc.Returning(Foods.Search(3,
            Foods.Food(1, "Rice, white, raw", "Foundation", 90, [(2048, "Energy (Atwater Specific Factors)", "KCAL", 370), (1003, "Protein", "G", 7)]),
            Foods.Food(2, "Rice, brown, cooked", "SR Legacy", 80, Foods.Complete())));

        await data.RunAsync(fake, null, "suggest", "--ids", "gao,trung-ga");

        foreach (var id in new[] { "gao", "trung-ga" })
        {
            Schemas.Validate("nutrition-suggestion", File.ReadAllText(data.SuggestionPath(id))).ShouldBeEmpty(id);
        }
    }

    // ---- the files from a real run ---------------------------------------------------------------------------------------

    private static (string Id, string Text)[] Samples() =>
        [.. Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "samples"), "*.json")
            .Order(StringComparer.Ordinal)
            .Select(path => (Path.GetFileNameWithoutExtension(path), File.ReadAllText(path)))];

    [Fact]
    public void The_samples_from_a_real_fdc_run_are_valid_and_each_names_its_ingredient()
    {
        var samples = Samples();
        samples.Length.ShouldBeGreaterThanOrEqualTo(5);

        foreach (var (id, text) in samples)
        {
            Schemas.Validate("nutrition-suggestion", text).ShouldBeEmpty(id);
            var json = JsonNode.Parse(text)!;
            json["ingredientId"]!.GetValue<string>().ShouldBe(id);
            File.Exists(Path.Combine(AppContext.BaseDirectory, "data", "ingredients", id + ".json")).ShouldBeTrue($"{id} is an ingredient of the seed");
        }
    }

    [Fact]
    public void The_samples_are_numbered_checkable_and_never_applied_to_an_ingredient_file()
    {
        foreach (var (id, text) in Samples())
        {
            var candidates = JsonNode.Parse(text)!["candidates"]!.AsArray();
            candidates.ShouldNotBeEmpty(id);
            candidates.Select(c => c!["rank"]!.GetValue<int>()).ShouldBe(Enumerable.Range(1, candidates.Count));
            foreach (var candidate in candidates)
            {
                candidate!["detailsUrl"]!.GetValue<string>().ShouldContain($"/{candidate["fdcId"]!.GetValue<long>()}/");
            }

            // The ingredient file still has no number: choosing one is a person's job in Studio.
            JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "ingredients", id + ".json")))!["nutritionSource"]!["db"]!.GetValue<string>().ShouldBe("placeholder");
        }
    }

    // ---- the schema bites --------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("Branded")]
    [InlineData("Survey (FNDDS)")]
    [InlineData("")]
    public void Only_foundation_and_sr_legacy_foods_are_allowed(string dataType)
    {
        Check(Broken(o => o["candidates"]![0]!["dataType"] = dataType)).ShouldContain(i => i.Location == "/candidates/0/dataType");
    }

    [Fact]
    public void Each_wrong_thing_is_named_where_it_is()
    {
        var cases = new (string Name, Action<JsonObject> Break, string Location)[]
        {
            ("source other than usda-fdc", o => o["source"] = "usda", "/source"),
            ("id in capitals", o => o["ingredientId"] = "Gao", "/ingredientId"),
            ("empty query", o => o["query"] = "", "/query"),
            ("date that does not exist", o => o["retrievedAt"] = "2026-13-45", "/retrievedAt"),
            ("negative total hits", o => o["totalHits"] = -1, "/totalHits"),
            ("fdcId of zero", o => o["candidates"]![0]!["fdcId"] = 0, "/candidates/0/fdcId"),
            ("rank of zero", o => o["candidates"]![0]!["rank"] = 0, "/candidates/0/rank"),
            ("details link elsewhere", o => o["candidates"]![0]!["detailsUrl"] = "https://example.com/1", "/candidates/0/detailsUrl"),
            ("empty reason", o => o["candidates"]![0]!["reason"] = "", "/candidates/0/reason"),
            ("negative nutrient", o => o["candidates"]![0]!["per100g"]!["fatG"] = -1, "/candidates/0/per100g/fatG"),
            ("more than 100 g of protein in 100 g", o => o["candidates"]![0]!["per100g"]!["proteinG"] = 101, "/candidates/0/per100g/proteinG"),
            ("more energy than pure fat", o => o["candidates"]![0]!["per100g"]!["kcal"] = 901, "/candidates/0/per100g/kcal"),
            ("unknown missing nutrient", o => o["candidates"]![0]!["missing"] = new JsonArray("vitaminC"), "/candidates/0/missing/0"),
            ("missing nutrient listed twice", o => o["candidates"]![0]!["missing"] = new JsonArray("kcal", "kcal"), "/candidates/0/missing"),
            ("extra property on a candidate", o => o["candidates"]![0]!["note"] = "x", "/candidates/0/note"),
            ("extra property on the file", o => o["comment"] = "x", "/comment"),
            ("a missing property", o => o.Remove("totalHits"), ""),
            ("too many candidates", o => o["candidates"] = new JsonArray(Enumerable.Range(1, 11).Select(i => (JsonNode?)JsonNode.Parse(Candidate(i).ToJson())).ToArray()), "/candidates"),
        };

        foreach (var (name, change, location) in cases)
        {
            Check(Broken(change)).ShouldContain(i => i.Location == location, $"{name}: expected an issue at '{location}'");
        }
    }

    // ---- the schema and the records agree ----------------------------------------------------------------------------

    private static JsonNode SchemaAt(string pointer)
    {
        var node = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "nutrition-suggestion.schema.json")))!;
        foreach (var segment in pointer.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            node = node[segment]!;
        }

        return node;
    }

    private static string[] Sorted(IEnumerable<string> names) => [.. names.Order(StringComparer.Ordinal)];

    private static string[] FieldsOf(Type record) =>
        Sorted(record.GetConstructors().Single().GetParameters().Select(p => JsonNamingPolicy.CamelCase.ConvertName(p.Name!)));

    [Theory]
    [InlineData("", typeof(NutritionSuggestions))]
    [InlineData("/$defs/candidate", typeof(NutritionCandidate))]
    [InlineData("/$defs/candidate/properties/per100g", typeof(NutritionFacts))]
    public void A_schema_object_lists_exactly_the_fields_of_its_record(string pointer, Type record)
    {
        var node = SchemaAt(pointer);

        Sorted(node["properties"]!.AsObject().Select(p => p.Key)).ShouldBe(FieldsOf(record));
        Sorted(node["required"]!.AsArray().Select(n => n!.GetValue<string>())).ShouldBe(FieldsOf(record));
        node["additionalProperties"]!.GetValue<bool>().ShouldBeFalse();
    }

    [Fact]
    public void The_missing_list_in_the_schema_names_the_six_nutrients_of_an_ingredient_file()
    {
        var allowed = SchemaAt("/$defs/candidate/properties/missing/items/enum").AsArray().Select(n => n!.GetValue<string>());

        Sorted(allowed).ShouldBe(Sorted(FieldsOf(typeof(NutritionFacts))));
    }

    [Fact]
    public void The_source_in_the_schema_is_the_constant_the_importer_writes()
    {
        SchemaAt("/properties/source/const").GetValue<string>().ShouldBe(NutritionSuggestions.UsdaFdc);
    }

    [Fact]
    public void A_candidates_numbers_fit_straight_into_an_ingredient_file_field_for_field()
    {
        // Studio copies per100g into nutritionPer100gEdible, so the names must be the ones the ingredient schema uses.
        var ingredient = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "ingredient.schema.json")))!;
        var ingredientFields = ingredient["$defs"]!["nutrition"]!["properties"]!.AsObject().Select(p => p.Key);

        Sorted(SchemaAt("/$defs/candidate/properties/per100g/properties").AsObject().Select(p => p.Key)).ShouldBe(Sorted(ingredientFields));
    }
}

internal static class CandidateJson
{
    public static string ToJson(this NutritionCandidate candidate) =>
        JsonSerializer.Serialize(candidate, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
}
