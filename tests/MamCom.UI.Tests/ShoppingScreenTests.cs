using System.Reflection;
using AngleSharp.Dom;
using Bunit.TestDoubles;
using MamCom.Core;
using MamCom.Core.Data;
using MamCom.Core.Models;
using MamCom.Core.Shopping;
using MamCom.Core.Spinning;
using MamCom.Data;
using MamCom.Data.Bundles;
using MamCom.Data.Device;
using MamCom.Data.State;
using MamCom.UI.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace MamCom.UI.Tests;

public class ShoppingScreenTests : BunitContext
{
    private static readonly DateOnly Today = new(2026, 10, 20);   // a Tuesday

    private readonly FakeAppData _data = new() { Bundle = Bundle() };
    private readonly FakeTrayStore _trays = new();
    private readonly FakeShoppingStore _shopping = new();
    private readonly FakeDeviceFeatures _device = new();

    public ShoppingScreenTests()
    {
        Services.AddSingleton<IAppData>(_data);
        Services.AddSingleton<ITrayStore>(_trays);
        Services.AddSingleton<IShoppingStore>(_shopping);
        Services.AddSingleton<IDeviceFeatures>(_device);
        Services.AddSingleton<IClock>(new FixedClock(Today));
        Services.AddSingleton<IRandomSource>(new SeededRandomSource(5));
        _trays.Saved = SessionOf("mon-di-cho");
    }

    // ---- test-only fixture: two adults make 2.0 servings, so the dish below buys exactly what the tests say -------------------

    private static Ingredient Item(string id, string name, Aisle aisle, MarketUnit unit, string category, double waste = 0, bool pantry = false, ImageRef? image = null) => new(
        Id: id,
        Names: new Dictionary<string, string> { [RegionKeys.Default] = name },
        Aliases: [],
        Category: category,
        Aisle: aisle,
        ProteinSource: null,
        MarketUnit: new Dictionary<string, MarketUnit> { [RegionKeys.Default] = unit },
        WasteRatio: waste,
        IsPantry: pantry,
        NutritionPer100gEdible: new NutritionFacts(null, null, null, null, null, null),
        NutritionSource: new NutritionSource(NutritionDbs.Placeholder, null, null, null, null),
        Image: image ?? new ImageRef(null, null, null, null, false));

    private static DishIngredient Line(string id, double gramsPerServing, ScaleKind scale = ScaleKind.Linear) => new(id, gramsPerServing, scale, 1, null);

    private static Dish ShoppingDish(string id, params DishIngredient[] lines) => TestConfig.SeedBundle.Dishes.First() with
    {
        Id = id,
        Name = id,
        Slot = DishSlot.Main,
        Ingredients = lines,
        Steps = [],
    };

    // 200 g pork is 2 lạng; 200 g of fish with 30% waste is 285.7 g, one fish of 500 g; 160 g greens are 2 bunches; 100 g of egg is 2 eggs of 55 g; 4 g of salt are 5 g;
    // the seed rice is 160 g raw, half a kilo. Salt and rice are things a kitchen has.
    private static DataBundle Bundle() => TestConfig.SeedBundle with
    {
        Ingredients =
        [
            .. TestConfig.SeedBundle.Ingredients.Where(i => i.Id is not ("thit" or "ca" or "rau" or "trung" or "muoi")),   // the seed has some of these ids; the ones here are the ones the tests count
            Item("thit", "Thịt", Aisle.MeatAndFish, new MarketUnit("lạng", 100, 0.5), "thit-heo"),
            Item("ca", "Cá", Aisle.MeatAndFish, new MarketUnit("con", 500, 1, ShowApproxGrams: true), "ca", waste: 0.3),
            Item("rau", "Rau", Aisle.Produce, new MarketUnit("mớ", 150, 1), "rau-cu"),
            Item("trung", "Trứng", Aisle.EggsAndTofu, new MarketUnit("quả", 55, 1), "trung"),
            Item("muoi", "Muối", Aisle.DryGoodsAndSpices, new MarketUnit("g", 1, 5), "gia-vi", pantry: true),
        ],
        Dishes =
        [
            .. TestConfig.SeedBundle.Dishes,
            ShoppingDish("mon-di-cho", Line("thit", 100), Line("ca", 100), Line("rau", 80), Line("trung", 50, ScaleKind.Integer), Line("muoi", 2, ScaleKind.Fixed)),
            ShoppingDish("chi-muoi", Line("muoi", 2, ScaleKind.Fixed)),
        ],
    };

