using System.Text.Json.Nodes;
using MamCom.DataBuilder.Validation;

namespace MamCom.DataBuilder.Tests.Validation;

/// <summary>One test per rule of SPEC §7.3 and ADR-010: break one thing in the frozen seed (tests/fixtures/seed) and expect that finding, there.</summary>
public class RuleTests
{
    private static Diagnostic Only(ValidationReport report, string code)
    {
        var matching = report.Diagnostics.Where(d => d.Code == code).ToArray();
        matching.Length.ShouldBeGreaterThan(0, $"no finding with code {code}; got: {string.Join("; ", report.Diagnostics.Select(d => d.Code))}");
        return matching[0];
    }

    private static void ShouldHave(ValidationReport report, string code, string file, string location, Severity severity = Severity.Error)
    {
        report.Diagnostics.ShouldContain(
            d => d.Code == code && d.File == file && d.Location == location && d.Severity == severity,
            $"expected {severity} {code} at {file}:{location}; got:{Environment.NewLine}"
            + string.Join(Environment.NewLine, report.Diagnostics.Select(d => $"  {d.Severity} {d.Code} {d.File}:{d.Location} {d.Message}")));
    }

    private static JsonObject Line(JsonObject dish, int index) => dish["ingredients"]![index]!.AsObject();

    // ---- the seed is clean -------------------------------------------------------------------------------------

    [Fact]
    public void The_frozen_seed_has_no_error_and_no_warning_in_a_development_build()
    {
        using var data = new TempData();

        var report = data.Validate();

        report.Diagnostics.ShouldBeEmpty();
        report.HasErrors.ShouldBeFalse();
    }

    // ---- ids and names ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_file_not_named_after_its_id_is_an_error()
    {
        using var data = new TempData();
        File.Move(data.PathOf("dishes", "canh-bau-tom"), data.PathOf("dishes", "canh-bau"));

        var report = data.Validate();

        ShouldHave(report, DiagnosticCodes.FileName, "dishes/canh-bau.json", "/id");
        Only(report, DiagnosticCodes.FileName).Message.ShouldContain("canh-bau-tom");
    }

    [Fact]
    public void Two_ingredient_files_with_the_same_id_are_an_error()
    {
        using var data = new TempData();
        data.CopyFile("ingredients", "ca-loc", "ca-loc-ban-sao");

        var report = data.Validate();

        Only(report, DiagnosticCodes.DuplicateId).Message.ShouldContain("ca-loc");
        ShouldHave(report, DiagnosticCodes.FileName, "ingredients/ca-loc-ban-sao.json", "/id");
    }

    [Fact]
    public void Two_dish_files_with_the_same_id_are_an_error()
    {
        using var data = new TempData();
        data.CopyFile("dishes", "canh-bau-tom", "canh-bau-tom-2");

        Only(data.Validate(), DiagnosticCodes.DuplicateId).Message.ShouldContain("canh-bau-tom");
    }

    // ---- references ------------------------------------------------------------------------------------------------

    [Fact]
    public void An_unknown_ingredient_in_the_recipe_is_an_error_at_that_line()
    {
        using var data = new TempData();
        data.Edit("dishes", "thit-kho-trung", o => Line(o, 1)["ingredientId"] = "trung-vit");

        ShouldHave(data.Validate(), DiagnosticCodes.UnknownIngredient, "dishes/thit-kho-trung.json", "/ingredients/1/ingredientId");
    }

    [Fact]
    public void An_unknown_ingredient_in_a_step_is_an_error_at_that_step()
    {
        using var data = new TempData();
        data.Edit("dishes", "thit-kho-trung", o => o["steps"]![0]!["ingredientIds"]![0] = "trung-vit");

        ShouldHave(data.Validate(), DiagnosticCodes.UnknownIngredient, "dishes/thit-kho-trung.json", "/steps/0/ingredientIds/0");
    }

    [Fact]
    public void A_season_that_is_neither_all_nor_in_the_month_table_is_an_error()
    {
        using var data = new TempData();
        data.Edit("dishes", "canh-bau-tom", o => o["seasons"] = new JsonArray("summer", "monsoon"));

        var report = data.Validate();

        ShouldHave(report, DiagnosticCodes.UnknownSeason, "dishes/canh-bau-tom.json", "/seasons/1");
        report.Errors.Count().ShouldBe(1);
    }

