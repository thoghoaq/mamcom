using MamCom.Core.Models;

namespace MamCom.Studio.Editing;

/// <summary>
/// One of the four keys of a region-keyed object (<c>names</c> and <c>marketUnit</c>): the default, or a region.
/// </summary>
public static class RegionKeyList
{
    /// <summary>The keys in the order they are written to the file.</summary>
    public static readonly IReadOnlyList<string> Keys = [RegionKeys.Default, "north", "central", "south"];
}

/// <summary>The market unit of one key as the form holds it. A unit that is not <see cref="Enabled"/> is not in the file.</summary>
public sealed class UnitForm
{
    public bool Enabled { get; set; }

    public string Display { get; set; } = string.Empty;

    public double GramsPerUnit { get; set; } = 1;

    public double Step { get; set; } = 1;

    public bool ShowApproxGrams { get; set; }
}

/// <summary>The picture of an ingredient as the form holds it. Empty text is no value.</summary>
public sealed class ImageForm
{
    public string Thumb { get; set; } = string.Empty;

    public string Full { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public string License { get; set; } = string.Empty;

    public bool IsIllustration { get; set; }
}

/// <summary>
/// The editable part of an ingredient, in a shape a form can bind to. The id and the nutrition (numbers and source) are not in
/// it: they are not the editor's to change, and <see cref="ToIngredient"/> takes them from the ingredient being edited. A name
/// or unit left out of a key is simply not written; the schema says whether what is left is enough.
/// </summary>
public sealed class IngredientForm
{
    /// <summary>The name for each key of <see cref="RegionKeyList.Keys"/>; empty means the key is not written.</summary>
    public Dictionary<string, string> Names { get; set; } = RegionKeyList.Keys.ToDictionary(k => k, _ => string.Empty);

    /// <summary>One alias per line.</summary>
    public string AliasesText { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public Aisle Aisle { get; set; }

    /// <summary>Empty when the ingredient is not a protein source.</summary>
    public string ProteinSource { get; set; } = string.Empty;

    public Dictionary<string, UnitForm> Units { get; set; } = RegionKeyList.Keys.ToDictionary(k => k, _ => new UnitForm());

    public double WasteRatio { get; set; }

    public bool IsPantry { get; set; }

    public ImageForm Image { get; set; } = new();

    public static IngredientForm From(Ingredient ingredient)
    {
        ArgumentNullException.ThrowIfNull(ingredient);

        var form = new IngredientForm
        {
            AliasesText = string.Join('\n', ingredient.Aliases),
            Category = ingredient.Category,
            Aisle = ingredient.Aisle,
            ProteinSource = ingredient.ProteinSource ?? string.Empty,
            WasteRatio = ingredient.WasteRatio,
            IsPantry = ingredient.IsPantry,
            Image = new ImageForm
            {
                Thumb = ingredient.Image.Thumb ?? string.Empty,
                Full = ingredient.Image.Full ?? string.Empty,
                Source = ingredient.Image.Source ?? string.Empty,
                License = ingredient.Image.License ?? string.Empty,
                IsIllustration = ingredient.Image.IsIllustration,
            },
        };

        foreach (var key in RegionKeyList.Keys)
        {
            form.Names[key] = ingredient.Names.GetValueOrDefault(key) ?? string.Empty;
            if (ingredient.MarketUnit.TryGetValue(key, out var unit))
            {
                form.Units[key] = new UnitForm
                {
                    Enabled = true,
                    Display = unit.Display,
                    GramsPerUnit = unit.GramsPerUnit,
                    Step = unit.Step,
                    ShowApproxGrams = unit.ShowApproxGrams,
                };
            }
        }

        return form;
    }

    /// <summary>The ingredient the form describes: <paramref name="original"/> with the form's values.</summary>
    public Ingredient ToIngredient(Ingredient original)
    {
        ArgumentNullException.ThrowIfNull(original);

        return original with
        {
            Names = InFileOrder(RegionKeyList.Keys.Where(k => !string.IsNullOrWhiteSpace(Names[k])), original.Names.Keys)
                .ToDictionary(k => k, k => Names[k].Trim()),
            Aliases = [.. AliasesText.Split('\n').Select(a => a.Trim()).Where(a => a.Length > 0)],
            Category = Category.Trim(),
            Aisle = Aisle,
            ProteinSource = NullIfBlank(ProteinSource),
            MarketUnit = InFileOrder(RegionKeyList.Keys.Where(k => Units[k].Enabled), original.MarketUnit.Keys)
                .ToDictionary(k => k, k => new MarketUnit(Units[k].Display.Trim(), Units[k].GramsPerUnit, Units[k].Step, Units[k].ShowApproxGrams)),
            WasteRatio = WasteRatio,
            IsPantry = IsPantry,
            Image = new ImageRef(NullIfBlank(Image.Thumb), NullIfBlank(Image.Full), NullIfBlank(Image.Source), NullIfBlank(Image.License), Image.IsIllustration),
        };
    }

    /// <summary>
    /// The keys with the ones the file already has first, in the order the file has them, then the new ones in the order of
    /// <see cref="RegionKeyList.Keys"/>. A file the person did not touch keeps its order, so Git sees no change.
    /// </summary>
    private static IEnumerable<string> InFileOrder(IEnumerable<string> keys, IEnumerable<string> inFile)
    {
        var order = inFile.ToList();
        return keys.OrderBy(k => order.IndexOf(k) is var i && i >= 0 ? i : int.MaxValue);
    }

    private static string? NullIfBlank(string text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
