using System.Text.Encodings.Web;
using System.Text.Json;
using MamCom.Core.Models;

namespace MamCom.FdcImporter.Suggestions;

/// <summary>
/// One FDC food offered as the nutrition source of an ingredient. The numbers are FDC's, per 100 g, unchanged; nothing
/// here is applied to an ingredient file until a person chooses it in MamCom.Studio.
/// </summary>
/// <param name="Rank">1 is the importer's first choice.</param>
/// <param name="Missing">JSON names of the nutrients this food does not state.</param>
/// <param name="Reason">Why it was ranked here, in Vietnamese, for the person choosing.</param>
public sealed record NutritionCandidate(
    int Rank,
    long FdcId,
    string Description,
    string DataType,
    string? FoodCategory,
    string DetailsUrl,
    NutritionFacts Per100g,
    string? KcalSource,
    IReadOnlyList<string> Missing,
    string Reason);

/// <summary>The content of <c>data/_suggestions/nutrition/&lt;ingredientId&gt;.json</c>.</summary>
/// <param name="Query">The English text sent to FDC.</param>
/// <param name="RetrievedAt">The day FDC was asked.</param>
/// <param name="Source">Nutrition database id, as in <c>nutritionSource.db</c> of an ingredient.</param>
/// <param name="TotalHits">How many foods FDC found for the query, before the ranking kept the best few.</param>
public sealed record NutritionSuggestions(
    string IngredientId,
    string Query,
    DateOnly RetrievedAt,
    string Source,
    int TotalHits,
    IReadOnlyList<NutritionCandidate> Candidates)
{
    public const string UsdaFdc = "usda-fdc";
}

/// <summary>Where suggestion files live and how they are written.</summary>
public static class SuggestionFile
{
    // Two-space indent, LF, nulls written, Vietnamese unescaped: the same shape as the other data files.
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        IndentSize = 2,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string PathFor(string dataRoot, string ingredientId) =>
        Path.Combine(dataRoot, "_suggestions", "nutrition", ingredientId + ".json");

    public static bool Exists(string dataRoot, string ingredientId) => File.Exists(PathFor(dataRoot, ingredientId));

    /// <summary>The file text: indented JSON ending with one line feed.</summary>
    public static string Serialize(NutritionSuggestions suggestions) => JsonSerializer.Serialize(suggestions, Options) + "\n";

    /// <summary>
    /// Writes the file, creating the folders. It writes a temporary file and moves it into place, so a run that is killed
    /// leaves the old file or the new one, never half of one. Never touches anything outside <c>_suggestions/nutrition</c>.
    /// </summary>
    public static string Write(string dataRoot, NutritionSuggestions suggestions)
    {
        var path = PathFor(dataRoot, suggestions.IngredientId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var temporary = path + ".tmp";
        File.WriteAllText(temporary, Serialize(suggestions), new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.Move(temporary, path, overwrite: true);
        return path;
    }
}