    private static Household TwoAdults() => new(Region.North, BowlSize.Medium, [new Member("Bố", AgeGroup.Adult, 2, [], []), new Member("Mẹ", AgeGroup.Adult, 2, [], [])]);

    private static TraySession SessionOf(string dishId, DateOnly? date = null) => new(
        new SpinHistory([new SpinResult(new MealTray(date ?? Today, [new SlotResult(DishSlot.Main, dishId, false)]), [], [])]),
        0,
        1,
        QuickFilters.None,
        null);

    private static string TrayKey(string dishId) => $"{Today:yyyy-MM-dd}|{dishId}";

    private BunitNavigationManager Navigation => Services.GetRequiredService<BunitNavigationManager>();

    private IRenderedComponent<ShoppingScreen> Show(Household? household = null) => Render<ShoppingScreen>(p => p.Add(s => s.Household, household ?? TwoAdults()));

    private static string[] Texts(IRenderedComponent<ShoppingScreen> cut, string selector) => [.. cut.FindAll(selector).Select(e => e.TextContent.Words())];

    private static IElement Row(IRenderedComponent<ShoppingScreen> cut, string name) =>
        cut.FindAll(".shop-item").Single(r => r.QuerySelector(".shop-item__name")!.TextContent.Trim() == name);

    private static IElement ShareButton(IRenderedComponent<ShoppingScreen> cut) => cut.Find(".app-actions button");

    // ---- the page and its way in -------------------------------------------------------------------------------------------

    [Fact]
    public void The_page_is_the_route_of_the_shopping_list_and_the_route_is_the_one_the_app_links_to()
    {
        var route = typeof(ShoppingPage).GetCustomAttribute<RouteAttribute>();

        route.ShouldNotBeNull();
        route.Template.ShouldBe("/" + Routes.Shopping);
    }

    [Fact]
    public void The_page_shows_the_list_for_the_household_of_the_device_and_otherwise_sends_the_person_to_the_first_screen()
    {
        var store = new FakeHouseholdStore();
        Services.AddSingleton<IHouseholdStore>(store);

        var without = Render<ShoppingPage>();

        without.FindAll("h1").ShouldBeEmpty();
        Navigation.History.ShouldHaveSingleItem().Uri.ShouldBe(Routes.Household);

        store.Saved = TwoAdults();
        Render<ShoppingPage>().Find("h1").TextContent.ShouldBe("Đi chợ");
    }

    [Fact]
    public void A_way_back_to_the_tray_comes_first()
    {
        var link = Show().Find(".back__link");

        link.GetAttribute("href").ShouldBe(Navigation.BaseUri);
        link.TextContent.ShouldBe("Về mâm hôm nay");
    }

    // ---- the list ----------------------------------------------------------------------------------------------------------

    [Fact]
    public void The_list_is_that_of_the_tray_on_the_screen_by_aisle_with_the_pantry_left_out()
    {
        var cut = Show();

        cut.Find("h1").TextContent.ShouldBe("Đi chợ");
        Texts(cut, ".shop-group h2").ShouldBe(["Thịt cá", "Rau củ quả", "Trứng đậu phụ"]);
        Texts(cut, ".shop-item__text").ShouldBe(["2 lạng Thịt", "1 con Cá (≈ 500 g)", "2 mớ Rau", "2 quả Trứng"]);
        cut.FindAll(".shop-group").ShouldAllBe(g => g.GetAttribute("aria-labelledby") == g.QuerySelector("h2")!.Id);
    }

    [Fact]
    public void The_head_says_whose_meal_it_is_and_how_far_along_the_shopping_is()
    {
        var cut = Show();

        Texts(cut, "p.text-muted").ShouldContain("Mâm hôm nay, cho 2 người.");
        cut.Find(".shop-progress").TextContent.ShouldBe("Đã mua 0/4");
        cut.Find(".shop-progress").GetAttribute("role").ShouldBe("status");
    }

