using System.Text.Json.Nodes;
using MamCom.Core.Models;
using MamCom.DataBuilder.Loading;
using MamCom.DataBuilder.Validation;
using MamCom.Studio.Components;
using MamCom.Studio.Review;

namespace MamCom.Studio.Tests;

public class IngredientWorkbenchTests : PageTestBase
{
    private const string Ing = "gao";

    private static readonly double?[] Rice = [370, 7.04, 1.03, 80.3, 0.149, 0.462];

    private string Disk(string id = Ing) => Data.ReadText($"ingredients/{id}.json");

    private static string Text(IRenderedComponent<IngredientWorkbench> cut, string selector) => cut.Find(selector).TextContent.Trim();

    private static AngleSharp.Dom.IElement Button(IRenderedComponent<IngredientWorkbench> cut, string marker) => cut.Find($"[data-{marker}]");

    private static bool Disabled(IRenderedComponent<IngredientWorkbench> cut, string marker) => Button(cut, marker).HasAttribute("disabled");

    private void SuggestRice() => Data.WriteSuggestion(Ing, (111, [100, 1, 1, 1, 1, 1]), (222, Rice));

    // ---- what is shown --------------------------------------------------------------------------------------------------------

    [Fact]
    public void An_ingredient_that_is_not_there_says_so_and_a_way_back_is_given()
    {
        var cut = OpenIngredient("khong-co");

        cut.Find("[data-not-found]").TextContent.ShouldBe("Không tìm thấy nguyên liệu này.");
        cut.Find("[data-back]").GetAttribute("href").ShouldBe("/ingredients");
        cut.FindAll("[data-save]").ShouldBeEmpty();
    }

    [Fact]
    public void An_id_that_could_leave_the_folder_is_just_not_found()
    {
        OpenIngredient("../config/portion-defaults").Find("[data-not-found]").ShouldNotBeNull();
    }

    [Fact]
    public void A_file_that_is_not_an_ingredient_shows_why_and_cannot_be_edited()
    {
        Data.WriteText("ingredients/hong.json", "{ not json");

        var cut = OpenIngredient("hong");

        cut.Find("[data-broken]").ShouldNotBeNull();
        cut.Find("pre.problem").TextContent.ShouldNotBeNullOrWhiteSpace();
        cut.FindAll("[data-save]").ShouldBeEmpty();
    }

    [Fact]
    public void Shows_the_name_the_state_of_the_numbers_and_the_file()
    {
        var cut = OpenIngredient();

        cut.Find("h1").TextContent.ShouldBe("Gạo");
        cut.Find("[data-nutrition-badge]").TextContent.ShouldBe("Chưa có số liệu");
        cut.Find(".dish-header__meta").TextContent.ShouldContain("File: ingredients/gao.json");
        cut.Find("[data-back]").ClassList.ShouldContain("back-link");
    }

    [Fact]
    public void The_title_is_the_first_name_there_is_when_there_is_no_default_name()
    {
        Data.EditJson("ingredients/gao.json", o => o["names"] = new JsonObject { ["north"] = "Gạo Bắc", ["central"] = "Gạo Trung", ["south"] = "Gạo Nam" });

        OpenIngredient().Find("h1").TextContent.ShouldBe("Gạo Bắc");
    }

    [Fact]
    public void The_fields_hold_the_values_of_the_file()
    {
        var cut = OpenIngredient();

        cut.Find("[data-name=default]").GetAttribute("value").ShouldBe("Gạo");
        cut.Find("[data-name=north]").GetAttribute("value").ShouldBeNullOrEmpty();
        cut.Find("#ing-aliases").GetAttribute("value").ShouldBe("gạo tẻ");
        cut.Find("#ing-category").GetAttribute("value").ShouldBe("ngu-coc");
        cut.Find("#ing-aisle").GetAttribute("value").ShouldBe("DryGoodsAndSpices");
        cut.Find("#ing-protein").GetAttribute("value").ShouldBeNullOrEmpty();
        cut.Find("#ing-waste").GetAttribute("value").ShouldBe("0");
        cut.Find("[data-pantry]").HasAttribute("checked").ShouldBeTrue();
    }

