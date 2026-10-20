using System.Text.Json;
using System.Text.Json.Nodes;
using MamCom.Core.Json;
using MamCom.Core.Models;
using MamCom.DataBuilder.Validation;

namespace MamCom.DataBuilder.Tests.Validation;

/// <summary>One ingredient held to the rules of the data, without the rest of the data getting in the way.</summary>
public class IngredientCheckTests
{
    private const string File = "ingredients/rau-muong.json";

    private static (string Text, Ingredient Ingredient) Node(JsonObject node)
    {
        var text = node.ToJsonString();
        return (text, JsonSerializer.Deserialize<Ingredient>(text, CoreJson.Options)!);
    }

    private static IReadOnlyList<Diagnostic> Check(TempData data, Action<JsonObject>? change = null, string file = File, string name = "rau-muong")
    {
        var node = data.Read("ingredients", name);
        change?.Invoke(node);
        var (text, ingredient) = Node(node);
        return IngredientCheck.Check(file, text, ingredient, data.Load(), Tests.Schemas.DataFiles.Schemas);
    }

    [Fact]
    public void A_good_ingredient_has_no_finding()
    {
        using var data = new TempData();

        Check(data).ShouldBeEmpty();
    }

    [Fact]
    public void A_schema_problem_is_an_error_at_its_place_under_the_given_file()
    {
        using var data = new TempData();

        var findings = Check(data, o => o["wasteRatio"] = 1.5);

        findings.ShouldNotBeEmpty();
        findings.ShouldAllBe(f => f.Severity == Severity.Error && f.Code == DiagnosticCodes.Schema && f.File == File);
        findings.ShouldContain(f => f.Location == "/wasteRatio");
    }

    [Fact]
    public void A_real_source_must_name_the_entry_it_took_the_numbers_from()
    {
        using var data = new TempData();

        var findings = Check(data, o => o["nutritionSource"]!["db"] = "usda-fdc");   // the placeholder numbers are null and no ref is given

        findings.ShouldNotBeEmpty();
        findings.ShouldAllBe(f => f.Code == DiagnosticCodes.Schema);
    }

    [Fact]
    public void When_the_schema_fails_the_other_rules_are_not_run()
    {
        using var data = new TempData();

        var findings = Check(data, o =>
        {
            o["wasteRatio"] = 1.5;
            o["image"]!["thumb"] = "khong-co.webp";   // would be a missing file
            o["image"]!["source"] = "tự chụp";
            o["image"]!["license"] = "CC0";
        });

        findings.ShouldAllBe(f => f.Code == DiagnosticCodes.Schema);
    }

    [Fact]
    public void A_file_whose_name_is_not_the_ingredient_id_is_reported()
    {
        using var data = new TempData();

        var findings = Check(data, file: "ingredients/ten-khac.json");

        findings.ShouldContain(f => f.Code == DiagnosticCodes.FileName && f.File == "ingredients/ten-khac.json");
    }

    [Fact]
    public void An_image_file_that_is_not_in_the_images_folder_is_an_error_and_one_that_is_there_is_not()
    {
        using var data = new TempData();
        Action<JsonObject> declare = o =>
        {
            o["image"]!["thumb"] = "rau-muong-200.webp";
            o["image"]!["source"] = "tự chụp";
            o["image"]!["license"] = "CC0";
        };

        Check(data, declare).ShouldContain(f => f.Code == DiagnosticCodes.ImageMissing && f.Location == "/image/thumb" && f.Severity == Severity.Error);

        data.WriteImage("rau-muong-200.webp");

        Check(data, declare).ShouldBeEmpty();
    }

    [Fact]
    public void An_image_name_with_a_path_is_an_error()
    {
        using var data = new TempData();

        var findings = Check(data, o =>
        {
            o["image"]!["full"] = "../secrets.webp";
            o["image"]!["source"] = "tự chụp";
            o["image"]!["license"] = "CC0";
        });

        findings.ShouldContain(f => f.Code == DiagnosticCodes.ImagePath && f.Location == "/image/full");
    }

    [Fact]
    public void A_weight_unit_that_asks_for_approximate_grams_is_a_warning()
    {
        using var data = new TempData();

        var findings = Check(data, o =>
        {
            o["marketUnit"] = new JsonObject { ["default"] = new JsonObject { ["display"] = "g", ["gramsPerUnit"] = 1, ["step"] = 50, ["showApproxGrams"] = true } };
        });

        var finding = findings.ShouldHaveSingleItem();
        finding.Code.ShouldBe(DiagnosticCodes.ApproxOnWeightUnit);
        finding.Severity.ShouldBe(Severity.Warning);
        finding.Location.ShouldBe("/marketUnit/default/showApproxGrams");
    }

    [Fact]
    public void An_ingredient_no_dish_uses_is_a_warning_only_for_itself()
    {
        using var data = new TempData();
        data.CopyFile("ingredients", "rau-muong", "rau-muong-2", o => o["id"] = "rau-muong-2");

        var findings = Check(data, o => o["id"] = "rau-muong-2", "ingredients/rau-muong-2.json", "rau-muong");

        var finding = findings.ShouldHaveSingleItem();
        finding.Code.ShouldBe(DiagnosticCodes.UnusedIngredient);
        finding.Severity.ShouldBe(Severity.Warning);
    }

    [Fact]
    public void Findings_about_the_rest_of_the_data_are_not_the_ingredients_business()
    {
        using var data = new TempData();
        data.WriteText("ingredients", "hong", "{ not json");
        data.WriteText("dishes", "hong", "{ not json");
        data.CopyFile("ingredients", "ot", "ot-chi-thien", o => o["id"] = "ot-chi-thien");   // another unused ingredient
        data.Edit("dishes", "rau-muong-luoc", d => d["ingredients"]![0]!["ingredientId"] = "khong-co");   // a dish with an error

        Check(data).ShouldBeEmpty();
    }

    [Fact]
    public void What_the_loader_found_wrong_with_the_file_on_disk_does_not_count_against_the_ingredient_being_checked()
    {
        using var data = new TempData();
        data.Edit("ingredients", "rau-muong", o => o["wasteRatio"] = 1.5);   // the same file is rejected when the folder is loaded
        var loaded = data.Load();
        loaded.Diagnostics.ShouldContain(d => d.File == File);
        var corrected = data.Read("ingredients", "rau-muong");
        corrected["wasteRatio"] = 0.25;
        var (text, ingredient) = Node(corrected);

        var findings = IngredientCheck.Check(File, text, ingredient, loaded, Tests.Schemas.DataFiles.Schemas);

        findings.ShouldBeEmpty();
    }

    [Fact]
    public void The_checker_checks_its_arguments()
    {
        using var data = new TempData();
        var (text, ingredient) = Node(data.Read("ingredients", "rau-muong"));
        var loaded = data.Load();
        var schemas = Tests.Schemas.DataFiles.Schemas;

        Should.Throw<ArgumentException>(() => IngredientCheck.Check(" ", text, ingredient, loaded, schemas));
        Should.Throw<ArgumentNullException>(() => IngredientCheck.Check(File, null!, ingredient, loaded, schemas));
        Should.Throw<ArgumentNullException>(() => IngredientCheck.Check(File, text, null!, loaded, schemas));
        Should.Throw<ArgumentNullException>(() => IngredientCheck.Check(File, text, ingredient, null!, schemas));
        Should.Throw<ArgumentNullException>(() => IngredientCheck.Check(File, text, ingredient, loaded, null!));
    }
}
