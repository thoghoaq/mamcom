using System.Reflection;
using AngleSharp.Dom;
using Bunit.TestDoubles;
using MamCom.Core.Models;
using MamCom.Core.Taste;
using MamCom.Data;
using MamCom.Data.State;
using MamCom.UI.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace MamCom.UI.Tests;

public class SettingsScreenTests : BunitContext
{
    private readonly FakeAppData _data = new() { Bundle = TestConfig.SeedBundle };
    private readonly FakeHouseholdStore _households = new();
    private readonly FakeTasteStore _taste = new();
    private readonly FakeUserStateStore _userState = new();

    public SettingsScreenTests()
    {
        Services.AddSingleton<IAppData>(_data);
        Services.AddSingleton<IHouseholdStore>(_households);
        Services.AddSingleton<ITasteStore>(_taste);
        Services.AddSingleton<IUserStateStore>(_userState);
    }

    private static Member Adult(string name) => new(name, AgeGroup.Adult, 2, [], []);

    private static Household TwoAdults(Region region = Region.North, BowlSize bowlSize = BowlSize.Medium) => new(region, bowlSize, [Adult("Bố"), Adult("Mẹ")]);

    private IRenderedComponent<SettingsScreen> Show(Household? household = null)
    {
        _households.Saved = household ?? TwoAdults();
        return Render<SettingsScreen>(p => p.Add(s => s.Household, _households.Saved));
    }

    private BunitNavigationManager Navigation => Services.GetRequiredService<BunitNavigationManager>();

    private static string Words(IElement element) => element.TextContent.Words();

    private static IReadOnlyList<IElement> Group(IRenderedComponent<SettingsScreen> cut, string labelId) =>
        [.. cut.Find($"[aria-labelledby={labelId}]").QuerySelectorAll(".chip")];

    private static IReadOnlyList<IElement> Regions(IRenderedComponent<SettingsScreen> cut) => Group(cut, "settings-region-label");

    private static IReadOnlyList<IElement> Bowls(IRenderedComponent<SettingsScreen> cut) => Group(cut, "settings-bowl-label");

    private static string[] Checked(IEnumerable<IElement> chips) => [.. chips.Where(c => c.GetAttribute("aria-checked") == "true").Select(Words)];

    private static IElement Chip(IEnumerable<IElement> chips, string label) => chips.Single(c => Words(c) == label);

    private static string Status(IRenderedComponent<SettingsScreen> cut) => Words(cut.Find(".settings__status"));

    private static IElement Section(IRenderedComponent<SettingsScreen> cut, string id) => cut.Find($"section[aria-labelledby={id}]");

    private static string Hint(IElement group) => Words(group.NextElementSibling!);

    // ---- the page and what it needs ----------------------------------------------------------------------------------------

    [Fact]
    public void The_page_is_the_route_of_the_settings_and_the_route_is_the_one_the_header_links_to()
    {
        var route = typeof(SettingsPage).GetCustomAttribute<RouteAttribute>();

        route.ShouldNotBeNull();
        route.Template.ShouldBe("/" + Routes.Settings);
        Routes.Settings.ShouldBe("cai-dat");
    }

    [Fact]
    public void The_page_shows_the_settings_for_the_household_of_the_device_and_otherwise_sends_the_person_to_the_first_screen()
    {
        var without = Render<SettingsPage>();

        without.FindAll("h1").ShouldBeEmpty();
        Navigation.History.ShouldHaveSingleItem().Uri.ShouldBe(Routes.Household);

        _households.Saved = TwoAdults();
        var with = Render<SettingsPage>();

        with.Find("h1").TextContent.ShouldBe("Cài đặt");
    }

    [Fact]
    public void The_screen_has_a_way_back_to_the_tray_and_a_section_for_the_household_the_blocked_dishes_and_the_data()
    {
        var cut = Show();

        Words(cut.Find(".back__link")).ShouldBe("Về mâm hôm nay");
        cut.FindAll("section.settings__section h2").Select(Words).ShouldBe(["Nhà mình", "Món đã chặn", "Xoá dữ liệu của tôi"]);
        cut.FindAll("section.settings__section").ShouldAllBe(s => s.GetAttribute("aria-labelledby") == s.QuerySelector("h2")!.Id);
        _data.Initialized.ShouldBe(1);
    }