    [Fact]
    public void Only_the_units_that_are_switched_on_show_their_fields()
    {
        var cut = OpenIngredient();

        cut.Find("[data-unit=default] [data-unit-enabled]").HasAttribute("checked").ShouldBeTrue();
        cut.Find("[data-unit=default] [data-unit-display]").GetAttribute("value").ShouldBe("kg");
        cut.Find("[data-unit=default] [data-unit-grams]").GetAttribute("value").ShouldBe("1000");
        cut.Find("[data-unit=default] [data-unit-step]").GetAttribute("value").ShouldBe("0.5");
        cut.Find("[data-unit=north] [data-unit-enabled]").HasAttribute("checked").ShouldBeFalse();
        cut.FindAll("[data-unit=north] [data-unit-display]").ShouldBeEmpty();
    }

    [Fact]
    public void The_choices_are_in_words()
    {
        var cut = OpenIngredient();

        cut.Find("#ing-aisle").QuerySelectorAll("option").Select(o => o.TextContent).ShouldBe(["Thịt cá", "Rau củ quả", "Trứng đậu phụ", "Khô và gia vị", "Khác"]);
        var protein = cut.Find("#ing-protein").QuerySelectorAll("option").Select(o => (o.GetAttribute("value"), o.TextContent)).ToArray();
        protein[0].ShouldBe(("", "Không phải nguồn đạm"));
        protein.Select(p => p.Item1).ShouldNotContain("none");
        protein.Select(p => p.Item1).ShouldContain("heo");
        cut.FindAll("label").Select(l => l.TextContent.Trim()).ShouldContain("Mặc định (mọi vùng)");
    }

    [Fact]
    public void A_protein_source_the_kitchen_does_not_know_stays_visible_as_an_option()
    {
        Data.EditJson("ingredients/gao.json", o => o["proteinSource"] = "nhim");

        OpenIngredient().Find("#ing-protein").QuerySelectorAll("option").Select(o => o.GetAttribute("value")).ShouldContain("nhim");
    }

    [Fact]
    public void The_categories_the_kitchen_already_uses_are_offered()
    {
        var options = OpenIngredient().Find("#ing-categories").QuerySelectorAll("option").Select(o => o.GetAttribute("value")).ToArray();

        options.ShouldContain("ngu-coc");
        options.ShouldContain("rau-la");
        options.Distinct().Count().ShouldBe(options.Length);
    }

    [Fact]
    public void Lists_the_dishes_that_use_the_ingredient_with_links()
    {
        var cut = OpenIngredient("rau-muong");

        var links = cut.FindAll("[data-used-by] a").Select(a => (a.GetAttribute("href"), a.TextContent)).ToArray();

        links.ShouldContain(("/dishes/rau-muong-luoc", "Rau muống luộc"));
        links.Select(l => l.Item1).ShouldBe([.. links.Select(l => l.Item1).Order(StringComparer.Ordinal)]);
    }

    [Fact]
    public void Says_so_when_no_dish_uses_the_ingredient()
    {
        var copy = Data.ReadJson("ingredients/rau-muong.json");
        copy["id"] = "rau-muong-moi";
        Data.WriteJson("ingredients/rau-muong-moi.json", copy);

        var cut = OpenIngredient("rau-muong-moi");

        cut.FindAll("[data-used-by] a").ShouldBeEmpty();
        Text(cut, "[data-used-by]").ShouldContain("Chưa món nào dùng nguyên liệu này.");
    }

    [Fact]
    public void Shows_the_findings_of_the_checks()
    {
        Data.EditJson("ingredients/thit-bo.json", o => o["marketUnit"] = new JsonObject { ["default"] = new JsonObject { ["display"] = "g", ["gramsPerUnit"] = 1, ["step"] = 50, ["showApproxGrams"] = true } });

        var cut = OpenIngredient("thit-bo");

        cut.Find("[data-findings] .finding").GetAttribute("data-severity").ShouldBe("warning");
        cut.Find("[data-warnings]").TextContent.ShouldBe("1 cảnh báo");
        cut.FindAll("[data-errors]").ShouldBeEmpty();
    }

