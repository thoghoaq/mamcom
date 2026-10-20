using System.Text.Json.Nodes;
using MamCom.Core.Models;
using MamCom.DataBuilder.Loading;
using MamCom.DataBuilder.Validation;
using MamCom.Studio.Editing;
using MamCom.Studio.Review;

namespace MamCom.Studio.Tests;

public class IngredientReviewerTests
{
    private const string Id = "gao";

    private static StoredIngredient Read(StudioData data, string id = Id) => data.Ingredients.Read(id)!;

    /// <summary>The lines of two texts that differ, for texts with the same number of lines; what a Git diff would show.</summary>
    private static string[] ChangedLines(string before, string after)
    {
        var a = before.Split('\n');
        var b = after.Split('\n');
        a.Length.ShouldBe(b.Length, "a change of the numbers must not add or remove lines");
        return [.. a.Zip(b).Where(pair => pair.First != pair.Second).Select(pair => $"{pair.First.Trim()} => {pair.Second.Trim()}")];
    }

    private static readonly double?[] Rice = [370, 7.04, 1.03, 80.3, 0.149, 0.462];

    // ---- Catalog ------------------------------------------------------------------------------------------------------------

    [Fact]
    public void The_catalog_lists_every_ingredient_by_id_with_where_its_numbers_stand()
    {
        using var data = new StudioData();

        var catalog = data.IngredientReviewer.Catalog();

        catalog.Entries.Count.ShouldBe(data.Ingredients.Ids().Count);
        catalog.Entries.Select(e => e.Id).ShouldBe([.. catalog.Entries.Select(e => e.Id).Order(StringComparer.Ordinal)]);
        var entry = catalog.Entries.Single(e => e.Id == "rau-muong");
        entry.Name.ShouldBe("Rau muống");
        entry.NutritionDb.ShouldBe("placeholder");
        entry.IsPlaceholder.ShouldBeTrue();
        entry.VerifiedBy.ShouldBeNull();
        entry.Problem.ShouldBeNull();
        entry.HasImage.ShouldBeFalse();
    }

    [Fact]
    public void The_catalog_counts_the_candidates_FdcImporter_suggested_and_says_none_when_there_is_no_suggestion()
    {
        using var data = new StudioData();
        data.WriteSuggestion("ca-rot", (1, Rice), (2, Rice));
        data.WriteSuggestion("hanh-la", (3, Rice));
        File.Delete(data.PathOf("_suggestions/nutrition/gao.json"));

        var entries = data.IngredientReviewer.Catalog().Entries;

        entries.Single(e => e.Id == "ca-rot").Candidates.ShouldBe(2);
        entries.Single(e => e.Id == "hanh-la").Candidates.ShouldBe(1);
        entries.Single(e => e.Id == "gao").Candidates.ShouldBeNull();
    }

    [Fact]
    public void The_catalog_counts_the_dishes_that_use_an_ingredient()
    {
        using var data = new StudioData();
        var expected = Directory.GetFiles(data.PathOf("dishes"), "*.json")
            .Count(f => JsonNode.Parse(File.ReadAllText(f))!["ingredients"]!.AsArray().Any(l => l!["ingredientId"]!.GetValue<string>() == "rau-muong"));

        expected.ShouldBeGreaterThan(0);
        data.IngredientReviewer.Catalog().Entries.Single(e => e.Id == "rau-muong").UsedBy.ShouldBe(expected);
    }

    [Fact]
    public void The_catalog_says_who_checked_the_numbers_and_that_there_is_a_picture()
    {
        using var data = new StudioData();
        data.WriteImage("gao-200.webp");
        data.EditJson("ingredients/gao.json", o =>
        {
            o["image"]!["thumb"] = "gao-200.webp";
            o["image"]!["source"] = "tự chụp";
            o["image"]!["license"] = "CC0";
        });
        data.WriteSuggestion("gao", (111, Rice));
        data.IngredientReviewer.ChooseNutrition(Id, 111, "Mẹ Lan", Read(data).Version);

        var entry = data.IngredientReviewer.Catalog().Entries.Single(e => e.Id == Id);

        entry.NutritionDb.ShouldBe("usda-fdc");
        entry.IsPlaceholder.ShouldBeFalse();
        entry.VerifiedBy.ShouldBe("Mẹ Lan");
        entry.HasImage.ShouldBeTrue();
        entry.Candidates.ShouldBe(1);
    }

