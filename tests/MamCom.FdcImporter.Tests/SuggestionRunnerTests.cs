using System.Net;
using MamCom.FdcImporter.Fdc;
using MamCom.FdcImporter.Running;
using MamCom.FdcImporter.Suggestions;

namespace MamCom.FdcImporter.Tests;

public class SuggestionRunnerTests
{
    private static readonly RunOptions Options = new(Top: 3, Refresh: false, DelayBetweenRequests: TimeSpan.FromSeconds(1), MaxRequests: 25, MaxRetryWait: TimeSpan.FromSeconds(60), MaxRetries: 2);

    private static readonly IReadOnlyDictionary<string, string> Names = new Dictionary<string, string>
    {
        ["gao"] = "rice white raw",
        ["trung-ga"] = "egg whole raw",
        ["ca-chua"] = "tomatoes raw",
    };

    private static string Rice => Foods.Search(2026,
        Foods.Food(1, "Rice, white, raw", "SR Legacy", 90, Foods.Complete(kcal: 365)),
        Foods.Food(2, "Rice, white, cooked", "SR Legacy", 95, Foods.Complete(kcal: 130)),
        Foods.Food(3, "Rice, brown, raw", "Foundation", 80, Foods.Complete(kcal: 360)),
        Foods.Food(4, "Rice flour, raw", "SR Legacy", 70, Foods.Complete(kcal: 366)));

    private sealed class Run
    {
        public List<TimeSpan> Delays { get; } = [];

        public List<IngredientOutcome> Reported { get; } = [];

        public SuggestionRunner Runner(FakeFdc fake) =>
            new(new FdcClient(new HttpClient(fake), "TEST-KEY"), new FixedClock(new DateOnly(2026, 10, 19)), (delay, _) =>
            {
                Delays.Add(delay);
                return Task.CompletedTask;
            });
    }

    private static Task<RunResult> Go(Run run, FakeFdc fake, TempData data, string[] ids, RunOptions? options = null) =>
        run.Runner(fake).RunAsync(data.Root, ids, Names, options ?? Options, run.Reported.Add, TestContext.Current.CancellationToken);

    // ---- what is written -----------------------------------------------------------------------------------------

    [Fact]
    public async Task Each_ingredient_gets_a_file_with_the_best_candidates_first()
    {
        using var data = new TempData();
        var run = new Run();

        var result = await Go(run, FakeFdc.Returning(Rice), data, ["gao"]);

        result.StoppedBecause.ShouldBeNull();
        result.RequestsMade.ShouldBe(1);
        var json = File.ReadAllText(data.SuggestionPath("gao"));
        json.ShouldContain("\"ingredientId\": \"gao\"");
        json.ShouldContain("\"query\": \"rice white raw\"");
        json.ShouldContain("\"retrievedAt\": \"2026-10-19\"");
        json.ShouldContain("\"source\": \"usda-fdc\"");
        json.ShouldContain("\"totalHits\": 2026");
        json.IndexOf("\"fdcId\": 1,", StringComparison.Ordinal).ShouldBeLessThan(json.IndexOf("\"fdcId\": 3,", StringComparison.Ordinal));
        json.ShouldNotContain("\"fdcId\": 2,");   // cooked rice is ranked below the three kept
    }

    [Fact]
    public async Task The_file_is_indented_ends_with_one_line_feed_and_keeps_vietnamese_readable()
    {
        using var data = new TempData();
        await Go(new Run(), FakeFdc.Returning(Rice), data, ["gao"]);

        var json = File.ReadAllText(data.SuggestionPath("gao"));

        json.ShouldStartWith("{\n  \"ingredientId\"");
        json.ShouldEndWith("}\n");
        json.ShouldNotEndWith("\n\n");
        json.ShouldNotContain('\r');
        json.ShouldContain("nguyên liệu sống");   // not "nguyên liệu sống"
    }

    [Fact]
    public async Task Only_the_top_candidates_are_kept()
    {
        using var data = new TempData();

        await Go(new Run(), FakeFdc.Returning(Rice), data, ["gao"], Options with { Top = 2 });

        File.ReadAllText(data.SuggestionPath("gao")).Split("\"fdcId\"").Length.ShouldBe(3);   // two candidates
    }

