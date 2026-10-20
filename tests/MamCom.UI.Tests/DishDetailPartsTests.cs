using MamCom.Core.Details;
using MamCom.Core.Models;
using MamCom.Data.Bundles;
using MamCom.UI.Components;
using MamCom.UI.Formatting;

namespace MamCom.UI.Tests;

public class DishFormatTests
{
    private static IngredientLine Grams(double grams) => new("x", "X", grams, null, null, null);

    private static IngredientLine Pieces(int units, string display, double grams) => new("x", "X", grams, units, display, null);

    // ---- amounts -----------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(198.0, "198 g")]
    [InlineData(159.5, "160 g")]     // from ten up, whole grams; halves go up
    [InlineData(10.0, "10 g")]
    [InlineData(14.2, "14 g")]       // ten and over has no decimal, even well under a hundred
    [InlineData(9.96, "10 g")]      // under ten a decimal, and the decimal can round up to ten
    [InlineData(9.94, "9,9 g")]
    [InlineData(0.94, "0,9 g")]      // a pinch of pepper keeps a decimal
    [InlineData(5.0, "5 g")]         // and drops it when it is a whole number
    [InlineData(1234.0, "1.234 g")]
    public void An_amount_in_grams_is_whole_from_ten_and_has_a_decimal_below(double grams, string expected)
    {
        DishFormat.Amount(Grams(grams)).Plain().ShouldBe(expected);
    }

    [Fact]
    public void An_amount_in_pieces_is_the_number_and_the_unit_with_the_grams_given_apart()
    {
        var eggs = Pieces(3, "quả", 165);

        DishFormat.Amount(eggs).ShouldBe("3 quả");
        DishFormat.AmountInGrams(eggs)!.ShouldBe("≈ 165 g");
        DishFormat.AmountInGrams(Grams(165)).ShouldBeNull();
    }

    [Fact]
    public void One_piece_is_still_counted_in_pieces_and_a_line_with_a_unit_but_no_count_is_in_grams()
    {
        DishFormat.Amount(Pieces(1, "quả", 55)).ShouldBe("1 quả");
        DishFormat.Amount(new IngredientLine("x", "X", 55, null, "quả", null)).ShouldBe("55 g");   // the unit alone says nothing without a count
        DishFormat.Amount(new IngredientLine("x", "X", 55, 1, null, null)).ShouldBe("55 g");       // and a count alone has no word to go with
    }

    [Fact]
    public void A_number_and_its_unit_are_kept_on_one_line()
    {
        DishFormat.Amount(Grams(198)).ShouldBe("198 g");
        DishFormat.Servings(3.79).ShouldBe("cho ≈ 3,8 suất");
        DishFormat.MemberServings(0.595).ShouldBe("0,6 suất");
        DishFormat.Percent(0.342).ShouldBe("≈ 34%");
    }

    [Theory]
    [InlineData(3.7875, "cho ≈ 3,8 suất")]
    [InlineData(2.0, "cho ≈ 2 suất")]
    [InlineData(2.2, "cho ≈ 2,2 suất")]
    [InlineData(0.0, "cho ≈ 0 suất")]
    public void The_servings_have_one_decimal_that_is_dropped_when_it_is_zero(double servings, string expected)
    {
        DishFormat.Servings(servings).Plain().ShouldBe(expected);
    }

    [Theory]
    [InlineData(1.0, "1 suất")]
    [InlineData(0.595, "0,6 suất")]
    [InlineData(1.2, "1,2 suất")]
    public void A_members_servings_are_written_the_same_way(double servings, string expected)
    {
        DishFormat.MemberServings(servings).Plain().ShouldBe(expected);
    }

    [Fact]
    public void A_line_cannot_be_formatted_without_a_line()
    {
        Should.Throw<ArgumentNullException>(() => DishFormat.Amount(null!));
        Should.Throw<ArgumentNullException>(() => DishFormat.AmountInGrams(null!));
        Should.Throw<ArgumentNullException>(() => DishFormat.Recommended(null!));
        Should.Throw<ArgumentNullException>(() => DishFormat.ShareBar(null!));
    }

    // ---- steps -------------------------------------------------------------------------------------------------------------

    [Fact]
    public void The_time_of_a_step_says_what_is_hands_on_and_what_is_waiting()
    {
        DishFormat.StepActive(8).Plain().ShouldBe("Làm 8 phút");
        DishFormat.StepPassive(40).Plain().ShouldBe("Chờ 40 phút");
        DishFormat.StepPassive(90).Plain().ShouldBe("Chờ 1 giờ 30 phút");
    }

