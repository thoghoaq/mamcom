using System.Reflection;
using Bunit.TestDoubles;
using MamCom.Core.Models;
using MamCom.Data;
using MamCom.Data.Bundles;
using MamCom.Data.State;
using MamCom.UI.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace MamCom.UI.Tests;

public class OnboardingScreenTests : BunitContext
{
    private readonly FakeAppData _data = new() { Bundle = TestConfig.Bundle };
    private readonly FakeHouseholdStore _store = new();

    public OnboardingScreenTests()
    {
        Services.AddSingleton<IAppData>(_data);
        Services.AddSingleton<IHouseholdStore>(_store);
    }

    private static Household Family() => new(
        Region.South,
        BowlSize.Medium,
        [new Member("Ba", AgeGroup.Adult, 2, [], []), new Member("Má", AgeGroup.Adult, 2, [], [])]);

    private BunitNavigationManager Navigation => Services.GetRequiredService<BunitNavigationManager>();

    [Fact]
    public void The_screen_is_the_page_of_the_household_route_and_the_route_is_the_one_the_app_links_to()
    {
        var route = typeof(OnboardingScreen).GetCustomAttribute<RouteAttribute>();

        route.ShouldNotBeNull();
        route.Template.ShouldBe("/" + Routes.Household);
        Routes.Household.ShouldBe("nha-minh");
        Routes.Home.ShouldBe(string.Empty);
    }

    [Fact]
    public void It_asks_for_the_data_and_then_shows_the_question_and_the_form()
    {
        var cut = Render<OnboardingScreen>();

        _data.Initialized.ShouldBe(1);
        cut.Find("h1").TextContent.ShouldBe("Nhà mình có ai?");
        cut.FindAll(".chips").ShouldNotBeEmpty();
        cut.FindAll(".member-card").ShouldBeEmpty();
        cut.Find(".app-actions .button--primary").TextContent.ShouldBe("Bắt đầu");
    }

    [Fact]
    public void Without_the_data_there_is_only_the_question_because_the_notices_say_what_is_wrong()
    {
        _data.Bundle = null;
        _data.Status = BundleLoadStatus.NeedsNetwork;

        var cut = Render<OnboardingScreen>();

        cut.Find("h1").TextContent.ShouldBe("Nhà mình có ai?");
        cut.FindAll(".chips").ShouldBeEmpty();
        cut.FindAll("button").ShouldBeEmpty();
    }

    [Fact]
    public void A_saved_household_is_opened_to_be_changed()
    {
        _store.Saved = Family();

        var cut = Render<OnboardingScreen>();

        cut.FindAll(".member-card").Count.ShouldBe(2);
        cut.Find(".app-actions .button--primary").TextContent.ShouldBe("Lưu");
    }

    [Fact]
    public async Task The_form_waits_until_the_saved_household_has_been_read_so_it_never_starts_empty_and_stays_so()
    {
        _store.Saved = Family();
        _store.LoadGate = new TaskCompletionSource();

        var cut = Render<OnboardingScreen>();

        cut.FindAll(".chips").ShouldBeEmpty();   // the bundle is there, the household is not yet known

        _store.LoadGate.SetResult();
        await cut.WaitForStateAsync(() => cut.FindAll(".member-card").Count == 2);
        cut.Find(".app-actions .button--primary").TextContent.ShouldBe("Lưu");
    }

    [Fact]
    public void A_saved_household_that_cannot_be_used_is_the_same_as_none()
    {
        _store.Saved = new Household(Region.North, BowlSize.Medium, [new Member("Bố", AgeGroup.Adult, 99, [], [])]);

        var cut = Render<OnboardingScreen>();

        cut.FindAll(".member-card").ShouldBeEmpty();
        cut.Find(".app-actions .button--primary").TextContent.ShouldBe("Bắt đầu");
    }

    [Fact]
    public void Finishing_saves_the_household_and_goes_home()
    {
        var cut = Render<OnboardingScreen>();
        cut.FindAll(".chips")[0].QuerySelectorAll("button").First(b => b.TextContent.Trim() == "Bố").Click();
        cut.FindAll(".chips")[0].QuerySelectorAll("button").First(b => b.TextContent.Trim() == "Bà").Click();

        cut.Find(".app-actions .button--primary").Click();

        _store.Saves.ShouldBe(1);
        _store.Saved!.Members.Select(m => m.DisplayName).ShouldBe(["Bố", "Bà"]);
        Navigation.History.ShouldHaveSingleItem().Uri.ShouldBe(Routes.Home);
    }

    [Fact]
    public void Skipping_saves_a_household_of_two_grown_ups_and_goes_home()
    {
        var cut = Render<OnboardingScreen>();

        cut.Find(".app-actions .button--quiet").Click();

        _store.Saved!.Members.Select(m => m.DisplayName).ShouldBe(["Bố", "Mẹ"]);
        _store.Saved.Region.ShouldBe(Region.North);
        Navigation.History.ShouldHaveSingleItem().Uri.ShouldBe(Routes.Home);
    }