    [Fact]
    public void An_amount_is_in_the_unit_of_the_market_and_what_varies_in_size_has_its_grams_beside_it()
    {
        var cut = Show();

        Row(cut, "Cá").QuerySelector(".shop-item__amount")!.TextContent.Words().ShouldBe("1 con");
        Row(cut, "Cá").QuerySelector(".shop-item__approx")!.TextContent.Words().ShouldBe("(≈ 500 g)");
        Row(cut, "Thịt").QuerySelectorAll(".shop-item__approx").ShouldBeEmpty();
    }

    [Fact]
    public void A_row_is_a_label_around_a_tick_a_thumbnail_and_the_words_so_the_whole_row_is_the_target()
    {
        var cut = Show();

        var row = Row(cut, "Thịt");

        row.QuerySelector("label.shop-item__label input[type=checkbox]").ShouldNotBeNull();
        row.QuerySelector("label .tile").ShouldNotBeNull();
        row.QuerySelector("label .shop-item__text")!.TextContent.Words().ShouldBe("2 lạng Thịt");
    }

    [Fact]
    public void An_ingredient_without_a_picture_has_the_tile_of_its_kind_and_one_with_a_thumb_has_the_thumb()
    {
        var withThumb = TestConfig.SeedBundle.Ingredients.First(i => i.Id == "gao") with { Image = new ImageRef("gao-200.webp", null, "tự chụp", "CC0", false) };
        _data.Bundle = Bundle() with { Ingredients = [.. Bundle().Ingredients.Where(i => i.Id != "gao"), withThumb] };
        _shopping.Saved = new ShoppingState(TrayKey("mon-di-cho"), [], true);

        var cut = Show();

        Row(cut, "Gạo").QuerySelector("img.picture__image")!.GetAttribute("src").ShouldBe(ThumbPreloader.ThumbUrl("gao-200.webp"));
        Row(cut, "Thịt").QuerySelectorAll("img").ShouldBeEmpty();
    }

    // ---- the things a kitchen always has -----------------------------------------------------------------------------------

    [Fact]
    public void The_switch_shows_the_pantry_items_marked_as_such_and_keeps_the_choice()
    {
        var cut = Show();
        var chip = cut.Find(".chip");
        chip.TextContent.ShouldBe("Hiện đồ có sẵn");
        chip.GetAttribute("aria-pressed").ShouldBe("false");

        chip.Click();

        cut.Find(".chip").GetAttribute("aria-pressed").ShouldBe("true");
        Texts(cut, ".shop-group h2").ShouldBe(["Thịt cá", "Rau củ quả", "Trứng đậu phụ", "Khô và gia vị"]);
        Texts(cut, ".shop-group:last-child .shop-item__text").ShouldBe(["0,5 kg Gạo Có sẵn", "5 g Muối Có sẵn"]);   // the rice is used first
        cut.Find(".shop-progress").TextContent.ShouldBe("Đã mua 0/6");
        _shopping.Saved!.ShowPantry.ShouldBeTrue();

        cut.Find(".chip").Click();

        cut.Find(".chip").GetAttribute("aria-pressed").ShouldBe("false");
        cut.FindAll(".shop-group").Count.ShouldBe(3);
        _shopping.Saved!.ShowPantry.ShouldBeFalse();
    }

    [Fact]
    public void The_choice_of_the_pantry_switch_is_there_in_the_next_visit()
    {
        _shopping.Saved = new ShoppingState(TrayKey("mon-di-cho"), [], true);

        var cut = Show();

        cut.Find(".chip").GetAttribute("aria-pressed").ShouldBe("true");
        cut.FindAll(".shop-group").Count.ShouldBe(4);
    }

    [Fact]
    public void When_everything_is_in_the_pantry_it_says_nothing_is_needed_and_there_is_nothing_to_send()
    {
        _trays.Saved = SessionOf("chi-muoi");
        _shopping.Saved = null;
        var onlyPantry = Bundle() with { Ingredients = [.. Bundle().Ingredients.Select(i => i.Id == "gao" ? i with { IsPantry = true } : i)] };
        _data.Bundle = onlyPantry;
        var household = new Household(Region.North, BowlSize.Medium, [new Member("Bố", AgeGroup.Adult, 2, [], [])]);

        var cut = Show(household);

        cut.FindAll(".shop-group").ShouldBeEmpty();
        cut.Find("p:not(.text-muted):not(.shop-progress)").TextContent.ShouldBe("Không cần mua gì thêm: mọi thứ bếp nhà đã có.");
        cut.Find(".shop-progress").TextContent.ShouldBe("Đã mua 0/0");
        ShareButton(cut).HasAttribute("disabled").ShouldBeTrue();
    }

