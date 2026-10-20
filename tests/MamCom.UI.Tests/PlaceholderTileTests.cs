using MamCom.Core.Models;
using MamCom.UI.Components;
using MamCom.UI.Placeholders;

namespace MamCom.UI.Tests;

public class PlaceholderTileTests : BunitContext
{
    private IRenderedComponent<PlaceholderTile> Tile(Placeholder placeholder, bool compact = false) =>
        Render<PlaceholderTile>(p => p.Add(x => x.Placeholder, placeholder).Add(x => x.Compact, compact));

    [Fact]
    public void An_ingredient_tile_is_square_in_the_colour_of_its_aisle_and_says_it_is_not_a_photo()
    {
        var cut = Tile(PlaceholderCatalog.ForIngredient("thit-heo", Aisle.MeatAndFish));

        var tile = cut.Find("div.tile");
        tile.ClassList.ShouldBe(["tile", "tile--ingredient", "tile--aisle-meat"], ignoreOrder: true);
        tile.GetAttribute("role").ShouldBe("img");
        tile.GetAttribute("aria-label").ShouldBe("Chưa có ảnh: thịt heo");
    }

    [Fact]
    public void A_dish_tile_is_four_by_three_in_the_colour_of_its_part_of_the_meal()
    {
        var cut = Tile(PlaceholderCatalog.ForDish(DishSlot.Soup, "ca"));

        cut.Find("div.tile").ClassList.ShouldBe(["tile", "tile--dish", "tile--slot-soup"], ignoreOrder: true);
        cut.Find("div.tile").GetAttribute("aria-label").ShouldBe("Chưa có ảnh: canh · cá");
    }

    [Fact]
    public void The_tile_draws_the_icon_of_its_type_from_the_sprite_and_names_the_type_in_words()
    {
        var cut = Tile(PlaceholderCatalog.ForIngredient("hai-san", Aisle.MeatAndFish));

        cut.Find("svg.icon.tile__icon use").GetAttribute("href").ShouldBe("_content/MamCom.UI/icons/icons.svg#shrimp");
        cut.Find(".tile__label").TextContent.ShouldBe("Hải sản");
    }

    [Fact]
    public void The_words_on_the_tile_are_not_read_twice_because_the_tile_already_has_its_label()
    {
        var cut = Tile(PlaceholderCatalog.ForIngredient("ca", Aisle.MeatAndFish));

        cut.Find(".tile__label").GetAttribute("aria-hidden").ShouldBe("true");
        cut.Find("svg").GetAttribute("aria-hidden").ShouldBe("true");
    }

    [Fact]
    public void A_compact_tile_is_a_thumbnail_and_a_normal_one_is_not()
    {
        Tile(PlaceholderCatalog.ForIngredient("ca", Aisle.MeatAndFish), compact: true).Find("div.tile").ClassList.ShouldContain("tile--compact");
        Tile(PlaceholderCatalog.ForIngredient("ca", Aisle.MeatAndFish)).Find("div.tile").ClassList.ShouldNotContain("tile--compact");
    }

    [Fact]
    public void A_thumbnail_keeps_the_name_for_the_screen_reader_even_though_no_words_are_shown()
    {
        var cut = Tile(PlaceholderCatalog.ForIngredient("nam", Aisle.Produce), compact: true);

        cut.Find("div.tile").GetAttribute("aria-label").ShouldBe("Chưa có ảnh: nấm");
        cut.FindAll(".tile__label").Count.ShouldBe(1);   // present, and hidden by tile--compact in base.css
    }

    [Fact]
    public void Two_different_kinds_never_render_the_same_tile()
    {
        var kinds = PlaceholderCatalog.KnownCategories.Select(c => PlaceholderCatalog.ForIngredient(c, Aisle.Produce))
            .Concat(PlaceholderCatalog.KnownProteinSources.Select(p => PlaceholderCatalog.ForDish(DishSlot.Main, p)));

        var html = kinds.Select(k => Tile(k).Markup).ToArray();

        html.Distinct().Count().ShouldBe(html.Length);
    }
}

public class IconTests : BunitContext
{
    [Fact]
    public void An_icon_is_a_use_of_the_sprite_and_is_hidden_from_a_screen_reader_when_it_is_decoration()
    {
        var cut = Render<Icon>(p => p.Add(x => x.Name, "dice"));

        var svg = cut.Find("svg");
        svg.ClassList.ShouldBe(["icon"]);
        svg.GetAttribute("aria-hidden").ShouldBe("true");
        svg.HasAttribute("role").ShouldBeFalse();
        svg.HasAttribute("aria-label").ShouldBeFalse();
        svg.GetAttribute("focusable").ShouldBe("false");
        cut.Find("use").GetAttribute("href").ShouldBe("_content/MamCom.UI/icons/icons.svg#dice");
    }

    [Fact]
    public void An_icon_with_a_label_is_an_image_for_a_screen_reader_and_is_not_hidden()
    {
        var cut = Render<Icon>(p => p.Add(x => x.Name, "lock").Add(x => x.Label, "Khoá món"));

        var svg = cut.Find("svg");
        svg.GetAttribute("role").ShouldBe("img");
        svg.GetAttribute("aria-label").ShouldBe("Khoá món");
        svg.HasAttribute("aria-hidden").ShouldBeFalse();
    }

    [Fact]
    public void More_classes_come_after_icon()
    {
        Render<Icon>(p => p.Add(x => x.Name, "x").Add(x => x.Class, "icon--large")).Find("svg").ClassList.ShouldBe(["icon", "icon--large"]);
        Render<Icon>(p => p.Add(x => x.Name, "x").Add(x => x.Class, "  ")).Find("svg").ClassList.ShouldBe(["icon"]);
    }
}
