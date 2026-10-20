using MamCom.Core.Models;
using MamCom.DataBuilder.Schemas;
using MamCom.Studio.Review;

namespace MamCom.Studio.Tests;

public class NutritionSuggestionTests
{
    private static readonly string DataRoot = Path.Combine(AppContext.BaseDirectory, "data");

    public static TheoryData<string> RealSuggestions()
    {
        var data = new TheoryData<string>();
        foreach (var path in Directory.GetFiles(Path.Combine(DataRoot, "_suggestions", "nutrition"), "*.json").Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetFileNameWithoutExtension(path));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(RealSuggestions))]
    public void A_suggestion_FdcImporter_wrote_is_read_with_its_numbers_exactly_as_FDC_gave_them(string id)
    {
        var text = File.ReadAllText(Path.Combine(DataRoot, "_suggestions", "nutrition", id + ".json"));
        SchemaSet.Load(Path.Combine(DataRoot, "schemas")).Validate("nutrition-suggestion", text).ShouldBeEmpty();   // the DTO and the schema stay in step

        var suggestion = NutritionSuggestion.TryParse(text)!;

        suggestion.IngredientId.ShouldBe(id);
        suggestion.Source.ShouldBe("usda-fdc");
        suggestion.Candidates.ShouldNotBeEmpty();
        suggestion.Candidates.ShouldAllBe(c => c.FdcId > 0 && c.Description.Length > 0 && c.DetailsUrl.StartsWith("https://fdc.nal.usda.gov/food-details/"));
        suggestion.Candidates.Select(c => c.Rank).ShouldBe([.. Enumerable.Range(1, suggestion.Candidates.Count)]);
    }

    [Fact]
    public void The_first_candidate_of_the_rice_suggestion_has_the_numbers_of_the_file()
    {
        var text = File.ReadAllText(Path.Combine(DataRoot, "_suggestions", "nutrition", "gao.json"));

        var first = NutritionSuggestion.TryParse(text)!.Candidates[0];

        first.FdcId.ShouldBe(2512381);
        first.Description.ShouldBe("Rice, white, long grain, unenriched, raw");
        first.DataType.ShouldBe("Foundation");
        first.Per100g.ShouldBe(new NutritionFacts(370, 7.04, 1.03, 80.3, 0.149, 0.462));
        first.KcalSource.ShouldBe("Energy (Atwater Specific Factors)");
        first.Missing.ShouldBeEmpty();
        first.HasNoNumbers.ShouldBeFalse();
    }

    private static SuggestionCandidate Candidate(NutritionFacts per100g) =>
        new(1, 1, "x", "Foundation", null, "https://fdc.nal.usda.gov/food-details/1/nutrients", per100g, null, [], "r");

    [Fact]
    public void A_candidate_has_no_numbers_only_when_all_six_are_unknown()
    {
        Candidate(new NutritionFacts(null, null, null, null, null, null)).HasNoNumbers.ShouldBeTrue();
        Candidate(new NutritionFacts(1, null, null, null, null, null)).HasNoNumbers.ShouldBeFalse();
        Candidate(new NutritionFacts(null, 1, null, null, null, null)).HasNoNumbers.ShouldBeFalse();
        Candidate(new NutritionFacts(null, null, 1, null, null, null)).HasNoNumbers.ShouldBeFalse();
        Candidate(new NutritionFacts(null, null, null, 1, null, null)).HasNoNumbers.ShouldBeFalse();
        Candidate(new NutritionFacts(null, null, null, null, 1, null)).HasNoNumbers.ShouldBeFalse();
        Candidate(new NutritionFacts(null, null, null, null, null, 1)).HasNoNumbers.ShouldBeFalse();
        Candidate(new NutritionFacts(0, 0, 0, 0, 0, 0)).HasNoNumbers.ShouldBeFalse();   // zero is a number, not an unknown
    }

    [Fact]
    public void A_suggestion_with_no_candidates_is_still_a_suggestion()
    {
        var suggestion = NutritionSuggestion.TryParse("""{ "ingredientId": "x", "query": "q", "retrievedAt": "2026-10-19", "source": "usda-fdc", "totalHits": 0, "candidates": [] }""")!;

        suggestion.Candidates.ShouldBeEmpty();
        suggestion.TotalHits.ShouldBe(0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{ not json")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("""{ "ingredientId": "" }""")]
    [InlineData("""{ "ingredientId": "", "query": "q", "retrievedAt": "2026-10-19", "source": "usda-fdc", "totalHits": 0, "candidates": [] }""")]
    [InlineData("""{ "ingredientId": "x", "candidates": null }""")]
    [InlineData("""{ "ingredientId": "x", "query": "q", "retrievedAt": "2026-10-19", "source": "usda-fdc", "totalHits": 1, "candidates": [ null ] }""")]
    [InlineData("""{ "ingredientId": "x", "query": "q", "retrievedAt": "2026-10-19", "source": "usda-fdc", "totalHits": 1, "candidates": [ { "rank": 1, "fdcId": 1, "description": "d", "dataType": "Foundation", "detailsUrl": "u", "per100g": null, "missing": [], "reason": "r" } ] }""")]
    [InlineData("""{ "ingredientId": "x", "query": "q", "retrievedAt": "khong-phai-ngay", "source": "usda-fdc", "totalHits": 1, "candidates": [] }""")]
    public void A_text_that_is_not_a_suggestion_is_null_rather_than_an_error(string text)
    {
        NutritionSuggestion.TryParse(text).ShouldBeNull();
    }
}
