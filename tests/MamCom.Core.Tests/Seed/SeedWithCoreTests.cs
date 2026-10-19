using MamCom.Core.Cooking;
using MamCom.Core.Models;
using MamCom.Core.Nutrition;
using MamCom.Core.Portions;
using MamCom.Core.Scaling;
using MamCom.Core.Shopping;
using MamCom.Core.Spinning;
using MamCom.Core.Tests.Spinning;

namespace MamCom.Core.Tests.Seed;

/// <summary>The seed run through the real services, to catch data the code cannot use before the DataBuilder exists (PLAN 2.2).</summary>
public class SeedWithCoreTests
{
    private static readonly SpinnerConfig SpinnerSettings = SpinnerConfig.Parse(TestFiles.Config("spinner-defaults.json"));
    private static readonly PortionConfig PortionSettings = PortionConfig.Parse(TestFiles.Config("portion-defaults.json"));
    private static readonly NutritionConfig NutritionSettings = NutritionConfig.Parse(TestFiles.Config("nutrition-defaults.json"));
    private static readonly CookPlanScheduler Scheduler = new(CookingConfig.Parse(TestFiles.Config("cooking-defaults.json")));
    private static readonly DateOnly Today = new(2026, 10, 19);

    private static readonly Region[] AllRegions = [Region.North, Region.Central, Region.South];

    private static Member Adult(string name, double bowls = 2, MemberTag[]? tags = null, string[]? avoid = null) =>
        new(name, AgeGroup.Adult, bowls, tags ?? [], avoid ?? []);

    private static Household Family(Region region, int adults, MemberTag[]? tags = null, string[]? avoid = null, int? minutes = null) =>
        new(region, BowlSize.Medium, Enumerable.Range(0, adults).Select(i => Adult($"P{i}", tags: tags, avoid: avoid)).ToArray(), minutes);

    private static MealSpinner Spinner(ulong seed) => new(
        SpinnerSettings, PortionSettings, new SeededRandomSource(seed), new FixedClock(Today), Scheduler);

    private static SpinContext ContextFor(Household household) => new(
        household,
        SeedData.Dishes,
        SeedData.Ingredients,
        new HashSet<string>(StringComparer.Ordinal),
        new Dictionary<string, DateOnly>(),
        new Dictionary<string, DishPreference>());

    private static Dish[] DishesOf(SpinResult result) =>
        result.Tray.Slots.Select(slot => SeedData.Dishes.Single(d => d.Id == slot.DishId)).ToArray();

    // ---- spinning ----------------------------------------------------------------------------------

    [Fact]
    public void Every_region_and_household_size_gets_a_full_tray_without_relaxing_any_rule()
    {
        foreach (var region in AllRegions)
        {
            foreach (var adults in new[] { 2, 4, 5, 6 })
            {
                var household = Family(region, adults);
                var expectedDishes = adults >= 3 ? 3 + (adults >= 5 ? 1 : 0) : 3;

                for (ulong seed = 0; seed < 300; seed++)
                {
                    var result = Spinner(seed).Spin(ContextFor(household));

                    result.UnfilledSlots.ShouldBeEmpty($"{region} {adults} adults seed {seed}");
                    result.RelaxedRules.ShouldBeEmpty($"{region} {adults} adults seed {seed}");
                    result.Tray.Slots.Count.ShouldBe(expectedDishes);
                    result.Tray.Slots.Select(s => s.DishId).ShouldBeUnique();
                }
            }
        }
    }

    [Fact]
    public void The_hard_rules_hold_for_a_household_that_avoids_seafood_and_cannot_eat_spicy_food()
    {
        var household = Family(Region.South, adults: 5, tags: [MemberTag.NoSpicy], avoid: ["hai-san", "trung"]);
        var unrestricted = Family(Region.South, adults: 5);

        static bool Spicy(Dish dish) => dish.Spicy == SpicyLevel.Required;
        static bool HasAvoided(Dish dish) => dish.Ingredients
            .Select(l => SeedData.Ingredients[l.IngredientId])
            .Any(i => i.Category is "hai-san" or "trung");

        var sawSpicyWhenAllowed = false;
        var sawAvoidedWhenAllowed = false;

        for (ulong seed = 0; seed < 500; seed++)
        {
            var result = Spinner(seed).Spin(ContextFor(household));
            var dishes = DishesOf(result);

            result.UnfilledSlots.ShouldBeEmpty();
            dishes.Any(Spicy).ShouldBeFalse($"seed {seed}: a required-spicy dish for a household that cannot eat spicy food");
            dishes.Any(HasAvoided).ShouldBeFalse($"seed {seed}: a dish with an avoided ingredient");

            // Control: without the restrictions the same seeds do draw such dishes, so the checks above are not vacuous.
            var free = DishesOf(Spinner(seed).Spin(ContextFor(unrestricted)));
            sawSpicyWhenAllowed |= free.Any(Spicy);
            sawAvoidedWhenAllowed |= free.Any(HasAvoided);
        }

        sawSpicyWhenAllowed.ShouldBeTrue();
        sawAvoidedWhenAllowed.ShouldBeTrue();
    }

