using MamCom.DataBuilder.Loading;
using MamCom.DataBuilder.Schemas;
using MamCom.Studio.Review;

namespace MamCom.Studio.Tests;

public class DraftNoteTests
{
    // A report as RecipeDrafter writes it (the first real run, 2026-10-19).
    private const string RealReport = """
        {
          "dishId": "ca-loc-kho-to",
          "model": "claude-opus-5-5",
          "draftedAt": "2026-10-19",
          "validation": "warnings",
          "stopReason": "end_turn",
          "problems": [
            {
              "severity": "warning",
              "code": "missing-ingredient",
              "location": "",
              "message": "Món cần 'Nước dừa tươi' (khoảng 80 g mỗi suất) mà data/ingredients chưa có."
            }
          ],
          "missingIngredients": [
            { "name": "Nước dừa tươi", "gramsEdiblePerServing": 80, "note": "có thể thay bằng nước lọc" }
          ],
          "usage": { "inputTokens": 88, "outputTokens": 1670, "cacheCreationInputTokens": 4515, "cacheReadInputTokens": 0 }
        }
        """;

    [Fact]
    public void A_report_from_RecipeDrafter_is_read()
    {
        var note = DraftNote.TryParse(RealReport)!;

        note.Validation.ShouldBe("warnings");
        note.Model.ShouldBe("claude-opus-5-5");
        note.DraftedAt.ShouldBe(new DateOnly(2026, 10, 19));
        note.Problems.Single().ShouldBe(new DraftNoteProblem("warning", "missing-ingredient", "", "Món cần 'Nước dừa tươi' (khoảng 80 g mỗi suất) mà data/ingredients chưa có."));
        note.MissingIngredients.Single().ShouldBe(new DraftNoteMissingIngredient("Nước dừa tươi", 80, "có thể thay bằng nước lọc"));
    }

    [Fact]
    public void The_sample_is_a_report_the_schema_accepts_so_the_two_stay_in_step()
    {
        var schemas = SchemaSet.Load(new DataFolder(Path.Combine(AppContext.BaseDirectory, "data")).SchemaFolder);

        schemas.Validate("draft-report", RealReport).ShouldBeEmpty();
    }

    [Fact]
    public void A_failed_report_is_read_too()
    {
        var note = DraftNote.TryParse("""{ "dishId": "x", "model": "m", "draftedAt": "2026-10-19", "validation": "failed", "stopReason": "", "problems": [], "missingIngredients": [] }""")!;

        note.Validation.ShouldBe(DraftNote.Failed);
    }

    [Fact]
    public void Members_the_reviewer_does_not_need_are_ignored()
    {
        var note = DraftNote.TryParse("""{ "validation": "valid", "model": "m", "future": [1, 2, 3] }""")!;

        note.Validation.ShouldBe("valid");
    }

    [Fact]
    public void Names_are_matched_without_regard_to_case()
    {
        DraftNote.TryParse("""{ "Validation": "valid", "Model": "m" }""")!.Validation.ShouldBe("valid");
    }

    [Fact]
    public void A_report_without_lists_has_empty_lists()
    {
        var note = DraftNote.TryParse("""{ "validation": "valid", "model": "m" }""")!;

        note.Problems.ShouldBeEmpty();
        note.MissingIngredients.ShouldBeEmpty();
        note.DraftedAt.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("{ not json")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("""{ "validation": "" }""")]
    [InlineData("""{ "validation": 3 }""")]
    public void A_text_that_is_not_a_report_is_null_rather_than_an_error(string text)
    {
        DraftNote.TryParse(text).ShouldBeNull();
    }
}
