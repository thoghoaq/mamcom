using System.Net;
using MamCom.FdcImporter.Fdc;

namespace MamCom.FdcImporter.Tests;

public class FdcClientTests
{
    private const string Key = "SECRET-KEY-123";

    private static FdcClient Client(FakeFdc fake, string key = Key) => new(new HttpClient(fake), key);

    private static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    // ---- the request -----------------------------------------------------------------------------------------------

    [Fact]
    public async Task The_request_searches_foundation_and_sr_legacy_foods_with_the_key_in_a_header()
    {
        var fake = FakeFdc.Returning(Foods.Search(0));

        await Client(fake).SearchAsync("rice white raw", cancellationToken: TestContext.Current.CancellationToken);

        var request = fake.Requests.ShouldHaveSingleItem();
        request.Uri.GetLeftPart(UriPartial.Path).ShouldBe("https://api.nal.usda.gov/fdc/v1/foods/search");
        request.ApiKey.ShouldBe(Key);
        var query = System.Web.HttpUtility.ParseQueryString(request.Uri.Query);
        query["query"].ShouldBe("rice white raw");
        query.GetValues("dataType").ShouldBe(["Foundation", "SR Legacy"]);
        query["pageSize"].ShouldBe("25");
    }

    [Fact]
    public async Task The_key_never_appears_in_the_url()
    {
        var fake = FakeFdc.Returning(Foods.Search(0));

        await Client(fake).SearchAsync("rice", cancellationToken: TestContext.Current.CancellationToken);

        fake.Requests.Single().Uri.ToString().ShouldNotContain(Key);
        fake.Requests.Single().Uri.ToString().ShouldNotContain("api_key");
    }

    [Fact]
    public async Task A_query_with_special_characters_arrives_unchanged()
    {
        var fake = FakeFdc.Returning(Foods.Search(0));
        const string query = "pork, fresh & raw (belly) 100% café";

        await Client(fake).SearchAsync(query, cancellationToken: TestContext.Current.CancellationToken);

        fake.Requests.Single().Query.ShouldBe(query);
    }

    [Fact]
    public async Task The_page_size_can_be_chosen()
    {
        var fake = FakeFdc.Returning(Foods.Search(0));

        await Client(fake).SearchAsync("rice", pageSize: 7, cancellationToken: TestContext.Current.CancellationToken);

        System.Web.HttpUtility.ParseQueryString(fake.Requests.Single().Uri.Query)["pageSize"].ShouldBe("7");
    }

