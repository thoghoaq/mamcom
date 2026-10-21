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
/// <para>
/// The kitchen has a number of stoves (ADR-028). The data does not say which step needs one, so a dish that is cooked
/// (every <see cref="CookingMethod"/> but <see cref="CookingMethod.Toss"/>) holds one stove from the start of its first
/// step to the end of its last, and no more such dishes are under way at once than there are stoves. The rice cooker is
/// not a stove.
/// </para>
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

    /// <summary>As many stoves as there are dishes: no dish ever waits for one, the plan of one cook alone.</summary>
    public const int AnyNumberOfStoves = int.MaxValue;

    /// <summary>Whether a dish holds a stove while it is under way: everything that is cooked, not a salad that is only tossed.</summary>
    public static bool NeedsStove(Dish dish)
    {
        ArgumentNullException.ThrowIfNull(dish);

        return dish.CookingMethod != CookingMethod.Toss;
    }

    /// <summary>The stoves of this household's kitchen: what it said, or the usual number of the config.</summary>
    public int StovesOf(Household household) => _config.StovesOf(household);

    public int EstimateMealMinutes(IReadOnlyList<Dish> dishes, int stoves = AnyNumberOfStoves) => Schedule(dishes, stoves).MealTimeMinutes;

    /// <summary>
    /// The largest of: the rice, the longest dish (its steps run one after another), all hands-on time added up
    /// (one cook can do one thing at a time), and the minutes the cooked dishes hold a stove shared out over the
    /// stoves. Every plan this scheduler makes is at least this long.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="stoves"/> is less than one.</exception>
    public int MinimumMealMinutes(IReadOnlyList<Dish> dishes, int stoves = AnyNumberOfStoves)
    {
        ArgumentNullException.ThrowIfNull(dishes);
        ArgumentOutOfRangeException.ThrowIfLessThan(stoves, 1);

        var riceTotal = _config.RiceActiveMinutes + _config.RicePassiveMinutes;
        var longestDish = dishes.Select(dish => new Job(dish).TotalMinutes).DefaultIfEmpty(0).Max();
        var allHandsOn = _config.RiceActiveMinutes + dishes.Sum(dish => dish.Steps.Sum(step => step.ActiveMinutes));
        var onStoves = dishes.Where(NeedsStove).Sum(dish => new Job(dish).TotalMinutes);
        var sharedOut = onStoves == 0 ? 0 : ((onStoves - 1) / stoves) + 1;

        return Math.Max(Math.Max(riceTotal, longestDish), Math.Max(allHandsOn, sharedOut));
    }

    /// <summary>
    /// The plan for the dishes plus rice, steps in start order (rice first). The result does not depend on the order
    /// of <paramref name="dishes"/>; ties are broken by dish id.
    /// </summary>
    /// <param name="stoves">The stoves of the kitchen; <see cref="AnyNumberOfStoves"/> when they do not matter.</param>
    /// <exception cref="ArgumentException">A step has negative minutes.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="stoves"/> is less than one.</exception>
    public CookPlan Schedule(IReadOnlyList<Dish> dishes, int stoves = AnyNumberOfStoves)
    {
        ArgumentNullException.ThrowIfNull(dishes);
        ArgumentOutOfRangeException.ThrowIfLessThan(stoves, 1);

        var timeline = new Timeline();
        var kitchen = new Stoves(stoves);
        timeline.Place(
            CookPlan.RiceDishId,
            new Step(1, _config.RiceStepText, _config.RiceActiveMinutes, _config.RicePassiveMinutes, []),
            start: 0);

        var jobs = dishes.Select(dish => new Job(dish)).Where(job => job.Steps.Length > 0).ToList();
        var cold = Prioritised(jobs.Where(job => !job.Dish.ServeHot));
        var hot = Prioritised(jobs.Where(job => job.Dish.ServeHot));

        ScheduleCold(timeline, kitchen, cold);
        ScheduleHot(timeline, kitchen, hot);

        return new CookPlan(timeline.InStartOrder(), timeline.EndMinute);
    }

    /// <summary>Longest total time first; equal times go in id order so the plan never depends on the input order.</summary>
    private static List<Job> Prioritised(IEnumerable<Job> jobs) =>
        jobs.OrderByDescending(job => job.TotalMinutes).ThenBy(job => job.Dish.Id, StringComparer.Ordinal).ToList();

    private static void ScheduleCold(Timeline timeline, Stoves stoves, List<Job> jobs)
    {
        // Waiting steps that open a dish (soaking, marinating) start at once, in the order of priority, as long as the dish finds a
        // stove: it holds one from its first step on.
        foreach (var job in jobs.Where(job => job.Current is { ActiveMinutes: 0 } && stoves.MayGoOn(job, job.ReadyAt)))
        {
            stoves.Start(job, job.ReadyAt);
            job.PlacePassiveOnlySteps(timeline);
            stoves.FinishIfDone(job);
        }

        // The cook starts on the first free minute after the rice is washed.
        var cookFreeAt = timeline.HandsBusyUntil;
        while (jobs.Any(job => job.Current is not null))
        {
            var next = jobs.FirstOrDefault(job => job.Current is not null && job.ReadyAt <= cookFreeAt && stoves.MayGoOn(job, cookFreeAt));
            if (next is null)
            {
                // Nothing can be done now: the cook waits for the earliest step that will be ready and have its stove. A dish
                // that waits for a stove held by a dish still under way has no such minute yet; that dish has, and goes first.
                var from = cookFreeAt;
                cookFreeAt = jobs.Where(job => job.Current is not null)
                    .Select(job => stoves.EarliestGoOn(job, Math.Max(job.ReadyAt, from)))
                    .OfType<int>()
                    .Min();
                continue;
            }

            var step = next.Current!;
            var placed = timeline.Place(next.Dish.Id, step, cookFreeAt);
            stoves.Start(next, placed.StartMinute);
            cookFreeAt = placed.StartMinute + step.ActiveMinutes;
            next.Advance(placed);
            next.PlacePassiveOnlySteps(timeline);
            stoves.FinishIfDone(next);
        }
    }

    private static void ScheduleHot(Timeline timeline, Stoves stoves, List<Job> jobs)
    {
        foreach (var job in jobs)
        {
            // Aim to finish exactly when the meal ends, then let the hands decide. When every stove is taken somewhere in the
            // span that gives, try again from the minute the first one is given back after the dish would have started.
            var readyAt = Math.Max(0, timeline.EndMinute - job.TotalMinutes);
            while (true)
            {
                var (start, end) = PlaceHot(timeline.Copy(), job.Copy(), readyAt);
                if (!job.HoldsStove || stoves.FreeAllAlong(start, end))
                {
                    var (placedStart, _) = PlaceHot(timeline, job, readyAt);
                    stoves.Start(job, placedStart);
                    stoves.FinishIfDone(job);
                    break;
                }

                readyAt = stoves.NextRelease(start);
            }
        }
    }

    /// <returns>The minute the dish starts and the minute it is done.</returns>
    private static (int Start, int End) PlaceHot(Timeline timeline, Job job, int readyAt)
    {
        job.ReadyAt = readyAt;
        int? start = null;
        while (job.Current is { } step)
        {
            var at = step.ActiveMinutes == 0 ? job.ReadyAt : timeline.EarliestFreeHands(job.ReadyAt, step.ActiveMinutes);
            start ??= at;
            job.Advance(timeline.Place(job.Dish.Id, step, at));
        }

        return (start ?? readyAt, job.ReadyAt);
    }

    /// <summary>
    /// The stoves of the kitchen, as the spans the cooked dishes hold one. A dish that has begun holds its stove until its last
    /// step is done, so while it is under way its span has no end yet.
    /// </summary>
    private sealed class Stoves(int count)
    {
        private readonly List<(int Start, int End)> _done = [];
        private readonly HashSet<Job> _underWay = [];
        private readonly Dictionary<Job, int> _startedAt = [];

        /// <summary>Whether the next step of the dish may be taken at <paramref name="minute"/>: a dish that has begun has its stove already.</summary>
        public bool MayGoOn(Job job, int minute) => !job.HoldsStove || _underWay.Contains(job) || InUse(minute) < count;

        /// <summary>The first minute from <paramref name="from"/> on at which the dish may go on; null while that depends on a dish still under way.</summary>
        public int? EarliestGoOn(Job job, int from) =>
            MayGoOn(job, from)
                ? from
                : _done.Select(span => span.End).Where(end => end > from).Order().Where(end => InUse(end) < count).Cast<int?>().FirstOrDefault();

        public void Start(Job job, int minute)
        {
            if (job.HoldsStove && _underWay.Add(job))
            {
                _startedAt[job] = minute;
            }
        }

        /// <summary>Gives the stove back when the dish has no step left.</summary>
        public void FinishIfDone(Job job)
        {
            if (job.Current is null && _underWay.Remove(job))
            {
                _done.Add((_startedAt[job], job.ReadyAt));
            }
        }

        /// <summary>Whether a dish cooked from <paramref name="start"/> to <paramref name="end"/> finds a free stove all along.</summary>
        public bool FreeAllAlong(int start, int end) =>
            _done.Select(span => span.Start).Where(minute => minute > start && minute < end).Append(start).All(minute => InUse(minute) < count);

        /// <summary>The first minute after <paramref name="from"/> at which a stove is given back.</summary>
        public int NextRelease(int from) => _done.Select(span => span.End).Where(end => end > from).Min();

        private int InUse(int minute) => _underWay.Count + _done.Count(span => span.Start <= minute && minute < span.End);
    }

    private sealed class Job
    {
        public Job(Dish dish)
        {
            Dish = dish;
            HoldsStove = NeedsStove(dish);
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

        public bool HoldsStove { get; }

        public Step[] Steps { get; }

        public int TotalMinutes { get; }

        /// <summary>When the previous step, passive part included, is done: the earliest the next step may start.</summary>
        public int ReadyAt { get; set; }

        public int NextIndex { get; private set; }

        public Step? Current => NextIndex < Steps.Length ? Steps[NextIndex] : null;

        /// <summary>A job at the same point, to try a placement on.</summary>
        public Job Copy()
        {
            var copy = new Job(Dish) { ReadyAt = ReadyAt };
            copy.NextIndex = NextIndex;
            return copy;
        }

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

        /// <summary>A timeline with the same steps, to try a placement on.</summary>
        public Timeline Copy()
        {
            var copy = new Timeline();
            copy._steps.AddRange(_steps);
            copy._handsBusy.AddRange(_handsBusy);
            return copy;
        }

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
