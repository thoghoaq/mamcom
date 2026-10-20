using System.Reflection;
using AngleSharp.Dom;
using Bunit.TestDoubles;
using MamCom.Core.Data;
using MamCom.Core.Models;
using MamCom.Core.Taste;
using MamCom.Data;
using MamCom.Data.Bundles;
using MamCom.Data.State;
using MamCom.UI.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace MamCom.UI.Tests;

public class DishDetailScreenTests : BunitContext
{
    private readonly FakeAppData _data = new() { Bundle = TestConfig.SeedBundle };
    private readonly FakeTasteStore _taste = new();

    public DishDetailScreenTests()
    {
        Services.AddSingleton<IAppData>(_data);
        Services.AddSingleton<ITasteStore>(_taste);
    }

    private static Member Adult(string name, params MemberTag[] tags) => new(name, AgeGroup.Adult, 2, tags, []);

    /// <summary>Two adults at two bowls: exactly one serving each, so 2.0 servings for every kind of dish.</summary>
    private static Household TwoAdults(Region region = Region.North, params MemberTag[] fatherTags) =>
        new(region, BowlSize.Medium, [Adult("Bố", fatherTags), Adult("Mẹ")]);

    /// <summary>A father of 1.0 serving and a child of 0.595: 1.595 servings.</summary>
    private static Household WithChild() =>
        new(Region.North, BowlSize.Medium, [Adult("Bố"), new Member("Con", AgeGroup.Child, 1, [], [])]);

    private static Dish Seed(string id) => TestConfig.SeedBundle.Dishes.Single(d => d.Id == id);

    private static Dish Braise => Seed("thit-kho-trung");

    // Test-only numbers, not nutrition data: every ingredient gets the same per 100 g, so a serving of 169.5 g is easy to work out.
    private static DataBundle Known(Func<Ingredient, Ingredient>? change = null) => TestConfig.SeedBundle with
    {
        Ingredients = [.. TestConfig.SeedBundle.Ingredients.Select(i => i with { NutritionPer100gEdible = new NutritionFacts(100, 10, 5, 8, 1, 50) }).Select(i => change?.Invoke(i) ?? i)],
    };

    private static DataBundle WithDish(Dish dish, params Ingredient[] extra) => TestConfig.SeedBundle with
    {
        Dishes = [.. TestConfig.SeedBundle.Dishes.Where(d => d.Id != dish.Id), dish],
        Ingredients = [.. TestConfig.SeedBundle.Ingredients, .. extra],
    };

    private IRenderedComponent<DishDetailScreen> Show(string id = "thit-kho-trung", Household? household = null) =>
        Render<DishDetailScreen>(p => p.Add(s => s.Household, household ?? TwoAdults()).Add(s => s.DishId, id));

    private BunitNavigationManager Navigation => Services.GetRequiredService<BunitNavigationManager>();

    private static string[] Texts(IRenderedComponent<DishDetailScreen> cut, string selector) => [.. cut.FindAll(selector).Select(e => e.TextContent.Words())];

    // ---- the page and its way in -------------------------------------------------------------------------------------------

    [Fact]
    public void The_page_is_the_route_of_a_dish_and_the_route_is_the_one_the_cards_link_to()
    {
        var route = typeof(DishDetailPage).GetCustomAttribute<RouteAttribute>();

        route.ShouldNotBeNull();
        route.Template.ShouldBe("/" + Routes.DishFolder + "/{DishId}");
        Routes.DishFolder.ShouldBe("mon");
        Routes.Dish("thit-kho-trung").ShouldBe("mon/thit-kho-trung");
    }

    [Fact]
    public void A_dish_id_is_escaped_in_the_address_and_an_empty_one_is_refused()
    {
        Routes.Dish("a b/c").ShouldBe("mon/a%20b%2Fc");
        Should.Throw<ArgumentException>(() => Routes.Dish(string.Empty));
        Should.Throw<ArgumentNullException>(() => Routes.Dish(null!));
    }