    [Fact]
    public void Explains_what_is_wrong_when_the_data_cannot_be_read_and_turns_the_buttons_off()
    {
        SuggestRice();
        Data.Delete("config/spinner-defaults.json");
        Identity.Name = "Mẹ Lan";

        var cut = OpenIngredient();
        cut.Find("[data-name=default]").Change("Gạo tẻ");   // there is something to save, but the data cannot be checked

        cut.Find("[data-kitchen-problems]").TextContent.ShouldContain("config/spinner-defaults.json");
        Disabled(cut, "save").ShouldBeTrue();
        cut.FindAll("[data-choose]").ShouldAllBe(b => b.HasAttribute("disabled"));
    }

    // ---- the numbers ------------------------------------------------------------------------------------------------------------

    [Fact]
    public void With_unusable_data_choosing_is_off_even_when_nothing_else_is_in_the_way_and_the_hint_says_why()
    {
        SuggestRice();
        Identity.Name = "Mẹ Lan";   // a name is given, nothing is unsaved: only the data stands in the way
        Data.Delete("config/spinner-defaults.json");

        var cut = OpenIngredient();

        cut.FindAll("[data-choose]").ShouldAllBe(b => b.HasAttribute("disabled"));
        Text(cut, "[data-choose-hint]").ShouldBe("Chưa đọc được dữ liệu nên chưa lưu hay duyệt được.");
    }

    [Fact]
    public void A_placeholder_shows_unknown_numbers_and_says_there_is_no_source()
    {
        var cut = OpenIngredient();

        Text(cut, "[data-nutrition] .nutrition__kcal").ShouldBe("chưa đủ số liệu");
        Text(cut, "[data-source-none]").ShouldBe("Chưa có nguồn: số liệu đang để trống.");
        cut.Find("[data-nutrition] section").GetAttribute("aria-label").ShouldBe("Dinh dưỡng trên 100 g ăn được");
        cut.FindAll("[data-clear]").ShouldBeEmpty();
        cut.FindAll("[data-source-link]").ShouldBeEmpty();
    }

    [Fact]
    public void A_checked_ingredient_shows_its_numbers_its_source_the_link_and_who_checked()
    {
        SuggestRice();
        Identity.Name = "Mẹ Lan";
        var cut = OpenIngredient();
        Button(cut, "choose").Click();   // first candidate of two

        cut = OpenIngredient();

        Text(cut, "[data-nutrition] .nutrition__kcal").ShouldBe("≈ 100 kcal");
        Text(cut, "[data-source]").ShouldBe("Nguồn: USDA FoodData Central, mã 111, \"Test food 111, raw\".");
        cut.Find("[data-source-link]").GetAttribute("href").ShouldBe("https://fdc.nal.usda.gov/food-details/111/nutrients");
        cut.Find("[data-source-link]").GetAttribute("rel").ShouldBe("noopener noreferrer");
        cut.Find("[data-source-link]").ClassList.ShouldContain("link-action");
        Text(cut, "[data-verified]").ShouldBe("Xác nhận bởi Mẹ Lan, ngày 19/10/2026.");
        cut.Find("[data-nutrition-badge]").TextContent.ShouldBe("Đã xác nhận bởi Mẹ Lan");
        cut.FindAll("[data-source-none]").ShouldBeEmpty();
    }

    [Fact]
    public void A_source_that_is_not_fdc_has_no_fdc_link_and_shows_the_database_by_name()
    {
        Data.EditJson("ingredients/gao.json", o =>
        {
            o["nutritionSource"]!["db"] = "vn-fct-2017";
            o["nutritionSource"]!["ref"] = "trang 12";
            o["nutritionSource"]!["matchedName"] = "Gạo tẻ";
        });

        var cut = OpenIngredient();

        Text(cut, "[data-source]").ShouldBe("Nguồn: Bảng thành phần thực phẩm Việt Nam 2017, mã trang 12, \"Gạo tẻ\".");
        cut.FindAll("[data-source-link]").ShouldBeEmpty();
        cut.FindAll("[data-verified]").ShouldBeEmpty();
        cut.Find("[data-nutrition-badge]").TextContent.ShouldBe("Bảng thành phần thực phẩm Việt Nam 2017");
    }

