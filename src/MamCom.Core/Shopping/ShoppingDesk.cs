using System.Text.Json.Serialization;
using MamCom.Core.Models;
using MamCom.Core.Portions;

namespace MamCom.Core.Shopping;

/// <summary>
/// What the shopping screen remembers (SPEC §8.1-4): which items are ticked off for which tray, and whether the items a kitchen
/// always has are shown. Kept on the device so the ticks are still there when the app is opened again in the market.
/// </summary>
/// <param name="TrayKey">The tray the ticks belong to (<see cref="ShoppingDesk.TrayKeyOf"/>); empty while there are none.</param>
/// <param name="CheckedIngredientIds">The ingredients ticked off, in the order they were ticked.</param>
/// <param name="ShowPantry">The "Hiện đồ có sẵn" switch. It belongs to the person, not to a tray, so it outlives the ticks.</param>
public sealed record ShoppingState(string TrayKey, IReadOnlyList<string> CheckedIngredientIds, bool ShowPantry)
{
    public static ShoppingState Empty { get; } = new(string.Empty, [], false);

    /// <summary>False for a state that was edited by hand or came from another shape: no ticks without a tray, no blank or repeated id.</summary>
    [JsonIgnore]
    public bool IsUsable =>
        TrayKey is not null
        && CheckedIngredientIds is not null
        && CheckedIngredientIds.All(id => !string.IsNullOrWhiteSpace(id))
        && CheckedIngredientIds.Distinct(StringComparer.Ordinal).Count() == CheckedIngredientIds.Count
        && (TrayKey.Length > 0 || CheckedIngredientIds.Count == 0);
}

/// <summary>The shopping list of one tray for one household, with what the header of the shared text needs.</summary>
/// <param name="Date">The day of the meal.</param>
/// <param name="Items">Every item, pantry ones included and flagged, ordered by aisle (<see cref="ShoppingListBuilder"/>).</param>
public sealed record ShoppingSheet(DateOnly Date, int MemberCount, IReadOnlyList<ShoppingItem> Items);

/// <summary>How far along the shopping is, among the items on the screen.</summary>
public sealed record ShoppingProgress(int Done, int Total);

/// <summary>
/// The shopping screen as functions from one state to the next, as <c>TrayDesk</c> is for the tray: the list of a tray, the state when the
/// screen opens, ticking an item, the pantry switch, how far along it is, and the text that goes to Zalo.
/// </summary>
public sealed class ShoppingDesk
{
    private readonly PortionCalculator _portions;
    private readonly ShoppingListBuilder _builder;
    private readonly ShoppingTextFormatter _formatter;

    /// <exception cref="ArgumentException">The portion config has a value the formulas cannot use.</exception>
    public ShoppingDesk(PortionConfig portions, ShareTextLabels? labels = null)
    {
        ArgumentNullException.ThrowIfNull(portions);

        _portions = new PortionCalculator(portions);
        _builder = new ShoppingListBuilder(portions);
        _formatter = new ShoppingTextFormatter(labels);
    }

    /// <summary>Names a tray, so ticks made for one tray are not shown on another: the day and the dishes in the order of the slots.</summary>
    public static string TrayKeyOf(MealTray tray)
    {
        return TrayKeys.Of(tray);
    }

    /// <exception cref="InvalidOperationException">A dish uses an unknown ingredient, or an ingredient has no market unit for the region of the household (a data error).</exception>
    /// <exception cref="ArgumentOutOfRangeException">A member's bowls per meal is outside the configured range.</exception>
    public ShoppingSheet Sheet(Household household, MealTray tray, IReadOnlyList<Dish> dishes, IReadOnlyDictionary<string, Ingredient> ingredients)
    {
        ArgumentNullException.ThrowIfNull(household);
        ArgumentNullException.ThrowIfNull(tray);

        var items = _builder.Build(household, _portions.Calculate(household), dishes, ingredients);
        return new ShoppingSheet(tray.Date, household.Members.Count, items);
    }

    /// <summary>
    /// The state to start the screen from: what was saved, if it is for this tray (ticks of items that are no longer on the list are dropped),
    /// else no ticks. The pantry switch carries over either way.
    /// </summary>
    public ShoppingState Open(ShoppingState? saved, MealTray tray, ShoppingSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(tray);
        ArgumentNullException.ThrowIfNull(sheet);

        var usable = saved is { IsUsable: true } ? saved : ShoppingState.Empty;
        var key = TrayKeyOf(tray);

        if (usable.TrayKey != key)
        {
            return new ShoppingState(key, [], usable.ShowPantry);
        }

        var onTheList = sheet.Items.Select(item => item.IngredientId).ToHashSet(StringComparer.Ordinal);
        return usable with { CheckedIngredientIds = [.. usable.CheckedIngredientIds.Where(onTheList.Contains)] };
    }

    /// <summary>Ticks an item that is not ticked, and takes the tick off one that is.</summary>
    /// <exception cref="ArgumentException">The id is blank.</exception>
    public ShoppingState Toggle(ShoppingState state, string ingredientId)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(ingredientId);

        var ticked = state.CheckedIngredientIds.Contains(ingredientId, StringComparer.Ordinal);
        return state with
        {
            CheckedIngredientIds = ticked
                ? [.. state.CheckedIngredientIds.Where(id => !string.Equals(id, ingredientId, StringComparison.Ordinal))]
                : [.. state.CheckedIngredientIds, ingredientId],
        };
    }

    public ShoppingState SetShowPantry(ShoppingState state, bool showPantry)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state with { ShowPantry = showPantry };
    }

    /// <summary>The items on the screen by aisle: the pantry items only when the switch is on.</summary>
    public IReadOnlyList<ShoppingGroup> Groups(ShoppingSheet sheet, ShoppingState state)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(state);

        return ShoppingGroups.ByAisle(sheet.Items, state.ShowPantry);
    }

    /// <summary>How many of the items on the screen are ticked. A pantry item that is hidden counts neither way, ticked or not.</summary>
    public ShoppingProgress Progress(ShoppingSheet sheet, ShoppingState state)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(state);

        var shown = Groups(sheet, state).SelectMany(group => group.Items).ToArray();
        var ticked = state.CheckedIngredientIds.ToHashSet(StringComparer.Ordinal);
        return new ShoppingProgress(shown.Count(item => ticked.Contains(item.IngredientId)), shown.Length);
    }

    /// <summary>
    /// The text for Zalo (SPEC §6.2). It is the whole list for whoever does the shopping, so ticks do not change it; the pantry items are
    /// in it only when they are on the screen.
    /// </summary>
    public string ShareText(ShoppingSheet sheet, ShoppingState state)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(state);

        return _formatter.Format(sheet.Items, sheet.Date, sheet.MemberCount, state.ShowPantry);
    }
}
