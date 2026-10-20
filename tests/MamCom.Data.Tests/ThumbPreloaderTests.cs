using MamCom.Core.Data;
using MamCom.Data.Bundles;

namespace MamCom.Data.Tests;

public class ThumbPreloaderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void The_thumbs_of_a_bundle_are_each_named_once_in_order_and_the_full_pictures_are_not_among_them()
    {
        var bundle = Bundles.WithThumbs("b-200.webp", "a-200.webp", "b-200.webp");

        var thumbs = ThumbPreloader.ThumbsOf(bundle);

        thumbs.ShouldBe(["a-200.webp", "b-200.webp"]);
    }

    [Fact]
    public void A_bundle_with_no_pictures_has_no_thumbs()
    {
        ThumbPreloader.ThumbsOf(Bundles.A).ShouldBeEmpty();
    }

    [Fact]
    public void Ingredients_count_as_well_as_dishes()
    {
        var withIngredient = Bundles.A with
        {
            Ingredients = [.. Bundles.A.Ingredients.Select((i, n) => n == 0 ? i with { Image = i.Image with { Thumb = "i-200.webp", Source = "t", License = "c" } } : i)],
        };

        ThumbPreloader.ThumbsOf(withIngredient).ShouldBe(["i-200.webp"]);
    }

    [Fact]
    public async Task Every_thumb_is_asked_for_under_the_images_folder_and_counted()
    {
        var bundle = Bundles.WithThumbs("a-200.webp", "b-200.webp", "c-200.webp");
        var server = new FakeServer();
        foreach (var name in new[] { "a-200.webp", "b-200.webp", "c-200.webp" })
        {
            server.Serve($"images/{name}", "webp-bytes", "image/webp");
        }

        var fetched = await new ThumbPreloader(server.Client()).PreloadAsync(bundle, cancellationToken: Ct);

        fetched.ShouldBe(3);
        server.Requests.Order(StringComparer.Ordinal).ShouldBe(["images/a-200.webp", "images/b-200.webp", "images/c-200.webp"]);
    }

    [Fact]
    public async Task Each_request_says_it_is_only_to_fill_the_cache_so_the_service_worker_does_not_renew_what_it_has()
    {
        var server = new FakeServer().Serve("images/a-200.webp", "x");

        await new ThumbPreloader(server.Client()).PreloadAsync(Bundles.WithThumbs("a-200.webp"), cancellationToken: Ct);

        server.Seen.Single().Headers.GetValues("X-MamCom-Preload").ShouldBe(["1"]);
        ThumbPreloader.PreloadHeader.ShouldBe("X-MamCom-Preload");
    }

    [Fact]
    public async Task A_picture_that_is_missing_or_cannot_be_had_is_skipped_and_the_rest_still_come()
    {
        var bundle = Bundles.WithThumbs("a-200.webp", "b-200.webp", "c-200.webp");
        var server = new FakeServer().Serve("images/a-200.webp", "x").ServeStatus("images/b-200.webp", System.Net.HttpStatusCode.NotFound);

        var fetched = await new ThumbPreloader(server.Client()).PreloadAsync(bundle, cancellationToken: Ct);

        fetched.ShouldBe(1);   // b is missing, c is not served either
        server.Requests.Count.ShouldBe(3);
    }

    [Fact]
    public async Task Offline_nothing_comes_and_nothing_fails()
    {
        var server = new FakeServer { Offline = true };

        var fetched = await new ThumbPreloader(server.Client()).PreloadAsync(Bundles.WithThumbs("a-200.webp"), cancellationToken: Ct);

        fetched.ShouldBe(0);
    }

    [Fact]
    public async Task Only_a_few_are_asked_for_at_a_time()
    {
        var thumbs = Enumerable.Range(0, 12).Select(i => $"t{i:00}-200.webp").ToArray();
        var gate = new ConcurrencyGate();
        var http = new HttpClient(gate) { BaseAddress = new Uri("https://mamcom.test/") };

        await new ThumbPreloader(http).PreloadAsync(Bundles.WithThumbs(thumbs), parallelism: 3, cancellationToken: Ct);

        gate.MostAtOnce.ShouldBeLessThanOrEqualTo(3);
        gate.MostAtOnce.ShouldBeGreaterThan(1);   // and they are not done one by one either
    }

    [Fact]
    public async Task Cancelling_stops_the_preload()
    {
        var server = new FakeServer { Delay = TimeSpan.FromSeconds(30) };
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Should.ThrowAsync<OperationCanceledException>(() => new ThumbPreloader(server.Client()).PreloadAsync(Bundles.WithThumbs("a-200.webp"), cancellationToken: cancel.Token));
    }

    [Fact]
    public void A_name_is_made_safe_for_an_address_and_the_arguments_are_checked()
    {
        ThumbPreloader.ThumbUrl("gao-200.webp").ShouldBe("images/gao-200.webp");
        ThumbPreloader.ThumbUrl("a b.webp").ShouldBe("images/a%20b.webp");
        Should.Throw<ArgumentNullException>(() => ThumbPreloader.ThumbsOf(null!));
        Should.Throw<ArgumentOutOfRangeException>(async () => await new ThumbPreloader(new FakeServer().Client()).PreloadAsync(Bundles.A, parallelism: 0, cancellationToken: Ct));
    }

    [Fact]
    public async Task The_whole_body_of_each_picture_is_read_so_the_cache_gets_all_of_it()
    {
        var body = new TrackedContent();
        var http = new HttpClient(new Reply(body)) { BaseAddress = new Uri("https://mamcom.test/") };

        var fetched = await new ThumbPreloader(http).PreloadAsync(Bundles.WithThumbs("a-200.webp"), cancellationToken: Ct);

        fetched.ShouldBe(1);
        body.WasRead.ShouldBeTrue();
    }

    private sealed class TrackedContent : HttpContent
    {
        public bool WasRead { get; private set; }

        protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context)
        {
            WasRead = true;
            return stream.WriteAsync(new byte[] { 1, 2, 3 }).AsTask();
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 3;
            return true;
        }
    }

    private sealed class Reply(HttpContent content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = content });
    }

    private sealed class ConcurrencyGate : HttpMessageHandler
    {
        private int _now;
        private int _most;

        public int MostAtOnce => _most;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var now = Interlocked.Increment(ref _now);
            int seen;
            while ((seen = _most) < now && Interlocked.CompareExchange(ref _most, now, seen) != seen)
            {
            }

            await Task.Delay(40, cancellationToken);
            Interlocked.Decrement(ref _now);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("x") };
        }
    }
}
