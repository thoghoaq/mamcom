using MamCom.Core.Models;
using MamCom.DataBuilder.Loading;
using MamCom.DataBuilder.Schemas;
using MamCom.DataBuilder.Validation;

namespace MamCom.RecipeDrafter.Drafting;

/// <summary>
/// Holds a draft to the rules the shipped data is held to, through <see cref="DishCheck"/>, and words the findings the way a
/// draft report does.
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

        return [.. DishCheck.Check(DraftFile(dish.Id), draftText, dish, data, schemas)
            .Select(d => new DraftProblem(d.Severity == Severity.Error ? "error" : "warning", d.Code, d.Location, d.Message))];
    }
}