    [Fact]
    public void The_page_shows_the_dish_for_the_household_of_the_device_and_otherwise_sends_the_person_to_the_first_screen()
    {
        var store = new FakeHouseholdStore();
        Services.AddSingleton<IHouseholdStore>(store);

        var without = Render<DishDetailPage>(p => p.Add(s => s.DishId, "thit-kho-trung"));

        without.FindAll("article").ShouldBeEmpty();
        Navigation.History.ShouldHaveSingleItem().Uri.ShouldBe(Routes.Household);

        store.Saved = TwoAdults();
        var with = Render<DishDetailPage>(p => p.Add(s => s.DishId, "thit-kho-trung"));

        with.Find("h1").TextContent.ShouldBe("Thịt kho trứng");
    }

    // ---- the head of the page ----------------------------------------------------------------------------------------------

    [Fact]
    public void The_dish_is_named_by_a_heading_with_its_kind_in_the_colour_of_that_kind()
    {
        var cut = Show();

        cut.Find("h1").TextContent.ShouldBe("Thịt kho trứng");
        cut.Find(".detail__slot").TextContent.ShouldBe("Món mặn");
        cut.Find(".detail__slot").ClassList.ShouldContain("slot--main");
        Show("canh-chua-ca-loc").Find(".detail__slot").ClassList.ShouldContain("slot--soup");
        Show("rau-muong-luoc").Find(".detail__slot").ClassList.ShouldContain("slot--veg");
    }

    [Fact]
    public void The_head_tells_how_long_the_whole_dish_takes_and_that_its_energy_is_not_known_when_it_is_not()
    {
        var cut = Show();

        // Steps: 3 + 10, 6 + 15, 8 + 0 and 5 + 40 minutes.
        Texts(cut, ".detail__fact").ShouldBe(["1 giờ 27 phút", "chưa đủ số liệu"]);
        cut.FindAll(".detail__fact svg").ShouldAllBe(s => s.GetAttribute("aria-hidden") == "true");
    }

    [Fact]
    public void The_head_tells_the_energy_of_a_serving_as_an_estimate_when_it_is_known()
    {
        _data.Bundle = Known();

        Texts(Show(), ".detail__fact")[1].ShouldBe("≈ 170 kcal/suất");
    }

    [Fact]
    public void An_energy_of_zero_that_is_known_is_shown_as_zero_and_not_as_unknown()
    {
        var water = Balanced(new NutritionFacts(0, 0, 0, 0, 0, 0)) with { Id = "nuoc-loc" };
        var dish = Seed("rau-muong-luoc") with
        {
            Id = "nuoc-trang",
            Name = "Nước trắng",
            Ingredients = [new DishIngredient("nuoc-loc", 200, ScaleKind.Linear, 1, null)],
            Steps = [],
        };
        _data.Bundle = WithDish(dish, water);

        Texts(Show("nuoc-trang"), ".detail__fact")[1].ShouldBe("≈ 0 kcal/suất");
    }

    [Fact]
    public void A_way_back_to_the_tray_comes_first_and_is_a_target_the_finger_can_hit()
    {
        var link = Show().Find(".back__link");

        link.GetAttribute("href").ShouldBe(Navigation.BaseUri);
        link.TextContent.ShouldBe("Về mâm hôm nay");
        link.QuerySelector("svg")!.GetAttribute("aria-hidden").ShouldBe("true");
    }