    [Fact]
    public async Task Bad_arguments_are_refused_before_anything_is_sent()
    {
        var fake = FakeFdc.Returning(Foods.Search(0));

        Should.Throw<ArgumentException>(() => new FdcClient(new HttpClient(fake), " "));
        Should.Throw<ArgumentNullException>(() => new FdcClient(null!, Key));
        await Should.ThrowAsync<ArgumentException>(() => Client(fake).SearchAsync(" ", cancellationToken: TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => Client(fake).SearchAsync("rice", pageSize: 0, cancellationToken: TestContext.Current.CancellationToken));
        fake.Requests.ShouldBeEmpty();
    }

    // ---- the answer ------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_captured_response_is_read_with_every_food_and_nutrient()
    {
        var fake = FakeFdc.Returning(Fixture("search-rice-captured.json"), limit: 10, remaining: 8);

        var result = await Client(fake).SearchAsync("rice white long-grain raw", cancellationToken: TestContext.Current.CancellationToken);

        result.TotalHits.ShouldBe(2026);
        result.Foods.Select(f => f.FdcId).ShouldBe([2512381L, 168877L, 169756L]);
        result.Foods[0].Description.ShouldBe("Rice, white, long grain, unenriched, raw");
        result.Foods[0].DataType.ShouldBe("Foundation");
        result.Foods[0].FoodCategory.ShouldBe("Cereal Grains and Pasta");
        result.Foods[0].Score.ShouldBe(1141.3729);
        result.Foods[0].FoodNutrients!.Single(n => n.NutrientId == 1093).Value.ShouldBe(0.462);
        result.Foods[1].FoodNutrients!.Single(n => n.NutrientId == 1008).UnitName.ShouldBe("KCAL");
        result.RateLimit.ShouldBe(new RateLimit(10, 8));
    }

    [Fact]
    public async Task Missing_or_unreadable_rate_limit_headers_are_unknown_not_zero()
    {
        var withoutHeaders = await Client(FakeFdc.Returning(Foods.Search(0), limit: null, remaining: null)).SearchAsync("rice", cancellationToken: TestContext.Current.CancellationToken);
        withoutHeaders.RateLimit.ShouldBe(new RateLimit(null, null));

        var junk = FakeFdc.Always(_ =>
        {
            var response = FakeFdc.Json(Foods.Search(0), limit: null, remaining: null);
            response.Headers.Add("X-RateLimit-Remaining", "plenty");
            response.Headers.Add("X-RateLimit-Limit", "-5");
            return response;
        });
        (await Client(junk).SearchAsync("rice", cancellationToken: TestContext.Current.CancellationToken)).RateLimit.ShouldBe(new RateLimit(null, null));
    }

    [Fact]
    public async Task A_response_without_foods_is_an_empty_result()
    {
        var result = await Client(FakeFdc.Returning("""{ "totalHits": 0 }""")).SearchAsync("rice", cancellationToken: TestContext.Current.CancellationToken);

        result.Foods.ShouldBeEmpty();
        result.TotalHits.ShouldBe(0);
    }

    [Fact]
    public async Task Fields_the_importer_does_not_read_are_ignored()
    {
        var json = """{ "totalHits": 1, "pageList": [1], "aggregations": {}, "foods": [{ "fdcId": 5, "description": "Rice", "dataType": "SR Legacy", "newField": { "a": 1 }, "foodNutrients": [] }] }""";

        var result = await Client(FakeFdc.Returning(json)).SearchAsync("rice", cancellationToken: TestContext.Current.CancellationToken);

        result.Foods.ShouldHaveSingleItem().FdcId.ShouldBe(5);
    }

    // ---- failures ------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Too_many_requests_carries_how_long_to_wait_and_what_is_left()
    {
        var fake = FakeFdc.Always(_ => FakeFdc.Status(HttpStatusCode.TooManyRequests, TimeSpan.FromSeconds(30), remaining: 0));

        var error = await Should.ThrowAsync<FdcRateLimitedException>(() => Client(fake).SearchAsync("rice", cancellationToken: TestContext.Current.CancellationToken));

        error.RetryAfter.ShouldBe(TimeSpan.FromSeconds(30));
        error.RateLimit.Remaining.ShouldBe(0);
        error.Status.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task A_retry_after_date_becomes_the_time_left()
    {
        var fake = FakeFdc.Always(_ =>
        {
            var response = FakeFdc.Status(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(DateTimeOffset.UtcNow.AddSeconds(45));
            return response;
        });

        var error = await Should.ThrowAsync<FdcRateLimitedException>(() => Client(fake).SearchAsync("rice", cancellationToken: TestContext.Current.CancellationToken));

        error.RetryAfter.ShouldNotBeNull();
        error.RetryAfter.Value.TotalSeconds.ShouldBeInRange(30, 46);
    }

    [Fact]
    public async Task A_retry_after_date_in_the_past_means_no_wait()
    {
        var fake = FakeFdc.Always(_ =>
        {
            var response = FakeFdc.Status(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(DateTimeOffset.UtcNow.AddMinutes(-5));
            return response;
        });

        var error = await Should.ThrowAsync<FdcRateLimitedException>(() => Client(fake).SearchAsync("rice", cancellationToken: TestContext.Current.CancellationToken));

        error.RetryAfter.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public async Task Too_many_requests_without_a_retry_after_says_it_does_not_know()
    {
        var fake = FakeFdc.Always(_ => FakeFdc.Status(HttpStatusCode.TooManyRequests));

        var error = await Should.ThrowAsync<FdcRateLimitedException>(() => Client(fake).SearchAsync("rice", cancellationToken: TestContext.Current.CancellationToken));

        error.RetryAfter.ShouldBeNull();
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task A_refused_key_is_an_auth_error(HttpStatusCode status)
    {
        var fake = FakeFdc.Always(_ => FakeFdc.Status(status));

        var error = await Should.ThrowAsync<FdcAuthException>(() => Client(fake).SearchAsync("rice", cancellationToken: TestContext.Current.CancellationToken));

        error.Status.ShouldBe(status);
        error.Message.ShouldNotContain(Key);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Any_other_failure_is_an_fdc_error_with_the_status_and_the_query(HttpStatusCode status)
    {
        var fake = FakeFdc.Always(_ => FakeFdc.Status(status));

        var error = await Should.ThrowAsync<FdcException>(() => Client(fake).SearchAsync("rice raw", cancellationToken: TestContext.Current.CancellationToken));

        error.GetType().ShouldBe(typeof(FdcException));
        error.Status.ShouldBe(status);
        error.Message.ShouldContain("rice raw");
        error.Message.ShouldNotContain(Key);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("""{ "totalHits": "many" }""")]
    public async Task A_body_that_is_not_a_search_result_is_an_fdc_error(string body)
    {
        var error = await Should.ThrowAsync<FdcException>(() => Client(FakeFdc.Returning(body)).SearchAsync("rice", cancellationToken: TestContext.Current.CancellationToken));

        error.Status.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_network_failure_is_an_fdc_error_without_a_status_and_without_the_key()
    {
        var fake = FakeFdc.Always(_ => throw new HttpRequestException("no route to host"));

        var error = await Should.ThrowAsync<FdcException>(() => Client(fake).SearchAsync("rice", cancellationToken: TestContext.Current.CancellationToken));

        error.Status.ShouldBeNull();
        error.Message.ShouldContain("no route to host");
        error.Message.ShouldNotContain(Key);
    }

    [Fact]
    public async Task A_timeout_is_an_fdc_error()
    {
        var fake = FakeFdc.Always(_ => throw new TaskCanceledException("timed out"));

        var error = await Should.ThrowAsync<FdcException>(() => Client(fake).SearchAsync("rice", cancellationToken: TestContext.Current.CancellationToken));

        error.Status.ShouldBeNull();
    }

    [Fact]
    public async Task A_cancelled_call_is_cancelled_not_reported_as_an_fdc_error()
    {
        using var cancellation = new CancellationTokenSource();
        var fake = FakeFdc.Always(_ =>
        {
            cancellation.Cancel();
            throw new TaskCanceledException("cancelled");
        });

        await Should.ThrowAsync<OperationCanceledException>(() => Client(fake).SearchAsync("rice", cancellationToken: cancellation.Token));
    }
}
