using System.Text.Json.Nodes;
using MamCom.Studio.Components.Pages;

namespace MamCom.Studio.Tests;

public class IngredientListTests : PageTestBase
{
    private static readonly double?[] Rice = [370, 7.04, 1.03, 80.3, 0.149, 0.462];

    private string[] Shown(IRenderedComponent<Ingredients> cut) => [.. cut.FindAll(".dish-row").Select(r => r.GetAttribute("data-ingredient")!)];

    [Fact]
    public void Lists_every_ingredient_with_its_name_and_a_link_to_it()
    {
        var cut = Render<Ingredients>();

        cut.FindAll(".dish-row").Count.ShouldBe(Data.Ingredients.Ids().Count);
        var row = cut.Find("[data-ingredient=rau-muong]");
        row.QuerySelector(".dish-row__name")!.TextContent.ShouldBe("Rau muống");
        row.QuerySelector("a")!.GetAttribute("href").ShouldBe("/ingredients/rau-muong");
    }

    [Fact]
    public void A_placeholder_says_it_has_no_numbers_and_how_many_dishes_use_it()
    {
        var row = Render<Ingredients>().Find("[data-ingredient=rau-muong]");

        row.QuerySelector("[data-nutrition=placeholder]")!.TextContent.ShouldBe("Chưa có số liệu");
        row.QuerySelector("[data-used-by]")!.TextContent.ShouldMatch(@"^Dùng trong [1-9]\d* món$");
    }

    [Fact]
    public void A_checked_ingredient_says_who_checked()
    {
        Data.WriteSuggestion("gao", (222, Rice));
        Data.IngredientReviewer.ChooseNutrition("gao", 222, "Mẹ Lan", Data.Ingredients.Read("gao")!.Version);

        var row = Render<Ingredients>().Find("[data-ingredient=gao]");

        row.QuerySelector("[data-nutrition=verified]")!.TextContent.ShouldBe("Đã xác nhận bởi Mẹ Lan");
        row.QuerySelectorAll("[data-nutrition=placeholder]").ShouldBeEmpty();
    }

    [Fact]
    public void A_source_without_a_name_of_who_checked_shows_the_database()
    {
        Data.EditJson("ingredients/gao.json", o =>
        {
            o["nutritionSource"]!["db"] = "vn-fct-2017";
            o["nutritionSource"]!["ref"] = "trang 12";
            o["nutritionSource"]!["matchedName"] = "Gạo tẻ";
        });

        Render<Ingredients>().Find("[data-ingredient=gao] [data-nutrition=verified]").TextContent.ShouldBe("Bảng thành phần thực phẩm Việt Nam 2017");
    }

    [Fact]
    public void Shows_how_many_candidates_wait_and_nothing_when_there_is_no_suggestion()
    {
        Data.WriteSuggestion("ca-rot", (1, Rice), (2, Rice));
        File.Delete(Data.PathOf("_suggestions/nutrition/gao.json"));
        var cut = Render<Ingredients>();

        cut.Find("[data-ingredient=ca-rot] [data-candidates]").TextContent.ShouldBe("2 gợi ý");
        cut.FindAll("[data-ingredient=gao] [data-candidates]").ShouldBeEmpty();
    }

    [Fact]
    public void Shows_a_picture_badge_only_for_an_ingredient_that_names_a_picture()
    {
        Data.WriteImage("gao-200.webp");
        Data.EditJson("ingredients/gao.json", o =>
        {
            o["image"]!["thumb"] = "gao-200.webp";
            o["image"]!["source"] = "tự chụp";
            o["image"]!["license"] = "CC0";
        });
        var cut = Render<Ingredients>();

        cut.Find("[data-ingredient=gao] [data-has-image]").TextContent.ShouldBe("Có ảnh");
        cut.FindAll("[data-ingredient=ca-rot] [data-has-image]").ShouldBeEmpty();
    }

