using System.Text.Json;
using System.Text.Json.Nodes;
using MamCom.Core.Models;
using MamCom.DataBuilder.Loading;
using MamCom.DataBuilder.Validation;
using MamCom.RecipeDrafter.Drafting;
using MamCom.RecipeDrafter.Model;
using MamCom.RecipeDrafter.Plan;

namespace MamCom.RecipeDrafter.Tests;

public class DrafterTests
{
    private static readonly DishRequest Fish = Answers.FishMain;
    private static readonly DishRequest Tofu = new("dau-phu-sot-ca-chua", "Đậu phụ sốt cà chua", DishSlot.Main, [Region.North, Region.South]);
    private static readonly DishRequest Soup = new("canh-cai-ngot", "Canh cải ngọt", DishSlot.Soup, []);

    private static JsonObject ReportOf(TempData data, string id) => JsonNode.Parse(File.ReadAllText(Path.Combine(data.DraftsFolder, id + ".report.json")))!.AsObject();

    // ---- a good draft -------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_dish_gets_a_draft_file_and_a_report_that_says_it_is_valid()
    {
        using var data = new TempData();
        var reported = new List<DishOutcome>();

        var result = await data.DraftAsync(FakeModel.Answering(Answers.Recipe()), [Fish], reported: reported);

        result.StoppedBecause.ShouldBeNull();
        result.ModelCalls.ShouldBe(1);
        data.DraftFiles().ShouldBe(["ca-loc-kho-to.json", "ca-loc-kho-to.report.json"]);
        var outcome = reported.ShouldHaveSingleItem();
        outcome.Kind.ShouldBe(DraftOutcomeKind.Drafted);
        outcome.Validation.ShouldBe("valid");

        var report = ReportOf(data, "ca-loc-kho-to");
        report["dishId"]!.GetValue<string>().ShouldBe("ca-loc-kho-to");
        report["model"]!.GetValue<string>().ShouldBe("fake-model");
        report["draftedAt"]!.GetValue<string>().ShouldBe("2026-10-19");
        report["validation"]!.GetValue<string>().ShouldBe("valid");
        report["stopReason"]!.GetValue<string>().ShouldBe("end_turn");
        report["problems"]!.AsArray().ShouldBeEmpty();
        report["missingIngredients"]!.AsArray().ShouldBeEmpty();
        report["usage"]!["inputTokens"]!.GetValue<int>().ShouldBe(1200);
        report["usage"]!["cacheCreationInputTokens"]!.GetValue<int>().ShouldBe(1000);
    }

