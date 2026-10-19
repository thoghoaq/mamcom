using MamCom.Core.Cooking;
using MamCom.Core.Models;
using MamCom.Core.Portions;

namespace MamCom.Core.Spinning;

/// <summary>
/// Draws a meal tray (SPEC §5): hard rules first, then a weighted random pick per slot, then tray rules that are
/// relaxed one by one, in the order of <see cref="RelaxedRule"/>, when no draw satisfies them. The same seed and the
/// same input always give the same tray.
/// </summary>
public sealed class MealSpinner
{
    // Declared in relaxation order, see RelaxedRule.
    private static readonly RelaxedRule[] RelaxationOrder = Enum.GetValues<RelaxedRule>();

    private readonly SpinnerConfig _config;
    private readonly PortionCalculator _portions;
    private readonly IRandomSource _random;
    private readonly IClock _clock;
    private readonly IMealTimeEstimator _mealTime;

    /// <exception cref="ArgumentException">A config has a value the algorithm cannot use.</exception>
    public MealSpinner(
        SpinnerConfig config,
        PortionConfig portions,
        IRandomSource random,
        IClock clock,
        IMealTimeEstimator mealTime)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(portions);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(mealTime);

        config.Validate();
        _config = config;
        _portions = new PortionCalculator(portions);
        _random = random;
        _clock = clock;
        _mealTime = mealTime;
    }

    /// <summary>
    /// Fills every slot that is not locked. The slots are soup, main, vegetable, and a second main when the household
    /// needs at least <see cref="SpinnerConfig.SecondMainMinBaseServings"/> base servings. Locks are matched from
    /// <paramref name="current"/> by dish kind and position; a locked dish that is no longer in the catalog is drawn again.
    /// </summary>
    /// <exception cref="InvalidOperationException">A dish uses an ingredient that is not in the ingredient list.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A member's bowls per meal is outside the range of <see cref="PortionConfig"/>.</exception>
    public SpinResult Spin(SpinContext context, MealTray? current = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var state = new SpinState(this, context);
        var layout = LayoutFor(_portions.Calculate(context.Household).BaseServings);
        var seen = new Dictionary<DishSlot, int>();

        var plan = layout.Select(kind =>
        {
            var position = seen.GetValueOrDefault(kind);
            seen[kind] = position + 1;

            var existing = current?.Slots.Where(s => s.Slot == kind).Skip(position).FirstOrDefault();
            return existing is { IsLocked: true } && state.FindDish(existing.DishId) is { } locked
                ? new SlotPlan(kind, locked, IsLocked: true, Spin: false, Previous: null)
                : new SlotPlan(kind, Fixed: null, IsLocked: false, Spin: true, Previous: null);
        }).ToArray();

        return Run(state, plan);
    }

    /// <summary>
    /// Swaps one dish: only <paramref name="slotIndex"/> is drawn again, under the same rules, and every other slot
    /// stays as it is. The dish being replaced is not offered again unless it is the only one left.
    /// </summary>
    /// <param name="slotIndex">Index into <see cref="MealTray.Slots"/> of <paramref name="current"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="slotIndex"/> is not a slot of the tray.</exception>
    /// <exception cref="ArgumentException">The slot is locked.</exception>
    /// <exception cref="InvalidOperationException">A dish of the tray is no longer in the catalog, or a recipe uses an unknown ingredient.</exception>
    public SpinResult Reroll(SpinContext context, MealTray current, int slotIndex)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentOutOfRangeException.ThrowIfNegative(slotIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(slotIndex, current.Slots.Count);

        if (current.Slots[slotIndex].IsLocked)
        {
            throw new ArgumentException($"Slot {slotIndex} is locked.", nameof(slotIndex));
        }

        var state = new SpinState(this, context);
        var plan = current.Slots.Select((slot, index) =>
        {
            var dish = state.FindDish(slot.DishId)
                ?? throw new InvalidOperationException($"Dish '{slot.DishId}' of the tray is not in the catalog.");
            return index == slotIndex
                ? new SlotPlan(slot.Slot, Fixed: null, IsLocked: false, Spin: true, Previous: dish)
                : new SlotPlan(slot.Slot, dish, slot.IsLocked, Spin: false, Previous: null);
        }).ToArray();

        return Run(state, plan);
    }

    /// <summary>Whether the hard rules of SPEC §5.2 let <paramref name="dish"/> be drawn for this household today.</summary>
    /// <exception cref="InvalidOperationException">The dish uses an ingredient that is not in the ingredient list.</exception>
    public bool IsEligible(Dish dish, SpinContext context)
    {
        ArgumentNullException.ThrowIfNull(dish);
        ArgumentNullException.ThrowIfNull(context);
        return new SpinState(this, context).IsEligible(dish);
    }

    /// <summary>The soft weight of SPEC §5.4: recency × preference × season × region, 1 when nothing applies.</summary>
    public double WeightOf(Dish dish, SpinContext context)
    {
        ArgumentNullException.ThrowIfNull(dish);
        ArgumentNullException.ThrowIfNull(context);

        var state = new SpinState(this, context);
        return state.BaseWeight(dish) * state.SeasonRegionFactor(dish);
    }

    // Servings are summed in member order, so exactly 5 can come out as 4.999999999999999; round off that noise.
    private DishSlot[] LayoutFor(double baseServings) =>
        Math.Round(baseServings, 9) >= _config.SecondMainMinBaseServings
            ? [DishSlot.Soup, DishSlot.Main, DishSlot.Main, DishSlot.Vegetable]
            : [DishSlot.Soup, DishSlot.Main, DishSlot.Vegetable];

    private SpinResult Run(SpinState state, SlotPlan[] plan)
    {
        var fixedIds = plan.Where(p => p.Fixed is not null).Select(p => p.Fixed!.Id).ToHashSet(StringComparer.Ordinal);
        var pools = new List<Candidate>?[plan.Length];
        var byKind = new Dictionary<DishSlot, List<Candidate>>();

        for (var i = 0; i < plan.Length; i++)
        {
            if (!plan[i].Spin)
            {
                continue;
            }

            if (!byKind.TryGetValue(plan[i].Kind, out var kindPool))
            {
                kindPool = byKind[plan[i].Kind] = state.Candidates(plan[i].Kind);
            }

            var pool = kindPool.Where(c => !fixedIds.Contains(c.Dish.Id)).ToList();
            if (plan[i].Previous is { } replaced)
            {
                var others = pool.Where(c => c.Dish.Id != replaced.Id).ToList();
                if (others.Count > 0)
                {
                    pool = others;
                }
            }

            pools[i] = pool;
        }

        // The slot with the fewest candidates goes first; ties keep the tray order.
        var order = Enumerable.Range(0, plan.Length)
            .Where(i => plan[i].Spin)
            .OrderBy(i => pools[i]!.Count)
            .ThenBy(i => i)
            .ToArray();

        if (order.Length == 0)
        {
            return Result(plan, plan.Select(p => p.Fixed).ToArray(), relaxed: [], state);
        }

        var relaxable = RelaxationOrder.Where(rule => Applies(rule, state, pools)).ToArray();

        // A rule the fixed dishes already break on their own cannot be met by drawing more dishes, so it is given up
        // from the start instead of costing the rules before it in the order.
        var fixedDishes = plan.Select(p => p.Fixed).OfType<Dish>().ToArray();
        var relaxed = relaxable.Where(rule => Violates(rule, fixedDishes, state)).ToHashSet();
        var toRelaxInTurn = relaxable.Where(rule => !relaxed.Contains(rule)).ToArray();
        Dish?[] drawn = [];

        for (var level = 0; ; level++)
        {
            var weightsOn = !relaxed.Contains(RelaxedRule.SeasonAndRegionWeights);
            var enforced = relaxable.Where(rule => !relaxed.Contains(rule)).ToArray();

            for (var attempt = 0; attempt < _config.AttemptsPerLevel; attempt++)
            {
                drawn = Draw(plan, pools, order, weightsOn);
                var dishes = drawn.OfType<Dish>().ToArray();
                if (!enforced.Any(rule => Violates(rule, dishes, state)))
                {
                    return Result(plan, drawn, relaxable.Where(relaxed.Contains).ToArray(), state);
                }
            }

            if (level == toRelaxInTurn.Length)
            {
                return Result(plan, drawn, relaxable.Where(relaxed.Contains).ToArray(), state);
            }

            relaxed.Add(toRelaxInTurn[level]);
        }
    }

    /// <summary>The weights rule is only worth relaxing if some candidate's weight depends on season or region.</summary>
    private static bool Applies(RelaxedRule rule, SpinState state, List<Candidate>?[] pools) => rule switch
    {
        RelaxedRule.SeasonAndRegionWeights => pools.Any(p => p is not null && p.Any(c => c.SeasonRegionFactor != 1)),
        RelaxedRule.AvailableMinutes => state.Context.Household.AvailableMinutes is not null,
        _ => true,
    };

    private Dish?[] Draw(SlotPlan[] plan, List<Candidate>?[] pools, int[] order, bool weightsOn)
    {
        var chosen = plan.Select(p => p.Fixed).ToArray();

        // Fixed dishes are already left out of the pools; this only stops one drawn dish from filling two slots.
        var used = new HashSet<string>(StringComparer.Ordinal);

        foreach (var index in order)
        {
            var available = pools[index]!.Where(c => !used.Contains(c.Dish.Id)).ToList();
            if (available.Count == 0)
            {
                continue;
            }

            var pick = Pick(available, weightsOn);
            chosen[index] = pick.Dish;
            used.Add(pick.Dish.Id);
        }

        return chosen;
    }

    private Candidate Pick(List<Candidate> candidates, bool weightsOn)
    {
        double WeightOf(Candidate c) => c.BaseWeight * (weightsOn ? c.SeasonRegionFactor : 1);

        var target = _random.NextDouble() * candidates.Sum(WeightOf);
        var cumulative = 0.0;
        foreach (var candidate in candidates)
        {
            cumulative += WeightOf(candidate);
            if (target < cumulative)
            {
                return candidate;
            }
        }

        return candidates[^1];
    }

    /// <summary>Whether the tray breaks a rule. The weights are not a check, so they never "violate".</summary>
    private bool Violates(RelaxedRule rule, Dish[] dishes, SpinState state) => rule switch
    {
        RelaxedRule.AvailableMinutes =>
            state.Context.Household.AvailableMinutes is { } limit && _mealTime.EstimateMealMinutes(dishes) > limit,
        RelaxedRule.SingleFriedDish =>
            dishes.Count(d => d.CookingMethod == CookingMethod.Fry) > _config.MaxFriedDishes,
        RelaxedRule.DistinctProteinSources =>
            dishes.Where(d => !string.IsNullOrWhiteSpace(d.ProteinSource)
                    && d.ProteinSource != ProteinSources.None
                    && d.ProteinSource != ProteinSources.Vegetarian)
                .GroupBy(d => d.ProteinSource, StringComparer.Ordinal)
                .Any(group => group.Count() > 1),
        _ => false,
    };

    private SpinResult Result(SlotPlan[] plan, Dish?[] drawn, RelaxedRule[] relaxed, SpinState state)
    {
        var dishes = drawn.OfType<Dish>().ToArray();

        // Report what the delivered tray really breaks, so the message to the user is true.
        var reported = relaxed
            .Where(rule => rule == RelaxedRule.SeasonAndRegionWeights || Violates(rule, dishes, state))
            .ToArray();

        var slots = new List<SlotResult>();
        var unfilled = new List<DishSlot>();
        for (var i = 0; i < plan.Length; i++)
        {
            if (drawn[i] is { } dish)
            {
                slots.Add(new SlotResult(plan[i].Kind, dish.Id, plan[i].IsLocked));
            }
            else
            {
                unfilled.Add(plan[i].Kind);
            }
        }

        return new SpinResult(new MealTray(state.Today, slots), reported, unfilled);
    }

    private sealed record SlotPlan(DishSlot Kind, Dish? Fixed, bool IsLocked, bool Spin, Dish? Previous);

    private sealed record Candidate(Dish Dish, double BaseWeight, double SeasonRegionFactor);

    /// <summary>The per-call view of the context: today, season, avoid set, and the rule and weight lookups.</summary>
    private sealed class SpinState
    {
        private readonly MealSpinner _owner;
        private readonly Dictionary<string, Dish> _byId;
        private readonly HashSet<string> _avoided;
        private readonly bool _noSpicy;
        private readonly DateOnly _today;
        private readonly string _season;

        public SpinState(MealSpinner owner, SpinContext context)
        {
            _owner = owner;
            Context = context;
            _byId = new Dictionary<string, Dish>(StringComparer.Ordinal);
            foreach (var dish in context.Dishes)
            {
                _byId.TryAdd(dish.Id, dish);
            }

            _avoided = context.Household.Members.SelectMany(m => m.Avoid).ToHashSet(StringComparer.Ordinal);
            _noSpicy = context.Household.Members.Any(m => m.Tags.Contains(MemberTag.NoSpicy));
            _today = owner._clock.Today;
            _season = owner._config.SeasonByMonth[_today.Month - 1];
        }

        public SpinContext Context { get; }

        /// <summary>The date read from the clock once, so the rules and the tray's date always agree.</summary>
        public DateOnly Today => _today;

        public Dish? FindDish(string id) => _byId.GetValueOrDefault(id);

        public bool IsEligible(Dish dish) =>
            !Context.BlockedDishIds.Contains(dish.Id)
            && !EatenTooRecently(dish)
            && !(_noSpicy && dish.Spicy == SpicyLevel.Required)
            && !ContainsAvoided(dish);

        public double BaseWeight(Dish dish)
        {
            var recency = _owner._config.Recency;
            var recencyWeight = 1.0;
            if (Context.LastEatenOn.TryGetValue(dish.Id, out var last))
            {
                recencyWeight = DaysSince(last) <= recency.ReducedUntilDays ? recency.ReducedWeight : 1.0;
            }

            var preference = Context.Preferences.GetValueOrDefault(dish.Id, DishPreference.Neutral) switch
            {
                DishPreference.Liked => _owner._config.Preference.Liked,
                DishPreference.Disliked => _owner._config.Preference.Disliked,
                _ => _owner._config.Preference.Neutral,
            };

            return recencyWeight * preference;
        }

        public double SeasonRegionFactor(Dish dish)
        {
            var season = dish.Seasons.Contains(_season) ? _owner._config.SeasonMatchWeight : 1.0;
            var region = dish.Regions.Contains(Context.Household.Region) ? _owner._config.RegionMatchWeight : 1.0;
            return season * region;
        }

        /// <summary>Eligible dishes of one kind, sorted by id so the result does not depend on catalog order.</summary>
        public List<Candidate> Candidates(DishSlot kind) => Context.Dishes
            .Where(d => d.Slot == kind && IsEligible(d))
            .DistinctBy(d => d.Id, StringComparer.Ordinal)
            .OrderBy(d => d.Id, StringComparer.Ordinal)
            .Select(d => new Candidate(d, BaseWeight(d), SeasonRegionFactor(d)))
            .ToList();

        private int DaysSince(DateOnly date) => _today.DayNumber - date.DayNumber;

        private bool EatenTooRecently(Dish dish) =>
            Context.LastEatenOn.TryGetValue(dish.Id, out var last) && DaysSince(last) <= _owner._config.Recency.ExcludeWithinDays;

        private bool ContainsAvoided(Dish dish)
        {
            var avoided = false;
            foreach (var line in dish.Ingredients)
            {
                if (!Context.Ingredients.TryGetValue(line.IngredientId, out var ingredient))
                {
                    throw new InvalidOperationException($"Dish '{dish.Id}' uses unknown ingredient '{line.IngredientId}'.");
                }

                avoided |= _avoided.Contains(ingredient.Id) || _avoided.Contains(ingredient.Category);
            }

            return avoided;
        }
    }
}
