using System.Text;
using MamCom.Core.Models;
using MamCom.Studio.Review;

namespace MamCom.Studio.Tests;

public class DishStoreTests
{
    [Fact]
    public void Ids_are_the_dish_files_in_id_order_and_nothing_else()
    {
        using var data = new StudioData();
        var seeds = Directory.GetFiles(data.PathOf("dishes"), "*.json").Length;
        data.WriteText("dishes/Khong-hop-le.json", "{}");      // not kebab-case
        data.WriteText("dishes/ghi-chu.txt", "note");           // not a json file
        data.WriteText("dishes/dang-ghi.json.tmp", "{");        // a write in progress
        data.WriteText("dishes/_drafts/ca-kho.json", "{}");

        var ids = data.Store.Ids(DishLocation.Data);

        ids.ShouldBe(ids.Order(StringComparer.Ordinal).ToArray());
        ids.ShouldContain("rau-muong-luoc");
        ids.Count.ShouldBe(seeds);
        ids.ShouldNotContain("Khong-hop-le");
        ids.ShouldNotContain("dang-ghi");
        ids.ShouldNotContain("ca-kho");
    }

    [Fact]
    public void Drafts_are_listed_without_their_reports()
    {
        using var data = new StudioData();
        data.WriteText("dishes/_drafts/ca-kho.json", "{}");
        data.WriteText("dishes/_drafts/ca-kho.report.json", "{}");
        data.WriteText("dishes/_drafts/bun-rieu.json", "{}");

        data.Store.Ids(DishLocation.Draft).ShouldBe(["bun-rieu", "ca-kho"]);
    }

    [Fact]
    public void A_missing_folder_has_no_ids()
    {
        using var data = new StudioData();

        data.Store.Ids(DishLocation.Draft).ShouldBeEmpty();
        data.Store.ReportIds().ShouldBeEmpty();
    }

    [Fact]
    public void Report_ids_are_the_ids_with_a_report_in_id_order()
    {
        using var data = new StudioData();
        data.WriteText("dishes/_drafts/ca-kho.report.json", "{}");
        data.WriteText("dishes/_drafts/bun-rieu.report.json", "{}");
        data.WriteText("dishes/_drafts/Sai-ten.report.json", "{}");
        data.WriteText("dishes/_drafts/chi-co-ban-nhap.json", "{}");

        data.Store.ReportIds().ShouldBe(["bun-rieu", "ca-kho"]);
    }

    [Fact]
    public void Exists_is_false_for_an_id_that_cannot_be_a_dish()
    {
        using var data = new StudioData();

        data.Store.Exists(DishLocation.Data, "rau-muong-luoc").ShouldBeTrue();
        data.Store.Exists(DishLocation.Draft, "rau-muong-luoc").ShouldBeFalse();
        data.Store.Exists(DishLocation.Data, "nope").ShouldBeFalse();
        data.Store.Exists(DishLocation.Data, "../config/portion-defaults").ShouldBeFalse();
    }

    // ---- Read ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public void A_dish_is_read_with_its_text_and_a_version()
    {
        using var data = new StudioData();

        var stored = data.Store.Read(DishLocation.Data, "rau-muong-luoc")!;

        stored.Id.ShouldBe("rau-muong-luoc");
        stored.Location.ShouldBe(DishLocation.Data);
        stored.File.ShouldBe("dishes/rau-muong-luoc.json");
        stored.Text.ShouldBe(data.ReadText("dishes/rau-muong-luoc.json"));
        stored.Dish!.Name.ShouldBe("Rau muống luộc");
        stored.Problem.ShouldBeNull();
        stored.Version.Length.ShouldBe(16);
    }

    [Fact]
    public void The_version_is_the_same_for_the_same_text_and_changes_with_any_change()
    {
        using var data = new StudioData();
        var first = data.Store.Read(DishLocation.Data, "rau-muong-luoc")!;

        data.Store.Read(DishLocation.Data, "rau-muong-luoc")!.Version.ShouldBe(first.Version);

        data.WriteText("dishes/rau-muong-luoc.json", first.Text.Replace("Rau muống luộc", "Rau muống luộc "));
        data.Store.Read(DishLocation.Data, "rau-muong-luoc")!.Version.ShouldNotBe(first.Version);
    }

    [Fact]
    public void A_dish_that_is_not_there_or_has_no_valid_id_is_null()
    {
        using var data = new StudioData();

        data.Store.Read(DishLocation.Data, "khong-co").ShouldBeNull();
        data.Store.Read(DishLocation.Draft, "rau-muong-luoc").ShouldBeNull();
        data.Store.Read(DishLocation.Data, "../config/portion-defaults").ShouldBeNull();
        data.Store.Read(DishLocation.Data, "").ShouldBeNull();
    }

