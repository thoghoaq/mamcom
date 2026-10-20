using System.Reflection;
using AngleSharp.Dom;
using Bunit.TestDoubles;
using MamCom.Core;
using MamCom.Core.Models;
using MamCom.Core.Spinning;
using MamCom.Core.Taste;
using MamCom.Data;
using MamCom.Data.State;
using MamCom.UI.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace MamCom.UI.Tests;

public class AfterMealScreenTests : BunitContext
{
    private static readonly DateOnly Today = new(2026, 10, 20);

    private const string Soup = "canh-chua-ca-loc";
    private const string Braise = "thit-kho-trung";
    private const string Greens = "rau-muong-luoc";

    private readonly FakeAppData _data = new() { Bundle = TestConfig.SeedBundle };
    private readonly FakeTrayStore _trays = new();
    private readonly FakeTasteStore _taste = new();

    public AfterMealScreenTests()
    {
        Services.AddSingleton<IAppData>(_data);
        Services.AddSingleton<ITrayStore>(_trays);
        Services.AddSingleton<ITasteStore>(_taste);
        Services.AddSingleton<IClock>(new FixedClock(Today));
        Services.AddSingleton<IRandomSource>(new SeededRandomSource(3));
        _trays.Saved = Session();
    }

    private static TraySession Session(DateOnly? confirmedOn = null, DateOnly? day = null) => new(
        new SpinHistory([new SpinResult(new MealTray(day ?? Today, [new SlotResult(DishSlot.Soup, Soup, false), new SlotResult(DishSlot.Main, Braise, false), new SlotResult(DishSlot.Vegetable, Greens, false)]), [], [])]),
        0,
        0,
        QuickFilters.None,
        confirmedOn ?? Today);

    private static TasteProfile Said(params (string Id, DishPreference Preference)[] preferences) =>
        new(preferences.ToDictionary(p => p.Id, p => p.Preference), []);

    private IRenderedComponent<AfterMealScreen> Show() => Render<AfterMealScreen>();

    private BunitNavigationManager Navigation => Services.GetRequiredService<BunitNavigationManager>();

    private static string Words(IElement element) => element.TextContent.Words();

    private static IReadOnlyList<IElement> WholeChips(IRenderedComponent<AfterMealScreen> cut) => cut.FindAll(".chips--rate-whole .chip");

    private static IElement Row(IRenderedComponent<AfterMealScreen> cut, string dishName) =>
        cut.FindAll(".rate-row").Single(r => Words(r.QuerySelector(".rate-row__name")!) == dishName);

    private static IReadOnlyList<IElement> ChipsOf(IElement row) => [.. row.QuerySelectorAll(".chip")];

    private static string[] Checked(IEnumerable<IElement> chips) => [.. chips.Where(c => c.GetAttribute("aria-checked") == "true").Select(Words)];

    private static IElement Chip(IEnumerable<IElement> chips, string label) => chips.Single(c => Words(c) == label);

    private string Status(IRenderedComponent<AfterMealScreen> cut) => Words(cut.Find(".settings__status"));

    // ---- the page and what it needs ----------------------------------------------------------------------------------------

    [Fact]
    public void The_page_is_the_route_of_the_question_and_the_route_is_the_one_the_app_links_to()
    {
        var route = typeof(AfterMealScreen).GetCustomAttribute<RouteAttribute>();

        route.ShouldNotBeNull();
        route.Template.ShouldBe("/" + Routes.AfterMeal);
        Routes.AfterMeal.ShouldBe("sau-bua");
    }

    [Fact]
    public void The_page_asks_whether_the_whole_family_liked_it_with_a_way_back_to_the_tray()
    {
        var cut = Show();

        cut.Find("h1").TextContent.ShouldBe("Cả nhà thích không?");
        cut.Find(".back__link").GetAttribute("href").ShouldBe(Navigation.BaseUri);
        Words(cut.Find(".back__link")).ShouldBe("Về mâm hôm nay");
        _data.Initialized.ShouldBe(1);
    }

    // ---- when there is no meal to ask about --------------------------------------------------------------------------------

    [Fact]
    public void With_no_tray_at_all_the_page_says_so_and_asks_nothing()
    {
        _trays.Saved = null;

        var cut = Show();

        cut.Markup.Plain().ShouldContain("Chưa có mâm nào đã chốt để chấm. Hãy quay và chốt một mâm trước.");
        cut.FindAll(".chip").ShouldBeEmpty();
        cut.FindAll(".app-actions").ShouldBeEmpty();
    }

    [Fact]
    public void A_tray_that_was_not_confirmed_is_not_a_meal_that_was_eaten()
    {
        _trays.Saved = Session() with { ConfirmedOn = null };

        var cut = Show();

        cut.Markup.Plain().ShouldContain("Chưa có mâm nào đã chốt để chấm.");
        cut.FindAll(".chip").ShouldBeEmpty();
    }

    [Fact]
    public void A_tray_of_another_day_is_not_asked_about()
    {
        _trays.Saved = Session(day: Today.AddDays(-1));

        var cut = Show();

        cut.Markup.Plain().ShouldContain("Chưa có mâm nào đã chốt để chấm.");
        cut.FindAll(".chip").ShouldBeEmpty();
    }

