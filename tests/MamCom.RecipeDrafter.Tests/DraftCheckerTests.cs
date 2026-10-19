using System.Text.Json;
using System.Text.Json.Nodes;
using MamCom.Core.Json;
using MamCom.Core.Models;
using MamCom.RecipeDrafter.Drafting;

namespace MamCom.RecipeDrafter.Tests;

/// <summary>A draft is held to the rules the shipped data is held to, but only for what is the draft's own.</summary>
public class DraftCheckerTests
{
    private static DraftProblem[] Check(TempData data, Dish dish) =>
        [.. DraftChecker.Check(DraftComposer.Serialize(dish), dish, data.Load(), data.Schemas)];

    private static Dish Draft(Action<JsonObject>? change = null) =>
        DraftComposer.Compose(Answers.FishMain, LlmRecipe.Parse(Answers.Recipe(change).ToJsonString()));

    /// <summary>Changes the dish after it is composed, as a person editing the file would.</summary>
    private static Dish Edited(Action<JsonObject> change)
    {
        var json = JsonNode.Parse(DraftComposer.Serialize(Draft()))!.AsObject();
        change(json);
        return JsonSerializer.Deserialize<Dish>(json.ToJsonString(), CoreJson.Options)!;
    }

    [Fact]
    public void A_good_draft_has_no_finding()
    {
        using var data = new TempData();

        Check(data, Draft()).ShouldBeEmpty();
    }

    [Fact]
    public void A_value_the_dish_schema_refuses_is_an_error_at_that_place()
    {
        using var data = new TempData();
        var dish = Draft(r =>
        {
            r["steps"]![0]!["activeMinutes"] = 0;
            r["steps"]![0]!["passiveMinutes"] = 0;
        });

        var problems = Check(data, dish);

        problems.ShouldContain(p => p.Severity == "error" && p.Code == "schema" && p.Location == "/steps/0");
    }

    [Fact]
    public void Grams_of_zero_are_a_schema_error()
    {
        using var data = new TempData();

        Check(data, Draft(r => r["ingredients"]![0]!["gramsEdiblePerServing"] = 0))
            .ShouldContain(p => p.Code == "schema" && p.Location == "/ingredients/0/gramsEdiblePerServing");
    }

    [Fact]
    public void An_ingredient_that_does_not_exist_is_an_error_even_though_the_model_was_told_not_to()
    {
        using var data = new TempData();
        var dish = Edited(d =>
        {
            d["ingredients"]![0]!["ingredientId"] = "ca-chuoi";
            d["steps"]![0]!["ingredientIds"]![0] = "ca-chuoi";
            d["steps"]![1]!["ingredientIds"]![2] = "ca-chuoi";
            d["steps"]![2]!["ingredientIds"]![1] = "ca-chuoi";
        });

        var problems = Check(data, dish);

        problems.ShouldContain(p => p.Severity == "error" && p.Code == "unknown-ingredient" && p.Location == "/ingredients/0/ingredientId");
        problems.ShouldContain(p => p.Code == "unknown-ingredient" && p.Location == "/steps/0/ingredientIds/0");
    }

    [Fact]
    public void A_season_the_config_does_not_know_is_an_error()
    {
        using var data = new TempData();

        Check(data, Edited(d => d["seasons"] = new JsonArray("monsoon")))
            .ShouldContain(p => p.Severity == "error" && p.Code == "unknown-season" && p.Location == "/seasons/0");
    }

    [Fact]
    public void An_ingredient_on_two_lines_is_an_error()
    {
        using var data = new TempData();

        Check(data, Edited(d => d["ingredients"]!.AsArray().Add(d["ingredients"]![1]!.DeepClone())))
            .ShouldContain(p => p.Severity == "error" && p.Code == "duplicate-line");
    }

    [Fact]
    public void A_main_with_far_too_much_protein_is_a_warning_not_an_error()
    {
        using var data = new TempData();

        var problems = Check(data, Draft(r => r["ingredients"]![0]!["gramsEdiblePerServing"] = 400));

        problems.ShouldHaveSingleItem().ShouldBe(new DraftProblem("warning", "protein-range", string.Empty, problems[0].Message));
        problems[0].Message.ShouldContain("400");
    }

    [Fact]
    public void An_ingredient_no_step_uses_is_a_warning()
    {
        using var data = new TempData();

        var problems = Check(data, Draft(r => r["steps"]![1]!["ingredientIds"] = new JsonArray("duong", "ca-loc")));

        problems.ShouldContain(p => p.Severity == "warning" && p.Code == "line-not-in-steps" && p.Location == "/ingredients/6/ingredientId");
    }

    [Fact]
    public void Findings_about_other_dishes_and_other_ingredients_are_not_the_drafts_business()
    {
        using var data = new TempData();
        File.WriteAllText(Path.Combine(data.Root, "dishes", "hong.json"), "{ not json");
        File.Delete(Path.Combine(data.Root, "ingredients", "gao.json"));   // would be "rice missing" for the data as a whole

        Check(data, Draft()).ShouldBeEmpty();
    }

    [Fact]
    public void Ingredients_the_draft_does_not_use_are_not_reported_as_unused()
    {
        using var data = new TempData();

        // The seed has 33 ingredients and the draft uses 7: a data-wide warning must not leak into a draft's report.
        Check(data, Draft()).ShouldNotContain(p => p.Code == "unused-ingredient");
    }

    [Fact]
    public void The_draft_file_name_is_where_findings_are_filed()
    {
        DraftChecker.DraftFile("ca-loc-kho-to").ShouldBe("dishes/_drafts/ca-loc-kho-to.json");
    }

    [Fact]
    public void The_checker_checks_its_arguments()
    {
        using var data = new TempData();
        var dish = Draft();

        Should.Throw<ArgumentNullException>(() => DraftChecker.Check(null!, dish, data.Load(), data.Schemas));
        Should.Throw<ArgumentNullException>(() => DraftChecker.Check("{}", null!, data.Load(), data.Schemas));
        Should.Throw<ArgumentNullException>(() => DraftChecker.Check("{}", dish, null!, data.Schemas));
        Should.Throw<ArgumentNullException>(() => DraftChecker.Check("{}", dish, data.Load(), null!));
    }
}
