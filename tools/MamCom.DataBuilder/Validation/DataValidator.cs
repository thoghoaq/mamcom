using System.Text.Json;
using MamCom.Core.Json;
using MamCom.Core.Models;
using MamCom.Core.Nutrition;
using MamCom.DataBuilder.Loading;

namespace MamCom.DataBuilder.Validation;

/// <summary>The outcome of checking the data folder.</summary>
public sealed record ValidationReport(ValidationMode Mode, LoadedData Data, IReadOnlyList<Diagnostic> Diagnostics)
{
    public IEnumerable<Diagnostic> Errors => Diagnostics.Where(d => d.Severity == Severity.Error);

    public IEnumerable<Diagnostic> Warnings => Diagnostics.Where(d => d.Severity == Severity.Warning);

    public bool HasErrors => Diagnostics.Any(d => d.Severity == Severity.Error);
}

/// <summary>
/// The rules of SPEC §7.3 that need more than one file or more than a schema (ADR-010 lists what the schemas already
/// cover). A release build adds the placeholder and review rules and treats every warning as an error.
/// </summary>
public static class DataValidator
{
    // Display texts of market units that are weights, for the showApproxGrams warning.
    private static readonly HashSet<string> WeightUnitLabels = new(StringComparer.OrdinalIgnoreCase) { "g", "gam", "kg", "lạng", "lượng", "cân" };

    public static ValidationReport Validate(LoadedData data, ValidationMode mode)
    {
        ArgumentNullException.ThrowIfNull(data);

        var found = new List<Diagnostic>(data.Diagnostics);
        var ingredientsById = IndexById(data.Ingredients, found);
        _ = IndexById(data.Dishes, found);   // duplicate dish ids are reported while indexing

        CheckFileNames(data.Ingredients, i => i.Id, found);
        CheckFileNames(data.Dishes, d => d.Id, found);
        CheckImages(data, found);

        foreach (var (file, dish) in data.Dishes)
        {
            CheckDish(file, dish, ingredientsById, data, found);
        }

        foreach (var (file, ingredient) in data.Ingredients)
        {
            CheckIngredient(file, ingredient, found);
        }

        CheckUnusedIngredients(data, found);

        if (data.Config is { } config && !IsKnown(data, ingredientsById, config.Portion.RiceIngredientId))
        {
            found.Add(Error(DiagnosticCodes.RiceMissing, "config/portion-defaults.json", "/riceIngredientId", Messages.RiceMissing(config.Portion.RiceIngredientId)));
        }

        if (data.Config is not null && data.Validation is not null)
        {
            CheckNutritionRanges(data, ingredientsById, found);
        }

        if (mode == ValidationMode.Release)
        {
            CheckRelease(data, found);
        }

        return new ValidationReport(mode, data, Finish(found, mode));
    }

    private static List<Diagnostic> Finish(List<Diagnostic> found, ValidationMode mode)
    {
        IEnumerable<Diagnostic> all = found;
        if (mode == ValidationMode.Release)
        {
            all = all.Select(d => d with { Severity = Severity.Error });
        }

        return all
            .OrderBy(d => d.File, StringComparer.Ordinal)
            .ThenBy(d => d.Location, StringComparer.Ordinal)
            .ThenBy(d => d.Code, StringComparer.Ordinal)
            .ThenBy(d => d.Message, StringComparer.Ordinal)
            .ToList();
    }

    // ---- ids and file names -----------------------------------------------------------------------------

    private static Dictionary<string, T> IndexById<T>(IReadOnlyList<LoadedFile<T>> files, List<Diagnostic> found)
    {
        var byId = new Dictionary<string, T>(StringComparer.Ordinal);
        var fileOfId = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (file, value) in files)
        {
            var id = IdOf(value);
            if (fileOfId.TryGetValue(id, out var first))
            {
                found.Add(Error(DiagnosticCodes.DuplicateId, file, "/id", Messages.DuplicateId(id, first)));
                continue;
            }

            fileOfId[id] = file;
            byId[id] = value;
        }

