using MamCom.Core.Models;
using MamCom.DataBuilder.Loading;
using MamCom.DataBuilder.Validation;
using MamCom.Studio.Editing;
using MamCom.Studio.Review;

namespace MamCom.Studio.Tests;

public class DishReviewerTests
{
    private const string Id = "rau-muong-luoc";

    private static string[] Lines(string text) => text.Split('\n');

    /// <summary>The lines of two texts that differ, for texts with the same number of lines; this is what a Git diff would show.</summary>
    private static string[] ChangedLines(string before, string after)
    {
        var a = Lines(before);
        var b = Lines(after);
        a.Length.ShouldBe(b.Length, "a status change must not add or remove lines");
        return [.. a.Zip(b).Where(pair => pair.First != pair.Second).Select(pair => $"{pair.First.Trim()} => {pair.Second.Trim()}")];
    }

    private static StoredDish Read(StudioData data, DishLocation location = DishLocation.Data, string id = Id) => data.Store.Read(location, id)!;

    private static Dish Edited(StudioData data, Func<Dish, Dish> change) => change(Read(data).Dish!);

    // ---- Catalog ------------------------------------------------------------------------------------------------------------

    [Fact]
    public void The_catalog_lists_every_dish_and_draft_by_id_with_its_status()
    {
        using var data = new StudioData();
        data.MakeDraft("canh-bau-tom");
        data.WriteReport("canh-bau-tom", "warnings");

        var catalog = data.Reviewer.Catalog();

        catalog.Entries.Count.ShouldBe(20);
        catalog.Entries.Select(e => e.Id).ShouldBe(catalog.Entries.Select(e => e.Id).Order(StringComparer.Ordinal).ToArray());
        var draft = catalog.Entries.Single(e => e.Id == "canh-bau-tom");
        draft.Location.ShouldBe(DishLocation.Draft);
        draft.Name.ShouldNotBeNullOrWhiteSpace();
        draft.Slot.ShouldBe(DishSlot.Soup);
        draft.Status.ShouldBe(DishStatus.Draft);
        draft.Note!.Validation.ShouldBe("warnings");
        catalog.Entries.Single(e => e.Id == Id).Location.ShouldBe(DishLocation.Data);
        catalog.Entries.Single(e => e.Id == Id).Note.ShouldBeNull();
    }

    [Fact]
    public void A_dish_and_a_draft_with_the_same_id_are_two_entries_dish_first()
    {
        using var data = new StudioData();
        data.WriteText($"dishes/_drafts/{Id}.json", data.ReadText($"dishes/{Id}.json"));

        var entries = data.Reviewer.Catalog().Entries.Where(e => e.Id == Id).ToArray();

        entries.Select(e => e.Location).ShouldBe([DishLocation.Data, DishLocation.Draft]);
    }

    [Fact]
    public void The_seed_dishes_have_no_errors_and_the_counts_are_per_dish()
    {
        using var data = new StudioData();

        var entries = data.Reviewer.Catalog().Entries;

        entries.ShouldAllBe(e => e.Errors == 0);
        entries.ShouldAllBe(e => e.Problem == null);
    }