    [Fact]
    public void A_tight_time_limit_picks_quick_dishes_and_the_estimate_stays_within_it()
    {
        // Rice alone takes 35 minutes; 60 leaves room for quick dishes but not for a long braise or a long soup.
        var limited = Family(Region.South, adults: 2, minutes: 60);
        var unlimited = Family(Region.South, adults: 2);
        var overTheLimitWithoutIt = 0;

        for (ulong seed = 0; seed < 300; seed++)
        {
            var result = Spinner(seed).Spin(ContextFor(limited));

            // The seed has quick dishes in every slot, so the limit can always be met without giving the rule up.
            result.RelaxedRules.ShouldBeEmpty($"seed {seed}");
            Scheduler.EstimateMealMinutes(DishesOf(result)).ShouldBeLessThanOrEqualTo(60, $"seed {seed}");

            // Control: without the limit a good share of trays take longer, so the limit is really being applied.
            if (Scheduler.EstimateMealMinutes(DishesOf(Spinner(seed).Spin(ContextFor(unlimited)))) > 60)
            {
                overTheLimitWithoutIt++;
            }
        }

        overTheLimitWithoutIt.ShouldBeGreaterThan(30);
    }

    // ---- the rest of a meal ---------------------------------------------------------------------------

    [Fact]
    public void A_spun_meal_has_a_shopping_list_a_nutrition_summary_and_a_cook_plan()
    {
        var calculator = new PortionCalculator(PortionSettings);
        var shopping = new ShoppingListBuilder(PortionSettings);
        var nutrition = new NutritionCalculator(NutritionSettings, PortionSettings);

        foreach (var region in AllRegions)
        {
            var household = Family(region, adults: 4);
            var portions = calculator.Calculate(household);

            for (ulong seed = 0; seed < 100; seed++)
            {
                var dishes = DishesOf(Spinner(seed).Spin(ContextFor(household)));

                var list = shopping.Build(household, portions, dishes, SeedData.Ingredients);
                list.ShouldNotBeEmpty();
                list.Select(i => i.IngredientId).ShouldBeUnique();
                list.ShouldContain(i => i.IngredientId == PortionSettings.RiceIngredientId);
                list.ShouldAllBe(i => i.Quantity > 0 && i.PurchaseGrams > 0 && !string.IsNullOrEmpty(i.UnitDisplay));

                // Placeholder data: the summary exists but admits what it does not know.
                var summary = nutrition.ForMeal(household, portions, dishes, SeedData.Ingredients);
                summary.Total.Kcal.ShouldBeNull();
                summary.EnergyShares.ShouldBeNull();
                summary.Dishes.ShouldAllBe(d => d.IngredientIdsMissingData.Count > 0);

                var plan = Scheduler.Schedule(dishes);
                plan.MealTimeMinutes.ShouldBeGreaterThanOrEqualTo(35);   // the rice
                plan.Steps[0].DishId.ShouldBe(CookPlan.RiceDishId);
                plan.Steps.Select(s => s.DishId).Distinct().Count().ShouldBe(dishes.Length + 1);
            }
        }
    }

    [Fact]
    public void Every_dish_scales_for_every_region_and_household_size()
    {
        var scaler = new IngredientScaler(PortionSettings);

        foreach (var dish in SeedData.Dishes)
        {
            foreach (var region in AllRegions)
            {
                foreach (var servings in new[] { 0.4, 1.2, 3.7875, 5, 9 })
                {
                    var scaled = scaler.ScaleDish(dish, SeedData.Ingredients, region, servings);

                    scaled.Count.ShouldBe(dish.Ingredients.Count);
                    scaled.ShouldAllBe(s => s.GramsEdible > 0, $"{dish.Id} {region} {servings}");
                }
            }
        }
    }

    [Fact]
    public void Rice_is_not_listed_as_a_dish_ingredient_and_the_meal_buys_it_by_the_kilo()
    {
        var household = Family(Region.Central, adults: 3);
        var portions = new PortionCalculator(PortionSettings).Calculate(household);

        var list = new ShoppingListBuilder(PortionSettings).Build(household, portions, [], SeedData.Ingredients);

        var rice = list.ShouldHaveSingleItem();
        rice.IngredientId.ShouldBe("gao");
        rice.UnitDisplay.ShouldBe("kg");
        rice.IsPantry.ShouldBeTrue();
    }
}