        return byId;
    }

    private static string IdOf<T>(T value) => value switch
    {
        Ingredient ingredient => ingredient.Id,
        Dish dish => dish.Id,
        _ => throw new ArgumentException($"No id for {typeof(T).Name}.", nameof(value)),
    };

    private static void CheckFileNames<T>(IReadOnlyList<LoadedFile<T>> files, Func<T, string> idOf, List<Diagnostic> found)
    {
        foreach (var (file, value) in files)
        {
            var stem = Path.GetFileNameWithoutExtension(file);
            var id = idOf(value);
            if (!string.Equals(stem, id, StringComparison.Ordinal))
            {
                found.Add(Error(DiagnosticCodes.FileName, file, "/id", Messages.FileNameDiffersFromId(stem, id)));
            }
        }
    }

    // ---- images --------------------------------------------------------------------------------------------

    private static void CheckImages(LoadedData data, List<Diagnostic> found)
    {
        foreach (var (file, ingredient) in data.Ingredients)
        {
            CheckImage(data.Root, file, ingredient.Image, found);
        }

        foreach (var (file, dish) in data.Dishes)
        {
            CheckImage(data.Root, file, dish.Image, found);
        }
    }

    private static void CheckImage(string root, string file, ImageRef image, List<Diagnostic> found)
    {
        foreach (var (field, name) in new[] { ("thumb", image.Thumb), ("full", image.Full) })
        {
            if (name is null)
            {
                continue;
            }

            var location = $"/image/{field}";
            if (Path.IsPathRooted(name) || name.Contains('/') || name.Contains('\\') || name.Contains(".."))
            {
                found.Add(Error(DiagnosticCodes.ImagePath, file, location, Messages.ImagePath(name)));
            }
            else if (!File.Exists(Path.Combine(root, "images", "out", name)))
            {
                found.Add(Error(DiagnosticCodes.ImageMissing, file, location, Messages.ImageMissing(name)));
            }
        }
    }

    // ---- one dish -------------------------------------------------------------------------------------------

    private static void CheckDish(string file, Dish dish, Dictionary<string, Ingredient> ingredients, LoadedData data, List<Diagnostic> found)
    {
        var knownSeasons = data.Config is { } config
            ? new HashSet<string>(config.Spinner.SeasonByMonth.Append(SeasonIds.All), StringComparer.Ordinal)
            : null;

        for (var i = 0; i < dish.Seasons.Count; i++)
        {
            if (knownSeasons is not null && !knownSeasons.Contains(dish.Seasons[i]))
            {
                found.Add(Error(DiagnosticCodes.UnknownSeason, file, $"/seasons/{i}", Messages.UnknownSeason(dish.Seasons[i])));
            }
        }

        for (var i = 0; i < dish.Ingredients.Count; i++)
        {
            var id = dish.Ingredients[i].IngredientId;
            if (!IsKnown(data, ingredients, id))
            {
                found.Add(Error(DiagnosticCodes.UnknownIngredient, file, $"/ingredients/{i}/ingredientId", Messages.UnknownIngredient(id)));
            }
        }

        foreach (var group in dish.Ingredients.GroupBy(l => l.IngredientId).Where(g => g.Count() > 1))
        {
            var index = dish.Ingredients.ToList().FindLastIndex(l => l.IngredientId == group.Key);
            found.Add(Error(DiagnosticCodes.DuplicateLine, file, $"/ingredients/{index}/ingredientId", Messages.DuplicateLine(group.Key, group.Count())));
        }

        for (var s = 0; s < dish.Steps.Count; s++)
        {
            for (var i = 0; i < dish.Steps[s].IngredientIds.Count; i++)
            {
                var id = dish.Steps[s].IngredientIds[i];
                if (!IsKnown(data, ingredients, id))
                {
                    found.Add(Error(DiagnosticCodes.UnknownIngredient, file, $"/steps/{s}/ingredientIds/{i}", Messages.UnknownIngredient(id)));
                }
                else if (dish.Ingredients.All(l => l.IngredientId != id))
                {
                    found.Add(Warning(DiagnosticCodes.StepIngredientNotInRecipe, file, $"/steps/{s}/ingredientIds/{i}", Messages.StepIngredientNotInRecipe(dish.Steps[s].Order, id)));
                }
            }
        }

        var orders = dish.Steps.Select(s => s.Order).Order().ToArray();
        if (!orders.SequenceEqual(Enumerable.Range(1, orders.Length)))
        {
            found.Add(Error(DiagnosticCodes.StepOrder, file, "/steps", Messages.StepOrder(orders.Length, dish.Steps.Select(s => s.Order))));
        }

        var usedInSteps = dish.Steps.SelectMany(s => s.IngredientIds).ToHashSet(StringComparer.Ordinal);
        for (var i = 0; i < dish.Ingredients.Count; i++)
        {
            var id = dish.Ingredients[i].IngredientId;
            if (!usedInSteps.Contains(id))
            {
                found.Add(Warning(DiagnosticCodes.LineNotInSteps, file, $"/ingredients/{i}/ingredientId", Messages.LineNotInSteps(id)));
            }
        }
    }

    /// <summary>An ingredient exists when its file loaded, or when it is there but broken (that is reported once, on its own file).</summary>
    private static bool IsKnown(LoadedData data, Dictionary<string, Ingredient> ingredients, string id) =>
        ingredients.ContainsKey(id) || data.RejectedIngredientIds.Contains(id);

    // ---- one ingredient -------------------------------------------------------------------------------------

    private static void CheckIngredient(string file, Ingredient ingredient, List<Diagnostic> found)
    {
        foreach (var (key, unit) in ingredient.MarketUnit)
        {
            if (unit.ShowApproxGrams && WeightUnitLabels.Contains(unit.Display))
            {
                found.Add(Warning(DiagnosticCodes.ApproxOnWeightUnit, file, $"/marketUnit/{key}/showApproxGrams", Messages.ApproxOnWeightUnit(unit.Display)));
            }
        }
    }

    private static void CheckUnusedIngredients(LoadedData data, List<Diagnostic> found)
    {
        var used = data.Dishes.SelectMany(d => d.Value.Steps).SelectMany(s => s.IngredientIds).ToHashSet(StringComparer.Ordinal);

        // Rice is a pseudo-dish in the cook plan, so no recipe step names it.
        var rice = data.Config?.Portion.RiceIngredientId;
        foreach (var (file, ingredient) in data.Ingredients)
        {
            if (!used.Contains(ingredient.Id) && !string.Equals(ingredient.Id, rice, StringComparison.Ordinal))
            {
                found.Add(Warning(DiagnosticCodes.UnusedIngredient, file, string.Empty, Messages.UnusedIngredient()));
            }
        }
    }

    // ---- nutrition ranges -------------------------------------------------------------------------------------

    private static void CheckNutritionRanges(LoadedData data, Dictionary<string, Ingredient> ingredients, List<Diagnostic> found)
    {
        var calculator = new NutritionCalculator(data.Config!.Nutrition, data.Config.Portion);
        var thresholds = data.Validation!;

        foreach (var (file, dish) in data.Dishes)
        {
            if (dish.Ingredients.Any(l => !ingredients.ContainsKey(l.IngredientId)))
            {
                continue;   // already reported as an unknown ingredient
            }

            var perServing = calculator.ForDish(dish, ingredients).PerServing;

            if (perServing.Kcal is { } kcal && !thresholds.KcalPerServing.For(dish.Slot).Contains(kcal))
            {
                found.Add(Warning(DiagnosticCodes.KcalRange, file, string.Empty, Messages.KcalRange(kcal, thresholds.KcalPerServing.For(dish.Slot), SlotName(dish.Slot))));
            }

            if (perServing.SodiumMg is { } sodium && sodium > thresholds.MaxSodiumMgPerServing)
            {
                found.Add(Warning(DiagnosticCodes.SodiumHigh, file, string.Empty, Messages.SodiumHigh(sodium, thresholds.MaxSodiumMgPerServing)));
            }

            if (dish.Slot == DishSlot.Main)
            {
                var proteinGrams = dish.Ingredients
                    .Where(l => ingredients[l.IngredientId].ProteinSource is not null)
                    .Sum(l => l.GramsEdiblePerServing);

                if (!thresholds.MainProteinGramsPerServing.Contains(proteinGrams))
                {
                    found.Add(Warning(DiagnosticCodes.ProteinRange, file, string.Empty, Messages.ProteinRange(proteinGrams, thresholds.MainProteinGramsPerServing)));
                }
            }
        }
    }

    private static string SlotName(DishSlot slot) => JsonSerializer.Serialize(slot, CoreJson.Options).Trim('"');

    // ---- release only -----------------------------------------------------------------------------------------

    private static void CheckRelease(LoadedData data, List<Diagnostic> found)
    {
        foreach (var (file, ingredient) in data.Ingredients)
        {
            if (ingredient.NutritionSource.Db == NutritionDbs.Placeholder)
            {
                found.Add(Error(DiagnosticCodes.ReleasePlaceholder, file, "/nutritionSource/db", Messages.ReleasePlaceholder()));
            }
        }

        foreach (var (file, dish) in data.Dishes)
        {
            if (dish.Status < DishStatus.Reviewed)
            {
                var name = JsonSerializer.Serialize(dish.Status, CoreJson.Options).Trim('"');
                found.Add(Error(DiagnosticCodes.ReleaseStatus, file, "/status", Messages.ReleaseStatus(name)));
            }
        }
    }

    private static Diagnostic Error(string code, string file, string location, string message) => new(Severity.Error, code, file, location, message);

    private static Diagnostic Warning(string code, string file, string location, string message) => new(Severity.Warning, code, file, location, message);
}