    [Fact]
    public async Task A_run_writes_only_suggestion_files_and_leaves_every_ingredient_file_as_it_was()
    {
        using var data = new TempData();
        var before = data.Snapshot("ingredients", "schemas", "plan");

        await Go(new Run(), FakeFdc.Returning(Rice), data, ["gao", "trung-ga", "ca-chua"]);

        data.Snapshot("ingredients", "schemas", "plan").ShouldBe(before);
        data.SuggestionFiles().ShouldBe(["ca-chua", "gao", "trung-ga"]);
    }

    [Fact]
    public async Task Nothing_found_still_writes_a_file_saying_so()
    {
        using var data = new TempData();
        var run = new Run();

        await Go(run, FakeFdc.Returning(Foods.Search(0)), data, ["gao"]);

        File.ReadAllText(data.SuggestionPath("gao")).ShouldContain("\"candidates\": []");
        run.Reported.Single().Kind.ShouldBe(OutcomeKind.Written);
        run.Reported.Single().Message.ShouldContain("không có món nào khớp");
    }

    [Fact]
    public async Task Outcomes_are_reported_in_order_as_they_happen()
    {
        using var data = new TempData();
        var run = new Run();

        await Go(run, FakeFdc.Returning(Rice), data, ["gao", "ca-chua"]);

        run.Reported.Select(o => (o.IngredientId, o.Kind)).ShouldBe([("gao", OutcomeKind.Written), ("ca-chua", OutcomeKind.Written)]);
    }

    // ---- what is skipped ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task An_ingredient_that_already_has_a_file_is_not_asked_again()
    {
        using var data = new TempData();
        var fake = FakeFdc.Returning(Rice);
        await Go(new Run(), fake, data, ["gao"]);
        var firstText = File.ReadAllText(data.SuggestionPath("gao"));
        var run = new Run();

        var result = await Go(run, fake, data, ["gao"]);

        result.RequestsMade.ShouldBe(0);
        fake.Requests.Count.ShouldBe(1);
        run.Reported.Single().Kind.ShouldBe(OutcomeKind.SkippedExisting);
        File.ReadAllText(data.SuggestionPath("gao")).ShouldBe(firstText);
    }

    [Fact]
    public async Task Refresh_asks_again_and_replaces_the_file()
    {
        using var data = new TempData();
        await Go(new Run(), FakeFdc.Returning(Foods.Search(0)), data, ["gao"]);

        var result = await Go(new Run(), FakeFdc.Returning(Rice), data, ["gao"], Options with { Refresh = true });

        result.RequestsMade.ShouldBe(1);
        File.ReadAllText(data.SuggestionPath("gao")).ShouldContain("\"fdcId\": 1,");
    }

    [Fact]
    public async Task An_ingredient_with_no_english_name_is_skipped_without_a_request()
    {
        using var data = new TempData();
        var fake = FakeFdc.Returning(Rice);
        var run = new Run();

        var result = await Go(run, fake, data, ["thit-bo"]);

        fake.Requests.ShouldBeEmpty();
        result.RequestsMade.ShouldBe(0);
        run.Reported.Single().Kind.ShouldBe(OutcomeKind.NoEnglishName);
        data.SuggestionFiles().ShouldBeEmpty();
    }

    // ---- being a good guest ------------------------------------------------------------------------------------------------

    [Fact]
    public async Task The_run_pauses_between_calls_but_not_before_the_first()
    {
        using var data = new TempData();
        var run = new Run();

        await Go(run, FakeFdc.Returning(Rice), data, ["gao", "trung-ga", "ca-chua"], Options with { DelayBetweenRequests = TimeSpan.FromMilliseconds(250) });

        run.Delays.ShouldBe([TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(250)]);
    }

    [Fact]
    public async Task The_run_stops_at_the_request_cap_and_keeps_what_it_wrote()
    {
        using var data = new TempData();
        var fake = FakeFdc.Returning(Rice);

        var result = await Go(new Run(), fake, data, ["gao", "trung-ga", "ca-chua"], Options with { MaxRequests = 2 });

        result.StoppedBecause.ShouldNotBeNull().ShouldContain("--max-requests");
        fake.Requests.Count.ShouldBe(2);
        data.SuggestionFiles().ShouldBe(["gao", "trung-ga"]);
    }

