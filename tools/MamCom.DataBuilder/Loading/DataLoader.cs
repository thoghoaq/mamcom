using System.Text.Json;
using MamCom.Core.Cooking;
using MamCom.Core.Data;
using MamCom.Core.Json;
using MamCom.Core.Models;
using MamCom.Core.Nutrition;
using MamCom.Core.Portions;
using MamCom.Core.Spinning;
using MamCom.DataBuilder.Schemas;
using MamCom.DataBuilder.Validation;

namespace MamCom.DataBuilder.Loading;

/// <summary>A parsed data file and where it came from.</summary>
/// <param name="File">Path under the data folder with forward slashes, for example <c>dishes/thit-kho-trung.json</c>.</param>
public sealed record LoadedFile<T>(string File, T Value);

/// <summary>What was read from the data folder: the files that are well formed, and what was wrong with the rest.</summary>
/// <param name="Config">The four runtime config files; null when any of them is missing or invalid.</param>
/// <param name="Validation">The warning thresholds; null when the file is missing or invalid.</param>
/// <param name="RejectedIngredientIds">
/// File names (without <c>.json</c>) of ingredient files that could not be loaded. The ingredient is there but broken,
/// so a dish that uses it is not also reported as using an unknown ingredient.
/// </param>
public sealed record LoadedData(
    string Root,
    BundleConfig? Config,
    ValidationConfig? Validation,
    IReadOnlyList<LoadedFile<Ingredient>> Ingredients,
    IReadOnlyList<LoadedFile<Dish>> Dishes,
    IReadOnlySet<string> RejectedIngredientIds,
    IReadOnlyList<Diagnostic> Diagnostics);

/// <summary>Reads <c>data/</c>: the config files, then every ingredient and dish, each checked against its schema first.</summary>
public static class DataLoader
{
    public const string PortionConfigName = "portion-defaults";
    public const string NutritionConfigName = "nutrition-defaults";
    public const string SpinnerConfigName = "spinner-defaults";
    public const string CookingConfigName = "cooking-defaults";
    public const string ValidationConfigName = "validation-defaults";
    public const string IngredientSchemaName = "ingredient";
    public const string DishSchemaName = "dish";

    private static readonly string[] RequiredSchemas =
    [
        IngredientSchemaName, DishSchemaName, PortionConfigName, NutritionConfigName, SpinnerConfigName, CookingConfigName, ValidationConfigName,
    ];

    /// <summary>
    /// Only the <c>.json</c> files directly inside <c>ingredients</c> and <c>dishes</c> are data; subfolders such as
    /// <c>dishes/_drafts</c> are not read.
    /// </summary>
    public static LoadedData Load(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        var diagnostics = new List<Diagnostic>();
        LoadedData Stop() => new(root, null, null, [], [], new HashSet<string>(), diagnostics);

        if (!Directory.Exists(root))
        {
            diagnostics.Add(Error(DiagnosticCodes.DataFolder, string.Empty, Messages.MissingDataFolder(root)));
            return Stop();
        }

        var schemaFolder = Path.Combine(root, "schemas");
        if (!Directory.Exists(schemaFolder))
        {
            diagnostics.Add(Error(DiagnosticCodes.DataFolder, string.Empty, Messages.MissingSubfolder("schemas")));
            return Stop();
        }

        SchemaSet schemas;
        try
        {
            schemas = SchemaSet.Load(schemaFolder);
        }
        catch (InvalidOperationException ex)
        {
            diagnostics.Add(Error(DiagnosticCodes.SchemaMissing, "schemas", Messages.UnusableSchemas(ex.Message)));
            return Stop();
        }

        foreach (var name in RequiredSchemas.Where(name => !schemas.Names.Contains(name)))
        {
            diagnostics.Add(Error(DiagnosticCodes.SchemaMissing, "schemas", Messages.MissingSchema(name)));
        }

        if (diagnostics.Count > 0)
        {
            return Stop();
        }

        var portion = ReadConfig(root, schemas, diagnostics, PortionConfigName, PortionConfig.Parse, c => c.Validate());
        var nutrition = ReadConfig(root, schemas, diagnostics, NutritionConfigName, NutritionConfig.Parse, c => c.Validate());
        var spinner = ReadConfig(root, schemas, diagnostics, SpinnerConfigName, SpinnerConfig.Parse, c => c.Validate());
        var cooking = ReadConfig(root, schemas, diagnostics, CookingConfigName, CookingConfig.Parse, c => c.Validate());
        var validation = ReadConfig(root, schemas, diagnostics, ValidationConfigName, ValidationConfig.Parse, c => c.Validate());

        var config = portion is null || nutrition is null || spinner is null || cooking is null
            ? null
            : new BundleConfig(portion, nutrition, spinner, cooking);

        var ingredients = ReadFolder<Ingredient>(root, "ingredients", IngredientSchemaName, schemas, diagnostics, out var rejected);
        var dishes = ReadFolder<Dish>(root, "dishes", DishSchemaName, schemas, diagnostics, out _);

        return new LoadedData(root, config, validation, ingredients, dishes, rejected, diagnostics);
    }

