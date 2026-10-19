using System.Net;
using MamCom.FdcImporter.Cli;

namespace MamCom.FdcImporter.Tests;

public class CommandLineTests
{
    private const string Secret = "my-secret-key-9f3a";

    private static string Rice => Foods.Search(2026,
        Foods.Food(1, "Rice, white, raw", "SR Legacy", 90, Foods.Complete(kcal: 365)),
        Foods.Food(2, "Rice, brown, raw", "Foundation", 80, Foods.Complete(kcal: 360)),
        Foods.Food(3, "Rice, white, cooked", "SR Legacy", 95, Foods.Complete(kcal: 130)),
        Foods.Food(4, "Rice flour, raw", "SR Legacy", 70, Foods.Complete(kcal: 366)));

    // ---- usage -------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task No_arguments_print_the_usage_and_fail_with_the_usage_code()
    {
        using var data = new TempData();
        var output = new StringWriter();
        var error = new StringWriter();

        var code = await CommandLine.RunAsync([], output, error, ImporterEnvironment.Create(), TestContext.Current.CancellationToken);

        code.ShouldBe(CommandLine.UsageError);
        error.ToString().ShouldContain("Cách dùng:");
        output.ToString().ShouldBeEmpty();
    }

    [Theory]
    [InlineData("help")]
    [InlineData("--help")]
    [InlineData("-h")]
    public async Task Asking_for_help_prints_the_usage_and_succeeds(string flag)
    {
        var output = new StringWriter();

        var code = await CommandLine.RunAsync([flag], output, new StringWriter(), ImporterEnvironment.Create(), TestContext.Current.CancellationToken);

        code.ShouldBe(CommandLine.Success);
        output.ToString().ShouldContain("suggest");
        output.ToString().ShouldContain("Fdc:ApiKey");
        output.ToString().ShouldContain("DEMO_KEY");
    }

    [Theory]
    [InlineData("import")]
    [InlineData("SUGGEST")]
    public async Task An_unknown_command_is_a_usage_error(string command)
    {
        using var data = new TempData();
        var fake = FakeFdc.Returning(Rice);

        var (code, _, error, _) = await data.RunAsync(fake, null, command);

        code.ShouldBe(CommandLine.UsageError);
        error.ShouldContain($"'{command}'");
        fake.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("--fast")]
    [InlineData("--ids")]
    [InlineData("--top")]
    [InlineData("--top", "0")]
    [InlineData("--top", "11")]
    [InlineData("--top", "three")]
    [InlineData("--top", "-1")]
    [InlineData("--delay-ms", "soon")]
    [InlineData("--max-requests", "0")]
    [InlineData("--max-requests", "many")]
    [InlineData("--max-wait-seconds", "forever")]
    [InlineData("--ids", ",,")]
    public async Task A_wrong_option_is_a_usage_error_and_nothing_is_asked(params string[] options)
    {
        using var data = new TempData();
        var fake = FakeFdc.Returning(Rice);

        var (code, _, error, _) = await data.RunAsync(fake, null, ["suggest", .. options]);

        code.ShouldBe(CommandLine.UsageError);
        error.ShouldContain(options[0]);
        fake.Requests.ShouldBeEmpty();
        data.SuggestionFiles().ShouldBeEmpty();
    }

    // ---- a run -------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Asking_for_chosen_ingredients_sends_their_english_names_in_the_order_given()
    {
        using var data = new TempData();
        var fake = FakeFdc.Returning(Rice);

        var (code, output, error, _) = await data.RunAsync(fake, null, "suggest", "--ids", "trung-ga, gao");

        code.ShouldBe(CommandLine.Success);
        error.ShouldBeEmpty();
        fake.Requests.Select(r => r.Query).ShouldBe(["egg whole raw fresh", "rice white long-grain raw"]);
        data.SuggestionFiles().ShouldBe(["gao", "trung-ga"]);
        output.ShouldContain("gao: ghi");
        output.ShouldContain("Xong: ghi 2, bỏ qua 0, lỗi 0; đã gọi FDC 2 lần.");
    }

