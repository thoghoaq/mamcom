using MamCom.Core.Models;
using MamCom.DataBuilder.Loading;
using MamCom.Studio.Review;

namespace MamCom.Studio.Tests;

public class CookTestWorkbenchTests : PageTestBase
{
    private string Disk(string id = Id) => Data.ReadText($"dishes/{id}.json");

    /// <summary>Approves a dish the way Studio does, so the file is in the canonical format and can be compared byte for byte.</summary>
    private string MakeReviewed(string id = Id) =>
        Data.Reviewer.Approve(DishLocation.Data, id, "Bà Hoa", Data.Store.Read(DishLocation.Data, id)!.Version).Stored!.Text;

    private static void Fill(AngleSharp.Dom.IElement field, string value) => field.Input(value);

    private static void TypeNumbers(Bunit.IRenderedComponent<MamCom.Studio.Components.DishWorkbench> cut, string minutes, string servings = "", string grams = "")
    {
        Fill(cut.Find("#cook-minutes"), minutes);
        if (servings.Length > 0)
        {
            Fill(cut.Find("#cook-servings"), servings);
        }

        if (grams.Length > 0)
        {
            Fill(cut.Find("#cook-grams"), grams);
        }
    }

    // ---- what is shown --------------------------------------------------------------------------------------------------------

    [Fact]
    public void A_draft_has_no_cook_test_to_record_and_none_to_take_back()
    {
        var cut = Open();

        cut.FindAll("[data-cooktest]").ShouldBeEmpty();
        cut.FindAll("[data-cooktest-done]").ShouldBeEmpty();
    }

    [Fact]
    public void A_reviewed_dish_offers_the_cook_test_with_today_and_nothing_else_filled_in()
    {
        MakeReviewed();

        var cut = Open();

        cut.Find("[data-cooktest] h2").TextContent.ShouldBe("Nấu thử");
        cut.Find("#cook-date").GetAttribute("value").ShouldBe("2026-10-19");
        (cut.Find("#cook-minutes").GetAttribute("value") ?? string.Empty).ShouldBeEmpty();
        (cut.Find("#cook-servings").GetAttribute("value") ?? string.Empty).ShouldBeEmpty();
        (cut.Find("#cook-grams").GetAttribute("value") ?? string.Empty).ShouldBeEmpty();
        Button(cut, "record").HasAttribute("disabled").ShouldBeTrue();
        Text(cut, "[data-cook-hint]").ShouldBe("Nhập số phút nấu thật.");
        cut.FindAll("[data-cooktest-done]").ShouldBeEmpty();
        cut.FindAll("[data-cook-problems]").ShouldBeEmpty();
        cut.Find("[data-reopen]").ShouldNotBeNull();
    }

    [Fact]
    public void The_number_fields_say_what_they_take_and_are_big_enough_to_touch()
    {
        MakeReviewed();

        var cut = Open();

        foreach (var (id, min, max) in new[] { ("cook-minutes", "1", "1440"), ("cook-servings", "1", "100"), ("cook-grams", "1", "50000") })
        {
            var input = cut.Find($"#{id}");
            input.GetAttribute("type").ShouldBe("number");
            input.GetAttribute("min").ShouldBe(min);
            input.GetAttribute("max").ShouldBe(max);
            input.GetAttribute("step").ShouldBe("1");
            input.ClassList.ShouldContain("input");
            cut.Find($"label[for={id}]").TextContent.ShouldNotBeNullOrWhiteSpace();
        }

        cut.Find("#cook-date").GetAttribute("type").ShouldBe("date");
        Button(cut, "record").ClassList.ShouldContain("button");   // the class that gives it a 48px touch target
    }

    [Fact]
    public void Typing_the_minutes_turns_the_button_on_and_the_hint_off_and_clearing_them_turns_it_back()
    {
        MakeReviewed();
        var cut = Open();

        Fill(cut.Find("#cook-minutes"), "45");

        Button(cut, "record").HasAttribute("disabled").ShouldBeFalse();
        cut.FindAll("[data-cook-hint]").ShouldBeEmpty();

        Fill(cut.Find("#cook-minutes"), string.Empty);

        Button(cut, "record").HasAttribute("disabled").ShouldBeTrue();
        cut.Find("[data-cook-hint]").ShouldNotBeNull();
    }

