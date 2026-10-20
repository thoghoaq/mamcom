using MamCom.Core.Models;
using MamCom.Core.Spinning;
using MamCom.Data.State;
using MamCom.Data.Storage;

namespace MamCom.Data.Tests;

public class MemoryStorageTests
{
    [Fact]
    public async Task It_keeps_what_it_is_given_for_the_life_of_the_page_and_says_it_is_not_persistent()
    {
        var storage = new MemoryKeyValueStorage();

        (await storage.SetAsync("a", "1")).ShouldBeFalse();
        (await storage.GetAsync("a")).ShouldBe("1");
        (await storage.GetAsync("b")).ShouldBeNull();
        await storage.RemoveAsync("a");
        (await storage.GetAsync("a")).ShouldBeNull();
        storage.IsPersistent.ShouldBeFalse();
        (await storage.RequestPersistenceAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task The_latest_value_wins()
    {
        var storage = new MemoryKeyValueStorage();
        await storage.SetAsync("a", "1");
        await storage.SetAsync("a", "2");

        (await storage.GetAsync("a")).ShouldBe("2");
    }

    [Fact]
    public async Task The_arguments_are_checked()
    {
        var storage = new MemoryKeyValueStorage();

        await Should.ThrowAsync<ArgumentNullException>(async () => await storage.GetAsync(null!));
        await Should.ThrowAsync<ArgumentNullException>(async () => await storage.SetAsync(null!, "x"));
        await Should.ThrowAsync<ArgumentNullException>(async () => await storage.SetAsync("a", null!));
        await Should.ThrowAsync<ArgumentNullException>(async () => await storage.RemoveAsync(null!));
    }
}

public class LocalStorageTests
{
    [Fact]
    public async Task It_stores_in_the_browsers_localStorage_and_reads_it_back()
    {
        var browser = new FakeBrowser();
        var storage = new LocalStorageKeyValueStorage(browser);

        (await storage.SetAsync("k", "v")).ShouldBeTrue();

        browser.Items["k"].ShouldBe("v");
        (await storage.GetAsync("k")).ShouldBe("v");
        (await storage.GetAsync("other")).ShouldBeNull();
        storage.IsPersistent.ShouldBeTrue();
        await storage.RemoveAsync("k");
        browser.Items.ContainsKey("k").ShouldBeFalse();
    }

    [Fact]
    public async Task It_leaves_no_probe_behind()
    {
        var browser = new FakeBrowser();

        await new LocalStorageKeyValueStorage(browser).GetAsync("x");

        browser.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task It_tests_the_browser_once_and_not_on_every_call()
    {
        var browser = new FakeBrowser();
        var storage = new LocalStorageKeyValueStorage(browser);

        await storage.SetAsync("a", "1");
        await storage.SetAsync("b", "2");
        await storage.GetAsync("a");

        browser.Calls.Count(c => c == "localStorage.removeItem").ShouldBe(1);   // the probe, once
    }

    [Fact]
    public async Task A_browser_that_refuses_to_keep_anything_still_lets_the_page_work_and_says_it_is_not_persistent()
    {
        var browser = new FakeBrowser { RefuseWrites = true };
        var storage = new LocalStorageKeyValueStorage(browser);

        (await storage.SetAsync("k", "v")).ShouldBeFalse();

        (await storage.GetAsync("k")).ShouldBe("v");   // for this visit
        storage.IsPersistent.ShouldBeFalse();
        browser.Items.ShouldBeEmpty();
        await storage.RemoveAsync("k");
        (await storage.GetAsync("k")).ShouldBeNull();
    }

    [Fact]
    public async Task A_browser_with_no_localStorage_at_all_reads_nothing_instead_of_failing()
    {
        var storage = new LocalStorageKeyValueStorage(new FakeBrowser { RefuseWrites = true, RefuseReads = true });

        (await storage.GetAsync("k")).ShouldBeNull();
        storage.IsPersistent.ShouldBeFalse();
    }

    [Fact]
    public async Task A_browser_that_refuses_everything_is_not_asked_to_read_what_it_cannot_have()
    {
        var browser = new FakeBrowser { RefuseWrites = true };
        var storage = new LocalStorageKeyValueStorage(browser);

        (await storage.GetAsync("k")).ShouldBeNull();

        browser.Calls.ShouldNotContain("localStorage.getItem");
    }

    [Fact]
    public async Task A_value_too_big_for_the_quota_is_kept_for_this_visit_and_the_rest_is_still_saved()
    {
        var browser = new FakeBrowser { RefuseKey = "big" };
        var storage = new LocalStorageKeyValueStorage(browser);

        (await storage.SetAsync("small", "1")).ShouldBeTrue();
        (await storage.SetAsync("big", "x")).ShouldBeFalse();

        (await storage.GetAsync("big")).ShouldBe("x");
        browser.Items.Keys.ShouldBe(["small"]);
        storage.IsPersistent.ShouldBeFalse();   // something is not kept, and the app should say so

        browser.RefuseKey = null;
        (await storage.SetAsync("big", "y")).ShouldBeTrue();
        browser.Items["big"].ShouldBe("y");
        (await storage.GetAsync("big")).ShouldBe("y");
        storage.IsPersistent.ShouldBeTrue();
    }

    [Fact]
    public async Task What_the_page_could_not_save_wins_over_an_older_saved_value()
    {
        var browser = new FakeBrowser();
        var storage = new LocalStorageKeyValueStorage(browser);
        await storage.SetAsync("k", "old");
        browser.RefuseKey = "k";

        await storage.SetAsync("k", "new");

        (await storage.GetAsync("k")).ShouldBe("new");   // the page must see its own latest value
    }

    [Fact]
    public async Task A_read_that_the_browser_refuses_is_nothing_not_an_error()
    {
        var browser = new FakeBrowser();
        var storage = new LocalStorageKeyValueStorage(browser);
        await storage.SetAsync("k", "v");
        browser.RefuseReads = true;

        (await storage.GetAsync("k")).ShouldBeNull();
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(null, false)]
    public async Task Asking_the_browser_to_keep_the_data_says_whether_it_agreed(bool? answer, bool expected)
    {
        var browser = new FakeBrowser { PersistAnswer = answer };

        (await new LocalStorageKeyValueStorage(browser).RequestPersistenceAsync()).ShouldBe(expected);
    }

    [Fact]
    public async Task The_arguments_are_checked()
    {
        var storage = new LocalStorageKeyValueStorage(new FakeBrowser());

        await Should.ThrowAsync<ArgumentNullException>(async () => await storage.GetAsync(null!));
        await Should.ThrowAsync<ArgumentNullException>(async () => await storage.SetAsync(null!, "x"));
        await Should.ThrowAsync<ArgumentNullException>(async () => await storage.SetAsync("a", null!));
        await Should.ThrowAsync<ArgumentNullException>(async () => await storage.RemoveAsync(null!));
    }
}

public class UserStateStoreTests
{
    private static readonly IReadOnlyDictionary<string, DishPreference> Likes = new Dictionary<string, DishPreference>
    {
        ["canh-chua-ca-loc"] = DishPreference.Liked,
        ["ca-thu-chien"] = DishPreference.Disliked,
    };

    private static (UserStateStore Store, FakeBrowser Browser) Create()
    {
        var browser = new FakeBrowser();
        return (new UserStateStore(new LocalStorageKeyValueStorage(browser)), browser);
    }

    [Fact]
    public async Task What_is_saved_comes_back_the_same()
    {
        var (store, _) = Create();

        await store.SetAsync("preferences.v1", Likes);

        var back = await store.GetAsync<IReadOnlyDictionary<string, DishPreference>>("preferences.v1");
        back.ShouldNotBeNull();
        back.OrderBy(p => p.Key, StringComparer.Ordinal).ShouldBe(Likes.OrderBy(p => p.Key, StringComparer.Ordinal));
    }

    [Fact]
    public async Task A_history_of_meals_survives_a_new_visit()
    {
        var browser = new FakeBrowser();
        var history = new SpinHistory([]);
        await new UserStateStore(new LocalStorageKeyValueStorage(browser)).SetAsync("history.v1", history);

        // A new visit: a new page, the same browser.
        var back = await new UserStateStore(new LocalStorageKeyValueStorage(browser)).GetAsync<SpinHistory>("history.v1");

        back.ShouldNotBeNull();
        back.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task Something_never_saved_is_nothing()
    {
        var (store, _) = Create();

        (await store.GetAsync<SpinHistory>("history.v1")).ShouldBeNull();
    }

    [Fact]
    public async Task Values_live_under_the_prefix_of_the_store()
    {
        var (store, browser) = Create();

        await store.SetAsync("history.v1", SpinHistory.Empty);

        browser.Items.Keys.ShouldContain("mamcom.state.history.v1");
        UserStateStore.Prefix.ShouldBe("mamcom.state.");
    }

    [Fact]
    public async Task A_value_that_cannot_be_read_is_nothing_and_is_not_deleted()
    {
        var (store, browser) = Create();
        browser.Items["mamcom.state.history.v1"] = "{ half written";

        (await store.GetAsync<SpinHistory>("history.v1")).ShouldBeNull();

        browser.Items["mamcom.state.history.v1"].ShouldBe("{ half written");   // the caller decides what to do with it
    }

    [Fact]
    public async Task One_key_is_replaced_by_the_next_save()
    {
        var (store, _) = Create();
        await store.SetAsync("preferences.v1", Likes);

        await store.SetAsync("preferences.v1", (IReadOnlyDictionary<string, DishPreference>)new Dictionary<string, DishPreference> { ["x"] = DishPreference.Neutral });

        (await store.GetAsync<IReadOnlyDictionary<string, DishPreference>>("preferences.v1"))!.Keys.ShouldBe(["x"]);
    }

    [Fact]
    public async Task Removing_a_key_removes_it_and_only_it()
    {
        var (store, browser) = Create();
        await store.SetAsync("history.v1", SpinHistory.Empty);
        await store.SetAsync("preferences.v1", Likes);

        await store.RemoveAsync("history.v1");

        (await store.GetAsync<SpinHistory>("history.v1")).ShouldBeNull();
        (await store.GetAsync<IReadOnlyDictionary<string, DishPreference>>("preferences.v1")).ShouldNotBeNull();
        await store.RemoveAsync("never-saved.v1");   // nothing to do, no error
        browser.Items.Keys.ShouldNotContain("mamcom.state.history.v1");
    }

    [Fact]
    public async Task Clearing_forgets_everything_the_store_saved_and_nothing_else_on_the_device()
    {
        var (store, browser) = Create();
        browser.Items["mamcom.data.current"] = "abc123";   // the version of the data bundle: not the person's data
        browser.Items["other.site.setting"] = "keep";
        await store.SetAsync("history.v1", SpinHistory.Empty);
        await store.SetAsync("preferences.v1", Likes);

        await store.ClearAsync();

        (await store.GetAsync<SpinHistory>("history.v1")).ShouldBeNull();
        browser.Items.Keys.OrderBy(k => k, StringComparer.Ordinal).ShouldBe(["mamcom.data.current", "other.site.setting"]);
    }

    [Fact]
    public async Task Clearing_an_empty_store_is_fine()
    {
        var (store, _) = Create();

        await store.ClearAsync();
        await store.ClearAsync();
    }

    [Fact]
    public async Task A_save_repairs_a_damaged_list_of_keys_so_a_clear_still_finds_what_was_saved_since()
    {
        var (store, browser) = Create();
        browser.Items["mamcom.state.@keys"] = "[not json";

        await store.SetAsync("history.v1", SpinHistory.Empty);
        await store.ClearAsync();

        (await store.GetAsync<SpinHistory>("history.v1")).ShouldBeNull();
        browser.Items.Keys.ShouldNotContain("mamcom.state.@keys");
        browser.Items.Keys.ShouldNotContain("mamcom.state.history.v1");
    }

    [Fact]
    public async Task A_list_of_keys_damaged_after_the_save_does_not_make_a_clear_fail_but_cannot_find_the_value()
    {
        var (store, browser) = Create();
        await store.SetAsync("history.v1", SpinHistory.Empty);
        browser.Items["mamcom.state.@keys"] = "[not json";

        await store.ClearAsync();   // no error

        browser.Items.Keys.ShouldNotContain("mamcom.state.@keys");
        browser.Items.Keys.ShouldContain("mamcom.state.history.v1");   // the one thing the list was for: a known limit, written down in ADR-020
    }

    [Fact]
    public async Task A_save_says_whether_it_will_be_there_next_time()
    {
        var kept = new UserStateStore(new LocalStorageKeyValueStorage(new FakeBrowser()));
        var lost = new UserStateStore(new LocalStorageKeyValueStorage(new FakeBrowser { RefuseWrites = true }));

        (await kept.SetAsync("history.v1", SpinHistory.Empty)).ShouldBeTrue();
        kept.IsPersistent.ShouldBeTrue();
        (await lost.SetAsync("history.v1", SpinHistory.Empty)).ShouldBeFalse();
        lost.IsPersistent.ShouldBeFalse();
        (await lost.GetAsync<SpinHistory>("history.v1")).ShouldNotBeNull();   // it still works for this visit
    }

    [Fact]
    public async Task A_save_that_fits_but_whose_list_of_keys_does_not_is_not_called_saved()
    {
        var browser = new FakeBrowser { RefuseKey = "mamcom.state.@keys" };
        var store = new UserStateStore(new LocalStorageKeyValueStorage(browser));

        (await store.SetAsync("history.v1", SpinHistory.Empty)).ShouldBeFalse();
    }

    [Fact]
    public async Task A_save_that_does_not_fit_is_not_called_saved_even_when_its_list_of_keys_fits()
    {
        var browser = new FakeBrowser { RefuseKey = "mamcom.state.history.v1" };
        var store = new UserStateStore(new LocalStorageKeyValueStorage(browser));

        (await store.SetAsync("history.v1", SpinHistory.Empty)).ShouldBeFalse();
        (await store.GetAsync<SpinHistory>("history.v1")).ShouldNotBeNull();   // for this visit it is there
    }

    [Fact]
    public async Task The_list_of_keys_names_each_key_once_and_in_order()
    {
        var (store, browser) = Create();

        await store.SetAsync("b.v1", SpinHistory.Empty);
        await store.SetAsync("a.v1", SpinHistory.Empty);
        await store.SetAsync("b.v1", SpinHistory.Empty);   // saved again: still one entry

        System.Text.Json.JsonSerializer.Deserialize<string[]>(browser.Items["mamcom.state.@keys"]).ShouldBe(["a.v1", "b.v1"]);
    }

    [Fact]
    public async Task Removing_a_key_takes_it_off_the_list_and_removing_the_last_one_takes_the_list_away()
    {
        var (store, browser) = Create();
        await store.SetAsync("a.v1", SpinHistory.Empty);
        await store.SetAsync("b.v1", SpinHistory.Empty);

        await store.RemoveAsync("a.v1");

        System.Text.Json.JsonSerializer.Deserialize<string[]>(browser.Items["mamcom.state.@keys"]).ShouldBe(["b.v1"]);

        await store.RemoveAsync("b.v1");

        browser.Items.Keys.ShouldNotContain("mamcom.state.@keys");
        browser.Items.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("a\n")]
    [InlineData("History")]
    [InlineData("a b")]
    [InlineData("@keys")]
    [InlineData(".hidden")]
    [InlineData("a/b")]
    [InlineData("tiếng-việt")]
    public async Task A_key_that_is_not_a_plain_lower_case_name_is_refused(string key)
    {
        var (store, _) = Create();

        await Should.ThrowAsync<ArgumentException>(async () => await store.GetAsync<SpinHistory>(key));
        await Should.ThrowAsync<ArgumentException>(async () => await store.SetAsync(key, SpinHistory.Empty));
        await Should.ThrowAsync<ArgumentException>(async () => await store.RemoveAsync(key));
    }

    [Theory]
    [InlineData("household.v1")]
    [InlineData("a")]
    [InlineData("last-meals_2")]
    public async Task A_plain_lower_case_key_with_a_version_is_fine(string key)
    {
        var (store, _) = Create();

        (await store.SetAsync(key, SpinHistory.Empty)).ShouldBeTrue();
    }

    [Fact]
    public async Task A_type_core_does_not_know_how_to_write_is_refused_loudly()
    {
        var (store, _) = Create();

        await Should.ThrowAsync<NotSupportedException>(async () => await store.SetAsync("x.v1", new Unknown()));
        await Should.ThrowAsync<ArgumentNullException>(async () => await store.SetAsync<SpinHistory>("x.v1", null!));
    }

    private sealed record Unknown;
}