    [Theory]
    [InlineData("canh-chua-ca-loc", true, true)]    // chilli that can be left out, and somebody who does not eat it
    [InlineData("canh-chua-ca-loc", false, false)]
    [InlineData("thit-kho-trung", true, false)]     // no chilli in it
    public void The_chilli_that_can_be_left_out_is_said_only_for_a_table_with_somebody_who_does_not_eat_it(string id, bool noSpicy, bool shown)
    {
        var household = TwoAdults(Region.North, noSpicy ? [MemberTag.NoSpicy] : []);

        var cut = Show(id, household);

        cut.FindAll(".detail__aside").Count.ShouldBe(shown ? 1 : 0);
        if (shown)
        {
            cut.Find(".detail__aside").TextContent.ShouldBe("Để ớt riêng");
        }
    }

    [Fact]
    public void A_dish_that_must_be_spicy_does_not_pretend_the_chilli_can_be_left_out()
    {
        _data.Bundle = WithDish(Seed("canh-chua-ca-loc") with { Spicy = SpicyLevel.Required });

        Show("canh-chua-ca-loc", TwoAdults(Region.North, MemberTag.NoSpicy)).FindAll(".detail__aside").ShouldBeEmpty();
    }

    // ---- ingredients -------------------------------------------------------------------------------------------------------

    [Fact]
    public void The_ingredients_are_scaled_to_the_servings_of_the_kind_of_dish_and_say_so()
    {
        Texts(Show(), ".detail__subtitle")[0].ShouldBe("cho ≈ 2 suất");
        Texts(Show(household: TwoAdults(Region.North, MemberTag.MoreProtein)), ".detail__subtitle")[0].ShouldBe("cho ≈ 2,2 suất");   // a main dish follows the protein servings
        Texts(Show("rau-muong-luoc", TwoAdults(Region.North, MemberTag.MoreProtein)), ".detail__subtitle")[0].ShouldBe("cho ≈ 2 suất");   // a vegetable dish does not
        Texts(Show(household: WithChild()), ".detail__subtitle")[0].ShouldBe("cho ≈ 1,6 suất");
    }

    [Fact]
    public void Each_ingredient_has_its_name_and_its_amount_in_grams_or_in_pieces_with_the_grams_under_them()
    {
        var cut = Show(household: TwoAdults(Region.North, MemberTag.MoreProtein));   // 2.2 servings

        var rows = cut.FindAll(".ingredient").ToDictionary(r => r.QuerySelector(".ingredient__name")!.TextContent.Words().Split(" (")[0].Trim(), r => r);

        rows["Thịt ba chỉ"].QuerySelector(".ingredient__amount")!.TextContent.Words().ShouldBe("198 g");          // 90 g × 2.2
        rows["Trứng gà"].QuerySelector(".ingredient__amount")!.FirstChild!.TextContent.Words().Trim().ShouldBe("3 quả");   // 55 g × 2.2 is 2.2 eggs
        rows["Trứng gà"].QuerySelector(".ingredient__grams")!.TextContent.Words().ShouldBe("≈ 165 g");
        rows["Hạt tiêu"].QuerySelector(".ingredient__amount")!.TextContent.Words().ShouldBe("0,9 g");             // a pinch keeps a decimal
        rows["Thịt ba chỉ"].QuerySelectorAll(".ingredient__grams").ShouldBeEmpty();
    }

    [Fact]
    public void The_ingredients_come_in_the_order_of_the_recipe_with_their_notes_beside_them()
    {
        var cut = Show();

        Texts(cut, ".ingredient__name").Select(n => n.Split(" (")[0].Trim()).ShouldBe(["Thịt ba chỉ", "Trứng gà", "Hành tím", "Nước mắm", "Đường", "Hạt tiêu", "Dầu ăn"]);
        Texts(cut, ".ingredient__note").ShouldBe(["(cắt miếng vuông)", "(luộc, bóc vỏ)"]);
        Texts(cut, ".ingredient__name")[0].ShouldBe("Thịt ba chỉ (cắt miếng vuông)");   // a space between the name and the note, not run together
    }

