using System.Text.RegularExpressions;
using MamCom.Core.Models;
using MamCom.Core.Portions;

namespace MamCom.Core.Tests.Seed;

/// <summary>
/// The dev seed is plain data, so these tests are its contract: it is well formed, it never invents numbers, and it
/// is varied enough to exercise every rule of SPEC §5 and §6 (spicy, fried and shared-protein dishes, every scale kind…).
/// </summary>
public partial class SeedDataTests
{
    private static readonly Region[] AllRegions = [Region.North, Region.Central, Region.South];

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex KebabCase();

    private static IEnumerable<Dish> Of(DishSlot slot) => SeedData.Dishes.Where(d => d.Slot == slot);

    // ---- shape of the files -------------------------------------------------------------------------

    [Fact]
    public void The_seed_has_seven_soups_eight_mains_and_five_vegetables()
    {
        Of(DishSlot.Soup).Count().ShouldBe(7);
        Of(DishSlot.Main).Count().ShouldBe(8);
        Of(DishSlot.Vegetable).Count().ShouldBe(5);
    }

    [Fact]
    public void Every_file_is_named_after_its_id_and_ids_are_kebab_case_and_unique()
    {
        foreach (var file in SeedData.IngredientFiles)
        {
            file.FileName.ShouldBe(file.Value.Id + ".json");
        }

        foreach (var file in SeedData.DishFiles)
        {
            file.FileName.ShouldBe(file.Value.Id + ".json");
        }

        var ids = SeedData.IngredientList.Select(i => i.Id).Concat(SeedData.Dishes.Select(d => d.Id)).ToArray();
        ids.ShouldAllBe(id => KebabCase().IsMatch(id));
        SeedData.IngredientList.Select(i => i.Id).ShouldBeUnique();
        SeedData.Dishes.Select(d => d.Id).ShouldBeUnique();
        SeedData.Dishes.ShouldNotContain(d => d.Id == CookPlan.RiceDishId);
    }

    [Fact]
    public void Files_are_in_the_canonical_format_so_a_tool_rewriting_them_makes_no_diff()
    {
        foreach (var file in SeedData.IngredientFiles)
        {
            file.Text.ShouldBe(SeedData.Write(file.Value), $"ingredients/{file.FileName}");
        }

        foreach (var file in SeedData.DishFiles)
        {
            file.Text.ShouldBe(SeedData.Write(file.Value), $"dishes/{file.FileName}");
        }
    }

    // ---- references ------------------------------------------------------------------------------

    [Fact]
    public void Every_ingredient_reference_exists()
    {
        foreach (var dish in SeedData.Dishes)
        {
            foreach (var line in dish.Ingredients)
            {
                SeedData.Ingredients.ShouldContainKey(line.IngredientId, $"{dish.Id} uses it");
            }

            foreach (var id in dish.Steps.SelectMany(s => s.IngredientIds))
            {
                SeedData.Ingredients.ShouldContainKey(id, $"a step of {dish.Id} uses it");
            }
        }
    }

    [Fact]
    public void Every_recipe_line_appears_in_a_step_and_every_step_ingredient_is_a_recipe_line()
    {
        foreach (var dish in SeedData.Dishes)
        {
            var lines = dish.Ingredients.Select(l => l.IngredientId).ToHashSet();
            var inSteps = dish.Steps.SelectMany(s => s.IngredientIds).ToHashSet();

            inSteps.ShouldBeSubsetOf(lines, $"{dish.Id}: a step names an ingredient the recipe does not list");
            lines.ShouldBeSubsetOf(inSteps, $"{dish.Id}: a recipe line is never used in a step");
        }
    }

    [Fact]
    public void Every_ingredient_except_rice_is_used_by_some_dish()
    {
        var used = SeedData.Dishes.SelectMany(d => d.Ingredients).Select(l => l.IngredientId).ToHashSet();
        var unused = SeedData.IngredientList.Select(i => i.Id).Where(id => !used.Contains(id)).ToArray();

        unused.ShouldBe([RiceId]);
    }

    private static string RiceId => PortionConfig.Parse(TestFiles.Config("portion-defaults.json")).RiceIngredientId;

    [Fact]
    public void The_rice_ingredient_named_in_the_config_exists_and_is_a_pantry_item()
    {
        SeedData.Ingredients.ShouldContainKey(RiceId);
        SeedData.Ingredients[RiceId].IsPantry.ShouldBeTrue();
    }

    // ---- recipes -------------------------------------------------------------------------------------