    [Fact]
    public void A_dish_that_breaks_a_rule_shows_its_errors_in_the_catalog()
    {
        using var data = new StudioData();
        data.EditJson($"dishes/{Id}.json", d => d["ingredients"]![0]!["ingredientId"] = "khong-co");

        data.Reviewer.Catalog().Entries.Single(e => e.Id == Id).Errors.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void A_file_that_is_not_a_dish_is_an_entry_with_the_reason_and_no_name()
    {
        using var data = new StudioData();
        data.WriteText("dishes/hong.json", "{ not json");

        var entry = data.Reviewer.Catalog().Entries.Single(e => e.Id == "hong");

        entry.Name.ShouldBeNull();
        entry.Status.ShouldBeNull();
        entry.Slot.ShouldBeNull();
        entry.Problem.ShouldNotBeNullOrWhiteSpace();
        entry.Errors.ShouldBe(0);
    }

    [Fact]
    public void A_report_with_no_dish_and_no_draft_is_a_failed_draft_only_when_it_says_failed()
    {
        using var data = new StudioData();
        data.WriteReport("bun-rieu", "failed");
        data.WriteReport("ca-kho", "warnings");      // a report without its draft, but not a failure
        data.WriteReport(Id, "failed");              // a dish of that id exists, so nothing is failed about it
        data.WriteReport("canh-bau-tom", "failed");
        data.MakeDraft("canh-bau-tom");              // and so does a draft

        data.Reviewer.Catalog().FailedDrafts.ShouldBe(["bun-rieu"]);
    }

    [Fact]
    public void A_stray_report_next_to_a_dish_that_is_not_a_draft_is_not_its_note()
    {
        using var data = new StudioData();
        data.WriteReport(Id, "valid");

        data.Reviewer.Catalog().Entries.Single(e => e.Id == Id).Note.ShouldBeNull();
    }

    [Fact]
    public void The_catalog_of_an_unusable_kitchen_still_lists_the_dishes_without_counts()
    {
        using var data = new StudioData();
        data.Delete("config/portion-defaults.json");

        var catalog = data.Reviewer.Catalog();

        catalog.Kitchen.IsUsable.ShouldBeFalse();
        catalog.Entries.Count.ShouldBe(20);
        catalog.Entries.ShouldAllBe(e => e.Errors == 0 && e.Warnings == 0);
    }

    // ---- Evaluate -----------------------------------------------------------------------------------------------------------

    [Fact]
    public void A_good_dish_has_no_findings()
    {
        using var data = new StudioData();

        var evaluation = DishReviewer.Evaluate(data.Loader.Load(), DishLocation.Data, Read(data).Dish!);

        evaluation.Findings.ShouldBeEmpty();
        evaluation.Errors.ShouldBe(0);
        evaluation.Warnings.ShouldBe(0);
        evaluation.HasErrors.ShouldBeFalse();
    }

    [Fact]
    public void Without_nutrition_data_the_nutrition_is_unknown_and_the_ingredients_missing_data_are_named()
    {
        using var data = new StudioData();

        var evaluation = DishReviewer.Evaluate(data.Loader.Load(), DishLocation.Data, Read(data).Dish!);

        evaluation.Nutrition!.PerServing.Kcal.ShouldBeNull();
        evaluation.Nutrition.IngredientIdsMissingData.ShouldBe(["rau-muong", "muoi"]);
        evaluation.NutritionProblem.ShouldBeNull();
    }

    [Fact]
    public void The_nutrition_is_the_sum_Core_adds_up_from_the_ingredients()
    {
        using var data = new StudioData();
        data.SetNutrient(Id, "kcal", 40);   // test-only number; both lines are 100 g (rau muống) and 1 g (muối) edible per serving

        var evaluation = DishReviewer.Evaluate(data.Loader.Load(), DishLocation.Data, Read(data).Dish!);

        evaluation.Nutrition!.PerServing.Kcal!.Value.ShouldBe(40.0 * (100 + 1) / 100, 1e-9);
    }

    [Fact]
    public void A_dish_that_uses_an_ingredient_the_kitchen_does_not_have_gets_an_error_and_no_nutrition()
    {
        using var data = new StudioData();
        var dish = Edited(data, d => d with { Ingredients = [d.Ingredients[0] with { IngredientId = "khong-co" }, .. d.Ingredients.Skip(1)] });

        var evaluation = DishReviewer.Evaluate(data.Loader.Load(), DishLocation.Data, dish);

        evaluation.Findings.ShouldContain(f => f.Code == DiagnosticCodes.UnknownIngredient && f.Severity == Severity.Error);
        evaluation.Nutrition.ShouldBeNull();
        evaluation.NutritionProblem!.ShouldContain("khong-co");
        evaluation.HasErrors.ShouldBeTrue();
    }

    [Fact]
    public void Warnings_are_counted_apart_from_errors()
    {
        using var data = new StudioData();
        var dish = Edited(data, d => d with { Steps = [d.Steps[0], d.Steps[1] with { IngredientIds = ["rau-muong"] }] });

        var evaluation = DishReviewer.Evaluate(data.Loader.Load(), DishLocation.Data, dish);

        evaluation.Warnings.ShouldBe(1);
        evaluation.Errors.ShouldBe(0);
        evaluation.Findings.Single().Code.ShouldBe(DiagnosticCodes.LineNotInSteps);
    }

    [Fact]
    public void Findings_are_filed_under_the_file_the_dish_has_or_will_have()
    {
        using var data = new StudioData();
        var dish = Edited(data, d => d with { Steps = [d.Steps[0], d.Steps[1] with { IngredientIds = ["rau-muong"] }] });
        var kitchen = data.Loader.Load();

        DishReviewer.Evaluate(kitchen, DishLocation.Data, dish).Findings.Single().File.ShouldBe($"dishes/{Id}.json");
        DishReviewer.Evaluate(kitchen, DishLocation.Draft, dish).Findings.Single().File.ShouldBe($"dishes/_drafts/{Id}.json");
    }

    [Fact]
    public void An_unusable_kitchen_gives_no_findings_but_does_not_fail()
    {
        using var data = new StudioData();
        var dish = Read(data).Dish!;
        data.Delete("config/validation-defaults.json");

        var evaluation = DishReviewer.Evaluate(data.Loader.Load(), DishLocation.Data, dish);

        evaluation.Findings.ShouldBeEmpty();
    }

    [Fact]
    public void Evaluate_checks_its_arguments()
    {
        using var data = new StudioData();

        Should.Throw<ArgumentNullException>(() => DishReviewer.Evaluate(null!, DishLocation.Data, Read(data).Dish!));
        Should.Throw<ArgumentNullException>(() => DishReviewer.Evaluate(data.Loader.Load(), DishLocation.Data, null!));
    }

    // ---- Save ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Saving_a_draft_that_passes_the_checks_makes_it_validated_and_changes_only_that_line()
    {
        using var data = new StudioData();
        var before = Read(data);

        var result = data.Reviewer.Save(DishLocation.Data, Id, before.Dish!, before.Version);

        result.Status.ShouldBe(ReviewStatus.Saved);
        result.Stored!.Dish!.Status.ShouldBe(DishStatus.Validated);
        ChangedLines(before.Text, data.ReadText($"dishes/{Id}.json")).ShouldBe(["\"status\": \"draft\", => \"status\": \"validated\","]);
        result.Stored.Text.ShouldBe(data.ReadText($"dishes/{Id}.json"));
    }

    [Fact]
    public void Saving_a_validated_dish_that_did_not_change_writes_nothing()
    {
        using var data = new StudioData();
        var first = data.Reviewer.Save(DishLocation.Data, Id, Read(data).Dish!, Read(data).Version).Stored!;
        var written = File.GetLastWriteTimeUtc(data.PathOf($"dishes/{Id}.json"));

        var again = data.Reviewer.Save(DishLocation.Data, Id, first.Dish!, first.Version);

        again.Status.ShouldBe(ReviewStatus.Unchanged);
        again.Stored!.Version.ShouldBe(first.Version);
        File.GetLastWriteTimeUtc(data.PathOf($"dishes/{Id}.json")).ShouldBe(written);
    }

    [Fact]
    public void An_edit_changes_only_the_lines_that_were_edited()
    {
        using var data = new StudioData();
        var before = Read(data);
        var edited = before.Dish! with { Name = "Rau muống luộc nước chanh" };

        var result = data.Reviewer.Save(DishLocation.Data, Id, edited, before.Version);

        result.Status.ShouldBe(ReviewStatus.Saved);
        ChangedLines(before.Text, data.ReadText($"dishes/{Id}.json")).ShouldBe(
        [
            "\"name\": \"Rau muống luộc\", => \"name\": \"Rau muống luộc nước chanh\",",
            "\"status\": \"draft\", => \"status\": \"validated\",",
        ]);
    }

    [Fact]
    public void Saving_a_draft_in_the_drafts_folder_writes_there_and_does_not_touch_dishes()
    {
        using var data = new StudioData();
        data.MakeDraft(Id);
        var before = Read(data, DishLocation.Draft);

        var result = data.Reviewer.Save(DishLocation.Draft, Id, before.Dish! with { Name = "Rau muống luộc, bản nháp" }, before.Version);

        result.Status.ShouldBe(ReviewStatus.Saved);
        result.Stored!.Location.ShouldBe(DishLocation.Draft);
        data.ReadText($"dishes/_drafts/{Id}.json").ShouldContain("Rau muống luộc, bản nháp");
        data.Exists($"dishes/{Id}.json").ShouldBeFalse();
    }

    [Fact]
    public void The_editor_cannot_change_the_id_the_status_the_provenance_or_the_image()
    {
        using var data = new StudioData();
        var before = Read(data);
        var sneaky = before.Dish! with
        {
            Id = "ten-khac",
            Name = "Đổi tên",
            Status = DishStatus.CookTested,
            Provenance = new Provenance(DraftAuthor.Human, "Mẹ", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 2), 30),
            Image = new ImageRef("a.webp", "b.webp", "tự chụp", "CC0", false),
        };

        var result = data.Reviewer.Save(DishLocation.Data, Id, sneaky, before.Version);

        result.Status.ShouldBe(ReviewStatus.Saved);
        var saved = result.Stored!.Dish!;
        saved.Id.ShouldBe(Id);
        saved.Name.ShouldBe("Đổi tên");
        saved.Status.ShouldBe(DishStatus.Validated);
        saved.Provenance.ShouldBe(before.Dish!.Provenance);
        saved.Image.ShouldBe(before.Dish.Image);
        data.Exists("dishes/ten-khac.json").ShouldBeFalse();
    }

