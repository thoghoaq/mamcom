using System.Text.Json;
using MamCom.Core.Json;
using MamCom.Core.Models;
using MamCom.UI.Placeholders;

namespace MamCom.UI.Tests;

public class PlaceholderCatalogTests
{
    private static readonly Aisle[] Aisles = Enum.GetValues<Aisle>();
    private static readonly DishSlot[] Slots = Enum.GetValues<DishSlot>();

    // ---- ingredients ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void Every_known_category_has_a_drawing_of_its_own()
    {
        var icons = PlaceholderCatalog.KnownCategories.Select(c => PlaceholderCatalog.ForIngredient(c, Aisle.Produce).Icon).ToArray();

        icons.Length.ShouldBe(13);
        icons.Distinct().Count().ShouldBe(icons.Length, "two categories must not share a drawing");
    }

    [Fact]
    public void Every_known_category_has_its_own_name()
    {
        var names = PlaceholderCatalog.KnownCategories.Select(c => PlaceholderCatalog.ForIngredient(c, Aisle.Produce).Name).ToArray();

        names.ShouldAllBe(n => !string.IsNullOrWhiteSpace(n));
        names.Distinct().Count().ShouldBe(names.Length);
    }

    [Theory]
    [InlineData("thit-heo", "pig", "Thịt heo")]
    [InlineData("thit-bo", "cow", "Thịt bò")]
    [InlineData("thit-ga", "meat", "Thịt gà")]
    [InlineData("ca", "fish", "Cá")]
    [InlineData("hai-san", "shrimp", "Hải sản")]
    [InlineData("trung", "egg", "Trứng")]
    [InlineData("dau-hu", "tofu", "Đậu phụ")]
    [InlineData("rau-cu", "carrot", "Rau củ")]
    [InlineData("rau-la", "leaf", "Rau lá")]
    [InlineData("qua", "apple", "Quả")]
    [InlineData("nam", "mushroom", "Nấm")]
    [InlineData("gia-vi", "salt", "Gia vị")]
    [InlineData("ngu-coc", "wheat", "Ngũ cốc")]
    public void A_category_is_drawn_as_what_it_is(string category, string icon, string name)
    {
        var placeholder = PlaceholderCatalog.ForIngredient(category, Aisle.Other);

        placeholder.Icon.ShouldBe(icon);
        placeholder.Name.ShouldBe(name);
        placeholder.Kind.ShouldBe(PlaceholderKind.Ingredient);
    }

    [Theory]
    [InlineData(Aisle.MeatAndFish, "aisle-meat")]
    [InlineData(Aisle.Produce, "aisle-produce")]
    [InlineData(Aisle.EggsAndTofu, "aisle-egg")]
    [InlineData(Aisle.DryGoodsAndSpices, "aisle-dry")]
    [InlineData(Aisle.Other, "aisle-other")]
    public void An_ingredient_takes_the_colour_of_its_aisle(Aisle aisle, string tone)
    {
        PlaceholderCatalog.ForIngredient("rau-cu", aisle).Tone.ShouldBe(tone);
        PlaceholderCatalog.ForIngredient("khong-biet", aisle).Tone.ShouldBe(tone);
    }

    [Fact]
    public void Each_aisle_has_a_colour_and_a_fallback_drawing_of_its_own()
    {
        var fallbacks = Aisles.Select(a => PlaceholderCatalog.ForIngredient("chua-co-hinh", a)).ToArray();

        fallbacks.Select(p => p.Tone).Distinct().Count().ShouldBe(Aisles.Length);
        fallbacks.Select(p => p.Icon).Distinct().Count().ShouldBe(Aisles.Length);
        fallbacks.Select(p => p.Name).Distinct().Count().ShouldBe(Aisles.Length);
    }

