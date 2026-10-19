using MamCom.Core.Models;
using MamCom.DataBuilder.Loading;
using MamCom.DataBuilder.Schemas;

namespace MamCom.DataBuilder.Validation;

/// <summary>
/// Holds one dish to the rules the shipped data is held to: the dish schema first, then the DataBuilder rules that look at a
/// single dish against the ingredient list (unknown ingredient or season, step order, a repeated line, the protein range,
/// an ingredient no step uses…). The rest of the data is not the dish's business, so only findings about this dish count: the
/// "ingredient nobody uses" of the whole kitchen, a broken file next to it, or a missing rice ingredient do not appear.
/// It is how MamCom.Studio and RecipeDrafter check a dish that is not part of the data folder yet, or has just been edited.
/// </summary>
public static class DishCheck
{
    /// <param name="file">Path to file the findings under, with forward slashes, for example <c>dishes/_drafts/ca-kho.json</c>. Its name must be the dish id.</param>
    /// <param name="text">The dish as it would be written to the file; this is what the schema reads.</param>
    /// <param name="data">The loaded data folder: the ingredient list and the config the rules need.</param>
    public static IReadOnlyList<Diagnostic> Check(string file, string text, Dish dish, LoadedData data, SchemaSet schemas)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(file);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(dish);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(schemas);

        var schemaIssues = schemas.Validate(DataLoader.DishSchemaName, text);
        if (schemaIssues.Count > 0)
        {
            return [.. schemaIssues.Select(i => new Diagnostic(Severity.Error, DiagnosticCodes.Schema, file, i.Location, $"{i.Keyword}: {i.Message}"))];
        }

        var alone = data with { Dishes = [new LoadedFile<Dish>(file, dish)], Diagnostics = [] };

        return [.. DataValidator.Validate(alone, ValidationMode.Dev).Diagnostics.Where(d => d.File == file)];
    }
}