    [Fact]
    public void A_dish_that_is_broken_on_disk_can_be_corrected_and_saved()
    {
        using var data = new StudioData();
        data.EditJson($"dishes/{Id}.json", d => d["ingredients"]![0]!["gramsEdiblePerServing"] = -5);   // the schema rejects the file on disk
        var seen = Read(data);
        data.Reviewer.Catalog().Entries.Single(e => e.Id == Id).Errors.ShouldBeGreaterThan(0);
        var corrected = seen.Dish! with { Ingredients = [seen.Dish.Ingredients[0] with { GramsEdiblePerServing = 100 }, seen.Dish.Ingredients[1]] };

        var result = data.Reviewer.Save(DishLocation.Data, Id, corrected, seen.Version);

        // The error the kitchen read from this very file must not count against the corrected dish.
        result.Status.ShouldBe(ReviewStatus.Saved);
        data.Reviewer.Catalog().Entries.Single(e => e.Id == Id).Errors.ShouldBe(0);
    }

    [Fact]
    public void A_dish_with_an_error_is_not_saved_and_the_file_is_left_alone()
    {
        using var data = new StudioData();
        var before = Read(data);
        var broken = before.Dish! with { Ingredients = [before.Dish.Ingredients[0] with { IngredientId = "khong-co" }, before.Dish.Ingredients[1]] };

        var result = data.Reviewer.Save(DishLocation.Data, Id, broken, before.Version);

        result.Status.ShouldBe(ReviewStatus.HasErrors);
        result.Findings.ShouldContain(f => f.Code == DiagnosticCodes.UnknownIngredient);
        result.Stored!.Text.ShouldBe(before.Text);
        data.ReadText($"dishes/{Id}.json").ShouldBe(before.Text);
    }

