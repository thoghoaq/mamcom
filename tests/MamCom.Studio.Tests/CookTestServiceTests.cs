using System.Text.Json.Nodes;
using MamCom.Core.Models;
using MamCom.DataBuilder.Loading;
using MamCom.DataBuilder.Validation;
using MamCom.Studio.Review;

namespace MamCom.Studio.Tests;

public class CookTestServiceTests
{
    private const string Id = "rau-muong-luoc";

    private static readonly DateOnly Today = StudioData.Today;

    private static StoredDish Read(StudioData data, string id = Id) => data.Store.Read(DishLocation.Data, id)!;

    /// <summary>A dish a person has reviewed, written the way Studio writes it.</summary>
    private static StoredDish Reviewed(StudioData data, string id = Id)
    {
        var seen = Read(data, id);
        return data.Reviewer.Approve(DishLocation.Data, id, "Mẹ Lan", seen.Version).Stored!;
    }

    private static CookTestEntry Entry(int? minutes = 47, int? servings = 4, int? grams = 1450, DateOnly? day = null, ImageRef? photo = null) =>
        new(day ?? Today, minutes, servings, grams, photo);

    private static string[] ChangedLines(string before, string after)
    {
        var a = before.Split('\n');
        var b = after.Split('\n');
        a.Length.ShouldBe(b.Length, "this change must not add or remove lines");
        return [.. a.Zip(b).Where(pair => pair.First != pair.Second).Select(pair => $"{pair.First.Trim()} => {pair.Second.Trim()}")];
    }

    // ---- recording ----------------------------------------------------------------------------------------------------------

    [Fact]
    public void A_cook_test_of_a_reviewed_dish_makes_it_cooktested_with_the_day_the_minutes_the_servings_and_the_weight()
    {
        using var data = new StudioData();
        var reviewed = Reviewed(data);

        var result = data.Reviewer.RecordCookTest(DishLocation.Data, Id, Entry(), reviewed.Version);

        result.Status.ShouldBe(ReviewStatus.CookTested);
        var dish = result.Stored!.Dish!;
        dish.Status.ShouldBe(DishStatus.CookTested);
        dish.Provenance.ShouldBe(new Provenance(DraftAuthor.Llm, "Mẹ Lan", Today, Today, 47, 4, 1450));
        result.Stored.Text.ShouldBe(data.ReadText($"dishes/{Id}.json"));
    }

    [Fact]
    public void Nothing_else_about_the_dish_changes_when_a_cook_test_is_recorded()
    {
        using var data = new StudioData();
        var reviewed = Reviewed(data);
        data.Clock.Today = new DateOnly(2026, 10, 25);

        var cooked = data.Reviewer.RecordCookTest(DishLocation.Data, Id, Entry(day: new DateOnly(2026, 10, 24)), reviewed.Version).Stored!.Dish!;

        cooked.ShouldBe(reviewed.Dish! with
        {
            Status = DishStatus.CookTested,
            Provenance = reviewed.Dish.Provenance with { CookTestedAt = new DateOnly(2026, 10, 24), MeasuredTotalMinutes = 47, MeasuredServings = 4, MeasuredFinishedGrams = 1450 },
        }, new DishComparer());
    }

    [Fact]
    public void Without_servings_and_weight_the_file_gets_exactly_three_changed_lines_and_none_added()
    {
        using var data = new StudioData();
        var reviewed = Reviewed(data);

        var result = data.Reviewer.RecordCookTest(DishLocation.Data, Id, Entry(servings: null, grams: null), reviewed.Version);

        result.Status.ShouldBe(ReviewStatus.CookTested);
        ChangedLines(reviewed.Text, result.Stored!.Text).ShouldBe(
        [
            "\"status\": \"reviewed\", => \"status\": \"cooktested\",",
            "\"cookTestedAt\": null, => \"cookTestedAt\": \"2026-10-19\",",
            "\"measuredTotalMinutes\": null => \"measuredTotalMinutes\": 47",
        ]);
        result.Stored.Text.ShouldNotContain("measuredServings");
    }

    [Fact]
    public void With_servings_and_weight_they_are_written_after_the_minutes()
    {
        using var data = new StudioData();
        var reviewed = Reviewed(data);

        var text = data.Reviewer.RecordCookTest(DishLocation.Data, Id, Entry(), reviewed.Version).Stored!.Text;

        text.ShouldContain("\"measuredTotalMinutes\": 47,\n    \"measuredServings\": 4,\n    \"measuredFinishedGrams\": 1450\n  },");
    }