    [Fact]
    public void Without_the_data_nothing_is_drawn_yet()
    {
        _data.Bundle = null;

        var cut = Show();

        cut.FindAll(".chip").ShouldBeEmpty();
        cut.Markup.Plain().ShouldNotContain("Chưa có mâm nào đã chốt");
    }

    // ---- the question ------------------------------------------------------------------------------------------------------

    [Fact]
    public void One_row_is_for_the_whole_meal_and_a_row_for_each_dish_in_the_order_of_the_tray_each_with_the_three_answers()
    {
        var cut = Show();

        WholeChips(cut).Select(Words).ShouldBe(["Thích", "Bình thường", "Không"]);
        cut.FindAll(".rate-row__name").Select(Words).ShouldBe(["Canh chua cá lóc", "Thịt kho trứng", "Rau muống luộc"]);
        foreach (var row in cut.FindAll(".rate-row"))
        {
            ChipsOf(row).Select(Words).ShouldBe(["Thích", "Bình thường", "Không"]);
        }
    }

    [Fact]
    public void Each_row_is_a_group_of_choices_named_by_what_it_is_about()
    {
        var cut = Show();

        var groups = cut.FindAll("[role=radiogroup]");
        groups.Count.ShouldBe(4);
        groups.ShouldAllBe(g => cut.Find("#" + g.GetAttribute("aria-labelledby")) != null);
        Words(cut.Find("#" + groups[0].GetAttribute("aria-labelledby"))).ShouldBe("Cả mâm hôm nay");
        Words(cut.Find("#" + groups[1].GetAttribute("aria-labelledby"))).ShouldBe("Canh chua cá lóc");
        cut.FindAll(".chip").ShouldAllBe(c => c.GetAttribute("role") == "radio" && c.GetAttribute("type") == "button");
        cut.FindAll(".chip").Select(c => c.GetAttribute("aria-checked")).Distinct().ShouldBe(["false"]);
    }

    [Fact]
    public void The_page_ends_with_a_button_that_goes_back_to_the_tray()
    {
        var cut = Show();

        var done = cut.Find(".app-actions a");

        Words(done).ShouldBe("Xong");
        done.ClassList.ShouldContain("button--primary");
        done.GetAttribute("href").ShouldBe(Navigation.BaseUri);
    }

    [Fact]
    public void The_status_line_is_there_from_the_start_so_what_is_said_into_it_is_read_out()
    {
        var cut = Show();

        cut.Find(".settings__status").GetAttribute("role").ShouldBe("status");
        Status(cut).ShouldBeEmpty();
    }

    // ---- one tap for the whole meal ----------------------------------------------------------------------------------------

    [Fact]
    public async Task One_tap_on_Thich_rates_every_dish_of_the_meal_keeps_it_and_says_so()
    {
        var cut = Show();

        await Chip(WholeChips(cut), "Thích").ClickAsync(new());

        new[] { Soup, Braise, Greens }.ShouldAllBe(id => TasteDesk.PreferenceOf(_taste.Saved, id) == DishPreference.Liked);
        _taste.Saves.ShouldBe(1);
        Status(cut).ShouldBe("Đã ghi. Món cả nhà thích sẽ hay ra hơn.");
        Checked(WholeChips(cut)).ShouldBe(["Thích"]);
        cut.FindAll(".rate-row").ShouldAllBe(r => Checked(ChipsOf(r)).SequenceEqual(new[] { "Thích" }));
    }

    [Fact]
    public async Task One_tap_on_Khong_rates_every_dish_as_not_liked()
    {
        var cut = Show();

        await Chip(WholeChips(cut), "Không").ClickAsync(new());

        new[] { Soup, Braise, Greens }.ShouldAllBe(id => TasteDesk.PreferenceOf(_taste.Saved, id) == DishPreference.Disliked);
        Status(cut).ShouldBe("Đã ghi. Món cả nhà không thích sẽ ít ra hơn.");
        Checked(WholeChips(cut)).ShouldBe(["Không"]);
    }

    [Fact]
    public async Task One_tap_on_Binh_thuong_takes_the_dishes_out_of_the_list_and_only_then_shows_as_chosen()
    {
        _taste.Saved = Said((Soup, DishPreference.Liked), (Braise, DishPreference.Disliked));
        var cut = Show();
        Checked(WholeChips(cut)).ShouldBeEmpty();   // the dishes differ

        await Chip(WholeChips(cut), "Bình thường").ClickAsync(new());

        _taste.Saved.Preferences.ShouldBeEmpty();
        Status(cut).ShouldBe("Đã ghi: bình thường.");
        Checked(WholeChips(cut)).ShouldBe(["Bình thường"]);
        cut.FindAll(".rate-row").ShouldAllBe(r => Checked(ChipsOf(r)).SequenceEqual(new[] { "Bình thường" }));
    }

    [Fact]
    public void A_meal_never_asked_about_has_no_answer_shown_though_the_dishes_count_as_normal()
    {
        var cut = Show();

        Checked(WholeChips(cut)).ShouldBeEmpty();
        cut.FindAll(".rate-row").ShouldAllBe(r => Checked(ChipsOf(r)).Length == 0);
    }

