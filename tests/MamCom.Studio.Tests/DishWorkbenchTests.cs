using System.Text.Json.Nodes;
using MamCom.Core.Models;
using MamCom.DataBuilder.Loading;
using MamCom.Studio.Review;

namespace MamCom.Studio.Tests;

public class DishWorkbenchTests : PageTestBase
{
    private string Disk(string id = Id) => Data.ReadText($"dishes/{id}.json");

    // ---- what is shown --------------------------------------------------------------------------------------------------------

    [Fact]
    public void A_dish_that_is_not_there_says_so_and_a_way_back_is_given()
    {
        var cut = Open("khong-co");

        cut.Find("[data-not-found]").TextContent.ShouldBe("Không tìm thấy món này.");
        cut.Find("[data-back]").GetAttribute("href").ShouldBe("/dishes");
        cut.FindAll("[data-save]").ShouldBeEmpty();
    }

    [Fact]
    public void An_id_that_could_leave_the_folder_is_just_not_found()
    {
        Open("../config/portion-defaults").Find("[data-not-found]").ShouldNotBeNull();
    }

    [Fact]
    public void A_file_that_is_not_a_dish_shows_why_and_cannot_be_edited()
    {
        Data.WriteText("dishes/hong.json", "{ not json");

        var cut = Open("hong");

        cut.Find("[data-broken]").ShouldNotBeNull();
        cut.Find("pre.problem").TextContent.ShouldNotBeNullOrWhiteSpace();
        cut.FindAll("[data-save]").ShouldBeEmpty();
        cut.FindAll("[data-approve]").ShouldBeEmpty();
    }

    [Fact]
    public void Shows_the_name_the_status_and_the_file()
    {
        var cut = Open();

        cut.Find("h1").TextContent.ShouldBe("Rau muống luộc");
        cut.Find("[data-back]").ClassList.ShouldContain("back-link");   // the class that gives it a 48px touch target
        cut.Find(".dish-header__meta .badge").TextContent.ShouldBe("Nháp");
        cut.Find(".dish-header__meta").TextContent.ShouldContain("File: dishes/rau-muong-luoc.json");
        cut.FindAll("[data-draft]").ShouldBeEmpty();
    }

    [Fact]
    public void The_fields_hold_the_values_of_the_file()
    {
        var cut = Open();

        cut.Find("#dish-name").GetAttribute("value").ShouldBe("Rau muống luộc");
        cut.Find("#dish-slot").GetAttribute("value").ShouldBe("Vegetable");
        cut.Find("#dish-method").GetAttribute("value").ShouldBe("Boil");
        cut.Find("#dish-spicy").GetAttribute("value").ShouldBe("None");
        cut.Find("#dish-protein").GetAttribute("value").ShouldBe("none");
        cut.Find("[data-serve-hot]").HasAttribute("checked").ShouldBeTrue();
        cut.FindAll("[data-region]").Select(c => (c.GetAttribute("data-region"), c.HasAttribute("checked")))
            .ShouldBe([("north", true), ("central", true), ("south", true)]);
        cut.FindAll("[data-season]").Where(c => c.HasAttribute("checked")).Select(c => c.GetAttribute("data-season")).ShouldBe(["summer", "autumn"]);
        cut.FindAll("[data-line]").Count.ShouldBe(2);
        cut.FindAll("[data-step]").Count.ShouldBe(2);
        cut.Find("[data-line='0'] [data-line-grams]").GetAttribute("value").ShouldBe("100");
        cut.Find("[data-step='1'] [data-step-active]").GetAttribute("value").ShouldBe("3");
    }

    [Fact]
    public void The_choices_are_in_words_and_the_ingredients_by_name()
    {
        var cut = Open();

        cut.Find("#dish-slot").QuerySelectorAll("option").Select(o => o.TextContent).ShouldBe(["Canh", "Món mặn", "Rau"]);
        cut.Find("#dish-spicy").QuerySelectorAll("option").Select(o => o.TextContent).ShouldBe(["Không cay", "Cay tuỳ chọn", "Cay"]);
        cut.Find("[data-line='0'] [data-line-ingredient]").QuerySelectorAll("option").Select(o => o.TextContent).ShouldContain("Rau muống");
        cut.Find("[data-line='0'] [data-line-ingredient]").GetAttribute("value").ShouldBe("rau-muong");
        cut.FindAll("[data-season]").Select(c => c.GetAttribute("data-season")).Last().ShouldBe("all");
    }