    [Fact]
    public void A_photo_replaces_the_picture_of_the_dish_and_no_photo_keeps_the_one_it_has()
    {
        using var data = new StudioData();
        data.WriteImage("rau-muong-luoc-200.webp");
        data.WriteImage("rau-muong-luoc-800.webp");
        var reviewed = Reviewed(data);
        var photo = new ImageRef("rau-muong-luoc-200.webp", "rau-muong-luoc-800.webp", "tự chụp", "CC0", false);

        var withPhoto = data.Reviewer.RecordCookTest(DishLocation.Data, Id, Entry(photo: photo), reviewed.Version);

        withPhoto.Status.ShouldBe(ReviewStatus.CookTested);
        withPhoto.Stored!.Dish!.Image.ShouldBe(photo);

        var undone = data.Reviewer.UndoCookTest(DishLocation.Data, Id, withPhoto.Stored.Version).Stored!;
        var again = data.Reviewer.RecordCookTest(DishLocation.Data, Id, Entry(photo: null), undone.Version);

        again.Status.ShouldBe(ReviewStatus.CookTested);   // not an error that leaves the file, and so its picture, as it was
        again.Stored!.Dish!.Image.ShouldBe(photo);   // the picture the dish has now is kept
        DishFile.Parse(data.ReadText($"dishes/{Id}.json")).Image.ShouldBe(photo);
    }

    [Fact]
    public void The_whole_data_folder_still_validates_after_a_cook_test()
    {
        using var data = new StudioData();
        data.Reviewer.RecordCookTest(DishLocation.Data, Id, Entry(), Reviewed(data).Version);

        var report = DataValidator.Validate(DataLoader.Load(data.Root), ValidationMode.Dev);

        report.Diagnostics.Where(d => d.File == $"dishes/{Id}.json").ShouldBeEmpty();
        report.HasErrors.ShouldBeFalse();
    }

    [Fact]
    public void Today_is_a_fine_day_for_a_cook_test_and_so_is_an_earlier_one()
    {
        using var data = new StudioData();
        var reviewed = Reviewed(data);

        data.Reviewer.RecordCookTest(DishLocation.Data, Id, Entry(day: Today), reviewed.Version).Status.ShouldBe(ReviewStatus.CookTested);

        var undone = data.Reviewer.UndoCookTest(DishLocation.Data, Id, Read(data).Version).Stored!;
        data.Reviewer.RecordCookTest(DishLocation.Data, Id, Entry(day: Today.AddDays(-30)), undone.Version).Status.ShouldBe(ReviewStatus.CookTested);
    }

    // ---- refusals ------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("draft")]
    [InlineData("validated")]
    public void A_dish_nobody_has_reviewed_cannot_be_cook_tested(string status)
    {
        using var data = new StudioData();
        data.EditJson($"dishes/{Id}.json", d => d["status"] = status);
        var seen = Read(data);

        var result = data.Reviewer.RecordCookTest(DishLocation.Data, Id, Entry(), seen.Version);

        result.Status.ShouldBe(ReviewStatus.NotCookTestable);
        data.ReadText($"dishes/{Id}.json").ShouldBe(seen.Text);
    }

    [Fact]
    public void A_dish_that_is_already_cook_tested_has_to_be_taken_back_before_another_test_is_recorded()
    {
        using var data = new StudioData();
        var cooked = data.Reviewer.RecordCookTest(DishLocation.Data, Id, Entry(), Reviewed(data).Version).Stored!;

        var result = data.Reviewer.RecordCookTest(DishLocation.Data, Id, Entry(minutes: 90), cooked.Version);

        result.Status.ShouldBe(ReviewStatus.NotCookTestable);
        data.ReadText($"dishes/{Id}.json").ShouldBe(cooked.Text);
    }

    [Fact]
    public void A_cook_test_without_minutes_is_refused_and_the_file_is_left_alone()
    {
        using var data = new StudioData();
        var reviewed = Reviewed(data);

        var result = data.Reviewer.RecordCookTest(DishLocation.Data, Id, Entry(minutes: null), reviewed.Version);

        result.Status.ShouldBe(ReviewStatus.MinutesMissing);
        data.ReadText($"dishes/{Id}.json").ShouldBe(reviewed.Text);
    }