    [Fact]
    public void Steps_are_numbered_from_one_and_each_takes_time()
    {
        foreach (var dish in SeedData.Dishes)
        {
            dish.Steps.Select(s => s.Order).ShouldBe(Enumerable.Range(1, dish.Steps.Count).ToArray(), dish.Id);
            dish.Steps.ShouldNotBeEmpty(dish.Id);
            foreach (var step in dish.Steps)
            {
                step.ActiveMinutes.ShouldBeGreaterThanOrEqualTo(0, dish.Id);
                step.PassiveMinutes.ShouldBeGreaterThanOrEqualTo(0, dish.Id);
                (step.ActiveMinutes + step.PassiveMinutes).ShouldBeGreaterThan(0, $"{dish.Id} step {step.Order}");
                step.Text.ShouldNotBeNullOrWhiteSpace();
            }
        }
    }

    [Fact]
    public void Recipe_amounts_are_positive_and_per_serving_sized()
    {
        foreach (var dish in SeedData.Dishes)
        {
            foreach (var line in dish.Ingredients)
            {
                line.GramsEdiblePerServing.ShouldBeGreaterThan(0, $"{dish.Id}/{line.IngredientId}");
                line.GramsEdiblePerServing.ShouldBeLessThan(300, $"{dish.Id}/{line.IngredientId}");
                line.ConsumedRatio.ShouldBeInRange(0.0, 1.0);
            }

            dish.Ingredients.Select(l => l.IngredientId).ShouldBeUnique(dish.Id);
        }
    }

    [Fact]
    public void A_main_dish_has_between_fifty_and_one_hundred_fifty_grams_of_protein_source_per_serving()
    {
        // The SPEC §7.3 warning range, kept clean in the seed so a future validate has no noise to explain.
        foreach (var dish in Of(DishSlot.Main).Where(d => d.ProteinSource is not (ProteinSources.None or ProteinSources.Vegetarian)))
        {
            var grams = dish.Ingredients
                .Where(l => SeedData.Ingredients[l.IngredientId].ProteinSource == dish.ProteinSource)
                .Sum(l => l.GramsEdiblePerServing);

            grams.ShouldBeInRange(50.0, 150.0, dish.Id);
        }
    }

    [Fact]
    public void A_dish_protein_source_is_the_protein_of_one_of_its_ingredients_or_none()
    {
        foreach (var dish in SeedData.Dishes)
        {
            var proteins = dish.Ingredients.Select(l => SeedData.Ingredients[l.IngredientId].ProteinSource).OfType<string>().ToHashSet();

            if (dish.ProteinSource == ProteinSources.None)
            {
                proteins.ShouldBeEmpty(dish.Id);
            }
            else
            {
                proteins.ShouldContain(dish.ProteinSource, dish.Id);
            }
        }
    }

    // ---- data that must stay honest -----------------------------------------------------------------

    [Fact]
    public void No_nutrition_number_is_invented()
    {
        foreach (var ingredient in SeedData.IngredientList)
        {
            ingredient.NutritionPer100gEdible.ShouldBe(new NutritionFacts(null, null, null, null, null, null), ingredient.Id);
            ingredient.NutritionSource.ShouldBe(new NutritionSource(NutritionDbs.Placeholder, null, null, null, null), ingredient.Id);
        }
    }

    [Fact]
    public void Every_dish_is_an_unreviewed_draft_and_no_one_has_cooked_it()
    {
        foreach (var dish in SeedData.Dishes)
        {
            dish.Status.ShouldBe(DishStatus.Draft, dish.Id);
            dish.Provenance.ShouldBe(new Provenance(DraftAuthor.Llm, null, null, null, null), dish.Id);
        }
    }

    [Fact]
    public void No_image_is_declared()
    {
        var none = new ImageRef(null, null, null, null, false);
        SeedData.IngredientList.ShouldAllBe(i => i.Image == none);
        SeedData.Dishes.ShouldAllBe(d => d.Image == none);
    }

    // ---- ingredients ---------------------------------------------------------------------------------

    [Fact]
    public void Every_ingredient_has_a_name_and_a_market_unit_for_all_three_regions()
    {
        foreach (var ingredient in SeedData.IngredientList)
        {
            ingredient.Names.ShouldContainKey(RegionKeys.Default, ingredient.Id);
            foreach (var region in AllRegions)
            {
                var unit = ingredient.MarketUnitFor(region);
                unit.ShouldNotBeNull($"{ingredient.Id} in {region}");
                unit.Display.ShouldNotBeNullOrWhiteSpace();
                unit.GramsPerUnit.ShouldBeGreaterThan(0);
                unit.Step.ShouldBeGreaterThan(0);
            }
        }
    }

    [Fact]
    public void Waste_ratios_are_fractions_below_one()
    {
        SeedData.IngredientList.ShouldAllBe(i => i.WasteRatio >= 0 && i.WasteRatio < 1);
    }

