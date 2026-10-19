using MamCom.Core.Models;
using MamCom.FdcImporter.Fdc;

namespace MamCom.FdcImporter.Tests;

public class NutrientExtractorTests
{
    private static ExtractedNutrition Extract(params (int Id, string Name, string Unit, double Value)[] nutrients) =>
        NutrientExtractor.Extract(Foods.Parsed(1, "Test food", nutrients: nutrients));

    [Fact]
    public void A_food_that_states_all_six_nutrients_has_nothing_missing()
    {
        var result = Extract(Foods.Complete(kcal: 365, protein: 7.13, fat: 0.66, carb: 80, fiber: 1.3, sodium: 5));

        result.Per100g.ShouldBe(new NutritionFacts(365, 7.13, 0.66, 80, 1.3, 5));
        result.Missing.ShouldBeEmpty();
        result.KcalSource.ShouldBe("Energy");
    }

    [Fact]
    public void Foundation_energy_comes_from_the_specific_atwater_factors_before_the_general_ones()
    {
        var result = Extract(
            (2047, "Energy (Atwater General Factors)", "KCAL", 359),
            (2048, "Energy (Atwater Specific Factors)", "KCAL", 370));

        result.Per100g.Kcal.ShouldBe(370);
        result.KcalSource.ShouldBe("Energy (Atwater Specific Factors)");
    }

    [Fact]
    public void The_general_atwater_energy_is_used_when_it_is_the_only_one()
    {
        var result = Extract((2047, "Energy (Atwater General Factors)", "KCAL", 359));

        result.Per100g.Kcal.ShouldBe(359);
        result.KcalSource.ShouldBe("Energy (Atwater General Factors)");
    }

    [Fact]
    public void Plain_energy_wins_over_both_atwater_energies()
    {
        var result = Extract(
            (2047, "Energy (Atwater General Factors)", "KCAL", 359),
            (2048, "Energy (Atwater Specific Factors)", "KCAL", 370),
            (1008, "Energy", "KCAL", 365));

        result.Per100g.Kcal.ShouldBe(365);
        result.KcalSource.ShouldBe("Energy");
    }

    [Fact]
    public void Energy_in_kilojoules_is_never_read_as_kilocalories()
    {
        var result = Extract((1062, "Energy", "kJ", 1530), (1008, "Energy", "kJ", 1530));

        result.Per100g.Kcal.ShouldBeNull();
        result.KcalSource.ShouldBeNull();
        result.Missing.ShouldContain("kcal");
    }

    [Theory]
    [InlineData(1003, "G", "MG")]
    [InlineData(1004, "G", "KCAL")]
    [InlineData(1005, "G", "UG")]
    [InlineData(1079, "G", "MG")]
    [InlineData(1093, "MG", "G")]
    public void A_nutrient_in_the_wrong_unit_is_left_out(int id, string rightUnit, string wrongUnit)
    {
        var right = Extract((id, "x", rightUnit, 3));
        var wrong = Extract((id, "x", wrongUnit, 3));

        right.Missing.Count.ShouldBe(5);
        wrong.Missing.Count.ShouldBe(6);
    }

    [Fact]
    public void Units_are_compared_without_regard_to_case()
    {
        var result = Extract((1008, "Energy", "kcal", 100), (1093, "Sodium, Na", "mg", 7));

        result.Per100g.Kcal.ShouldBe(100);
        result.Per100g.SodiumMg.ShouldBe(7);
    }

    [Fact]
    public void A_stated_zero_is_a_value_not_a_missing_nutrient()
    {
        var result = Extract(Foods.Complete(fiber: 0, sodium: 0));

        result.Per100g.FiberG.ShouldBe(0);
        result.Per100g.SodiumMg.ShouldBe(0);
        result.Missing.ShouldBeEmpty();
    }

    [Fact]
    public void A_negative_value_is_not_believed()
    {
        var result = Extract((1003, "Protein", "G", -1), (1004, "Total lipid (fat)", "G", 2));

        result.Per100g.ProteinG.ShouldBeNull();
        result.Per100g.FatG.ShouldBe(2);
        result.Missing.ShouldContain("proteinG");
    }

    [Theory]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NaN)]
    public void A_value_that_is_not_a_finite_number_is_not_believed(double value)
    {
        var result = Extract((1003, "Protein", "G", value));

        result.Per100g.ProteinG.ShouldBeNull();
        result.Missing.ShouldContain("proteinG");
    }

    [Fact]
    public void A_nutrient_without_a_value_is_missing()
    {
        var food = new FdcFood(1, "Test", "SR Legacy", null, null, [new FdcNutrient(1003, "Protein", "G", null)]);

        NutrientExtractor.Extract(food).Per100g.ProteinG.ShouldBeNull();
    }

    [Fact]
    public void A_food_with_no_nutrient_list_has_all_six_missing_in_the_order_of_an_ingredient_file()
    {
        var result = NutrientExtractor.Extract(new FdcFood(1, "Test", "SR Legacy", null, null, null));

        result.Per100g.ShouldBe(new NutritionFacts(null, null, null, null, null, null));
        result.Missing.ShouldBe(["kcal", "proteinG", "fatG", "carbG", "fiberG", "sodiumMg"]);
        result.KcalSource.ShouldBeNull();
    }

    [Fact]
    public void The_missing_list_names_only_what_is_missing_in_the_order_of_an_ingredient_file()
    {
        var result = Extract((1003, "Protein", "G", 5), (1093, "Sodium, Na", "MG", 5));

        result.Missing.ShouldBe(["kcal", "fatG", "carbG", "fiberG"]);
    }

    [Fact]
    public void When_a_nutrient_is_listed_twice_the_first_usable_one_wins()
    {
        var result = Extract((1003, "Protein", "G", -3), (1003, "Protein", "G", 4), (1003, "Protein", "G", 9));

        result.Per100g.ProteinG.ShouldBe(4);
    }

    [Fact]
    public void The_captured_foundation_rice_gets_its_numbers_exactly_as_fdc_states_them()
    {
        var food = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "search-rice-captured.json")))
            .GetProperty("foods")[0];
        var nutrients = food.GetProperty("foodNutrients").EnumerateArray()
            .Select(n => new FdcNutrient(n.GetProperty("nutrientId").GetInt32(), n.GetProperty("nutrientName").GetString(), n.GetProperty("unitName").GetString(), n.GetProperty("value").GetDouble()))
            .ToArray();

        var result = NutrientExtractor.Extract(new FdcFood(2512381, "Rice", "Foundation", null, null, nutrients));

        result.Per100g.ShouldBe(new NutritionFacts(370, 7.04, 1.03, 80.3, 0.149, 0.462));
        result.KcalSource.ShouldBe("Energy (Atwater Specific Factors)");
        result.Missing.ShouldBeEmpty();
    }
}