    [Fact]
    public async Task The_run_stops_before_the_call_that_would_go_over_the_keys_quota()
    {
        using var data = new TempData();
        var fake = new FakeFdc((_, n) => FakeFdc.Json(Rice, limit: 10, remaining: n == 1 ? 0 : 5));

        var result = await Go(new Run(), fake, data, ["gao", "trung-ga", "ca-chua"]);

        result.StoppedBecause.ShouldNotBeNull().ShouldContain("hết hạn mức");
        result.StoppedBecause.ShouldContain("10");
        fake.Requests.Count.ShouldBe(1);
        data.SuggestionFiles().ShouldBe(["gao"]);
    }

    [Fact]
    public async Task One_request_left_is_still_a_request()
    {
        using var data = new TempData();
        var fake = new FakeFdc((_, n) => FakeFdc.Json(Rice, limit: 10, remaining: n == 1 ? 1 : 0));

        var result = await Go(new Run(), fake, data, ["gao", "trung-ga", "ca-chua"]);

        fake.Requests.Count.ShouldBe(2);
        data.SuggestionFiles().ShouldBe(["gao", "trung-ga"]);
        result.StoppedBecause.ShouldNotBeNull();
    }

    [Fact]
    public async Task When_the_server_does_not_say_how_many_are_left_the_run_goes_on()
    {
        using var data = new TempData();
        var fake = FakeFdc.Returning(Rice, limit: null, remaining: null);

        var result = await Go(new Run(), fake, data, ["gao", "trung-ga", "ca-chua"]);

        result.StoppedBecause.ShouldBeNull();
        fake.Requests.Count.ShouldBe(3);
    }

    [Fact]
    public async Task After_a_short_429_the_run_waits_as_told_and_tries_again()
    {
        using var data = new TempData();
        var fake = new FakeFdc((_, n) => n == 1 ? FakeFdc.Status(HttpStatusCode.TooManyRequests, TimeSpan.FromSeconds(5), remaining: 3) : FakeFdc.Json(Rice));
        var run = new Run();

        var result = await Go(run, fake, data, ["gao"]);

        result.StoppedBecause.ShouldBeNull();
        result.RequestsMade.ShouldBe(2);   // the refused call counts against the cap
        run.Delays.ShouldBe([TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(1)]);   // the wait, then the usual pause
        data.SuggestionFiles().ShouldBe(["gao"]);
    }

    [Fact]
    public async Task A_429_that_asks_for_longer_than_the_limit_stops_the_run()
    {
        using var data = new TempData();
        var fake = FakeFdc.Always(_ => FakeFdc.Status(HttpStatusCode.TooManyRequests, TimeSpan.FromSeconds(61)));

        var result = await Go(new Run(), fake, data, ["gao", "trung-ga"]);

        result.StoppedBecause.ShouldNotBeNull().ShouldContain("429");
        result.StoppedBecause.ShouldContain("61");
        fake.Requests.Count.ShouldBe(1);
        data.SuggestionFiles().ShouldBeEmpty();
    }

    [Fact]
    public async Task A_wait_exactly_at_the_limit_is_waited_out()
    {
        using var data = new TempData();
        var fake = new FakeFdc((_, n) => n == 1 ? FakeFdc.Status(HttpStatusCode.TooManyRequests, TimeSpan.FromSeconds(60)) : FakeFdc.Json(Rice));

        var result = await Go(new Run(), fake, data, ["gao"]);

        result.StoppedBecause.ShouldBeNull();
    }