    [Fact]
    public void A_file_that_is_not_a_dish_is_still_read_with_the_reason()
    {
        using var data = new StudioData();
        data.WriteText("dishes/hong.json", "{ not json");

        var stored = data.Store.Read(DishLocation.Data, "hong")!;

        stored.Dish.ShouldBeNull();
        stored.Problem.ShouldNotBeNullOrWhiteSpace();
        stored.Text.ShouldBe("{ not json");
    }

    // ---- notes --------------------------------------------------------------------------------------------------------------

    [Fact]
    public void The_note_of_a_draft_is_read()
    {
        using var data = new StudioData();
        data.WriteReport("ca-kho", "warnings", "thiếu nước dừa");

        var note = data.Store.ReadNote("ca-kho")!;

        note.Validation.ShouldBe("warnings");
        note.Problems.Single().Message.ShouldBe("thiếu nước dừa");
    }

    [Fact]
    public void A_missing_or_damaged_note_or_an_invalid_id_is_null()
    {
        using var data = new StudioData();
        data.WriteText("dishes/_drafts/hong.report.json", "{ not json");

        data.Store.ReadNote("khong-co").ShouldBeNull();
        data.Store.ReadNote("hong").ShouldBeNull();
        data.Store.ReadNote("../x").ShouldBeNull();
    }

    // ---- Write --------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Write_replaces_the_file_and_leaves_no_temporary_file()
    {
        using var data = new StudioData();

        data.Store.Write(DishLocation.Data, "rau-muong-luoc", "{ \"x\": 1 }\n");

        data.ReadText("dishes/rau-muong-luoc.json").ShouldBe("{ \"x\": 1 }\n");
        Directory.GetFiles(Path.Combine(data.Root, "dishes"), "*.tmp").ShouldBeEmpty();
    }

    [Fact]
    public void Write_makes_the_drafts_folder_when_it_is_not_there()
    {
        using var data = new StudioData();

        data.Store.Write(DishLocation.Draft, "ca-kho", "{}\n");

        data.Exists("dishes/_drafts/ca-kho.json").ShouldBeTrue();
    }

    [Fact]
    public void Write_does_not_put_a_byte_order_mark_in_the_file()
    {
        using var data = new StudioData();

        data.Store.Write(DishLocation.Data, "rau-muong-luoc", "tiếng Việt\n");

        var bytes = File.ReadAllBytes(data.PathOf("dishes/rau-muong-luoc.json"));
        bytes.Take(3).ShouldNotBe(Encoding.UTF8.GetPreamble());
        Encoding.UTF8.GetString(bytes).ShouldBe("tiếng Việt\n");
    }

    [Fact]
    public void Write_refuses_an_id_that_could_leave_the_folder()
    {
        using var data = new StudioData();

        Should.Throw<ArgumentException>(() => data.Store.Write(DishLocation.Data, "../config/portion-defaults", "{}"));
        Should.Throw<ArgumentNullException>(() => data.Store.Write(DishLocation.Data, "ca-kho", null!));
    }

    [Fact]
    public void Deleting_a_draft_removes_the_draft_and_its_note_and_touches_nothing_else()
    {
        using var data = new StudioData();
        data.MakeDraft("rau-muong-luoc");
        data.WriteReport("rau-muong-luoc", "valid");
        data.WriteReport("khac", "valid");

        data.Store.DeleteDraft("rau-muong-luoc");

        data.Exists("dishes/_drafts/rau-muong-luoc.json").ShouldBeFalse();
        data.Exists("dishes/_drafts/rau-muong-luoc.report.json").ShouldBeFalse();
        data.Exists("dishes/_drafts/khac.report.json").ShouldBeTrue();
        data.Exists("dishes/rau-muong-xao-toi.json").ShouldBeTrue();
    }

    [Fact]
    public void Deleting_a_draft_that_has_no_note_is_fine()
    {
        using var data = new StudioData();
        data.MakeDraft("rau-muong-luoc");

        data.Store.DeleteDraft("rau-muong-luoc");

        data.Exists("dishes/_drafts/rau-muong-luoc.json").ShouldBeFalse();
    }

    [Fact]
    public void A_parsed_dish_has_the_status_the_file_has()
    {
        using var data = new StudioData();

        data.Store.Read(DishLocation.Data, "rau-muong-luoc")!.Dish!.Status.ShouldBe(DishStatus.Draft);
    }
}