    [Fact]
    public void Approximate_grams_are_only_asked_for_on_counted_units()
    {
        // "(~500g)" after "1 con" makes sense; after "3 lạng" or "250 g" it would repeat the quantity.
        foreach (var ingredient in SeedData.IngredientList)
        {
            foreach (var (key, unit) in ingredient.MarketUnit.Where(pair => pair.Value.ShowApproxGrams))
            {
                unit.GramsPerUnit.ShouldBeGreaterThan(1, $"{ingredient.Id}/{key}");
            }
        }
    }

    // ---- variety for the rules --------------------------------------------------------------------------

    [Fact]
    public void The_seed_has_dishes_to_test_the_spicy_rule()
    {
        SeedData.Dishes.Select(d => d.Spicy).Distinct().Order().ShouldBe([SpicyLevel.None, SpicyLevel.Optional, SpicyLevel.Required]);
        Of(DishSlot.Main).ShouldContain(d => d.Spicy == SpicyLevel.Required);
    }

    [Fact]
    public void The_seed_has_several_fried_mains_to_test_the_one_fried_dish_rule()
    {
        Of(DishSlot.Main).Count(d => d.CookingMethod == CookingMethod.Fry).ShouldBeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void The_seed_has_mains_sharing_a_protein_source_and_a_soup_sharing_one_with_a_main()
    {
        var counted = Of(DishSlot.Main)
            .Select(d => d.ProteinSource)
            .Where(p => p is not (ProteinSources.None or ProteinSources.Vegetarian))
            .GroupBy(p => p);

        counted.ShouldContain(g => g.Count() >= 2);

        var mainProteins = Of(DishSlot.Main).Select(d => d.ProteinSource).ToHashSet();
        Of(DishSlot.Soup).ShouldContain(d => mainProteins.Contains(d.ProteinSource) && d.ProteinSource != ProteinSources.None);
    }

    [Fact]
    public void The_seed_has_dishes_with_no_protein_source_and_with_a_vegetarian_one()
    {
        SeedData.Dishes.ShouldContain(d => d.ProteinSource == ProteinSources.None);
        SeedData.Dishes.ShouldContain(d => d.ProteinSource == ProteinSources.Vegetarian);
    }

    [Fact]
    public void The_seed_has_dishes_for_each_region_and_for_the_seasons()
    {
        foreach (var region in AllRegions)
        {
            SeedData.Dishes.ShouldContain(d => d.Regions.Contains(region), region.ToString());
            Of(DishSlot.Soup).ShouldContain(d => d.Regions.Contains(region), $"soup for {region}");
            Of(DishSlot.Main).ShouldContain(d => d.Regions.Contains(region), $"main for {region}");
        }

        var seasons = SeedData.Dishes.SelectMany(d => d.Seasons).ToHashSet();
        seasons.ShouldBeSubsetOf(["all", "spring", "summer", "autumn", "winter"]);
        seasons.ShouldContain(SeasonIds.All);
        seasons.ShouldContain("summer");
        seasons.ShouldContain("winter");
    }

    [Fact]
    public void The_seed_uses_every_scale_kind_and_every_aisle_a_shopper_meets()
    {
        SeedData.Dishes.SelectMany(d => d.Ingredients).Select(l => l.Scale).Distinct().Order()
            .ShouldBe([ScaleKind.Linear, ScaleKind.Sublinear, ScaleKind.Integer, ScaleKind.Fixed]);

        SeedData.IngredientList.Select(i => i.Aisle).Distinct()
            .ShouldBeSubsetOf([Aisle.MeatAndFish, Aisle.Produce, Aisle.EggsAndTofu, Aisle.DryGoodsAndSpices, Aisle.Other]);
        SeedData.IngredientList.Select(i => i.Aisle).Distinct().Count().ShouldBeGreaterThanOrEqualTo(4);
        SeedData.IngredientList.ShouldContain(i => i.IsPantry);
        SeedData.IngredientList.ShouldContain(i => i.MarketUnit.Values.Any(u => u.ShowApproxGrams));
    }

    [Fact]
    public void The_seed_has_hot_and_not_hot_dishes_and_a_slow_and_a_quick_dish()
    {
        SeedData.Dishes.ShouldContain(d => d.ServeHot);
        SeedData.Dishes.ShouldContain(d => !d.ServeHot);
        SeedData.Dishes.ShouldContain(d => d.Steps.Sum(s => s.ActiveMinutes + s.PassiveMinutes) >= 60);
        SeedData.Dishes.ShouldContain(d => d.Steps.Sum(s => s.ActiveMinutes + s.PassiveMinutes) <= 10);
    }

    [Fact]
    public void Ingredients_come_from_categories_a_member_could_avoid()
    {
        var categories = SeedData.IngredientList.Select(i => i.Category).ToHashSet();

        categories.ShouldContain("hai-san");
        categories.ShouldContain("thit-heo");
        categories.ShouldContain("trung");
    }
}
