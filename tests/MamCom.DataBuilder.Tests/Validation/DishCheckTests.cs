using System.Text.Json;
using System.Text.Json.Nodes;
using MamCom.Core.Json;
using MamCom.Core.Models;
using MamCom.DataBuilder.Validation;

namespace MamCom.DataBuilder.Tests.Validation;

/// <summary>One dish held to the rules of the data, without the rest of the data getting in the way.</summary>
public class DishCheckTests
{
    private const string File = "dishes/_drafts/rau-muong-luoc.json";

    private static (string Text, Dish Dish) Dish(TempData data, Action<JsonObject>? change = null)
    {
        var node = data.Read("dishes", "rau-muong-luoc");
        change?.Invoke(node);
        var text = node.ToJsonString();
        return (text, JsonSerializer.Deserialize<Dish>(text, CoreJson.Options)!);
    }

    private static IReadOnlyList<Diagnostic> Check(TempData data, Action<JsonObject>? change = null)
    {
        var (text, dish) = Dish(data, change);
        return DishCheck.Check(File, text, dish, data.Load(), Tests.Schemas.DataFiles.Schemas);
    }

    [Fact]
    public void A_good_dish_has_no_finding()
    {
        using var data = new TempData();

        Check(data).ShouldBeEmpty();
    }

    [Fact]
    public void A_schema_problem_is_an_error_at_its_place_under_the_given_file()
    {
        using var data = new TempData();

        var findings = Check(data, d => d["steps"]![0]!["activeMinutes"] = -1);

        findings.ShouldAllBe(f => f.Severity == Severity.Error && f.Code == DiagnosticCodes.Schema && f.File == File);
        // The schema also says the step needs some minutes at all (an anyOf), at the step itself.
        var finding = findings.Single(f => f.Location == "/steps/0/activeMinutes");
        finding.Message.ShouldStartWith("minimum:");
    }

    [Fact]
    public void When_the_schema_fails_the_other_rules_are_not_run_on_a_dish_that_is_not_well_formed()
    {
        using var data = new TempData();

        var findings = Check(data, d =>
        {
            d["steps"]![0]!["activeMinutes"] = -1;
            d["ingredients"]![0]!["ingredientId"] = "khong-co";   // would be an unknown ingredient
        });

        findings.ShouldAllBe(f => f.Code == DiagnosticCodes.Schema);
    }

    [Fact]
    public void A_rule_that_needs_the_ingredient_list_is_applied()
    {
        using var data = new TempData();

        Check(data, d => d["ingredients"]![0]!["ingredientId"] = "khong-co")
            .ShouldContain(f => f.Code == DiagnosticCodes.UnknownIngredient && f.Severity == Severity.Error && f.File == File && f.Location == "/ingredients/0/ingredientId");
    }

    [Fact]
    public void A_season_the_config_does_not_know_is_an_error()
    {
        using var data = new TempData();

        Check(data, d => d["seasons"] = new JsonArray("monsoon")).ShouldContain(f => f.Code == DiagnosticCodes.UnknownSeason);
    }

    [Fact]
    public void A_warning_stays_a_warning()
    {
        using var data = new TempData();

        var findings = Check(data, d => d["steps"]![1]!["ingredientIds"] = new JsonArray("rau-muong"));

        findings.ShouldContain(f => f.Code == DiagnosticCodes.LineNotInSteps && f.Severity == Severity.Warning);
    }

    [Fact]
    public void Findings_about_the_rest_of_the_data_are_not_the_dishs_business()
    {
        using var data = new TempData();
        data.WriteText("dishes", "hong", "{ not json");
        data.CopyFile("ingredients", "ot", "ot-chi-thien", o => o["id"] = "ot-chi-thien");   // an unused ingredient
        data.Delete("ingredients", "gao");   // would be "rice missing"

        Check(data).ShouldBeEmpty();
    }

    [Fact]
    public void The_dish_is_judged_alone_so_a_second_dish_with_its_id_in_the_folder_is_no_duplicate()
    {
        using var data = new TempData();

        // The same dish is already in dishes/; checking an edited copy of it must not report an id clash with itself.
        Check(data).ShouldNotContain(f => f.Code == DiagnosticCodes.DuplicateId);
    }

    [Fact]
    public void What_the_loader_found_wrong_with_the_file_on_disk_does_not_count_against_the_dish_being_checked()
    {
        using var data = new TempData();
        data.Edit("dishes", "rau-muong-luoc", d => d["ingredients"]![0]!["gramsEdiblePerServing"] = -5);   // the same file is rejected when the folder is loaded
        var loaded = data.Load();
        loaded.Diagnostics.ShouldContain(d => d.File == "dishes/rau-muong-luoc.json");
        var corrected = data.Read("dishes", "rau-muong-luoc");
        corrected["ingredients"]![0]!["gramsEdiblePerServing"] = 100;
        var text = corrected.ToJsonString();

        var findings = DishCheck.Check("dishes/rau-muong-luoc.json", text, JsonSerializer.Deserialize<Dish>(text, CoreJson.Options)!, loaded, Tests.Schemas.DataFiles.Schemas);

        findings.ShouldBeEmpty();
    }

    [Fact]
    public void A_file_whose_name_is_not_the_dish_id_is_reported()
    {
        using var data = new TempData();
        var (text, dish) = Dish(data);

        var findings = DishCheck.Check("dishes/_drafts/ten-khac.json", text, dish, data.Load(), Tests.Schemas.DataFiles.Schemas);

        findings.ShouldContain(f => f.Code == DiagnosticCodes.FileName && f.File == "dishes/_drafts/ten-khac.json");
    }

    [Fact]
    public void The_checker_checks_its_arguments()
    {
        using var data = new TempData();
        var (text, dish) = Dish(data);
        var loaded = data.Load();
        var schemas = Tests.Schemas.DataFiles.Schemas;

        Should.Throw<ArgumentException>(() => DishCheck.Check(" ", text, dish, loaded, schemas));
        Should.Throw<ArgumentNullException>(() => DishCheck.Check(File, null!, dish, loaded, schemas));
        Should.Throw<ArgumentNullException>(() => DishCheck.Check(File, text, null!, loaded, schemas));
        Should.Throw<ArgumentNullException>(() => DishCheck.Check(File, text, dish, null!, schemas));
        Should.Throw<ArgumentNullException>(() => DishCheck.Check(File, text, dish, loaded, null!));
    }
}