    // ---- shares ------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Every_nutrient_has_its_name_and_its_colour_class_and_nothing_else_is_a_nutrient()
    {
        Enum.GetValues<Macro>().Select(DishFormat.MacroName).ShouldBe(["Đạm", "Béo", "Bột đường"]);
        Enum.GetValues<Macro>().Select(DishFormat.MacroTone).ShouldBe(["protein", "fat", "carb"]);
        Should.Throw<ArgumentOutOfRangeException>(() => DishFormat.MacroName((Macro)9));
        Should.Throw<ArgumentOutOfRangeException>(() => DishFormat.MacroTone((Macro)9));
    }

    [Fact]
    public void Every_place_against_the_range_has_its_words_and_nothing_else_is_a_place()
    {
        Enum.GetValues<RangeStatus>().Select(DishFormat.Status).ShouldBe(["thấp hơn", "vừa mức", "cao hơn"]);
        Should.Throw<ArgumentOutOfRangeException>(() => DishFormat.Status((RangeStatus)9));
    }

    [Theory]
    [InlineData(0.0, "≈ 0%")]
    [InlineData(0.3419, "≈ 34%")]
    [InlineData(0.3451, "≈ 35%")]
    [InlineData(1.0, "≈ 100%")]
    public void A_share_is_a_whole_percent(double share, string expected)
    {
        DishFormat.Percent(share).Plain().ShouldBe(expected);
    }

    [Fact]
    public void The_recommended_range_is_in_whole_percent()
    {
        DishFormat.Recommended(new MacroShare(Macro.Protein, 0.3, 0.13, 0.2, RangeStatus.Above)).ShouldBe("khuyến nghị 13–20%");
        DishFormat.Recommended(new MacroShare(Macro.Carb, 0.6, 0.55, 0.65, RangeStatus.Within)).ShouldBe("khuyến nghị 55–65%");
    }

    [Fact]
    public void The_bar_is_read_out_in_the_order_of_the_words_whatever_the_order_of_the_list()
    {
        MacroShare[] shares =
        [
            new(Macro.Carb, 0.5, 0.55, 0.65, RangeStatus.Below),
            new(Macro.Protein, 0.2, 0.13, 0.2, RangeStatus.Within),
            new(Macro.Fat, 0.3, 0.2, 0.25, RangeStatus.Above),
        ];

        DishFormat.ShareBar(shares).Plain().ShouldBe("Đạm ≈ 20%, béo ≈ 30%, bột đường ≈ 50%");
    }
}

public class EnergyShareBarTests : BunitContext
{
    private static readonly MacroShare[] Shares =
    [
        new(Macro.Protein, 0.25, 0.13, 0.2, RangeStatus.Above),
        new(Macro.Fat, 0.75, 0.2, 0.25, RangeStatus.Above),
        new(Macro.Carb, 0, 0.55, 0.65, RangeStatus.Below),
    ];

    [Fact]
    public void A_nutrient_that_gives_no_energy_has_no_segment_but_still_has_its_row()
    {
        var cut = Render<EnergyShareBar>(p => p.Add(b => b.Shares, Shares));

        cut.FindAll(".shares__segment").Select(s => s.GetAttribute("style")).ShouldBe(["flex-grow: 0.25", "flex-grow: 0.75"]);
        cut.FindAll(".shares__row").Count.ShouldBe(3);
        cut.FindAll(".shares__row")[2].QuerySelector(".shares__value")!.TextContent.Words().ShouldBe("≈ 0%");
    }

    [Fact]
    public void The_bar_is_a_picture_with_the_figures_said_in_words_and_the_rows_are_a_list()
    {
        var cut = Render<EnergyShareBar>(p => p.Add(b => b.Shares, Shares));

        cut.Find(".shares__bar").GetAttribute("role").ShouldBe("img");
        cut.Find(".shares__bar").GetAttribute("aria-label")!.Plain().ShouldBe("Đạm ≈ 25%, béo ≈ 75%, bột đường ≈ 0%");
        cut.Find("ul.shares__legend").GetAttribute("aria-label").ShouldBe("Năng lượng đến từ đâu");
        cut.FindAll(".shares__row").ShouldAllBe(r => r.TagName == "LI");
    }

    [Fact]
    public void Each_row_has_the_colour_of_its_segment_and_a_status_in_words_next_to_the_range()
    {
        var cut = Render<EnergyShareBar>(p => p.Add(b => b.Shares, Shares));

        cut.FindAll(".shares__swatch").Select(s => s.ClassList.Last()).ShouldBe(["shares__swatch--protein", "shares__swatch--fat", "shares__swatch--carb"]);
        cut.FindAll(".shares__range").Select(r => r.TextContent.Words()).ShouldBe(["khuyến nghị 13–20% cao hơn", "khuyến nghị 20–25% cao hơn", "khuyến nghị 55–65% thấp hơn"]);
    }
}

public class DishPictureLargeTests : BunitContext
{
    private static Dish Dish(ImageRef image) => TestConfig.SeedBundle.Dishes.First() with { Image = image };