    private static T? ReadConfig<T>(string root, SchemaSet schemas, List<Diagnostic> diagnostics, string name, Func<string, T> parse, Action<T> validate)
        where T : class
    {
        var file = $"config/{name}.json";
        var path = Path.Combine(root, "config", name + ".json");
        if (!File.Exists(path))
        {
            diagnostics.Add(Error(DiagnosticCodes.ConfigMissing, file, Messages.MissingConfig(name)));
            return null;
        }

        var text = File.ReadAllText(path);
        if (AddSchemaIssues(diagnostics, file, schemas.Validate(name, text)))
        {
            return null;
        }

        try
        {
            var value = parse(text);
            validate(value);
            return value;
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            diagnostics.Add(Error(DiagnosticCodes.ConfigInvalid, file, Messages.InvalidConfig(ex.Message)));
            return null;
        }
    }

    private static List<LoadedFile<T>> ReadFolder<T>(
        string root, string folder, string schemaName, SchemaSet schemas, List<Diagnostic> diagnostics, out HashSet<string> rejectedNames)
    {
        var loaded = new List<LoadedFile<T>>();
        rejectedNames = new HashSet<string>(StringComparer.Ordinal);
        var path = Path.Combine(root, folder);
        if (!Directory.Exists(path))
        {
            diagnostics.Add(Error(DiagnosticCodes.DataFolder, folder, Messages.MissingSubfolder(folder)));
            return loaded;
        }

        foreach (var filePath in Directory.GetFiles(path, "*.json").Order(StringComparer.Ordinal))
        {
            var file = $"{folder}/{Path.GetFileName(filePath)}";
            var text = File.ReadAllText(filePath);
            var name = Path.GetFileNameWithoutExtension(filePath);
            if (AddSchemaIssues(diagnostics, file, schemas.Validate(schemaName, text)))
            {
                rejectedNames.Add(name);
                continue;
            }

            try
            {
                loaded.Add(new LoadedFile<T>(file, JsonSerializer.Deserialize<T>(text, CoreJson.Options)!));
            }
            catch (JsonException ex)
            {
                rejectedNames.Add(name);
                diagnostics.Add(Error(DiagnosticCodes.Parse, file, Messages.NotJson(ex.Message)));
            }
        }

        return loaded;
    }

    /// <returns>True when there was at least one issue.</returns>
    private static bool AddSchemaIssues(List<Diagnostic> diagnostics, string file, IReadOnlyList<SchemaIssue> issues)
    {
        foreach (var issue in issues)
        {
            diagnostics.Add(new Diagnostic(Severity.Error, DiagnosticCodes.Schema, file, issue.Location, $"{issue.Keyword}: {issue.Message}"));
        }

        return issues.Count > 0;
    }

    private static Diagnostic Error(string code, string file, string message) => new(Severity.Error, code, file, string.Empty, message);
}