    [Fact]
    public void A_line_whose_ingredient_is_not_in_the_kitchen_is_still_an_option_of_its_own_select()
    {
        Data.EditJson($"dishes/{Id}.json", d => d["ingredients"]![0]!["ingredientId"] = "khong-co");

        var options = Open().Find("[data-line='0'] [data-line-ingredient]").QuerySelectorAll("option").Select(o => o.GetAttribute("value")).ToArray();

        options.ShouldContain("khong-co");
        options.Count(o => o == "khong-co").ShouldBe(1);
    }

    [Fact]
    public void The_ingredients_to_choose_from_are_sorted_by_name()
    {
        var labels = Open().Find("[data-line='0'] [data-line-ingredient]").QuerySelectorAll("option").Select(o => o.TextContent).ToArray();

        labels.Length.ShouldBeGreaterThan(10);
        labels.ShouldBe([.. labels.Order(StringComparer.CurrentCultureIgnoreCase)]);
    }

    [Fact]
    public void A_protein_source_the_kitchen_does_not_know_stays_visible_as_an_option()
    {
        Data.EditJson($"dishes/{Id}.json", d => d["proteinSource"] = "nhim");

        var options = Open().Find("#dish-protein").QuerySelectorAll("option").Select(o => o.GetAttribute("value")).ToArray();

        options.ShouldContain("nhim");
        options.ShouldContain("none");
    }

    [Fact]
    public void A_season_the_config_does_not_know_stays_visible_and_is_an_error()
    {
        Data.EditJson($"dishes/{Id}.json", d => d["seasons"] = new JsonArray("monsoon"));

        var cut = Open();

        cut.Find("[data-season=monsoon]").HasAttribute("checked").ShouldBeTrue();
        cut.Find("[data-errors]").ShouldNotBeNull();
    }

    [Fact]
    public void A_step_can_use_an_ingredient_the_recipe_has_no_line_for_and_it_is_shown_ticked()
    {
        Data.EditJson($"dishes/{Id}.json", d => d["steps"]![0]!["ingredientIds"] = new JsonArray("rau-muong", "hanh-la"));

        var uses = Open().Find("[data-step='0']").QuerySelectorAll("[data-step-ingredient]").ToArray();

        uses.Select(c => c.GetAttribute("data-step-ingredient")).ShouldBe(["rau-muong", "muoi", "hanh-la"]);
        uses.Single(c => c.GetAttribute("data-step-ingredient") == "hanh-la").HasAttribute("checked").ShouldBeTrue();
    }

    [Fact]
    public void The_steps_say_which_ingredients_they_use()
    {
        var cut = Open();

        cut.Find("[data-step='1']").QuerySelectorAll("[data-step-ingredient]").Where(c => c.HasAttribute("checked"))
            .Select(c => c.GetAttribute("data-step-ingredient")).ShouldBe(["rau-muong", "muoi"]);
        cut.Find("[data-step='0']").QuerySelectorAll("[data-step-ingredient]").Where(c => c.HasAttribute("checked"))
            .Select(c => c.GetAttribute("data-step-ingredient")).ShouldBe(["rau-muong"]);
    }

    [Fact]
    public void A_line_whose_ingredient_the_kitchen_does_not_have_stays_visible_and_is_an_error()
    {
        Data.EditJson($"dishes/{Id}.json", d => d["ingredients"]![0]!["ingredientId"] = "khong-co");

        var cut = Open();

        cut.Find("[data-line='0'] [data-line-ingredient]").GetAttribute("value").ShouldBe("khong-co");
        cut.Find("[data-errors]").ShouldNotBeNull();
    }

