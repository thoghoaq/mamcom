using MamCom.Core.Models;
using MamCom.Core.Spinning;
using static MamCom.Core.Tests.Spinning.SpinnerFixtures;

namespace MamCom.Core.Tests.Spinning;

/// <summary>
/// PLAN 1.6: across ten thousand seeds, each building its own household, avoid lists, blocked dishes, eating history,
/// ratings, time limit and date, the spinner never breaks a hard rule, never moves a lock, and says honestly which
/// tray rules the delivered tray breaks. The checks below are written independently of the spinner's code.
/// </summary>
public class MealSpinnerPropertyTests
{
    private static readonly string[] Avoidable = ["bo", "hai-san", "dau-phu", "ca-chua", "trung", "ot", "thit-heo", "tom"];

    private sealed record Scenario(SpinContext Context, DateOnly Today, SeededRandomSource Random);

    private static Scenario BuildScenario(ulong seed)
    {
        var r = new SeededRandomSource(seed * 7919 + 13);
        var region = Enum.GetValues<Region>()[r.NextInt(3)];
        var members = Enumerable.Range(0, 1 + r.NextInt(7)).Select(i =>
        {
            var avoid = Enumerable.Range(0, r.NextInt(3)).Select(_ => Avoidable[r.NextInt(Avoidable.Length)]).Distinct().ToArray();
            var tags = r.NextInt(5) == 0 ? new[] { MemberTag.NoSpicy } : [];
            return new Member($"P{i}", Enum.GetValues<AgeGroup>()[r.NextInt(5)], 0.5 * (1 + r.NextInt(10)), tags, avoid);
        }).ToArray();
        var minutes = r.NextInt(2) == 0 ? (int?)null : 15 + r.NextInt(106);
        var household = new Household(region, BowlSize.Medium, members, minutes);

        var catalog = Catalog();
        var today = new DateOnly(2026, 1 + r.NextInt(12), 1 + r.NextInt(28));
        var blocked = catalog.Where(_ => r.NextInt(10) == 0).Select(d => d.Id).ToArray();
        var eaten = catalog.Where(_ => r.NextInt(3) == 0).ToDictionary(d => d.Id, _ => today.AddDays(-r.NextInt(11)));
        var prefs = catalog.Where(_ => r.NextInt(4) == 0).ToDictionary(d => d.Id, _ => r.NextInt(2) == 0 ? DishPreference.Liked : DishPreference.Disliked);

        return new Scenario(Context(household, catalog, blocked, eaten, prefs), today, new SeededRandomSource(seed));
    }

    // ---- independent oracles -----------------------------------------------------------------

    private static bool EligibleByTheSpec(Dish dish, SpinContext context, DateOnly today)
    {
        var members = context.Household.Members;
        if (context.BlockedDishIds.Contains(dish.Id))
        {
            return false;
        }

        if (context.LastEatenOn.TryGetValue(dish.Id, out var last) && today.DayNumber - last.DayNumber <= 2)
        {
            return false;
        }

        if (dish.Spicy == SpicyLevel.Required && members.Any(m => m.Tags.Contains(MemberTag.NoSpicy)))
        {
            return false;
        }

        foreach (var line in dish.Ingredients)
        {
            var ingredient = context.Ingredients[line.IngredientId];
            if (members.Any(m => m.Avoid.Contains(ingredient.Id) || m.Avoid.Contains(ingredient.Category)))
            {
                return false;
            }
        }

        return true;
    }

    private static bool BreaksFried(IReadOnlyList<Dish> dishes) => dishes.Count(d => d.CookingMethod == CookingMethod.Fry) > 1;

    private static bool BreaksProtein(IReadOnlyList<Dish> dishes) => dishes
        .Select(d => d.ProteinSource)
        .Where(p => p != ProteinSources.None && p != ProteinSources.Vegetarian)
        .GroupBy(p => p)
        .Any(g => g.Count() > 1);

    private static bool BreaksMinutes(IReadOnlyList<Dish> dishes, int? limit) =>
        limit is { } l && new SumOfStepsEstimator().EstimateMealMinutes(dishes) > l;

