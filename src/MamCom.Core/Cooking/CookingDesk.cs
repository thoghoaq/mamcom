using MamCom.Core.Details;
using MamCom.Core.Models;
using MamCom.Core.Portions;
using MamCom.Core.Scaling;

namespace MamCom.Core.Cooking;

/// <summary>One ingredient named in a step of the cooking mode.</summary>
/// <param name="ShowAmount">True the first time the dish names this ingredient (that step says how much); later steps only name it, so "180 g thịt" is not read as 180 g in each step.</param>
public sealed record CookingIngredient(IngredientLine Line, bool ShowAmount);

/// <summary>One screen of the cooking mode (SPEC §8.1-5): one step of one dish, or of the rice.</summary>
/// <param name="Index">Where the step is in the guide, from 0. Timers are kept by it.</param>
/// <param name="DishName">The dish's name; empty for the rice, which has no data file (<see cref="IsRice"/>).</param>
/// <param name="Slot">The kind of dish, for its colour; null for the rice.</param>
/// <param name="ActiveMinutes">Minutes that need the cook's hands.</param>
/// <param name="TimerMinutes">Minutes that run on their own and get a timer; 0 when the step has none.</param>
public sealed record CookingStep(
    int Index,
    string DishId,
    string DishName,
    DishSlot? Slot,
    string Text,
    int ActiveMinutes,
    int TimerMinutes,
    IReadOnlyList<CookingIngredient> Ingredients)
{
    public bool IsRice => DishId == CookPlan.RiceDishId;
}

/// <summary>The steps of a whole meal in the order one cook does them (<see cref="CookPlanScheduler"/>), each with what it uses scaled to the household.</summary>
public sealed record CookingGuide(IReadOnlyList<CookingStep> Steps, int MealMinutes);

/// <summary>A timer the cook started on a step. It is kept as the minute it ends, not as a count, so it is right after the screen was closed and opened again.</summary>
/// <param name="Alerted">The alarm for it has gone off, so it does not go off again when the screen is opened.</param>
public sealed record CookTimer(int StepIndex, DateTimeOffset EndsAt, bool Alerted);

/// <summary>Where the cooking is (SPEC §8.1-5): the step on the screen and the timers that were started, for the tray they belong to.</summary>
/// <param name="TrayKey">The tray (<see cref="TrayKeys.Of"/>); empty while there is none.</param>
public sealed record CookingState(string TrayKey, int StepIndex, IReadOnlyList<CookTimer> Timers)
{
    public static CookingState Empty { get; } = new(string.Empty, 0, []);

    /// <summary>False for a state that was edited by hand or came from another shape: nothing without a tray, no step before the first, no two timers on one step.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsUsable =>
        TrayKey is not null
        && Timers is not null
        && StepIndex >= 0
        && Timers.All(timer => timer is not null && timer.StepIndex >= 0)
        && Timers.Select(timer => timer.StepIndex).Distinct().Count() == Timers.Count
        && (TrayKey.Length > 0 || (StepIndex == 0 && Timers.Count == 0));
}

/// <summary>What a timer chip says.</summary>
/// <param name="RemainingSeconds">Whole seconds left, rounded up so "0:00" only shows when it is over; 0 when <paramref name="IsDone"/>.</param>
public sealed record TimerView(int StepIndex, string DishId, string DishName, int RemainingSeconds, bool IsDone)
{
    public bool IsRice => DishId == CookPlan.RiceDishId;
}

/// <summary>
/// The cooking mode as functions from one state to the next, as <c>TrayDesk</c> is for the tray: the guide of a meal, the state when the
/// screen opens, moving between steps, starting and cancelling timers, and which timers are over. The clock is a parameter so a test sets it.
/// </summary>
public sealed class CookingDesk
{
    private readonly PortionConfig _portionConfig;
    private readonly PortionCalculator _portions;
    private readonly IngredientScaler _scaler;
    private readonly CookPlanScheduler _scheduler;