    [Fact]
    public void An_ingredient_is_named_the_way_the_region_of_the_household_names_it()
    {
        Texts(Show(household: TwoAdults(Region.North)), ".ingredient__name")[0].ShouldStartWith("Thịt ba chỉ");
        Texts(Show(household: TwoAdults(Region.South)), ".ingredient__name")[0].ShouldStartWith("Thịt ba rọi");
    }

    // ---- steps -------------------------------------------------------------------------------------------------------------

    [Fact]
    public void The_steps_are_a_numbered_list_with_the_time_that_needs_hands_and_the_time_that_does_not()
    {
        var cut = Show();

        cut.FindAll("ol.steps > li").Count.ShouldBe(4);
        Texts(cut, ".step__text")[0].ShouldBe("Luộc trứng chín, bóc vỏ.");
        Texts(cut, ".step")[0].ShouldContain("Làm 3 phút");
        Texts(cut, ".step")[0].ShouldContain("Chờ 10 phút");
        cut.FindAll(".step")[2].QuerySelectorAll(".step__time").Select(t => t.TextContent.Words()).ShouldBe(["Làm 8 phút"]);   // nothing to wait for, so nothing said
    }

    [Fact]
    public void The_steps_follow_their_order_whatever_the_order_of_the_file()
    {
        var dish = Braise with
        {
            Steps =
            [
                new Step(2, "Thứ hai", 1, 0, []),
                new Step(3, "Thứ ba", 0, 1, []),
                new Step(1, "Thứ nhất", 1, 0, []),
            ],
        };
        _data.Bundle = WithDish(dish);

        Texts(Show(), ".step__text").ShouldBe(["Thứ nhất", "Thứ hai", "Thứ ba"]);
    }

    [Fact]
    public void A_step_that_only_waits_says_only_how_long_to_wait_and_one_that_only_needs_hands_says_only_that()
    {
        _data.Bundle = WithDish(Braise with { Steps = [new Step(1, "Ngâm.", 0, 30, []), new Step(2, "Xào.", 5, 0, [])] });

        var steps = Show().FindAll(".step");

        steps[0].QuerySelectorAll(".step__time").Select(t => t.TextContent.Words()).ShouldBe(["Chờ 30 phút"]);
        steps[1].QuerySelectorAll(".step__time").Select(t => t.TextContent.Words()).ShouldBe(["Làm 5 phút"]);
    }

    [Fact]
    public void A_step_with_no_time_at_all_has_no_time_line()
    {
        _data.Bundle = WithDish(Braise with { Steps = [new Step(1, "Dọn ra bát.", 0, 0, [])] });

        var cut = Show();

        cut.FindAll(".step__times").ShouldBeEmpty();
        Texts(cut, ".detail__fact")[0].ShouldBe("0 phút");
    }

    [Fact]
    public void A_dish_without_steps_says_so_instead_of_showing_an_empty_list()
    {
        _data.Bundle = WithDish(Braise with { Steps = [] });

        var cut = Show();

        cut.FindAll("ol").ShouldBeEmpty();
        cut.Find("#detail-steps").ParentElement!.QuerySelector("p")!.TextContent.ShouldBe("Món này chưa có các bước làm.");
    }

    // ---- nutrition ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void Nutrition_of_one_serving_is_shown_as_estimates_without_the_general_note_that_the_footer_gives()
    {
        _data.Bundle = Known();

        var cut = Show();

        cut.Find(".nutrition__kcal").TextContent.ShouldBe("≈ 170 kcal");
        cut.FindAll(".nutrition__fact dd").Select(d => d.TextContent).ShouldBe(["≈ 17 g", "≈ 8 g", "≈ 14 g", "≈ 2 g", "≈ 80 mg"]);
        cut.FindAll(".nutrition__note").ShouldBeEmpty();
        cut.FindAll(".nutrition__missing").ShouldBeEmpty();
        Texts(cut, ".detail__subtitle")[1].ShouldBe("Một suất");
    }

