using MamCom.Core.Models;
using MamCom.DataBuilder.Loading;
using MamCom.DataBuilder.Schemas;

namespace MamCom.DataBuilder.Validation;

/// <summary>
/// Holds one ingredient to the rules the shipped data is held to: the ingredient schema first, then the DataBuilder rules that
/// look at a single ingredient (the file name is the id, the image files exist, a weight unit does not ask for "~Ng", an
/// ingredient no dish uses). Only findings about this ingredient count. What the loader found wrong with the same file on disk
/// is left out too, so an ingredient that is broken on disk can be corrected. It is how MamCom.Studio checks an ingredient
/// that has just been edited, the same way <see cref="DishCheck"/> does for a dish.
/// </summary>
public static class IngredientCheck
{
    /// <param name="file">Path to file the findings under, with forward slashes, for example <c>ingredients/gao.json</c>. Its name must be the ingredient id.</param>
    /// <param name="text">The ingredient as it would be written to the file; this is what the schema reads.</param>
    /// <param name="data">The loaded data folder: the dishes (for "no dish uses it"), the config and where the images are.</param>
    public static IReadOnlyList<Diagnostic> Check(string file, string text, Ingredient ingredient, LoadedData data, SchemaSet schemas)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(file);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(ingredient);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(schemas);

        var schemaIssues = schemas.Validate(DataLoader.IngredientSchemaName, text);
        if (schemaIssues.Count > 0)
        {
            return [.. schemaIssues.Select(i => new Diagnostic(Severity.Error, DiagnosticCodes.Schema, file, i.Location, $"{i.Keyword}: {i.Message}"))];
        }

        var alone = data with { Ingredients = [new LoadedFile<Ingredient>(file, ingredient)], Diagnostics = [] };

        return [.. DataValidator.Validate(alone, ValidationMode.Dev).Diagnostics.Where(d => d.File == file)];
    }
}