    private static DishSlot[] ExpectedLayout(SpinContext context)
    {
        var baseServings = new MamCom.Core.Portions.PortionCalculator(PortionSettings).Calculate(context.Household).BaseServings;
        return baseServings >= 5
            ? [DishSlot.Soup, DishSlot.Main, DishSlot.Main, DishSlot.Vegetable]
            : [DishSlot.Soup, DishSlot.Main, DishSlot.Vegetable];
    }

    private static void CheckResult(SpinResult result, Scenario scenario, MealTray? current, string where)
    {
        var context = scenario.Context;
        var dishes = result.Tray.Slots.Select(s => Find(s.DishId)).ToArray();
        var layout = ExpectedLayout(context);

        // Shape: the slots in layout order with the unfilled ones missing, no dish twice, today's date.
        var kinds = result.Tray.Slots.Select(s => s.Slot).Concat(result.UnfilledSlots).Order().ToArray();
        kinds.ShouldBe(layout.Order().ToArray(), where);
        result.Tray.Slots.Select(s => s.DishId).Distinct().Count().ShouldBe(result.Tray.Slots.Count, where);
        result.Tray.Date.ShouldBe(scenario.Today, where);
        foreach (var kind in Enum.GetValues<DishSlot>())
        {
            result.Tray.Slots.Where(s => s.Slot == kind).ShouldAllBe(s => Find(s.DishId).Slot == kind, where);
        }

        // Locks: matched by kind and position, unchanged, still flagged.
        var lockedIds = new HashSet<string>();
        foreach (var kind in Enum.GetValues<DishSlot>())
        {
            var before = current?.Slots.Where(s => s.Slot == kind).ToArray() ?? [];
            var after = result.Tray.Slots.Where(s => s.Slot == kind).ToArray();
            var layoutCount = layout.Count(k => k == kind);
            for (var position = 0; position < Math.Min(before.Length, layoutCount); position++)
            {
                if (before[position].IsLocked)
                {
                    after[position].ShouldBe(before[position], where);
                    lockedIds.Add(before[position].DishId);
                }
            }
        }

        // Hard rules for every dish that was drawn, not for locked ones.
        foreach (var slot in result.Tray.Slots.Where(s => !s.IsLocked))
        {
            EligibleByTheSpec(Find(slot.DishId), context, scenario.Today).ShouldBeTrue($"{where}: {slot.DishId} breaks a hard rule");
        }

        // Unfilled slots are exactly the shortage of eligible dishes.
        foreach (var kind in Enum.GetValues<DishSlot>())
        {
            var needed = layout.Count(k => k == kind);
            var lockedOfKind = result.Tray.Slots.Count(s => s.Slot == kind && s.IsLocked);
            var pool = context.Dishes.Count(d => d.Slot == kind && !lockedIds.Contains(d.Id) && EligibleByTheSpec(d, context, scenario.Today));
            var expectedUnfilled = Math.Max(0, needed - lockedOfKind - pool);
            result.UnfilledSlots.Count(k => k == kind).ShouldBe(expectedUnfilled, $"{where}: unfilled {kind}");
        }

        // Relaxed rules: in order, none twice, and exactly the broken tray rules plus the weights if dropped.
        var relaxed = result.RelaxedRules.Select(r => (int)r).ToArray();
        relaxed.ShouldBe(relaxed.Distinct().Order().ToArray(), where);

        // A tray whose every dish is locked is not drawn at all, so nothing is relaxed whatever the locks break.
        var nothingDrawn = result.UnfilledSlots.Count == 0 && result.Tray.Slots.All(s => s.IsLocked);
        if (nothingDrawn)
        {
            result.RelaxedRules.ShouldBeEmpty(where);
            return;
        }

        var limit = context.Household.AvailableMinutes;
        result.RelaxedRules.Contains(RelaxedRule.SingleFriedDish).ShouldBe(BreaksFried(dishes), $"{where}: fried");
        result.RelaxedRules.Contains(RelaxedRule.DistinctProteinSources).ShouldBe(BreaksProtein(dishes), $"{where}: protein");
        result.RelaxedRules.Contains(RelaxedRule.AvailableMinutes).ShouldBe(BreaksMinutes(dishes, limit), $"{where}: minutes");
    }

    // ---- the properties ----------------------------------------------------------------------