    [Fact]
    public void What_the_household_said_before_is_shown_when_the_page_opens()
    {
        _taste.Saved = Said((Soup, DishPreference.Liked), (Braise, DishPreference.Liked), (Greens, DishPreference.Liked));

        var cut = Show();

        Checked(WholeChips(cut)).ShouldBe(["Thích"]);
        Checked(ChipsOf(Row(cut, "Thịt kho trứng"))).ShouldBe(["Thích"]);
    }

    // ---- a tap for one dish ------------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_tap_on_one_dish_rates_that_dish_only_and_the_whole_row_shows_no_answer_as_the_dishes_differ()
    {
        var cut = Show();

        await Chip(ChipsOf(Row(cut, "Thịt kho trứng")), "Không").ClickAsync(new());

        _taste.Saved.Preferences.ShouldBe(new Dictionary<string, DishPreference> { [Braise] = DishPreference.Disliked });
        Checked(ChipsOf(Row(cut, "Thịt kho trứng"))).ShouldBe(["Không"]);
        Checked(ChipsOf(Row(cut, "Canh chua cá lóc"))).ShouldBeEmpty();
        Checked(WholeChips(cut)).ShouldBeEmpty();
        Status(cut).ShouldBe("Đã ghi. Món cả nhà không thích sẽ ít ra hơn.");
    }

    [Fact]
    public async Task A_tap_on_the_first_dish_alone_is_not_the_answer_of_the_meal()
    {
        var cut = Show();

        await Chip(ChipsOf(Row(cut, "Canh chua cá lóc")), "Thích").ClickAsync(new());

        Checked(ChipsOf(Row(cut, "Canh chua cá lóc"))).ShouldBe(["Thích"]);
        Checked(WholeChips(cut)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Binh_thuong_for_one_dish_shows_for_that_dish_only_and_not_for_the_meal()
    {
        var cut = Show();

        await Chip(ChipsOf(Row(cut, "Rau muống luộc")), "Bình thường").ClickAsync(new());

        Checked(ChipsOf(Row(cut, "Rau muống luộc"))).ShouldBe(["Bình thường"]);
        Checked(ChipsOf(Row(cut, "Canh chua cá lóc"))).ShouldBeEmpty();
        Checked(WholeChips(cut)).ShouldBeEmpty();   // two of the three were not answered
    }

    [Fact]
    public async Task Each_dish_can_be_answered_and_when_all_three_are_the_same_the_whole_row_shows_it()
    {
        var cut = Show();

        foreach (var name in new[] { "Canh chua cá lóc", "Thịt kho trứng", "Rau muống luộc" })
        {
            await Chip(ChipsOf(Row(cut, name)), "Thích").ClickAsync(new());
        }

        Checked(WholeChips(cut)).ShouldBe(["Thích"]);
        _taste.Saves.ShouldBe(3);
    }

    [Fact]
    public async Task A_tap_on_the_whole_meal_after_a_tap_on_one_dish_replaces_that_answer()
    {
        var cut = Show();
        await Chip(ChipsOf(Row(cut, "Thịt kho trứng")), "Không").ClickAsync(new());

        await Chip(WholeChips(cut), "Thích").ClickAsync(new());

        TasteDesk.PreferenceOf(_taste.Saved, Braise).ShouldBe(DishPreference.Liked);
    }

    [Fact]
    public async Task Rating_leaves_what_was_said_about_other_dishes_and_the_blocked_dishes_as_they_were()
    {
        _taste.Saved = new TasteProfile(new Dictionary<string, DishPreference> { ["ga-xao-sa-ot"] = DishPreference.Liked }, ["canh-bau-tom"]);
        var cut = Show();

        await Chip(WholeChips(cut), "Không").ClickAsync(new());

        TasteDesk.PreferenceOf(_taste.Saved, "ga-xao-sa-ot").ShouldBe(DishPreference.Liked);
        _taste.Saved.BlockedDishIds.ShouldBe(["canh-bau-tom"]);
    }

    // ---- new data ----------------------------------------------------------------------------------------------------------

    [Fact]
    public void When_a_newer_bundle_takes_away_a_dish_of_the_meal_the_page_no_longer_asks_about_it()
    {
        var cut = Show();
        cut.FindAll(".rate-row").Count.ShouldBe(3);

        _data.Bundle = TestConfig.SeedBundle with { Version = "v2", Dishes = [.. TestConfig.SeedBundle.Dishes.Where(d => d.Id != Braise)] };
        _data.Raise();

        cut.WaitForAssertion(() =>
        {
            cut.FindAll(".chip").ShouldBeEmpty();
            cut.Markup.Plain().ShouldContain("Chưa có mâm nào đã chốt để chấm.");
        });
    }

    [Fact]
    public async Task A_page_that_is_gone_stops_listening_to_the_data()
    {
        var cut = Show();
        _data.Listeners.ShouldBe(1);

        await DisposeComponentsAsync();

        _data.Listeners.ShouldBe(0);
    }
}