    [Fact]
    public void Shows_how_many_errors_and_warnings_the_checks_found()
    {
        Data.EditJson("ingredients/gao.json", o => o["wasteRatio"] = 1.5);
        Data.EditJson("ingredients/thit-bo.json", o => o["marketUnit"] = new JsonObject { ["default"] = new JsonObject { ["display"] = "g", ["gramsPerUnit"] = 1, ["step"] = 50, ["showApproxGrams"] = true } });
        var cut = Render<Ingredients>();

        cut.Find("[data-ingredient=gao] [data-errors]").TextContent.ShouldBe("1 lỗi");
        cut.Find("[data-ingredient=thit-bo] [data-warnings]").TextContent.ShouldBe("1 cảnh báo");
        cut.FindAll("[data-ingredient=ca-rot] [data-errors]").ShouldBeEmpty();
        cut.FindAll("[data-ingredient=ca-rot] [data-warnings]").ShouldBeEmpty();
    }

    [Fact]
    public void A_file_that_is_not_an_ingredient_is_in_the_list_and_says_so()
    {
        Data.WriteText("ingredients/hong.json", "{ not json");

        var row = Render<Ingredients>().Find("[data-ingredient=hong]");

        row.QuerySelector(".dish-row__name")!.TextContent.ShouldBe("hong");
        row.QuerySelector("[data-broken]")!.TextContent.ShouldBe("File không đọc được");
        row.QuerySelectorAll("[data-used-by]").ShouldBeEmpty();
    }

    [Fact]
    public void The_filter_chips_show_how_many_ingredients_each_has()
    {
        var total = Data.Ingredients.Ids().Count;
        Data.WriteSuggestion("ca-rot", (1, Rice));
        File.Delete(Data.PathOf("_suggestions/nutrition/gao.json"));
        Data.WriteSuggestion("gao", (222, Rice));
        Data.IngredientReviewer.ChooseNutrition("gao", 222, "Mẹ Lan", Data.Ingredients.Read("gao")!.Version);

        var chips = Render<Ingredients>().FindAll(".chip").Select(c => c.TextContent.Trim()).ToArray();

        var suggested = Data.Ingredients.SuggestionIds().Count(id => id != "gao");   // gao is checked now, so it no longer waits
        chips.ShouldBe([$"Tất cả ({total})", $"Chưa có số liệu ({total - 1})", $"Có gợi ý chờ chọn ({suggested})", "Đã xác nhận (1)"]);
    }

    [Fact]
    public void Choosing_a_filter_shows_only_those_ingredients_and_all_shows_everything_again()
    {
        Data.WriteSuggestion("gao", (222, Rice));
        Data.IngredientReviewer.ChooseNutrition("gao", 222, "Mẹ Lan", Data.Ingredients.Read("gao")!.Version);
        var cut = Render<Ingredients>();

        cut.Find("[data-filter=verified]").Click();
        Shown(cut).ShouldBe(["gao"]);
        cut.Find("[data-filter=verified]").GetAttribute("aria-pressed").ShouldBe("true");

        cut.Find("[data-filter=nodata]").Click();
        Shown(cut).ShouldNotContain("gao");
        Shown(cut).Length.ShouldBe(Data.Ingredients.Ids().Count - 1);

        cut.Find("[data-filter=all]").Click();
        Shown(cut).Length.ShouldBe(Data.Ingredients.Ids().Count);
        cut.Find("[data-filter=all]").GetAttribute("aria-pressed").ShouldBe("true");
        cut.Find("[data-filter=verified]").GetAttribute("aria-pressed").ShouldBe("false");
    }

    [Fact]
    public void The_suggested_filter_shows_the_placeholders_that_have_candidates_waiting()
    {
        foreach (var id in Data.Ingredients.SuggestionIds())
        {
            File.Delete(Data.PathOf($"_suggestions/nutrition/{id}.json"));
        }

        Data.WriteSuggestion("ca-rot", (1, Rice));
        Data.WriteSuggestion("gao", (2, Rice));
        Data.IngredientReviewer.ChooseNutrition("gao", 2, "Mẹ Lan", Data.Ingredients.Read("gao")!.Version);
        var cut = Render<Ingredients>();

        cut.Find("[data-filter=suggested]").Click();

        Shown(cut).ShouldBe(["ca-rot"]);   // gao has a suggestion too, but it is already chosen
    }