    // ---- the candidates -----------------------------------------------------------------------------------------------------------

    [Fact]
    public void Without_a_suggestion_it_says_where_one_comes_from()
    {
        File.Delete(Data.PathOf("_suggestions/nutrition/gao.json"));

        var cut = OpenIngredient();

        cut.Find("[data-no-suggestion]").TextContent.ShouldContain("MamCom.FdcImporter");
        cut.FindAll("[data-candidate]").ShouldBeEmpty();
    }

    [Fact]
    public void Lists_the_candidates_with_the_numbers_exactly_as_FDC_gave_them_and_what_it_did_not_state()
    {
        Data.WriteSuggestion(Ing, (222, Rice), (333, [365, 7.13, 0.66, 80, null, 5]));

        var cut = OpenIngredient();

        cut.FindAll("[data-candidate]").Select(c => c.GetAttribute("data-candidate")).ShouldBe(["222", "333"]);
        cut.Find("[data-candidate='222'] [data-candidate-numbers]").TextContent.ShouldContain("0.149");
        cut.Find("[data-candidate='222'] [data-candidate-numbers]").TextContent.ShouldContain("0.462");
        cut.FindAll("[data-candidate='222'] [data-missing]").ShouldBeEmpty();
        cut.Find("[data-candidate='333'] [data-candidate-numbers]").TextContent.ShouldContain("FDC không nêu");
        Text(cut, "[data-candidate='333'] [data-missing]").ShouldBe("FDC không nêu: fiberG.");
        Text(cut, "[data-suggestion-info]").ShouldBe("Tìm bằng \"test query\" ngày 19/10/2026: FDC có 12 món khớp, giữ 2.");
        var link = cut.Find("[data-candidate='222'] a");
        link.GetAttribute("href").ShouldBe("https://fdc.nal.usda.gov/food-details/222/nutrients");
        link.GetAttribute("rel").ShouldBe("noopener noreferrer");
        link.GetAttribute("target").ShouldBe("_blank");
        link.ClassList.ShouldContain("link-action");   // the class that gives it a 48px touch target
    }

    [Fact]
    public void A_candidate_with_no_number_at_all_cannot_be_chosen_and_says_why()
    {
        Data.WriteSuggestion(Ing, (555, [null, null, null, null, null, null]), (222, Rice));
        Identity.Name = "Mẹ Lan";

        var cut = OpenIngredient();

        cut.Find("[data-candidate='555'] [data-no-numbers]").ShouldNotBeNull();
        cut.Find("[data-candidate='555'] [data-choose]").HasAttribute("disabled").ShouldBeTrue();
        cut.Find("[data-candidate='222'] [data-choose]").HasAttribute("disabled").ShouldBeFalse();
        cut.FindAll("[data-candidate='222'] [data-no-numbers]").ShouldBeEmpty();
    }

    [Fact]
    public void Choosing_is_off_until_a_name_is_given_and_says_why()
    {
        SuggestRice();
        var cut = OpenIngredient();

        cut.FindAll("[data-choose]").ShouldAllBe(b => b.HasAttribute("disabled"));
        Text(cut, "[data-choose-hint]").ShouldBe("Nhập tên người xác nhận.");

        cut.Find("#verifier").Input("Mẹ Lan");

        cut.FindAll("[data-choose]").ShouldAllBe(b => !b.HasAttribute("disabled"));
        cut.FindAll("[data-choose-hint]").ShouldBeEmpty();
    }

    [Fact]
    public void A_name_of_only_spaces_is_no_name_and_the_name_typed_last_time_is_there_already()
    {
        SuggestRice();
        Identity.Name = "Mẹ Lan";
        var cut = OpenIngredient();

        cut.Find("#verifier").GetAttribute("value").ShouldBe("Mẹ Lan");
        cut.FindAll("[data-choose]").ShouldAllBe(b => !b.HasAttribute("disabled"));

        cut.Find("#verifier").Input("   ");

        cut.FindAll("[data-choose]").ShouldAllBe(b => b.HasAttribute("disabled"));
    }

