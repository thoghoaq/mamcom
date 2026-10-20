using MamCom.DataBuilder.Loading;
using MamCom.DataBuilder.Validation;

namespace MamCom.DataBuilder.Tests.Validation;

/// <summary>Reading the data folder: what is loaded, and what is reported instead when a file is not usable.</summary>
public class LoadingTests
{
    [Fact]
    public void The_frozen_seed_loads_without_a_finding_under_the_live_schemas_and_config()
    {
        using var data = new TempData();

        var loaded = data.Load();

        loaded.Diagnostics.ShouldBeEmpty();
        loaded.Config.ShouldNotBeNull();
        loaded.Validation.ShouldNotBeNull();
        loaded.Ingredients.Count.ShouldBe(33);
        loaded.Dishes.Count.ShouldBe(20);
        loaded.Ingredients.ShouldAllBe(f => f.File.StartsWith("ingredients/") && f.File.EndsWith(".json"));
        loaded.Dishes.Select(f => f.File).ShouldBe(loaded.Dishes.Select(f => f.File).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void A_missing_data_folder_is_reported_and_nothing_is_loaded()
    {
        var loaded = DataLoader.Load(Path.Combine(Path.GetTempPath(), "mamcom-no-such-folder"));

        var issue = loaded.Diagnostics.ShouldHaveSingleItem();
        issue.Code.ShouldBe(DiagnosticCodes.DataFolder);
        issue.Message.ShouldContain("mamcom-no-such-folder");
        loaded.Ingredients.ShouldBeEmpty();
        loaded.Dishes.ShouldBeEmpty();
        loaded.Config.ShouldBeNull();
    }

    [Fact]
    public void A_data_folder_without_schemas_is_reported()
    {
        using var data = new TempData();
        Directory.Delete(Path.Combine(data.Root, "schemas"), recursive: true);

        var loaded = data.Load();

        var issue = loaded.Diagnostics.ShouldHaveSingleItem();
        issue.Code.ShouldBe(DiagnosticCodes.DataFolder);
        issue.Message.ShouldContain("schemas");
        loaded.Ingredients.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("ingredient")]
    [InlineData("dish")]
    [InlineData("portion-defaults")]
    [InlineData("nutrition-defaults")]
    [InlineData("spinner-defaults")]
    [InlineData("cooking-defaults")]
    [InlineData("validation-defaults")]
    public void A_missing_schema_is_reported_by_name_and_nothing_is_loaded(string schema)
    {
        using var data = new TempData();
        File.Delete(Path.Combine(data.Root, "schemas", schema + ".schema.json"));

        var loaded = data.Load();

        var issue = loaded.Diagnostics.ShouldHaveSingleItem();
        issue.Code.ShouldBe(DiagnosticCodes.SchemaMissing);
        issue.Message.ShouldContain(schema + ".schema.json");
        loaded.Ingredients.ShouldBeEmpty();
        loaded.Dishes.ShouldBeEmpty();
    }

    [Fact]
    public void A_schema_file_that_is_not_json_schema_is_reported_and_nothing_is_loaded()
    {
        using var data = new TempData();
        File.WriteAllText(Path.Combine(data.Root, "schemas", "dish.schema.json"), "{ not json");

        var loaded = data.Load();

        var issue = loaded.Diagnostics.ShouldHaveSingleItem();
        issue.Code.ShouldBe(DiagnosticCodes.SchemaMissing);
        issue.Message.ShouldContain("dish.schema.json");
        loaded.Dishes.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("portion-defaults")]
    [InlineData("nutrition-defaults")]
    [InlineData("spinner-defaults")]
    [InlineData("cooking-defaults")]
    [InlineData("validation-defaults")]
    public void A_missing_config_file_is_reported_and_the_rest_still_loads(string name)
    {
        using var data = new TempData();
        data.Delete("config", name);

        var loaded = data.Load();

        var issue = loaded.Diagnostics.ShouldHaveSingleItem();
        issue.Code.ShouldBe(DiagnosticCodes.ConfigMissing);
        issue.File.ShouldBe($"config/{name}.json");
        if (name == "validation-defaults")
        {
            loaded.Validation.ShouldBeNull();
            loaded.Config.ShouldNotBeNull();
        }
        else
        {
            loaded.Config.ShouldBeNull();
            loaded.Validation.ShouldNotBeNull();
        }
        loaded.Ingredients.Count.ShouldBe(33);
        loaded.Dishes.Count.ShouldBe(20);
    }

    [Fact]
    public void A_config_file_that_breaks_its_schema_is_reported_with_the_place()
    {
        using var data = new TempData();
        data.Edit("config", "portion-defaults", o => o["sublinearExponent"] = 1.5);

        var loaded = data.Load();

        var issue = loaded.Diagnostics.ShouldHaveSingleItem();
        issue.Code.ShouldBe(DiagnosticCodes.Schema);
        issue.File.ShouldBe("config/portion-defaults.json");
        issue.Location.ShouldBe("/sublinearExponent");
        issue.Message.ShouldStartWith("maximum:");
        loaded.Config.ShouldBeNull();
    }

    [Fact]
    public void A_portion_config_whose_values_contradict_each_other_is_reported_by_the_core_check()
    {
        using var data = new TempData();
        data.Edit("config", "portion-defaults", o =>
        {
            o["minBowlsPerMeal"] = 3;
            o["maxBowlsPerMeal"] = 2;
        });

        var issue = data.Load().Diagnostics.ShouldHaveSingleItem();

        issue.Code.ShouldBe(DiagnosticCodes.ConfigInvalid);
        issue.File.ShouldBe("config/portion-defaults.json");
        issue.Message.ShouldContain("MaxBowlsPerMeal");
    }

    [Fact]
    public void A_spinner_config_whose_values_contradict_each_other_is_reported_by_the_core_check()
    {
        using var data = new TempData();
        data.Edit("config", "spinner-defaults", o => o["recency"]!["reducedUntilDays"] = 1);

        var issue = data.Load().Diagnostics.ShouldHaveSingleItem();

        issue.Code.ShouldBe(DiagnosticCodes.ConfigInvalid);
        issue.File.ShouldBe("config/spinner-defaults.json");
    }

    [Fact]
    public void A_nutrition_config_with_a_range_the_wrong_way_round_is_reported_by_the_core_check()
    {
        using var data = new TempData();
        data.Edit("config", "nutrition-defaults", o =>
        {
            o["recommendedEnergyShare"]!["protein"]!["min"] = 0.5;
            o["recommendedEnergyShare"]!["protein"]!["max"] = 0.2;
        });

        data.Load().Diagnostics.ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCodes.ConfigInvalid);
    }

    [Fact]
    public void A_validation_config_with_a_range_the_wrong_way_round_is_reported_and_leaves_the_runtime_config_alone()
    {
        using var data = new TempData();
        data.Edit("config", "validation-defaults", o =>
        {
            o["mainProteinGramsPerServing"]!["min"] = 200;
            o["mainProteinGramsPerServing"]!["max"] = 100;
        });

        var loaded = data.Load();

        var issue = loaded.Diagnostics.ShouldHaveSingleItem();
        issue.Code.ShouldBe(DiagnosticCodes.ConfigInvalid);
        issue.File.ShouldBe("config/validation-defaults.json");
        loaded.Validation.ShouldBeNull();
        loaded.Config.ShouldNotBeNull();
    }

    [Fact]
    public void An_ingredient_that_breaks_its_schema_is_reported_and_left_out()
    {
        using var data = new TempData();
        data.Edit("ingredients", "ca-loc", o => o["wasteRatio"] = 1);

        var loaded = data.Load();

        var issue = loaded.Diagnostics.ShouldHaveSingleItem();
        issue.Code.ShouldBe(DiagnosticCodes.Schema);
        issue.File.ShouldBe("ingredients/ca-loc.json");
        issue.Location.ShouldBe("/wasteRatio");
        loaded.Ingredients.Count.ShouldBe(32);
        loaded.Ingredients.ShouldNotContain(f => f.Value.Id == "ca-loc");
    }

    [Fact]
    public void A_dish_that_is_not_json_is_reported_and_left_out()
    {
        using var data = new TempData();
        data.WriteText("dishes", "canh-bau-tom", "{ \"id\": ");

        var loaded = data.Load();

        var issue = loaded.Diagnostics.ShouldHaveSingleItem();
        issue.Code.ShouldBe(DiagnosticCodes.Schema);
        issue.File.ShouldBe("dishes/canh-bau-tom.json");
        issue.Message.ShouldStartWith("json:");
        loaded.Dishes.Count.ShouldBe(19);
    }

    [Fact]
    public void A_number_the_schema_allows_but_the_core_cannot_hold_is_a_parse_finding()
    {
        using var data = new TempData();
        data.Edit("dishes", "canh-bau-tom", o => o["steps"]![0]!["order"] = 99999999999);

        var loaded = data.Load();

        var issue = loaded.Diagnostics.ShouldHaveSingleItem();
        issue.Code.ShouldBe(DiagnosticCodes.Parse);
        issue.File.ShouldBe("dishes/canh-bau-tom.json");
        loaded.Dishes.Count.ShouldBe(19);
    }

    [Fact]
    public void Only_json_files_directly_in_the_folder_are_data()
    {
        using var data = new TempData();
        data.WriteText("dishes/_drafts", "canh-nhap", "{ this is a draft and not valid }");
        data.WriteText("dishes", "ghi-chu", "{}");
        File.Move(data.PathOf("dishes", "ghi-chu"), Path.Combine(data.Root, "dishes", "ghi-chu.txt"));

        var loaded = data.Load();

        loaded.Diagnostics.ShouldBeEmpty();
        loaded.Dishes.Count.ShouldBe(20);
    }

    [Fact]
    public void A_missing_dishes_folder_is_reported_and_the_ingredients_still_load()
    {
        using var data = new TempData();
        Directory.Delete(Path.Combine(data.Root, "dishes"), recursive: true);

        var loaded = data.Load();

        var issue = loaded.Diagnostics.ShouldHaveSingleItem();
        issue.Code.ShouldBe(DiagnosticCodes.DataFolder);
        issue.File.ShouldBe("dishes");
        loaded.Ingredients.Count.ShouldBe(33);
        loaded.Dishes.ShouldBeEmpty();
    }

    [Fact]
    public void A_property_written_twice_in_a_file_is_a_finding_not_a_silent_last_one_wins()
    {
        using var data = new TempData();
        var text = File.ReadAllText(data.PathOf("ingredients", "gao")).Replace("\"isPantry\": true,", "\"isPantry\": true,\n  \"isPantry\": false,");
        data.WriteText("ingredients", "gao", text);

        var issue = data.Load().Diagnostics.ShouldHaveSingleItem();

        issue.File.ShouldBe("ingredients/gao.json");
        issue.Message.ShouldStartWith("json:");
    }
}