    // ---- ticking -----------------------------------------------------------------------------------------------------------

    [Fact]
    public void Ticking_an_item_strikes_it_through_counts_it_and_keeps_it_for_next_time()
    {
        var cut = Show();

        Row(cut, "Cá").QuerySelector("input")!.Change(true);

        Row(cut, "Cá").ClassList.ShouldContain("shop-item--done");
        Row(cut, "Thịt").ClassList.ShouldNotContain("shop-item--done");
        cut.Find(".shop-progress").TextContent.ShouldBe("Đã mua 1/4");
        _shopping.Saved!.CheckedIngredientIds.ShouldBe(["ca"]);
        _shopping.Saved.TrayKey.ShouldBe(TrayKey("mon-di-cho"));
        Row(cut, "Cá").QuerySelector("input")!.HasAttribute("checked").ShouldBeTrue();
    }

    [Fact]
    public void Ticking_again_takes_the_tick_off()
    {
        var cut = Show();
        Row(cut, "Cá").QuerySelector("input")!.Change(true);

        Row(cut, "Cá").QuerySelector("input")!.Change(false);

        Row(cut, "Cá").ClassList.ShouldNotContain("shop-item--done");
        cut.Find(".shop-progress").TextContent.ShouldBe("Đã mua 0/4");
        _shopping.Saved!.CheckedIngredientIds.ShouldBeEmpty();
        _shopping.Saves.ShouldBe(2);
    }

    [Fact]
    public void An_item_stays_where_it_is_when_it_is_ticked()
    {
        var cut = Show();
        var before = Texts(cut, ".shop-item__name");

        Row(cut, "Thịt").QuerySelector("input")!.Change(true);

        Texts(cut, ".shop-item__name").ShouldBe(before);
    }

    [Fact]
    public void The_ticks_that_were_made_are_there_again_in_a_new_visit()
    {
        _shopping.Saved = new ShoppingState(TrayKey("mon-di-cho"), ["thit", "trung"], false);

        var cut = Show();

        Row(cut, "Thịt").ClassList.ShouldContain("shop-item--done");
        Row(cut, "Trứng").ClassList.ShouldContain("shop-item--done");
        Row(cut, "Cá").ClassList.ShouldNotContain("shop-item--done");
        cut.Find(".shop-progress").TextContent.ShouldBe("Đã mua 2/4");
    }

    [Fact]
    public void The_ticks_of_another_tray_are_not_shown()
    {
        _shopping.Saved = new ShoppingState(TrayKey("chi-muoi"), ["thit"], false);

        var cut = Show();

        cut.FindAll(".shop-item--done").ShouldBeEmpty();
    }

    [Fact]
    public void A_hidden_pantry_item_that_was_ticked_does_not_count_until_it_is_shown()
    {
        _shopping.Saved = new ShoppingState(TrayKey("mon-di-cho"), ["gao"], false);

        var cut = Show();
        cut.Find(".shop-progress").TextContent.ShouldBe("Đã mua 0/4");

        cut.Find(".chip").Click();

        cut.Find(".shop-progress").TextContent.ShouldBe("Đã mua 1/6");
        Row(cut, "Gạo").ClassList.ShouldContain("shop-item--done");
    }

    // ---- sending the list --------------------------------------------------------------------------------------------------

    [Fact]
    public void The_button_sends_the_text_of_the_list_to_the_share_sheet()
    {
        var cut = Show();

        ShareButton(cut).TextContent.Trim().ShouldBe("Gửi Zalo");
        ShareButton(cut).Click();

        _device.Shared.ShouldBe(
        [
            "Đi chợ tối T3 20/10 (2 người)\n"
            + "THỊT CÁ: 2 lạng thịt · 1 con cá (~500g)\n"
            + "RAU: 2 mớ rau\n"
            + "KHÁC: 2 quả trứng",
        ]);
    }