    [Fact]
    public void Choosing_a_candidate_writes_its_numbers_and_source_and_remembers_the_name()
    {
        SuggestRice();
        var before = Disk();
        var cut = OpenIngredient();
        cut.Find("#verifier").Input("  Mẹ Lan ");

        cut.Find("[data-candidate='222'] [data-choose]").Click();

        var after = Disk();
        before.Split('\n').Length.ShouldBe(after.Split('\n').Length);
        before.Split('\n').Zip(after.Split('\n')).Count(pair => pair.First != pair.Second).ShouldBe(11);   // six numbers and five source lines
        var ingredient = IngredientFile.Parse(after);
        ingredient.NutritionPer100gEdible.ShouldBe(new NutritionFacts(370, 7.04, 1.03, 80.3, 0.149, 0.462));
        ingredient.NutritionSource.ShouldBe(new NutritionSource("usda-fdc", "222", "Test food 222, raw", "Mẹ Lan", StudioData.Today));
        Text(cut, "[data-message]").ShouldBe("Đã ghi số liệu dinh dưỡng, nguồn và người xác nhận vào nguyên liệu.");
        cut.Find("[data-message]").ClassList.ShouldContain("notice--ok");
        cut.Find("[data-candidate='222'] [data-in-use]").TextContent.ShouldBe("Đang dùng");
        cut.FindAll("[data-candidate='111'] [data-in-use]").ShouldBeEmpty();
        cut.Find("[data-nutrition-badge]").TextContent.ShouldBe("Đã xác nhận bởi Mẹ Lan");
        Identity.Name.ShouldBe("Mẹ Lan");
        DataValidator.Validate(DataLoader.Load(Data.Root), ValidationMode.Dev).HasErrors.ShouldBeFalse();
    }

    [Fact]
    public void Choosing_is_off_while_there_are_changes_that_are_not_saved()
    {
        SuggestRice();
        Identity.Name = "Mẹ Lan";
        var cut = OpenIngredient();

        cut.Find("[data-name=default]").Change("Gạo tẻ");

        cut.FindAll("[data-choose]").ShouldAllBe(b => b.HasAttribute("disabled"));
        Text(cut, "[data-choose-hint]").ShouldBe("Lưu thay đổi trước khi chọn số liệu.");
    }

    [Fact]
    public void Clearing_is_off_while_there_are_changes_that_are_not_saved_and_needs_no_name()
    {
        SuggestRice();
        Identity.Name = "Mẹ Lan";
        OpenIngredient().Find("[data-candidate='222'] [data-choose]").Click();
        Identity.Name = string.Empty;
        var cut = OpenIngredient();

        Disabled(cut, "clear").ShouldBeFalse();   // no name is typed, and none is needed to take the numbers out

        cut.Find("[data-name=default]").Change("Gạo tẻ");

        Disabled(cut, "clear").ShouldBeTrue();
    }

    [Fact]
    public void Choosing_is_off_while_the_ingredient_has_an_error()
    {
        SuggestRice();
        Identity.Name = "Mẹ Lan";
        var cut = OpenIngredient();

        cut.Find("#ing-waste").Change("1.5");

        cut.FindAll("[data-choose]").ShouldAllBe(b => b.HasAttribute("disabled"));
        Disk().ShouldContain("\"wasteRatio\": 0,");
    }

    [Fact]
    public void An_ingredient_that_already_has_an_error_on_disk_cannot_get_numbers_until_it_is_fixed_and_the_hint_says_why()
    {
        SuggestRice();
        Identity.Name = "Mẹ Lan";   // a name is given and nothing is unsaved: only the error in the file stands in the way
        var seen = IngredientFile.Parse(Disk());
        Data.WriteText("ingredients/gao.json", IngredientFile.Serialize(seen with { WasteRatio = 1.5 }));   // in the canonical format, so nothing counts as unsaved

        var cut = OpenIngredient();

        cut.FindAll("[data-unsaved]").ShouldBeEmpty();
        cut.FindAll("[data-choose]").ShouldAllBe(b => b.HasAttribute("disabled"));
        Text(cut, "[data-choose-hint]").ShouldBe("Sửa hết lỗi trước khi chọn số liệu.");
    }

