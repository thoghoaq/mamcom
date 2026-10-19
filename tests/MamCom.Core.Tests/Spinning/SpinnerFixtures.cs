using MamCom.Core.Cooking;
using MamCom.Core.Models;
using MamCom.Core.Portions;
using MamCom.Core.Spinning;

namespace MamCom.Core.Tests.Spinning;

internal sealed class FixedClock(DateOnly today) : IClock
{
    public DateOnly Today { get; set; } = today;

    public DateTimeOffset Now => new(Today.ToDateTime(new TimeOnly(18, 0)), TimeSpan.Zero);
}

/// <summary>Cooking time as the sum of every step of every dish; simple enough to set up exact cases.</summary>
internal sealed class SumOfStepsEstimator : IMealTimeEstimator
{
    public int EstimateMealMinutes(IReadOnlyList<Dish> dishes) =>
        dishes.Sum(d => d.Steps.Sum(s => s.ActiveMinutes + s.PassiveMinutes));

    // A sum only grows as dishes are added, so it is its own lower bound.
    public int MinimumMealMinutes(IReadOnlyList<Dish> dishes) => EstimateMealMinutes(dishes);
}

/// <summary>Same estimate as the sum, but a lower bound of zero: the spinner can never prove a limit is lost early.</summary>
internal sealed class SumWithNoBoundEstimator : IMealTimeEstimator
{
    private readonly SumOfStepsEstimator _sum = new();

    public int EstimateMealMinutes(IReadOnlyList<Dish> dishes) => _sum.EstimateMealMinutes(dishes);

    public int MinimumMealMinutes(IReadOnlyList<Dish> dishes) => 0;
}

/// <summary>Returns the given values in turn, then 0 forever: lets a test decide exactly which candidate is picked.</summary>
internal sealed class ScriptedRandom(params double[] values) : IRandomSource
{
    private int _next;

    public int Calls => _next;

    public double NextDouble() => _next < values.Length ? values[_next++] : Advance(0.0);

    public int NextInt(int maxExclusive) => (int)(NextDouble() * maxExclusive);

    private double Advance(double value)
    {
        _next++;
        return value;
    }
}

/// <summary>
/// A small invented catalog (test-only data) with the variety the SPEC rules need: spicy, fried and shared protein
/// dishes, seasonal and regional dishes, and ingredients in categories members can avoid.
/// </summary>
internal static class SpinnerFixtures
{
    public static readonly SpinnerConfig Config = SpinnerConfig.Parse(TestFiles.Config("spinner-defaults.json"));

    public static readonly PortionConfig PortionSettings = PortionConfig.Parse(TestFiles.Config("portion-defaults.json"));

    public static readonly DateOnly Today = new(2026, 10, 19);   // a Monday in autumn per the shipped season map

    public static Dictionary<string, Ingredient> Ingredients() => TestData.Catalog(
        Ing("thit-heo", "thit-heo"), Ing("thit-bo", "bo"), Ing("ga", "thit-ga"), Ing("ca-loc", "ca"),
        Ing("tom", "hai-san"), Ing("muc", "hai-san"), Ing("dau-phu", "dau-phu"), Ing("trung", "trung"),
        Ing("rau-muong", "rau"), Ing("bi-do", "rau"), Ing("ca-chua", "rau"), Ing("cai", "rau"), Ing("ot", "gia-vi"));

    private static Ingredient Ing(string id, string category) => TestData.Ingredient(id) with { Category = category };

    public static Dish D(
        string id,
        DishSlot slot,
        string protein,
        CookingMethod method,
        string[] ingredients,
        SpicyLevel spicy = SpicyLevel.None,
        string[]? seasons = null,
        Region[]? regions = null,
        int minutes = 20) => TestData.Dish(id, ingredients.Select(i => TestData.Line(i, 50)).ToArray()) with
    {
        Slot = slot,
        ProteinSource = protein,
        CookingMethod = method,
        Spicy = spicy,
        Seasons = seasons ?? [SeasonIds.All],
        Regions = regions ?? [],
        Steps = [new Step(1, "Nấu.", minutes, 0, ingredients)],
    };

