using MamCom.Core.Cooking;
using MamCom.Core.Models;
using MamCom.Core.Nutrition;
using MamCom.Core.Portions;
using MamCom.Core.Shopping;
using MamCom.Core.Spinning;
using static MamCom.Core.Tests.Spinning.SpinnerFixtures;

namespace MamCom.Core.Tests.Spinning;

/// <summary>Meals that are not a tray of rice (ADR-037): a bowl, a hot pot, a breakfast.</summary>
public class MealKindTests
{
    private static readonly Dish Pho = D("pho-bo", DishSlot.Bowl, "bo", CookingMethod.Simmer, ["thit-bo", "cai"], minutes: 40);
    private static readonly Dish BunCha = D("bun-cha", DishSlot.Bowl, "heo", CookingMethod.Grill, ["thit-heo", "rau-muong"], minutes: 45);
    private static readonly Dish LauCa = D("lau-ca", DishSlot.Hotpot, "ca", CookingMethod.Simmer, ["ca-loc", "cai"], minutes: 50);
    private static readonly Dish BanhMiTrung = D("banh-mi-trung", DishSlot.Breakfast, "trung", CookingMethod.Fry, ["trung"], minutes: 10);

    private static Dish[] AllDishes() => [.. Catalog(), Pho, BunCha, LauCa, BanhMiTrung];

    private static TrayDesk Desk(DateOnly? today = null)
    {
        var clock = new FixedClock(today ?? Today);
        return new TrayDesk(new MealSpinner(Config, PortionSettings, new SeededRandomSource(3), clock, new SumOfStepsEstimator()), Config, clock);
    }

    private static SpinContext Home() => Context(SmallHome(), AllDishes());

    private static DishSlot[] Kinds(TraySession session) => [.. session.Viewed!.Tray.Slots.Select(s => s.Slot)];

    [Fact]
    public void A_meal_has_rice_when_it_has_a_dish_of_the_tray_or_no_dish_at_all()
    {
        new[] { DishSlot.Soup, DishSlot.Main, DishSlot.Vegetable }.HasRice().ShouldBeTrue();
        new[] { DishSlot.Bowl }.HasRice().ShouldBeFalse();
        new[] { DishSlot.Hotpot }.HasRice().ShouldBeFalse();
        new[] { DishSlot.Breakfast }.HasRice().ShouldBeFalse();
        Array.Empty<DishSlot>().HasRice().ShouldBeTrue();
    }

    [Fact]
    public void Each_kind_of_meal_is_spun_as_its_one_dish_and_a_tray_as_before()
    {
        var desk = Desk();

        Kinds(desk.Spin(TraySession.Empty, Home())).ShouldBe([DishSlot.Soup, DishSlot.Main, DishSlot.Vegetable]);
        Kinds(desk.ChooseMeal(TraySession.Empty, Home(), MealKind.Bowl)).ShouldBe([DishSlot.Bowl]);
        Kinds(desk.ChooseMeal(TraySession.Empty, Home(), MealKind.Hotpot)).ShouldBe([DishSlot.Hotpot]);
        Kinds(desk.ChooseMeal(TraySession.Empty, Home(), MealKind.Breakfast)).ShouldBe([DishSlot.Breakfast]);
    }

    [Fact]
    public void The_meal_of_today_is_kept_by_the_next_spins_and_another_day_is_a_tray_again()
    {
        var desk = Desk();
        var bowl = desk.ChooseMeal(desk.Spin(TraySession.Empty, Home()), Home(), MealKind.Bowl);

        desk.MealOf(bowl).ShouldBe(MealKind.Bowl);
        Kinds(desk.Spin(bowl, Home())).ShouldBe([DishSlot.Bowl]);
        desk.CanAddDish(bowl).ShouldBeFalse();   // one dish more or less is for a tray of rice
        desk.CanRemoveDish(bowl).ShouldBeFalse();

        var tomorrow = Desk(Today.AddDays(1));
        tomorrow.MealOf(bowl).ShouldBe(MealKind.Tray);
        Kinds(tomorrow.Spin(bowl, Home())).Length.ShouldBe(3);
    }

    [Fact]
    public void Choosing_a_meal_drops_the_locks_of_the_tray_and_choosing_the_same_one_changes_nothing()
    {
        var desk = Desk();
        var locked = desk.ToggleLock(desk.Spin(TraySession.Empty, Home()), 1);

        var bowl = desk.ChooseMeal(locked, Home(), MealKind.Bowl);

        bowl.Viewed!.Tray.Slots.ShouldAllBe(s => !s.IsLocked);
        desk.ChooseMeal(bowl, Home(), MealKind.Bowl).ShouldBeSameAs(bowl);
        Kinds(desk.ChooseMeal(bowl, Home(), MealKind.Tray)).ShouldBe([DishSlot.Soup, DishSlot.Main, DishSlot.Vegetable]);
    }

    [Fact]
    public void A_bowl_has_no_rice_in_the_cook_plan_the_list_or_the_energy()
    {
        var cooking = CookingConfig.Parse(TestFiles.Config("cooking-defaults.json"));
        var nutrition = NutritionConfig.Parse(TestFiles.Config("nutrition-defaults.json"));
        var household = SmallHome();
        var ingredients = new Dictionary<string, Ingredient>(Ingredients()) { [PortionSettings.RiceIngredientId] = MamCom.Core.Tests.Shopping.ShoppingFixtures.Rice };
        var portions = new PortionCalculator(PortionSettings).Calculate(household);

        new CookPlanScheduler(cooking).Schedule([Pho]).Steps.ShouldNotContain(s => s.DishId == CookPlan.RiceDishId);
        new CookPlanScheduler(cooking).Schedule([Find("canh-bi-do")]).Steps.ShouldContain(s => s.DishId == CookPlan.RiceDishId);

        // Dishes of nothing but water, so the list has only what the meal itself adds: the rice, or not.
        var plainBowl = D("to-nuoc", DishSlot.Bowl, ProteinSources.None, CookingMethod.Simmer, []);
        var plainSoup = D("canh-nuoc", DishSlot.Soup, ProteinSources.None, CookingMethod.Simmer, []);
        new ShoppingListBuilder(PortionSettings).Build(household, portions, [plainBowl], ingredients).ShouldBeEmpty();
        new ShoppingListBuilder(PortionSettings).Build(household, portions, [plainSoup], ingredients).Single().IngredientId.ShouldBe(PortionSettings.RiceIngredientId);

        var calculator = new NutritionCalculator(nutrition, PortionSettings);
        var withPho = calculator.ForMeal(household, portions, [Pho], ingredients).Total;
        var phoAlone = calculator.ForDish(Pho, ingredients).PerServing.Times(portions.BaseServings);
        withPho.Kcal.ShouldBe(phoAlone.Kcal);
    }
}