    [Fact]
    public void A_dish_that_breaks_the_schema_is_not_saved()
    {
        using var data = new StudioData();
        var before = Read(data);

        var result = data.Reviewer.Save(DishLocation.Data, Id, before.Dish! with { Name = string.Empty }, before.Version);

        result.Status.ShouldBe(ReviewStatus.HasErrors);
        result.Findings.ShouldContain(f => f.Code == DiagnosticCodes.Schema);
        data.ReadText($"dishes/{Id}.json").ShouldBe(before.Text);
    }

    [Fact]
    public void A_warning_does_not_stop_a_save()
    {
        using var data = new StudioData();
        var before = Read(data);
        var edited = before.Dish! with { Steps = [before.Dish.Steps[0], before.Dish.Steps[1] with { IngredientIds = ["rau-muong"] }] };

        var result = data.Reviewer.Save(DishLocation.Data, Id, edited, before.Version);

        result.Status.ShouldBe(ReviewStatus.Saved);
        result.Findings.Single().Severity.ShouldBe(Severity.Warning);
    }

    [Fact]
    public void A_file_that_changed_since_it_was_read_is_never_overwritten()
    {
        using var data = new StudioData();
        var seen = Read(data);
        data.WriteText($"dishes/{Id}.json", seen.Text.Replace("nhặt, rửa sạch", "nhặt kỹ, rửa sạch"));
        var onDisk = data.ReadText($"dishes/{Id}.json");

        var result = data.Reviewer.Save(DishLocation.Data, Id, seen.Dish! with { Name = "Tên của người xem bản cũ" }, seen.Version);

        result.Status.ShouldBe(ReviewStatus.Conflict);
        result.Stored!.Text.ShouldBe(onDisk);
        data.ReadText($"dishes/{Id}.json").ShouldBe(onDisk);
    }