    [Fact]
    public void A_dish_cannot_have_been_cooked_tomorrow()
    {
        using var data = new StudioData();
        var reviewed = Reviewed(data);

        var result = data.Reviewer.RecordCookTest(DishLocation.Data, Id, Entry(day: Today.AddDays(1)), reviewed.Version);

        result.Status.ShouldBe(ReviewStatus.CookDateInFuture);
        data.ReadText($"dishes/{Id}.json").ShouldBe(reviewed.Text);
    }

    [Theory]
    [InlineData(0, 4, 1450)]          // no minutes at all
    [InlineData(1441, 4, 1450)]       // more than a day
    [InlineData(47, 4, null)]         // servings without their weight
    [InlineData(47, null, 1450)]      // a weight without its servings
    [InlineData(47, 0, 1450)]
    [InlineData(47, 101, 1450)]
    [InlineData(47, 4, 0)]
    [InlineData(47, 4, 50001)]
    public void Numbers_the_schema_refuses_are_an_error_and_nothing_is_written(int minutes, int? servings, int? grams)
    {
        using var data = new StudioData();
        var reviewed = Reviewed(data);

        var result = data.Reviewer.RecordCookTest(DishLocation.Data, Id, Entry(minutes, servings, grams), reviewed.Version);

        result.Status.ShouldBe(ReviewStatus.HasErrors);
        result.Findings.ShouldContain(f => f.Severity == Severity.Error && f.Code == DiagnosticCodes.Schema);
        data.ReadText($"dishes/{Id}.json").ShouldBe(reviewed.Text);
    }

    [Fact]
    public void A_photo_that_is_not_in_the_images_folder_or_has_no_source_is_an_error()
    {
        using var data = new StudioData();
        data.WriteImage("rau-muong-luoc-200.webp");
        var reviewed = Reviewed(data);

        var missing = data.Reviewer.RecordCookTest(DishLocation.Data, Id, Entry(photo: new ImageRef("khong-co.webp", null, "tự chụp", "CC0", false)), reviewed.Version);
        var unsourced = data.Reviewer.RecordCookTest(DishLocation.Data, Id, Entry(photo: new ImageRef("rau-muong-luoc-200.webp", null, null, null, false)), reviewed.Version);

        missing.Status.ShouldBe(ReviewStatus.HasErrors);
        missing.Findings.ShouldContain(f => f.Code == DiagnosticCodes.ImageMissing);
        unsourced.Status.ShouldBe(ReviewStatus.HasErrors);
        data.ReadText($"dishes/{Id}.json").ShouldBe(reviewed.Text);
    }

    [Fact]
    public void A_file_that_changed_since_it_was_read_is_never_overwritten()
    {
        using var data = new StudioData();
        var reviewed = Reviewed(data);
        data.WriteText($"dishes/{Id}.json", reviewed.Text.Replace("nhặt, rửa sạch", "nhặt kỹ, rửa sạch"));
        var elsewhere = data.ReadText($"dishes/{Id}.json");

        var result = data.Reviewer.RecordCookTest(DishLocation.Data, Id, Entry(), reviewed.Version);

        result.Status.ShouldBe(ReviewStatus.Conflict);
        result.Stored!.Text.ShouldBe(elsewhere);
        data.ReadText($"dishes/{Id}.json").ShouldBe(elsewhere);
    }

    [Fact]
    public void A_dish_that_is_gone_or_a_file_that_is_not_a_dish_or_unusable_data_is_reported()
    {
        using var data = new StudioData();
        var reviewed = Reviewed(data);

        data.Reviewer.RecordCookTest(DishLocation.Data, "khong-co", Entry(), reviewed.Version).Status.ShouldBe(ReviewStatus.NotFound);
        data.Reviewer.RecordCookTest(DishLocation.Data, "../config/x", Entry(), reviewed.Version).Status.ShouldBe(ReviewStatus.NotFound);

        data.WriteText("dishes/hong.json", "{ not json");
        data.Reviewer.RecordCookTest(DishLocation.Data, "hong", Entry(), Read(data, "hong").Version).Status.ShouldBe(ReviewStatus.NotCookTestable);

        data.Delete("config/spinner-defaults.json");
        data.Reviewer.RecordCookTest(DishLocation.Data, Id, Entry(), reviewed.Version).Status.ShouldBe(ReviewStatus.DataNotUsable);
        data.ReadText($"dishes/{Id}.json").ShouldBe(reviewed.Text);
    }

