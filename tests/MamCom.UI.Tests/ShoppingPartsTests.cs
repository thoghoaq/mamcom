using MamCom.Core.Models;
using MamCom.Core.Shopping;
using MamCom.Data.Bundles;
using MamCom.UI.Components;
using MamCom.UI.Formatting;
using Microsoft.Extensions.DependencyInjection;

namespace MamCom.UI.Tests;

public class ShoppingFormatTests
{
    private static ShoppingItem Item(double quantity, string unit, double? approxGrams = null) =>
        new("x", "X", Aisle.Produce, quantity, unit, quantity * 100, false, approxGrams);

    [Theory]
    [InlineData(3.0, "lạng", "3 lạng")]
    [InlineData(2.5, "kg", "2,5 kg")]
    [InlineData(0.75, "kg", "0,75 kg")]
    [InlineData(0.333, "kg", "0,33 kg")]       // up to two places, as the text for Zalo writes it
    [InlineData(1.0, "con", "1 con")]
    [InlineData(1200.0, "g", "1200 g")]            // no dot between thousands, as in the text for Zalo
    public void An_amount_is_the_number_in_vietnamese_and_the_unit_of_the_market(double quantity, string unit, string expected)
    {
        ShoppingFormat.Amount(Item(quantity, unit)).Plain().ShouldBe(expected);
    }

    [Fact]
    public void A_number_and_its_unit_are_kept_on_one_line()
    {
        ShoppingFormat.Amount(Item(3, "lạng")).ShouldBe("3 lạng");
        ShoppingFormat.Approx(Item(1, "con", 500))!.ShouldBe("≈ 500 g");
        ShoppingFormat.Meta(4).ShouldBe("Mâm hôm nay, cho 4 người.");
    }

    [Theory]
    [InlineData(500.0, "≈ 500 g")]
    [InlineData(499.6, "≈ 500 g")]
    [InlineData(1250.0, "≈ 1.250 g")]
    public void The_grams_of_what_varies_in_size_are_whole_grams(double grams, string expected)
    {
        ShoppingFormat.Approx(Item(1, "con", grams))!.Plain().ShouldBe(expected);
    }

    [Fact]
    public void What_does_not_vary_in_size_has_no_grams_beside_it()
    {
        ShoppingFormat.Approx(Item(3, "lạng")).ShouldBeNull();
    }

    [Fact]
    public void Every_aisle_has_its_name_and_nothing_else_is_an_aisle()
    {
        Enum.GetValues<Aisle>().Select(ShoppingFormat.AisleName).ShouldBe(["Thịt cá", "Rau củ quả", "Trứng đậu phụ", "Khô và gia vị", "Khác"]);
        Should.Throw<ArgumentOutOfRangeException>(() => ShoppingFormat.AisleName((Aisle)9));
    }

    [Fact]
    public void The_progress_says_how_many_of_how_many()
    {
        ShoppingFormat.Progress(new ShoppingProgress(3, 9)).ShouldBe("Đã mua 3/9");
        ShoppingFormat.Progress(new ShoppingProgress(0, 0)).ShouldBe("Đã mua 0/0");
    }

    [Fact]
    public void An_item_and_a_progress_are_required()
    {
        Should.Throw<ArgumentNullException>(() => ShoppingFormat.Amount(null!));
        Should.Throw<ArgumentNullException>(() => ShoppingFormat.Approx(null!));
        Should.Throw<ArgumentNullException>(() => ShoppingFormat.Progress(null!));
    }
}

public class IngredientPictureTests : BunitContext
{
    private static Ingredient Ingredient(ImageRef image) => TestConfig.SeedBundle.Ingredients.First() with { Image = image };

    private IRenderedComponent<IngredientPicture> Show(ImageRef image) => Render<IngredientPicture>(p => p.Add(c => c.Ingredient, Ingredient(image)));

    private static Task Fail(IRenderedComponent<IngredientPicture> cut) =>
        cut.Find("img").TriggerEventAsync("onerror", new Microsoft.AspNetCore.Components.Web.ErrorEventArgs());

