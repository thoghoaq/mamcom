using MamCom.Core.Models;

namespace MamCom.Core.Cooking;

/// <summary>
/// Plans the cooking of a whole meal for ONE cook (SPEC §5.6). Hands-on (active) time never overlaps with other
/// hands-on time; unattended (passive) time runs alongside anything. Within a dish each step starts only after the
/// previous step, passive part included, is done.
/// </summary>
/// <remarks>
/// Rice starts at minute 0. The other dishes are placed first, longest total time first, and the cook picks up the
/// highest-priority step that is ready whenever the hands are free. Dishes marked <c>serveHot</c> are placed last, as
/// late as possible and back to back, longest first, so the shortest one finishes at the very end.
/// </remarks>
public sealed class CookPlanScheduler : IMealTimeEstimator
{
    private readonly CookingConfig _config;

    /// <exception cref="ArgumentException">The config has a value the scheduler cannot use.</exception>
    public CookPlanScheduler(CookingConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.Validate();
        _config = config;
    }

    public int EstimateMealMinutes(IReadOnlyList<Dish> dishes) => Schedule(dishes).MealTimeMinutes;

    /// <summary>
    /// The largest of: the rice, the longest dish (its steps run one after another), and all hands-on time added up
    /// (one cook can do one thing at a time). Every plan this scheduler makes is at least this long.
    /// </summary>
    public int MinimumMealMinutes(IReadOnlyList<Dish> dishes)
    {
        ArgumentNullException.ThrowIfNull(dishes);

        var riceTotal = _config.RiceActiveMinutes + _config.RicePassiveMinutes;
        var longestDish = dishes.Select(dish => new Job(dish).TotalMinutes).DefaultIfEmpty(0).Max();
        var allHandsOn = _config.RiceActiveMinutes + dishes.Sum(dish => dish.Steps.Sum(step => step.ActiveMinutes));

        return Math.Max(riceTotal, Math.Max(longestDish, allHandsOn));
    }

    /// <summary>
    /// The plan for the dishes plus rice, steps in start order (rice first). The result does not depend on the order
    /// of <paramref name="dishes"/>; ties are broken by dish id.
    /// </summary>
    /// <exception cref="ArgumentException">A step has negative minutes.</exception>
    public CookPlan Schedule(IReadOnlyList<Dish> dishes)
    {
        ArgumentNullException.ThrowIfNull(dishes);

        var timeline = new Timeline();
        timeline.Place(
            CookPlan.RiceDishId,
            new Step(1, _config.RiceStepText, _config.RiceActiveMinutes, _config.RicePassiveMinutes, []),
            start: 0);

        var jobs = dishes.Select(dish => new Job(dish)).Where(job => job.Steps.Length > 0).ToList();
        var cold = Prioritised(jobs.Where(job => !job.Dish.ServeHot));
        var hot = Prioritised(jobs.Where(job => job.Dish.ServeHot));

        ScheduleCold(timeline, cold);
        ScheduleHot(timeline, hot);

        return new CookPlan(timeline.InStartOrder(), timeline.EndMinute);
    }

    /// <summary>Longest total time first; equal times go in id order so the plan never depends on the input order.</summary>
    private static List<Job> Prioritised(IEnumerable<Job> jobs) =>
        jobs.OrderByDescending(job => job.TotalMinutes).ThenBy(job => job.Dish.Id, StringComparer.Ordinal).ToList();

    private static void ScheduleCold(Timeline timeline, List<Job> jobs)
    {
        foreach (var job in jobs)
        {
            job.PlacePassiveOnlySteps(timeline);
        }

        // The cook starts on the first free minute after the rice is washed.
        var cookFreeAt = timeline.HandsBusyUntil;
        while (jobs.Any(job => job.Current is not null))
        {
            var next = jobs.FirstOrDefault(job => job.Current is not null && job.ReadyAt <= cookFreeAt);
            if (next is null)
            {
                // Nothing is ready: the cook waits for the earliest step that will be.
                cookFreeAt = jobs.Where(job => job.Current is not null).Min(job => job.ReadyAt);
                continue;
            }

            var step = next.Current!;
            var placed = timeline.Place(next.Dish.Id, step, cookFreeAt);
            cookFreeAt = placed.StartMinute + step.ActiveMinutes;
            next.Advance(placed);
            next.PlacePassiveOnlySteps(timeline);
        }
    }

