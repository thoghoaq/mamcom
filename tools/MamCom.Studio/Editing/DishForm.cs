using MamCom.Core.Models;

namespace MamCom.Studio.Editing;

/// <summary>One recipe line as the form holds it. <see cref="Note"/> is empty text, never null, so an input can bind to it.</summary>
public sealed class LineForm
{
    public string IngredientId { get; set; } = string.Empty;

    public double GramsEdiblePerServing { get; set; }

    public ScaleKind Scale { get; set; } = ScaleKind.Linear;

    public double ConsumedRatio { get; set; } = 1;

    public string Note { get; set; } = string.Empty;
}

/// <summary>One step as the form holds it; its number is its place in the list.</summary>
public sealed class StepForm
{
    public string Text { get; set; } = string.Empty;

    public int ActiveMinutes { get; set; }

    public int PassiveMinutes { get; set; }

    public List<string> IngredientIds { get; set; } = [];
}

/// <summary>
/// The editable part of a dish, in a shape a form can bind to. Id, status, provenance and image are not in it: they are not the
/// editor's to change, and <see cref="ToDish"/> takes them from the dish being edited.
/// </summary>
public sealed class DishForm
{
    public string Name { get; set; } = string.Empty;

    public DishSlot Slot { get; set; }

    public List<Region> Regions { get; set; } = [];

    public List<string> Seasons { get; set; } = [];

    public SpicyLevel Spicy { get; set; }

    public CookingMethod CookingMethod { get; set; }

    public string ProteinSource { get; set; } = string.Empty;

    public bool ServeHot { get; set; }

    public List<LineForm> Lines { get; set; } = [];

    public List<StepForm> Steps { get; set; } = [];

    public static DishForm From(Dish dish)
    {
        ArgumentNullException.ThrowIfNull(dish);

        return new DishForm
        {
            Name = dish.Name,
            Slot = dish.Slot,
            Regions = [.. dish.Regions],
            Seasons = [.. dish.Seasons],
            Spicy = dish.Spicy,
            CookingMethod = dish.CookingMethod,
            ProteinSource = dish.ProteinSource,
            ServeHot = dish.ServeHot,
            Lines = [.. dish.Ingredients.Select(l => new LineForm
            {
                IngredientId = l.IngredientId,
                GramsEdiblePerServing = l.GramsEdiblePerServing,
                Scale = l.Scale,
                ConsumedRatio = l.ConsumedRatio,
                Note = l.Note ?? string.Empty,
            })],
            Steps = [.. dish.Steps.Select(s => new StepForm
            {
                Text = s.Text,
                ActiveMinutes = s.ActiveMinutes,
                PassiveMinutes = s.PassiveMinutes,
                IngredientIds = [.. s.IngredientIds],
            })],
        };
    }

    /// <summary>The dish the form describes: <paramref name="original"/> with the form's values. Steps are numbered from 1 in list order.</summary>
    public Dish ToDish(Dish original)
    {
        ArgumentNullException.ThrowIfNull(original);

        return original with
        {
            Name = Name.Trim(),
            Slot = Slot,
            Regions = [.. Regions],
            Seasons = [.. Seasons],
            Spicy = Spicy,
            CookingMethod = CookingMethod,
            ProteinSource = ProteinSource,
            ServeHot = ServeHot,
            Ingredients = [.. Lines.Select(l => new DishIngredient(
                l.IngredientId,
                l.GramsEdiblePerServing,
                l.Scale,
                l.ConsumedRatio,
                string.IsNullOrWhiteSpace(l.Note) ? null : l.Note.Trim()))],
            Steps = [.. Steps.Select((s, i) => new Step(i + 1, s.Text.Trim(), s.ActiveMinutes, s.PassiveMinutes, [.. s.IngredientIds]))],
        };
    }

    // ---- choices ------------------------------------------------------------------------------------------------------------

    /// <summary>Adds or removes a region; the regions stay in north, central, south order.</summary>
    public void ToggleRegion(Region region)
    {
        if (!Regions.Remove(region))
        {
            Regions.Add(region);
            Regions = [.. Regions.Order()];
        }
    }

    /// <summary>
    /// Adds or removes a season. <c>all</c> excludes the others: choosing it clears them, and choosing another clears it.
    /// The seasons stay in the order of <paramref name="allSeasons"/>.
    /// </summary>
    public void ToggleSeason(string season, IReadOnlyList<string> allSeasons)
    {
        ArgumentNullException.ThrowIfNull(season);
        ArgumentNullException.ThrowIfNull(allSeasons);

        if (Seasons.Remove(season))
        {
            return;
        }

        if (season == SeasonIds.All)
        {
            Seasons.Clear();
        }
        else
        {
            Seasons.Remove(SeasonIds.All);
        }

        Seasons.Add(season);
        Seasons = [.. Seasons.OrderBy(s => IndexOf(allSeasons, s))];
    }

    // ---- lines --------------------------------------------------------------------------------------------------------------

    public void AddLine(string ingredientId) => Lines.Add(new LineForm { IngredientId = ingredientId, GramsEdiblePerServing = 1 });

    public void RemoveLine(int index)
    {
        if (index >= 0 && index < Lines.Count)
        {
            Lines.RemoveAt(index);
        }
    }

    // ---- steps --------------------------------------------------------------------------------------------------------------

    public void AddStep() => Steps.Add(new StepForm { ActiveMinutes = 1 });

    public void RemoveStep(int index)
    {
        if (index >= 0 && index < Steps.Count)
        {
            Steps.RemoveAt(index);
        }
    }

    /// <summary>Moves a step up (negative <paramref name="by"/>) or down; a move off either end does nothing.</summary>
    public void MoveStep(int index, int by)
    {
        var target = index + by;
        if (index < 0 || index >= Steps.Count || target < 0 || target >= Steps.Count)
        {
            return;
        }

        (Steps[index], Steps[target]) = (Steps[target], Steps[index]);
    }

    /// <summary>Marks an ingredient as used by a step, or not.</summary>
    public void ToggleStepIngredient(int stepIndex, string ingredientId)
    {
        ArgumentNullException.ThrowIfNull(ingredientId);

        if (stepIndex < 0 || stepIndex >= Steps.Count)
        {
            return;
        }

        var ids = Steps[stepIndex].IngredientIds;
        if (!ids.Remove(ingredientId))
        {
            ids.Add(ingredientId);
        }
    }

    private static int IndexOf(IReadOnlyList<string> values, string value)
    {
        for (var i = 0; i < values.Count; i++)
        {
            if (values[i] == value)
            {
                return i;
            }
        }

        return int.MaxValue;
    }
}
