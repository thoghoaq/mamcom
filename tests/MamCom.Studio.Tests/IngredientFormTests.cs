using MamCom.Core.Models;
using MamCom.DataBuilder.Loading;
using MamCom.Studio.Editing;

namespace MamCom.Studio.Tests;

public class IngredientFormTests
{
    private static Ingredient Seed(string id = "gao")
    {
        using var data = new StudioData();
        return IngredientFile.Parse(data.ReadText($"ingredients/{id}.json"));
    }

    public static TheoryData<string> SeedIngredients()
    {
        var ids = new TheoryData<string>();
        using var data = new StudioData();
        foreach (var path in Directory.GetFiles(data.PathOf("ingredients"), "*.json").Order(StringComparer.Ordinal))
        {
            ids.Add(Path.GetFileNameWithoutExtension(path));
        }

        return ids;
    }

    [Theory]
    [MemberData(nameof(SeedIngredients))]
    public void A_seed_ingredient_goes_through_the_form_and_back_unchanged(string id)
    {
        var ingredient = Seed(id);

        // Records compare their collections by reference, so the file text is what tells whether anything changed.
        IngredientFile.Serialize(IngredientForm.From(ingredient).ToIngredient(ingredient)).ShouldBe(IngredientFile.Serialize(ingredient));
    }

    [Fact]
    public void The_form_holds_what_the_ingredient_has()
    {
        var form = IngredientForm.From(Seed());

        form.Names["default"].ShouldBe("Gạo");
        form.Names["north"].ShouldBe(string.Empty);
        form.AliasesText.ShouldBe("gạo tẻ");
        form.Category.ShouldBe("ngu-coc");
        form.Aisle.ShouldBe(Aisle.DryGoodsAndSpices);
        form.ProteinSource.ShouldBe(string.Empty);
        form.WasteRatio.ShouldBe(0);
        form.IsPantry.ShouldBeTrue();
        form.Units["default"].Enabled.ShouldBeTrue();
        form.Units["default"].Display.ShouldBe("kg");
        form.Units["default"].GramsPerUnit.ShouldBe(1000);
        form.Units["default"].Step.ShouldBe(0.5);
        form.Units["default"].ShowApproxGrams.ShouldBeFalse();
        form.Units["north"].Enabled.ShouldBeFalse();
        form.Image.Thumb.ShouldBe(string.Empty);
        form.Image.IsIllustration.ShouldBeFalse();
    }

    [Fact]
    public void An_ingredient_with_a_unit_for_each_region_is_held_key_by_key()
    {
        var ingredient = Seed("ca-loc");

        var form = IngredientForm.From(ingredient);

        form.Units.Where(u => u.Value.Enabled).Select(u => u.Key).ShouldBe([.. ingredient.MarketUnit.Keys.Order(StringComparer.Ordinal).Select(k => k).Intersect(RegionKeyList.Keys)], ignoreOrder: true);
        form.Names.Where(n => n.Value.Length > 0).Select(n => n.Key).ShouldBe([.. ingredient.Names.Keys], ignoreOrder: true);
    }

    [Fact]
    public void The_edited_values_end_up_in_the_ingredient_and_the_id_and_nutrition_come_from_the_original()
    {
        var original = Seed("rau-muong");
        var form = IngredientForm.From(original);
        form.Names["north"] = "Rau muống Bắc";
        form.AliasesText = "muống\nrau muống nước";
        form.Category = "rau-la-moi";
        form.Aisle = Aisle.EggsAndTofu;
        form.ProteinSource = "heo";
        form.WasteRatio = 0.3;
        form.IsPantry = true;
        form.Units["default"].Display = "bó";
        form.Units["default"].GramsPerUnit = 250;
        form.Units["default"].Step = 2;
        form.Units["default"].ShowApproxGrams = true;
        form.Units["south"] = new UnitForm { Enabled = true, Display = "g", GramsPerUnit = 1, Step = 50 };

        var ingredient = form.ToIngredient(original);

        ingredient.Id.ShouldBe("rau-muong");
        ingredient.Names.ShouldBe(new Dictionary<string, string> { ["default"] = "Rau muống", ["north"] = "Rau muống Bắc" });
        ingredient.Aliases.ShouldBe(["muống", "rau muống nước"]);
        ingredient.Category.ShouldBe("rau-la-moi");
        ingredient.Aisle.ShouldBe(Aisle.EggsAndTofu);
        ingredient.ProteinSource.ShouldBe("heo");
        ingredient.WasteRatio.ShouldBe(0.3);
        ingredient.IsPantry.ShouldBeTrue();
        ingredient.MarketUnit["default"].ShouldBe(new MarketUnit("bó", 250, 2, true));
        ingredient.MarketUnit["south"].ShouldBe(new MarketUnit("g", 1, 50));
        ingredient.MarketUnit.Keys.ShouldBe(["default", "south"]);
        ingredient.NutritionPer100gEdible.ShouldBe(original.NutritionPer100gEdible);
        ingredient.NutritionSource.ShouldBe(original.NutritionSource);
    }