    [Fact]
    public void The_season_ids_come_from_the_spinner_config_not_from_a_fixed_list()
    {
        using var data = new TempData();
        data.Edit("config", "spinner-defaults", o =>
        {
            var months = o["seasonByMonth"]!.AsArray();
            for (var i = 0; i < months.Count; i++)
            {
                months[i] = months[i]!.GetValue<string>() == "summer" ? "dry" : months[i]!.GetValue<string>();
            }
        });

        var report = data.Validate();

        // Every dish that names the old id is now wrong; dishes using the other seasons are not.
        report.Diagnostics.Where(d => d.Code == DiagnosticCodes.UnknownSeason).Select(d => d.File).Distinct().Count().ShouldBe(4);
        report.Diagnostics.ShouldAllBe(d => d.Code == DiagnosticCodes.UnknownSeason);
    }

    [Fact]
    public void Steps_numbered_with_a_gap_or_a_repeat_are_an_error()
    {
        using var data = new TempData();
        data.Edit("dishes", "canh-bau-tom", o => o["steps"]![3]!["order"] = 5);
        data.Edit("dishes", "rau-muong-luoc", o => o["steps"]![1]!["order"] = 1);

        var report = data.Validate();

        ShouldHave(report, DiagnosticCodes.StepOrder, "dishes/canh-bau-tom.json", "/steps");
        ShouldHave(report, DiagnosticCodes.StepOrder, "dishes/rau-muong-luoc.json", "/steps");
    }

    [Fact]
    public void Steps_listed_out_of_order_are_fine_because_the_order_number_decides()
    {
        using var data = new TempData();
        data.Edit("dishes", "rau-muong-luoc", o =>
        {
            var steps = o["steps"]!.AsArray();
            var first = steps[0]!.DeepClone();
            steps.RemoveAt(0);
            steps.Add(first);
        });

        data.Validate().Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void An_ingredient_on_two_lines_of_one_recipe_is_an_error_at_the_later_line()
    {
        using var data = new TempData();
        data.Edit("dishes", "rau-muong-luoc", o => o["ingredients"]!.AsArray().Add(Line(o, 0).DeepClone()));

        ShouldHave(data.Validate(), DiagnosticCodes.DuplicateLine, "dishes/rau-muong-luoc.json", "/ingredients/2/ingredientId");
    }

    [Fact]
    public void The_rice_ingredient_of_the_portion_config_must_exist()
    {
        using var data = new TempData();
        data.Delete("ingredients", "gao");

        ShouldHave(data.Validate(), DiagnosticCodes.RiceMissing, "config/portion-defaults.json", "/riceIngredientId");
    }

    [Fact]
    public void An_ingredient_whose_file_is_broken_is_reported_once_not_again_in_every_dish_that_uses_it()
    {
        using var data = new TempData();
        data.Edit("ingredients", "ca-loc", o => o["wasteRatio"] = 1);

        var report = data.Validate();

        var issue = report.Diagnostics.ShouldHaveSingleItem();
        issue.Code.ShouldBe(DiagnosticCodes.Schema);
        issue.File.ShouldBe("ingredients/ca-loc.json");
    }

    [Fact]
    public void A_broken_ingredient_that_cannot_be_read_at_all_is_also_only_reported_once()
    {
        using var data = new TempData();
        data.WriteText("ingredients", "ca-loc", "{ not json");

        data.Validate().Diagnostics.ShouldHaveSingleItem().File.ShouldBe("ingredients/ca-loc.json");
    }

    [Fact]
    public void A_broken_rice_ingredient_is_not_also_reported_as_missing()
    {
        using var data = new TempData();
        data.Edit("ingredients", "gao", o => o["wasteRatio"] = 1);

        data.Validate().Diagnostics.ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCodes.Schema);
    }

    [Fact]
    public void An_ingredient_with_no_file_at_all_is_still_unknown_in_every_dish_that_uses_it()
    {
        using var data = new TempData();
        data.Delete("ingredients", "ca-loc");

        var report = data.Validate();

        report.Diagnostics.Where(d => d.Code == DiagnosticCodes.UnknownIngredient).Select(d => d.File).Distinct()
            .ShouldBe(["dishes/canh-chua-ca-loc.json"]);
    }