    [Fact]
    public void An_ingredient_without_a_picture_has_the_tile_of_its_kind_with_only_the_drawing_and_named_for_a_screen_reader()
    {
        var cut = Show(new ImageRef(null, null, null, null, false));

        var tile = cut.Find(".tile");
        tile.ClassList.ShouldContain("tile--ingredient");
        tile.ClassList.ShouldContain("tile--compact");
        tile.GetAttribute("aria-label")!.ShouldStartWith("Chưa có ảnh");
        cut.FindAll("img").ShouldBeEmpty();
    }

    [Fact]
    public void An_ingredient_with_a_thumb_shows_it_from_the_images_folder_and_not_the_tile()
    {
        var cut = Show(new ImageRef("rau muong-200.webp", null, "tự chụp", "CC0", false));

        var image = cut.Find("figure.picture--ingredient img.picture__image");
        image.GetAttribute("src").ShouldBe(ThumbPreloader.ThumbUrl("rau muong-200.webp"));
        image.GetAttribute("alt").ShouldBe(string.Empty);   // the name is written next to it
        image.GetAttribute("loading").ShouldBe("lazy");
        image.GetAttribute("draggable").ShouldBe("false");   // a swipe over a row is not the drag of an image
        cut.FindAll(".tile").ShouldBeEmpty();
        cut.FindAll("figcaption").ShouldBeEmpty();
        cut.Find("figure").HasAttribute("title").ShouldBeFalse();
    }

    [Fact]
    public void A_thumb_that_is_an_illustration_says_so_to_a_screen_reader_and_in_a_tooltip()
    {
        var cut = Show(new ImageRef("a-200.webp", null, "AI", "CC0", true));

        cut.Find("figcaption").TextContent.ShouldBe("Ảnh minh hoạ");
        cut.Find("figcaption").ClassList.ShouldContain("visually-hidden");
        cut.Find("figure").GetAttribute("title").ShouldBe("Ảnh minh hoạ");
    }

    [Fact]
    public async Task A_thumb_that_cannot_be_loaded_gives_way_to_the_tile_and_is_never_left_broken()
    {
        var cut = Show(new ImageRef("missing-200.webp", null, "tự chụp", "CC0", false));

        await Fail(cut);

        cut.FindAll("img").ShouldBeEmpty();
        cut.Find(".tile").ShouldNotBeNull();
    }

    [Fact]
    public async Task Another_file_gets_its_own_chance_after_one_failed()
    {
        var cut = Show(new ImageRef("missing-200.webp", null, "tự chụp", "CC0", false));
        await Fail(cut);

        cut.Render(p => p.Add(c => c.Ingredient, Ingredient(new ImageRef("other-200.webp", null, "tự chụp", "CC0", false))));

        cut.Find("img").GetAttribute("src").ShouldBe(ThumbPreloader.ThumbUrl("other-200.webp"));
    }

    [Fact]
    public async Task The_same_file_is_not_tried_again_when_the_page_draws_again()
    {
        var cut = Show(new ImageRef("missing-200.webp", null, "tự chụp", "CC0", false));
        await Fail(cut);

        cut.Render(p => p.Add(c => c.Ingredient, Ingredient(new ImageRef("missing-200.webp", null, "tự chụp", "CC0", false))));

        cut.FindAll("img").ShouldBeEmpty();
    }
}

public class BackLinkTests : BunitContext
{
    [Fact]
    public void It_leads_to_the_start_page_of_the_app_with_a_picture_that_is_only_decoration()
    {
        var cut = Render<BackLink>();

        var link = cut.Find("nav.back a.back__link");
        link.GetAttribute("href").ShouldBe(Services.GetRequiredService<Bunit.TestDoubles.BunitNavigationManager>().BaseUri);
        link.TextContent.ShouldBe("Về mâm hôm nay");
        link.QuerySelector("svg")!.GetAttribute("aria-hidden").ShouldBe("true");
    }
}