    [Fact]
    public void Nutrition_that_is_not_known_is_said_to_be_unknown_and_never_shown_as_zero_with_the_ingredients_that_lack_it_named()
    {
        var cut = Show();   // the seed has no confirmed nutrition

        cut.Find(".nutrition__kcal").TextContent.ShouldBe("chưa đủ số liệu");
        cut.FindAll(".nutrition__fact dd").ShouldAllBe(d => d.TextContent == "chưa đủ số liệu");
        cut.Find(".nutrition__missing").TextContent.ShouldStartWith("Chưa có số liệu dinh dưỡng của: Thịt ba chỉ, Trứng gà");
        cut.FindAll(".shares").ShouldBeEmpty();
        Texts(cut, ".detail__section p.text-muted").ShouldContain("Chưa đủ số liệu để chia tỷ lệ đạm, béo và bột đường.");
        cut.FindAll(".member-energy dd").ShouldAllBe(d => d.TextContent == "chưa đủ số liệu");
    }

    [Fact]
    public void A_nutrient_that_is_missing_takes_the_bar_away_but_leaves_the_numbers_that_are_known()
    {
        _data.Bundle = Known(i => i.Id == "thit-ba-chi" ? i with { NutritionPer100gEdible = i.NutritionPer100gEdible with { FatG = null } } : i);

        var cut = Show();

        cut.Find(".nutrition__kcal").TextContent.ShouldBe("≈ 170 kcal");
        cut.FindAll(".nutrition__fact dd")[1].TextContent.ShouldBe("chưa đủ số liệu");
        cut.FindAll(".shares").ShouldBeEmpty();
        cut.Find(".nutrition__missing").TextContent.ShouldContain("Thịt ba chỉ");
    }

    [Fact]
    public void The_bar_shows_protein_fat_and_carbohydrate_in_that_order_and_each_row_says_how_it_stands_against_the_recommendation()
    {
        _data.Bundle = Known();

        var cut = Show();

        // 16.95 g protein, 8.475 g fat and 13.56 g carbohydrate make 67.8, 76.3 and 54.2 kcal of 198.3.
        cut.Find(".shares__bar").GetAttribute("aria-label")!.Plain().ShouldBe("Đạm ≈ 34%, béo ≈ 38%, bột đường ≈ 27%");
        cut.FindAll(".shares__segment").Select(s => s.ClassList.Single(c => c.StartsWith("shares__segment--", StringComparison.Ordinal))).ShouldBe(["shares__segment--protein", "shares__segment--fat", "shares__segment--carb"]);
        cut.FindAll(".shares__segment").Select(s => s.GetAttribute("style")).ShouldBe(["flex-grow: 0.3419", "flex-grow: 0.3846", "flex-grow: 0.2735"]);

        var rows = cut.FindAll(".shares__row");
        rows.Select(r => r.QuerySelector(".shares__name")!.TextContent).ShouldBe(["Đạm", "Béo", "Bột đường"]);
        rows.Select(r => r.QuerySelector(".shares__value")!.TextContent.Words()).ShouldBe(["≈ 34%", "≈ 38%", "≈ 27%"]);
        rows.Select(r => r.QuerySelector(".shares__range")!.TextContent.Words().Replace("  ", " ").Trim()).ShouldBe(["khuyến nghị 13–20% cao hơn", "khuyến nghị 20–25% cao hơn", "khuyến nghị 55–65% thấp hơn"]);
        rows.Select(r => r.QuerySelector(".badge")!.ClassList.Contains("badge--warning")).ShouldAllBe(w => w);
        rows.SelectMany(r => r.QuerySelectorAll(".shares__swatch")).ShouldAllBe(s => s.GetAttribute("aria-hidden") == "true");
    }

