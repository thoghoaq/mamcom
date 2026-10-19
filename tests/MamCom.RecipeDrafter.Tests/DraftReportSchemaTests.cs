using System.Text.Json;
using System.Text.Json.Nodes;
using MamCom.DataBuilder.Schemas;
using MamCom.RecipeDrafter.Drafting;
using MamCom.RecipeDrafter.Model;

namespace MamCom.RecipeDrafter.Tests;

/// <summary>The report is read by Studio, so its shape is a contract: the schema, the records and the files written must agree.</summary>
public class DraftReportSchemaTests
{
    private static readonly SchemaSet Schemas = SchemaSet.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas"));

    private static readonly DraftProblem Warning = new("warning", "missing-ingredient", string.Empty, "Thiếu nguyên liệu.");
    private static readonly DraftProblem Error = new("error", "schema", "/steps/0", "anyOf: step has no minutes.");

    private static DraftReport Report(string validation, params DraftProblem[] problems) =>
        new("ca-loc-kho-to", "claude-opus-5-5", new DateOnly(2026, 10, 19), validation, validation == "failed" ? "refusal" : "end_turn", problems, [], Answers.Usage);

    private static IReadOnlyList<SchemaIssue> Check(DraftReport report) => Schemas.Validate("draft-report", DraftFiles.SerializeReport(report));

    private static JsonObject Broken(Action<JsonObject> change)
    {
        var root = JsonNode.Parse(DraftFiles.SerializeReport(Report("valid")))!.AsObject();
        change(root);
        return root;
    }

    // ---- valid reports ------------------------------------------------------------------------------------------------------

    [Fact]
    public void A_report_of_each_kind_satisfies_the_schema()
    {
        Check(Report("valid")).ShouldBeEmpty();
        Check(Report("warnings", Warning)).ShouldBeEmpty();
        Check(Report("invalid", Error, Warning)).ShouldBeEmpty();
        Check(Report("failed", new DraftProblem("error", "model-refusal", string.Empty, "Model từ chối."))).ShouldBeEmpty();
    }

    [Fact]
    public void A_report_with_missing_ingredients_satisfies_the_schema()
    {
        var report = Report("warnings", Warning) with { MissingIngredients = [new MissingIngredient("Cải ngọt", 80, "cắt khúc"), new MissingIngredient("Gừng", 3, null)] };

        Check(report).ShouldBeEmpty();
    }

    [Fact]
    public async Task The_reports_a_real_run_writes_satisfy_the_schema()
    {
        using var data = new TempData();
        var answers = new Func<Plan.DishRequest, ModelReply>[]
        {
            _ => Answers.Reply(Answers.Recipe()),
            _ => Answers.Reply(Answers.Recipe(r => r["missingIngredients"] = new JsonArray(Answers.Missing("Cải ngọt", 80)))),
            _ => Answers.Raw(null, "refusal", "cyber"),
        };
        var dishes = new[]
        {
            Answers.FishMain,
            new Plan.DishRequest("dau-phu-sot-ca-chua", "Đậu phụ sốt cà chua", MamCom.Core.Models.DishSlot.Main, []),
            new Plan.DishRequest("canh-cai-ngot", "Canh cải ngọt", MamCom.Core.Models.DishSlot.Soup, []),
        };
        var model = new FakeModel((_, n) => answers[n - 1](dishes[n - 1]));

        await data.DraftAsync(model, dishes);

        foreach (var dish in dishes)
        {
            var text = File.ReadAllText(Path.Combine(data.DraftsFolder, dish.Id + ".report.json"));
            Schemas.Validate("draft-report", text).ShouldBeEmpty(dish.Id);
        }
    }

    // ---- the schema bites ----------------------------------------------------------------------------------------------------

    [Fact]
    public void A_valid_report_cannot_carry_problems()
    {
        Check(Report("valid", Warning)).ShouldNotBeEmpty();
    }

    [Fact]
    public void A_warnings_report_needs_a_problem_and_only_warnings()
    {
        Check(Report("warnings")).ShouldNotBeEmpty();
        Check(Report("warnings", Warning, Error)).ShouldNotBeEmpty();
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("failed")]
    public void An_invalid_or_failed_report_needs_an_error_to_name(string validation)
    {
        Check(Report(validation)).ShouldNotBeEmpty();
        Check(Report(validation, Warning)).ShouldNotBeEmpty();
    }