    [Fact]
    public void An_image_counts_when_only_the_full_picture_is_named()
    {
        using var data = new StudioData();
        data.WriteImage("gao-800.webp");
        data.EditJson("ingredients/gao.json", o =>
        {
            o["image"]!["full"] = "gao-800.webp";
            o["image"]!["source"] = "tự chụp";
            o["image"]!["license"] = "CC0";
        });

        data.IngredientReviewer.Catalog().Entries.Single(e => e.Id == Id).HasImage.ShouldBeTrue();
    }

    [Fact]
    public void An_ingredient_that_breaks_a_rule_shows_its_errors()
    {
        using var data = new StudioData();
        data.EditJson("ingredients/gao.json", o => o["wasteRatio"] = 1.5);

        data.IngredientReviewer.Catalog().Entries.Single(e => e.Id == Id).Errors.ShouldBeGreaterThan(0);
        data.IngredientReviewer.Catalog().Entries.Single(e => e.Id == "ca-rot").Errors.ShouldBe(0);
    }

    [Fact]
    public void A_file_that_is_not_an_ingredient_is_an_entry_with_the_reason_and_no_name()
    {
        using var data = new StudioData();
        data.WriteText("ingredients/hong.json", "{ not json");

        var entry = data.IngredientReviewer.Catalog().Entries.Single(e => e.Id == "hong");

        entry.Name.ShouldBeNull();
        entry.NutritionDb.ShouldBeNull();
        entry.IsPlaceholder.ShouldBeFalse();
        entry.Problem.ShouldNotBeNullOrWhiteSpace();
        entry.Errors.ShouldBe(0);
        entry.UsedBy.ShouldBe(0);
    }

    [Fact]
    public void The_name_is_the_default_one_else_the_first_that_exists_else_the_id()
    {
        using var data = new StudioData();
        data.EditJson("ingredients/gao.json", o => o["names"] = new JsonObject { ["north"] = "Gạo Bắc", ["central"] = "Gạo Trung", ["south"] = "Gạo Nam" });
        data.EditJson("ingredients/ca-rot.json", o => o["names"] = new JsonObject { ["default"] = "Cà rốt", ["north"] = "Cà rốt Bắc" });

        var entries = data.IngredientReviewer.Catalog().Entries;

        entries.Single(e => e.Id == Id).Name.ShouldBe("Gạo Bắc");
        entries.Single(e => e.Id == "ca-rot").Name.ShouldBe("Cà rốt");
    }

    [Fact]
    public void The_catalog_of_an_unusable_kitchen_still_lists_the_ingredients_without_counts()
    {
        using var data = new StudioData();
        data.Delete("config/portion-defaults.json");

        var catalog = data.IngredientReviewer.Catalog();

        catalog.Kitchen.IsUsable.ShouldBeFalse();
        catalog.Entries.Count.ShouldBe(data.Ingredients.Ids().Count);
        catalog.Entries.ShouldAllBe(e => e.Errors == 0 && e.Warnings == 0);
    }

    // ---- Evaluate -----------------------------------------------------------------------------------------------------------