    private static void ScheduleHot(Timeline timeline, List<Job> jobs)
    {
        foreach (var job in jobs)
        {
            // Aim to finish exactly when the meal ends, then let the hands decide.
            job.ReadyAt = Math.Max(0, timeline.EndMinute - job.TotalMinutes);

            while (job.Current is { } step)
            {
                var start = step.ActiveMinutes == 0 ? job.ReadyAt : timeline.EarliestFreeHands(job.ReadyAt, step.ActiveMinutes);
                job.Advance(timeline.Place(job.Dish.Id, step, start));
            }
        }
    }

    private sealed class Job
    {
        public Job(Dish dish)
        {
            Dish = dish;
            Steps = dish.Steps.OrderBy(step => step.Order).ToArray();
            foreach (var step in Steps)
            {
                if (step.ActiveMinutes < 0 || step.PassiveMinutes < 0)
                {
                    throw new ArgumentException($"Dish '{dish.Id}' step {step.Order} has negative minutes.", nameof(dish));
                }
            }

            TotalMinutes = Steps.Sum(step => step.ActiveMinutes + step.PassiveMinutes);
        }

        public Dish Dish { get; }

        public Step[] Steps { get; }

        public int TotalMinutes { get; }

        /// <summary>When the previous step, passive part included, is done: the earliest the next step may start.</summary>
        public int ReadyAt { get; set; }

        public int NextIndex { get; private set; }

        public Step? Current => NextIndex < Steps.Length ? Steps[NextIndex] : null;

        public void Advance(CookPlanStep placed)
        {
            ReadyAt = placed.StartMinute + placed.ActiveMinutes + (placed.TimerMinutes ?? 0);
            NextIndex++;
        }

        /// <summary>Steps that need no hands (waiting, resting) go in as soon as the step before them is done.</summary>
        public void PlacePassiveOnlySteps(Timeline timeline)
        {
            while (Current is { ActiveMinutes: 0 } step)
            {
                Advance(timeline.Place(Dish.Id, step, ReadyAt));
            }
        }
    }

    private sealed class Timeline
    {
        private readonly List<CookPlanStep> _steps = [];
        private readonly List<(int Start, int End)> _handsBusy = [];

        public int EndMinute => _steps.Count == 0 ? 0 : _steps.Max(step => step.StartMinute + step.ActiveMinutes + (step.TimerMinutes ?? 0));

        public int HandsBusyUntil => _handsBusy.Count == 0 ? 0 : _handsBusy.Max(busy => busy.End);

        public CookPlanStep Place(string dishId, Step step, int start)
        {
            var placed = new CookPlanStep(
                start,
                dishId,
                step.Text,
                step.ActiveMinutes,
                step.PassiveMinutes > 0 ? step.PassiveMinutes : null,
                step.IngredientIds);

            _steps.Add(placed);
            if (step.ActiveMinutes > 0)
            {
                _handsBusy.Add((start, start + step.ActiveMinutes));
            }

            return placed;
        }

        /// <summary>The first minute from <paramref name="from"/> on at which the hands are free for <paramref name="minutes"/> in a row.</summary>
        public int EarliestFreeHands(int from, int minutes)
        {
            var start = from;
            bool moved;
            do
            {
                moved = false;
                foreach (var (busyStart, busyEnd) in _handsBusy)
                {
                    if (start < busyEnd && busyStart < start + minutes)
                    {
                        start = busyEnd;
                        moved = true;
                    }
                }
            }
            while (moved);

            return start;
        }

        /// <summary>Steps ordered by start minute; steps starting together keep the order they were placed in.</summary>
        public IReadOnlyList<CookPlanStep> InStartOrder() => _steps.OrderBy(step => step.StartMinute).ToArray();
    }
}
