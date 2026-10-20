using MamCom.Core.Models;
using MamCom.Core.Spinning;
using MamCom.Data.Device;
using MamCom.Data.State;
using MamCom.Data.Storage;

namespace MamCom.Data.Tests;

public class TrayStoreTests
{
    private static readonly DateOnly Day = new(2026, 10, 20);

    private static TraySession Session() => new(
        new SpinHistory(
        [
            new SpinResult(new MealTray(Day, [new SlotResult(DishSlot.Soup, "canh-chua", true), new SlotResult(DishSlot.Main, "ca-kho", false)]), [RelaxedRule.AvailableMinutes], [DishSlot.Vegetable]),
            new SpinResult(new MealTray(Day.AddDays(-1), [new SlotResult(DishSlot.Soup, "canh-bi", false)]), [], []),
        ]),
        ViewIndex: 1,
        SpinsSinceConfirm: 3,
        new QuickFilters(true, false, true),
        ConfirmedOn: null);

    private static (UserStateStore State, FakeBrowser Browser) Create(FakeBrowser? browser = null)
    {
        browser ??= new FakeBrowser();
        return (new UserStateStore(new LocalStorageKeyValueStorage(browser)), browser);
    }

    // ---- the session ------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_session_that_is_saved_comes_back_in_a_new_visit()
    {
        var (state, browser) = Create();
        (await new TrayStore(state).SaveAsync(Session())).ShouldBeTrue();

        var back = await new TrayStore(Create(browser).State).LoadAsync();

        back.ShouldNotBeNull();
        back.ViewIndex.ShouldBe(1);
        back.SpinsSinceConfirm.ShouldBe(3);
        back.Filters.ShouldBe(new QuickFilters(true, false, true));
        back.History.Items.Count.ShouldBe(2);
        back.History.Items[0].Tray.Slots.Select(s => (s.DishId, s.IsLocked)).ShouldBe([("canh-chua", true), ("ca-kho", false)]);
        back.History.Items[0].RelaxedRules.ShouldBe([RelaxedRule.AvailableMinutes]);
        back.History.Items[0].UnfilledSlots.ShouldBe([DishSlot.Vegetable]);
        back.History.Items[1].Tray.Date.ShouldBe(Day.AddDays(-1));
    }

    [Fact]
    public async Task It_lives_under_tray_v1()
    {
        var (state, browser) = Create();

        await new TrayStore(state).SaveAsync(Session());

        TrayStore.Key.ShouldBe("tray.v1");
        browser.Items.Keys.ShouldContain("mamcom.state.tray.v1");
    }

    [Fact]
    public async Task Nothing_saved_is_nothing()
    {
        (await new TrayStore(Create().State).LoadAsync()).ShouldBeNull();
    }

    [Theory]
    [InlineData("{ half written")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("""{ "viewIndex": 0 }""")]
    public async Task A_session_that_cannot_be_read_is_nothing_and_is_left_where_it_is(string json)
    {
        var (state, browser) = Create();
        browser.Items["mamcom.state.tray.v1"] = json;

        (await new TrayStore(state).LoadAsync()).ShouldBeNull();

        browser.Items["mamcom.state.tray.v1"].ShouldBe(json);
    }

    [Fact]
    public async Task A_browser_that_keeps_nothing_still_gives_the_session_back_for_this_visit_and_says_it_is_not_kept()
    {
        var (state, browser) = Create(new FakeBrowser { RefuseWrites = true });
        var store = new TrayStore(state);

        (await store.SaveAsync(Session())).ShouldBeFalse();

        (await store.LoadAsync())!.SpinsSinceConfirm.ShouldBe(3);
        browser.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_next_save_replaces_the_session()
    {
        var store = new TrayStore(Create().State);
        await store.SaveAsync(Session());

        await store.SaveAsync(TraySession.Empty);

        (await store.LoadAsync())!.History.Items.ShouldBeEmpty();
    }

    // ---- the log of what was eaten ----------------------------------------------------------------------------------------

    [Fact]
    public async Task What_was_eaten_comes_back_in_a_new_visit()
    {
        var (state, browser) = Create();
        (await new EatenLogStore(state).SaveAsync(EatenLog.Empty.With(["canh-chua", "ca-kho"], Day))).ShouldBeTrue();

        var back = await new EatenLogStore(Create(browser).State).LoadAsync();

        back.LastEatenOn.OrderBy(p => p.Key, StringComparer.Ordinal).ShouldBe([KeyValuePair.Create("ca-kho", Day), KeyValuePair.Create("canh-chua", Day)]);
        EatenLogStore.Key.ShouldBe("eaten.v1");
        browser.Items.Keys.ShouldContain("mamcom.state.eaten.v1");
    }

    [Fact]
    public async Task Nothing_eaten_is_an_empty_log_and_not_nothing()
    {
        var back = await new EatenLogStore(Create().State).LoadAsync();

        back.LastEatenOn.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("{ half written")]
    [InlineData("null")]
    [InlineData("""{ "lastEatenOn": { "a": "not a day" } }""")]
    public async Task A_log_that_cannot_be_read_is_an_empty_log_and_is_left_where_it_is(string json)
    {
        var (state, browser) = Create();
        browser.Items["mamcom.state.eaten.v1"] = json;

        (await new EatenLogStore(state).LoadAsync()).LastEatenOn.ShouldBeEmpty();

        browser.Items["mamcom.state.eaten.v1"].ShouldBe(json);
    }

    [Fact]
    public async Task Both_stores_are_cleared_with_the_state_of_the_app()
    {
        var (state, _) = Create();
        await new TrayStore(state).SaveAsync(Session());
        await new EatenLogStore(state).SaveAsync(EatenLog.Empty.With(["a"], Day));

        await state.ClearAsync();

        (await new TrayStore(state).LoadAsync()).ShouldBeNull();
        (await new EatenLogStore(state).LoadAsync()).LastEatenOn.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_session_and_a_log_are_required()
    {
        var (state, _) = Create();

        await Should.ThrowAsync<ArgumentNullException>(async () => await new TrayStore(state).SaveAsync(null!));
        await Should.ThrowAsync<ArgumentNullException>(async () => await new EatenLogStore(state).SaveAsync(null!));
    }
}

public class DeviceFeaturesTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_vibration_says_whether_the_device_buzzed_and_how_long_it_was_asked_to(bool buzzed)
    {
        var browser = new FakeBrowser { VibrateAnswer = buzzed };

        (await new BrowserDeviceFeatures(browser).VibrateAsync(25)).ShouldBe(buzzed);

        browser.Vibrations.ShouldBe([25]);
    }

    [Fact]
    public async Task A_page_without_the_script_does_not_buzz_and_does_not_fail()
    {
        var browser = new FakeBrowser();   // no mamcomDevice

        (await new BrowserDeviceFeatures(browser).VibrateAsync(25)).ShouldBeFalse();
        (await new BrowserDeviceFeatures(browser).PrefersReducedMotionAsync()).ShouldBeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_wish_for_less_movement_is_the_answer_of_the_browser(bool reduced)
    {
        var browser = new FakeBrowser { ReducedMotionAnswer = reduced };

        (await new BrowserDeviceFeatures(browser).PrefersReducedMotionAsync()).ShouldBe(reduced);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task A_vibration_of_no_time_is_refused(int milliseconds)
    {
        await Should.ThrowAsync<ArgumentOutOfRangeException>(async () => await new BrowserDeviceFeatures(new FakeBrowser()).VibrateAsync(milliseconds));
    }
}