    [Fact]
    public void Saving_a_dish_that_is_gone_says_so()
    {
        using var data = new StudioData();
        var seen = Read(data);
        data.Delete($"dishes/{Id}.json");

        var result = data.Reviewer.Save(DishLocation.Data, Id, seen.Dish!, seen.Version);

        result.Status.ShouldBe(ReviewStatus.NotFound);
        result.Stored.ShouldBeNull();
        data.Exists($"dishes/{Id}.json").ShouldBeFalse();
    }

    [Theory]
    [InlineData("reviewed")]
    [InlineData("cooktested")]
    public void A_reviewed_or_cooktested_dish_cannot_be_edited(string status)
    {
        using var data = new StudioData();
        MakeReviewed(data, status);
        var seen = Read(data);

        var result = data.Reviewer.Save(DishLocation.Data, Id, seen.Dish! with { Name = "Sửa lén" }, seen.Version);

        result.Status.ShouldBe(ReviewStatus.NotEditable);
        data.ReadText($"dishes/{Id}.json").ShouldBe(seen.Text);
    }

    [Fact]
    public void A_file_that_is_not_a_dish_cannot_be_edited()
    {
        using var data = new StudioData();
        data.WriteText("dishes/hong.json", "{ not json");
        var seen = Read(data, id: "hong");

        var result = data.Reviewer.Save(DishLocation.Data, "hong", Read(data).Dish!, seen.Version);

        result.Status.ShouldBe(ReviewStatus.NotEditable);
    }

    [Fact]
    public void Nothing_is_saved_when_the_kitchen_is_not_usable()
    {
        using var data = new StudioData();
        var seen = Read(data);
        data.Delete("config/cooking-defaults.json");

        var result = data.Reviewer.Save(DishLocation.Data, Id, seen.Dish!, seen.Version);

        result.Status.ShouldBe(ReviewStatus.DataNotUsable);
        data.ReadText($"dishes/{Id}.json").ShouldBe(seen.Text);
    }

    // ---- Approve ------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Approving_a_seed_dish_changes_exactly_the_status_and_the_two_review_lines()
    {
        using var data = new StudioData();
        var before = Read(data);

        var result = data.Reviewer.Approve(DishLocation.Data, Id, "Mẹ Lan", before.Version);

        result.Status.ShouldBe(ReviewStatus.Approved);
        ChangedLines(before.Text, data.ReadText($"dishes/{Id}.json")).ShouldBe(
        [
            "\"status\": \"draft\", => \"status\": \"reviewed\",",
            "\"reviewedBy\": null, => \"reviewedBy\": \"Mẹ Lan\",",
            "\"reviewedAt\": null, => \"reviewedAt\": \"2026-10-19\",",
        ]);
        result.Stored!.Dish!.Status.ShouldBe(DishStatus.Reviewed);
        result.Stored.Dish.Provenance.ReviewedBy.ShouldBe("Mẹ Lan");
        result.Stored.Dish.Provenance.ReviewedAt.ShouldBe(StudioData.Today);
        result.Stored.Dish.Provenance.DraftedBy.ShouldBe(DraftAuthor.Llm);
    }