    [Fact]
    public void Nutrition_is_unknown_while_the_ingredients_have_no_data_and_the_ingredients_are_named()
    {
        var cut = Open();

        Text(cut, "[data-nutrition] .nutrition__kcal").ShouldBe("chưa đủ số liệu");
        Text(cut, "[data-nutrition] .nutrition__missing").ShouldBe("Chưa có số liệu dinh dưỡng của: Rau muống, Muối.");
    }

    [Fact]
    public void Nutrition_is_added_up_from_the_ingredients_and_shown_as_an_estimate()
    {
        Data.SetNutrient(Id, "kcal", 40);   // test-only number: 101 g edible per serving at 40 kcal per 100 g = 40.4 kcal

        var cut = Open();

        Text(cut, "[data-nutrition] .nutrition__kcal").ShouldBe("≈ 40 kcal");
        cut.FindAll("[data-nutrition] .nutrition__missing").Count.ShouldBe(1);   // the other nutrients are still unknown, so the ingredients are still listed
    }

    [Fact]
    public void Nutrition_that_cannot_be_added_up_says_why()
    {
        Data.EditJson($"dishes/{Id}.json", d => d["ingredients"]![0]!["ingredientId"] = "khong-co");

        var cut = Open();

        Text(cut, "[data-nutrition] .notice--warning").ShouldContain("khong-co");
    }

    [Fact]
    public void Shows_the_findings_of_the_checks()
    {
        Data.EditJson($"dishes/{Id}.json", d => d["steps"]![1]!["ingredientIds"] = new JsonArray("rau-muong"));

        var cut = Open();

        cut.Find("[data-findings] .finding").GetAttribute("data-severity").ShouldBe("warning");
        cut.Find("[data-warnings]").TextContent.ShouldBe("1 cảnh báo");
        cut.FindAll("[data-errors]").ShouldBeEmpty();
    }

    [Fact]
    public void Says_when_the_checks_found_nothing()
    {
        Text(Open(), "[data-findings] .notice--ok").ShouldBe("Máy kiểm không thấy gì để báo.");
    }

    [Fact]
    public void Shows_who_drafted_it_and_that_nobody_has_reviewed_it()
    {
        var cut = Open();

        Text(cut, "[data-provenance]").ShouldContain("Soạn bởi: Máy soạn (Claude)");
        cut.Find("[data-not-reviewed]").TextContent.ShouldBe("Chưa ai duyệt.");
    }

    [Fact]
    public void A_draft_shows_what_the_drafter_said_and_where_it_lives()
    {
        Data.MakeDraft(Id);
        Data.WriteReport(Id, "warnings");

        var cut = Open(Id, DishLocation.Draft);

        cut.Find("[data-draft]").TextContent.ShouldBe("Bản nháp");
        cut.Find(".dish-header__meta").TextContent.ShouldContain("File: dishes/_drafts/rau-muong-luoc.json");
        var note = cut.Find("[data-draft-note]").TextContent;
        note.ShouldContain("Có cảnh báo");
        note.ShouldContain("claude-opus-5-5");
        note.ShouldContain("19/10/2026");
        note.ShouldContain("Nước dừa tươi, khoảng 80 g mỗi suất");
    }

    [Fact]
    public void A_dish_that_is_not_a_draft_has_no_drafter_note()
    {
        Data.WriteReport(Id, "valid");   // a stray report next to the dish must not show for a dish in dishes/

        Open().FindAll("[data-draft-note]").ShouldBeEmpty();
    }

    [Fact]
    public void Explains_what_is_wrong_when_the_data_cannot_be_read_and_turns_the_buttons_off()
    {
        Data.Delete("config/spinner-defaults.json");

        var cut = Open();

        cut.Find("[data-kitchen-problems]").TextContent.ShouldContain("config/spinner-defaults.json");
        cut.Find("#dish-name").Change("Tên khác");   // there is something to save, but the data cannot be checked
        Button(cut, "save").HasAttribute("disabled").ShouldBeTrue();
        Button(cut, "approve").HasAttribute("disabled").ShouldBeTrue();
        Text(cut, "[data-approve-hint]").ShouldBe("Chưa đọc được dữ liệu nên chưa lưu hay duyệt được.");
    }

