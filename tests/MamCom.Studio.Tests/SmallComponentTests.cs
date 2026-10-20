using MamCom.Core.Models;
using MamCom.DataBuilder.Validation;
using MamCom.Studio.Components;
using MamCom.Studio.Components.Layout;
using MamCom.Studio.Components.Pages;
using MamCom.Studio.Review;

namespace MamCom.Studio.Tests;

public class StatusBadgeTests : BunitContext
{
    [Theory]
    [InlineData(DishStatus.Draft, "badge--warning", "Nháp")]
    [InlineData(DishStatus.Validated, "badge--info", "Đã qua máy kiểm")]
    [InlineData(DishStatus.Reviewed, "badge--ok", "Đã duyệt")]
    [InlineData(DishStatus.CookTested, "badge--ok", "Đã nấu thử")]
    public void Shows_the_word_for_the_status_in_its_own_colour(DishStatus status, string tone, string word)
    {
        var cut = Render<StatusBadge>(p => p.Add(x => x.Status, status));

        cut.Find(".badge").ClassList.ShouldContain(tone);
        cut.Find(".badge").TextContent.ShouldBe(word);
        cut.Find(".badge").GetAttribute("data-status").ShouldBe(status.ToString().ToLowerInvariant());
    }
}

public class FindingsPanelTests : BunitContext
{
    private static Diagnostic Warning(string message) => new(Severity.Warning, DiagnosticCodes.LineNotInSteps, "dishes/x.json", "/ingredients/0", message);

    private static Diagnostic Error(string message) => new(Severity.Error, DiagnosticCodes.UnknownIngredient, "dishes/x.json", string.Empty, message);

    [Fact]
    public void Says_so_when_there_is_nothing_to_report()
    {
        var cut = Render<FindingsPanel>(p => p.Add(x => x.Findings, []));

        cut.Find(".notice--ok").TextContent.ShouldBe("Máy kiểm không thấy gì để báo.");
        cut.FindAll(".finding").ShouldBeEmpty();
    }

    [Fact]
    public void Lists_errors_before_warnings_each_with_its_word_message_code_and_place()
    {
        var cut = Render<FindingsPanel>(p => p.Add(x => x.Findings, [Warning("một cảnh báo"), Error("một lỗi")]));

        var rows = cut.FindAll(".finding");
        rows.Select(r => r.GetAttribute("data-severity")).ShouldBe(["error", "warning"]);
        rows[0].QuerySelector(".badge")!.TextContent.ShouldBe("Lỗi");
        rows[0].QuerySelector(".badge")!.ClassList.ShouldContain("badge--danger");
        rows[0].QuerySelector(".finding__text")!.TextContent.ShouldBe("một lỗi");
        rows[1].QuerySelector(".badge")!.TextContent.ShouldBe("Cảnh báo");
        rows[1].QuerySelector(".badge")!.ClassList.ShouldContain("badge--warning");
        rows[1].QuerySelectorAll("code").Select(c => c.TextContent).ShouldBe([DiagnosticCodes.LineNotInSteps, "/ingredients/0"]);
        rows[0].QuerySelectorAll("code").Select(c => c.TextContent).ShouldBe([DiagnosticCodes.UnknownIngredient]);
    }

    [Fact]
    public void A_place_of_one_character_is_still_shown_and_an_empty_place_is_not()
    {
        var root = new Diagnostic(Severity.Error, DiagnosticCodes.Schema, "dishes/x.json", "/", "ở gốc");
        var whole = new Diagnostic(Severity.Error, DiagnosticCodes.Schema, "dishes/x.json", string.Empty, "cả file");

        var cut = Render<FindingsPanel>(p => p.Add(x => x.Findings, [root, whole]));

        cut.FindAll(".finding")[0].QuerySelectorAll("code").Select(c => c.TextContent).ShouldBe([DiagnosticCodes.Schema, "/"]);
        cut.FindAll(".finding")[1].QuerySelectorAll("code").Select(c => c.TextContent).ShouldBe([DiagnosticCodes.Schema]);
    }

    [Fact]
    public void Warns_that_errors_stop_a_save_only_when_there_is_an_error()
    {
        Render<FindingsPanel>(p => p.Add(x => x.Findings, [Error("lỗi")])).FindAll(".notice--danger").Count.ShouldBe(1);
        Render<FindingsPanel>(p => p.Add(x => x.Findings, [Warning("cảnh báo")])).FindAll(".notice--danger").ShouldBeEmpty();
    }
}

