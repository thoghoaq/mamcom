using MamCom.Core.Data;
using MamCom.Data.Bundles;

namespace MamCom.Data.Tests;

public class AppDataTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string BundleUrl(DataBundle bundle) => $"{BundleFiles.Bundle}?v={bundle.Version}";

    private static AppData Create(FakeServer server, SpyStorage storage)
    {
        var http = server.Client();
        return new AppData(new BundleLoader(http, storage), new ThumbPreloader(http), storage);
    }

    [Fact]
    public void Before_anything_is_loaded_the_app_is_loading_and_has_no_bundle()
    {
        var data = Create(new FakeServer(), new SpyStorage());

        data.IsLoading.ShouldBeTrue();
        data.Bundle.ShouldBeNull();
        data.LoadResult.ShouldBeNull();
        data.UpdateReady.ShouldBeFalse();
    }

    [Fact]
    public async Task A_development_bundle_is_trial_data_a_release_is_not_and_both_get_the_background_work()
    {
        var release = (Bundles.A with { IsRelease = true }).WithComputedVersion();
        var trialStorage = new SpyStorage();
        var finalStorage = new SpyStorage();
        var trial = Create(new FakeServer().Publish(Bundles.A), trialStorage);
        var final = Create(new FakeServer().Publish(release), finalStorage);
        trial.Status.ShouldBeNull();
        trial.IsTrialData.ShouldBeFalse();   // nothing loaded: nothing to call trial data

        await trial.InitializeAsync(Ct);
        await final.InitializeAsync(Ct);
        await trial.BackgroundWork;
        await final.BackgroundWork;

        trial.Status.ShouldBe(BundleLoadStatus.Loaded);
        trial.IsTrialData.ShouldBeTrue();
        final.Status.ShouldBe(BundleLoadStatus.Loaded);
        final.IsTrialData.ShouldBeFalse();
        trialStorage.PersistenceRequests.ShouldBe(1);
        finalStorage.PersistenceRequests.ShouldBe(1);
    }

    [Fact]
    public async Task The_screens_are_told_when_the_bundle_is_loaded_and_again_when_it_is_known_whether_the_device_keeps_it()
    {
        var storage = new SpyStorage { Durable = true };
        var data = Create(new FakeServer().Publish(Bundles.A), storage);
        var seen = new List<(bool IsLoading, int PersistenceRequests)>();
        data.Changed += () => seen.Add((data.IsLoading, storage.PersistenceRequests));

        await data.InitializeAsync(Ct);
        await data.BackgroundWork;

        seen.ShouldBe([(false, 0), (false, 1)]);   // loaded; then the browser has been asked. Nothing else changed.
    }

    [Fact]
    public async Task Initializing_loads_the_bundle_and_tells_the_screens()
    {
        var data = Create(new FakeServer().Publish(Bundles.A), new SpyStorage());
        var changes = 0;
        data.Changed += () => changes++;

        await data.InitializeAsync(Ct);

        data.IsLoading.ShouldBeFalse();
        data.Bundle!.Version.ShouldBe(Bundles.A.Version);
        data.LoadResult!.Status.ShouldBe(BundleLoadStatus.Loaded);
        changes.ShouldBeGreaterThan(0);
        await data.BackgroundWork;
    }

    [Fact]
    public async Task Initializing_twice_loads_once()
    {
        var server = new FakeServer().Publish(Bundles.A);
        var data = Create(server, new SpyStorage());

        var first = data.InitializeAsync(Ct);
        var second = data.InitializeAsync(Ct);
        await Task.WhenAll(first, second);
        await data.BackgroundWork;

        second.ShouldBeSameAs(first);
        server.Count(BundleUrl(Bundles.A)).ShouldBe(1);
    }

    [Fact]
    public async Task The_first_screen_can_draw_before_the_news_are_looked_for_and_the_thumbs_asked_for()
    {
        var withThumb = Bundles.WithThumbs("a-200.webp");
        var server = new FakeServer().Publish(withThumb).Serve("images/a-200.webp", "x");
        var data = Create(server, new SpyStorage());
        string[]? atFirstDraw = null;
        data.Changed += () => atFirstDraw ??= [.. server.Requests];   // what has been asked when the screens are first told

        await data.InitializeAsync(Ct);
        await data.BackgroundWork;

        atFirstDraw.ShouldBe([BundleFiles.Version, BundleUrl(withThumb)]);
        server.Requests.ShouldContain("images/a-200.webp");
    }

    [Fact]
    public async Task After_a_load_the_browser_is_asked_to_keep_the_data_and_each_thumb_is_kept_once()
    {
        var withThumbs = Bundles.WithThumbs("a-200.webp", "b-200.webp");
        var server = new FakeServer().Publish(withThumbs).Serve("images/a-200.webp", "x").Serve("images/b-200.webp", "y");
        var storage = new SpyStorage();
        var data = Create(server, storage);

        await data.InitializeAsync(Ct);
        await data.BackgroundWork;

        storage.PersistenceRequests.ShouldBe(1);
        server.Count("images/a-200.webp").ShouldBe(1);
        server.Count("images/b-200.webp").ShouldBe(1);
    }

    [Fact]
    public async Task A_newer_bundle_is_found_in_the_background_while_the_old_one_stays_on_screen()
    {
        var server = new FakeServer().Publish(Bundles.A);
        var storage = new SpyStorage();
        await Create(server, storage).InitializeAsync(Ct);   // an earlier visit: the device now holds A
        server.Publish(Bundles.B);
        var data = Create(server, storage);
        var seen = new List<(string? Version, bool UpdateReady)>();
        data.Changed += () => seen.Add((data.Bundle?.Version, data.UpdateReady));

        await data.InitializeAsync(Ct);
        await data.BackgroundWork;

        seen[0].ShouldBe((Bundles.A.Version, false));   // opened from what the device holds, and nothing pending yet
        seen[^1].ShouldBe((Bundles.A.Version, true));   // the news came after, and A is still what is on screen
        seen.Select(s => s.Version).Distinct().ShouldBe([Bundles.A.Version]);
    }

    [Fact]
    public async Task Taking_the_update_in_switches_the_bundle_and_clears_the_flag()
    {
        var server = new FakeServer().Publish(Bundles.A);
        var storage = new SpyStorage();
        await Create(server, storage).InitializeAsync(Ct);
        server.Publish(Bundles.B);
        var data = Create(server, storage);
        await data.InitializeAsync(Ct);
        await data.BackgroundWork;
        var changes = 0;
        data.Changed += () => changes++;

        await data.ApplyUpdateAsync(Ct);

        data.Bundle!.Version.ShouldBe(Bundles.B.Version);
        data.LoadResult!.IsNewVersion.ShouldBeTrue();
        data.UpdateReady.ShouldBeFalse();
        changes.ShouldBe(1);
    }

    [Fact]
    public async Task Taking_the_update_in_when_the_server_cannot_be_reached_keeps_what_is_on_screen()
    {
        var server = new FakeServer().Publish(Bundles.A);
        var storage = new SpyStorage();
        var data = Create(server, storage);
        await data.InitializeAsync(Ct);
        await data.BackgroundWork;
        server.Forget(BundleUrl(Bundles.A));
        server.Offline = true;
        var changes = 0;
        data.Changed += () => changes++;

        await data.ApplyUpdateAsync(Ct);

        data.Bundle!.Version.ShouldBe(Bundles.A.Version);
        changes.ShouldBe(0);
    }

    [Fact]
    public async Task When_the_server_cannot_be_reached_the_news_are_not_looked_for_and_the_thumbs_are_not_asked_for()
    {
        var withThumb = Bundles.WithThumbs("a-200.webp");
        var storage = new SpyStorage();
        await Create(new FakeServer().Publish(withThumb), storage).InitializeAsync(Ct);   // an earlier visit: the device holds this bundle
        var offline = new FakeServer().Cache(withThumb).ServeStatus(BundleFiles.Version, System.Net.HttpStatusCode.ServiceUnavailable);
        var data = Create(offline, storage);

        await data.InitializeAsync(Ct);
        await data.BackgroundWork;

        data.Bundle!.Version.ShouldBe(withThumb.Version);
        data.UpdateReady.ShouldBeFalse();
        offline.Requests.ShouldBe([BundleUrl(withThumb), BundleFiles.Version]);   // and no thumb was asked for
    }

    [Fact]
    public async Task A_device_with_no_copy_and_no_connection_has_nothing_to_show_and_does_no_background_work()
    {
        var server = new FakeServer { Offline = true };
        var storage = new SpyStorage();
        var data = Create(server, storage);

        await data.InitializeAsync(Ct);
        await data.BackgroundWork;

        data.IsLoading.ShouldBeFalse();
        data.Bundle.ShouldBeNull();
        data.LoadResult!.Status.ShouldBe(BundleLoadStatus.NeedsNetwork);
        storage.PersistenceRequests.ShouldBe(0);
    }

    [Fact]
    public void Whether_the_device_keeps_what_it_is_told_comes_from_the_storage()
    {
        var kept = new SpyStorage { Durable = true };
        var lost = new SpyStorage { Durable = false };

        Create(new FakeServer(), kept).IsPersistent.ShouldBeTrue();
        Create(new FakeServer(), lost).IsPersistent.ShouldBeFalse();
    }

    [Fact]
    public async Task Leaving_the_page_in_the_middle_of_the_background_work_is_not_an_error()
    {
        var server = new FakeServer().Publish(Bundles.A);
        var storage = new SpyStorage();
        using var cancel = new CancellationTokenSource();
        storage.OnPersistenceRequested = cancel.Cancel;   // the page goes away right after the load
        var data = Create(server, storage);

        await data.InitializeAsync(cancel.Token);

        await data.BackgroundWork;   // completes, does not throw
        data.Bundle.ShouldNotBeNull();
        data.UpdateReady.ShouldBeFalse();
    }
}