    [Fact]
    public void Without_the_data_nothing_is_drawn_yet()
    {
        _data.Bundle = null;

        Show().FindAll("section").ShouldBeEmpty();
    }

    // ---- the household -----------------------------------------------------------------------------------------------------

    [Fact]
    public void The_people_of_the_house_are_told_with_the_way_to_change_them()
    {
        var cut = Show();

        Words(cut.Find(".household-home")).ShouldContain("Nhà mình: 2 người, ≈ 2 suất mỗi bữa.");
        cut.Find(".household-home a").GetAttribute("href").ShouldBe(Routes.Household);
    }

    [Theory]
    [InlineData(Region.North, "Bắc")]
    [InlineData(Region.Central, "Trung")]
    [InlineData(Region.South, "Nam")]
    public void The_region_of_the_household_is_the_chip_that_is_chosen(Region region, string name)
    {
        var cut = Show(TwoAdults(region));

        Regions(cut).Select(Words).ShouldBe(["Bắc", "Trung", "Nam"]);
        Checked(Regions(cut)).ShouldBe([name]);
    }

    [Fact]
    public async Task A_tap_on_a_region_keeps_it_at_once_and_changes_nothing_else_about_the_household()
    {
        var household = new Household(Region.North, BowlSize.Large, [Adult("Bố"), new Member("Bà", AgeGroup.Senior, 1, [MemberTag.NoSpicy], ["tom"])], 40);
        var cut = Show(household);

        await Chip(Regions(cut), "Nam").ClickAsync(new());

        _households.Saved.ShouldNotBeNull();
        _households.Saved.Region.ShouldBe(Region.South);
        _households.Saved.BowlSize.ShouldBe(BowlSize.Large);
        _households.Saved.AvailableMinutes.ShouldBe(40);
        _households.Saved.Members.ShouldBe(household.Members);
        Checked(Regions(cut)).ShouldBe(["Nam"]);
        Status(cut).ShouldBe("Đã lưu.");
        _households.Saves.ShouldBe(1);
    }

    [Fact]
    public void The_region_is_a_group_of_choices_named_by_its_question()
    {
        var cut = Show();

        var group = cut.Find("[aria-labelledby=settings-region-label]");

        group.GetAttribute("role").ShouldBe("radiogroup");
        Words(cut.Find("#settings-region-label")).ShouldBe("Nhà mình ở miền nào?");
        Regions(cut).ShouldAllBe(c => c.GetAttribute("role") == "radio" && c.GetAttribute("type") == "button");
        Hint(group).ShouldBe("Miền ảnh hưởng tên nguyên liệu và đơn vị đi chợ.");
    }

    [Theory]
    [InlineData(BowlSize.Small, "Nhỏ", "35")]
    [InlineData(BowlSize.Medium, "Vừa", "40")]
    [InlineData(BowlSize.Large, "To", "50")]
    public void The_bowl_of_the_household_is_the_chip_that_is_chosen_and_the_hint_says_what_rice_it_is(BowlSize size, string name, string grams)
    {
        var cut = Show(TwoAdults(bowlSize: size));

        Bowls(cut).Select(Words).ShouldBe(["Nhỏ", "Vừa", "To"]);
        Checked(Bowls(cut)).ShouldBe([name]);
        Hint(cut.Find("[aria-labelledby=settings-bowl-label]")).ShouldBe($"Mỗi chén ≈ {grams} g gạo sống. Cỡ chén đổi lượng gạo cần nấu và kcal của mỗi người.");
    }

    [Fact]
    public void The_hint_keeps_the_number_and_its_unit_on_one_line()
    {
        var hint = Show().Find("[aria-labelledby=settings-bowl-label]").NextElementSibling!.TextContent;

        hint.ShouldContain("40 g");
    }

