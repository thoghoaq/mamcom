using MamCom.Studio.Components.Pages;
using MamCom.Studio.Review;

namespace MamCom.Studio.Tests;

public class HomeTests : PageTestBase
{
    [Fact]
    public void Counts_the_dishes_in_every_status_with_the_drafts_among_the_drafts()
    {
        Data.MakeDraft("canh-bau-tom");
        Data.EditJson("dishes/rau-muong-xao-toi.json", d => d["status"] = "validated");
        Data.Reviewer.Approve(DishLocation.Data, Id, "Mẹ Lan", Data.Store.Read(DishLocation.Data, Id)!.Version);

        var cut = Render<Home>();

        cut.Find("[data-count-status=draft] strong").TextContent.ShouldBe("18");
        cut.Find("[data-count-status=validated] strong").TextContent.ShouldBe("1");
        cut.Find("[data-count-status=reviewed] strong").TextContent.ShouldBe("1");
        cut.Find("[data-count-status=cooktested] strong").TextContent.ShouldBe("0");
    }

    [Fact]
    public void Names_every_status_in_words()
    {
        var cut = Render<Home>();

        cut.FindAll(".counts .badge").Select(b => b.TextContent).ShouldBe(["Nháp", "Đã qua máy kiểm", "Đã duyệt", "Đã nấu thử"]);
    }

    [Fact]
    public void Counts_the_drafts_that_wait_for_a_person_and_not_the_ones_already_done()
    {
        Data.MakeDraft("canh-bau-tom");
        Data.MakeDraft("rau-muong-xao-toi");
        Data.EditJson("dishes/_drafts/rau-muong-xao-toi.json", d => d["status"] = "validated");

        Render<Home>().Find("[data-waiting-drafts] strong").TextContent.ShouldBe("2");
    }

    [Fact]
    public void A_draft_that_is_already_reviewed_does_not_wait()
    {
        Data.MakeDraft("canh-bau-tom");
        Data.EditJson("dishes/_drafts/canh-bau-tom.json", d =>
        {
            d["status"] = "reviewed";
            d["provenance"]!["reviewedBy"] = "test-only";
            d["provenance"]!["reviewedAt"] = "2026-10-19";
        });

        Render<Home>().Find("[data-waiting-drafts] strong").TextContent.ShouldBe("0");
    }

    [Fact]
    public void Says_which_drafts_failed_and_have_no_draft_file()
    {
        Data.WriteReport("bun-rieu", "failed");
        Data.WriteReport("ca-kho", "failed");

        Render<Home>().Find("[data-failed-drafts]").TextContent.Trim().ShouldContain("bun-rieu, ca-kho");
    }

    [Fact]
    public void One_failed_draft_is_mentioned_too()
    {
        Data.WriteReport("bun-rieu", "failed");

        Render<Home>().Find("[data-failed-drafts]").TextContent.ShouldContain("bun-rieu");
    }

    [Fact]
    public void Says_nothing_about_failures_when_there_are_none()
    {
        Render<Home>().FindAll("[data-failed-drafts]").ShouldBeEmpty();
    }

    [Fact]
    public void Links_to_the_list_and_shows_the_data_folder()
    {
        var cut = Render<Home>();

        cut.Find("a.button--primary").GetAttribute("href").ShouldBe("/dishes");
        cut.Markup.ShouldContain(Data.Root);
    }

    [Fact]
    public void Explains_what_is_wrong_when_the_data_cannot_be_read()
    {
        Data.Delete("config/nutrition-defaults.json");

        var cut = Render<Home>();

        cut.Find("[data-kitchen-problems]").TextContent.ShouldContain("config/nutrition-defaults.json");
    }

    [Fact]
    public void Shows_no_problem_panel_when_the_data_is_fine()
    {
        Render<Home>().FindAll("[data-kitchen-problems]").ShouldBeEmpty();
    }
}

public class DishesTests : PageTestBase
{
    [Fact]
    public void Lists_every_dish_with_its_name_slot_and_status_and_a_link_to_it()
    {
        var cut = Render<Dishes>();

        cut.FindAll(".dish-row").Count.ShouldBe(20);
        var row = cut.Find("[data-dish=rau-muong-luoc]");
        row.QuerySelector(".dish-row__name")!.TextContent.ShouldBe("Rau muống luộc");
        row.QuerySelector(".dish-row__slot")!.TextContent.ShouldBe("Rau");
        row.QuerySelector(".badge")!.TextContent.ShouldBe("Nháp");
        row.QuerySelector("a")!.GetAttribute("href").ShouldBe("/dishes/rau-muong-luoc");
    }

    [Fact]
    public void A_draft_links_to_its_own_page_and_says_it_is_a_draft_with_what_the_drafter_said()
    {
        Data.MakeDraft("canh-bau-tom");
        Data.WriteReport("canh-bau-tom", "warnings");

        var row = Render<Dishes>().Find("[data-dish=canh-bau-tom]");

        row.GetAttribute("data-location").ShouldBe("draft");
        row.QuerySelector("a")!.GetAttribute("href").ShouldBe("/drafts/canh-bau-tom");
        row.QuerySelector("[data-draft]")!.TextContent.ShouldBe("Bản nháp");
        row.QuerySelector("[data-note]")!.TextContent.ShouldBe("Có cảnh báo");
    }