    public static Dish[] Catalog() =>
    [
        D("canh-chua-ca", DishSlot.Soup, "ca", CookingMethod.Simmer, ["ca-loc", "ca-chua"], SpicyLevel.Optional, ["summer"], [Region.South]),
        D("canh-bi-do", DishSlot.Soup, ProteinSources.None, CookingMethod.Simmer, ["bi-do"]),
        D("canh-rau-thit-bam", DishSlot.Soup, "heo", CookingMethod.Simmer, ["rau-muong", "thit-heo"]),
        D("canh-khoai-mo-tom", DishSlot.Soup, "tom", CookingMethod.Simmer, ["tom", "cai"], seasons: ["winter"], regions: [Region.North]),
        D("canh-bi-xanh", DishSlot.Soup, ProteinSources.None, CookingMethod.Simmer, ["bi-do", "cai"]),
        D("canh-dau-ca-chua", DishSlot.Soup, ProteinSources.Vegetarian, CookingMethod.Simmer, ["dau-phu", "ca-chua"]),
        D("canh-cai-thit-bo", DishSlot.Soup, "bo", CookingMethod.Simmer, ["cai", "thit-bo"]),
        D("canh-trung-ca-chua", DishSlot.Soup, "trung", CookingMethod.Simmer, ["trung", "ca-chua"], regions: [Region.Central]),

        D("thit-kho-trung", DishSlot.Main, "heo", CookingMethod.Braise, ["thit-heo", "trung"], minutes: 50),
        D("ca-kho-to", DishSlot.Main, "ca", CookingMethod.Braise, ["ca-loc"], regions: [Region.South], minutes: 45),
        D("ga-ran", DishSlot.Main, "ga", CookingMethod.Fry, ["ga"], minutes: 30),
        D("thit-bo-xao", DishSlot.Main, "bo", CookingMethod.StirFry, ["thit-bo", "cai"], minutes: 15),
        D("tom-rang", DishSlot.Main, "tom", CookingMethod.Fry, ["tom"], minutes: 20),
        D("dau-phu-sot-ca", DishSlot.Main, ProteinSources.Vegetarian, CookingMethod.Simmer, ["dau-phu", "ca-chua"], minutes: 25),
        D("ca-chien", DishSlot.Main, "ca", CookingMethod.Fry, ["ca-loc"], minutes: 25),
        D("suon-nuong", DishSlot.Main, "heo", CookingMethod.Grill, ["thit-heo"], seasons: ["autumn"], minutes: 40),
        D("ga-kho-gung", DishSlot.Main, "ga", CookingMethod.Braise, ["ga"], minutes: 35),
        D("muc-xao-cay", DishSlot.Main, "muc", CookingMethod.StirFry, ["muc", "ot"], SpicyLevel.Required, minutes: 15),

        D("rau-muong-xao-toi", DishSlot.Vegetable, ProteinSources.None, CookingMethod.StirFry, ["rau-muong"], minutes: 10),
        D("cai-luoc", DishSlot.Vegetable, ProteinSources.None, CookingMethod.Boil, ["cai"], minutes: 8),
        D("bi-xao-trung", DishSlot.Vegetable, "trung", CookingMethod.StirFry, ["bi-do", "trung"], minutes: 12),
        D("ca-chua-tron", DishSlot.Vegetable, ProteinSources.None, CookingMethod.Toss, ["ca-chua"], minutes: 5),
        D("rau-cai-xao-ot", DishSlot.Vegetable, ProteinSources.None, CookingMethod.StirFry, ["cai", "ot"], SpicyLevel.Required, minutes: 10),
        D("dau-phu-chien", DishSlot.Vegetable, ProteinSources.Vegetarian, CookingMethod.Fry, ["dau-phu"], minutes: 15),
    ];

    public static Member Person(string name = "A", params MemberTag[] tags) => TestData.Person(name, AgeGroup.Adult, 2, tags);

    /// <summary>Five adults at two bowls make exactly 5.0 base servings, the threshold for a second main.</summary>
    public static Household BigHome(Region region = Region.South, int? availableMinutes = null) => new(
        region,
        BowlSize.Medium,
        Enumerable.Range(0, 5).Select(i => Person($"P{i}")).ToArray(),
        availableMinutes);

    public static Household SmallHome(Region region = Region.South, int? availableMinutes = null, params Member[] members) =>
        new(region, BowlSize.Medium, members.Length > 0 ? members : [Person("A"), Person("B")], availableMinutes);

    public static SpinContext Context(
        Household household,
        IReadOnlyList<Dish>? dishes = null,
        IEnumerable<string>? blocked = null,
        IReadOnlyDictionary<string, DateOnly>? lastEaten = null,
        IReadOnlyDictionary<string, DishPreference>? preferences = null) => new(
        household,
        dishes ?? Catalog(),
        Ingredients(),
        new HashSet<string>(blocked ?? [], StringComparer.Ordinal),
        lastEaten ?? new Dictionary<string, DateOnly>(),
        preferences ?? new Dictionary<string, DishPreference>());

    public static MealSpinner Spinner(
        IRandomSource? random = null,
        SpinnerConfig? config = null,
        DateOnly? today = null,
        IMealTimeEstimator? estimator = null) => new(
        config ?? Config,
        PortionSettings,
        random ?? new SeededRandomSource(1),
        new FixedClock(today ?? Today),
        estimator ?? new SumOfStepsEstimator());

    public static Dish Find(string id) => Catalog().Single(d => d.Id == id);

    public static IEnumerable<string> Ids(SpinResult result) => result.Tray.Slots.Select(s => s.DishId);

    public static IEnumerable<Dish> Dishes(SpinResult result) =>
        result.Tray.Slots.Select(s => Find(s.DishId));
}