    [Fact]
    public void Text_is_trimmed_blank_values_are_left_out_and_aliases_lose_empty_lines()
    {
        var original = Seed();
        var form = IngredientForm.From(original);
        form.Names["default"] = "  Gạo tẻ  ";
        form.Names["north"] = "   ";
        form.AliasesText = "  gạo thường \n\n   \r\ngạo cũ\n";
        form.Category = "  ngu-coc ";
        form.ProteinSource = "   ";
        form.Units["default"].Display = "  kg ";

        var ingredient = form.ToIngredient(original);

        ingredient.Names.ShouldBe(new Dictionary<string, string> { ["default"] = "Gạo tẻ" });
        ingredient.Aliases.ShouldBe(["gạo thường", "gạo cũ"]);
        ingredient.Category.ShouldBe("ngu-coc");
        ingredient.ProteinSource.ShouldBeNull();
        ingredient.MarketUnit["default"].Display.ShouldBe("kg");
    }

    [Fact]
    public void The_keys_keep_the_order_the_file_has_and_new_ones_come_after_in_the_order_default_north_central_south()
    {
        var original = Seed("tom");   // the file has north before default
        original.MarketUnit.Keys.ShouldBe(["north", "default"]);
        var form = IngredientForm.From(original);
        form.Units["south"] = new UnitForm { Enabled = true, Display = "kg", GramsPerUnit = 1000, Step = 0.5 };
        form.Units["central"] = new UnitForm { Enabled = true, Display = "lạng", GramsPerUnit = 100, Step = 1 };
        form.Names["south"] = "Tôm Nam";
        form.Names["north"] = "Tôm Bắc";

        var ingredient = form.ToIngredient(original);

        ingredient.MarketUnit.Keys.ShouldBe(["north", "default", "central", "south"]);
        ingredient.Names.Keys.ShouldBe(["default", "north", "south"]);
    }

    [Fact]
    public void A_new_ingredient_has_its_keys_in_the_order_default_north_central_south()
    {
        var original = Seed() with { Names = new Dictionary<string, string>(), MarketUnit = new Dictionary<string, MarketUnit>() };
        var form = new IngredientForm();
        foreach (var key in RegionKeyList.Keys.Reverse())
        {
            form.Names[key] = key;
            form.Units[key] = new UnitForm { Enabled = true, Display = "g", GramsPerUnit = 1, Step = 1 };
        }

        var ingredient = form.ToIngredient(original);

        ingredient.Names.Keys.ShouldBe(["default", "north", "central", "south"]);
        ingredient.MarketUnit.Keys.ShouldBe(["default", "north", "central", "south"]);
    }

    [Fact]
    public void A_unit_that_is_switched_off_is_not_written_whatever_it_holds()
    {
        var original = Seed();
        var form = IngredientForm.From(original);
        form.Units["north"] = new UnitForm { Enabled = false, Display = "lạng", GramsPerUnit = 100, Step = 1 };

        var ingredient = form.ToIngredient(original);

        ingredient.MarketUnit.Keys.ShouldBe(["default"]);
    }

    [Fact]
    public void The_picture_is_held_as_text_and_blank_means_no_value()
    {
        var original = Seed();
        var form = IngredientForm.From(original);
        form.Image.Thumb = "gao-200.webp";
        form.Image.Full = "  ";
        form.Image.Source = " tự chụp ";
        form.Image.License = "CC0";
        form.Image.IsIllustration = true;

        var image = form.ToIngredient(original).Image;

        image.ShouldBe(new ImageRef("gao-200.webp", null, "tự chụp", "CC0", true));
    }

    [Fact]
    public void The_form_is_a_copy_so_editing_it_does_not_change_the_ingredient_it_came_from()
    {
        var original = Seed();
        var form = IngredientForm.From(original);

        form.Names["default"] = "Khác";
        form.Units["default"].GramsPerUnit = 1;

        original.Names["default"].ShouldBe("Gạo");
        original.MarketUnit["default"].GramsPerUnit.ShouldBe(1000);
    }

    [Fact]
    public void The_arguments_are_checked()
    {
        Should.Throw<ArgumentNullException>(() => IngredientForm.From(null!));
        Should.Throw<ArgumentNullException>(() => new IngredientForm().ToIngredient(null!));
    }

    [Fact]
    public void The_four_keys_are_the_default_then_the_three_regions()
    {
        RegionKeyList.Keys.ShouldBe(["default", "north", "central", "south"]);
    }
}