    [Fact]
    public void The_pantry_is_in_the_text_when_it_is_on_the_screen_and_ticks_do_not_change_it()
    {
        var cut = Show();
        cut.Find(".chip").Click();
        Row(cut, "Thịt").QuerySelector("input")!.Change(true);

        ShareButton(cut).Click();

        _device.Shared.Single().ShouldEndWith("KHÁC: 2 quả trứng · 0,5 kg gạo · 5 g muối");
        _device.Shared.Single().ShouldContain("2 lạng thịt");
    }

    [Fact]
    public void A_text_that_was_shared_or_cancelled_says_nothing_more()
    {
        var cut = Show();

        ShareButton(cut).Click();
        _device.ShareResult = ShareOutcome.Cancelled;
        ShareButton(cut).Click();

        cut.FindAll(".app-actions__status").ShouldBeEmpty();
        _device.Shared.Count.ShouldBe(2);
    }

    [Fact]
    public void A_text_that_was_copied_says_so_and_what_to_do_next()
    {
        _device.ShareResult = ShareOutcome.Copied;
        var cut = Show();

        ShareButton(cut).Click();

        var notice = cut.Find(".app-actions .app-actions__status");
        notice.TextContent.ShouldBe("Đã chép danh sách. Mở Zalo và dán vào để gửi.");
        notice.ClassList.ShouldContain("notice--ok");
        notice.GetAttribute("role").ShouldBe("status");
    }

    [Fact]
    public void A_text_that_could_not_be_sent_says_so()
    {
        _device.ShareResult = ShareOutcome.Failed;
        var cut = Show();

        ShareButton(cut).Click();

        var notice = cut.Find(".app-actions__status");
        notice.TextContent.ShouldBe("Chưa gửi được danh sách. Bạn thử lại nhé.");
        notice.ClassList.ShouldContain("notice--danger");
    }

    [Fact]
    public void The_notice_goes_when_the_list_is_sent_again()
    {
        _device.ShareResult = ShareOutcome.Failed;
        var cut = Show();
        ShareButton(cut).Click();

        _device.ShareResult = ShareOutcome.Shared;
        ShareButton(cut).Click();

        cut.FindAll(".app-actions__status").ShouldBeEmpty();
    }