    /// <exception cref="ArgumentException">A config has a value the formulas cannot use.</exception>
    public CookingDesk(PortionConfig portions, CookingConfig cooking)
    {
        ArgumentNullException.ThrowIfNull(portions);
        ArgumentNullException.ThrowIfNull(cooking);

        _portionConfig = portions;
        _portions = new PortionCalculator(portions);
        _scaler = new IngredientScaler(portions);
        _scheduler = new CookPlanScheduler(cooking);
    }

    /// <summary>
    /// The guide for the dishes of a tray: the rice first, then the steps as <see cref="CookPlanScheduler"/> orders them for one cook.
    /// </summary>
    /// <exception cref="InvalidOperationException">A dish uses an unknown ingredient, or a whole-unit line has no market unit for the region of the household (a data error).</exception>
    /// <exception cref="ArgumentOutOfRangeException">A member's bowls per meal is outside the configured range.</exception>
    public CookingGuide Guide(Household household, IReadOnlyList<Dish> dishes, IReadOnlyDictionary<string, Ingredient> ingredients)
    {
        ArgumentNullException.ThrowIfNull(household);
        ArgumentNullException.ThrowIfNull(dishes);
        ArgumentNullException.ThrowIfNull(ingredients);

        var portions = _portions.Calculate(household);
        var plan = _scheduler.Schedule(dishes);
        var dishesById = dishes.GroupBy(dish => dish.Id, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var scaledByDish = new Dictionary<string, IReadOnlyDictionary<string, ScaledIngredient>>(StringComparer.Ordinal);
        var named = new HashSet<(string DishId, string IngredientId)>();
        var steps = new List<CookingStep>();

        foreach (var planStep in plan.Steps)
        {
            if (planStep.DishId == CookPlan.RiceDishId)
            {
                steps.Add(new CookingStep(steps.Count, planStep.DishId, string.Empty, null, planStep.Text, planStep.ActiveMinutes, planStep.TimerMinutes ?? 0, RiceLines(household, portions, ingredients)));
                continue;
            }

            var dish = dishesById[planStep.DishId];
            if (!scaledByDish.TryGetValue(dish.Id, out var scaled))
            {
                scaled = _scaler.ScaleDish(dish, ingredients, household.Region, portions.ServingsFor(dish.Slot))
                    .GroupBy(line => line.IngredientId, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
                scaledByDish[dish.Id] = scaled;
            }

            var lines = (planStep.IngredientIds ?? [])
                .Distinct(StringComparer.Ordinal)
                .Where(scaled.ContainsKey)   // an id the recipe does not list has no amount to give (a data error DataBuilder reports)
                .Select(id => new CookingIngredient(
                    new IngredientLine(id, ingredients[id].NameFor(household.Region), scaled[id].GramsEdible, scaled[id].Units, scaled[id].UnitDisplay, scaled[id].Note),
                    named.Add((dish.Id, id))))
                .ToArray();

            steps.Add(new CookingStep(steps.Count, dish.Id, dish.Name, dish.Slot, planStep.Text, planStep.ActiveMinutes, planStep.TimerMinutes ?? 0, lines));
        }

        return new CookingGuide(steps, plan.MealTimeMinutes);
    }

    private IReadOnlyList<CookingIngredient> RiceLines(Household household, PortionResult portions, IReadOnlyDictionary<string, Ingredient> ingredients)
    {
        // The rice is the one step whose amount is not in a recipe: it is what the household eats (SPEC §4.2).
        if (portions.RawRiceGrams <= 0 || !ingredients.TryGetValue(_portionConfig.RiceIngredientId, out var rice))
        {
            return [];
        }

        return [new CookingIngredient(new IngredientLine(rice.Id, rice.NameFor(household.Region), portions.RawRiceGrams, null, null, null), true)];
    }

    /// <summary>
    /// The state to open the screen with: what was saved if it is for this tray (the step kept inside the guide, a timer whose step is gone or has
    /// no timer dropped), else the first step with no timers.
    /// </summary>
    public CookingState Open(CookingState? saved, MealTray tray, CookingGuide guide)
    {
        ArgumentNullException.ThrowIfNull(tray);
        ArgumentNullException.ThrowIfNull(guide);

        var key = TrayKeys.Of(tray);
        if (saved is not { IsUsable: true } || saved.TrayKey != key)
        {
            return new CookingState(key, 0, []);
        }

        return saved with
        {
            StepIndex = Clamp(saved.StepIndex, guide),
            Timers = [.. saved.Timers.Where(timer => timer.StepIndex < guide.Steps.Count && guide.Steps[timer.StepIndex].TimerMinutes > 0)],
        };
    }

    /// <summary>Goes to a step; one that is not in the guide is the nearest one that is.</summary>
    public CookingState GoTo(CookingState state, CookingGuide guide, int stepIndex)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(guide);

        return state with { StepIndex = Clamp(stepIndex, guide) };
    }

    public CookingState Next(CookingState state, CookingGuide guide) => GoTo(state, guide, (state ?? throw new ArgumentNullException(nameof(state))).StepIndex + 1);

    public CookingState Previous(CookingState state, CookingGuide guide) => GoTo(state, guide, (state ?? throw new ArgumentNullException(nameof(state))).StepIndex - 1);

    /// <summary>Starts the timer of a step, to end <c>TimerMinutes</c> from <paramref name="now"/>. A step with no timer, and one whose timer already runs (or has run out and was not dismissed), is left as it is.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The step is not in the guide.</exception>
    public CookingState StartTimer(CookingState state, CookingGuide guide, int stepIndex, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(guide);
        ArgumentOutOfRangeException.ThrowIfNegative(stepIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(stepIndex, guide.Steps.Count);

        var minutes = guide.Steps[stepIndex].TimerMinutes;
        if (minutes <= 0 || state.Timers.Any(timer => timer.StepIndex == stepIndex))
        {
            return state;
        }

        return state with { Timers = [.. state.Timers, new CookTimer(stepIndex, now.AddMinutes(minutes), Alerted: false)] };
    }

    /// <summary>Takes a timer away: one that is running is cancelled, one that has run out is dismissed. Nothing happens when the step has none.</summary>
    public CookingState CancelTimer(CookingState state, int stepIndex)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state with { Timers = [.. state.Timers.Where(timer => timer.StepIndex != stepIndex)] };
    }

    /// <summary>The timers for the chips, the soonest to end first (ties by step).</summary>
    public IReadOnlyList<TimerView> Timers(CookingState state, CookingGuide guide, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(guide);

        return
        [
            .. state.Timers
                .Where(timer => timer.StepIndex < guide.Steps.Count)
                .OrderBy(timer => timer.EndsAt)
                .ThenBy(timer => timer.StepIndex)
                .Select(timer =>
                {
                    var step = guide.Steps[timer.StepIndex];
                    var done = now >= timer.EndsAt;
                    return new TimerView(timer.StepIndex, step.DishId, step.DishName, done ? 0 : (int)Math.Ceiling((timer.EndsAt - now).TotalSeconds), done);
                }),
        ];
    }

    /// <summary>The steps whose timers have run out and have not sounded yet, in the order they ended.</summary>
    public IReadOnlyList<int> Due(CookingState state, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(state);

        return [.. state.Timers.Where(timer => !timer.Alerted && now >= timer.EndsAt).OrderBy(timer => timer.EndsAt).ThenBy(timer => timer.StepIndex).Select(timer => timer.StepIndex)];
    }

    /// <summary>Notes that the alarm sounded for these steps, so it does not sound again.</summary>
    public CookingState MarkAlerted(CookingState state, IEnumerable<int> stepIndexes)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(stepIndexes);

        var sounded = stepIndexes.ToHashSet();
        return state with { Timers = [.. state.Timers.Select(timer => sounded.Contains(timer.StepIndex) ? timer with { Alerted = true } : timer)] };
    }

    private static int Clamp(int stepIndex, CookingGuide guide) => guide.Steps.Count == 0 ? 0 : Math.Clamp(stepIndex, 0, guide.Steps.Count - 1);
}
