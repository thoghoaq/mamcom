using MamCom.Core.Models;
using MamCom.UI.Components;
using MamCom.UI.Formatting;

namespace MamCom.UI.Tests;

public class TrayPlateTests : BunitContext
{
    [Fact]
    public void The_tray_has_the_rice_and_a_plate_per_dish_in_its_colour_with_the_locked_one_ringed_and_is_hidden_from_a_screen_reader()
    {
        var cut = Render<TrayPlate>(p => p.Add(t => t.Slots, [(DishSlot.Soup, false), (DishSlot.Main, true), (DishSlot.Vegetable, false)]));

        cut.Find(".mam").GetAttribute("aria-hidden").ShouldBe("true");
        cut.Find(".mam__rice").TextContent.ShouldBe("Cơm");
        cut.FindAll(".mam__plate").Select(p => p.ClassName).ShouldBe([
            "mam__plate mam__plate--1 mam__plate--soup",
            "mam__plate mam__plate--2 mam__plate--main mam__plate--locked",
            "mam__plate mam__plate--3 mam__plate--veg"]);
        cut.FindAll(".mam__plate use").Select(u => u.GetAttribute("href")!.Split('#')[1]).ShouldBe(["soup", "meat", "leaf"]);
        cut.Find(".mam").ClassList.ShouldNotContain("mam--rolling");
    }

    [Fact]
    public void While_the_dice_roll_the_tray_turns()
    {
        var cut = Render<TrayPlate>(p => p.Add(t => t.Slots, [(DishSlot.Soup, false)]).Add(t => t.Rolling, true));

        cut.Find(".mam").ClassList.ShouldContain("mam--rolling");
    }

    [Theory]
    [InlineData(2026, 11, 5, "Thứ Năm, 5 tháng 11")]
    [InlineData(2026, 11, 8, "Chủ nhật, 8 tháng 11")]
    [InlineData(2026, 12, 28, "Thứ Hai, 28 tháng 12")]
    public void The_day_under_the_title_is_said_the_vietnamese_way(int year, int month, int day, string expected)
    {
        TrayFormat.Day(new DateOnly(year, month, day)).ShouldBe(expected);
    }
}