    // ---- taking it back ------------------------------------------------------------------------------------------------------

    [Fact]
    public void Taking_a_cook_test_back_gives_the_reviewed_file_back_byte_for_byte()
    {
        using var data = new StudioData();
        var reviewed = Reviewed(data);
        var cooked = data.Reviewer.RecordCookTest(DishLocation.Data, Id, Entry(), reviewed.Version).Stored!;

        var result = data.Reviewer.UndoCookTest(DishLocation.Data, Id, cooked.Version);

        result.Status.ShouldBe(ReviewStatus.CookTestUndone);
        result.Stored!.Dish!.Status.ShouldBe(DishStatus.Reviewed);
        data.ReadText($"dishes/{Id}.json").ShouldBe(reviewed.Text);
    }

    [Fact]
    public void Taking_a_cook_test_back_keeps_the_picture_and_the_review()
    {
        using var data = new StudioData();
        data.WriteImage("rau-muong-luoc-200.webp");
        var photo = new ImageRef("rau-muong-luoc-200.webp", null, "tự chụp", "CC0", false);
        var cooked = data.Reviewer.RecordCookTest(DishLocation.Data, Id, Entry(photo: photo), Reviewed(data).Version).Stored!;

        var back = data.Reviewer.UndoCookTest(DishLocation.Data, Id, cooked.Version).Stored!.Dish!;

        back.Image.ShouldBe(photo);
        back.Provenance.ShouldBe(new Provenance(DraftAuthor.Llm, "Mẹ Lan", Today, null, null));
    }

    [Theory]
    [InlineData("draft")]
    [InlineData("validated")]
    [InlineData("reviewed")]
    public void Only_a_cook_tested_dish_has_a_cook_test_to_take_back(string status)
    {
        using var data = new StudioData();
        data.EditJson($"dishes/{Id}.json", d =>
        {
            d["status"] = status;
            if (status == "reviewed")
            {
                d["provenance"]!["reviewedBy"] = "Mẹ Lan";
                d["provenance"]!["reviewedAt"] = "2026-10-19";
            }
        });
        var seen = Read(data);

        var result = data.Reviewer.UndoCookTest(DishLocation.Data, Id, seen.Version);

        result.Status.ShouldBe(ReviewStatus.NotCookTested);
        data.ReadText($"dishes/{Id}.json").ShouldBe(seen.Text);
    }

    [Fact]
    public void Taking_back_refuses_a_version_the_person_did_not_see_and_a_dish_that_is_gone()
    {
        using var data = new StudioData();
        var cooked = data.Reviewer.RecordCookTest(DishLocation.Data, Id, Entry(), Reviewed(data).Version).Stored!;

        data.Reviewer.UndoCookTest(DishLocation.Data, Id, "0000000000000000").Status.ShouldBe(ReviewStatus.Conflict);
        data.Reviewer.UndoCookTest(DishLocation.Data, "khong-co", cooked.Version).Status.ShouldBe(ReviewStatus.NotFound);
        data.ReadText($"dishes/{Id}.json").ShouldBe(cooked.Text);
    }

    [Fact]
    public void The_calls_check_their_arguments()
    {
        using var data = new StudioData();
        var seen = Read(data);

        Should.Throw<ArgumentNullException>(() => data.Reviewer.RecordCookTest(DishLocation.Data, Id, null!, seen.Version));
        Should.Throw<ArgumentNullException>(() => data.Reviewer.RecordCookTest(DishLocation.Data, Id, Entry(), null!));
        Should.Throw<ArgumentNullException>(() => data.Reviewer.UndoCookTest(DishLocation.Data, Id, null!));
    }

    /// <summary>Records compare their lists by reference; two dishes are the same when they are written the same.</summary>
    private sealed class DishComparer : IEqualityComparer<Dish>
    {
        public bool Equals(Dish? x, Dish? y) => x is not null && y is not null && DishFile.Serialize(x) == DishFile.Serialize(y);

        public int GetHashCode(Dish dish) => DishFile.Serialize(dish).GetHashCode(StringComparison.Ordinal);
    }
}