    [Fact]
    public void A_dish_that_is_not_a_draft_has_no_draft_badge_and_no_note()
    {
        var row = Render<Dishes>().Find("[data-dish=rau-muong-luoc]");

        row.QuerySelectorAll("[data-draft]").ShouldBeEmpty();
        row.QuerySelectorAll("[data-note]").ShouldBeEmpty();
    }

    [Fact]
    public void Shows_how_many_errors_and_warnings_the_checks_found()
    {
        Data.EditJson("dishes/rau-muong-xao-toi.json", d => d["ingredients"]![0]!["ingredientId"] = "khong-co");
        Data.EditJson("dishes/rau-muong-luoc.json", d => d["steps"]![1]!["ingredientIds"] = new System.Text.Json.Nodes.JsonArray("rau-muong"));

        var cut = Render<Dishes>();

        cut.Find("[data-dish=rau-muong-xao-toi] [data-errors]").TextContent.ShouldBe("1 lỗi");
        cut.Find("[data-dish=rau-muong-luoc] [data-warnings]").TextContent.ShouldBe("1 cảnh báo");
        cut.FindAll("[data-dish=bap-cai-luoc] [data-errors]").ShouldBeEmpty();
        cut.FindAll("[data-dish=bap-cai-luoc] [data-warnings]").ShouldBeEmpty();
    }

    [Fact]
    public void A_file_that_is_not_a_dish_is_in_the_list_and_says_so()
    {
        Data.WriteText("dishes/hong.json", "{ not json");

        var row = Render<Dishes>().Find("[data-dish=hong]");

        row.QuerySelector(".dish-row__name")!.TextContent.ShouldBe("hong");
        row.QuerySelector("[data-broken]")!.TextContent.ShouldBe("File không đọc được");
    }

    [Fact]
    public void The_filter_chips_show_how_many_dishes_each_has()
    {
        Data.EditJson("dishes/rau-muong-xao-toi.json", d => d["status"] = "validated");

        var chips = Render<Dishes>().FindAll(".chip").Select(c => c.TextContent.Trim()).ToArray();

        chips.ShouldBe(["Tất cả (20)", "Nháp (19)", "Đã qua máy kiểm (1)", "Đã duyệt (0)", "Đã nấu thử (0)"]);
    }

    [Fact]
    public void Choosing_a_status_shows_only_those_dishes_and_choosing_all_shows_everything_again()
    {
        Data.EditJson("dishes/rau-muong-xao-toi.json", d => d["status"] = "validated");
        var cut = Render<Dishes>();

        cut.Find("[data-filter=validated]").Click();

        cut.FindAll(".dish-row").Select(r => r.GetAttribute("data-dish")).ShouldBe(["rau-muong-xao-toi"]);
        cut.Find("[data-filter=validated]").GetAttribute("aria-pressed").ShouldBe("true");
        cut.Find("[data-filter=all]").GetAttribute("aria-pressed").ShouldBe("false");

        cut.Find("[data-filter=all]").Click();

        cut.FindAll(".dish-row").Count.ShouldBe(20);
        cut.Find("[data-filter=all]").GetAttribute("aria-pressed").ShouldBe("true");
    }

    [Fact]
    public void A_status_nobody_has_says_so()
    {
        var cut = Render<Dishes>();

        cut.Find("[data-filter=reviewed]").Click();

        cut.FindAll(".dish-row").ShouldBeEmpty();
        cut.Find("[data-empty]").TextContent.ShouldBe("Không có món nào ở trạng thái này.");
    }

    [Fact]
    public void A_file_that_is_not_a_dish_is_in_no_status_filter_but_in_all()
    {
        Data.WriteText("dishes/hong.json", "{ not json");
        var cut = Render<Dishes>();

        cut.FindAll("[data-dish=hong]").Count.ShouldBe(1);
        cut.Find("[data-filter=draft]").Click();
        cut.FindAll("[data-dish=hong]").ShouldBeEmpty();
    }

    [Fact]
    public void Explains_what_is_wrong_when_the_data_cannot_be_read()
    {
        Data.Delete("schemas/dish.schema.json");

        Render<Dishes>().Find("[data-kitchen-problems]").ShouldNotBeNull();
    }

    [Fact]
    public void A_reviewed_dish_shows_its_status()
    {
        Data.Reviewer.Approve(DishLocation.Data, Id, "Mẹ Lan", Data.Store.Read(DishLocation.Data, Id)!.Version);

        var cut = Render<Dishes>();

        cut.Find($"[data-dish={Id}] .badge").TextContent.ShouldBe("Đã duyệt");
        cut.Find("[data-filter=reviewed]").Click();
        cut.FindAll(".dish-row").Single().GetAttribute("data-dish").ShouldBe(Id);
    }

    [Fact]
    public void Slots_are_named_in_words()
    {
        var cut = Render<Dishes>();

        cut.Find("[data-dish=canh-bau-tom] .dish-row__slot").TextContent.ShouldBe("Canh");
        cut.Find("[data-dish=bo-xao-hanh-tay] .dish-row__slot").TextContent.ShouldBe("Món mặn");
    }
}