    [Fact]
    public void A_file_changed_elsewhere_is_not_written_over_when_choosing()
    {
        SuggestRice();
        Identity.Name = "Mẹ Lan";
        var cut = OpenIngredient();
        Data.WriteText("ingredients/gao.json", Disk().Replace("\"Gạo\"", "\"Gạo (đã sửa nơi khác)\""));
        var elsewhere = Disk();

        cut.Find("[data-candidate='222'] [data-choose]").Click();

        Text(cut, "[data-message]").ShouldStartWith("File nguyên liệu đã bị đổi ở chỗ khác");
        cut.Find("[data-message]").ClassList.ShouldContain("notice--danger");
        Disk().ShouldBe(elsewhere);
        cut.Find("[data-name=default]").GetAttribute("value").ShouldBe("Gạo (đã sửa nơi khác)");
    }

    [Fact]
    public void Clearing_a_choice_gives_the_ingredient_back_its_original_bytes()
    {
        SuggestRice();
        Identity.Name = "Mẹ Lan";
        var original = Disk();
        var cut = OpenIngredient();
        cut.Find("[data-candidate='222'] [data-choose]").Click();

        Button(cut, "clear").Click();

        Text(cut, "[data-message]").ShouldBe("Đã bỏ số liệu dinh dưỡng; nguyên liệu về chưa có nguồn.");
        Disk().ShouldBe(original);
        cut.Find("[data-nutrition-badge]").TextContent.ShouldBe("Chưa có số liệu");
        cut.FindAll("[data-clear]").ShouldBeEmpty();
    }

    // ---- editing ----------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Nothing_to_save_before_anything_is_changed()
    {
        var cut = OpenIngredient();

        Disabled(cut, "save").ShouldBeTrue();
        Disabled(cut, "discard").ShouldBeTrue();
        cut.FindAll("[data-unsaved]").ShouldBeEmpty();
    }

    [Fact]
    public void A_change_says_it_is_not_saved_and_putting_it_back_is_no_change()
    {
        var cut = OpenIngredient();

        cut.Find("[data-name=default]").Change("Gạo tẻ");

        Text(cut, "[data-unsaved]").ShouldBe("Có thay đổi chưa lưu. Lưu rồi mới chọn được số liệu dinh dưỡng.");
        Disabled(cut, "save").ShouldBeFalse();
        Disabled(cut, "discard").ShouldBeFalse();

        cut.Find("[data-name=default]").Change("Gạo");

        cut.FindAll("[data-unsaved]").ShouldBeEmpty();
        Disabled(cut, "save").ShouldBeTrue();
    }

    [Fact]
    public void Every_kind_of_field_reaches_the_file()
    {
        var cut = OpenIngredient();

        cut.Find("[data-name=north]").Change("Gạo Bắc");
        cut.Find("#ing-aliases").Change("gạo tẻ\ngạo thường");
        cut.Find("#ing-category").Change("ngu-coc-moi");
        cut.Find("#ing-aisle").Change("Other");
        cut.Find("#ing-protein").Change("heo");
        cut.Find("#ing-waste").Change("0.05");
        cut.Find("[data-pantry]").Change(false);
        cut.Find("[data-unit=default] [data-unit-display]").Change("bao");
        cut.Find("[data-unit=default] [data-unit-grams]").Change("5000");
        cut.Find("[data-unit=default] [data-unit-step]").Change("1");
        cut.Find("[data-unit=default] [data-unit-approx]").Change(true);
        cut.Find("[data-unit=south] [data-unit-enabled]").Change(true);
        cut.Find("[data-unit=south] [data-unit-display]").Change("kg");
        Button(cut, "save").Click();

        var ingredient = IngredientFile.Parse(Disk());
        ingredient.Names["north"].ShouldBe("Gạo Bắc");
        ingredient.Aliases.ShouldBe(["gạo tẻ", "gạo thường"]);
        ingredient.Category.ShouldBe("ngu-coc-moi");
        ingredient.Aisle.ShouldBe(Aisle.Other);
        ingredient.ProteinSource.ShouldBe("heo");
        ingredient.WasteRatio.ShouldBe(0.05);
        ingredient.IsPantry.ShouldBeFalse();
        ingredient.MarketUnit["default"].ShouldBe(new MarketUnit("bao", 5000, 1, true));
        ingredient.MarketUnit["south"].Display.ShouldBe("kg");
    }