    [Fact]
    public void Each_wrong_thing_is_named_where_it_is()
    {
        var cases = new (string Name, Action<JsonObject> Break, string Location)[]
        {
            ("unknown validation", o => o["validation"] = "fine", "/validation"),
            ("id in capitals", o => o["dishId"] = "Ca-Loc", "/dishId"),
            ("empty model", o => o["model"] = "", "/model"),
            ("date that does not exist", o => o["draftedAt"] = "2026-13-45", "/draftedAt"),
            ("negative tokens", o => o["usage"]!["outputTokens"] = -1, "/usage/outputTokens"),
            ("fractional tokens", o => o["usage"]!["inputTokens"] = 1.5, "/usage/inputTokens"),
            ("extra member", o => o["comment"] = "x", "/comment"),
            ("a missing member", o => o.Remove("usage"), ""),
            ("a problem with no message", o => o["problems"] = new JsonArray(new JsonObject { ["severity"] = "warning", ["code"] = "x", ["location"] = "", ["message"] = "" }), "/problems/0/message"),
            ("a severity that does not exist", o => o["problems"] = new JsonArray(new JsonObject { ["severity"] = "info", ["code"] = "x", ["location"] = "", ["message"] = "m" }), "/problems/0/severity"),
            ("a missing ingredient with no grams", o => o["missingIngredients"] = new JsonArray(new JsonObject { ["name"] = "Gừng", ["gramsEdiblePerServing"] = 0, ["note"] = null }), "/missingIngredients/0/gramsEdiblePerServing"),
        };

        foreach (var (name, change, location) in cases)
        {
            Schemas.Validate("draft-report", Broken(change).ToJsonString()).ShouldContain(i => i.Location == location, $"{name}: expected an issue at '{location}'");
        }
    }

    // ---- the schema and the records agree ------------------------------------------------------------------------------------------

    private static JsonNode SchemaAt(string pointer)
    {
        var node = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "draft-report.schema.json")))!;
        foreach (var segment in pointer.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            node = node[segment]!;
        }

        return node;
    }

    private static string[] Sorted(IEnumerable<string> names) => [.. names.Order(StringComparer.Ordinal)];

    private static string[] FieldsOf(Type record) =>
        Sorted(record.GetConstructors().Single().GetParameters().Select(p => JsonNamingPolicy.CamelCase.ConvertName(p.Name!)));

    [Theory]
    [InlineData("", typeof(DraftReport))]
    [InlineData("/properties/problems/items", typeof(DraftProblem))]
    [InlineData("/properties/missingIngredients/items", typeof(MissingIngredient))]
    [InlineData("/properties/usage", typeof(TokenUsage))]
    public void A_schema_object_lists_exactly_the_fields_of_its_record(string pointer, Type record)
    {
        var node = SchemaAt(pointer);

        Sorted(node["properties"]!.AsObject().Select(p => p.Key)).ShouldBe(FieldsOf(record));
        Sorted(node["required"]!.AsArray().Select(n => n!.GetValue<string>())).ShouldBe(FieldsOf(record));
        node["additionalProperties"]!.GetValue<bool>().ShouldBeFalse();
    }

    [Fact]
    public void The_validation_values_in_the_schema_are_the_ones_the_drafter_writes()
    {
        var allowed = SchemaAt("/properties/validation/enum").AsArray().Select(n => n!.GetValue<string>());

        Sorted(allowed).ShouldBe(Sorted([DraftReport.Valid, DraftReport.Warnings, DraftReport.Invalid, DraftReport.Failed]));
    }

    [Fact]
    public void The_report_and_the_draft_live_side_by_side_under_the_drafts_folder()
    {
        DraftFiles.DraftPath("data", "ca-kho").ShouldBe(Path.Combine("data", "dishes", "_drafts", "ca-kho.json"));
        DraftFiles.ReportPath("data", "ca-kho").ShouldBe(Path.Combine("data", "dishes", "_drafts", "ca-kho.report.json"));
        DraftFiles.FinalPath("data", "ca-kho").ShouldBe(Path.Combine("data", "dishes", "ca-kho.json"));
    }
}