    [Fact]
    public void A_share_inside_its_recommended_range_is_said_to_be_on_target_in_the_calm_colour()
    {
        // Test-only numbers: 450 kcal per 100 g, 18 g protein, 11 g fat, 69.75 g carbohydrate: 16%, 22% and 62% of the energy.
        var balanced = Balanced(new NutritionFacts(450, 18, 11, 69.75, 0, 0));
        var dish = Seed("rau-muong-luoc") with
        {
            Id = "mon-can-bang",
            Name = "Món cân bằng",
            Ingredients = [new DishIngredient("can-bang", 100, ScaleKind.Linear, 1, null)],
            Steps = [],
        };
        _data.Bundle = WithDish(dish, balanced);

        var cut = Show("mon-can-bang");

        cut.FindAll(".shares__row .shares__value").Select(v => v.TextContent.Words()).ShouldBe(["≈ 16%", "≈ 22%", "≈ 62%"]);
        cut.FindAll(".shares__row .badge").Select(b => b.TextContent).ShouldBe(["vừa mức", "vừa mức", "vừa mức"]);
        cut.FindAll(".shares__row .badge").ShouldAllBe(b => b.ClassList.Contains("badge--ok"));
    }

    [Fact]
    public void Each_member_gets_the_energy_of_their_servings_of_this_dish_without_the_rice()
    {
        _data.Bundle = Known();

        var cut = Show(household: WithChild());

        Texts(cut, ".detail__subheading").ShouldBe(["Mỗi người ăn từ món này"]);
        Texts(cut, ".detail__note").ShouldBe(["Năng lượng của riêng món này, chưa tính cơm."]);
        var rows = cut.FindAll(".member-energy__row");
        rows.Select(r => r.QuerySelector("dt")!.TextContent.Words().Trim()).ShouldBe(["Bố 1 suất", "Con 0,6 suất"]);
        rows.Select(r => r.QuerySelector("dd")!.TextContent.Words()).ShouldBe(["≈ 170 kcal", "≈ 100 kcal"]);   // 169.5 × 1 and 169.5 × 0.595
    }

    [Fact]
    public void A_household_without_members_has_no_list_of_people()
    {
        var cut = Show(household: new Household(Region.North, BowlSize.Medium, []));

        cut.FindAll(".member-energy").ShouldBeEmpty();
        cut.FindAll(".detail__subheading").ShouldBeEmpty();
    }

    [Fact]
    public void The_footer_says_the_numbers_are_estimates_and_not_medical_advice()
    {
        Show().Find(".detail__footer").TextContent.ShouldBe("Số liệu ước tính, không thay tư vấn y tế.");
    }

    // ---- the structure -----------------------------------------------------------------------------------------------------

    [Fact]
    public void The_three_parts_stand_one_under_the_other_each_named_by_its_heading()
    {
        var cut = Show();

        var sections = cut.FindAll("section.detail__section");
        sections.Count.ShouldBe(3);
        sections.Select(s => s.QuerySelector("h2")!.TextContent).ShouldBe(["Nguyên liệu", "Cách làm", "Dinh dưỡng"]);
        sections.ShouldAllBe(s => s.GetAttribute("aria-labelledby") == s.QuerySelector("h2")!.Id);
        cut.Find("article").Children.Select(c => c.TagName).ShouldBe(["DIV", "HEADER", "SECTION", "SECTION", "SECTION", "SECTION", "P"]);   // the fourth is "Món này với nhà mình"
    }

    [Fact]
    public void A_dish_without_a_picture_has_the_tile_of_its_kind_with_its_words_and_one_with_a_large_file_has_the_large_file()
    {
        var plain = Show();

        plain.Find("article > .tile").ClassList.ShouldNotContain("tile--compact");
        plain.Find("article > .tile .tile__label").ShouldNotBeNull();

        _data.Bundle = WithDish(Braise with { Image = new ImageRef("tkt-200.webp", "tkt-800.webp", "tự chụp", "CC0", true) });
        var pictured = Show();

        pictured.Find("article > .picture .picture__image").GetAttribute("src").ShouldBe("images/tkt-800.webp");
        pictured.Find(".picture__badge").TextContent.ShouldBe("Ảnh minh hoạ");
        pictured.FindAll(".tile").ShouldBeEmpty();
    }

