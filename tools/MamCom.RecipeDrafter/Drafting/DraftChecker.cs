using MamCom.Core.Models;
using MamCom.DataBuilder.Loading;
using MamCom.DataBuilder.Schemas;
using MamCom.DataBuilder.Validation;

namespace MamCom.RecipeDrafter.Drafting;

/// <summary>
/// Holds a draft to the rules the shipped data is held to: the dish schema, then the DataBuilder rules that look at a
/// single dish against the ingredient list (unknown ingredient, season, step order, repeated line, protein range…).
/// The rest of the data is not the draft's business, so only findings about the draft itself count.
/// </summary>
public static class DraftChecker
{
    public static string DraftFile(string dishId) => $"dishes/_drafts/{dishId}.json";

    /// <param name="draftText">The draft file as it will be written.</param>
    /// <param name="data">The loaded data folder: the ingredient list and the config the rules need.</param>
    public static IReadOnlyList<DraftProblem> Check(string draftText, Dish dish, LoadedData data, SchemaSet schemas)
    {
        ArgumentNullException.ThrowIfNull(draftText);
        ArgumentNullException.ThrowIfNull(dish);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(schemas);

        var schemaIssues = schemas.Validate(DataLoader.DishSchemaName, draftText);
        if (schemaIssues.Count > 0)
        {
            return [.. schemaIssues.Select(i => new DraftProblem("error", DiagnosticCodes.Schema, i.Location, $"{i.Keyword}: {i.Message}"))];
        }

        var file = DraftFile(dish.Id);
        var alone = data with { Dishes = [new LoadedFile<Dish>(file, dish)], Diagnostics = [] };

        return [.. DataValidator.Validate(alone, ValidationMode.Dev).Diagnostics
            .Where(d => d.File == file)
            .Select(d => new DraftProblem(d.Severity == Severity.Error ? "error" : "warning", d.Code, d.Location, d.Message))];
    }
}