    [Fact]
    public void Ten_thousand_seeds_never_break_a_hard_rule_a_lock_or_the_tray_rule_report()
    {
        for (ulong seed = 0; seed < 10_000; seed++)
        {
            var scenario = BuildScenario(seed);
            var spinner = new MealSpinner(Config, PortionSettings, scenario.Random, new FixedClock(scenario.Today), new SumOfStepsEstimator());

            var first = spinner.Spin(scenario.Context);
            CheckResult(first, scenario, null, $"seed {seed} first spin");

            // Lock a pseudo-random subset of the slots and spin again.
            var pick = new SeededRandomSource(seed + 1_000_000);
            var withLocks = first.Tray with
            {
                Slots = first.Tray.Slots.Select(s => s with { IsLocked = pick.NextInt(2) == 0 }).ToArray(),
            };
            var second = spinner.Spin(scenario.Context, withLocks);
            CheckResult(second, scenario, withLocks, $"seed {seed} second spin with locks");

            // Swap one unlocked slot, if there is one: the others do not move.
            var swappable = withLocks.Slots.Select((s, i) => (s, i)).Where(x => !x.s.IsLocked).Select(x => x.i).ToArray();
            if (swappable.Length > 0)
            {
                var index = swappable[pick.NextInt(swappable.Length)];
                var swapped = spinner.Reroll(scenario.Context, withLocks, index);
                for (var i = 0; i < withLocks.Slots.Count; i++)
                {
                    if (i != index && i < swapped.Tray.Slots.Count)
                    {
                        swapped.Tray.Slots[i].ShouldBe(withLocks.Slots[i], $"seed {seed}: reroll moved slot {i}");
                    }
                }

                swapped.Tray.Slots.Select(s => s.DishId).Distinct().Count().ShouldBe(swapped.Tray.Slots.Count, $"seed {seed} reroll duplicate");
            }
        }
    }

    [Fact]
    public void Ten_thousand_seeds_give_the_same_result_when_run_twice()
    {
        for (ulong seed = 0; seed < 10_000; seed++)
        {
            var scenario = BuildScenario(seed);

            var a = new MealSpinner(Config, PortionSettings, new SeededRandomSource(seed), new FixedClock(scenario.Today), new SumOfStepsEstimator()).Spin(scenario.Context);
            var b = new MealSpinner(Config, PortionSettings, new SeededRandomSource(seed), new FixedClock(scenario.Today), new SumOfStepsEstimator()).Spin(scenario.Context);

            b.Tray.Slots.ShouldBe(a.Tray.Slots, $"seed {seed}");
            b.RelaxedRules.ShouldBe(a.RelaxedRules, $"seed {seed}");
            b.UnfilledSlots.ShouldBe(a.UnfilledSlots, $"seed {seed}");
        }
    }

    [Fact]
    public void The_scenarios_actually_exercise_relaxation_locks_and_shortages()
    {
        // Guards the guard: if the generator stopped producing hard cases the properties above would prove little.
        var relaxedKinds = new HashSet<RelaxedRule>();
        var unfilled = 0;
        var twoMains = 0;
        var withTimeLimit = 0;

        for (ulong seed = 0; seed < 2_000; seed++)
        {
            var scenario = BuildScenario(seed);
            var result = new MealSpinner(Config, PortionSettings, new SeededRandomSource(seed), new FixedClock(scenario.Today), new SumOfStepsEstimator()).Spin(scenario.Context);

            foreach (var rule in result.RelaxedRules)
            {
                relaxedKinds.Add(rule);
            }

            unfilled += result.UnfilledSlots.Count;
            twoMains += result.Tray.Slots.Count(s => s.Slot == DishSlot.Main) == 2 ? 1 : 0;
            withTimeLimit += scenario.Context.Household.AvailableMinutes is null ? 0 : 1;
        }

        relaxedKinds.ShouldContain(RelaxedRule.AvailableMinutes);
        relaxedKinds.ShouldContain(RelaxedRule.SingleFriedDish);
        unfilled.ShouldBeGreaterThan(0);
        twoMains.ShouldBeGreaterThan(100);
        withTimeLimit.ShouldBeGreaterThan(500);
    }
}
