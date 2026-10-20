using System.Net;
using MamCom.Core.Data;
using MamCom.Data.Bundles;
using MamCom.Data.Storage;

namespace MamCom.Data.Tests;

public class BundleLoaderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly string VersionUrl = BundleFiles.Version;

    private static string BundleUrl(DataBundle bundle) => $"{BundleFiles.Bundle}?v={bundle.Version}";

    private static (BundleLoader Loader, MemoryKeyValueStorage Storage) Loader(FakeServer server, BundleLoaderOptions? options = null)
    {
        var storage = new MemoryKeyValueStorage();
        return (new BundleLoader(server.Client(), storage, options), storage);
    }

    // ---- the first visit ------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task The_first_visit_asks_which_version_is_newest_then_takes_that_bundle_and_remembers_it()
    {
        var server = new FakeServer().Publish(Bundles.A);
        var (loader, storage) = Loader(server);

        var result = await loader.LoadAsync(Ct);

        result.Status.ShouldBe(BundleLoadStatus.Loaded);
        result.Bundle!.Version.ShouldBe(Bundles.A.Version);
        result.Bundle.Dishes.Count.ShouldBe(Bundles.A.Dishes.Count);
        result.IsNewVersion.ShouldBeTrue();
        server.Requests.ShouldBe([VersionUrl, BundleUrl(Bundles.A)]);
        (await storage.GetAsync(BundleLoader.CurrentKey)).ShouldBe(Bundles.A.Version);
        (await storage.GetAsync(BundleLoader.PendingKey)).ShouldBeNull();
    }

    [Fact]
    public async Task The_first_visit_without_a_connection_says_a_connection_is_needed_and_remembers_nothing()
    {
        var server = new FakeServer { Offline = true };
        var (loader, storage) = Loader(server);

        var result = await loader.LoadAsync(Ct);

        result.Status.ShouldBe(BundleLoadStatus.NeedsNetwork);
        result.Bundle.ShouldBeNull();
        (await storage.GetAsync(BundleLoader.CurrentKey)).ShouldBeNull();
    }

    [Fact]
    public async Task A_server_that_answers_but_has_no_data_says_it_was_never_published()
    {
        var (loader, _) = Loader(new FakeServer());

        (await loader.LoadAsync(Ct)).Status.ShouldBe(BundleLoadStatus.NotPublished);
    }

    [Fact]
    public async Task A_host_that_answers_every_unknown_path_with_its_start_page_is_still_not_published()
    {
        var server = new FakeServer().Serve(VersionUrl, "<!doctype html><title>MâmCơm</title>", "text/html");
        var (loader, _) = Loader(server);

        (await loader.LoadAsync(Ct)).Status.ShouldBe(BundleLoadStatus.NotPublished);
    }

    [Fact]
    public async Task A_server_that_says_the_file_is_gone_has_nothing_to_give_either()
    {
        var (loader, _) = Loader(new FakeServer().ServeStatus(VersionUrl, HttpStatusCode.Gone));

        (await loader.LoadAsync(Ct)).Status.ShouldBe(BundleLoadStatus.NotPublished);
    }

    [Fact]
    public async Task A_remembered_version_that_is_empty_is_no_version_and_is_not_asked_for()
    {
        var server = new FakeServer().Publish(Bundles.A);
        var (loader, storage) = Loader(server);
        await storage.SetAsync(BundleLoader.CurrentKey, "");
        await storage.SetAsync(BundleLoader.PendingKey, "");

        (await loader.LoadAsync(Ct)).Status.ShouldBe(BundleLoadStatus.Loaded);

        server.Requests.ShouldBe([VersionUrl, BundleUrl(Bundles.A)]);
    }

    [Fact]
    public async Task The_same_version_remembered_as_current_and_as_pending_is_asked_for_once()
    {
        var server = new FakeServer().Cache(Bundles.Damaged);   // the version of A, with the content of B: it cannot be used
        var (loader, storage) = Loader(server);
        await storage.SetAsync(BundleLoader.CurrentKey, Bundles.A.Version);
        await storage.SetAsync(BundleLoader.PendingKey, Bundles.A.Version);

        (await loader.LoadAsync(Ct)).Status.ShouldBe(BundleLoadStatus.NotPublished);   // then the server is asked, and has nothing

        server.Count(BundleUrl(Bundles.A)).ShouldBe(1);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  \n")]
    public async Task A_host_that_answers_a_missing_file_with_nothing_at_all_is_also_not_published(string body)
    {
        // The development server of Blazor does this for a path it has no file for: 200, an empty body, no type.
        var server = new FakeServer().Serve(VersionUrl, body, contentType: "");
        var (loader, _) = Loader(server);

        (await loader.LoadAsync(Ct)).Status.ShouldBe(BundleLoadStatus.NotPublished);
    }

    [Fact]
    public async Task An_empty_answer_in_place_of_the_bundle_of_the_remembered_version_is_not_published_either_and_the_update_check_agrees()
    {
        var bundle = Bundles.A;
        var server = new FakeServer().Serve(VersionUrl, "", contentType: "").Serve(BundleLoader.BundleUrl(bundle.Version), "", contentType: "");
        var (loader, storage) = Loader(server);
        await storage.SetAsync(BundleLoader.CurrentKey, bundle.Version);

        (await loader.LoadAsync(Ct)).Status.ShouldBe(BundleLoadStatus.NotPublished);
        (await loader.CheckForUpdateAsync(Ct)).Status.ShouldBe(BundleUpdateStatus.Failed);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task A_server_that_is_in_trouble_is_not_the_same_as_one_with_nothing_to_give(HttpStatusCode status)
    {
        var (loader, _) = Loader(new FakeServer().ServeStatus(VersionUrl, status));

        (await loader.LoadAsync(Ct)).Status.ShouldBe(BundleLoadStatus.NeedsNetwork);
    }

    [Fact]
    public async Task A_version_file_that_is_not_a_version_file_is_invalid()
    {
        foreach (var body in new[] { "not json", "null", "{}", """{ "version": "", "isRelease": false }""", "[1]"})
        {
            var (loader, _) = Loader(new FakeServer().Serve(VersionUrl, body));

            (await loader.LoadAsync(Ct)).Status.ShouldBe(BundleLoadStatus.Invalid, body);
        }
    }

    [Fact]
    public async Task A_bundle_that_does_not_match_its_own_version_is_refused_and_not_remembered()
    {
        var server = new FakeServer()
            .Serve(VersionUrl, BundleVersionInfo.Of(Bundles.A).ToJson())
            .Serve(BundleUrl(Bundles.A), Bundles.Damaged.ToJson());
        var (loader, storage) = Loader(server);

        var result = await loader.LoadAsync(Ct);

        result.Status.ShouldBe(BundleLoadStatus.Invalid);
        result.Bundle.ShouldBeNull();
        (await storage.GetAsync(BundleLoader.CurrentKey)).ShouldBeNull();
    }

    [Theory]
    [InlineData("this is not json")]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("""{ "version": "x" }""")]
    public async Task Something_that_is_not_a_bundle_is_refused(string body)
    {
        var server = new FakeServer()
            .Serve(VersionUrl, BundleVersionInfo.Of(Bundles.A).ToJson())
            .Serve(BundleUrl(Bundles.A), body);
        var (loader, _) = Loader(server);

        (await loader.LoadAsync(Ct)).Status.ShouldBe(BundleLoadStatus.Invalid);
    }

    [Fact]
    public async Task A_bundle_whose_version_is_empty_is_refused()
    {
        var empty = Bundles.A with { Version = string.Empty };
        var server = new FakeServer()
            .Serve(VersionUrl, BundleVersionInfo.Of(Bundles.A).ToJson())
            .Serve(BundleUrl(Bundles.A), empty.ToJson());
        var (loader, _) = Loader(server);

        (await loader.LoadAsync(Ct)).Status.ShouldBe(BundleLoadStatus.Invalid);
    }

    [Fact]
    public async Task A_version_that_is_named_but_whose_bundle_cannot_be_had_needs_a_connection()
    {
        var server = new FakeServer().Serve(VersionUrl, BundleVersionInfo.Of(Bundles.A).ToJson());   // no bundle: 404
        var (loader, _) = Loader(server);

        (await loader.LoadAsync(Ct)).Status.ShouldBe(BundleLoadStatus.NotPublished);

        server.ServeStatus(BundleUrl(Bundles.A), HttpStatusCode.ServiceUnavailable);
        (await loader.LoadAsync(Ct)).Status.ShouldBe(BundleLoadStatus.NeedsNetwork);
    }

    // ---- the next visits, with or without a connection ----------------------------------------------------------------------

    [Fact]
    public async Task A_device_that_has_seen_a_version_opens_it_from_the_cache_and_asks_the_server_nothing()
    {
        var server = new FakeServer().Publish(Bundles.A);
        var (loader, _) = Loader(server);
        await loader.LoadAsync(Ct);
        server.Requests.Clear();

        var again = await loader.LoadAsync(Ct);

        again.Status.ShouldBe(BundleLoadStatus.Loaded);
        again.IsNewVersion.ShouldBeFalse();
        server.Requests.ShouldBe([BundleUrl(Bundles.A)]);   // not even the version file: nothing waits for the network
    }

    [Fact]
    public async Task Offline_after_the_first_visit_the_app_still_opens()
    {
        var server = new FakeServer().Publish(Bundles.A);
        var (loader, _) = Loader(server);
        await loader.LoadAsync(Ct);

        // Now the network is gone. Only the cache (the service worker) answers the bundle's own URL.
        var offline = new FakeServer { Offline = false }.Cache(Bundles.A);
        var storage = new MemoryKeyValueStorage();
        await storage.SetAsync(BundleLoader.CurrentKey, Bundles.A.Version);
        var reopened = new BundleLoader(offline.Client(), storage);

        var result = await reopened.LoadAsync(Ct);

        result.Status.ShouldBe(BundleLoadStatus.Loaded);
        result.Bundle!.Dishes.Select(d => d.Id).ShouldBe(Bundles.A.Dishes.Select(d => d.Id));
        offline.Requests.ShouldBe([BundleUrl(Bundles.A)]);
    }

    [Fact]
    public async Task A_remembered_version_that_the_cache_lost_is_fetched_again_when_there_is_a_connection()
    {
        var server = new FakeServer().Publish(Bundles.A);
        var (loader, storage) = Loader(server);
        await storage.SetAsync(BundleLoader.CurrentKey, Bundles.A.Version);
        server.Forget(BundleUrl(Bundles.A));   // the cache and the server have lost this URL

        server.Publish(Bundles.B);
        var result = await loader.LoadAsync(Ct);

        result.Status.ShouldBe(BundleLoadStatus.Loaded);
        result.Bundle!.Version.ShouldBe(Bundles.B.Version);
        result.IsNewVersion.ShouldBeTrue();
        (await storage.GetAsync(BundleLoader.CurrentKey)).ShouldBe(Bundles.B.Version);
        server.Requests.ShouldBe([BundleUrl(Bundles.A), VersionUrl, BundleUrl(Bundles.B)]);
    }

    [Fact]
    public async Task A_remembered_version_that_the_cache_lost_and_no_connection_needs_a_connection()
    {
        var server = new FakeServer { Offline = true };
        var (loader, storage) = Loader(server);
        await storage.SetAsync(BundleLoader.CurrentKey, Bundles.A.Version);

        var result = await loader.LoadAsync(Ct);

        result.Status.ShouldBe(BundleLoadStatus.NeedsNetwork);
        (await storage.GetAsync(BundleLoader.CurrentKey)).ShouldBe(Bundles.A.Version);   // still remembered for when the cache is back
    }

    [Fact]
    public async Task A_server_that_gives_the_newest_bundle_for_an_old_address_is_trusted_if_the_bundle_is_good()
    {
        // A host with no service worker ignores the ?v= and always answers with the current bundle.
        var server = new FakeServer().Serve(BundleUrl(Bundles.A), Bundles.B.ToJson());
        var (loader, storage) = Loader(server);
        await storage.SetAsync(BundleLoader.CurrentKey, Bundles.A.Version);

        var result = await loader.LoadAsync(Ct);

        result.Bundle!.Version.ShouldBe(Bundles.B.Version);
        result.IsNewVersion.ShouldBeTrue();
        (await storage.GetAsync(BundleLoader.CurrentKey)).ShouldBe(Bundles.B.Version);
    }

    // ---- news ------------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_newer_bundle_is_downloaded_in_the_background_kept_pending_and_taken_in_at_the_next_load()
    {
        var server = new FakeServer().Publish(Bundles.A);
        var (loader, storage) = Loader(server);
        await loader.LoadAsync(Ct);

        server.Publish(Bundles.B);
        var update = await loader.CheckForUpdateAsync(Ct);

        update.ShouldBe(new BundleUpdateResult(BundleUpdateStatus.Ready, Bundles.B.Version));
        (await storage.GetAsync(BundleLoader.PendingKey)).ShouldBe(Bundles.B.Version);
        (await storage.GetAsync(BundleLoader.CurrentKey)).ShouldBe(Bundles.A.Version);   // nothing changes under the person's feet

        server.Requests.Clear();
        var next = await loader.LoadAsync(Ct);

        next.Bundle!.Version.ShouldBe(Bundles.B.Version);
        next.IsNewVersion.ShouldBeTrue();
        (await storage.GetAsync(BundleLoader.CurrentKey)).ShouldBe(Bundles.B.Version);
        (await storage.GetAsync(BundleLoader.PendingKey)).ShouldBeNull();
        server.Requests.ShouldBe([BundleUrl(Bundles.B)]);
    }

    [Fact]
    public async Task Looking_again_for_the_same_news_does_not_download_it_twice()
    {
        var server = new FakeServer().Publish(Bundles.A);
        var (loader, _) = Loader(server);
        await loader.LoadAsync(Ct);
        server.Publish(Bundles.B);
        await loader.CheckForUpdateAsync(Ct);

        var again = await loader.CheckForUpdateAsync(Ct);

        again.Status.ShouldBe(BundleUpdateStatus.Ready);
        server.Count(BundleUrl(Bundles.B)).ShouldBe(1);
    }

    [Fact]
    public async Task When_there_is_nothing_new_the_check_is_one_small_request()
    {
        var server = new FakeServer().Publish(Bundles.A);
        var (loader, _) = Loader(server);
        await loader.LoadAsync(Ct);
        server.Requests.Clear();

        var update = await loader.CheckForUpdateAsync(Ct);

        update.ShouldBe(new BundleUpdateResult(BundleUpdateStatus.UpToDate, Bundles.A.Version));
        server.Requests.ShouldBe([VersionUrl]);
    }

    [Fact]
    public async Task A_server_that_went_back_to_the_version_the_device_holds_cancels_the_pending_one()
    {
        var server = new FakeServer().Publish(Bundles.A);
        var (loader, storage) = Loader(server);
        await loader.LoadAsync(Ct);
        server.Publish(Bundles.B);
        await loader.CheckForUpdateAsync(Ct);
        server.Publish(Bundles.A);

        var update = await loader.CheckForUpdateAsync(Ct);

        update.Status.ShouldBe(BundleUpdateStatus.UpToDate);
        (await storage.GetAsync(BundleLoader.PendingKey)).ShouldBeNull();
    }

    [Fact]
    public async Task A_third_version_replaces_the_pending_second_one()
    {
        var server = new FakeServer().Publish(Bundles.A);
        var (loader, storage) = Loader(server);
        await loader.LoadAsync(Ct);
        server.Publish(Bundles.B);
        await loader.CheckForUpdateAsync(Ct);

        server.Publish(Bundles.C);
        var update = await loader.CheckForUpdateAsync(Ct);

        update.ShouldBe(new BundleUpdateResult(BundleUpdateStatus.Ready, Bundles.C.Version));
        (await storage.GetAsync(BundleLoader.PendingKey)).ShouldBe(Bundles.C.Version);
    }

    [Fact]
    public async Task Offline_the_check_changes_nothing_and_says_the_server_is_out_of_reach()
    {
        var server = new FakeServer().Publish(Bundles.A);
        var (loader, storage) = Loader(server);
        await loader.LoadAsync(Ct);
        server.Offline = true;

        var update = await loader.CheckForUpdateAsync(Ct);

        update.ShouldBe(new BundleUpdateResult(BundleUpdateStatus.Unreachable, null));
        (await storage.GetAsync(BundleLoader.PendingKey)).ShouldBeNull();
        (await storage.GetAsync(BundleLoader.CurrentKey)).ShouldBe(Bundles.A.Version);
    }

    [Fact]
    public async Task A_bundle_that_cannot_be_downloaded_leaves_no_pending_version()
    {
        var server = new FakeServer().Publish(Bundles.A);
        var (loader, storage) = Loader(server);
        await loader.LoadAsync(Ct);
        server.Serve(VersionUrl, BundleVersionInfo.Of(Bundles.B).ToJson());   // named, but not served

        (await loader.CheckForUpdateAsync(Ct)).Status.ShouldBe(BundleUpdateStatus.Failed);
        server.ServeStatus(BundleUrl(Bundles.B), HttpStatusCode.ServiceUnavailable);
        (await loader.CheckForUpdateAsync(Ct)).Status.ShouldBe(BundleUpdateStatus.Unreachable);
        (await storage.GetAsync(BundleLoader.PendingKey)).ShouldBeNull();
    }

    [Fact]
    public async Task A_damaged_download_is_never_kept_as_pending()
    {
        var server = new FakeServer().Publish(Bundles.A);
        var (loader, storage) = Loader(server);
        await loader.LoadAsync(Ct);
        server.Serve(VersionUrl, BundleVersionInfo.Of(Bundles.B).ToJson());
        server.Serve(BundleUrl(Bundles.B), Bundles.Damaged.ToJson());

        (await loader.CheckForUpdateAsync(Ct)).Status.ShouldBe(BundleUpdateStatus.Failed);
        (await storage.GetAsync(BundleLoader.PendingKey)).ShouldBeNull();
    }

    [Fact]
    public async Task A_version_file_that_is_garbage_fails_the_check_without_touching_anything()
    {
        var server = new FakeServer().Publish(Bundles.A);
        var (loader, storage) = Loader(server);
        await loader.LoadAsync(Ct);
        server.Serve(VersionUrl, "<html>");

        (await loader.CheckForUpdateAsync(Ct)).Status.ShouldBe(BundleUpdateStatus.Failed);
        (await storage.GetAsync(BundleLoader.CurrentKey)).ShouldBe(Bundles.A.Version);
    }

    [Fact]
    public async Task A_server_that_moved_on_between_the_two_requests_gives_what_it_gives()
    {
        var server = new FakeServer().Publish(Bundles.A);
        var (loader, storage) = Loader(server);
        await loader.LoadAsync(Ct);
        server.Serve(VersionUrl, BundleVersionInfo.Of(Bundles.B).ToJson());
        server.Serve(BundleUrl(Bundles.B), Bundles.C.ToJson());   // named B, but C is what comes

        var update = await loader.CheckForUpdateAsync(Ct);

        update.ShouldBe(new BundleUpdateResult(BundleUpdateStatus.Ready, Bundles.C.Version));
        (await storage.GetAsync(BundleLoader.PendingKey)).ShouldBe(Bundles.C.Version);
    }

    [Fact]
    public async Task A_server_whose_bundle_is_the_one_the_device_holds_is_up_to_date_whatever_the_version_file_says()
    {
        var server = new FakeServer().Publish(Bundles.A);
        var (loader, storage) = Loader(server);
        await loader.LoadAsync(Ct);
        server.Serve(VersionUrl, BundleVersionInfo.Of(Bundles.B).ToJson());
        server.Serve(BundleUrl(Bundles.B), Bundles.A.ToJson());

        (await loader.CheckForUpdateAsync(Ct)).Status.ShouldBe(BundleUpdateStatus.UpToDate);
        (await storage.GetAsync(BundleLoader.PendingKey)).ShouldBeNull();
    }

    [Fact]
    public async Task A_pending_bundle_that_turns_out_to_be_damaged_is_forgotten_and_the_current_one_is_used()
    {
        var server = new FakeServer().Publish(Bundles.A);
        var (loader, storage) = Loader(server);
        await loader.LoadAsync(Ct);
        await storage.SetAsync(BundleLoader.PendingKey, Bundles.B.Version);
        server.Serve(BundleUrl(Bundles.B), Bundles.Damaged.ToJson());

        var result = await loader.LoadAsync(Ct);

        result.Bundle!.Version.ShouldBe(Bundles.A.Version);
        (await storage.GetAsync(BundleLoader.PendingKey)).ShouldBeNull();
    }

    [Fact]
    public async Task A_pending_bundle_that_the_cache_cannot_give_leaves_pending_for_later_and_opens_the_current_one()
    {
        var server = new FakeServer().Publish(Bundles.A);
        var (loader, storage) = Loader(server);
        await loader.LoadAsync(Ct);
        await storage.SetAsync(BundleLoader.PendingKey, Bundles.B.Version);   // B is not served: 404

        var result = await loader.LoadAsync(Ct);

        result.Bundle!.Version.ShouldBe(Bundles.A.Version);
        result.IsNewVersion.ShouldBeFalse();
        (await storage.GetAsync(BundleLoader.PendingKey)).ShouldBe(Bundles.B.Version);
    }

    // ---- the shape of the requests ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task The_version_file_is_asked_for_fresh_and_the_bundle_by_an_address_of_its_own_version()
    {
        var server = new FakeServer().Publish(Bundles.A);
        var (loader, _) = Loader(server);

        await loader.LoadAsync(Ct);

        server.Seen[0].Headers.CacheControl!.NoCache.ShouldBeTrue();
        server.Seen.ShouldAllBe(r => r.Method == HttpMethod.Get);
        BundleLoader.BundleUrl("abc 123").ShouldBe("data-bundle.json?v=abc%20123");
        BundleLoader.BundleUrl(Bundles.A.Version).ShouldBe($"data-bundle.json?v={Bundles.A.Version}");
    }

    [Fact]
    public async Task A_server_that_does_not_answer_in_time_is_out_of_reach()
    {
        var server = new FakeServer { Delay = TimeSpan.FromSeconds(30) };
        var (loader, _) = Loader(server, new BundleLoaderOptions(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(50)));

        var started = DateTime.UtcNow;
        var result = await loader.LoadAsync(Ct);

        result.Status.ShouldBe(BundleLoadStatus.NeedsNetwork);
        (DateTime.UtcNow - started).ShouldBeLessThan(TimeSpan.FromSeconds(10));
        (await loader.CheckForUpdateAsync(Ct)).Status.ShouldBe(BundleUpdateStatus.Unreachable);
    }

    [Fact]
    public async Task The_bundle_gets_its_own_longer_time_than_the_version_file()
    {
        var options = BundleLoaderOptions.Default;

        options.VersionTimeout.ShouldBeLessThan(options.BundleTimeout);
        options.VersionTimeout.ShouldBeGreaterThan(TimeSpan.Zero);

        var server = new FakeServer { Delay = TimeSpan.FromMilliseconds(150) }.Publish(Bundles.A);
        var (loader, _) = Loader(server, new BundleLoaderOptions(TimeSpan.FromMilliseconds(30), TimeSpan.FromSeconds(30)));
        (await loader.LoadAsync(Ct)).Status.ShouldBe(BundleLoadStatus.NeedsNetwork);   // the version file waited too little

        var (patient, _) = Loader(server, new BundleLoaderOptions(TimeSpan.FromSeconds(30), TimeSpan.FromMilliseconds(30)));
        (await patient.LoadAsync(Ct)).Status.ShouldBe(BundleLoadStatus.NeedsNetwork);   // the bundle waited too little
    }

    [Fact]
    public async Task Cancelling_is_not_a_failed_load_but_the_end_of_it()
    {
        var server = new FakeServer { Delay = TimeSpan.FromSeconds(30) };
        var (loader, _) = Loader(server);
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Should.ThrowAsync<OperationCanceledException>(() => loader.LoadAsync(cancel.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => loader.CheckForUpdateAsync(cancel.Token));
    }
}