    [Fact]
    public async Task A_valid_draft_passes_the_data_builder_once_it_is_moved_into_the_dishes_folder()
    {
        using var data = new TempData();
        await data.DraftAsync(FakeModel.Answering(Answers.Recipe()), [Fish]);

        File.Move(Path.Combine(data.DraftsFolder, "ca-loc-kho-to.json"), Path.Combine(data.Root, "dishes", "ca-loc-kho-to.json"));
        var report = DataValidator.Validate(DataLoader.Load(data.Root), ValidationMode.Dev);

        report.Errors.ShouldBeEmpty();
        report.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_draft_is_a_draft_by_an_llm_in_the_canonical_format()
    {
        using var data = new TempData();
        await data.DraftAsync(FakeModel.Answering(Answers.Recipe()), [Fish]);

        var text = File.ReadAllText(Path.Combine(data.DraftsFolder, "ca-loc-kho-to.json"));

        text.ShouldContain("\"status\": \"draft\"");
        text.ShouldContain("\"draftedBy\": \"llm\"");
        text.ShouldContain("\"reviewedBy\": null");
        text.ShouldEndWith("}\n");
        text.ShouldNotContain('\r');
        data.Schemas.Validate("dish", text).ShouldBeEmpty();
    }

    // ---- what the model is told -------------------------------------------------------------------------------------------------

    [Fact]
    public async Task The_model_sees_every_ingredient_the_thresholds_and_the_dish()
    {
        using var data = new TempData();
        var model = FakeModel.Answering(Answers.Recipe());

        await data.DraftAsync(model, [Fish]);

        var request = model.Requests.ShouldHaveSingleItem();
        request.System.ShouldContain("- gao: Gạo (ngu-coc)");
        request.System.ShouldContain("- thit-ba-chi: Thịt ba chỉ / Thịt ba rọi (thit-heo; đạm: heo)");
        request.System.ShouldContain("- ca-loc: Cá lóc / Cá quả (ca; đạm: ca)");   // every regional name, the default first
        System.Text.RegularExpressions.Regex.Matches(request.System, "^- [a-z0-9]+(-[a-z0-9]+)*: ", System.Text.RegularExpressions.RegexOptions.Multiline).Count.ShouldBe(33);
        request.System.ShouldContain("từ 50 đến 150 g");
        request.System.ShouldNotContain("{{");
        request.User.ShouldContain("Món cần soạn: Cá lóc kho tộ");
        request.User.ShouldContain("món mặn (`man`)");
        request.User.ShouldContain("Vùng đặc trưng: south");
        request.User.ShouldNotContain("{{");
    }

    [Fact]
    public async Task The_season_list_in_the_prompt_is_the_one_in_the_config_without_repeats()
    {
        using var data = new TempData();
        var model = FakeModel.Answering(Answers.Recipe());

        await data.DraftAsync(model, [Fish]);

        model.Requests.Single().System.ShouldContain("winter, spring, summer, autumn");
    }

    [Fact]
    public async Task The_instructions_are_the_same_for_every_dish_so_they_can_be_cached_and_only_the_dish_changes()
    {
        using var data = new TempData();
        var model = FakeModel.Answering(Answers.Recipe());

        await data.DraftAsync(model, [Fish, Tofu, Soup]);

        model.Requests.Select(r => r.System).Distinct().Count().ShouldBe(1);
        model.Requests.Select(r => r.JsonSchema).Distinct().Count().ShouldBe(1);
        model.Requests.Select(r => r.User).Distinct().Count().ShouldBe(3);
        model.Requests[1].User.ShouldContain("Vùng đặc trưng: north, south");
        model.Requests[2].User.ShouldContain("Vùng đặc trưng: không đặc trưng vùng nào");
        model.Requests[2].User.ShouldContain("canh (`canh`)");
    }

    [Fact]
    public async Task The_schema_limits_ingredient_ids_to_the_ingredients_that_exist_and_protein_sources_to_known_ones()
    {
        using var data = new TempData();
        var model = FakeModel.Answering(Answers.Recipe());

        await data.DraftAsync(model, [Fish]);

        var schema = JsonNode.Parse(model.Requests.Single().JsonSchema)!;
        var ids = schema["properties"]!["ingredients"]!["items"]!["properties"]!["ingredientId"]!["enum"]!.AsArray().Select(n => n!.GetValue<string>()).ToArray();
        ids.Length.ShouldBe(33);
        ids.ShouldContain("gao");
        ids.ShouldBe(ids.Order(StringComparer.Ordinal));
        schema["properties"]!["proteinSource"]!["enum"]!.AsArray().Select(n => n!.GetValue<string>()).ShouldBe(["bo", "chay", "ca", "ga", "heo", "none", "trung", "tom"], ignoreOrder: true);
        schema["properties"]!["seasons"]!["items"]!["enum"]!.AsArray().Select(n => n!.GetValue<string>()).ShouldBe(["all", "autumn", "spring", "summer", "winter"]);
    }

    // ---- findings on a draft ----------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Missing_ingredients_are_listed_in_the_report_and_make_it_a_warning()
    {
        using var data = new TempData();
        var answer = Answers.Recipe(r => r["missingIngredients"] = new JsonArray(Answers.Missing("Cải ngọt", 80, "cắt khúc"), Answers.Missing("Gừng", 3)));

        var reported = new List<DishOutcome>();
        await data.DraftAsync(FakeModel.Answering(answer), [Fish], reported: reported);

        reported.Single().Validation.ShouldBe("warnings");
        var report = ReportOf(data, "ca-loc-kho-to");
        report["validation"]!.GetValue<string>().ShouldBe("warnings");
        report["missingIngredients"]!.AsArray().Select(m => m!["name"]!.GetValue<string>()).ShouldBe(["Cải ngọt", "Gừng"]);
        report["missingIngredients"]![0]!["note"]!.GetValue<string>().ShouldBe("cắt khúc");
        report["missingIngredients"]![1]!["note"].ShouldBeNull();
        report["problems"]!.AsArray().Count(p => p!["code"]!.GetValue<string>() == "missing-ingredient").ShouldBe(2);
        report["problems"]!.AsArray().ShouldAllBe(p => p!["severity"]!.GetValue<string>() == "warning");
        File.ReadAllText(Path.Combine(data.DraftsFolder, "ca-loc-kho-to.json")).ShouldNotContain("cai-ngot");
    }

    [Fact]
    public async Task A_draft_that_breaks_a_rule_is_still_written_and_flagged_invalid_with_the_place()
    {
        using var data = new TempData();
        var answer = Answers.Recipe(r =>
        {
            r["steps"]![1]!["activeMinutes"] = 0;
            r["steps"]![1]!["passiveMinutes"] = 0;
        });

        var reported = new List<DishOutcome>();
        await data.DraftAsync(FakeModel.Answering(answer), [Fish], reported: reported);

        reported.Single().Validation.ShouldBe("invalid");
        data.DraftFiles().ShouldContain("ca-loc-kho-to.json");
        var report = ReportOf(data, "ca-loc-kho-to");
        report["validation"]!.GetValue<string>().ShouldBe("invalid");
        report["problems"]!.AsArray().ShouldContain(p => p!["severity"]!.GetValue<string>() == "error" && p["location"]!.GetValue<string>() == "/steps/1");
    }

    [Fact]
    public async Task A_draft_with_both_an_error_and_a_warning_is_invalid_not_just_warnings()
    {
        using var data = new TempData();
        var answer = Answers.Recipe(r =>
        {
            r["steps"]![1]!["activeMinutes"] = 0;
            r["steps"]![1]!["passiveMinutes"] = 0;
            r["missingIngredients"] = new JsonArray(Answers.Missing("Cải ngọt", 80));
        });

        var reported = new List<DishOutcome>();
        await data.DraftAsync(FakeModel.Answering(answer), [Fish], reported: reported);

        reported.Single().Validation.ShouldBe("invalid");
        reported.Single().Message.ShouldContain("1 lỗi, 1 cảnh báo");
        ReportOf(data, "ca-loc-kho-to")["validation"]!.GetValue<string>().ShouldBe("invalid");
    }

    [Fact]
    public async Task An_ingredient_whose_regional_name_repeats_the_default_is_listed_under_one_name()
    {
        using var data = new TempData();
        File.WriteAllText(
            Path.Combine(data.Root, "ingredients", "thit-ba-chi.json"),
            File.ReadAllText(Path.Combine(data.Root, "ingredients", "thit-ba-chi.json")).Replace("\"default\": \"Thịt ba chỉ\",", "\"default\": \"Thịt ba chỉ\",\n    \"north\": \"Thịt ba chỉ\","));
        var model = FakeModel.Answering(Answers.Recipe());

        await data.DraftAsync(model, [Fish]);

        model.Requests.Single().System.ShouldContain("- thit-ba-chi: Thịt ba chỉ / Thịt ba rọi (thit-heo; đạm: heo)");
    }

    [Fact]
    public async Task A_warning_only_draft_is_flagged_as_warnings()
    {
        using var data = new TempData();

        var reported = new List<DishOutcome>();
        await data.DraftAsync(FakeModel.Answering(Answers.Recipe(r => r["ingredients"]![0]!["gramsEdiblePerServing"] = 400)), [Fish], reported: reported);

        reported.Single().Validation.ShouldBe("warnings");
        ReportOf(data, "ca-loc-kho-to")["problems"]![0]!["code"]!.GetValue<string>().ShouldBe("protein-range");
    }

    // ---- when the model gives nothing usable ----------------------------------------------------------------------------------------

    [Theory]
    [InlineData("refusal", "model-refusal")]
    [InlineData("max_tokens", "model-truncated")]
    public async Task A_refusal_or_a_cut_off_answer_is_a_failed_report_and_no_draft(string stopReason, string code)
    {
        using var data = new TempData();
        var reported = new List<DishOutcome>();

        await data.DraftAsync(FakeModel.Always(_ => Answers.Raw(stopReason == "refusal" ? null : "{\"seasons\": [", stopReason, stopReason == "refusal" ? "cyber" : null)), [Fish], reported: reported);

        data.DraftFiles().ShouldBe(["ca-loc-kho-to.report.json"]);
        reported.Single().Kind.ShouldBe(DraftOutcomeKind.Failed);
        var report = ReportOf(data, "ca-loc-kho-to");
        report["validation"]!.GetValue<string>().ShouldBe("failed");
        report["stopReason"]!.GetValue<string>().ShouldBe(stopReason);
        report["problems"]![0]!["code"]!.GetValue<string>().ShouldBe(code);
        report["problems"]![0]!["severity"]!.GetValue<string>().ShouldBe("error");
        report["usage"]!["outputTokens"]!.GetValue<int>().ShouldBe(800);
    }

    [Fact]
    public async Task A_refusal_names_the_category_the_model_gave()
    {
        using var data = new TempData();
        var reported = new List<DishOutcome>();

        await data.DraftAsync(FakeModel.Always(_ => Answers.Raw(null, "refusal", "general_harms")), [Fish], reported: reported);

        reported.Single().Message.ShouldContain("general_harms");
    }

    [Theory]
    [InlineData(null, "model-empty")]
    [InlineData("không phải JSON", "model-bad-answer")]
    [InlineData("{}", "model-bad-answer")]
    [InlineData("[]", "model-bad-answer")]
    [InlineData("null", "model-bad-answer")]
    public async Task An_answer_that_is_not_the_recipe_is_a_failed_report_and_no_draft(string? text, string code)
    {
        using var data = new TempData();

        await data.DraftAsync(FakeModel.Always(_ => Answers.Raw(text)), [Fish]);

        data.DraftFiles().ShouldBe(["ca-loc-kho-to.report.json"]);
        ReportOf(data, "ca-loc-kho-to")["problems"]![0]!["code"]!.GetValue<string>().ShouldBe(code);
    }

    [Theory]
    [InlineData("status", "reviewed")]
    [InlineData("kcal", 400)]
    [InlineData("name", "Món khác")]
    public async Task A_member_the_model_may_not_write_fails_the_answer_so_it_can_never_set_a_status_or_a_number(string member, object value)
    {
        using var data = new TempData();
        var answer = Answers.Recipe(r => r[member] = value is int n ? JsonValue.Create(n) : JsonValue.Create(value.ToString()!));

        await data.DraftAsync(FakeModel.Answering(answer), [Fish]);

        data.DraftFiles().ShouldBe(["ca-loc-kho-to.report.json"]);
        ReportOf(data, "ca-loc-kho-to")["problems"]![0]!["code"]!.GetValue<string>().ShouldBe("model-bad-answer");
    }

    [Theory]
    [InlineData("spicy", "hot")]
    [InlineData("cookingMethod", "fry")]
    public async Task A_value_the_dish_model_refuses_is_a_failed_report(string member, string value)
    {
        using var data = new TempData();

        await data.DraftAsync(FakeModel.Answering(Answers.Recipe(r => r[member] = value)), [Fish]);

        data.DraftFiles().ShouldBe(["ca-loc-kho-to.report.json"]);
        ReportOf(data, "ca-loc-kho-to")["problems"]![0]!["code"]!.GetValue<string>().ShouldBe("model-bad-value");
    }

    [Theory]
    [InlineData(ModelFailure.Rejected)]
    [InlineData(ModelFailure.Unavailable)]
    [InlineData(ModelFailure.Network)]
    [InlineData(ModelFailure.Other)]
    public async Task A_call_that_fails_for_one_dish_is_a_failed_report_and_the_run_goes_on(ModelFailure kind)
    {
        using var data = new TempData();
        var model = new FakeModel((_, n) => n == 1 ? throw new ModelException(kind, "boom") : Answers.Reply(Answers.Recipe()));
        var reported = new List<DishOutcome>();

        var result = await data.DraftAsync(model, [Fish, Tofu], reported: reported);

        result.StoppedBecause.ShouldBeNull();
        reported.Select(o => o.Kind).ShouldBe([DraftOutcomeKind.Failed, DraftOutcomeKind.Drafted]);
        ReportOf(data, "ca-loc-kho-to")["problems"]![0]!["code"]!.GetValue<string>().ShouldBe("model-error");
        data.DraftFiles().ShouldBe(["ca-loc-kho-to.report.json", "dau-phu-sot-ca-chua.json", "dau-phu-sot-ca-chua.report.json"]);
    }

    [Theory]
    [InlineData(ModelFailure.Auth, "khoá")]
    [InlineData(ModelFailure.RateLimited, "429")]
    [InlineData(ModelFailure.OutOfCredit, "hết tín dụng")]
    public async Task A_refused_key_a_rate_limit_or_no_credit_stops_the_run_without_a_report_for_that_dish(ModelFailure kind, string said)
    {
        using var data = new TempData();
        var model = new FakeModel((_, n) => n == 1 ? Answers.Reply(Answers.Recipe()) : throw new ModelException(kind, "boom"));

        var result = await data.DraftAsync(model, [Fish, Tofu, Soup]);

        result.StoppedBecause.ShouldNotBeNull().ShouldContain(said);
        result.LimitReached.ShouldBeFalse();   // it stopped for a reason, not because the limit was reached
        result.ModelCalls.ShouldBe(2);
        result.Usage.ShouldBe(Answers.Usage);  // what the first dish cost is still counted
        model.Requests.Count.ShouldBe(2);
        data.DraftFiles().ShouldBe(["ca-loc-kho-to.json", "ca-loc-kho-to.report.json"]);
    }

    [Fact]
    public async Task No_credit_on_the_first_dish_stops_the_run_at_once_with_one_call_and_nothing_written()
    {
        using var data = new TempData();
        var model = new FakeModel((_, _) => throw new ModelException(ModelFailure.OutOfCredit, "no credit"));
        var reported = new List<DishOutcome>();

        var result = await data.DraftAsync(model, [Fish, Tofu, Soup], reported: reported);

        model.Requests.Count.ShouldBe(1);
        result.ModelCalls.ShouldBe(1);
        result.StoppedBecause.ShouldNotBeNull().ShouldContain("Plans & Billing");
        result.LimitReached.ShouldBeFalse();
        reported.ShouldBeEmpty();
        data.DraftFiles().ShouldBeEmpty();
    }

    [Fact]
    public async Task A_failed_dish_is_tried_again_on_the_next_run_because_it_has_no_draft()
    {
        using var data = new TempData();
        await data.DraftAsync(FakeModel.Always(_ => Answers.Raw(null, "refusal")), [Fish]);
        var model = FakeModel.Answering(Answers.Recipe());

        var result = await data.DraftAsync(model, [Fish]);

        result.ModelCalls.ShouldBe(1);
        data.DraftFiles().ShouldBe(["ca-loc-kho-to.json", "ca-loc-kho-to.report.json"]);
        ReportOf(data, "ca-loc-kho-to")["validation"]!.GetValue<string>().ShouldBe("valid");
    }

    // ---- resume, limit, only, redo --------------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_dish_with_a_draft_is_skipped_and_the_draft_is_left_alone()
    {
        using var data = new TempData();
        await data.DraftAsync(FakeModel.Answering(Answers.Recipe()), [Fish]);
        var before = File.ReadAllText(Path.Combine(data.DraftsFolder, "ca-loc-kho-to.json"));
        var model = FakeModel.Answering(Answers.Recipe(r => r["spicy"] = "required"));
        var reported = new List<DishOutcome>();

        var result = await data.DraftAsync(model, [Fish], reported: reported);

        result.ModelCalls.ShouldBe(0);
        model.Requests.ShouldBeEmpty();
        reported.Single().Kind.ShouldBe(DraftOutcomeKind.SkippedDraft);
        File.ReadAllText(Path.Combine(data.DraftsFolder, "ca-loc-kho-to.json")).ShouldBe(before);
    }

    [Fact]
    public async Task A_dish_a_person_has_taken_over_is_never_touched_even_with_redo()
    {
        using var data = new TempData();
        File.WriteAllText(Path.Combine(data.Root, "dishes", "ca-loc-kho-to.json"), "mine");
        var model = FakeModel.Answering(Answers.Recipe());
        var reported = new List<DishOutcome>();

        await data.DraftAsync(model, [Fish], new DrafterOptions(null, null, Redo: true), reported);

        model.Requests.ShouldBeEmpty();
        reported.Single().Kind.ShouldBe(DraftOutcomeKind.SkippedFinal);
        File.ReadAllText(Path.Combine(data.Root, "dishes", "ca-loc-kho-to.json")).ShouldBe("mine");
        data.DraftFiles().ShouldBeEmpty();
    }

    [Fact]
    public async Task Redo_drafts_again_and_replaces_the_draft_and_the_report()
    {
        using var data = new TempData();
        await data.DraftAsync(FakeModel.Answering(Answers.Recipe()), [Fish]);

        await data.DraftAsync(FakeModel.Answering(Answers.Recipe(r => r["spicy"] = "required")), [Fish], new DrafterOptions(null, null, Redo: true));

        File.ReadAllText(Path.Combine(data.DraftsFolder, "ca-loc-kho-to.json")).ShouldContain("\"spicy\": \"required\"");
    }

    [Fact]
    public async Task The_limit_counts_the_dishes_asked_about_and_not_the_ones_skipped_and_a_second_run_goes_on()
    {
        using var data = new TempData();
        File.WriteAllText(Path.Combine(data.Root, "dishes", "dau-phu-sot-ca-chua.json"), "mine");
        var model = FakeModel.Answering(Answers.Recipe());
        var dishes = new[] { Tofu, Fish, Soup, new DishRequest("rau-cai", "Rau cải", DishSlot.Vegetable, []) };

        var first = await data.DraftAsync(model, dishes, new DrafterOptions(null, Limit: 2, false));

        first.ModelCalls.ShouldBe(2);
        first.LimitReached.ShouldBeTrue();
        data.DraftFiles().Where(f => !f.EndsWith(".report.json")).ShouldBe(["ca-loc-kho-to.json", "canh-cai-ngot.json"]);

        var second = await data.DraftAsync(model, dishes, new DrafterOptions(null, Limit: 2, false));

        second.ModelCalls.ShouldBe(1);
        second.LimitReached.ShouldBeFalse();
        data.DraftFiles().Where(f => !f.EndsWith(".report.json")).ShouldBe(["ca-loc-kho-to.json", "canh-cai-ngot.json", "rau-cai.json"]);
    }

    [Fact]
    public async Task A_limit_that_is_not_reached_is_not_reported_as_reached()
    {
        using var data = new TempData();

        var result = await data.DraftAsync(FakeModel.Answering(Answers.Recipe()), [Fish], new DrafterOptions(null, Limit: 5, false));

        result.LimitReached.ShouldBeFalse();
    }

    [Fact]
    public async Task Only_drafts_the_dishes_named_and_ignores_the_rest_silently()
    {
        using var data = new TempData();
        var model = FakeModel.Answering(Answers.Recipe());
        var reported = new List<DishOutcome>();

        await data.DraftAsync(model, [Fish, Tofu, Soup], new DrafterOptions(["canh-cai-ngot", "dau-phu-sot-ca-chua"], null, false), reported);

        reported.Select(o => o.DishId).ShouldBe(["dau-phu-sot-ca-chua", "canh-cai-ngot"]);
        model.Requests.Count.ShouldBe(2);
    }

    // ---- bookkeeping ------------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Token_usage_is_added_up_over_every_call_including_the_failed_ones()
    {
        using var data = new TempData();
        var model = new FakeModel((_, n) => n == 1
            ? Answers.Reply(Answers.Recipe(), usage: new TokenUsage(100, 50, 10, 5))
            : Answers.Raw(null, "refusal"));

        var result = await data.DraftAsync(model, [Fish, Tofu]);

        result.ModelCalls.ShouldBe(2);
        result.Usage.ShouldBe(new TokenUsage(100 + 1200, 50 + 800, 10 + 1000, 5));
    }

    [Fact]
    public async Task A_run_writes_only_under_dishes_drafts_and_leaves_no_temporary_file()
    {
        using var data = new TempData();
        var before = data.Snapshot("ingredients", "schemas", "config", "plan");
        var dishesBefore = Directory.GetFiles(Path.Combine(data.Root, "dishes")).Select(f => (f, File.ReadAllText(f))).ToArray();

        await data.DraftAsync(FakeModel.Answering(Answers.Recipe()), [Fish, Tofu]);

        data.Snapshot("ingredients", "schemas", "config", "plan").ShouldBe(before);
        Directory.GetFiles(Path.Combine(data.Root, "dishes")).Select(f => (f, File.ReadAllText(f))).ToArray().ShouldBe(dishesBefore);
        data.DraftFiles().ShouldAllBe(f => f.EndsWith(".json"));
    }

    [Fact]
    public async Task Cancelling_stops_the_run_between_dishes_leaving_only_whole_drafts()
    {
        using var data = new TempData();
        using var cancellation = new CancellationTokenSource();
        var model = new FakeModel((_, _) =>
        {
            cancellation.Cancel();   // seen before the next dish is asked
            return Answers.Reply(Answers.Recipe());
        });
        var drafter = new Drafter(model, new FixedClock(new DateOnly(2026, 10, 19)));

        await Should.ThrowAsync<OperationCanceledException>(() =>
            drafter.RunAsync(data.Root, data.Load(), data.Schemas, [Fish, Tofu], TempData.Prompts(), new DrafterOptions(null, null, false), _ => { }, cancellation.Token));

        model.Requests.Count.ShouldBe(1);
        data.DraftFiles().ShouldBe(["ca-loc-kho-to.json", "ca-loc-kho-to.report.json"]);
        Should.NotThrow(() => JsonDocument.Parse(File.ReadAllText(Path.Combine(data.DraftsFolder, "ca-loc-kho-to.json"))));
    }

    [Fact]
    public async Task A_data_folder_without_usable_config_cannot_be_drafted_from()
    {
        using var data = new TempData();
        File.Delete(Path.Combine(data.Root, "config", "spinner-defaults.json"));
        var drafter = new Drafter(FakeModel.Answering(Answers.Recipe()), new FixedClock(new DateOnly(2026, 10, 19)));

        await Should.ThrowAsync<InvalidOperationException>(() =>
            drafter.RunAsync(data.Root, data.Load(), data.Schemas, [Fish], TempData.Prompts(), new DrafterOptions(null, null, false), _ => { }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_prompt_with_a_place_nothing_fills_fails_before_any_call()
    {
        using var data = new TempData();
        var model = FakeModel.Answering(Answers.Recipe());
        var drafter = new Drafter(model, new FixedClock(new DateOnly(2026, 10, 19)));
        var prompts = new PromptSet("Danh sách: {{ingredient_catalog}} {{khong_co}}", "Món {{name}}");

        await Should.ThrowAsync<FormatException>(() =>
            drafter.RunAsync(data.Root, data.Load(), data.Schemas, [Fish], prompts, new DrafterOptions(null, null, false), _ => { }, TestContext.Current.CancellationToken));
        model.Requests.ShouldBeEmpty();
    }

    [Fact]
    public void The_report_file_is_indented_ends_with_one_line_feed_and_keeps_vietnamese_readable()
    {
        var report = new DraftReport("x", "m", new DateOnly(2026, 10, 19), "warnings", "end_turn",
            [new DraftProblem("warning", "missing-ingredient", string.Empty, "Thiếu nguyên liệu.")], [new MissingIngredient("Cải ngọt", 80, null)], Answers.Usage);

        var text = DraftFiles.SerializeReport(report);

        text.ShouldStartWith("{\n  \"dishId\": \"x\"");
        text.ShouldEndWith("}\n");
        text.ShouldNotEndWith("\n\n");
        text.ShouldNotContain('\r');
        text.ShouldContain("Thiếu nguyên liệu.");
        JsonDocument.Parse(text).RootElement.GetProperty("draftedAt").GetString().ShouldBe("2026-10-19");
    }
}