    [Fact]
    public void Saving_writes_the_file_and_says_so()
    {
        var cut = OpenIngredient();
        cut.Find("[data-name=default]").Change("Gạo tẻ");

        Button(cut, "save").Click();

        Text(cut, "[data-message]").ShouldBe("Đã lưu.");
        cut.Find("[data-message]").ClassList.ShouldContain("notice--ok");
        cut.Find("h1").TextContent.ShouldBe("Gạo tẻ");
        cut.FindAll("[data-unsaved]").ShouldBeEmpty();
        IngredientFile.Parse(Disk()).Names["default"].ShouldBe("Gạo tẻ");
    }

    [Fact]
    public void An_ingredient_with_an_error_shows_it_at_once_and_cannot_be_saved()
    {
        var cut = OpenIngredient();

        cut.Find("#ing-waste").Change("1.5");

        cut.Find("[data-errors]").TextContent.ShouldBe("1 lỗi");
        cut.Find("[data-findings] .finding").GetAttribute("data-severity").ShouldBe("error");
        Disabled(cut, "save").ShouldBeTrue();

        cut.Find("#ing-waste").Change("0");

        cut.FindAll("[data-errors]").ShouldBeEmpty();
    }

    [Fact]
    public void Switching_off_every_unit_is_an_error_the_schema_names()
    {
        var cut = OpenIngredient();

        cut.Find("[data-unit=default] [data-unit-enabled]").Change(false);

        cut.Find("[data-errors]").ShouldNotBeNull();
        Disabled(cut, "save").ShouldBeTrue();
        cut.FindAll("[data-unit=default] [data-unit-display]").ShouldBeEmpty();
    }

    [Fact]
    public void Discarding_puts_the_form_back_to_the_file_and_clears_the_message()
    {
        var cut = OpenIngredient();
        cut.Find("[data-name=default]").Change("Gạo tẻ");
        Button(cut, "save").Click();
        cut.Find("[data-message]").ShouldNotBeNull();
        cut.Find("[data-name=default]").Change("Gạo khác");

        Button(cut, "discard").Click();

        cut.Find("[data-name=default]").GetAttribute("value").ShouldBe("Gạo tẻ");
        cut.FindAll("[data-message]").ShouldBeEmpty();
        cut.FindAll("[data-unsaved]").ShouldBeEmpty();
    }

    [Fact]
    public void A_file_changed_elsewhere_is_never_overwritten_when_saving_and_the_new_version_is_shown()
    {
        var cut = OpenIngredient();
        cut.Find("[data-name=default]").Change("Tên của tôi");
        Data.WriteText("ingredients/gao.json", Disk().Replace("gạo tẻ", "gạo tẻ thơm"));
        var elsewhere = Disk();

        Button(cut, "save").Click();

        Text(cut, "[data-message]").ShouldStartWith("File nguyên liệu đã bị đổi ở chỗ khác");
        Disk().ShouldBe(elsewhere);
        cut.Find("[data-name=default]").GetAttribute("value").ShouldBe("Gạo");
        cut.Find("#ing-aliases").GetAttribute("value").ShouldBe("gạo tẻ thơm");
    }

    [Fact]
    public void An_ingredient_that_vanished_is_reported_when_saving()
    {
        var cut = OpenIngredient();
        cut.Find("[data-name=default]").Change("Gạo tẻ");
        Data.Delete("ingredients/gao.json");

        Button(cut, "save").Click();

        cut.Find("[data-not-found]").ShouldNotBeNull();
    }