    // ---- when the data is not there, or changes ----------------------------------------------------------------------------

    [Fact]
    public void A_dish_that_is_not_in_the_list_gets_a_page_that_says_so_and_leads_back()
    {
        var cut = Show("khong-co");

        cut.Find("h1").TextContent.ShouldBe("Không tìm thấy món");
        cut.Find("p").TextContent.ShouldBe("Món này không còn trong danh sách. Có thể dữ liệu vừa được cập nhật.");
        cut.Find(".back__link").GetAttribute("href").ShouldBe(Navigation.BaseUri);
        cut.FindAll("article").ShouldBeEmpty();
    }

    [Fact]
    public void Without_the_data_there_is_nothing_but_the_notices_that_the_layout_shows_and_it_is_not_taken_for_a_missing_dish()
    {
        _data.Bundle = null;
        _data.Status = BundleLoadStatus.NeedsNetwork;

        var cut = Show();

        cut.FindAll("h1, article").ShouldBeEmpty();
        cut.Markup.ShouldNotContain("Không tìm thấy món");
        _data.Initialized.ShouldBe(1);
    }

    [Fact]
    public void The_data_arriving_after_the_page_draws_it()
    {
        _data.Bundle = null;
        var cut = Show();
        cut.FindAll("article").ShouldBeEmpty();

        _data.Bundle = TestConfig.SeedBundle;
        _data.Raise();

        cut.WaitForAssertion(() => cut.Find("h1").TextContent.ShouldBe("Thịt kho trứng"));
    }

    [Fact]
    public void A_newer_bundle_changes_what_the_page_says_and_a_dish_that_went_away_gives_the_notice()
    {
        var cut = Show();

        _data.Bundle = WithDish(Braise with { Name = "Thịt kho tàu" });
        _data.Raise();
        cut.WaitForAssertion(() => cut.Find("h1").TextContent.ShouldBe("Thịt kho tàu"));

        _data.Bundle = TestConfig.SeedBundle with { Dishes = [.. TestConfig.SeedBundle.Dishes.Where(d => d.Id != "thit-kho-trung")] };
        _data.Raise();
        cut.WaitForAssertion(() => cut.Find("h1").TextContent.ShouldBe("Không tìm thấy món"));
    }

    [Fact]
    public void Another_dish_or_another_household_is_worked_out_again()
    {
        var cut = Show();

        cut.Render(p => p.Add(s => s.Household, TwoAdults()).Add(s => s.DishId, "canh-chua-ca-loc"));
        cut.Find("h1").TextContent.ShouldBe(Seed("canh-chua-ca-loc").Name);

        cut.Render(p => p.Add(s => s.Household, WithChild()).Add(s => s.DishId, "canh-chua-ca-loc"));
        Texts(cut, ".detail__subtitle")[0].ShouldBe("cho ≈ 1,6 suất");
    }

    [Fact]
    public async Task The_page_stops_listening_for_new_data_when_it_goes_away()
    {
        Show();
        _data.Listeners.ShouldBe(1);

        await DisposeComponentsAsync();

        _data.Listeners.ShouldBe(0);
    }

    private static Ingredient Balanced(NutritionFacts facts) => TestConfig.SeedBundle.Ingredients.First() with
    {
        Id = "can-bang",
        Names = new Dictionary<string, string> { [RegionKeys.Default] = "Món thử" },
        NutritionPer100gEdible = facts,
    };

    // ---- blocking a dish ---------------------------------------------------------------------------------------------------

    private static IElement TasteSection(IRenderedComponent<DishDetailScreen> cut) => cut.Find("section[aria-labelledby=detail-taste]");

    private static string[] ButtonsOf(IElement section) => [.. section.QuerySelectorAll("button").Select(b => b.TextContent.Words())];