    [Theory]
    [InlineData(Aisle.MeatAndFish, "steak", "Thịt cá")]
    [InlineData(Aisle.Produce, "basket", "Rau củ quả")]
    [InlineData(Aisle.EggsAndTofu, "egg-cracked", "Trứng đậu phụ")]
    [InlineData(Aisle.DryGoodsAndSpices, "bottle", "Khô và gia vị")]
    [InlineData(Aisle.Other, "package", "Khác")]
    public void A_category_nobody_has_drawn_yet_falls_back_to_its_aisle_not_to_one_picture_for_all(Aisle aisle, string icon, string name)
    {
        var placeholder = PlaceholderCatalog.ForIngredient("loai-moi", aisle);

        placeholder.Icon.ShouldBe(icon);
        placeholder.Name.ShouldBe(name);
    }

    [Fact]
    public void A_known_category_keeps_its_own_drawing_in_whatever_aisle_it_is_filed()
    {
        PlaceholderCatalog.ForIngredient("thit-heo", Aisle.Other).Icon.ShouldBe("pig");
        PlaceholderCatalog.ForIngredient("thit-heo", Aisle.Other).Tone.ShouldBe("aisle-other");
    }

    // ---- dishes --------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Every_known_protein_source_has_a_drawing_of_its_own()
    {
        var icons = PlaceholderCatalog.KnownProteinSources.Select(p => PlaceholderCatalog.ForDish(DishSlot.Main, p).Icon).ToArray();

        icons.Length.ShouldBe(7);
        icons.Distinct().Count().ShouldBe(icons.Length);
    }

    [Theory]
    [InlineData(DishSlot.Soup, "slot-soup")]
    [InlineData(DishSlot.Main, "slot-main")]
    [InlineData(DishSlot.Vegetable, "slot-veg")]
    public void A_dish_takes_the_colour_of_its_part_of_the_meal(DishSlot slot, string tone)
    {
        PlaceholderCatalog.ForDish(slot, "heo").Tone.ShouldBe(tone);
        PlaceholderCatalog.ForDish(slot, "none").Tone.ShouldBe(tone);
    }

    [Theory]
    [InlineData("heo", "pig")]
    [InlineData("bo", "cow")]
    [InlineData("ga", "meat")]
    [InlineData("ca", "fish")]
    [InlineData("tom", "shrimp")]
    [InlineData("trung", "egg")]
    [InlineData("chay", "seedling")]
    public void A_dish_is_drawn_by_its_protein_source(string protein, string icon)
    {
        PlaceholderCatalog.ForDish(DishSlot.Main, protein).Icon.ShouldBe(icon);
    }

    [Theory]
    [InlineData(DishSlot.Soup, "soup", "Canh")]
    [InlineData(DishSlot.Main, "tools-kitchen-2", "Món mặn")]
    [InlineData(DishSlot.Vegetable, "salad", "Rau")]
    public void A_dish_without_a_protein_source_is_drawn_as_its_part_of_the_meal(DishSlot slot, string icon, string name)
    {
        foreach (var none in new[] { "none", null, "vit", string.Empty })
        {
            var placeholder = PlaceholderCatalog.ForDish(slot, none);

            placeholder.Icon.ShouldBe(icon, $"protein source: {none ?? "null"}");
            placeholder.Name.ShouldBe(name);
            placeholder.Kind.ShouldBe(PlaceholderKind.Dish);
        }
    }

    [Fact]
    public void No_two_kinds_of_dish_look_alike()
    {
        var proteins = PlaceholderCatalog.KnownProteinSources.Append("none");
        var looks = (from slot in Slots
                     from protein in proteins
                     select PlaceholderCatalog.ForDish(slot, protein)).ToArray();

        looks.Length.ShouldBe(Slots.Length * 8);
        looks.Select(p => (p.Tone, p.Icon)).Distinct().Count().ShouldBe(looks.Length, "a soup and a main dish with the same meat must still differ by colour");
        looks.Select(p => p.Name).Distinct().Count().ShouldBe(looks.Length, "and by name");
    }