    [Fact]
    public void The_approved_dish_is_clean_for_the_data_builder_and_the_whole_data_folder_still_validates()
    {
        using var data = new StudioData();

        data.Reviewer.Approve(DishLocation.Data, Id, "Mẹ Lan", Read(data).Version);

        var report = DataValidator.Validate(DataLoader.Load(data.Root), ValidationMode.Dev);
        report.Diagnostics.Where(d => d.File == $"dishes/{Id}.json").ShouldBeEmpty();
        report.HasErrors.ShouldBeFalse();
    }

    [Fact]
    public void The_date_is_todays_date_from_the_clock()
    {
        using var data = new StudioData();
        data.Clock.Today = new DateOnly(2027, 2, 3);

        var result = data.Reviewer.Approve(DishLocation.Data, Id, "Bà", Read(data).Version);

        result.Stored!.Dish!.Provenance.ReviewedAt.ShouldBe(new DateOnly(2027, 2, 3));
    }

    [Fact]
    public void The_reviewers_name_is_trimmed()
    {
        using var data = new StudioData();

        var result = data.Reviewer.Approve(DishLocation.Data, Id, "  Mẹ Lan \t", Read(data).Version);

        result.Stored!.Dish!.Provenance.ReviewedBy.ShouldBe("Mẹ Lan");
    }