    // ---- editing --------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Nothing_to_save_before_anything_is_changed()
    {
        var cut = Open();

        Button(cut, "save").HasAttribute("disabled").ShouldBeTrue();
        Button(cut, "discard").HasAttribute("disabled").ShouldBeTrue();
        cut.FindAll("[data-unsaved]").ShouldBeEmpty();
    }

    [Fact]
    public void A_change_says_it_is_not_saved_and_turns_on_save_and_discard()
    {
        var cut = Open();

        cut.Find("#dish-name").Change("Rau muống luộc nước chanh");

        cut.Find("[data-unsaved]").TextContent.ShouldBe("Có thay đổi chưa lưu.");
        Button(cut, "save").HasAttribute("disabled").ShouldBeFalse();
        Button(cut, "discard").HasAttribute("disabled").ShouldBeFalse();
        Disk().ShouldContain("\"name\": \"Rau muống luộc\"");
    }

    [Fact]
    public void Putting_a_value_back_is_no_change()
    {
        var cut = Open();

        cut.Find("#dish-name").Change("Khác");
        cut.Find("#dish-name").Change("Rau muống luộc");

        cut.FindAll("[data-unsaved]").ShouldBeEmpty();
        Button(cut, "save").HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public void Saving_writes_the_file_and_the_dish_becomes_validated()
    {
        var cut = Open();
        cut.Find("#dish-name").Change("Rau muống luộc nước chanh");

        Button(cut, "save").Click();

        Text(cut, "[data-message]").ShouldBe("Đã lưu. Máy kiểm không thấy lỗi nên món ở trạng thái \"đã qua máy kiểm\".");
        cut.Find("[data-message]").ClassList.ShouldContain("notice--ok");
        cut.Find(".dish-header__meta .badge").TextContent.ShouldBe("Đã qua máy kiểm");
        cut.Find("h1").TextContent.ShouldBe("Rau muống luộc nước chanh");
        cut.FindAll("[data-unsaved]").ShouldBeEmpty();
        var saved = DishFile.Parse(Disk());
        saved.Name.ShouldBe("Rau muống luộc nước chanh");
        saved.Status.ShouldBe(DishStatus.Validated);
    }

    [Fact]
    public void Every_kind_of_field_reaches_the_file()
    {
        var cut = Open();

        cut.Find("#dish-slot").Change("Main");
        cut.Find("#dish-method").Change("Steam");
        cut.Find("#dish-spicy").Change("Optional");
        cut.Find("#dish-protein").Change("heo");
        cut.Find("[data-serve-hot]").Change(false);
        cut.Find("[data-region=central]").Change(false);
        cut.Find("[data-season=winter]").Change(true);
        cut.Find("[data-line='0'] [data-line-grams]").Change("120");
        cut.Find("#line-0-scale").Change("Fixed");
        cut.Find("#line-0-ratio").Change("0.5");
        cut.Find("#line-0-note").Change("rửa kỹ");
        cut.Find("[data-step='0'] [data-step-text]").Change("Nhặt rau thật sạch.");
        cut.Find("[data-step='0'] [data-step-active]").Change("7");
        cut.Find("[data-step='0'] [data-step-passive]").Change("2");
        cut.Find("[data-step='0'] [data-step-ingredient=muoi]").Change(true);
        Button(cut, "save").Click();

        var dish = DishFile.Parse(Disk());
        dish.Slot.ShouldBe(DishSlot.Main);
        dish.CookingMethod.ShouldBe(CookingMethod.Steam);
        dish.Spicy.ShouldBe(SpicyLevel.Optional);
        dish.ProteinSource.ShouldBe("heo");
        dish.ServeHot.ShouldBeFalse();
        dish.Regions.ShouldBe([Region.North, Region.South]);
        dish.Seasons.ShouldBe(["winter", "summer", "autumn"]);
        dish.Ingredients[0].ShouldBe(new DishIngredient("rau-muong", 120, ScaleKind.Fixed, 0.5, "rửa kỹ"));
        dish.Steps[0].Text.ShouldBe("Nhặt rau thật sạch.");
        dish.Steps[0].ActiveMinutes.ShouldBe(7);
        dish.Steps[0].PassiveMinutes.ShouldBe(2);
        dish.Steps[0].IngredientIds.ShouldBe(["rau-muong", "muoi"]);
    }

    [Fact]
    public void A_line_is_added_from_the_kitchens_ingredients_and_removed_again()
    {
        var cut = Open();
        Button(cut, "line-add").HasAttribute("disabled").ShouldBeTrue();

        cut.Find("[data-line-new]").Change("hanh-la");
        Button(cut, "line-add").HasAttribute("disabled").ShouldBeFalse();
        Button(cut, "line-add").Click();

        cut.FindAll("[data-line]").Count.ShouldBe(3);
        cut.Find("[data-line='2'] [data-line-ingredient]").GetAttribute("value").ShouldBe("hanh-la");
        cut.Find("[data-line-new]").GetAttribute("value").ShouldBe(string.Empty);
        Button(cut, "line-add").HasAttribute("disabled").ShouldBeTrue();
        cut.Find("[data-unsaved]").ShouldNotBeNull();

        cut.Find("[data-line='2'] [data-line-remove]").Click();

        cut.FindAll("[data-line]").Count.ShouldBe(2);
        cut.FindAll("[data-unsaved]").ShouldBeEmpty();
    }

    [Fact]
    public void A_step_is_added_moved_and_removed()
    {
        var cut = Open();

        Button(cut, "step-add").Click();
        cut.FindAll("[data-step]").Count.ShouldBe(3);
        cut.Find("[data-step='2'] [data-step-text]").Change("Bày ra đĩa.");
        cut.Find("[data-step='2'] [data-step-up]").Click();
        cut.Find("[data-step='1'] [data-step-text]").GetAttribute("value").ShouldBe("Bày ra đĩa.");

        cut.Find("[data-step='1'] [data-step-down]").Click();
        cut.Find("[data-step='2'] [data-step-text]").GetAttribute("value").ShouldBe("Bày ra đĩa.");

        cut.Find("[data-step='2'] [data-step-remove]").Click();
        cut.FindAll("[data-step]").Count.ShouldBe(2);
    }

    [Fact]
    public void The_first_step_cannot_go_up_and_the_last_cannot_go_down()
    {
        var cut = Open();

        cut.Find("[data-step='0'] [data-step-up]").HasAttribute("disabled").ShouldBeTrue();
        cut.Find("[data-step='1'] [data-step-down]").HasAttribute("disabled").ShouldBeTrue();
        cut.Find("[data-step='0'] [data-step-down]").HasAttribute("disabled").ShouldBeFalse();
        cut.Find("[data-step='1'] [data-step-up]").HasAttribute("disabled").ShouldBeFalse();
    }

    [Fact]
    public void A_dish_with_an_error_shows_it_at_once_and_cannot_be_saved_or_approved()
    {
        Identity.Name = "Mẹ Lan";
        var cut = Open();

        cut.Find("[data-line='0'] [data-line-grams]").Change("-5");

        cut.Find("[data-errors]").TextContent.ShouldBe("1 lỗi");
        cut.Find("[data-findings] .finding").GetAttribute("data-severity").ShouldBe("error");
        Text(cut, "[data-findings] .notice--danger").ShouldBe("Món còn lỗi nên chưa lưu hay duyệt được.");
        Button(cut, "save").HasAttribute("disabled").ShouldBeTrue();
        Button(cut, "approve").HasAttribute("disabled").ShouldBeTrue();
        Text(cut, "[data-approve-hint]").ShouldBe("Sửa hết lỗi trước khi duyệt.");

        cut.Find("[data-line='0'] [data-line-grams]").Change("100");

        cut.FindAll("[data-errors]").ShouldBeEmpty();
        Button(cut, "approve").HasAttribute("disabled").ShouldBeFalse();   // back to the file's own value: nothing unsaved, no error, and a name is there
    }

    [Fact]
    public void Discarding_puts_the_form_back_to_the_file()
    {
        var cut = Open();
        cut.Find("#dish-name").Change("Tên khác");
        cut.Find("[data-line='0'] [data-line-grams]").Change("-5");

        Button(cut, "discard").Click();

        cut.Find("#dish-name").GetAttribute("value").ShouldBe("Rau muống luộc");
        cut.Find("[data-line='0'] [data-line-grams]").GetAttribute("value").ShouldBe("100");
        cut.FindAll("[data-unsaved]").ShouldBeEmpty();
        cut.FindAll("[data-errors]").ShouldBeEmpty();
    }

    [Fact]
    public void Discarding_also_clears_the_message_of_the_last_action()
    {
        var cut = Open();
        cut.Find("#dish-name").Change("Tên đã lưu");
        Button(cut, "save").Click();
        cut.Find("[data-message]").ShouldNotBeNull();
        cut.Find("#dish-name").Change("Tên không lưu");

        Button(cut, "discard").Click();

        cut.FindAll("[data-message]").ShouldBeEmpty();
        cut.Find("#dish-name").GetAttribute("value").ShouldBe("Tên đã lưu");
    }

    [Fact]
    public void A_file_changed_elsewhere_is_never_overwritten_and_the_new_version_is_shown()
    {
        var cut = Open();
        cut.Find("#dish-name").Change("Tên của tôi");
        Data.WriteText($"dishes/{Id}.json", Disk().Replace("\"name\": \"Rau muống luộc\"", "\"name\": \"Rau muống luộc (đã sửa nơi khác)\""));
        var elsewhere = Disk();

        Button(cut, "save").Click();

        Text(cut, "[data-message]").ShouldStartWith("File món đã bị đổi ở chỗ khác");
        cut.Find("[data-message]").ClassList.ShouldContain("notice--danger");
        Disk().ShouldBe(elsewhere);
        cut.Find("#dish-name").GetAttribute("value").ShouldBe("Rau muống luộc (đã sửa nơi khác)");
        cut.FindAll("[data-unsaved]").ShouldBeEmpty();
    }

    [Fact]
    public void A_dish_that_vanished_is_reported_when_saving()
    {
        var cut = Open();
        cut.Find("#dish-name").Change("Tên của tôi");
        Data.Delete($"dishes/{Id}.json");

        Button(cut, "save").Click();

        cut.Find("[data-not-found]").ShouldNotBeNull();
    }

    // ---- approving ------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Approve_is_off_until_a_name_is_given_and_says_why()
    {
        var cut = Open();

        Button(cut, "approve").HasAttribute("disabled").ShouldBeTrue();
        Text(cut, "[data-approve-hint]").ShouldBe("Nhập tên người duyệt.");

        cut.Find("#reviewer").Input("Mẹ Lan");

        Button(cut, "approve").HasAttribute("disabled").ShouldBeFalse();
        cut.FindAll("[data-approve-hint]").ShouldBeEmpty();
    }

    [Fact]
    public void A_name_of_only_spaces_is_no_name()
    {
        var cut = Open();

        cut.Find("#reviewer").Input("   ");

        Button(cut, "approve").HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public void The_name_typed_last_time_is_there_already()
    {
        Identity.Name = "Mẹ Lan";

        var cut = Open();

        cut.Find("#reviewer").GetAttribute("value").ShouldBe("Mẹ Lan");
        Button(cut, "approve").HasAttribute("disabled").ShouldBeFalse();
    }

    [Fact]
    public void Approve_is_off_while_there_are_changes_that_are_not_saved()
    {
        Identity.Name = "Mẹ Lan";
        var cut = Open();

        cut.Find("#dish-name").Change("Tên khác");

        Button(cut, "approve").HasAttribute("disabled").ShouldBeTrue();
        Text(cut, "[data-approve-hint]").ShouldBe("Lưu thay đổi trước khi duyệt.");
    }

    [Fact]
    public void Approving_writes_exactly_the_status_the_reviewer_and_the_date_and_locks_the_dish()
    {
        var before = Disk();
        var cut = Open();
        var address = Navigation.Uri;
        cut.Find("#reviewer").Input("  Mẹ Lan ");

        Button(cut, "approve").Click();

        Navigation.Uri.ShouldBe(address);   // only a draft moves to its new page

        var after = Disk();
        before.Split('\n').Length.ShouldBe(after.Split('\n').Length);
        before.Split('\n').Zip(after.Split('\n')).Count(pair => pair.First != pair.Second).ShouldBe(3);
        after.ShouldContain("\"status\": \"reviewed\"");
        after.ShouldContain("\"reviewedBy\": \"Mẹ Lan\"");
        after.ShouldContain("\"reviewedAt\": \"2026-10-19\"");
        Text(cut, "[data-message]").ShouldBe("Đã duyệt. File món đã ghi trạng thái, người duyệt và ngày duyệt.");
        cut.Find(".dish-header__meta .badge").TextContent.ShouldBe("Đã duyệt");
        cut.Find("[data-reviewed]").TextContent.ShouldBe("Duyệt bởi Mẹ Lan, ngày 19/10/2026.");
        cut.Find("[data-locked]").TextContent.ShouldBe("Món đã duyệt nên chỉ xem được. Bấm \"Mở lại để sửa\" nếu cần sửa.");
        cut.FindAll("[data-approve]").ShouldBeEmpty();
        cut.FindAll("[data-save]").ShouldBeEmpty();
        cut.Find("fieldset.editor").HasAttribute("disabled").ShouldBeTrue();
        Identity.Name.ShouldBe("Mẹ Lan");
    }

    [Fact]
    public void A_dish_that_was_edited_and_saved_can_then_be_approved()
    {
        Identity.Name = "Mẹ Lan";
        var cut = Open();
        cut.Find("#dish-name").Change("Rau muống luộc nước chanh");
        Button(cut, "save").Click();

        Button(cut, "approve").HasAttribute("disabled").ShouldBeFalse();
        Button(cut, "approve").Click();

        var dish = DishFile.Parse(Disk());
        dish.Status.ShouldBe(DishStatus.Reviewed);
        dish.Name.ShouldBe("Rau muống luộc nước chanh");
    }

    [Fact]
    public void Approving_a_draft_makes_it_a_dish_and_goes_to_the_dish()
    {
        Data.MakeDraft(Id);
        Data.WriteReport(Id, "valid");
        Identity.Name = "Mẹ Lan";
        var cut = Open(Id, DishLocation.Draft);

        Button(cut, "approve").Click();

        Navigation.Uri.ShouldEndWith($"/dishes/{Id}");
        Data.Exists($"dishes/{Id}.json").ShouldBeTrue();
        Data.Exists($"dishes/_drafts/{Id}.json").ShouldBeFalse();
        Data.Exists($"dishes/_drafts/{Id}.report.json").ShouldBeFalse();
        DishFile.Parse(Disk()).Status.ShouldBe(DishStatus.Reviewed);
    }

    [Fact]
    public void A_dish_changed_elsewhere_is_not_approved()
    {
        Identity.Name = "Mẹ Lan";
        var cut = Open();
        Data.WriteText($"dishes/{Id}.json", Disk().Replace("nhặt, rửa sạch", "nhặt kỹ, rửa sạch"));
        var elsewhere = Disk();

        Button(cut, "approve").Click();

        Text(cut, "[data-message]").ShouldStartWith("File món đã bị đổi ở chỗ khác");
        Disk().ShouldBe(elsewhere);
        DishFile.Parse(Disk()).Status.ShouldBe(DishStatus.Draft);
    }

    [Fact]
    public void A_draft_is_not_promoted_over_a_dish_with_the_same_id()
    {
        Data.WriteText($"dishes/_drafts/{Id}.json", Disk());
        Identity.Name = "Mẹ Lan";
        var cut = Open(Id, DishLocation.Draft);

        Button(cut, "approve").Click();

        Text(cut, "[data-message]").ShouldStartWith("Đã có món cùng mã trong data/dishes");
        cut.Find("[data-message]").ClassList.ShouldContain("notice--danger");
        Data.Exists($"dishes/_drafts/{Id}.json").ShouldBeTrue();
        Navigation.Uri.ShouldNotEndWith($"/dishes/{Id}");
    }

    // ---- reviewed dishes ------------------------------------------------------------------------------------------------------

    private void MakeReviewed(string status = "reviewed") =>
        Data.EditJson($"dishes/{Id}.json", d =>
        {
            d["status"] = status;
            d["provenance"]!["reviewedBy"] = "Bà Hoa";
            d["provenance"]!["reviewedAt"] = "2026-09-01";
            if (status == "cooktested")
            {
                d["provenance"]!["cookTestedAt"] = "2026-09-02";
                d["provenance"]!["measuredTotalMinutes"] = 25;
            }
        });

    [Fact]
    public void A_reviewed_dish_is_shown_but_cannot_be_changed_and_says_who_reviewed_it()
    {
        MakeReviewed();

        var cut = Open();

        cut.Find("fieldset.editor").HasAttribute("disabled").ShouldBeTrue();
        cut.FindAll("[data-save]").ShouldBeEmpty();
        cut.FindAll("[data-approve]").ShouldBeEmpty();
        cut.FindAll("#reviewer").ShouldBeEmpty();
        cut.Find("[data-reviewed]").TextContent.ShouldBe("Duyệt bởi Bà Hoa, ngày 01/09/2026.");
        cut.Find("[data-reopen]").TextContent.Trim().ShouldBe("Mở lại để sửa");
    }

    [Fact]
    public void Reopening_makes_the_dish_a_draft_again_that_can_be_edited()
    {
        MakeReviewed();
        var cut = Open();

        Button(cut, "reopen").Click();

        Text(cut, "[data-message]").ShouldBe("Đã mở lại món về nháp để sửa.");
        cut.Find(".dish-header__meta .badge").TextContent.ShouldBe("Nháp");
        cut.Find("fieldset.editor").HasAttribute("disabled").ShouldBeFalse();
        cut.FindAll("[data-reopen]").ShouldBeEmpty();
        cut.Find("[data-not-reviewed]").ShouldNotBeNull();
        var dish = DishFile.Parse(Disk());
        dish.Status.ShouldBe(DishStatus.Draft);
        dish.Provenance.ReviewedBy.ShouldBeNull();
        dish.Provenance.ReviewedAt.ShouldBeNull();
    }

    [Fact]
    public void A_cooktested_dish_is_only_shown_with_its_test_and_cannot_be_reopened()
    {
        MakeReviewed("cooktested");

        var cut = Open();

        cut.Find("[data-locked]").TextContent.ShouldBe("Món đã nấu thử nên chỉ xem được.");
        cut.FindAll("[data-reopen]").ShouldBeEmpty();
        cut.FindAll("[data-save]").ShouldBeEmpty();
        Text(cut, "[data-provenance]").ShouldContain("Nấu thử ngày 02/09/2026.");
        cut.Find("fieldset.editor").HasAttribute("disabled").ShouldBeTrue();
    }

    // ---- moving between dishes ------------------------------------------------------------------------------------------------

    [Fact]
    public void The_same_page_shows_another_dish_when_its_parameters_change()
    {
        var cut = Open();

        cut.Render(p => p.Add(x => x.Id, "canh-bau-tom").Add(x => x.Location, DishLocation.Data));

        cut.Find("h1").TextContent.ShouldBe("Canh bầu nấu tôm");
        cut.Find("#dish-name").GetAttribute("value").ShouldBe("Canh bầu nấu tôm");
    }

    [Fact]
    public void A_message_about_one_dish_does_not_follow_the_reader_to_the_next()
    {
        Identity.Name = "Mẹ Lan";
        var cut = Open();
        Button(cut, "approve").Click();
        cut.Find("[data-message]").ShouldNotBeNull();

        cut.Render(p => p.Add(x => x.Id, "canh-bau-tom").Add(x => x.Location, DishLocation.Data));

        cut.FindAll("[data-message]").ShouldBeEmpty();
    }

    [Fact]
    public void Rendering_again_with_the_same_parameters_keeps_what_the_reader_has_typed()
    {
        var cut = Open();
        cut.Find("#dish-name").Change("Tên đang gõ dở");

        cut.Render(p => p.Add(x => x.Id, Id).Add(x => x.Location, DishLocation.Data));

        cut.Find("#dish-name").GetAttribute("value").ShouldBe("Tên đang gõ dở");
    }
}