public class DraftNotePanelTests : BunitContext
{
    private static DraftNote Note(string validation, DateOnly? date = null, params DraftNoteMissingIngredient[] missing) =>
        new(validation, "claude-opus-5-5", date, [], missing);

    [Theory]
    [InlineData("valid", "badge--ok", "Hợp lệ")]
    [InlineData("warnings", "badge--warning", "Có cảnh báo")]
    [InlineData("invalid", "badge--danger", "Vi phạm luật")]
    [InlineData("failed", "badge--danger", "Soạn lỗi")]
    public void Shows_how_the_draft_did_in_its_own_colour(string validation, string tone, string word)
    {
        var cut = Render<DraftNotePanel>(p => p.Add(x => x.Note, Note(validation)));

        cut.Find(".badge").ClassList.ShouldContain(tone);
        cut.Find(".badge").TextContent.ShouldBe(word);
    }

    [Fact]
    public void Shows_the_model_and_the_date_when_it_is_known()
    {
        var cut = Render<DraftNotePanel>(p => p.Add(x => x.Note, Note("valid", new DateOnly(2026, 10, 19))));

        cut.Find("p").TextContent.ShouldContain("Soạn bằng claude-opus-5-5, ngày 19/10/2026");
    }

    [Fact]
    public void Leaves_the_date_out_when_it_is_not_known()
    {
        var cut = Render<DraftNotePanel>(p => p.Add(x => x.Note, Note("valid")));

        cut.Find("p").TextContent.ShouldContain("Soạn bằng claude-opus-5-5");
        cut.Find("p").TextContent.ShouldNotContain("ngày");
        cut.Find("p").TextContent.ShouldNotContain(",");
    }

    [Fact]
    public void Lists_the_missing_ingredients_with_their_notes_only_when_there_are_some()
    {
        var with = Render<DraftNotePanel>(p => p.Add(x => x.Note, Note("warnings", null,
            new DraftNoteMissingIngredient("Nước dừa tươi", 80, "có thể thay bằng nước lọc"),
            new DraftNoteMissingIngredient("Cải ngọt", 100, null))));

        with.FindAll("li").Select(li => li.TextContent.Trim()).ShouldBe(
            ["Nước dừa tươi, khoảng 80 g mỗi suất — có thể thay bằng nước lọc", "Cải ngọt, khoảng 100 g mỗi suất"]);
        with.Markup.ShouldContain("Công thức cần mà kho nguyên liệu chưa có");

        var without = Render<DraftNotePanel>(p => p.Add(x => x.Note, Note("valid")));
        without.FindAll("li").ShouldBeEmpty();
        without.Markup.ShouldNotContain("Công thức cần mà kho nguyên liệu chưa có");
    }

    [Fact]
    public void A_blank_note_on_a_missing_ingredient_is_not_shown()
    {
        var cut = Render<DraftNotePanel>(p => p.Add(x => x.Note, Note("warnings", null, new DraftNoteMissingIngredient("Cải ngọt", 100, "  "))));

        cut.Find("li").TextContent.Trim().ShouldBe("Cải ngọt, khoảng 100 g mỗi suất");
    }
}

public class LayoutAndErrorPageTests : BunitContext
{
    [Fact]
    public void The_layout_has_the_brand_the_navigation_and_the_page()
    {
        var cut = Render<MainLayout>(p => p.Add(x => x.Body, builder => builder.AddMarkupContent(0, "<p id=\"inner\">nội dung</p>")));

        cut.Find(".app-brand").TextContent.ShouldBe("MâmCơm Studio");
        cut.Find(".site-header__brand").GetAttribute("href").ShouldBe("/");
        cut.FindAll(".site-nav a").Select(a => (a.TextContent, a.GetAttribute("href"))).ShouldBe([("Tổng quan", "/"), ("Món ăn", "/dishes"), ("Nguyên liệu", "/ingredients")]);
        cut.Find("nav").GetAttribute("aria-label").ShouldBe("Điều hướng chính");
        cut.Find("main #inner").TextContent.ShouldBe("nội dung");
    }

    [Fact]
    public void The_error_bar_of_blazor_is_in_vietnamese_and_can_be_closed_with_the_keyboard()
    {
        var cut = Render<MainLayout>();

        var bar = cut.Find("#blazor-error-ui");
        bar.TextContent.ShouldContain("Có lỗi xảy ra.");
        bar.QuerySelector(".reload")!.TextContent.ShouldBe("Tải lại");
        bar.QuerySelector(".dismiss")!.GetAttribute("aria-label").ShouldBe("Đóng");
        bar.QuerySelector(".dismiss")!.GetAttribute("tabindex").ShouldBe("0");
    }

