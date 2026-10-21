using MamCom.Core.Cooking;
using MamCom.Core.Data;
using MamCom.Core.Models;
using MamCom.Core.Taste;

namespace MamCom.Core.Spinning;

/// <summary>
/// Everything the "Mâm hôm nay" screen needs, built once from the data bundle: the desk that runs the session, the builder of the
/// facts under the cards, the catalog by id, and the context the spinner is given.
/// </summary>
public sealed class TrayEngine
{
    private readonly DataBundle _bundle;
    private readonly IClock _clock;

    private TrayEngine(DataBundle bundle, IClock clock, TrayDesk desk, TraySummaryBuilder summary)
    {
        _bundle = bundle;
        _clock = clock;
        Desk = desk;
        Summary = summary;
        DishesById = bundle.Dishes.ToDictionary(d => d.Id, StringComparer.Ordinal);
        IngredientsById = bundle.Ingredients.ToDictionary(i => i.Id, StringComparer.Ordinal);
    }

    public TrayDesk Desk { get; }

    public TraySummaryBuilder Summary { get; }

    public IReadOnlyDictionary<string, Dish> DishesById { get; }

    public IReadOnlyDictionary<string, Ingredient> IngredientsById { get; }

    public QuickFilterConfig QuickFilters => _bundle.Config.Spinner.QuickFilters;

    /// <exception cref="ArgumentException">A config of the bundle has a value the algorithms cannot use.</exception>
    public static TrayEngine Create(DataBundle bundle, IRandomSource random, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(clock);

        var config = bundle.Config;
        var scheduler = new CookPlanScheduler(config.Cooking);
        var spinner = new MealSpinner(config.Spinner, config.Portion, random, clock, scheduler);

        return new TrayEngine(
            bundle,
            clock,
            new TrayDesk(spinner, config.Spinner, clock),
            new TraySummaryBuilder(config.Portion, config.Nutrition, scheduler));
    }

    /// <summary>
    /// The context for a spin today: the catalog of the bundle, what this household has eaten (read as of today), and what it said about
    /// dishes: the ones it blocked are never drawn, the ones it liked or did not like weigh more or less (SPEC §5.4).
    /// </summary>
    /// <param name="taste">Null when nothing was said yet.</param>
    public SpinContext ContextFor(Household household, EatenLog eaten, TasteProfile? taste = null)
    {
        ArgumentNullException.ThrowIfNull(household);
        ArgumentNullException.ThrowIfNull(eaten);

        taste ??= TasteProfile.Empty;
        return new SpinContext(household, _bundle.Dishes, IngredientsById, TasteDesk.BlockedSet(taste), eaten.Until(_clock.Today), taste.Preferences, _bundle.Config.Cooking.StovesOf(household));
    }

    /// <summary>The session of an earlier visit can go on today: it is for today and every dish on its tray is still in the catalog.</summary>
    public bool CanContinue(TraySession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        return session.IsUsable
            && session.Viewed is { } viewed
            && viewed.Tray.Date == _clock.Today
            && viewed.Tray.Slots.All(slot => DishesById.ContainsKey(slot.DishId));
    }

    /// <summary>The dishes of a tray in the order of its slots; a dish that is not in the catalog is left out.</summary>
    public IReadOnlyList<Dish> DishesOf(MealTray tray)
    {
        ArgumentNullException.ThrowIfNull(tray);

        return [.. tray.Slots.Select(slot => DishesById.GetValueOrDefault(slot.DishId)).OfType<Dish>()];
    }
}