    private IRenderedComponent<DishPicture> Show(ImageRef image, bool large) =>
        Render<DishPicture>(p => p.Add(c => c.Dish, Dish(image)).Add(c => c.Large, large));

    private static readonly ImageRef Both = new("a-200.webp", "a-800.webp", "tự chụp", "CC0", false);

    private static Task Fail(IRenderedComponent<DishPicture> cut) =>
        cut.Find("img").TriggerEventAsync("onerror", new Microsoft.AspNetCore.Components.Web.ErrorEventArgs());

    [Fact]
    public void The_large_picture_is_the_large_file_and_a_small_one_is_the_thumb()
    {
        Show(Both, large: true).Find("img").GetAttribute("src").ShouldBe(ThumbPreloader.ThumbUrl("a-800.webp"));
        Show(Both, large: false).Find("img").GetAttribute("src").ShouldBe(ThumbPreloader.ThumbUrl("a-200.webp"));
    }

    [Fact]
    public void A_dish_with_only_a_thumb_shows_the_thumb_even_in_the_large_picture()
    {
        Show(new ImageRef("a-200.webp", null, "tự chụp", "CC0", false), large: true).Find("img").GetAttribute("src").ShouldBe(ThumbPreloader.ThumbUrl("a-200.webp"));
    }

    [Fact]
    public async Task A_large_file_that_cannot_be_loaded_gives_way_to_the_thumb_and_then_to_the_tile()
    {
        var cut = Show(Both, large: true);

        await Fail(cut);
        cut.Find("img").GetAttribute("src").ShouldBe(ThumbPreloader.ThumbUrl("a-200.webp"));

        await Fail(cut);
        cut.FindAll("img").ShouldBeEmpty();
        cut.Find(".tile").ShouldNotBeNull();
    }

    [Fact]
    public async Task A_thumb_that_cannot_be_loaded_gives_way_to_the_tile_at_once()
    {
        var cut = Show(Both, large: false);

        await Fail(cut);

        cut.FindAll("img").ShouldBeEmpty();
        cut.Find(".tile").ShouldNotBeNull();
    }

    [Fact]
    public async Task A_dish_with_only_a_thumb_gives_way_to_the_tile_at_once_in_the_large_picture_too()
    {
        var cut = Show(new ImageRef("a-200.webp", null, "tự chụp", "CC0", false), large: true);

        await Fail(cut);

        cut.FindAll("img").ShouldBeEmpty();
        cut.Find(".tile").ShouldNotBeNull();
    }

    [Fact]
    public async Task A_dish_with_a_large_file_only_gives_way_to_the_tile_when_it_cannot_be_loaded()
    {
        var cut = Show(new ImageRef(null, "a-800.webp", "tự chụp", "CC0", false), large: true);
        cut.Find("img").GetAttribute("src").ShouldBe(ThumbPreloader.ThumbUrl("a-800.webp"));

        await Fail(cut);

        cut.FindAll("img").ShouldBeEmpty();
        cut.Find(".tile").ShouldNotBeNull();
    }

    [Fact]
    public async Task Another_pair_of_files_gets_its_own_chance_after_both_failed()
    {
        var cut = Show(Both, large: true);
        await Fail(cut);
        await Fail(cut);

        cut.Render(p => p.Add(c => c.Dish, Dish(new ImageRef("b-200.webp", "b-800.webp", "tự chụp", "CC0", false))).Add(c => c.Large, true));

        cut.Find("img").GetAttribute("src").ShouldBe(ThumbPreloader.ThumbUrl("b-800.webp"));
    }

    [Fact]
    public async Task A_failed_large_file_is_tried_again_for_another_large_file_of_the_same_thumb()
    {
        var cut = Show(Both, large: true);
        await Fail(cut);

        cut.Render(p => p.Add(c => c.Dish, Dish(Both with { Full = "a-900.webp" })).Add(c => c.Large, true));

        cut.Find("img").GetAttribute("src").ShouldBe(ThumbPreloader.ThumbUrl("a-900.webp"));
    }

    [Fact]
    public void An_illustration_says_so_in_the_large_picture_too()
    {
        Show(Both with { IsIllustration = true }, large: true).Find(".picture__badge").TextContent.ShouldBe("Ảnh minh hoạ");
    }

    [Fact]
    public void Without_a_file_the_large_picture_is_the_tile_with_its_words_unless_it_is_compact()
    {
        var none = new ImageRef(null, null, null, null, false);

        Show(none, large: true).Find(".tile").ClassList.ShouldNotContain("tile--compact");
        Render<DishPicture>(p => p.Add(c => c.Dish, Dish(none)).Add(c => c.Compact, true)).Find(".tile").ClassList.ShouldContain("tile--compact");
    }
}