    // ---- images --------------------------------------------------------------------------------------------------------

    [Fact]
    public void A_declared_image_whose_file_is_missing_is_an_error_and_a_present_one_is_not()
    {
        using var data = new TempData();
        data.Edit("ingredients", "ca-loc", o =>
        {
            o["image"]!["thumb"] = "ca-loc-200.webp";
            o["image"]!["full"] = "ca-loc-800.webp";
            o["image"]!["source"] = "tự chụp";
            o["image"]!["license"] = "CC0";
        });

        var missing = data.Validate();
        ShouldHave(missing, DiagnosticCodes.ImageMissing, "ingredients/ca-loc.json", "/image/thumb");
        ShouldHave(missing, DiagnosticCodes.ImageMissing, "ingredients/ca-loc.json", "/image/full");

        data.WriteImage("ca-loc-200.webp");
        var oneLeft = data.Validate();
        oneLeft.Diagnostics.ShouldHaveSingleItem().Location.ShouldBe("/image/full");

        data.WriteImage("ca-loc-800.webp");
        data.Validate().Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void A_dish_image_is_checked_the_same_way()
    {
        using var data = new TempData();
        data.Edit("dishes", "canh-bau-tom", o =>
        {
            o["image"]!["full"] = "canh-bau-tom-800.webp";
            o["image"]!["source"] = "tự chụp";
            o["image"]!["license"] = "CC0";
        });

        ShouldHave(data.Validate(), DiagnosticCodes.ImageMissing, "dishes/canh-bau-tom.json", "/image/full");
    }

    [Theory]
    [InlineData("../secret.webp")]
    [InlineData("sub/ca-loc.webp")]
    [InlineData("sub\\ca-loc.webp")]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\Windows\\win.ini")]
    [InlineData("..")]
    public void An_image_name_that_is_a_path_is_an_error_whether_or_not_the_file_exists(string name)
    {
        using var data = new TempData();
        data.Edit("ingredients", "ca-loc", o =>
        {
            o["image"]!["thumb"] = name;
            o["image"]!["source"] = "tự chụp";
            o["image"]!["license"] = "CC0";
        });

        ShouldHave(data.Validate(), DiagnosticCodes.ImagePath, "ingredients/ca-loc.json", "/image/thumb");
    }

    // ---- release --------------------------------------------------------------------------------------------------------

    [Fact]
    public void A_release_build_refuses_every_placeholder_ingredient_and_every_dish_below_reviewed()
    {
        using var data = new TempData();

        var report = data.Validate(ValidationMode.Release);

        report.Diagnostics.Count(d => d.Code == DiagnosticCodes.ReleasePlaceholder).ShouldBe(33);
        report.Diagnostics.Count(d => d.Code == DiagnosticCodes.ReleaseStatus).ShouldBe(20);
        ShouldHave(report, DiagnosticCodes.ReleasePlaceholder, "ingredients/gao.json", "/nutritionSource/db");
        ShouldHave(report, DiagnosticCodes.ReleaseStatus, "dishes/thit-kho-trung.json", "/status");
        Only(report, DiagnosticCodes.ReleaseStatus).Message.ShouldContain("draft");
    }

    [Fact]
    public void The_same_data_is_fine_in_a_development_build()
    {
        using var data = new TempData();

        data.Validate(ValidationMode.Dev).HasErrors.ShouldBeFalse();
    }

    [Fact]
    public void Data_with_real_sources_and_reviewed_dishes_passes_a_release_build()
    {
        using var data = new TempData();
        data.MakeReleasable();

        var report = data.Validate(ValidationMode.Release);

        report.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void One_placeholder_ingredient_is_enough_to_fail_a_release_build()
    {
        using var data = new TempData();
        data.MakeReleasable();
        data.Edit("ingredients", "ca-loc", o =>
        {
            o["nutritionSource"]!["db"] = "placeholder";
            o["nutritionSource"]!["ref"] = null;
            o["nutritionSource"]!["matchedName"] = null;
        });

        var issue = data.Validate(ValidationMode.Release).Diagnostics.ShouldHaveSingleItem();

        issue.Code.ShouldBe(DiagnosticCodes.ReleasePlaceholder);
        issue.File.ShouldBe("ingredients/ca-loc.json");
    }

    [Theory]
    [InlineData("draft")]
    [InlineData("validated")]
    public void One_dish_below_reviewed_is_enough_to_fail_a_release_build(string status)
    {
        using var data = new TempData();
        data.MakeReleasable();
        data.Edit("dishes", "ca-thu-chien", o =>
        {
            o["status"] = status;
            o["provenance"]!["reviewedBy"] = null;
            o["provenance"]!["reviewedAt"] = null;
        });

        var issue = data.Validate(ValidationMode.Release).Diagnostics.ShouldHaveSingleItem();

        issue.Code.ShouldBe(DiagnosticCodes.ReleaseStatus);
        issue.File.ShouldBe("dishes/ca-thu-chien.json");
        issue.Message.ShouldContain(status);
    }

    [Fact]
    public void A_cook_tested_dish_passes_a_release_build()
    {
        using var data = new TempData();
        data.MakeReleasable();
        data.Edit("dishes", "ca-thu-chien", o =>
        {
            o["status"] = "cooktested";
            o["provenance"]!["cookTestedAt"] = "2026-10-20";
        });

        data.Validate(ValidationMode.Release).Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void A_warning_is_an_error_in_a_release_build_and_a_warning_in_a_development_build()
    {
        using var data = new TempData();
        data.MakeReleasable();
        data.CopyFile("ingredients", "ot", "ot-chi-thien", o => o["id"] = "ot-chi-thien");

        var dev = data.Validate(ValidationMode.Dev);
        var release = data.Validate(ValidationMode.Release);

        ShouldHave(dev, DiagnosticCodes.UnusedIngredient, "ingredients/ot-chi-thien.json", string.Empty, Severity.Warning);
        dev.HasErrors.ShouldBeFalse();
        ShouldHave(release, DiagnosticCodes.UnusedIngredient, "ingredients/ot-chi-thien.json", string.Empty);
        release.HasErrors.ShouldBeTrue();
    }

    // ---- warnings -------------------------------------------------------------------------------------------------------

    [Fact]
    public void An_ingredient_no_step_mentions_is_a_warning_but_rice_is_exempt()
    {
        using var data = new TempData();
        data.CopyFile("ingredients", "ot", "ot-chi-thien", o => o["id"] = "ot-chi-thien");

        var report = data.Validate();

        report.Diagnostics.ShouldHaveSingleItem();
        ShouldHave(report, DiagnosticCodes.UnusedIngredient, "ingredients/ot-chi-thien.json", string.Empty, Severity.Warning);
    }

    [Fact]
    public void A_recipe_line_that_no_step_uses_is_a_warning()
    {
        using var data = new TempData();
        data.Edit("dishes", "rau-muong-luoc", o => o["steps"]![1]!["ingredientIds"] = new JsonArray("rau-muong"));

        ShouldHave(data.Validate(), DiagnosticCodes.LineNotInSteps, "dishes/rau-muong-luoc.json", "/ingredients/1/ingredientId", Severity.Warning);
    }

    [Fact]
    public void A_step_that_names_an_ingredient_missing_from_the_recipe_is_a_warning()
    {
        using var data = new TempData();
        data.Edit("dishes", "rau-muong-luoc", o => o["steps"]![0]!["ingredientIds"]!.AsArray().Add("toi"));

        ShouldHave(data.Validate(), DiagnosticCodes.StepIngredientNotInRecipe, "dishes/rau-muong-luoc.json", "/steps/0/ingredientIds/1", Severity.Warning);
    }

    [Fact]
    public void Asking_for_approximate_grams_on_a_weight_unit_is_a_warning_and_on_a_counted_unit_is_not()
    {
        using var data = new TempData();
        data.Edit("ingredients", "thit-bo", o => o["marketUnit"]!["default"]!["showApproxGrams"] = true);
        data.Edit("ingredients", "ca-thu", o => o["marketUnit"]!["default"]!["showApproxGrams"] = true);

        var report = data.Validate();

        report.Diagnostics.ShouldHaveSingleItem();
        ShouldHave(report, DiagnosticCodes.ApproxOnWeightUnit, "ingredients/thit-bo.json", "/marketUnit/default/showApproxGrams", Severity.Warning);
    }

    [Theory]
    [InlineData("canh-trung-ca-chua", 900.0, true)]    // 128 g at 900 kcal/100g = 1152 kcal, far above a soup
    [InlineData("canh-trung-ca-chua", 1.0, true)]      // about 1 kcal, far below
    [InlineData("canh-trung-ca-chua", 100.0, false)]   // about 128 kcal, plausible
    [InlineData("thit-kho-trung", 900.0, true)]
    [InlineData("thit-kho-trung", 120.0, false)]
    [InlineData("rau-muong-luoc", 900.0, true)]
    [InlineData("rau-muong-luoc", 40.0, false)]
    public void Energy_per_serving_outside_the_range_of_its_slot_is_a_warning(string dish, double kcalPer100g, bool warns)
    {
        using var data = new TempData();
        data.SetNutrient(dish, "kcal", kcalPer100g);

        var report = data.Validate();

        if (warns)
        {
            ShouldHave(report, DiagnosticCodes.KcalRange, $"dishes/{dish}.json", string.Empty, Severity.Warning);
        }
        else
        {
            report.Diagnostics.ShouldBeEmpty();
        }
    }

    [Fact]
    public void The_energy_warning_says_how_much_and_what_range_was_expected()
    {
        using var data = new TempData();
        data.SetNutrient("canh-trung-ca-chua", "kcal", 900);

        var message = Only(data.Validate(), DiagnosticCodes.KcalRange).Message;

        message.ShouldContain("1152");
        message.ShouldContain("30");
        message.ShouldContain("250");
        message.ShouldContain("canh");
    }

    [Fact]
    public void Energy_that_is_unknown_because_one_ingredient_lacks_data_is_not_judged()
    {
        using var data = new TempData();
        data.SetNutrient("canh-trung-ca-chua", "kcal", 900);
        data.Edit("ingredients", "muoi", o => o["nutritionPer100gEdible"]!["kcal"] = null);   // salt is in that soup

        data.Validate().Diagnostics.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(2000.0, true)]
    [InlineData(500.0, false)]
    public void More_sodium_per_serving_than_the_limit_is_a_warning(double sodiumMgPer100g, bool warns)
    {
        using var data = new TempData();
        data.SetNutrient("rau-muong-luoc", "sodiumMg", sodiumMgPer100g);

        var report = data.Validate();

        if (warns)
        {
            ShouldHave(report, DiagnosticCodes.SodiumHigh, "dishes/rau-muong-luoc.json", string.Empty, Severity.Warning);
        }
        else
        {
            report.Diagnostics.ShouldBeEmpty();
        }
    }

    [Fact]
    public void Sodium_exactly_at_the_limit_is_fine_and_just_above_it_is_a_warning()
    {
        using var data = new TempData();
        data.Edit("dishes", "rau-muong-luoc", o =>
        {
            Line(o, 0)["gramsEdiblePerServing"] = 99;
            Line(o, 1)["gramsEdiblePerServing"] = 1;
        });

        data.SetNutrient("rau-muong-luoc", "sodiumMg", 1000);   // 100 g at 1000 mg per 100 g: exactly 1000 mg
        data.Validate().Diagnostics.ShouldBeEmpty();

        data.SetNutrient("rau-muong-luoc", "sodiumMg", 1001);
        ShouldHave(data.Validate(), DiagnosticCodes.SodiumHigh, "dishes/rau-muong-luoc.json", string.Empty, Severity.Warning);
    }

    [Fact]
    public void An_ingredient_only_a_recipe_line_names_is_unused_because_no_step_uses_it()
    {
        using var data = new TempData();
        data.Edit("dishes", "canh-chua-ca-loc", o => o["steps"]![1]!["ingredientIds"]!.AsArray().RemoveAt(2));   // bac-ha is only used here

        var report = data.Validate();

        ShouldHave(report, DiagnosticCodes.LineNotInSteps, "dishes/canh-chua-ca-loc.json", "/ingredients/2/ingredientId", Severity.Warning);
        ShouldHave(report, DiagnosticCodes.UnusedIngredient, "ingredients/bac-ha.json", string.Empty, Severity.Warning);
        report.Diagnostics.Count.ShouldBe(2);
    }

    [Theory]
    [InlineData("g", true)]
    [InlineData("gam", true)]
    [InlineData("kg", true)]
    [InlineData("lạng", true)]
    [InlineData("lượng", true)]
    [InlineData("cân", true)]
    [InlineData("KG", true)]
    [InlineData("con", false)]
    [InlineData("quả", false)]
    [InlineData("bó", false)]
    public void Weight_units_are_recognised_by_their_label(string display, bool warns)
    {
        using var data = new TempData();
        data.Edit("ingredients", "thit-bo", o =>
        {
            o["marketUnit"]!["default"]!["display"] = display;
            o["marketUnit"]!["default"]!["showApproxGrams"] = true;
        });

        data.Validate().Diagnostics.Any(d => d.Code == DiagnosticCodes.ApproxOnWeightUnit).ShouldBe(warns);
    }

    [Theory]
    [InlineData(200.0, 55.0, true)]    // 255 g of protein sources
    [InlineData(10.0, 5.0, true)]      // 15 g
    [InlineData(90.0, 55.0, false)]    // the seed's 145 g
    public void Protein_sources_per_serving_of_a_main_outside_the_range_are_a_warning(double meat, double egg, bool warns)
    {
        using var data = new TempData();
        data.Edit("dishes", "thit-kho-trung", o =>
        {
            Line(o, 0)["gramsEdiblePerServing"] = meat;
            Line(o, 1)["gramsEdiblePerServing"] = egg;
        });

        var report = data.Validate();

        if (warns)
        {
            ShouldHave(report, DiagnosticCodes.ProteinRange, "dishes/thit-kho-trung.json", string.Empty, Severity.Warning);
        }
        else
        {
            report.Diagnostics.ShouldBeEmpty();
        }
    }

    [Theory]
    [InlineData(95.0, 55.0, false)]    // 150 g exactly
    [InlineData(95.1, 55.0, true)]     // just over
    [InlineData(45.0, 5.0, false)]     // 50 g exactly
    [InlineData(44.9, 5.0, true)]      // just under
    public void The_protein_range_includes_both_ends(double meat, double egg, bool warns)
    {
        using var data = new TempData();
        data.Edit("dishes", "thit-kho-trung", o =>
        {
            Line(o, 0)["gramsEdiblePerServing"] = meat;
            Line(o, 1)["gramsEdiblePerServing"] = egg;
        });

        data.Validate().Diagnostics.Any(d => d.Code == DiagnosticCodes.ProteinRange).ShouldBe(warns);
    }

    [Fact]
    public void The_protein_range_applies_to_mains_only()
    {
        using var data = new TempData();
        data.Edit("dishes", "canh-rau-ngot-thit-bam", o => Line(o, 1)["gramsEdiblePerServing"] = 400);   // a soup with a lot of meat

        data.Validate().Diagnostics.Any(d => d.Code == DiagnosticCodes.ProteinRange).ShouldBeFalse();
    }

    [Fact]
    public void The_thresholds_come_from_the_validation_config()
    {
        using var data = new TempData();
        data.SetNutrient("canh-trung-ca-chua", "kcal", 100);   // about 128 kcal: fine by default
        data.Edit("config", "validation-defaults", o => o["kcalPerServing"]!["canh"]!["max"] = 100);

        ShouldHave(data.Validate(), DiagnosticCodes.KcalRange, "dishes/canh-trung-ca-chua.json", string.Empty, Severity.Warning);
    }

    // ---- reporting --------------------------------------------------------------------------------------------------------

    [Fact]
    public void Findings_come_in_a_fixed_order_by_file_then_place_then_code()
    {
        using var data = new TempData();
        data.Edit("dishes", "rau-muong-luoc", o => o["steps"]![1]!["order"] = 1);
        data.Edit("dishes", "canh-bau-tom", o => o["seasons"] = new JsonArray("monsoon"));
        data.CopyFile("ingredients", "ot", "ot-chi-thien", o => o["id"] = "ot-chi-thien");

        var findings = data.Validate().Diagnostics;

        findings.Select(d => d.File).ShouldBe(findings.Select(d => d.File).Order(StringComparer.Ordinal));
        findings.Count.ShouldBeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void Problems_found_while_loading_are_part_of_the_report()
    {
        using var data = new TempData();
        data.Edit("ingredients", "ca-loc", o => o["wasteRatio"] = 1);

        var report = data.Validate();

        report.HasErrors.ShouldBeTrue();
        ShouldHave(report, DiagnosticCodes.Schema, "ingredients/ca-loc.json", "/wasteRatio");
    }
}
