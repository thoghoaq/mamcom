using MamCom.Core.Models;
using MamCom.Core.Nutrition;
using MamCom.DataBuilder.Loading;
using MamCom.DataBuilder.Schemas;
using MamCom.DataBuilder.Validation;

namespace MamCom.Studio.Review;

/// <summary>
/// Everything a review needs to know about the kitchen, read from the data folder at one moment: the ingredients, the config,
/// the schemas, and the choices a dish may be made from (seasons, protein sources). It is read again for every page, so a
/// change made by hand or by another tool is seen at once.
/// </summary>
public sealed class Kitchen
{
    public Kitchen(LoadedData data, SchemaSet? schemas)
    {
        ArgumentNullException.ThrowIfNull(data);

        Data = data;
        Schemas = schemas;

        var ingredients = new Dictionary<string, Ingredient>(StringComparer.Ordinal);
        foreach (var file in data.Ingredients)
        {
            ingredients.TryAdd(file.Value.Id, file.Value);
        }

        Ingredients = ingredients;
        Nutrition = data.Config is { } config ? new NutritionCalculator(config.Nutrition, config.Portion) : null;
        Seasons = data.Config is { } c ? [.. c.Spinner.SeasonByMonth.Distinct().Append(SeasonIds.All)] : [SeasonIds.All];
        ProteinSourceIds =
        [
            ProteinSources.None,
            ProteinSources.Vegetarian,
            .. ingredients.Values.Select(i => i.ProteinSource).OfType<string>().Distinct().Except([ProteinSources.None, ProteinSources.Vegetarian]).Order(StringComparer.Ordinal),
        ];
    }

    public LoadedData Data { get; }

    /// <summary>Null when the schema folder is missing or unusable.</summary>
    public SchemaSet? Schemas { get; }

    public IReadOnlyDictionary<string, Ingredient> Ingredients { get; }

    /// <summary>Null when the config files are missing or invalid.</summary>
    public NutritionCalculator? Nutrition { get; }

    /// <summary>The season ids a dish may name: the ones of the spinner config, then <c>all</c>.</summary>
    public IReadOnlyList<string> Seasons { get; }

    /// <summary>The protein sources a dish may have: none, vegetarian, then the ones the ingredients have.</summary>
    public IReadOnlyList<string> ProteinSourceIds { get; }

    /// <summary>False when something a review stands on is missing; see <see cref="Problems"/>.</summary>
    public bool IsUsable => Schemas is not null && Data.Config is not null && Data.Validation is not null;

    /// <summary>What is wrong with the data folder itself (schemas, config, ingredients), not with one dish.</summary>
    public IReadOnlyList<Diagnostic> Problems =>
    [
        .. Data.Diagnostics.Where(d => d.Severity == Severity.Error && !d.File.StartsWith("dishes/", StringComparison.Ordinal)),
    ];

    /// <summary>The name an ingredient is shown by: its default name, else its id.</summary>
    public string IngredientName(string id) =>
        Ingredients.TryGetValue(id, out var ingredient) && ingredient.Names.TryGetValue(RegionKeys.Default, out var name) ? name : id;
}

/// <summary>Reads the kitchen from the data folder.</summary>
public sealed class KitchenLoader(DataFolder folder)
{
    public DataFolder Folder { get; } = folder;

    public Kitchen Load()
    {
        var data = DataLoader.Load(Folder.Root);

        SchemaSet? schemas = null;
        if (Directory.Exists(Folder.SchemaFolder))
        {
            try
            {
                schemas = SchemaSet.Load(Folder.SchemaFolder);
            }
            catch (InvalidOperationException)
            {
                // DataLoader has already reported the unusable schema; Kitchen.IsUsable tells the pages.
            }
        }

        return new Kitchen(data, schemas);
    }
}