    [Fact]
    public void The_same_page_shows_another_ingredient_when_its_id_changes_and_keeps_typing_when_it_does_not()
    {
        var cut = OpenIngredient();
        cut.Find("[data-name=default]").Change("Đang gõ dở");

        cut.Render(p => p.Add(x => x.Id, Ing));
        cut.Find("[data-name=default]").GetAttribute("value").ShouldBe("Đang gõ dở");

        cut.Render(p => p.Add(x => x.Id, "ca-rot"));
        cut.Find("h1").TextContent.ShouldBe("Cà rốt");
        cut.Find("[data-name=default]").GetAttribute("value").ShouldBe("Cà rốt");
    }

    // ---- the picture --------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Without_any_picture_file_it_says_so()
    {
        OpenIngredient().Find("[data-no-images]").TextContent.ShouldContain("data/images/out");
    }

    [Fact]
    public void The_picture_files_are_offered_and_the_chosen_one_is_previewed_and_saved_with_its_source_and_license()
    {
        Data.WriteImage("gao-200.webp");
        Data.WriteImage("gao-800.webp");
        var cut = OpenIngredient();

        cut.FindAll("[data-no-images]").ShouldBeEmpty();
        cut.FindAll("[data-image-preview]").ShouldBeEmpty();
        cut.Find("#ing-image-thumb").QuerySelectorAll("option").Select(o => o.GetAttribute("value")).ShouldBe(["", "gao-200.webp", "gao-800.webp"]);

        cut.Find("#ing-image-thumb").Change("gao-200.webp");
        cut.Find("#ing-image-full").Change("gao-800.webp");
        cut.Find("#ing-image-source").Change("tự chụp");
        cut.Find("#ing-image-license").Change("CC0");
        cut.Find("[data-illustration]").Change(true);

        cut.Find("[data-image-preview]").GetAttribute("src").ShouldBe("/data-images/gao-800.webp");   // the big picture is the one shown
        cut.Find("[data-image-preview]").GetAttribute("alt").ShouldBe("Ảnh của Gạo");
        Button(cut, "save").Click();
        IngredientFile.Parse(Disk()).Image.ShouldBe(new ImageRef("gao-200.webp", "gao-800.webp", "tự chụp", "CC0", true));
    }

    [Fact]
    public void Only_the_small_picture_is_previewed_when_there_is_no_big_one()
    {
        Data.WriteImage("gao-200.webp");
        var cut = OpenIngredient();

        cut.Find("#ing-image-thumb").Change("gao-200.webp");

        cut.Find("[data-image-preview]").GetAttribute("src").ShouldBe("/data-images/gao-200.webp");
    }

    [Fact]
    public void A_picture_without_its_source_and_license_is_an_error()
    {
        Data.WriteImage("gao-200.webp");
        var cut = OpenIngredient();

        cut.Find("#ing-image-thumb").Change("gao-200.webp");

        cut.Find("[data-errors]").ShouldNotBeNull();
        Disabled(cut, "save").ShouldBeTrue();
    }

    [Fact]
    public void A_picture_the_file_names_but_the_folder_does_not_have_stays_visible_as_an_option_with_an_error_and_no_preview()
    {
        Data.EditJson("ingredients/gao.json", o =>
        {
            o["image"]!["thumb"] = "mat-roi.webp";
            o["image"]!["source"] = "tự chụp";
            o["image"]!["license"] = "CC0";
        });

        var cut = OpenIngredient();

        cut.Find("#ing-image-thumb").QuerySelectorAll("option").Select(o => o.GetAttribute("value")).ShouldContain("mat-roi.webp");
        cut.Find("#ing-image-thumb").GetAttribute("value").ShouldBe("mat-roi.webp");
        cut.FindAll("[data-image-preview]").ShouldBeEmpty();
        cut.Find("[data-errors]").ShouldNotBeNull();
        cut.FindAll("[data-no-images]").ShouldBeEmpty();
    }
}