    [Fact]
    public void A_suggestion_with_no_candidates_is_not_waiting_for_a_choice()
    {
        foreach (var id in Data.Ingredients.SuggestionIds())
        {
            File.Delete(Data.PathOf($"_suggestions/nutrition/{id}.json"));
        }

        Data.WriteSuggestion("ca-rot");   // FDC found nothing
        var cut = Render<Ingredients>();

        cut.Find("[data-filter=suggested]").Click();

        cut.FindAll(".dish-row").ShouldBeEmpty();
        cut.Find("[data-empty]").TextContent.ShouldBe("Không có nguyên liệu nào trong mục này.");
    }

    [Fact]
    public void A_file_that_is_not_an_ingredient_is_in_all_but_in_no_other_filter()
    {
        Data.WriteText("ingredients/hong.json", "{ not json");
        var cut = Render<Ingredients>();

        Shown(cut).ShouldContain("hong");
        foreach (var filter in new[] { "nodata", "suggested", "verified" })
        {
            cut.Find($"[data-filter={filter}]").Click();
            Shown(cut).ShouldNotContain("hong");
        }
    }

    [Fact]
    public void Explains_what_is_wrong_when_the_data_cannot_be_read()
    {
        Data.Delete("schemas/ingredient.schema.json");

        Render<Ingredients>().Find("[data-kitchen-problems]").ShouldNotBeNull();
    }
}

public class OverviewIngredientTests : PageTestBase
{
    [Fact]
    public void The_overview_counts_the_ingredients_with_and_without_numbers()
    {
        var total = Data.Ingredients.Ids().Count;
        Data.WriteSuggestion("gao", (222, [370, 7, 1, 80, 0.1, 0.4]));
        Data.IngredientReviewer.ChooseNutrition("gao", 222, "Mẹ Lan", Data.Ingredients.Read("gao")!.Version);

        var cut = Render<Home>();

        cut.Find("[data-ingredient-counts]").TextContent.Trim().ShouldBe($"{total - 1} nguyên liệu chưa có số liệu dinh dưỡng, 1 đã xác nhận.");
    }

    [Fact]
    public void The_overview_says_how_many_ingredients_have_candidates_waiting_and_is_quiet_when_none_do()
    {
        foreach (var id in Data.Ingredients.SuggestionIds())
        {
            File.Delete(Data.PathOf($"_suggestions/nutrition/{id}.json"));
        }

        Render<Home>().FindAll("[data-ingredient-suggested]").ShouldBeEmpty();

        Data.WriteSuggestion("ca-rot", (1, [1, 2, 3, 4, 5, 6]));
        Data.WriteSuggestion("gao", (2, [1, 2, 3, 4, 5, 6]));

        Render<Home>().Find("[data-ingredient-suggested]").TextContent.ShouldBe("2 nguyên liệu đã có gợi ý từ USDA chờ bạn chọn.");
    }

    [Fact]
    public void An_ingredient_that_is_already_chosen_no_longer_waits_for_a_choice_in_the_overview()
    {
        foreach (var id in Data.Ingredients.SuggestionIds())
        {
            File.Delete(Data.PathOf($"_suggestions/nutrition/{id}.json"));
        }

        Data.WriteSuggestion("ca-rot", (1, [1, 2, 3, 4, 5, 6]));
        Data.WriteSuggestion("gao", (2, [370, 7, 1, 80, 0.1, 0.4]));
        Data.IngredientReviewer.ChooseNutrition("gao", 2, "Mẹ Lan", Data.Ingredients.Read("gao")!.Version);   // gao has a suggestion and is done

        Render<Home>().Find("[data-ingredient-suggested]").TextContent.ShouldBe("1 nguyên liệu đã có gợi ý từ USDA chờ bạn chọn.");
    }

    [Fact]
    public void The_overview_links_to_the_ingredient_list()
    {
        Render<Home>().Find("[data-ingredients] a.button").GetAttribute("href").ShouldBe("/ingredients");
    }
}