    [Fact]
    public async Task A_tap_on_a_bowl_keeps_it_at_once_and_the_hint_follows()
    {
        var cut = Show(TwoAdults(Region.Central));

        await Chip(Bowls(cut), "To").ClickAsync(new());

        _households.Saved!.BowlSize.ShouldBe(BowlSize.Large);
        _households.Saved.Region.ShouldBe(Region.Central);
        _households.Saved.Members.Count.ShouldBe(2);
        Checked(Bowls(cut)).ShouldBe(["To"]);
        Hint(cut.Find("[aria-labelledby=settings-bowl-label]")).ShouldContain("≈ 50 g");
        Status(cut).ShouldBe("Đã lưu.");

        await Chip(Bowls(cut), "Nhỏ").ClickAsync(new());

        _households.Saved.BowlSize.ShouldBe(BowlSize.Small);
        Hint(cut.Find("[aria-labelledby=settings-bowl-label]")).ShouldContain("≈ 35 g");
    }

    [Fact]
    public async Task The_region_and_the_bowl_changed_one_after_the_other_both_stay()
    {
        var cut = Show();

        await Chip(Regions(cut), "Trung").ClickAsync(new());
        await Chip(Bowls(cut), "Nhỏ").ClickAsync(new());

        _households.Saved!.Region.ShouldBe(Region.Central);
        _households.Saved.BowlSize.ShouldBe(BowlSize.Small);
    }

    private static IReadOnlyList<IElement> StoveChips(IRenderedComponent<SettingsScreen> cut) => Group(cut, "settings-stoves-label");

    [Fact]
    public void The_stoves_of_the_household_are_the_chip_that_is_chosen_and_the_usual_two_when_it_never_said()
    {
        StoveChips(Show()).Select(Words).ShouldBe(["1 bếp", "2 bếp", "3 bếp", "4 bếp"]);
        Checked(StoveChips(Show())).ShouldBe(["2 bếp"]);
        Checked(StoveChips(Show(TwoAdults() with { Stoves = 3 }))).ShouldBe(["3 bếp"]);
    }

    [Fact]
    public async Task A_tap_on_a_number_of_stoves_keeps_it_at_once_and_changes_nothing_else()
    {
        var cut = Show(TwoAdults(Region.South, BowlSize.Large));

        await Chip(StoveChips(cut), "1 bếp").ClickAsync(new());

        _households.Saved!.Stoves.ShouldBe(1);
        _households.Saved.Region.ShouldBe(Region.South);
        _households.Saved.BowlSize.ShouldBe(BowlSize.Large);
        Checked(StoveChips(cut)).ShouldBe(["1 bếp"]);
        Status(cut).ShouldBe("Đã lưu.");
        Hint(cut.Find("[aria-labelledby=settings-stoves-label]")).ShouldStartWith("Không tính nồi cơm điện.");
    }

    // ---- the blocked dishes ------------------------------------------------------------------------------------------------

    [Fact]
    public void With_no_dish_blocked_the_section_says_how_to_block_one()
    {
        var section = Section(Show(), "settings-blocked");

        Words(section).ShouldContain("Món đã chặn không bao giờ được quay.");
        Words(section).ShouldContain("Chưa chặn món nào. Mở một món và chạm \"Không bao giờ quay món này\".");
        section.QuerySelectorAll("li").ShouldBeEmpty();
    }

    [Fact]
    public void A_blocked_dish_is_listed_by_name_in_the_order_it_was_blocked_with_a_button_that_names_it()
    {
        _taste.Saved = new TasteProfile(new Dictionary<string, DishPreference>(), ["thit-kho-trung", "canh-chua-ca-loc"]);

        var section = Section(Show(), "settings-blocked");

        section.QuerySelectorAll(".blocked__name").Select(Words).ShouldBe(["Thịt kho trứng", "Canh chua cá lóc"]);
        section.QuerySelectorAll(".blocked__item button").Select(b => b.GetAttribute("aria-label")).ShouldBe(["Bỏ chặn Thịt kho trứng", "Bỏ chặn Canh chua cá lóc"]);
        section.QuerySelectorAll(".blocked__item button").Select(Words).ShouldBe(["Bỏ chặn", "Bỏ chặn"]);
        Words(section).ShouldNotContain("Chưa chặn món nào");
    }