    [Fact]
    public void A_good_ingredient_has_no_findings_and_lists_the_dishes_that_use_it_by_id()
    {
        using var data = new StudioData();

        var evaluation = IngredientReviewer.Evaluate(data.Loader.Load(), Read(data, "rau-muong").Ingredient!);

        evaluation.Findings.ShouldBeEmpty();
        evaluation.HasErrors.ShouldBeFalse();
        evaluation.UsedBy.ShouldNotBeEmpty();
        evaluation.UsedBy.Select(d => d.Id).ShouldBe([.. evaluation.UsedBy.Select(d => d.Id).Order(StringComparer.Ordinal)]);
        evaluation.UsedBy.ShouldContain(d => d.Id == "rau-muong-luoc" && d.Name == "Rau muống luộc");
        evaluation.UsedBy.ShouldNotContain(d => d.Id == "canh-bau-tom");
    }

    [Fact]
    public void Errors_and_warnings_are_counted_apart()
    {
        using var data = new StudioData();
        var ingredient = Read(data).Ingredient! with { MarketUnit = new Dictionary<string, MarketUnit> { ["default"] = new("g", 1, 50, ShowApproxGrams: true) } };

        var evaluation = IngredientReviewer.Evaluate(data.Loader.Load(), ingredient);

        evaluation.Warnings.ShouldBe(1);
        evaluation.Errors.ShouldBe(0);

        var broken = IngredientReviewer.Evaluate(data.Loader.Load(), ingredient with { WasteRatio = 2 });
        broken.HasErrors.ShouldBeTrue();
        broken.Errors.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void An_unusable_kitchen_gives_no_findings_but_still_the_dishes_that_use_the_ingredient()
    {
        using var data = new StudioData();
        var ingredient = Read(data, "rau-muong").Ingredient!;
        data.Delete("config/validation-defaults.json");

        var evaluation = IngredientReviewer.Evaluate(data.Loader.Load(), ingredient);

        evaluation.Findings.ShouldBeEmpty();
    }

    [Fact]
    public void Evaluate_checks_its_arguments()
    {
        using var data = new StudioData();

        Should.Throw<ArgumentNullException>(() => IngredientReviewer.Evaluate(null!, Read(data).Ingredient!));
        Should.Throw<ArgumentNullException>(() => IngredientReviewer.Evaluate(data.Loader.Load(), null!));
    }

    // ---- Save ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Saving_an_ingredient_that_did_not_change_writes_nothing()
    {
        using var data = new StudioData();
        var seen = Read(data);
        var written = File.GetLastWriteTimeUtc(data.PathOf("ingredients/gao.json"));

        var result = data.IngredientReviewer.Save(Id, seen.Ingredient!, seen.Version);

        result.Status.ShouldBe(IngredientStatus.Unchanged);
        result.Stored!.Text.ShouldBe(seen.Text);
        File.GetLastWriteTimeUtc(data.PathOf("ingredients/gao.json")).ShouldBe(written);
    }

    [Fact]
    public void An_edit_changes_only_the_lines_that_were_edited()
    {
        using var data = new StudioData();
        var seen = Read(data);
        var form = IngredientForm.From(seen.Ingredient!);
        form.Names["default"] = "Gạo tẻ";
        form.WasteRatio = 0.05;

        var result = data.IngredientReviewer.Save(Id, form.ToIngredient(seen.Ingredient!), seen.Version);

        result.Status.ShouldBe(IngredientStatus.Saved);
        ChangedLines(seen.Text, data.ReadText("ingredients/gao.json")).ShouldBe(
        [
            "\"default\": \"Gạo\" => \"default\": \"Gạo tẻ\"",
            "\"wasteRatio\": 0, => \"wasteRatio\": 0.05,",
        ]);
    }

    [Fact]
    public void The_editor_cannot_change_the_id_the_numbers_or_their_source()
    {
        using var data = new StudioData();
        var seen = Read(data);
        var sneaky = seen.Ingredient! with
        {
            Id = "ten-khac",
            Category = "gao-moi",
            NutritionPer100gEdible = new NutritionFacts(111, 1, 1, 1, 1, 1),
            NutritionSource = new NutritionSource("usda-fdc", "1", "tự bịa", "Ai đó", new DateOnly(2026, 1, 1)),
        };

        var result = data.IngredientReviewer.Save(Id, sneaky, seen.Version);

        result.Status.ShouldBe(IngredientStatus.Saved);
        var saved = result.Stored!.Ingredient!;
        saved.Id.ShouldBe(Id);
        saved.Category.ShouldBe("gao-moi");
        saved.NutritionPer100gEdible.ShouldBe(seen.Ingredient!.NutritionPer100gEdible);
        saved.NutritionSource.ShouldBe(seen.Ingredient.NutritionSource);
        data.Exists("ingredients/ten-khac.json").ShouldBeFalse();
    }

    [Fact]
    public void Saving_an_edit_keeps_the_numbers_a_person_already_chose()
    {
        using var data = new StudioData();
        data.WriteSuggestion("gao", (111, Rice));
        var chosen = data.IngredientReviewer.ChooseNutrition(Id, 111, "Mẹ Lan", Read(data).Version).Stored!;
        var form = IngredientForm.From(chosen.Ingredient!);
        form.IsPantry = false;   // the seed rice is a pantry item; this is a real change

        var result = data.IngredientReviewer.Save(Id, form.ToIngredient(chosen.Ingredient!), chosen.Version);

        result.Status.ShouldBe(IngredientStatus.Saved);
        result.Stored!.Ingredient!.NutritionSource.ShouldBe(chosen.Ingredient!.NutritionSource);
        result.Stored.Ingredient.NutritionPer100gEdible.ShouldBe(chosen.Ingredient.NutritionPer100gEdible);
    }

    [Fact]
    public void An_ingredient_with_an_error_is_not_saved_and_the_file_is_left_alone()
    {
        using var data = new StudioData();
        var seen = Read(data);

        var result = data.IngredientReviewer.Save(Id, seen.Ingredient! with { WasteRatio = 1.5 }, seen.Version);

        result.Status.ShouldBe(IngredientStatus.HasErrors);
        result.Findings.ShouldContain(f => f.Code == DiagnosticCodes.Schema && f.Location == "/wasteRatio");
        result.Stored!.Text.ShouldBe(seen.Text);
        data.ReadText("ingredients/gao.json").ShouldBe(seen.Text);
    }

    [Fact]
    public void A_picture_that_is_not_in_the_images_folder_is_an_error_and_one_that_is_there_is_saved()
    {
        using var data = new StudioData();
        var seen = Read(data);
        var form = IngredientForm.From(seen.Ingredient!);
        form.Image.Thumb = "gao-200.webp";
        form.Image.Source = "tự chụp";
        form.Image.License = "CC0";

        data.IngredientReviewer.Save(Id, form.ToIngredient(seen.Ingredient!), seen.Version).Status.ShouldBe(IngredientStatus.HasErrors);

        data.WriteImage("gao-200.webp");
        var result = data.IngredientReviewer.Save(Id, form.ToIngredient(seen.Ingredient!), seen.Version);

        result.Status.ShouldBe(IngredientStatus.Saved);
        result.Stored!.Ingredient!.Image.ShouldBe(new ImageRef("gao-200.webp", null, "tự chụp", "CC0", false));
    }

    [Fact]
    public void An_ingredient_that_is_broken_on_disk_can_be_corrected_and_saved()
    {
        using var data = new StudioData();
        data.EditJson("ingredients/gao.json", o => o["wasteRatio"] = 1.5);   // the schema rejects the file on disk
        var seen = Read(data);
        data.IngredientReviewer.Catalog().Entries.Single(e => e.Id == Id).Errors.ShouldBeGreaterThan(0);

        var result = data.IngredientReviewer.Save(Id, seen.Ingredient! with { WasteRatio = 0 }, seen.Version);

        result.Status.ShouldBe(IngredientStatus.Saved);   // the error read from this very file must not count against the corrected one
        data.IngredientReviewer.Catalog().Entries.Single(e => e.Id == Id).Errors.ShouldBe(0);
    }

    [Fact]
    public void A_file_that_changed_since_it_was_read_is_never_overwritten()
    {
        using var data = new StudioData();
        var seen = Read(data);
        data.WriteText("ingredients/gao.json", seen.Text.Replace("\"Gạo\"", "\"Gạo (đã sửa nơi khác)\""));
        var elsewhere = data.ReadText("ingredients/gao.json");

        var result = data.IngredientReviewer.Save(Id, seen.Ingredient! with { IsPantry = true }, seen.Version);

        result.Status.ShouldBe(IngredientStatus.Conflict);
        result.Stored!.Text.ShouldBe(elsewhere);
        data.ReadText("ingredients/gao.json").ShouldBe(elsewhere);
    }

    [Fact]
    public void An_ingredient_that_is_gone_or_not_an_ingredient_is_not_found()
    {
        using var data = new StudioData();
        var seen = Read(data);
        data.WriteText("ingredients/hong.json", "{ not json");
        var broken = Read(data, "hong");
        data.Delete("ingredients/gao.json");

        data.IngredientReviewer.Save(Id, seen.Ingredient!, seen.Version).Status.ShouldBe(IngredientStatus.NotFound);
        data.IngredientReviewer.Save("hong", seen.Ingredient!, broken.Version).Status.ShouldBe(IngredientStatus.NotFound);
        data.IngredientReviewer.Save("../config/x", seen.Ingredient!, seen.Version).Status.ShouldBe(IngredientStatus.NotFound);
        data.Exists("ingredients/gao.json").ShouldBeFalse();
    }

    [Fact]
    public void Nothing_is_saved_when_the_kitchen_is_not_usable()
    {
        using var data = new StudioData();
        var seen = Read(data);
        data.Delete("config/cooking-defaults.json");

        data.IngredientReviewer.Save(Id, seen.Ingredient! with { IsPantry = true }, seen.Version).Status.ShouldBe(IngredientStatus.DataNotUsable);
        data.ReadText("ingredients/gao.json").ShouldBe(seen.Text);
    }

    // ---- ChooseNutrition ----------------------------------------------------------------------------------------------------

    [Fact]
    public void Choosing_a_candidate_writes_its_numbers_exactly_and_where_they_came_from()
    {
        using var data = new StudioData();
        data.WriteSuggestion("gao", (111, [100, 1, 1, 1, 1, 1]), (222, Rice));
        var seen = Read(data);

        var result = data.IngredientReviewer.ChooseNutrition(Id, 222, "  Mẹ Lan ", seen.Version);

        result.Status.ShouldBe(IngredientStatus.NutritionChosen);
        var ingredient = result.Stored!.Ingredient!;
        ingredient.NutritionPer100gEdible.ShouldBe(new NutritionFacts(370, 7.04, 1.03, 80.3, 0.149, 0.462));
        ingredient.NutritionSource.ShouldBe(new NutritionSource("usda-fdc", "222", "Test food 222, raw", "Mẹ Lan", StudioData.Today));
        result.Stored.Text.ShouldBe(data.ReadText("ingredients/gao.json"));
    }

    [Fact]
    public void Choosing_a_candidate_changes_the_numbers_and_the_source_and_nothing_else_in_the_file()
    {
        using var data = new StudioData();
        data.WriteSuggestion("gao", (222, Rice));
        var seen = Read(data);

        data.IngredientReviewer.ChooseNutrition(Id, 222, "Mẹ Lan", seen.Version);

        ChangedLines(seen.Text, data.ReadText("ingredients/gao.json")).ShouldBe(
        [
            "\"kcal\": null, => \"kcal\": 370,",
            "\"proteinG\": null, => \"proteinG\": 7.04,",
            "\"fatG\": null, => \"fatG\": 1.03,",
            "\"carbG\": null, => \"carbG\": 80.3,",
            "\"fiberG\": null, => \"fiberG\": 0.149,",
            "\"sodiumMg\": null => \"sodiumMg\": 0.462",
            "\"db\": \"placeholder\", => \"db\": \"usda-fdc\",",
            "\"ref\": null, => \"ref\": \"222\",",
            "\"matchedName\": null, => \"matchedName\": \"Test food 222, raw\",",
            "\"verifiedBy\": null, => \"verifiedBy\": \"Mẹ Lan\",",
            "\"verifiedAt\": null => \"verifiedAt\": \"2026-10-19\"",
        ]);
    }

    [Fact]
    public void The_chosen_ingredient_passes_the_validator_for_the_whole_data_folder()
    {
        using var data = new StudioData();
        data.WriteSuggestion("gao", (222, Rice));

        data.IngredientReviewer.ChooseNutrition(Id, 222, "Mẹ Lan", Read(data).Version);

        var report = DataValidator.Validate(DataLoader.Load(data.Root), ValidationMode.Dev);
        report.Diagnostics.Where(d => d.File == "ingredients/gao.json").ShouldBeEmpty();
        report.HasErrors.ShouldBeFalse();
    }

    [Fact]
    public void The_date_is_todays_date_from_the_clock()
    {
        using var data = new StudioData();
        data.WriteSuggestion("gao", (222, Rice));
        data.Clock.Today = new DateOnly(2027, 2, 3);

        var result = data.IngredientReviewer.ChooseNutrition(Id, 222, "Bà Hoa", Read(data).Version);

        result.Stored!.Ingredient!.NutritionSource.VerifiedAt.ShouldBe(new DateOnly(2027, 2, 3));
    }

    [Fact]
    public void A_candidate_that_lacks_a_nutrient_is_chosen_as_it_is_and_the_nutrient_stays_unknown()
    {
        using var data = new StudioData();
        data.WriteSuggestion("gao", (333, [365, 7.13, 0.66, 80, null, 5]));

        var result = data.IngredientReviewer.ChooseNutrition(Id, 333, "Mẹ Lan", Read(data).Version);

        result.Status.ShouldBe(IngredientStatus.NutritionChosen);
        result.Stored!.Ingredient!.NutritionPer100gEdible.ShouldBe(new NutritionFacts(365, 7.13, 0.66, 80, null, 5));
    }

    [Fact]
    public void A_candidate_with_zeros_is_chosen_because_zero_is_a_number()
    {
        using var data = new StudioData();
        data.WriteSuggestion("muoi", (444, [0, 0, 0, 0, 0, 38758]));

        var result = data.IngredientReviewer.ChooseNutrition("muoi", 444, "Mẹ Lan", Read(data, "muoi").Version);

        result.Status.ShouldBe(IngredientStatus.NutritionChosen);
        result.Stored!.Ingredient!.NutritionPer100gEdible.Kcal.ShouldBe(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Nobody_confirms_numbers_without_a_name(string? verifier)
    {
        using var data = new StudioData();
        data.WriteSuggestion("gao", (222, Rice));
        var seen = Read(data);

        var result = data.IngredientReviewer.ChooseNutrition(Id, 222, verifier, seen.Version);

        result.Status.ShouldBe(IngredientStatus.VerifierMissing);
        data.ReadText("ingredients/gao.json").ShouldBe(seen.Text);
    }

    [Fact]
    public void A_candidate_that_is_not_in_the_suggestion_is_refused_so_numbers_never_come_from_anywhere_else()
    {
        using var data = new StudioData();
        data.WriteSuggestion("gao", (222, Rice));
        var seen = Read(data);

        data.IngredientReviewer.ChooseNutrition(Id, 999, "Mẹ Lan", seen.Version).Status.ShouldBe(IngredientStatus.NoSuchCandidate);
        data.IngredientReviewer.ChooseNutrition(Id, 0, "Mẹ Lan", seen.Version).Status.ShouldBe(IngredientStatus.NoSuchCandidate);
        data.ReadText("ingredients/gao.json").ShouldBe(seen.Text);
    }

    [Fact]
    public void An_ingredient_without_a_suggestion_has_no_candidate_to_choose()
    {
        using var data = new StudioData();
        File.Delete(data.PathOf("_suggestions/nutrition/gao.json"));
        var seen = Read(data);

        data.IngredientReviewer.ChooseNutrition(Id, 222, "Mẹ Lan", seen.Version).Status.ShouldBe(IngredientStatus.NoSuchCandidate);
    }

    [Fact]
    public void A_candidate_of_another_ingredients_suggestion_cannot_be_chosen()
    {
        using var data = new StudioData();
        data.WriteSuggestion("ca-rot", (222, Rice));
        File.Move(data.PathOf("_suggestions/nutrition/ca-rot.json"), data.PathOf("_suggestions/nutrition/gao.json"), overwrite: true);   // gao's file says it is about ca-rot
        var seen = Read(data);

        data.IngredientReviewer.ChooseNutrition(Id, 222, "Mẹ Lan", seen.Version).Status.ShouldBe(IngredientStatus.NoSuchCandidate);
        data.ReadText("ingredients/gao.json").ShouldBe(seen.Text);
    }

    [Fact]
    public void A_candidate_that_states_no_number_at_all_is_refused()
    {
        using var data = new StudioData();
        data.WriteSuggestion("gao", (555, [null, null, null, null, null, null]));
        var seen = Read(data);

        var result = data.IngredientReviewer.ChooseNutrition(Id, 555, "Mẹ Lan", seen.Version);

        result.Status.ShouldBe(IngredientStatus.CandidateHasNoNumbers);
        data.ReadText("ingredients/gao.json").ShouldBe(seen.Text);
    }

    [Fact]
    public void A_candidate_with_a_number_the_schema_refuses_is_not_written()
    {
        using var data = new StudioData();
        data.WriteSuggestion("gao", (666, [1000, 7, 1, 80, 0.1, 0.4]));   // more than 900 kcal in 100g of food is not possible
        var seen = Read(data);

        var result = data.IngredientReviewer.ChooseNutrition(Id, 666, "Mẹ Lan", seen.Version);

        result.Status.ShouldBe(IngredientStatus.HasErrors);
        result.Findings.ShouldContain(f => f.Code == DiagnosticCodes.Schema && f.Location == "/nutritionPer100gEdible/kcal");
        data.ReadText("ingredients/gao.json").ShouldBe(seen.Text);
    }

    [Fact]
    public void Choosing_again_replaces_the_earlier_choice_and_the_same_choice_on_the_same_day_changes_nothing()
    {
        using var data = new StudioData();
        data.WriteSuggestion("gao", (111, [100, 1, 1, 1, 1, 1]), (222, Rice));
        var first = data.IngredientReviewer.ChooseNutrition(Id, 111, "Mẹ Lan", Read(data).Version).Stored!;

        data.IngredientReviewer.ChooseNutrition(Id, 111, "Mẹ Lan", first.Version).Status.ShouldBe(IngredientStatus.Unchanged);

        var second = data.IngredientReviewer.ChooseNutrition(Id, 222, "Bà Hoa", first.Version);

        second.Status.ShouldBe(IngredientStatus.NutritionChosen);
        second.Stored!.Ingredient!.NutritionSource.Ref.ShouldBe("222");
        second.Stored.Ingredient.NutritionSource.VerifiedBy.ShouldBe("Bà Hoa");
        second.Stored.Ingredient.NutritionPer100gEdible.Kcal.ShouldBe(370);
    }

    [Fact]
    public void Choosing_for_a_version_the_person_did_not_see_is_refused()
    {
        using var data = new StudioData();
        data.WriteSuggestion("gao", (222, Rice));
        var seen = Read(data);
        data.WriteText("ingredients/gao.json", seen.Text.Replace("\"Gạo\"", "\"Gạo (đã sửa nơi khác)\""));
        var elsewhere = data.ReadText("ingredients/gao.json");

        var result = data.IngredientReviewer.ChooseNutrition(Id, 222, "Mẹ Lan", seen.Version);

        result.Status.ShouldBe(IngredientStatus.Conflict);
        data.ReadText("ingredients/gao.json").ShouldBe(elsewhere);
    }

    [Fact]
    public void Choosing_for_an_ingredient_that_is_gone_or_when_the_data_is_unusable_says_so()
    {
        using var data = new StudioData();
        data.WriteSuggestion("gao", (222, Rice));
        var seen = Read(data);

        data.IngredientReviewer.ChooseNutrition("khong-co", 222, "Mẹ Lan", seen.Version).Status.ShouldBe(IngredientStatus.NotFound);

        data.Delete("config/spinner-defaults.json");
        data.IngredientReviewer.ChooseNutrition(Id, 222, "Mẹ Lan", seen.Version).Status.ShouldBe(IngredientStatus.DataNotUsable);
        data.ReadText("ingredients/gao.json").ShouldBe(seen.Text);
    }

    // ---- ClearNutrition -----------------------------------------------------------------------------------------------------

    [Fact]
    public void Clearing_a_choice_gives_back_the_original_bytes()
    {
        using var data = new StudioData();
        data.WriteSuggestion("gao", (222, Rice));
        var original = data.ReadText("ingredients/gao.json");
        var chosen = data.IngredientReviewer.ChooseNutrition(Id, 222, "Mẹ Lan", Read(data).Version).Stored!;

        var result = data.IngredientReviewer.ClearNutrition(Id, chosen.Version);

        result.Status.ShouldBe(IngredientStatus.NutritionCleared);
        result.Stored!.Ingredient!.NutritionSource.Db.ShouldBe("placeholder");
        data.ReadText("ingredients/gao.json").ShouldBe(original);
    }

    [Fact]
    public void Clearing_a_placeholder_changes_nothing()
    {
        using var data = new StudioData();
        var seen = Read(data);

        var result = data.IngredientReviewer.ClearNutrition(Id, seen.Version);

        result.Status.ShouldBe(IngredientStatus.Unchanged);
        data.ReadText("ingredients/gao.json").ShouldBe(seen.Text);
    }

    [Fact]
    public void Clearing_refuses_a_version_the_person_did_not_see_an_ingredient_that_is_gone_and_unusable_data()
    {
        using var data = new StudioData();
        data.WriteSuggestion("gao", (222, Rice));
        var chosen = data.IngredientReviewer.ChooseNutrition(Id, 222, "Mẹ Lan", Read(data).Version).Stored!;

        data.IngredientReviewer.ClearNutrition(Id, "0000000000000000").Status.ShouldBe(IngredientStatus.Conflict);
        data.IngredientReviewer.ClearNutrition("khong-co", chosen.Version).Status.ShouldBe(IngredientStatus.NotFound);
        data.Ingredients.Read(Id)!.Ingredient!.NutritionSource.Db.ShouldBe("usda-fdc");

        data.Delete("config/nutrition-defaults.json");
        data.IngredientReviewer.ClearNutrition(Id, chosen.Version).Status.ShouldBe(IngredientStatus.DataNotUsable);
    }

    // ---- arguments ----------------------------------------------------------------------------------------------------------

    [Fact]
    public void The_calls_check_their_arguments()
    {
        using var data = new StudioData();
        var seen = Read(data);

        Should.Throw<ArgumentNullException>(() => data.IngredientReviewer.Save(Id, null!, seen.Version));
        Should.Throw<ArgumentNullException>(() => data.IngredientReviewer.Save(Id, seen.Ingredient!, null!));
        Should.Throw<ArgumentNullException>(() => data.IngredientReviewer.ChooseNutrition(Id, 1, "x", null!));
        Should.Throw<ArgumentNullException>(() => data.IngredientReviewer.ClearNutrition(Id, null!));
    }
}