    [Fact]
    public async Task The_old_notice_is_gone_as_soon_as_the_list_is_sent_again_not_when_the_answer_comes()
    {
        _device.ShareResult = ShareOutcome.Failed;
        var cut = Show();
        ShareButton(cut).Click();
        cut.FindAll(".app-actions__status").Count.ShouldBe(1);

        _device.ShareGate = new TaskCompletionSource();
        var second = ShareButton(cut).ClickAsync(new());

        cut.FindAll(".app-actions__status").ShouldBeEmpty();

        _device.ShareGate.SetResult();
        await second.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);
        cut.FindAll(".app-actions__status").Count.ShouldBe(1);   // it failed again, and says so again
    }

    [Fact]
    public async Task A_second_tap_while_the_first_is_being_sent_does_nothing_and_the_button_says_it_is_busy()
    {
        _device.ShareGate = new TaskCompletionSource();
        var cut = Show();

        var first = ShareButton(cut).ClickAsync(new());
        ShareButton(cut).HasAttribute("disabled").ShouldBeTrue();
        ShareButton(cut).Click();   // a tap on a disabled button still reaches the handler in bUnit
        _device.Shared.Count.ShouldBe(1);

        _device.ShareGate.SetResult();
        await first.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);

        ShareButton(cut).HasAttribute("disabled").ShouldBeFalse();
    }

    // ---- when there is nothing to shop for ---------------------------------------------------------------------------------

    [Fact]
    public void Without_a_tray_the_page_says_so_and_has_nothing_to_send()
    {
        _trays.Saved = null;

        var cut = Show();

        cut.Find("h1").TextContent.ShouldBe("Đi chợ");
        cut.Find("p").TextContent.ShouldBe("Chưa có mâm nào để đi chợ. Hãy quay và chốt một mâm trước.");
        cut.FindAll(".shop-group, .app-actions, .chip").ShouldBeEmpty();
        cut.Find(".back__link").ShouldNotBeNull();
    }

    [Fact]
    public void A_tray_of_yesterday_is_not_shopped_for()
    {
        _trays.Saved = SessionOf("mon-di-cho", Today.AddDays(-1));

        Show().FindAll(".shop-group").ShouldBeEmpty();
    }

    [Fact]
    public void A_tray_with_a_dish_that_is_gone_is_not_shopped_for()
    {
        _trays.Saved = SessionOf("mon-da-bo");

        Show().FindAll(".shop-group").ShouldBeEmpty();
    }

    [Fact]
    public void A_tray_with_no_dish_is_not_shopped_for()
    {
        _trays.Saved = new TraySession(new SpinHistory([new SpinResult(new MealTray(Today, []), [], [])]), 0, 1, QuickFilters.None, null);

        var cut = Show();

        // Only the rice would be left to list, and that is a pantry item: the page must say there is no tray, not "nothing to buy".
        cut.Find("p").TextContent.ShouldBe("Chưa có mâm nào để đi chợ. Hãy quay và chốt một mâm trước.");
        cut.FindAll(".shop-group, .app-actions, .chip").ShouldBeEmpty();
    }

    [Fact]
    public void The_list_is_for_the_tray_that_is_shown_not_the_newest_one()
    {
        var newest = new SpinResult(new MealTray(Today, [new SlotResult(DishSlot.Main, "chi-muoi", false)]), [], []);
        var older = new SpinResult(new MealTray(Today, [new SlotResult(DishSlot.Main, "mon-di-cho", false)]), [], []);
        _trays.Saved = new TraySession(new SpinHistory([newest, older]), 1, 2, QuickFilters.None, null);

        Texts(Show(), ".shop-item__name").ShouldBe(["Thịt", "Cá", "Rau", "Trứng"]);
    }

    // ---- when the data is not there, or changes ----------------------------------------------------------------------------

    [Fact]
    public void Without_the_data_there_is_nothing_but_the_notices_that_the_layout_shows()
    {
        _data.Bundle = null;
        _data.Status = BundleLoadStatus.NeedsNetwork;

        var cut = Show();

        cut.FindAll("h1, .shop-group, .app-actions").ShouldBeEmpty();
        _data.Initialized.ShouldBe(1);
    }

    [Fact]
    public void The_data_arriving_after_the_page_draws_the_list()
    {
        _data.Bundle = null;
        var cut = Show();
        cut.FindAll("h1").ShouldBeEmpty();

        _data.Bundle = Bundle();
        _data.Raise();

        cut.WaitForAssertion(() => cut.FindAll(".shop-group").Count.ShouldBe(3));
    }

    [Fact]
    public void A_newer_bundle_changes_what_the_list_says()
    {
        var cut = Show();

        _data.Bundle = Bundle() with { Ingredients = [.. Bundle().Ingredients.Select(i => i.Id == "thit" ? i with { Names = new Dictionary<string, string> { [RegionKeys.Default] = "Thịt nạc" } } : i)] };
        _data.Raise();

        cut.WaitForAssertion(() => Texts(cut, ".shop-item__name").ShouldContain("Thịt nạc"));
    }

    [Fact]
    public async Task A_newer_bundle_that_arrives_while_the_list_is_being_sent_does_not_pull_the_list_from_under_the_send()
    {
        _device.ShareGate = new TaskCompletionSource();
        var cut = Show();
        var sending = ShareButton(cut).ClickAsync(new());

        _data.Bundle = Bundle() with { Ingredients = [.. Bundle().Ingredients.Select(i => i.Id == "thit" ? i with { Names = new Dictionary<string, string> { [RegionKeys.Default] = "Thịt nạc" } } : i)] };
        _data.Raise();
        await Task.Delay(150, Xunit.TestContext.Current.CancellationToken);   // the page had its chance to react

        Texts(cut, ".shop-item__name").ShouldContain("Thịt");
        Texts(cut, ".shop-item__name").ShouldNotContain("Thịt nạc");

        _device.ShareGate.SetResult();
        await sending.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);
    }

    [Fact]
    public void The_same_bundle_announced_again_does_not_draw_the_list_from_the_start()
    {
        var cut = Show();
        cut.Find(".chip").Click();
        _shopping.Saved = ShoppingState.Empty;   // what the store holds now is not what is on the screen

        _data.Raise();

        cut.Find(".chip").GetAttribute("aria-pressed").ShouldBe("true");
    }

    [Fact]
    public async Task The_page_stops_listening_for_new_data_when_it_goes_away()
    {
        Show();
        _data.Listeners.ShouldBe(1);

        await DisposeComponentsAsync();

        _data.Listeners.ShouldBe(0);
    }
}