    [Fact]
    public void A_validated_dish_can_be_approved_too()
    {
        using var data = new StudioData();
        var validated = data.Reviewer.Save(DishLocation.Data, Id, Read(data).Dish!, Read(data).Version).Stored!;

        var result = data.Reviewer.Approve(DishLocation.Data, Id, "Mẹ Lan", validated.Version);

        result.Status.ShouldBe(ReviewStatus.Approved);
        ChangedLines(validated.Text, data.ReadText($"dishes/{Id}.json")).Length.ShouldBe(3);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Nobody_approves_without_a_name(string? reviewer)
    {
        using var data = new StudioData();
        var before = Read(data);

        var result = data.Reviewer.Approve(DishLocation.Data, Id, reviewer, before.Version);

        result.Status.ShouldBe(ReviewStatus.ReviewerMissing);
        data.ReadText($"dishes/{Id}.json").ShouldBe(before.Text);
    }

    [Fact]
    public void A_dish_with_an_error_is_not_approved()
    {
        using var data = new StudioData();
        data.EditJson($"dishes/{Id}.json", d => d["ingredients"]![0]!["ingredientId"] = "khong-co");
        var before = Read(data);

        var result = data.Reviewer.Approve(DishLocation.Data, Id, "Mẹ Lan", before.Version);

        result.Status.ShouldBe(ReviewStatus.HasErrors);
        result.Findings.ShouldContain(f => f.Code == DiagnosticCodes.UnknownIngredient);
        data.ReadText($"dishes/{Id}.json").ShouldBe(before.Text);
    }

    [Fact]
    public void A_dish_with_only_warnings_can_be_approved_and_the_warnings_are_returned()
    {
        using var data = new StudioData();
        data.EditJson($"dishes/{Id}.json", d => d["steps"]![1]!["ingredientIds"] = new System.Text.Json.Nodes.JsonArray("rau-muong"));

        var result = data.Reviewer.Approve(DishLocation.Data, Id, "Mẹ Lan", Read(data).Version);

        result.Status.ShouldBe(ReviewStatus.Approved);
        result.Findings.Single().Severity.ShouldBe(Severity.Warning);
    }

    [Fact]
    public void An_approval_of_a_version_the_person_did_not_see_is_refused()
    {
        using var data = new StudioData();
        var seen = Read(data);
        data.WriteText($"dishes/{Id}.json", seen.Text.Replace("nhặt, rửa sạch", "nhặt kỹ, rửa sạch"));
        var onDisk = data.ReadText($"dishes/{Id}.json");

        var result = data.Reviewer.Approve(DishLocation.Data, Id, "Mẹ Lan", seen.Version);

        result.Status.ShouldBe(ReviewStatus.Conflict);
        data.ReadText($"dishes/{Id}.json").ShouldBe(onDisk);
    }

    [Fact]
    public void Two_approvals_of_the_same_version_approve_once()
    {
        using var data = new StudioData();
        var seen = Read(data);

        var first = data.Reviewer.Approve(DishLocation.Data, Id, "Mẹ Lan", seen.Version);
        var second = data.Reviewer.Approve(DishLocation.Data, Id, "Bà Hoa", seen.Version);

        first.Status.ShouldBe(ReviewStatus.Approved);
        second.Status.ShouldBe(ReviewStatus.Conflict);
        Read(data).Dish!.Provenance.ReviewedBy.ShouldBe("Mẹ Lan");
    }

    [Theory]
    [InlineData("reviewed")]
    [InlineData("cooktested")]
    public void A_dish_that_is_already_reviewed_cannot_be_approved_again(string status)
    {
        using var data = new StudioData();
        MakeReviewed(data, status);
        var seen = Read(data);

        var result = data.Reviewer.Approve(DishLocation.Data, Id, "Người khác", seen.Version);

        result.Status.ShouldBe(ReviewStatus.NotApprovable);
        data.ReadText($"dishes/{Id}.json").ShouldBe(seen.Text);
    }

    [Fact]
    public void Approving_a_dish_that_is_gone_says_so()
    {
        using var data = new StudioData();

        data.Reviewer.Approve(DishLocation.Data, "khong-co", "Mẹ Lan", "0000000000000000").Status.ShouldBe(ReviewStatus.NotFound);
        data.Reviewer.Approve(DishLocation.Data, "../config/x", "Mẹ Lan", "0000000000000000").Status.ShouldBe(ReviewStatus.NotFound);
    }

    [Fact]
    public void Nothing_is_approved_when_the_kitchen_is_not_usable()
    {
        using var data = new StudioData();
        var seen = Read(data);
        data.Delete("config/spinner-defaults.json");

        data.Reviewer.Approve(DishLocation.Data, Id, "Mẹ Lan", seen.Version).Status.ShouldBe(ReviewStatus.DataNotUsable);
        data.ReadText($"dishes/{Id}.json").ShouldBe(seen.Text);
    }

    [Fact]
    public void Approving_a_draft_makes_it_a_dish_and_removes_the_draft_and_its_report()
    {
        using var data = new StudioData();
        var original = data.ReadText($"dishes/{Id}.json");
        data.MakeDraft(Id);
        data.WriteReport(Id, "valid");
        var draft = Read(data, DishLocation.Draft);

        var result = data.Reviewer.Approve(DishLocation.Draft, Id, "Mẹ Lan", draft.Version);

        result.Status.ShouldBe(ReviewStatus.Approved);
        result.Stored!.Location.ShouldBe(DishLocation.Data);
        data.Exists($"dishes/_drafts/{Id}.json").ShouldBeFalse();
        data.Exists($"dishes/_drafts/{Id}.report.json").ShouldBeFalse();
        ChangedLines(original, data.ReadText($"dishes/{Id}.json")).Length.ShouldBe(3);
    }

    [Fact]
    public void A_draft_is_not_promoted_over_a_dish_that_already_has_its_id()
    {
        using var data = new StudioData();
        data.WriteText($"dishes/_drafts/{Id}.json", data.ReadText($"dishes/{Id}.json"));
        var finalText = data.ReadText($"dishes/{Id}.json");
        var draft = Read(data, DishLocation.Draft);

        var result = data.Reviewer.Approve(DishLocation.Draft, Id, "Mẹ Lan", draft.Version);

        result.Status.ShouldBe(ReviewStatus.FinalExists);
        data.ReadText($"dishes/{Id}.json").ShouldBe(finalText);
        data.Exists($"dishes/_drafts/{Id}.json").ShouldBeTrue();
    }

    // ---- Reopen -------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Reopening_an_approved_dish_gives_back_the_original_bytes()
    {
        using var data = new StudioData();
        var original = data.ReadText($"dishes/{Id}.json");
        var approved = data.Reviewer.Approve(DishLocation.Data, Id, "Mẹ Lan", Read(data).Version).Stored!;

        var result = data.Reviewer.Reopen(DishLocation.Data, Id, approved.Version);

        result.Status.ShouldBe(ReviewStatus.Reopened);
        result.Stored!.Dish!.Status.ShouldBe(DishStatus.Draft);
        result.Stored.Dish.Provenance.ReviewedBy.ShouldBeNull();
        result.Stored.Dish.Provenance.ReviewedAt.ShouldBeNull();
        data.ReadText($"dishes/{Id}.json").ShouldBe(original);
    }

    [Theory]
    [InlineData("draft")]
    [InlineData("validated")]
    [InlineData("cooktested")]
    public void Only_a_reviewed_dish_can_be_reopened(string status)
    {
        using var data = new StudioData();
        if (status == "cooktested")
        {
            MakeReviewed(data, status);
        }
        else
        {
            data.EditJson($"dishes/{Id}.json", d => d["status"] = status);
        }

        var seen = Read(data);

        var result = data.Reviewer.Reopen(DishLocation.Data, Id, seen.Version);

        result.Status.ShouldBe(ReviewStatus.NotReopenable);
        data.ReadText($"dishes/{Id}.json").ShouldBe(seen.Text);
    }

    [Fact]
    public void Reopening_a_version_the_person_did_not_see_or_a_dish_that_is_gone_is_refused()
    {
        using var data = new StudioData();
        var approved = data.Reviewer.Approve(DishLocation.Data, Id, "Mẹ Lan", Read(data).Version).Stored!;

        data.Reviewer.Reopen(DishLocation.Data, Id, "0000000000000000").Status.ShouldBe(ReviewStatus.Conflict);
        data.ReadText($"dishes/{Id}.json").ShouldBe(approved.Text);
        data.Reviewer.Reopen(DishLocation.Data, "khong-co", approved.Version).Status.ShouldBe(ReviewStatus.NotFound);
    }

    // ---- arguments ----------------------------------------------------------------------------------------------------------

    [Fact]
    public void The_calls_check_their_arguments()
    {
        using var data = new StudioData();
        var seen = Read(data);

        Should.Throw<ArgumentNullException>(() => data.Reviewer.Save(DishLocation.Data, Id, null!, seen.Version));
        Should.Throw<ArgumentNullException>(() => data.Reviewer.Save(DishLocation.Data, Id, seen.Dish!, null!));
        Should.Throw<ArgumentNullException>(() => data.Reviewer.Approve(DishLocation.Data, Id, "x", null!));
        Should.Throw<ArgumentNullException>(() => data.Reviewer.Reopen(DishLocation.Data, Id, null!));
    }

    // ---- an end to end walk with the form --------------------------------------------------------------------------------------

    [Fact]
    public void Edit_save_approve_and_reopen_through_the_form_leaves_a_clean_diff_at_every_step()
    {
        using var data = new StudioData();
        var original = data.ReadText($"dishes/{Id}.json");

        var form = DishForm.From(Read(data).Dish!);
        form.Steps[1].Text = "Luộc rau trong nước sôi có chút muối đến khi vừa chín, vớt ra để ráo ngay.";
        var saved = data.Reviewer.Save(DishLocation.Data, Id, form.ToDish(Read(data).Dish!), Read(data).Version);
        saved.Status.ShouldBe(ReviewStatus.Saved);
        ChangedLines(original, saved.Stored!.Text).Length.ShouldBe(2);   // the step text and the status

        var approved = data.Reviewer.Approve(DishLocation.Data, Id, "Mẹ Lan", saved.Stored.Version);
        approved.Status.ShouldBe(ReviewStatus.Approved);
        ChangedLines(saved.Stored.Text, approved.Stored!.Text).Length.ShouldBe(3);

        data.Reviewer.Reopen(DishLocation.Data, Id, approved.Stored.Version).Status.ShouldBe(ReviewStatus.Reopened);
        data.Store.Read(DishLocation.Data, Id)!.Dish!.Status.ShouldBe(DishStatus.Draft);
    }

    private static void MakeReviewed(StudioData data, string status) =>
        data.EditJson($"dishes/{Id}.json", d =>
        {
            d["status"] = status;
            d["provenance"]!["reviewedBy"] = "test-only";
            d["provenance"]!["reviewedAt"] = "2026-10-19";
            if (status == "cooktested")
            {
                d["provenance"]!["cookTestedAt"] = "2026-10-19";
                d["provenance"]!["measuredTotalMinutes"] = 25;
            }
        });
}