    [Fact]
    public void Without_any_picture_file_the_cook_test_says_so_like_the_ingredients_do()
    {
        MakeReviewed();

        Open().Find("[data-cooktest] [data-no-images]").TextContent.ShouldContain("data/images/out");
    }

    [Fact]
    public void The_fields_of_the_cook_test_do_not_share_an_id_with_anything_else_on_the_page()
    {
        MakeReviewed();

        var ids = Open().FindAll("[id]").Select(e => e.Id!).ToArray();

        ids.Distinct().Count().ShouldBe(ids.Length);
    }

    // ---- recording ------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Recording_a_cook_test_writes_it_and_shows_it_with_the_way_to_take_it_back()
    {
        var reviewed = MakeReviewed();
        var cut = Open();
        TypeNumbers(cut, "47", "4", "1450");

        Button(cut, "record").Click();

        Text(cut, "[data-message]").ShouldBe("Đã ghi nhận nấu thử. Món ở trạng thái \"đã nấu thử\".");
        cut.Find("[data-message]").ClassList.ShouldContain("notice--ok");
        cut.Find(".dish-header__meta .badge").TextContent.ShouldBe("Đã nấu thử");
        Text(cut, "[data-cook-done]").ShouldBe("Đã nấu thử ngày 19/10/2026, hết 47 phút.");
        Text(cut, "[data-cook-yield]").ShouldBe("Nấu 4 suất, thành phẩm 1450 g.");
        Text(cut, "[data-undo-cook]").ShouldBe("Bỏ kết quả nấu thử");
        cut.FindAll("[data-cooktest]").ShouldBeEmpty();
        cut.FindAll("[data-reopen]").ShouldBeEmpty();
        cut.Find("[data-locked]").TextContent.ShouldBe("Món đã nấu thử nên chỉ xem được.");
        Text(cut, "[data-provenance]").ShouldContain("Nấu thử ngày 19/10/2026.");

        var dish = DishFile.Parse(Disk());
        dish.Status.ShouldBe(DishStatus.CookTested);
        dish.Provenance.ShouldBe(new Provenance(DraftAuthor.Llm, "Bà Hoa", new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 19), 47, 4, 1450));
        Disk().ShouldNotBe(reviewed);
    }

    [Fact]
    public void An_earlier_day_can_be_chosen_and_without_servings_and_weight_no_yield_is_shown()
    {
        MakeReviewed();
        var cut = Open();
        cut.Find("#cook-date").Change("2026-10-12");
        TypeNumbers(cut, "30");

        Button(cut, "record").Click();

        Text(cut, "[data-cook-done]").ShouldBe("Đã nấu thử ngày 12/10/2026, hết 30 phút.");
        cut.FindAll("[data-cook-yield]").ShouldBeEmpty();
        var provenance = DishFile.Parse(Disk()).Provenance;
        provenance.CookTestedAt.ShouldBe(new DateOnly(2026, 10, 12));
        provenance.MeasuredServings.ShouldBeNull();
        provenance.MeasuredFinishedGrams.ShouldBeNull();
        Disk().ShouldNotContain("measuredServings");
    }

    [Fact]
    public void The_photo_is_chosen_among_the_picture_files_and_goes_into_the_dish()
    {
        Data.WriteImage("rau-muong-luoc-200.webp");
        Data.WriteImage("rau-muong-luoc-800.webp");
        MakeReviewed();
        var cut = Open();
        TypeNumbers(cut, "47");

        cut.FindAll("[data-cooktest] [data-no-images]").ShouldBeEmpty();
        cut.Find("#cook-image-thumb").QuerySelectorAll("option").Select(o => o.GetAttribute("value")).ShouldBe(["", "rau-muong-luoc-200.webp", "rau-muong-luoc-800.webp"]);
        cut.Find("#cook-image-thumb").Change("rau-muong-luoc-200.webp");
        cut.Find("#cook-image-full").Change("rau-muong-luoc-800.webp");
        cut.Find("#cook-image-source").Change("tự chụp");
        cut.Find("#cook-image-license").Change("CC0");

        cut.Find("[data-cooktest] [data-image-preview]").GetAttribute("src").ShouldBe("/data-images/rau-muong-luoc-800.webp");
        cut.Find("[data-cooktest] [data-image-preview]").GetAttribute("alt").ShouldBe("Ảnh của Rau muống luộc");
        Button(cut, "record").Click();

        DishFile.Parse(Disk()).Image.ShouldBe(new ImageRef("rau-muong-luoc-200.webp", "rau-muong-luoc-800.webp", "tự chụp", "CC0", false));
    }

    [Fact]
    public void A_cook_test_with_no_photo_leaves_the_picture_of_the_dish_as_it_was()
    {
        MakeReviewed();
        var before = DishFile.Parse(Disk()).Image;
        var cut = Open();
        TypeNumbers(cut, "47");

        Button(cut, "record").Click();

        DishFile.Parse(Disk()).Image.ShouldBe(before);
    }

    // ---- taking it back -------------------------------------------------------------------------------------------------------

    [Fact]
    public void Taking_the_cook_test_back_gives_the_reviewed_dish_and_a_fresh_form()
    {
        var reviewed = MakeReviewed();
        var cut = Open();
        TypeNumbers(cut, "47", "4", "1450");
        Button(cut, "record").Click();

        Button(cut, "undo-cook").Click();

        Text(cut, "[data-message]").ShouldBe("Đã bỏ kết quả nấu thử; món về \"đã duyệt\".");
        cut.Find("[data-message]").ClassList.ShouldContain("notice--ok");
        cut.Find(".dish-header__meta .badge").TextContent.ShouldBe("Đã duyệt");
        cut.FindAll("[data-cooktest-done]").ShouldBeEmpty();
        cut.Find("[data-cooktest]").ShouldNotBeNull();
        (cut.Find("#cook-minutes").GetAttribute("value") ?? string.Empty).ShouldBeEmpty();   // not what was typed before
        cut.Find("#cook-date").GetAttribute("value").ShouldBe("2026-10-19");
        cut.Find("[data-reopen]").ShouldNotBeNull();
        Disk().ShouldBe(reviewed);
    }

    [Fact]
    public void A_cook_test_that_was_taken_back_elsewhere_is_reported_and_the_page_shows_what_is_on_disk()
    {
        var reviewed = MakeReviewed();
        Data.Reviewer.RecordCookTest(DishLocation.Data, Id, new CookTestEntry(StudioData.Today, 47, null, null, null), Data.Store.Read(DishLocation.Data, Id)!.Version);
        var cut = Open();
        Data.WriteText($"dishes/{Id}.json", reviewed);   // another Studio window took the test back

        Button(cut, "undo-cook").Click();

        Text(cut, "[data-message]").ShouldStartWith("File món đã bị đổi ở chỗ khác");
        Disk().ShouldBe(reviewed);
        cut.Find("[data-cooktest]").ShouldNotBeNull();
        cut.FindAll("[data-cooktest-done]").ShouldBeEmpty();
    }

    // ---- what is refused ------------------------------------------------------------------------------------------------------

    [Fact]
    public void A_day_that_has_not_come_yet_is_refused_and_the_file_is_left_alone()
    {
        var reviewed = MakeReviewed();
        var cut = Open();
        cut.Find("#cook-date").Change("2026-10-20");
        TypeNumbers(cut, "47");

        Button(cut, "record").Click();

        Text(cut, "[data-cook-note]").ShouldBe("Ngày nấu không thể là ngày mai trở đi.");   // under the button, where the person is looking
        cut.Find("[data-cook-note]").ClassList.ShouldContain("notice--danger");
        cut.FindAll("[data-message]").ShouldBeEmpty();
        Disk().ShouldBe(reviewed);
        cut.Find("#cook-minutes").GetAttribute("value").ShouldBe("47");   // what was typed is still there to be fixed
        cut.Find("#cook-date").GetAttribute("value").ShouldBe("2026-10-20");
    }

    [Fact]
    public void Numbers_the_schema_refuses_are_listed_under_the_button_and_the_typed_values_stay()
    {
        var reviewed = MakeReviewed();
        var cut = Open();
        TypeNumbers(cut, "47", "4");   // servings without their weight

        Button(cut, "record").Click();

        Text(cut, "[data-cook-note]").ShouldBe("Chưa ghi nhận được. Sửa các lỗi ngay dưới nút rồi bấm lại.");   // not the dish-wide "see the checker"
        cut.Find("[data-cook-note]").ClassList.ShouldContain("notice--danger");
        cut.FindAll("[data-message]").ShouldBeEmpty();
        cut.FindAll("[data-cook-problems] li").Count.ShouldBeGreaterThan(0);
        cut.Find("[data-cook-problems] li .badge").TextContent.ShouldBe("Lỗi");
        cut.Find("[data-cook-problems] li .finding__text").TextContent.ShouldNotBeNullOrWhiteSpace();
        cut.Find("[data-cook-problems] li .finding__where code").TextContent.ShouldContain("provenance");   // which field it is about
        Disk().ShouldBe(reviewed);
        cut.Find("#cook-servings").GetAttribute("value").ShouldBe("4");
        cut.Find("[data-cooktest]").ShouldNotBeNull();

        Fill(cut.Find("#cook-grams"), "1450");
        Button(cut, "record").Click();

        cut.FindAll("[data-cook-problems]").ShouldBeEmpty();
        cut.FindAll("[data-cook-note]").ShouldBeEmpty();
        Text(cut, "[data-cook-yield]").ShouldBe("Nấu 4 suất, thành phẩm 1450 g.");
    }

    [Fact]
    public void An_old_refusal_does_not_stay_when_the_next_try_is_refused_for_another_reason()
    {
        var reviewed = MakeReviewed();
        var cut = Open();
        TypeNumbers(cut, "47", "4");   // servings without their weight
        Button(cut, "record").Click();
        cut.FindAll("[data-cook-problems] li").Count.ShouldBeGreaterThan(0);

        Fill(cut.Find("#cook-servings"), string.Empty);
        cut.Find("#cook-date").Change("2026-10-20");
        Button(cut, "record").Click();

        Text(cut, "[data-cook-note]").ShouldBe("Ngày nấu không thể là ngày mai trở đi.");
        cut.FindAll("[data-cook-problems]").ShouldBeEmpty();
        Disk().ShouldBe(reviewed);
    }

    [Fact]
    public void A_photo_without_a_source_and_a_license_is_an_error_until_they_are_given()
    {
        Data.WriteImage("rau-muong-luoc-200.webp");
        var reviewed = MakeReviewed();
        var cut = Open();
        TypeNumbers(cut, "47");
        cut.Find("#cook-image-thumb").Change("rau-muong-luoc-200.webp");

        Button(cut, "record").Click();

        cut.FindAll("[data-cook-problems] li").Count.ShouldBeGreaterThan(0);
        Disk().ShouldBe(reviewed);

        cut.Find("#cook-image-source").Change("tự chụp");
        cut.Find("#cook-image-license").Change("CC0");
        Button(cut, "record").Click();

        cut.FindAll("[data-cook-problems]").ShouldBeEmpty();
        DishFile.Parse(Disk()).Status.ShouldBe(DishStatus.CookTested);
    }

    [Fact]
    public void A_dish_changed_elsewhere_is_not_overwritten_and_is_shown_as_it_is_now()
    {
        var reviewed = MakeReviewed();
        var cut = Open();
        TypeNumbers(cut, "47");
        Data.WriteText($"dishes/{Id}.json", reviewed.Replace("nhặt, rửa sạch", "nhặt kỹ, rửa sạch"));
        var elsewhere = Disk();

        Button(cut, "record").Click();

        Text(cut, "[data-message]").ShouldStartWith("File món đã bị đổi ở chỗ khác");
        Disk().ShouldBe(elsewhere);
        DishFile.Parse(Disk()).Status.ShouldBe(DishStatus.Reviewed);
        cut.Find("[data-cooktest]").ShouldNotBeNull();
    }

    [Fact]
    public void A_dish_that_was_cook_tested_elsewhere_is_shown_with_its_test_instead_of_a_second_one()
    {
        MakeReviewed();
        var cut = Open();
        TypeNumbers(cut, "47");
        Data.Reviewer.RecordCookTest(DishLocation.Data, Id, new CookTestEntry(StudioData.Today, 60, null, null, null), Data.Store.Read(DishLocation.Data, Id)!.Version);
        var elsewhere = Disk();

        Button(cut, "record").Click();

        Disk().ShouldBe(elsewhere);
        cut.FindAll("[data-cooktest]").ShouldBeEmpty();
        Text(cut, "[data-cook-done]").ShouldBe("Đã nấu thử ngày 19/10/2026, hết 60 phút.");
    }

    // ---- moving between dishes ------------------------------------------------------------------------------------------------

    [Fact]
    public void What_was_typed_for_one_dish_does_not_follow_the_reader_to_the_next()
    {
        MakeReviewed();
        MakeReviewed("canh-bau-tom");
        var cut = Open();
        TypeNumbers(cut, "47");

        cut.Render(p => p.Add(x => x.Id, "canh-bau-tom").Add(x => x.Location, DishLocation.Data));

        cut.Find("h1").TextContent.ShouldBe("Canh bầu nấu tôm");
        (cut.Find("#cook-minutes").GetAttribute("value") ?? string.Empty).ShouldBeEmpty();
        Button(cut, "record").HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public void A_refusal_for_one_dish_does_not_follow_the_reader_to_the_next()
    {
        MakeReviewed();
        MakeReviewed("canh-bau-tom");
        var cut = Open();
        cut.Find("#cook-date").Change("2026-10-20");
        TypeNumbers(cut, "47", "4");   // a day to come, and servings without their weight
        Button(cut, "record").Click();
        cut.Find("[data-cook-note]").ShouldNotBeNull();

        cut.Render(p => p.Add(x => x.Id, "canh-bau-tom").Add(x => x.Location, DishLocation.Data));

        cut.FindAll("[data-cook-note]").ShouldBeEmpty();
        cut.FindAll("[data-cook-problems]").ShouldBeEmpty();
    }

    [Fact]
    public void Errors_of_the_numbers_do_not_follow_the_reader_to_the_next_dish_either()
    {
        MakeReviewed();
        MakeReviewed("canh-bau-tom");
        var cut = Open();
        TypeNumbers(cut, "47", "4");   // servings without their weight
        Button(cut, "record").Click();
        cut.Find("[data-cook-problems]").ShouldNotBeNull();

        cut.Render(p => p.Add(x => x.Id, "canh-bau-tom").Add(x => x.Location, DishLocation.Data));

        cut.FindAll("[data-cook-note]").ShouldBeEmpty();
        cut.FindAll("[data-cook-problems]").ShouldBeEmpty();
    }

    [Fact]
    public void Only_errors_are_listed_under_the_button_and_not_the_warnings_of_the_dish()
    {
        // A line that no step uses is a warning of the checker, and does not stop a dish from being reviewed.
        Data.EditJson($"dishes/{Id}.json", d => d["ingredients"]!.AsArray().Add(new System.Text.Json.Nodes.JsonObject
        {
            ["ingredientId"] = "dau-an",
            ["gramsEdiblePerServing"] = 5,
            ["scale"] = "linear",
            ["consumedRatio"] = 1,
            ["note"] = null,
        }));
        MakeReviewed();
        var cut = Open();
        cut.Find("[data-warnings]").ShouldNotBeNull();   // the warning is there to be left out
        TypeNumbers(cut, "47");
        // A picture that is not in the folder is an error of the checker (not of the schema, which stops the other checks).
        cut.Find("#cook-image-thumb").Change("khong-co.webp");
        cut.Find("#cook-image-source").Change("tự chụp");
        cut.Find("#cook-image-license").Change("CC0");

        Button(cut, "record").Click();

        var badges = cut.FindAll("[data-cook-problems] li .badge").Select(b => b.TextContent).ToArray();
        badges.ShouldNotBeEmpty();
        badges.ShouldAllBe(b => b == "Lỗi");
    }

    [Fact]
    public void Rendering_again_with_the_same_parameters_keeps_what_the_reader_has_typed()
    {
        MakeReviewed();
        var cut = Open();
        TypeNumbers(cut, "47", "4", "1450");

        cut.Render(p => p.Add(x => x.Id, Id).Add(x => x.Location, DishLocation.Data));

        cut.Find("#cook-minutes").GetAttribute("value").ShouldBe("47");
        cut.Find("#cook-servings").GetAttribute("value").ShouldBe("4");
        cut.Find("#cook-grams").GetAttribute("value").ShouldBe("1450");
    }

    [Fact]
    public void The_day_is_the_one_of_the_clock_not_a_fixed_one()
    {
        MakeReviewed();
        Data.Clock.Today = new DateOnly(2027, 3, 5);

        Open().Find("#cook-date").GetAttribute("value").ShouldBe("2027-03-05");
    }
}