    [Fact]
    public async Task A_429_without_a_retry_after_stops_the_run()
    {
        using var data = new TempData();
        var fake = FakeFdc.Always(_ => FakeFdc.Status(HttpStatusCode.TooManyRequests));

        var result = await Go(new Run(), fake, data, ["gao"]);

        result.StoppedBecause.ShouldNotBeNull().ShouldContain("429");
        fake.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task The_run_gives_up_after_the_allowed_number_of_retries()
    {
        using var data = new TempData();
        var fake = FakeFdc.Always(_ => FakeFdc.Status(HttpStatusCode.TooManyRequests, TimeSpan.FromSeconds(1)));

        var result = await Go(new Run(), fake, data, ["gao", "trung-ga"], Options with { MaxRetries = 2 });

        result.StoppedBecause.ShouldNotBeNull();
        fake.Requests.Count.ShouldBe(3);   // the first call and two retries
        data.SuggestionFiles().ShouldBeEmpty();
    }

    [Fact]
    public async Task A_429_with_no_quota_left_stops_the_run_instead_of_retrying()
    {
        using var data = new TempData();
        var fake = FakeFdc.Always(_ => FakeFdc.Status(HttpStatusCode.TooManyRequests, TimeSpan.FromSeconds(2), remaining: 0));
        var run = new Run();

        var result = await Go(run, fake, data, ["gao"]);

        result.StoppedBecause.ShouldNotBeNull().ShouldContain("hết hạn mức");
        fake.Requests.Count.ShouldBe(1);
        run.Delays.ShouldBeEmpty();
    }

    // ---- failures ---------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_refused_key_stops_the_run_at_once()
    {
        using var data = new TempData();
        var fake = FakeFdc.Always(_ => FakeFdc.Status(HttpStatusCode.Forbidden));

        var result = await Go(new Run(), fake, data, ["gao", "trung-ga"]);

        result.StoppedBecause.ShouldNotBeNull().ShouldContain("khoá API");
        fake.Requests.Count.ShouldBe(1);
        data.SuggestionFiles().ShouldBeEmpty();
    }

    [Fact]
    public async Task One_ingredient_failing_does_not_stop_the_others()
    {
        using var data = new TempData();
        var fake = FakeFdc.Always(request => request.Query.Contains("egg") ? FakeFdc.Status(HttpStatusCode.BadRequest) : FakeFdc.Json(Rice));
        var run = new Run();

        var result = await Go(run, fake, data, ["gao", "trung-ga", "ca-chua"]);

        result.StoppedBecause.ShouldBeNull();
        run.Reported.Select(o => o.Kind).ShouldBe([OutcomeKind.Written, OutcomeKind.Failed, OutcomeKind.Written]);
        run.Reported[1].Message.ShouldContain("trung-ga");
        data.SuggestionFiles().ShouldBe(["ca-chua", "gao"]);
    }

    [Fact]
    public async Task A_network_failure_is_one_failed_ingredient()
    {
        using var data = new TempData();
        var fake = FakeFdc.Always(_ => throw new HttpRequestException("no route"));
        var run = new Run();

        var result = await Go(run, fake, data, ["gao"]);

        result.StoppedBecause.ShouldBeNull();
        run.Reported.Single().Kind.ShouldBe(OutcomeKind.Failed);
        run.Reported.Single().Message.ShouldContain("no route");
    }

    [Fact]
    public async Task Cancelling_stops_the_run_between_calls_leaving_only_whole_files()
    {
        using var data = new TempData();
        using var cancellation = new CancellationTokenSource();
        var pauses = 0;
        var run = new Run();
        var runner = new SuggestionRunner(new FdcClient(new HttpClient(FakeFdc.Returning(Rice)), "TEST-KEY"), new FixedClock(new DateOnly(2026, 10, 19)), (delay, token) =>
        {
            if (++pauses == 2)
            {
                cancellation.Cancel();   // between the second and the third ingredient
            }

            token.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        });

        await Should.ThrowAsync<OperationCanceledException>(() => runner.RunAsync(data.Root, ["gao", "trung-ga", "ca-chua"], Names, Options, run.Reported.Add, cancellation.Token));

        data.SuggestionFiles().ShouldBe(["gao", "trung-ga"]);
        foreach (var path in Directory.GetFiles(Path.Combine(data.Root, "_suggestions", "nutrition")))
        {
            path.ShouldEndWith(".json");
            Should.NotThrow(() => System.Text.Json.JsonDocument.Parse(File.ReadAllText(path)));
        }
    }

    [Fact]
    public async Task The_runner_checks_its_arguments()
    {
        using var data = new TempData();
        var runner = new Run().Runner(FakeFdc.Returning(Rice));

        await Should.ThrowAsync<ArgumentException>(() => runner.RunAsync(" ", ["gao"], Names, Options, _ => { }, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ArgumentNullException>(() => runner.RunAsync(data.Root, null!, Names, Options, _ => { }, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ArgumentNullException>(() => runner.RunAsync(data.Root, ["gao"], null!, Options, _ => { }, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ArgumentNullException>(() => runner.RunAsync(data.Root, ["gao"], Names, null!, _ => { }, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ArgumentNullException>(() => runner.RunAsync(data.Root, ["gao"], Names, Options, null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void The_file_helpers_agree_on_where_a_suggestion_lives()
    {
        SuggestionFile.PathFor("data", "gao").ShouldBe(Path.Combine("data", "_suggestions", "nutrition", "gao.json"));
        using var data = new TempData();
        SuggestionFile.Exists(data.Root, "gao").ShouldBeFalse();
    }
}