    [Fact]
    public async Task A_second_tap_while_the_household_is_being_saved_saves_nothing_more()
    {
        _store.SaveGate = new TaskCompletionSource();
        var cut = Render<OnboardingScreen>();
        cut.FindAll(".chips")[0].QuerySelectorAll("button").First(b => b.TextContent.Trim() == "Bố").Click();

        var first = cut.Find(".app-actions .button--primary").ClickAsync(new());
        cut.Find(".app-actions .button--primary").HasAttribute("disabled").ShouldBeTrue();
        var second = cut.Find(".app-actions .button--quiet").ClickAsync(new());   // even skipping is held back

        _store.Saves.ShouldBe(1);   // looked at before anything is awaited: if the second tap did save, it would wait on the gate for ever
        _store.SaveGate.SetResult();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken);
        Navigation.History.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task A_screen_that_is_gone_stops_listening_to_the_data()
    {
        Render<OnboardingScreen>();
        _data.Listeners.ShouldBe(1);

        await DisposeComponentsAsync();

        _data.Listeners.ShouldBe(0);
    }
}

public class HouseholdGateTests : BunitContext
{
    private readonly FakeAppData _data = new() { Bundle = TestConfig.Bundle };
    private readonly FakeHouseholdStore _store = new();

    public HouseholdGateTests()
    {
        Services.AddSingleton<IAppData>(_data);
        Services.AddSingleton<IHouseholdStore>(_store);
    }

    private IRenderedComponent<HouseholdGate> Show() =>
        Render<HouseholdGate>(p => p.Add(g => g.ChildContent, (Household h) => $"<p id=\"child\">{h.Members.Count} người</p>"));

    private BunitNavigationManager Navigation => Services.GetRequiredService<BunitNavigationManager>();

    [Fact]
    public void A_device_with_no_household_is_sent_to_the_first_screen_and_the_page_is_not_shown()
    {
        var cut = Show();

        cut.FindAll("#child").ShouldBeEmpty();
        var entry = Navigation.History.ShouldHaveSingleItem();
        entry.Uri.ShouldBe(Routes.Household);
        entry.Options.ReplaceHistoryEntry.ShouldBeTrue();   // "back" must not return to a page that sends the person on again
    }

    [Fact]
    public void A_household_that_cannot_be_used_is_sent_to_the_first_screen_too()
    {
        _store.Saved = new Household(Region.North, BowlSize.Medium, [new Member("Bố", AgeGroup.Adult, 0, [], [])]);

        var cut = Show();

        cut.FindAll("#child").ShouldBeEmpty();
        Navigation.History.ShouldHaveSingleItem().Uri.ShouldBe(Routes.Household);
    }

    [Fact]
    public void A_household_that_can_be_used_shows_the_page_with_that_household()
    {
        _store.Saved = new Household(
            Region.North,
            BowlSize.Medium,
            [new Member("Bố", AgeGroup.Adult, 2, [], []), new Member("Mẹ", AgeGroup.Adult, 2, [], []), new Member("Bé", AgeGroup.Toddler, 1, [], [])]);

        var cut = Show();

        cut.Find("#child").TextContent.ShouldBe("3 người");
        Navigation.History.ShouldBeEmpty();
    }

    [Fact]
    public void It_asks_for_the_data_first()
    {
        Show();

        _data.Initialized.ShouldBe(1);
    }

    [Fact]
    public void Without_the_data_it_shows_nothing_and_goes_nowhere_because_the_notices_say_what_is_wrong()
    {
        _data.Bundle = null;
        _data.Status = BundleLoadStatus.NotPublished;
        _store.Saved = new Household(Region.North, BowlSize.Medium, [new Member("Bố", AgeGroup.Adult, 2, [], [])]);

        var cut = Show();

        cut.Markup.Trim().ShouldBeEmpty();
        Navigation.History.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_gate_that_is_gone_stops_listening_to_the_data()
    {
        Show();
        _data.Listeners.ShouldBe(1);

        await DisposeComponentsAsync();

        _data.Listeners.ShouldBe(0);
    }
}

public class HouseholdSummaryTests : BunitContext
{
    private readonly FakeAppData _data = new() { Bundle = TestConfig.Bundle };

    public HouseholdSummaryTests()
    {
        Services.AddSingleton<IAppData>(_data);
    }

    private static Household SpecFamily() => new(
        Region.North,
        BowlSize.Medium,
        [
            new Member("Bố", AgeGroup.Adult, 3, [], []),
            new Member("Mẹ", AgeGroup.Adult, 2, [], []),
            new Member("Con", AgeGroup.Child, 1.5, [], []),
            new Member("Bà", AgeGroup.Senior, 1.5, [], []),
        ]);

    [Fact]
    public void It_says_how_many_people_and_how_many_servings_and_offers_to_change_them()
    {
        var cut = Render<HouseholdSummary>(p => p.Add(s => s.Household, SpecFamily()));

        cut.Find(".household-home").TextContent.Plain().ShouldContain("Nhà mình: 4 người, ≈ 3,8 suất mỗi bữa.");
        cut.Find(".household-home").TextContent.ShouldContain("≈ 3,8 suất");
        var link = cut.Find(".household-home a");
        link.TextContent.ShouldBe("Sửa nhà mình");
        link.GetAttribute("href").ShouldBe(Routes.Household);
    }

    [Fact]
    public void Without_the_data_there_is_nothing_to_calculate_with_so_it_shows_nothing()
    {
        _data.Bundle = null;

        Render<HouseholdSummary>(p => p.Add(s => s.Household, SpecFamily())).Markup.Trim().ShouldBeEmpty();
    }

    [Fact]
    public void When_the_data_arrives_it_appears()
    {
        _data.Bundle = null;
        var cut = Render<HouseholdSummary>(p => p.Add(s => s.Household, SpecFamily()));

        _data.Bundle = TestConfig.Bundle;
        _data.Raise();

        cut.WaitForAssertion(() => cut.Find(".household-home").TextContent.Plain().ShouldContain("4 người"));
    }
}