    [Fact]
    public async Task Unblocking_a_dish_takes_that_one_off_the_list_keeps_the_rest_and_says_which()
    {
        _taste.Saved = new TasteProfile(new Dictionary<string, DishPreference> { ["ga-xao-sa-ot"] = DishPreference.Liked }, ["thit-kho-trung", "canh-chua-ca-loc"]);
        var cut = Show();

        await Section(cut, "settings-blocked").QuerySelectorAll(".blocked__item button")[0].ClickAsync(new());

        _taste.Saved.BlockedDishIds.ShouldBe(["canh-chua-ca-loc"]);
        _taste.Saved.Preferences["ga-xao-sa-ot"].ShouldBe(DishPreference.Liked);
        Section(cut, "settings-blocked").QuerySelectorAll(".blocked__name").Select(Words).ShouldBe(["Canh chua cá lóc"]);
        Status(cut).ShouldBe("Đã bỏ chặn Thịt kho trứng.");
        _taste.Saves.ShouldBe(1);
    }

    [Fact]
    public async Task Unblocking_the_last_dish_brings_back_the_words_about_blocking_one()
    {
        _taste.Saved = new TasteProfile(new Dictionary<string, DishPreference>(), ["thit-kho-trung"]);
        var cut = Show();

        await Section(cut, "settings-blocked").QuerySelector(".blocked__item button")!.ClickAsync(new());

        Words(Section(cut, "settings-blocked")).ShouldContain("Chưa chặn món nào.");
        _taste.Saved.BlockedDishIds.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_dish_the_data_no_longer_has_is_said_to_be_gone_and_can_still_be_unblocked()
    {
        _taste.Saved = new TasteProfile(new Dictionary<string, DishPreference>(), ["mon-da-bo", "thit-kho-trung"]);
        var cut = Show();
        var section = Section(cut, "settings-blocked");

        section.QuerySelectorAll(".blocked__name").Select(Words).ShouldBe(["Món không còn trong dữ liệu", "Thịt kho trứng"]);

        await section.QuerySelectorAll(".blocked__item button")[0].ClickAsync(new());

        _taste.Saved.BlockedDishIds.ShouldBe(["thit-kho-trung"]);
        Status(cut).ShouldBe("Đã bỏ chặn Món không còn trong dữ liệu.");
    }

    // ---- delete my data ----------------------------------------------------------------------------------------------------

    private static IElement ClearButton(IRenderedComponent<SettingsScreen> cut) => Section(cut, "settings-clear").QuerySelector("button")!;

    [Fact]
    public void The_section_says_what_is_deleted_and_what_is_not_and_offers_one_button_that_looks_dangerous()
    {
        var section = Section(Show(), "settings-clear");

        Words(section).ShouldContain("Xoá nhà mình, mâm đã quay, danh sách đi chợ, món đã chấm và món đã chặn khỏi máy này. Dữ liệu món ăn của app không bị xoá.");
        section.QuerySelectorAll("button").Select(Words).ShouldBe(["Xoá dữ liệu của tôi"]);
        section.QuerySelector("button")!.ClassList.ShouldContain("button--danger");
        section.QuerySelector(".notice").ShouldBeNull();
        _userState.Clears.ShouldBe(0);
    }

    [Fact]
    public async Task The_first_tap_only_asks_and_deletes_nothing()
    {
        var cut = Show();

        await ClearButton(cut).ClickAsync(new());

        var section = Section(cut, "settings-clear");
        section.QuerySelector(".notice p")!.GetAttribute("role").ShouldBe("alert");
        Words(section.QuerySelector(".notice p")!).ShouldBe("Xoá hết? Không lấy lại được.");
        section.QuerySelectorAll("button").Select(Words).ShouldBe(["Xoá hết", "Thôi"]);
        section.QuerySelectorAll(".notice button").Select(b => b.ClassList.Contains("button--danger")).ShouldBe([true, false]);
        _userState.Clears.ShouldBe(0);
        Navigation.History.ShouldBeEmpty();
    }

    [Fact]
    public async Task Thoi_goes_back_to_the_one_button_and_deletes_nothing()
    {
        var cut = Show();
        await ClearButton(cut).ClickAsync(new());

        await Section(cut, "settings-clear").QuerySelectorAll("button")[1].ClickAsync(new());

        Section(cut, "settings-clear").QuerySelectorAll("button").Select(Words).ShouldBe(["Xoá dữ liệu của tôi"]);
        _userState.Clears.ShouldBe(0);
        Navigation.History.ShouldBeEmpty();
    }

    [Fact]
    public async Task Xoa_het_clears_what_the_app_keeps_and_starts_the_page_again_from_the_beginning()
    {
        var cut = Show();
        await ClearButton(cut).ClickAsync(new());

        await Section(cut, "settings-clear").QuerySelectorAll("button")[0].ClickAsync(new());

        _userState.Clears.ShouldBe(1);
        var navigation = Navigation.History.ShouldHaveSingleItem();
        navigation.Uri.ShouldBe(Routes.Home);
        navigation.Options.ForceLoad.ShouldBeTrue();
    }

    /// <summary>The elements that took the focus from the page, as the id bUnit gives each element on the page.</summary>
    private IReadOnlyList<string?> Focused() =>
        [.. JSInterop.Invocations.Where(i => i.Identifier == "Blazor._internal.domWrapper.focus").Select(i => ((ElementReference)i.Arguments[0]!).Id)];

    private static string? ReferenceOf(IElement element) => element.GetAttribute("blazor:elementreference");

    [Fact]
    public async Task The_question_gives_the_focus_to_the_safe_answer_and_withdrawing_it_gives_the_focus_back_to_the_button()
    {
        var cut = Show();
        Focused().ShouldBeEmpty();   // nothing takes the focus when the page opens

        await ClearButton(cut).ClickAsync(new());

        var cancel = Section(cut, "settings-clear").QuerySelectorAll("button")[1];
        Focused().ShouldBe([ReferenceOf(cancel)]);

        await cancel.ClickAsync(new());

        Focused().ShouldBe([ReferenceOf(cancel), ReferenceOf(ClearButton(cut))]);
    }

    [Fact]
    public async Task The_focus_is_moved_once_and_not_again_each_time_the_page_is_drawn_after_that()
    {
        var cut = Show();
        await ClearButton(cut).ClickAsync(new());
        Focused().Count.ShouldBe(1);

        await Chip(Regions(cut), "Nam").ClickAsync(new());   // draws the page again, with the question still on it
        await Chip(Bowls(cut), "To").ClickAsync(new());
        var cancel = Section(cut, "settings-clear").QuerySelectorAll("button")[1];
        await cancel.ClickAsync(new());
        await Chip(Regions(cut), "Trung").ClickAsync(new());

        Focused().Count.ShouldBe(2);   // the one for the question and the one for its withdrawal, nothing else
    }

    [Fact]
    public async Task Taps_that_change_nothing_about_the_buttons_do_not_move_the_focus()
    {
        var cut = Show();

        await Chip(Regions(cut), "Nam").ClickAsync(new());
        await Chip(Bowls(cut), "To").ClickAsync(new());

        Focused().ShouldBeEmpty();
    }

    [Fact]
    public async Task While_it_is_clearing_both_buttons_of_the_question_are_off()
    {
        var cut = Show();
        await ClearButton(cut).ClickAsync(new());

        await Section(cut, "settings-clear").QuerySelectorAll("button")[0].ClickAsync(new());

        Section(cut, "settings-clear").QuerySelectorAll("button").ShouldAllBe(b => b.HasAttribute("disabled"));
    }

    [Fact]
    public async Task A_second_tap_while_it_is_clearing_does_not_clear_twice()
    {
        var cut = Show();
        await ClearButton(cut).ClickAsync(new());
        var confirm = Section(cut, "settings-clear").QuerySelectorAll("button")[0];

        await confirm.ClickAsync(new());
        await confirm.ClickAsync(new());

        _userState.Clears.ShouldBe(1);
    }

    // ---- the way to it -----------------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_page_that_is_gone_stops_listening_to_the_data()
    {
        Show();
        _data.Listeners.ShouldBe(2);   // the screen and the summary of the household inside it

        await DisposeComponentsAsync();

        _data.Listeners.ShouldBe(0);
    }
}
