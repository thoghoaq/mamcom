using MamCom.Core.Models;
using MamCom.Data.State;
using MamCom.Data.Storage;

namespace MamCom.Data.Tests;

public class HouseholdStoreTests
{
    private static readonly Household Family = new(
        Region.Central,
        BowlSize.Large,
        [
            new Member("Bố", AgeGroup.Adult, 3, [MemberTag.MoreProtein], ["hai-san"]),
            new Member("Bà", AgeGroup.Senior, 1.5, [], []),
        ],
        AvailableMinutes: 40);

    private static (HouseholdStore Store, FakeBrowser Browser) Create()
    {
        var browser = new FakeBrowser();
        return (new HouseholdStore(new UserStateStore(new LocalStorageKeyValueStorage(browser))), browser);
    }

    [Fact]
    public async Task What_is_saved_comes_back_the_same_in_a_new_visit()
    {
        var (store, browser) = Create();

        (await store.SaveAsync(Family)).ShouldBeTrue();
        var back = await new HouseholdStore(new UserStateStore(new LocalStorageKeyValueStorage(browser))).LoadAsync();

        back.ShouldNotBeNull();
        back.Region.ShouldBe(Region.Central);
        back.BowlSize.ShouldBe(BowlSize.Large);
        back.AvailableMinutes.ShouldBe(40);
        back.Members.Select(m => (m.DisplayName, m.AgeGroup, m.BowlsPerMeal)).ShouldBe([("Bố", AgeGroup.Adult, 3), ("Bà", AgeGroup.Senior, 1.5)]);
        back.Members[0].Tags.ShouldBe([MemberTag.MoreProtein]);
        back.Members[0].Avoid.ShouldBe(["hai-san"]);
        back.Members[1].Tags.ShouldBeEmpty();
    }

    [Fact]
    public async Task Nothing_saved_is_nothing()
    {
        var (store, _) = Create();

        (await store.LoadAsync()).ShouldBeNull();
    }

    [Fact]
    public async Task It_lives_under_household_v1_with_the_names_of_the_enums_in_the_file()
    {
        var (store, browser) = Create();

        await store.SaveAsync(Family);

        HouseholdStore.Key.ShouldBe("household.v1");
        var json = browser.Items["mamcom.state.household.v1"];
        json.ShouldContain("\"region\": \"central\"");
        json.ShouldContain("\"bowlSize\": \"large\"");
        json.ShouldContain("\"ageGroup\": \"senior\"");
    }

    [Theory]
    [InlineData("{ half written")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("""{ "region": "north" }""")]
    [InlineData("""{ "region": "mars", "bowlSize": "medium", "members": [] }""")]
    public async Task A_household_that_cannot_be_read_is_nothing_and_is_left_where_it_is(string json)
    {
        var (store, browser) = Create();
        browser.Items["mamcom.state.household.v1"] = json;

        (await store.LoadAsync()).ShouldBeNull();

        browser.Items["mamcom.state.household.v1"].ShouldBe(json);
    }

    [Fact]
    public async Task The_next_save_replaces_the_household()
    {
        var (store, _) = Create();
        await store.SaveAsync(Family);

        await store.SaveAsync(Family with { Region = Region.South, Members = [Family.Members[1]] });

        var back = await store.LoadAsync();
        back!.Region.ShouldBe(Region.South);
        back.Members.Count.ShouldBe(1);
    }

    [Fact]
    public async Task A_browser_that_keeps_nothing_still_gives_the_household_back_for_this_visit_and_says_it_is_not_kept()
    {
        var browser = new FakeBrowser { RefuseWrites = true };
        var store = new HouseholdStore(new UserStateStore(new LocalStorageKeyValueStorage(browser)));

        (await store.SaveAsync(Family)).ShouldBeFalse();

        (await store.LoadAsync())!.Members.Count.ShouldBe(2);
        browser.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task Clearing_the_state_of_the_app_forgets_the_household()
    {
        var browser = new FakeBrowser();
        var state = new UserStateStore(new LocalStorageKeyValueStorage(browser));
        var store = new HouseholdStore(state);
        await store.SaveAsync(Family);

        await state.ClearAsync();

        (await store.LoadAsync()).ShouldBeNull();
    }

    [Fact]
    public async Task A_household_is_required()
    {
        var (store, _) = Create();

        await Should.ThrowAsync<ArgumentNullException>(async () => await store.SaveAsync(null!));
    }
}