    [Theory]
    [InlineData(DishSlot.Main, "heo", "Món mặn · thịt heo")]
    [InlineData(DishSlot.Soup, "ca", "Canh · cá")]
    [InlineData(DishSlot.Vegetable, "chay", "Rau · đạm chay")]
    public void A_dish_is_named_by_its_part_of_the_meal_and_its_protein(DishSlot slot, string protein, string name)
    {
        PlaceholderCatalog.ForDish(slot, protein).Name.ShouldBe(name);
    }

    // ---- the words for a screen reader -------------------------------------------------------------------------------------

    [Theory]
    [InlineData("thit-heo", "Chưa có ảnh: thịt heo")]
    [InlineData("dau-hu", "Chưa có ảnh: đậu phụ")]
    public void A_screen_reader_is_told_it_is_not_a_photo_and_what_it_stands_for(string category, string said)
    {
        PlaceholderCatalog.Describe(PlaceholderCatalog.ForIngredient(category, Aisle.MeatAndFish)).ShouldBe(said);
    }

    // ---- the data ------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Every_category_and_protein_source_of_the_seed_has_a_drawing_of_its_own_and_not_the_fallback()
    {
        var ingredients = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "seed", "ingredients"), "*.json")
            .Select(f => JsonSerializer.Deserialize<Ingredient>(File.ReadAllText(f), CoreJson.Options)!)
            .ToArray();
        var dishes = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "seed", "dishes"), "*.json")
            .Select(f => JsonSerializer.Deserialize<Dish>(File.ReadAllText(f), CoreJson.Options)!)
            .ToArray();

        ingredients.Length.ShouldBeGreaterThan(20);
        dishes.Length.ShouldBeGreaterThan(10);
        ingredients.Select(i => i.Category).Distinct().Except(PlaceholderCatalog.KnownCategories).ShouldBeEmpty("a category with no drawing of its own");
        dishes.Select(d => d.ProteinSource).Distinct()
            .Except(PlaceholderCatalog.KnownProteinSources)
            .ShouldBe(["none"], "every protein source but 'none' has a drawing of its own");
    }

    [Fact]
    public void The_placeholder_of_an_ingredient_or_a_dish_is_the_one_of_its_fields()
    {
        var dish = JsonSerializer.Deserialize<Dish>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "seed", "dishes", "ca-thu-chien.json")), CoreJson.Options)!;
        var ingredient = JsonSerializer.Deserialize<Ingredient>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "seed", "ingredients", "thit-ba-chi.json")), CoreJson.Options)!;

        PlaceholderCatalog.ForDish(dish).ShouldBe(PlaceholderCatalog.ForDish(dish.Slot, dish.ProteinSource));
        PlaceholderCatalog.ForIngredient(ingredient).ShouldBe(PlaceholderCatalog.ForIngredient(ingredient.Category, ingredient.Aisle));
        PlaceholderCatalog.ForIngredient(ingredient).Icon.ShouldBe("pig");
        PlaceholderCatalog.ForDish(dish).Icon.ShouldBe("fish");
    }

    [Fact]
    public void The_arguments_are_checked_and_an_aisle_or_slot_that_does_not_exist_is_refused()
    {
        Should.Throw<ArgumentNullException>(() => PlaceholderCatalog.ForIngredient((Ingredient)null!));
        Should.Throw<ArgumentNullException>(() => PlaceholderCatalog.ForIngredient(null!, Aisle.Other));
        Should.Throw<ArgumentNullException>(() => PlaceholderCatalog.ForDish((Dish)null!));
        Should.Throw<ArgumentNullException>(() => PlaceholderCatalog.Describe(null!));
        Should.Throw<ArgumentOutOfRangeException>(() => PlaceholderCatalog.ForIngredient("rau-cu", (Aisle)99));
        Should.Throw<ArgumentOutOfRangeException>(() => PlaceholderCatalog.ForDish((DishSlot)99, "heo"));
    }
}