    [Fact]
    public void The_not_found_page_says_so_and_leads_home()
    {
        var cut = Render<NotFound>();

        cut.Find("h1").TextContent.ShouldBe("Không tìm thấy trang");
        cut.Find("a.button").GetAttribute("href").ShouldBe("/");
    }

    [Fact]
    public void The_error_page_says_so_in_vietnamese()
    {
        var cut = Render<Error>();

        cut.Find("h1").TextContent.ShouldBe("Có lỗi");
        cut.Find(".notice--danger").TextContent.ShouldBe("Có lỗi khi xử lý yêu cầu.");
    }

    [Fact]
    public void The_reconnect_dialog_is_in_vietnamese()
    {
        var cut = Render<ReconnectModal>();

        var words = cut.Find("dialog").TextContent;
        words.ShouldContain("Đang kết nối lại với máy chủ");
        words.ShouldContain("Chưa kết nối lại được. Thử lại sau");
        cut.Find("#components-reconnect-button").TextContent.Trim().ShouldBe("Thử lại");
        cut.Find("#components-resume-button").TextContent.Trim().ShouldBe("Tiếp tục");
        cut.Find("#components-seconds-to-next-attempt").ShouldNotBeNull();
        words.ShouldNotContain("Rejoin");
        words.ShouldNotContain("Retry");
        words.ShouldNotContain("Failed");
    }
}

public class KitchenProblemsTests : BunitContext
{
    [Fact]
    public void Says_the_review_cannot_go_on_when_the_kitchen_is_not_usable()
    {
        using var data = new StudioData();
        data.Delete("config/cooking-defaults.json");

        var panel = Render<KitchenProblems>(p => p.Add(x => x.Kitchen, data.Loader.Load())).Find("[data-kitchen-problems]");

        panel.ClassList.ShouldContain("notice--danger");
        panel.TextContent.ShouldContain("Chưa đọc được dữ liệu");
    }

    [Fact]
    public void Only_warns_when_something_is_wrong_but_the_kitchen_is_still_usable()
    {
        using var data = new StudioData();
        data.WriteText("ingredients/hong.json", "{ not json");
        var kitchen = data.Loader.Load();

        var panel = Render<KitchenProblems>(p => p.Add(x => x.Kitchen, kitchen)).Find("[data-kitchen-problems]");

        kitchen.IsUsable.ShouldBeTrue();
        panel.ClassList.ShouldContain("notice--warning");
        panel.ClassList.ShouldNotContain("notice--danger");
        panel.TextContent.ShouldContain("Thư mục dữ liệu có vấn đề");
        panel.TextContent.ShouldContain("ingredients/hong.json");
        panel.TextContent.ShouldNotContain("Chưa đọc được dữ liệu");
    }

    [Fact]
    public void Shows_nothing_for_a_usable_kitchen()
    {
        using var data = new StudioData();

        Render<KitchenProblems>(p => p.Add(x => x.Kitchen, data.Loader.Load())).FindAll("[data-kitchen-problems]").ShouldBeEmpty();
    }

    [Fact]
    public void Lists_each_problem_with_its_file()
    {
        using var data = new StudioData();
        data.Delete("config/cooking-defaults.json");

        var cut = Render<KitchenProblems>(p => p.Add(x => x.Kitchen, data.Loader.Load()));

        cut.Find("[role=alert]").ShouldNotBeNull();
        cut.FindAll("li").Select(li => li.TextContent).ShouldContain(t => t.StartsWith("config/cooking-defaults.json: "));
    }

    [Fact]
    public void A_problem_with_no_file_at_all_is_filed_under_data()
    {
        var problem = new Diagnostic(Severity.Error, DiagnosticCodes.DataFolder, string.Empty, string.Empty, "thiếu thư mục");
        var data = new MamCom.DataBuilder.Loading.LoadedData("x", null, null, [], [], new HashSet<string>(), [problem]);

        var cut = Render<KitchenProblems>(p => p.Add(x => x.Kitchen, new Kitchen(data, null)));

        cut.Find("li").TextContent.ShouldBe("data: thiếu thư mục");
    }

    [Fact]
    public void A_problem_that_belongs_to_no_file_is_filed_under_data()
    {
        using var data = new StudioData();
        Directory.Delete(data.PathOf("dishes"), recursive: true);   // the data folder itself is incomplete

        var cut = Render<KitchenProblems>(p => p.Add(x => x.Kitchen, data.Loader.Load()));

        cut.FindAll("li").Select(li => li.TextContent).ShouldContain(t => t.StartsWith("dishes: "));
    }
}
