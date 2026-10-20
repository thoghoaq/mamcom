using MamCom.Core.Models;

namespace MamCom.Core.Spinning;

/// <summary>
/// What the "Mâm hôm nay" screen does with <see cref="MealSpinner"/> (SPEC §8.1-2): spin, swap one dish, lock a slot, step through the
/// last trays, apply the quick filters, confirm. Everything is a function from one <see cref="TraySession"/> to the next, so the
/// screen only draws what it is given and the rules can be tested without it.
/// </summary>
public sealed class TrayDesk
{
    private readonly MealSpinner _spinner;
    private readonly SpinnerConfig _config;
    private readonly IClock _clock;

    public TrayDesk(MealSpinner spinner, SpinnerConfig config, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(spinner);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(clock);

        config.Validate();
        _spinner = spinner;
        _config = config;
        _clock = clock;
    }

    /// <summary>
    /// Spins the tray. Locked slots of the tray on the screen stay; nothing carries over from a tray of another day or from a tray that
    /// was confirmed. The spin goes to the front of the history and onto the screen, and counts towards the quick filters being offered.
    /// </summary>
    public TraySession Spin(TraySession session, SpinContext context)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(context);

        var current = !session.IsConfirmed && session.Viewed?.Tray is { } tray && tray.Date == _clock.Today ? tray : null;
        var result = _spinner.Spin(Narrow(context, session.Filters, LockedIds(current)), current);

        return session with
        {
            History = session.History.Push(result, _config.HistorySize),
            ViewIndex = 0,
            SpinsSinceConfirm = session.SpinsSinceConfirm + 1,
            ConfirmedOn = null,
        };
    }

    /// <summary>Swaps the dish in one slot of the tray on the screen. The tray stays where it is in the history; it is changed, not added.</summary>
    /// <exception cref="InvalidOperationException">There is no tray on the screen, or it is confirmed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="slotIndex"/> is not a slot of the tray.</exception>
    /// <exception cref="ArgumentException">The slot is locked.</exception>
    public TraySession Reroll(TraySession session, SpinContext context, int slotIndex)
    {
        ArgumentNullException.ThrowIfNull(context);

        var viewed = Open(session);
        var kept = viewed.Tray.Slots.Select(s => s.DishId);   // the dishes already on the tray stay in the catalog the spinner sees
        var result = _spinner.Reroll(Narrow(context, session.Filters, kept), viewed.Tray, slotIndex);

        return Replace(session, result);
    }

    /// <summary>Locks the slot, or unlocks it when it was locked. A locked slot is kept by the next spin and cannot be swapped.</summary>
    /// <exception cref="InvalidOperationException">There is no tray on the screen, or it is confirmed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="slotIndex"/> is not a slot of the tray.</exception>
    public TraySession ToggleLock(TraySession session, int slotIndex)
    {
        var viewed = Open(session);
        ArgumentOutOfRangeException.ThrowIfNegative(slotIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(slotIndex, viewed.Tray.Slots.Count);

        var slots = viewed.Tray.Slots.Select((slot, index) => index == slotIndex ? slot with { IsLocked = !slot.IsLocked } : slot).ToArray();
        return Replace(session, viewed with { Tray = viewed.Tray with { Slots = slots } });
    }

    /// <summary>Shows the next older tray of the history; nothing changes at the end of it.</summary>
    public TraySession Previous(TraySession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.CanGoPrevious ? session with { ViewIndex = session.ViewIndex + 1 } : session;
    }

    /// <summary>Shows the next newer tray again, after going back; nothing changes at the newest.</summary>
    public TraySession Next(TraySession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.CanGoNext ? session with { ViewIndex = session.ViewIndex - 1 } : session;
    }

    /// <summary>
    /// Confirms the tray on the screen: it becomes the newest of the history, its dishes are recorded as eaten today, and the count
    /// of spins since the last confirmation starts again.
    /// </summary>
    /// <exception cref="InvalidOperationException">There is no tray on the screen, it is already confirmed, or it has no dish.</exception>
    public (TraySession Session, EatenLog Eaten) Confirm(TraySession session, EatenLog eaten)
    {
        ArgumentNullException.ThrowIfNull(eaten);

        var viewed = Open(session);
        if (viewed.Tray.Slots.Count == 0)
        {
            throw new InvalidOperationException("A tray with no dish cannot be confirmed.");
        }

        var today = _clock.Today;
        var history = new SpinHistory([viewed, .. session.History.Items.Where((_, index) => index != session.ViewIndex)]);

        return (
            session with { History = history, ViewIndex = 0, SpinsSinceConfirm = 0, ConfirmedOn = today },
            eaten.With(viewed.Tray.Slots.Select(s => s.DishId), today));
    }

    /// <summary>Takes the confirmation back so the tray can be changed again. The dishes stay recorded as eaten.</summary>
    public TraySession Reopen(TraySession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session with { ConfirmedOn = null };
    }

    public TraySession SetFilters(TraySession session, QuickFilters filters)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(filters);
        return session with { Filters = filters };
    }

    /// <summary>The quick filters are shown once enough spins went by without a confirmation, and stay while any of them is on.</summary>
    public bool ShowQuickFilters(TraySession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.Filters.Any || session.SpinsSinceConfirm >= _config.QuickFilters.SuggestAfterSpins;
    }

    /// <summary>
    /// The context the spinner is given once the quick filters are applied: dishes of the dropped kinds are not in the catalog it sees
    /// (those in <paramref name="keepIds"/>, the locked ones, are always kept) and the "short" filter lowers the minutes available.
    /// </summary>
    public SpinContext Narrow(SpinContext context, QuickFilters filters, IEnumerable<string> keepIds)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(filters);
        ArgumentNullException.ThrowIfNull(keepIds);

        if (!filters.Any)
        {
            return context;
        }

        var quick = _config.QuickFilters;
        var keep = keepIds.ToHashSet(StringComparer.Ordinal);

        bool Allowed(Dish dish) =>
            keep.Contains(dish.Id)
            || !((filters.NoRedMeat && quick.RedMeatProteinSources.Contains(dish.ProteinSource, StringComparer.Ordinal))
                || (filters.LowFat && quick.LowFatExcludedMethods.Contains(dish.CookingMethod)));

        var household = filters.Short
            ? context.Household with { AvailableMinutes = Math.Min(context.Household.AvailableMinutes ?? int.MaxValue, quick.MaxMinutes) }
            : context.Household;

        return context with { Household = household, Dishes = [.. context.Dishes.Where(Allowed)] };
    }

    private static IEnumerable<string> LockedIds(MealTray? tray) =>
        tray?.Slots.Where(s => s.IsLocked).Select(s => s.DishId) ?? [];

    private static SpinResult Open(TraySession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session.IsConfirmed)
        {
            throw new InvalidOperationException("The tray is confirmed; reopen it to change it.");
        }

        return session.Viewed ?? throw new InvalidOperationException("There is no tray on the screen.");
    }

    private static TraySession Replace(TraySession session, SpinResult result) =>
        session with { History = new SpinHistory([.. session.History.Items.Select((r, index) => index == session.ViewIndex ? result : r)]) };
}