    [Fact]
    public async Task Without_ids_every_ingredient_with_an_english_name_is_asked_up_to_the_request_cap()
    {
        using var data = new TempData();
        var fake = FakeFdc.Returning(Rice, limit: 1000, remaining: 900);

        var (code, output, error, _) = await data.RunAsync(fake, null, "suggest", "--max-requests", "40");

        code.ShouldBe(CommandLine.Success);
        fake.Requests.Count.ShouldBe(33);
        data.SuggestionFiles().Length.ShouldBe(33);
        output.ShouldContain("ghi 33");
        error.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_default_request_cap_stops_a_full_run_and_says_how_to_go_on()
    {
        using var data = new TempData();
        var fake = FakeFdc.Returning(Rice, limit: 1000, remaining: 900);

        var (code, output, error, _) = await data.RunAsync(fake, null, "suggest");

        code.ShouldBe(CommandLine.Incomplete);
        fake.Requests.Count.ShouldBe(25);
        error.ShouldContain("--max-requests");
        output.ShouldContain("ghi 25");

        // Running it again asks only for the rest: the first 25 already have files.
        var fakeAgain = FakeFdc.Returning(Rice, limit: 1000, remaining: 900);
        var (codeAgain, outputAgain, _, _) = await data.RunAsync(fakeAgain, null, "suggest");

        codeAgain.ShouldBe(CommandLine.Success);
        fakeAgain.Requests.Count.ShouldBe(8);
        outputAgain.ShouldContain("bỏ qua 25");
        data.SuggestionFiles().Length.ShouldBe(33);
    }

    [Fact]
    public async Task Refresh_asks_again_for_ingredients_that_already_have_a_file()
    {
        using var data = new TempData();
        await data.RunAsync(FakeFdc.Returning(Rice), null, "suggest", "--ids", "gao");
        var again = FakeFdc.Returning(Rice);

        var (code, _, _, _) = await data.RunAsync(again, null, "suggest", "--ids", "gao", "--refresh");

        code.ShouldBe(CommandLine.Success);
        again.Requests.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task The_top_option_limits_the_candidates()
    {
        using var data = new TempData();

        await data.RunAsync(FakeFdc.Returning(Rice), null, "suggest", "--ids", "gao", "--top", "1");

        File.ReadAllText(data.SuggestionPath("gao")).Split("\"fdcId\"").Length.ShouldBe(2);   // one candidate
    }

    [Fact]
    public async Task By_default_three_candidates_are_kept()
    {
        using var data = new TempData();

        await data.RunAsync(FakeFdc.Returning(Rice), null, "suggest", "--ids", "gao");

        File.ReadAllText(data.SuggestionPath("gao")).Split("\"fdcId\"").Length.ShouldBe(4);   // three candidates
    }

    [Fact]
    public async Task A_429_asking_for_less_than_a_minute_is_waited_out_by_default()
    {
        using var data = new TempData();
        var fake = new FakeFdc((_, n) => n == 1 ? FakeFdc.Status(HttpStatusCode.TooManyRequests, TimeSpan.FromSeconds(45)) : FakeFdc.Json(Rice));

        var (code, _, _, delays) = await data.RunAsync(fake, null, "suggest", "--ids", "gao");

        code.ShouldBe(CommandLine.Success);
        delays.ShouldContain(TimeSpan.FromSeconds(45));
    }

    [Fact]
    public async Task The_delay_option_sets_the_pause_between_calls()
    {
        using var data = new TempData();

        var (_, _, _, delays) = await data.RunAsync(FakeFdc.Returning(Rice), null, "suggest", "--ids", "gao,trung-ga", "--delay-ms", "250");
        var (_, _, _, defaults) = await data.RunAsync(FakeFdc.Returning(Rice), null, "suggest", "--ids", "ca-chua,ca-rot");

        delays.ShouldBe([TimeSpan.FromMilliseconds(250)]);
        defaults.ShouldBe([TimeSpan.FromSeconds(1)]);
    }

    [Fact]
    public async Task The_max_wait_option_decides_whether_a_429_is_waited_out()
    {
        using var data = new TempData();
        FakeFdc Limited() => new((_, n) => n == 1 ? FakeFdc.Status(HttpStatusCode.TooManyRequests, TimeSpan.FromSeconds(90)) : FakeFdc.Json(Rice));

        var (codeDefault, _, errorDefault, _) = await data.RunAsync(Limited(), null, "suggest", "--ids", "gao");
        var (codeLonger, _, _, delays) = await data.RunAsync(Limited(), null, "suggest", "--ids", "gao", "--max-wait-seconds", "120");

        codeDefault.ShouldBe(CommandLine.Incomplete);
        errorDefault.ShouldContain("429");
        codeLonger.ShouldBe(CommandLine.Success);
        delays.ShouldContain(TimeSpan.FromSeconds(90));
    }

    // ---- the key -------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Without_a_key_in_user_secrets_the_demo_key_is_used_and_the_user_is_told()
    {
        using var data = new TempData();
        var fake = FakeFdc.Returning(Rice);

        var (_, output, _, _) = await data.RunAsync(fake, null, "suggest", "--ids", "gao");

        fake.Requests.Single().ApiKey.ShouldBe("DEMO_KEY");
        output.ShouldContain("DEMO_KEY");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_key_counts_as_no_key(string blank)
    {
        using var data = new TempData();
        var fake = FakeFdc.Returning(Rice);

        await data.RunAsync(fake, blank, "suggest", "--ids", "gao");

        fake.Requests.Single().ApiKey.ShouldBe("DEMO_KEY");
    }

    [Fact]
    public async Task A_key_from_user_secrets_is_sent_and_never_printed()
    {
        using var data = new TempData();
        var fake = FakeFdc.Returning(Rice);

        var (_, output, error, _) = await data.RunAsync(fake, Secret, "suggest", "--ids", "gao,trung-ga");

        fake.Requests.ShouldAllBe(r => r.ApiKey == Secret);
        output.ShouldNotContain(Secret);
        error.ShouldNotContain(Secret);
        output.ShouldContain("user-secrets");
        File.ReadAllText(data.SuggestionPath("gao")).ShouldNotContain(Secret);
    }

    [Fact]
    public async Task The_key_is_not_printed_when_things_go_wrong_either()
    {
        using var data = new TempData();
        var refused = FakeFdc.Always(_ => FakeFdc.Status(HttpStatusCode.Forbidden));
        var broken = FakeFdc.Always(_ => throw new HttpRequestException("connection reset"));

        var (code, output1, error1, _) = await data.RunAsync(refused, Secret, "suggest", "--ids", "gao");
        var (_, output2, error2, _) = await data.RunAsync(broken, Secret, "suggest", "--ids", "gao");

        code.ShouldBe(CommandLine.Incomplete);
        error1.ShouldContain("khoá API");
        foreach (var text in new[] { output1, error1, output2, error2 })
        {
            text.ShouldNotContain(Secret);
        }
    }

    // ---- exit codes and messages -------------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_failed_ingredient_makes_the_run_incomplete_and_is_said_on_the_error_stream()
    {
        using var data = new TempData();
        var fake = FakeFdc.Always(_ => FakeFdc.Status(HttpStatusCode.BadRequest));

        var (code, output, error, _) = await data.RunAsync(fake, null, "suggest", "--ids", "gao");

        code.ShouldBe(CommandLine.Incomplete);
        error.ShouldContain("gao: lỗi");
        output.ShouldContain("lỗi 1");
    }

    [Fact]
    public async Task A_stopped_run_says_why_on_the_error_stream_and_is_incomplete()
    {
        using var data = new TempData();
        var fake = FakeFdc.Returning(Rice, limit: 30, remaining: 0);

        var (code, _, error, _) = await data.RunAsync(fake, null, "suggest", "--ids", "gao,trung-ga");

        code.ShouldBe(CommandLine.Incomplete);
        error.ShouldContain("hết hạn mức");
        data.SuggestionFiles().ShouldBe(["gao"]);
    }

    [Fact]
    public async Task Nothing_to_do_is_a_success()
    {
        using var data = new TempData();
        await data.RunAsync(FakeFdc.Returning(Rice), null, "suggest", "--ids", "gao");
        var fake = FakeFdc.Returning(Rice);

        var (code, output, _, _) = await data.RunAsync(fake, null, "suggest", "--ids", "gao");

        code.ShouldBe(CommandLine.Success);
        fake.Requests.ShouldBeEmpty();
        output.ShouldContain("bỏ qua 1");
    }

    [Fact]
    public async Task An_unknown_ingredient_id_is_refused_before_anything_is_asked()
    {
        using var data = new TempData();
        var fake = FakeFdc.Returning(Rice);

        var (code, _, error, _) = await data.RunAsync(fake, null, "suggest", "--ids", "gao,khong-co");

        code.ShouldBe(CommandLine.Incomplete);
        error.ShouldContain("'khong-co'");
        fake.Requests.ShouldBeEmpty();
        data.SuggestionFiles().ShouldBeEmpty();
    }

    [Fact]
    public async Task An_ingredient_without_an_english_name_is_skipped_and_counted()
    {
        using var data = new TempData();
        File.WriteAllText(Path.Combine(data.Root, "plan", "ingredient-names-en.csv"), "id,english\ngao,rice white raw\n");
        var fake = FakeFdc.Returning(Rice);

        var (code, output, _, _) = await data.RunAsync(fake, null, "suggest", "--ids", "gao,trung-ga");

        code.ShouldBe(CommandLine.Success);
        fake.Requests.Count.ShouldBe(1);
        output.ShouldContain("trung-ga: bỏ qua, chưa có tên tiếng Anh");
        output.ShouldContain("bỏ qua 1");
    }

    [Fact]
    public async Task English_names_for_ingredients_that_do_not_exist_are_warned_about()
    {
        using var data = new TempData();
        File.AppendAllText(Path.Combine(data.Root, "plan", "ingredient-names-en.csv"), "ma-quai,mystery raw\n");

        var (_, output, _, _) = await data.RunAsync(FakeFdc.Returning(Rice), null, "suggest", "--ids", "gao");

        output.ShouldContain("Cảnh báo: tên tiếng Anh cho nguyên liệu không có file: ma-quai.");
    }

    [Fact]
    public async Task A_missing_names_file_is_an_error_with_the_path()
    {
        using var data = new TempData();
        File.Delete(Path.Combine(data.Root, "plan", "ingredient-names-en.csv"));
        var fake = FakeFdc.Returning(Rice);

        var (code, _, error, _) = await data.RunAsync(fake, null, "suggest");

        code.ShouldBe(CommandLine.Incomplete);
        error.ShouldContain("ingredient-names-en.csv");
        fake.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_names_file_in_the_wrong_shape_is_an_error_naming_the_line()
    {
        using var data = new TempData();
        File.WriteAllText(Path.Combine(data.Root, "plan", "ingredient-names-en.csv"), "id,english\ngao\n");

        var (code, _, error, _) = await data.RunAsync(FakeFdc.Returning(Rice), null, "suggest");

        code.ShouldBe(CommandLine.Incomplete);
        error.ShouldContain("line 2");
    }

    [Fact]
    public async Task A_data_folder_without_ingredients_is_an_error()
    {
        using var data = new TempData();
        Directory.Delete(Path.Combine(data.Root, "ingredients"), recursive: true);
        var fake = FakeFdc.Returning(Rice);

        var (code, _, error, _) = await data.RunAsync(fake, null, "suggest");

        code.ShouldBe(CommandLine.Incomplete);
        error.ShouldContain("ingredients");
        fake.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_environment_default_reads_user_secrets_and_the_system_clock()
    {
        var environment = ImporterEnvironment.Create();

        environment.Configuration.ShouldNotBeNull();
        environment.Handler.ShouldBeNull();
        environment.Clock.Today.ShouldBe(DateOnly.FromDateTime(DateTime.Now), tolerance: 1);
        await environment.Delay(TimeSpan.Zero, CancellationToken.None);
    }
}

internal static class DateOnlyShouldlyExtensions
{
    /// <summary>Equal to within a number of days, for a clock read just before midnight.</summary>
    public static void ShouldBe(this DateOnly actual, DateOnly expected, int tolerance) =>
        Math.Abs(actual.DayNumber - expected.DayNumber).ShouldBeLessThanOrEqualTo(tolerance);
}
