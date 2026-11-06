using MamCom.Core.Models;
using MamCom.UI.Components;
using MamCom.UI.Formatting;
using Microsoft.AspNetCore.Components;

namespace MamCom.UI.Tests;

public class TrayPlateTests : BunitContext
{
    private static readonly TrayPlate.Plate[] ThreeDishes =
    [
        new(DishSlot.Soup, "Canh chua", false),
        new(DishSlot.Main, "Thịt kho", true),
        new(DishSlot.Vegetable, "Rau muống", false),
    ];

    private readonly List<int> _toggled = [];

    private IRenderedComponent<TrayPlate> Show(IReadOnlyList<TrayPlate.Plate>? dishes = null, bool rolling = false, bool disabled = false, IReadOnlyList<IReadOnlyList<string>>? reels = null) =>
        Render<TrayPlate>(p => p
            .Add(t => t.Dishes, dishes ?? ThreeDishes)
            .Add(t => t.Rolling, rolling)
            .Add(t => t.Disabled, disabled)
            .Add(t => t.Reels, reels ?? [])
            .Add(t => t.OnToggle, EventCallback.Factory.Create<int>(this, _toggled.Add)));

    [Fact]
    public void The_tray_has_the_rice_and_a_plate_per_dish_in_its_colour_with_the_kept_one_locked()
    {
        var cut = Show();

        cut.Find(".mam").GetAttribute("role").ShouldBe("group");
        cut.Find(".mam__rice").TextContent.ShouldBe("Cơm");
        cut.Find(".mam__rice").GetAttribute("aria-hidden").ShouldBe("true");
        cut.FindAll(".mam__plate").Select(p => p.ClassName).ShouldBe([
            "mam__plate mam__plate--1 mam__plate--soup",
            "mam__plate mam__plate--2 mam__plate--main mam__plate--locked",
            "mam__plate mam__plate--3 mam__plate--veg"]);
        cut.FindAll(".mam__dish use").Select(u => u.GetAttribute("href")!.Split('#')[1]).ShouldBe(["soup", "meat", "leaf"]);
        cut.FindAll(".mam__lock").Count.ShouldBe(1);
        cut.Find(".mam").ClassList.ShouldNotContain("mam--rolling");
    }

    [Fact]
    public void Each_plate_is_a_button_that_says_whether_its_dish_is_kept_and_a_tap_asks_to_keep_or_let_it_go()
    {
        var cut = Show();
        var plates = cut.FindAll(".mam__plate");

        plates.Select(p => (p.GetAttribute("aria-pressed"), p.GetAttribute("aria-label"))).ShouldBe([
            ("false", "Giữ Canh chua"), ("true", "Bỏ giữ Thịt kho"), ("false", "Giữ Rau muống")]);
        plates.ShouldAllBe(p => p.GetAttribute("type") == "button");

        cut.FindAll(".mam__plate")[2].Click();
        cut.FindAll(".mam__plate")[1].Click();

        _toggled.ShouldBe([2, 1]);
    }

    [Fact]
    public void While_nothing_can_be_kept_the_plates_are_turned_off()
    {
        Show(disabled: true).FindAll(".mam__plate").ShouldAllBe(p => p.HasAttribute("disabled"));
        Show().FindAll(".mam__plate").ShouldAllBe(p => !p.HasAttribute("disabled"));
    }

    [Fact]
    public void While_the_dice_roll_the_plates_not_kept_run_through_names_and_the_kept_one_stays()
    {
        var cut = Show(rolling: true, reels: [["Canh A", "Canh B"], ["Kho A"], ["Rau A", "Rau B"]]);

        cut.Find(".mam").ClassList.ShouldContain("mam--rolling");
        var plates = cut.FindAll(".mam__plate");
        plates[0].QuerySelectorAll(".mam__reel-item").Select(i => i.TextContent).ShouldBe(["Canh A", "Canh B", "Canh A", "Canh B"]);   // twice, so it loops
        plates[1].QuerySelector(".mam__reel").ShouldBeNull();
        plates[2].QuerySelector(".mam__reel").ShouldNotBeNull();
        cut.FindAll(".mam__dish--landed").ShouldBeEmpty();
    }

    [Fact]
    public void When_the_dice_stop_the_dishes_land_one_after_the_other()
    {
        var cut = Show();

        cut.FindAll(".mam__reel").ShouldBeEmpty();
        cut.FindAll(".mam__dish--landed").Count.ShouldBe(3);
        cut.FindAll(".mam__plate").Select(p => p.GetAttribute("style")).ShouldBe(["--i: 0", "--i: 1", "--i: 2"]);
    }

    [Fact]
    public void A_meal_of_one_dish_has_no_rice_and_its_plate_in_the_middle()
    {
        var cut = Show([new TrayPlate.Plate(DishSlot.Bowl, "Phở bò", false)]);

        cut.FindAll(".mam__rice").ShouldBeEmpty();
        cut.Find(".mam").ClassList.ShouldContain("mam--single");
        cut.Find(".mam__plate use").GetAttribute("href")!.ShouldEndWith("#bowl-chopsticks");
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