    [Fact]
    public void A_dish_that_can_be_drawn_says_what_blocking_does_and_offers_to_block_it_after_the_three_parts()
    {
        var cut = Show();
        var section = TasteSection(cut);

        section.QuerySelector("h2")!.TextContent.ShouldBe("Món này với nhà mình");
        section.GetAttribute("aria-labelledby").ShouldBe(section.QuerySelector("h2")!.Id);
        section.QuerySelector("[role=status]")!.TextContent.Words().ShouldBe("Chặn thì món này không ra ở lần quay nào nữa. Bỏ chặn được ở Cài đặt.");
        ButtonsOf(section).ShouldBe(["Không bao giờ quay món này"]);
        section.PreviousElementSibling!.GetAttribute("aria-labelledby").ShouldBe("detail-nutrition");
        section.NextElementSibling!.ClassList.ShouldContain("detail__footer");
    }

    [Fact]
    public async Task Blocking_keeps_the_dish_among_the_blocked_ones_and_the_section_then_offers_to_bring_it_back()
    {
        var cut = Show();

        await TasteSection(cut).QuerySelector("button")!.ClickAsync(new());

        _taste.Saved.BlockedDishIds.ShouldBe(["thit-kho-trung"]);
        _taste.Saves.ShouldBe(1);
        TasteSection(cut).QuerySelector("[role=status]")!.TextContent.Words().ShouldBe("Món này đang bị chặn: sẽ không được quay.");
        ButtonsOf(TasteSection(cut)).ShouldBe(["Cho quay lại"]);
    }

    [Fact]
    public async Task Blocking_leaves_what_was_said_about_other_dishes_and_the_dishes_blocked_before()
    {
        _taste.Saved = new TasteProfile(new Dictionary<string, DishPreference> { ["canh-chua-ca-loc"] = DishPreference.Liked }, ["bap-cai-luoc"]);
        var cut = Show();

        await TasteSection(cut).QuerySelector("button")!.ClickAsync(new());

        _taste.Saved.BlockedDishIds.ShouldBe(["bap-cai-luoc", "thit-kho-trung"]);
        _taste.Saved.Preferences["canh-chua-ca-loc"].ShouldBe(DishPreference.Liked);
    }

    [Fact]
    public void A_dish_that_was_blocked_before_says_so_when_the_page_opens()
    {
        _taste.Saved = new TasteProfile(new Dictionary<string, DishPreference>(), ["thit-kho-trung"]);

        var section = TasteSection(Show());

        section.QuerySelector("[role=status]")!.TextContent.Words().ShouldBe("Món này đang bị chặn: sẽ không được quay.");
        ButtonsOf(section).ShouldBe(["Cho quay lại"]);
    }

    [Fact]
    public async Task Bringing_a_dish_back_takes_only_that_dish_off_the_blocked_ones()
    {
        _taste.Saved = new TasteProfile(new Dictionary<string, DishPreference>(), ["bap-cai-luoc", "thit-kho-trung"]);
        var cut = Show();

        await TasteSection(cut).QuerySelector("button")!.ClickAsync(new());

        _taste.Saved.BlockedDishIds.ShouldBe(["bap-cai-luoc"]);
        ButtonsOf(TasteSection(cut)).ShouldBe(["Không bao giờ quay món này"]);
    }

    [Fact]
    public void Another_dish_being_blocked_does_not_make_this_one_blocked()
    {
        _taste.Saved = new TasteProfile(new Dictionary<string, DishPreference>(), ["bap-cai-luoc"]);

        ButtonsOf(TasteSection(Show())).ShouldBe(["Không bao giờ quay món này"]);
    }

    [Fact]
    public void A_dish_that_is_not_there_has_no_such_section()
    {
        Show("khong-co-mon-nay").FindAll("section").ShouldBeEmpty();
    }
}
