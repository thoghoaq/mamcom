using System.Net;
using System.Text.Json.Nodes;
using MamCom.RecipeDrafter.Cli;
using MamCom.RecipeDrafter.Model;

namespace MamCom.RecipeDrafter.Tests;

public class CommandLineTests
{
    private const string Secret = "sk-ant-secret-key-9f3a";

    private const string SevenDishes =
        "name,slot,regions\nMón một,man,south\nMón hai,man,north\nMón ba,canh,\nMón bốn,rau,south\nMón năm,man,central\nMón sáu,canh,north\nMón bảy,rau,\n";

    private static FakeModel Good() => FakeModel.Answering(Answers.Recipe());

    // ---- usage -------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task No_arguments_print_the_usage_and_fail_with_the_usage_code()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var code = await CommandLine.RunAsync([], output, error, TempData.Env(), TestContext.Current.CancellationToken);

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

        var code = await CommandLine.RunAsync([flag], output, new StringWriter(), TempData.Env(), TestContext.Current.CancellationToken);

        code.ShouldBe(CommandLine.Success);
        output.ToString().ShouldContain("draft");
        output.ToString().ShouldContain("Claude:ApiKey");
        output.ToString().ShouldContain("ANTHROPIC_API_KEY");
        output.ToString().ShouldContain("claude-opus-5-5");
    }

    [Fact]
    public async Task An_unknown_command_is_a_usage_error_and_nothing_is_asked()
    {
        using var data = new TempData();
        var model = Good();

        var (code, _, error) = await data.RunAsync(TempData.Env(model), "publish");

        code.ShouldBe(CommandLine.UsageError);
        error.ShouldContain("'publish'");
        model.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("--fast")]
    [InlineData("--limit")]
    [InlineData("--limit", "0")]
    [InlineData("--limit", "-3")]
    [InlineData("--limit", "many")]
    [InlineData("--only")]
    [InlineData("--only", ",,")]
    [InlineData("--list")]
    [InlineData("--prompts")]
    [InlineData("--data")]
    public async Task A_wrong_option_is_a_usage_error_and_nothing_is_asked(params string[] options)
    {
        using var data = new TempData();
        var model = Good();

        var (code, _, error) = await data.RunAsync(TempData.Env(model), ["draft", .. options]);

        code.ShouldBe(CommandLine.UsageError);
        error.ShouldContain(options[0]);
        model.Requests.ShouldBeEmpty();
        data.DraftFiles().ShouldBeEmpty();
    }

    // ---- a run -------------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task The_starter_list_gets_three_drafts_and_a_summary_and_exits_cleanly()
    {
        using var data = new TempData();
        var model = Good();

        var (code, output, error) = await data.RunAsync(TempData.Env(model), "draft");

        code.ShouldBe(CommandLine.Success);
        error.ShouldBeEmpty();
        model.Requests.Count.ShouldBe(3);
        data.DraftFiles().Length.ShouldBe(6);
        output.ShouldContain("Soạn tối đa 3 món bằng fake-model");
        output.ShouldContain("ca-loc-kho-to: valid");
        output.ShouldContain("Xong: soạn 3 (hợp lệ 3, cảnh báo 0, không hợp lệ 0), không soạn được 0, bỏ qua 0; gọi API 3 lần;");
        output.ShouldContain("token vào 3600, ra 2400, ghi cache 3000, đọc cache 0.");
    }

    [Fact]
    public async Task The_default_limit_is_five_dishes_a_run_and_the_next_run_goes_on()
    {
        using var data = new TempData(SevenDishes);
        var model = Good();

        var (code, output, _) = await data.RunAsync(TempData.Env(model), "draft");

        code.ShouldBe(CommandLine.Success);
        model.Requests.Count.ShouldBe(CommandLine.DefaultLimit);
        CommandLine.DefaultLimit.ShouldBe(5);
        output.ShouldContain("Đã soạn đủ 5 món của lượt này (--limit)");

        var second = Good();
        var (_, outputAgain, _) = await data.RunAsync(TempData.Env(second), "draft");

        second.Requests.Count.ShouldBe(2);
        outputAgain.ShouldContain("bỏ qua 5");
        outputAgain.ShouldContain("Soạn tối đa 2 món");   // the five with drafts are not counted
        data.DraftFiles().Count(f => !f.EndsWith(".report.json")).ShouldBe(7);
    }

    [Fact]
    public async Task All_removes_the_limit()
    {
        using var data = new TempData(SevenDishes);
        var model = Good();

        await data.RunAsync(TempData.Env(model), "draft", "--all");

        model.Requests.Count.ShouldBe(7);
    }

    [Fact]
    public async Task Limit_sets_how_many()
    {
        using var data = new TempData(SevenDishes);
        var model = Good();

        await data.RunAsync(TempData.Env(model), "draft", "--limit", "2");

        model.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Only_drafts_the_dishes_named_and_an_unknown_id_is_refused_before_anything_is_asked()
    {
        using var data = new TempData(SevenDishes);
        var model = Good();

        var (code, _, _) = await data.RunAsync(TempData.Env(model), "draft", "--only", "mon-hai, mon-ba");
        code.ShouldBe(CommandLine.Success);
        model.Requests.Count.ShouldBe(2);

        var other = Good();
        var (codeBad, _, error) = await data.RunAsync(TempData.Env(other), "draft", "--only", "mon-bay,khong-co");
        codeBad.ShouldBe(CommandLine.Incomplete);
        error.ShouldContain("'khong-co'");
        other.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Redo_drafts_again()
    {
        using var data = new TempData();
        await data.RunAsync(TempData.Env(Good()), "draft");
        var again = Good();

        await data.RunAsync(TempData.Env(again), "draft", "--redo");

        again.Requests.Count.ShouldBe(3);
    }

    [Fact]
    public async Task The_plan_line_counts_the_dishes_that_redo_will_draft_again()
    {
        using var data = new TempData();
        await data.RunAsync(TempData.Env(Good()), "draft");

        var (_, withoutRedo, _) = await data.RunAsync(TempData.Env(Good()), "draft");
        var (_, withRedo, _) = await data.RunAsync(TempData.Env(Good()), "draft", "--redo");

        withoutRedo.ShouldContain("Soạn tối đa 0 món");
        withRedo.ShouldContain("Soạn tối đa 3 món");
    }

    [Fact]
    public async Task Spaces_around_a_key_are_not_part_of_it()
    {
        using var data = new TempData();
        var api = Api();

        await data.RunAsync(TempData.Env(settings: new() { [CommandLine.ApiKeySetting] = "  sk-spaced  " }, handler: api), "draft", "--limit", "1");

        api.Requests.Single().ApiKey.ShouldBe("sk-spaced");
    }

    [Fact]
    public async Task A_dish_list_elsewhere_can_be_named()
    {
        using var data = new TempData();
        var other = Path.Combine(data.Root, "plan", "khac.csv");
        File.WriteAllText(other, "name,slot,regions\nMón riêng,man,south\n");
        var model = Good();

        await data.RunAsync(TempData.Env(model), "draft", "--list", other);

        model.Requests.ShouldHaveSingleItem().User.ShouldContain("Món riêng");
    }

    [Fact]
    public async Task Prompts_from_another_folder_are_used()
    {
        using var data = new TempData();
        var folder = Path.Combine(data.Root, "loi-dan");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "system.md"), "LỜI DẶN RIÊNG {{ingredient_catalog}} {{season_ids}} {{protein_min_grams}} {{protein_max_grams}}");
        File.WriteAllText(Path.Combine(folder, "dish.md"), "MÓN {{name}} {{slot}} {{slot_label}} {{regions}}");
        var model = Good();

        await data.RunAsync(TempData.Env(model), "draft", "--limit", "1", "--prompts", folder);

        var request = model.Requests.Single();
        request.System.ShouldStartWith("LỜI DẶN RIÊNG");
        request.User.ShouldStartWith("MÓN Cá lóc kho tộ");
    }

    // ---- what is wrong with the inputs ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_missing_prompt_file_is_an_error_naming_it()
    {
        using var data = new TempData();
        var empty = Path.Combine(data.Root, "trong");
        Directory.CreateDirectory(empty);
        File.WriteAllText(Path.Combine(empty, "system.md"), "x");
        var model = Good();

        var (code, _, error) = await data.RunAsync(TempData.Env(model), "draft", "--prompts", empty);

        code.ShouldBe(CommandLine.Incomplete);
        error.ShouldContain("dish.md");
        model.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_prompt_with_a_place_nothing_fills_is_an_error_and_nothing_is_asked()
    {
        using var data = new TempData();
        var folder = Path.Combine(data.Root, "sai");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "system.md"), "Danh sách {{ingredient_catalog}} {{khong_co}}");
        File.WriteAllText(Path.Combine(folder, "dish.md"), "Món {{name}}");
        var model = Good();

        var (code, _, error) = await data.RunAsync(TempData.Env(model), "draft", "--prompts", folder);

        code.ShouldBe(CommandLine.Incomplete);
        error.ShouldContain("Prompt sai");
        error.ShouldContain("{{khong_co}}");
        model.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_missing_dish_list_is_an_error_with_the_path()
    {
        using var data = new TempData();
        File.Delete(Path.Combine(data.Root, "plan", "dish-list.csv"));
        var model = Good();

        var (code, _, error) = await data.RunAsync(TempData.Env(model), "draft");

        code.ShouldBe(CommandLine.Incomplete);
        error.ShouldContain("dish-list.csv");
        model.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_dish_list_in_the_wrong_shape_is_an_error_naming_the_line()
    {
        using var data = new TempData("name,slot,regions\nCá kho,main,south\n");
        var model = Good();

        var (code, _, error) = await data.RunAsync(TempData.Env(model), "draft");

        code.ShouldBe(CommandLine.Incomplete);
        error.ShouldContain("Danh sách món sai");
        error.ShouldContain("line 2");
        model.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Data_that_cannot_be_read_stops_the_run_and_lists_what_is_wrong()
    {
        using var data = new TempData();
        File.Delete(Path.Combine(data.Root, "config", "spinner-defaults.json"));
        var model = Good();

        var (code, _, error) = await data.RunAsync(TempData.Env(model), "draft");

        code.ShouldBe(CommandLine.Incomplete);
        error.ShouldContain("spinner-defaults.json");
        model.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_data_folder_that_does_not_exist_is_an_error()
    {
        using var data = new TempData();
        var output = new StringWriter();
        var error = new StringWriter();

        var code = await CommandLine.RunAsync(["draft", "--data", Path.Combine(data.Root, "khong-co")], output, error, TempData.Env(Good()), TestContext.Current.CancellationToken);

        code.ShouldBe(CommandLine.Incomplete);
        error.ToString().ShouldContain("khong-co");
    }

    // ---- the key, the model and the effort -------------------------------------------------------------------------------------------

    private static FakeAnthropicApi Api() => FakeAnthropicApi.Always(_ => FakeAnthropicApi.Message(Answers.Recipe().ToJsonString()));

    [Fact]
    public async Task Without_any_key_nothing_is_asked_and_the_way_to_set_one_is_given()
    {
        using var data = new TempData();
        var api = Api();

        var (code, _, error) = await data.RunAsync(TempData.Env(handler: api), "draft");

        code.ShouldBe(CommandLine.Incomplete);
        error.ShouldContain("Claude:ApiKey");
        error.ShouldContain("ANTHROPIC_API_KEY");
        api.Requests.ShouldBeEmpty();
        data.DraftFiles().ShouldBeEmpty();
    }

    [Fact]
    public async Task A_key_from_user_secrets_is_sent_and_never_printed()
    {
        using var data = new TempData();
        var api = Api();

        var (code, output, error) = await data.RunAsync(TempData.Env(settings: new() { [CommandLine.ApiKeySetting] = Secret }, handler: api), "draft", "--limit", "1");

        code.ShouldBe(CommandLine.Success);
        api.Requests.ShouldHaveSingleItem().ApiKey.ShouldBe(Secret);
        output.ShouldNotContain(Secret);
        error.ShouldNotContain(Secret);
        data.DraftFiles().ShouldNotBeEmpty();
        foreach (var file in Directory.GetFiles(data.DraftsFolder))
        {
            File.ReadAllText(file).ShouldNotContain(Secret);
        }
    }

    [Fact]
    public async Task The_environment_variable_is_used_when_there_is_no_secret_and_the_secret_wins_when_there_are_both()
    {
        using var data = new TempData();
        var fromVariable = Api();
        await data.RunAsync(TempData.Env(handler: fromVariable, apiKeyVariable: "sk-from-variable"), "draft", "--limit", "1");
        fromVariable.Requests.ShouldHaveSingleItem().ApiKey.ShouldBe("sk-from-variable");

        using var other = new TempData();
        var both = Api();
        await other.RunAsync(TempData.Env(settings: new() { [CommandLine.ApiKeySetting] = "sk-from-secret" }, handler: both, apiKeyVariable: "sk-from-variable"), "draft", "--limit", "1");
        both.Requests.ShouldHaveSingleItem().ApiKey.ShouldBe("sk-from-secret");
    }

    [Fact]
    public async Task A_blank_secret_counts_as_none()
    {
        using var data = new TempData();
        var api = Api();

        await data.RunAsync(TempData.Env(settings: new() { [CommandLine.ApiKeySetting] = "   " }, handler: api, apiKeyVariable: "sk-from-variable"), "draft", "--limit", "1");

        api.Requests.Single().ApiKey.ShouldBe("sk-from-variable");
    }

    [Fact]
    public async Task The_default_model_and_effort_are_opus_5_5_and_medium_and_both_can_be_set()
    {
        using var data = new TempData();
        var defaults = Api();
        var (_, output, _) = await data.RunAsync(TempData.Env(settings: new() { [CommandLine.ApiKeySetting] = Secret }, handler: defaults), "draft", "--limit", "1");

        var body = defaults.Requests.Single().Body;
        body["model"]!.GetValue<string>().ShouldBe("claude-opus-5-5");
        body["output_config"]!["effort"]!.GetValue<string>().ShouldBe("medium");
        output.ShouldContain("claude-opus-5-5 (effort medium)");

        using var other = new TempData();
        var chosen = Api();
        await other.RunAsync(
            TempData.Env(settings: new() { [CommandLine.ApiKeySetting] = Secret, [CommandLine.ModelSetting] = " claude-sonnet-5-5 ", [CommandLine.EffortSetting] = "high" }, handler: chosen),
            "draft",
            "--limit",
            "1");

        var chosenBody = chosen.Requests.Single().Body;
        chosenBody["model"]!.GetValue<string>().ShouldBe("claude-sonnet-5-5");
        chosenBody["output_config"]!["effort"]!.GetValue<string>().ShouldBe("high");
    }

    [Fact]
    public async Task An_effort_that_does_not_exist_is_an_error_before_anything_is_asked()
    {
        using var data = new TempData();
        var api = Api();

        var (code, _, error) = await data.RunAsync(TempData.Env(settings: new() { [CommandLine.ApiKeySetting] = Secret, [CommandLine.EffortSetting] = "extreme" }, handler: api), "draft");

        code.ShouldBe(CommandLine.Incomplete);
        error.ShouldContain("extreme");
        error.ShouldContain("low, medium, high, max");
        api.Requests.ShouldBeEmpty();
    }

    // ---- exit codes ------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_dish_the_model_would_not_draft_makes_the_run_incomplete_and_is_said_on_the_error_stream()
    {
        using var data = new TempData();
        var model = FakeModel.Always(_ => Answers.Raw(null, "refusal", "cyber"));

        var (code, output, error) = await data.RunAsync(TempData.Env(model), "draft", "--limit", "1");

        code.ShouldBe(CommandLine.Incomplete);
        error.ShouldContain("ca-loc-kho-to: không soạn được");
        error.ShouldContain("cyber");
        output.ShouldContain("không soạn được 1");
    }

    [Fact]
    public async Task Invalid_drafts_are_flagged_but_are_not_a_failed_run()
    {
        using var data = new TempData();
        var model = FakeModel.Answering(Answers.Recipe(r =>
        {
            r["steps"]![0]!["activeMinutes"] = 0;
            r["steps"]![0]!["passiveMinutes"] = 0;
        }));

        var (code, output, _) = await data.RunAsync(TempData.Env(model), "draft", "--limit", "1");

        code.ShouldBe(CommandLine.Success);
        output.ShouldContain("ca-loc-kho-to: invalid");
        output.ShouldContain("không hợp lệ 1");
    }

    [Fact]
    public async Task A_refused_key_stops_the_run_says_so_and_is_incomplete()
    {
        using var data = new TempData();
        var api = FakeAnthropicApi.Always(_ => FakeAnthropicApi.Error(HttpStatusCode.Unauthorized, "authentication_error", "invalid x-api-key"));

        var (code, _, error) = await data.RunAsync(TempData.Env(settings: new() { [CommandLine.ApiKeySetting] = Secret }, handler: api), "draft");

        code.ShouldBe(CommandLine.Incomplete);
        error.ShouldContain("Dừng: API không nhận khoá");
        error.ShouldNotContain(Secret);
        data.DraftFiles().ShouldBeEmpty();
    }

    [Fact]
    public async Task Missing_ingredients_show_up_as_warnings_in_the_summary()
    {
        using var data = new TempData();
        var model = FakeModel.Answering(Answers.Recipe(r => r["missingIngredients"] = new JsonArray(Answers.Missing("Cải ngọt", 80))));

        var (code, output, _) = await data.RunAsync(TempData.Env(model), "draft", "--limit", "1");

        code.ShouldBe(CommandLine.Success);
        output.ShouldContain("ca-loc-kho-to: warnings");
        output.ShouldContain("cảnh báo 1");
    }
}
