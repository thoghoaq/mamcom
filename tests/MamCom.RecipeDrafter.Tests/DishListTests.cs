using MamCom.Core.Models;
using MamCom.RecipeDrafter.Plan;

namespace MamCom.RecipeDrafter.Tests;

public class DishListTests
{
    [Theory]
    [InlineData("Cá lóc kho tộ", "ca-loc-kho-to")]
    [InlineData("Đậu phụ sốt cà chua", "dau-phu-sot-ca-chua")]
    [InlineData("Canh cải ngọt nấu thịt bằm", "canh-cai-ngot-nau-thit-bam")]
    [InlineData("Gà xào sả, ớt!", "ga-xao-sa-ot")]
    [InlineData("  Rau   muống  luộc  ", "rau-muong-luoc")]
    [InlineData("ĐẬU HŨ CHIÊN", "dau-hu-chien")]
    [InlineData("Bún 2 sợi (nhỏ)", "bun-2-soi-nho")]
    [InlineData("!!!", "")]
    public void The_id_is_the_name_without_diacritics_in_lower_case_joined_by_hyphens(string name, string expected)
    {
        DishList.Slugify(name).ShouldBe(expected);
    }

    [Fact]
    public void Rows_become_requests_with_slot_regions_and_a_derived_id()
    {
        var dishes = DishList.Parse("name,slot,regions\nCá lóc kho tộ,man,south\nĐậu phụ sốt cà chua,man,north;south\nCanh cải ngọt,canh,\nRau muống luộc,rau,north; central ;south\n");

        dishes.Select(d => d.Id).ShouldBe(["ca-loc-kho-to", "dau-phu-sot-ca-chua", "canh-cai-ngot", "rau-muong-luoc"]);
        dishes.Select(d => d.Name).ShouldBe(["Cá lóc kho tộ", "Đậu phụ sốt cà chua", "Canh cải ngọt", "Rau muống luộc"]);
        dishes.Select(d => d.Slot).ShouldBe([DishSlot.Main, DishSlot.Main, DishSlot.Soup, DishSlot.Vegetable]);
        dishes[0].Regions.ShouldBe([Region.South]);
        dishes[1].Regions.ShouldBe([Region.North, Region.South]);
        dishes[2].Regions.ShouldBeEmpty();
        dishes[3].Regions.ShouldBe([Region.North, Region.Central, Region.South]);
    }

    [Fact]
    public void An_id_column_overrides_the_derived_id_and_a_blank_one_does_not()
    {
        var dishes = DishList.Parse("name,slot,regions,id\nCá lóc kho tộ,man,south,ca-kho-to\nĐậu phụ sốt cà chua,man,,\n");

        dishes.Select(d => d.Id).ShouldBe(["ca-kho-to", "dau-phu-sot-ca-chua"]);
    }

    [Fact]
    public void A_name_may_be_quoted_to_hold_a_comma()
    {
        var dish = DishList.Parse("name,slot,regions\n\"Cá, tôm kho\",man,south\n").Single();

        dish.Name.ShouldBe("Cá, tôm kho");
        dish.Id.ShouldBe("ca-tom-kho");
    }

    [Fact]
    public void Spaces_around_a_name_are_not_part_of_it()
    {
        DishList.Parse("name,slot,regions\n  Cá kho  ,man,south\n").Single().Name.ShouldBe("Cá kho");
    }

    [Fact]
    public void A_quote_in_the_middle_of_a_name_is_just_a_character()
    {
        var dish = DishList.Parse("name,slot,regions\nCá \"to\" kho,man,south\n").Single();

        dish.Name.ShouldBe("Cá \"to\" kho");
        dish.Id.ShouldBe("ca-to-kho");
    }

    [Fact]
    public void Windows_line_ends_a_byte_order_mark_and_blank_lines_are_tolerated()
    {
        var dishes = DishList.Parse((char)0xFEFF + "name,slot,regions\r\n\r\nCá kho,man,south\r\n\r\n");

        dishes.ShouldHaveSingleItem().Id.ShouldBe("ca-kho");
    }

    [Fact]
    public void A_header_only_file_is_an_empty_list()
    {
        DishList.Parse("name,slot,regions\n").ShouldBeEmpty();
    }

    [Theory]
    [InlineData("", "empty")]
    [InlineData("name,slot\nCá,man\n", "line 1")]
    [InlineData("slot,name,regions\nman,Cá,south\n", "line 1")]
    [InlineData("name,slot,regions,note\nCá,man,south,x\n", "line 1")]
    [InlineData("name,slot,regions\nCá,man\n", "line 2")]
    [InlineData("name,slot,regions\nCá,man,south,x\n", "line 2")]
    [InlineData("name,slot,regions\n,man,south\n", "the name must not be empty")]
    [InlineData("name,slot,regions\nCá,main,south\n", "line 2")]
    [InlineData("name,slot,regions\nCá,man,east\n", "line 2")]
    [InlineData("name,slot,regions\nCá,man,south;south\n", "line 2")]
    [InlineData("name,slot,regions\n!!!,man,south\n", "line 2")]
    [InlineData("name,slot,regions\nCom,man,south\n", "line 2")]
    [InlineData("name,slot,regions\nCá,man,south\nCa,man,south\n", "line 3")]
    [InlineData("name,slot,regions\n\"Cá,man,south\n", "line 2")]
    public void A_file_in_the_wrong_shape_is_rejected_naming_the_line(string csv, string expected)
    {
        Should.Throw<FormatException>(() => DishList.Parse(csv)).Message.ShouldContain(expected);
    }

    [Fact]
    public void Two_rows_with_the_same_id_name_both_lines()
    {
        var error = Should.Throw<FormatException>(() => DishList.Parse("name,slot,regions\nCá kho,man,south\nCa kho,man,north\n"));

        error.Message.ShouldContain("'ca-kho'");
        error.Message.ShouldContain("line 2");
    }

    [Fact]
    public void An_id_that_is_not_kebab_case_or_too_long_is_rejected()
    {
        Should.Throw<FormatException>(() => DishList.Parse("name,slot,regions,id\nCá kho,man,south,Ca_Kho\n")).Message.ShouldContain("'Ca_Kho'");
        Should.Throw<FormatException>(() => DishList.Parse($"name,slot,regions,id\nCá kho,man,south,{new string('a', 65)}\n")).Message.ShouldContain("64");
        Should.NotThrow(() => DishList.Parse($"name,slot,regions,id\nCá kho,man,south,{new string('a', 64)}\n"));
    }

    [Fact]
    public void The_shipped_starter_list_has_three_dishes_with_clean_ids()
    {
        using var data = new TempData();

        var dishes = DishList.Parse(File.ReadAllText(Path.Combine(data.Root, "plan", "dish-list.csv")));

        dishes.Select(d => d.Id).ShouldBe(["ca-loc-kho-to", "dau-phu-sot-ca-chua", "canh-cai-ngot-nau-thit-bam"]);
        dishes.Select(d => d.Slot).ShouldBe([DishSlot.Main, DishSlot.Main, DishSlot.Soup]);
    }
}
