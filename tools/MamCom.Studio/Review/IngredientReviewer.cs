using System.Globalization;
using MamCom.Core;
using MamCom.Core.Models;
using MamCom.DataBuilder.Loading;
using MamCom.DataBuilder.Validation;

namespace MamCom.Studio.Review;

public enum IngredientStatus
{
    Saved,
    Unchanged,

    /// <summary>A person chose one of FdcImporter's candidates: its numbers and where they came from are in the file.</summary>
    NutritionChosen,

    /// <summary>The numbers and their source were taken out: the ingredient is a placeholder again.</summary>
    NutritionCleared,

    NotFound,

    /// <summary>The file changed on disk after it was read, so what the person saw is not what is there.</summary>
    Conflict,

    /// <summary>The ingredient breaks a rule; the findings say which.</summary>
    HasErrors,

    /// <summary>A person's name is needed to say who checked the numbers.</summary>
    VerifierMissing,

    /// <summary>There is no such candidate in the ingredient's suggestion (or no suggestion at all).</summary>
    NoSuchCandidate,

    /// <summary>The candidate states none of the six nutrients, so choosing it would call an empty entry verified.</summary>
    CandidateHasNoNumbers,

    /// <summary>The data folder cannot be read well enough to check an ingredient.</summary>
    DataNotUsable,
}

/// <param name="Stored">The file as it is after the call; the current one on <see cref="IngredientStatus.Conflict"/> and <see cref="IngredientStatus.HasErrors"/>; null when there is none.</param>
public sealed record IngredientResult(IngredientStatus Status, StoredIngredient? Stored, IReadOnlyList<Diagnostic> Findings);

/// <summary>What the machine says about an ingredient: the findings of the checks and the dishes that use it.</summary>
public sealed record IngredientEvaluation(IReadOnlyList<Diagnostic> Findings, IReadOnlyList<UsedByDish> UsedBy)
{
    public int Errors => Findings.Count(f => f.Severity == Severity.Error);

    public int Warnings => Findings.Count(f => f.Severity == Severity.Warning);

    public bool HasErrors => Errors > 0;
}

public sealed record UsedByDish(string Id, string Name);

/// <param name="Name">Null when the file is not an ingredient (see <paramref name="Problem"/>).</param>
/// <param name="NutritionDb">The database the numbers are from, <c>placeholder</c> while there is none.</param>
/// <param name="Candidates">How many candidates FdcImporter suggested; null when there is no suggestion for it.</param>
public sealed record IngredientEntry(
    string Id,
    string? Name,
    string? NutritionDb,
    string? VerifiedBy,
    int? Candidates,
    int Errors,
    int Warnings,
    int UsedBy,
    bool HasImage,
    string? Problem)
{
    public bool IsPlaceholder => NutritionDb == NutritionDbs.Placeholder;
}

public sealed record IngredientCatalog(Kitchen Kitchen, IReadOnlyList<IngredientEntry> Entries);

/// <summary>
/// What a person does in Studio with ingredients: look at them, edit them, and choose where their nutrition numbers come from.
/// The rules live here. Numbers never come from the screen: they are copied from the candidate FdcImporter wrote down, and
/// only with the name of the person who checked them. Saving an edit never touches the numbers or their source. A file is
/// never written over a version the person has not seen.
/// </summary>
public sealed class IngredientReviewer(IngredientStore store, KitchenLoader loader, IClock clock)
{
    private readonly object _gate = new();

    public IngredientStore Store { get; } = store;

    public Kitchen LoadKitchen() => loader.Load();

    /// <summary>Every ingredient file, with where its numbers stand and how it does against the checks. Sorted by id.</summary>
    public IngredientCatalog Catalog()
    {
        var kitchen = loader.Load();
        var entries = new List<IngredientEntry>();
        foreach (var id in Store.Ids())
        {
            if (Store.Read(id) is { } stored)
            {
                entries.Add(EntryOf(kitchen, stored));
            }
        }

        return new IngredientCatalog(kitchen, entries);
    }

    /// <summary>The checks for an ingredient as it would be written, and the dishes that use it.</summary>
    public static IngredientEvaluation Evaluate(Kitchen kitchen, Ingredient ingredient)
    {
        ArgumentNullException.ThrowIfNull(kitchen);
        ArgumentNullException.ThrowIfNull(ingredient);

        var findings = kitchen.IsUsable
            ? IngredientCheck.Check(DataFolder.RelativeIngredientFile(ingredient.Id), IngredientFile.Serialize(ingredient), ingredient, kitchen.Data, kitchen.Schemas!)
            : [];

        var usedBy = kitchen.Data.Dishes
            .Where(d => d.Value.Ingredients.Any(l => l.IngredientId == ingredient.Id))
            .Select(d => new UsedByDish(d.Value.Id, d.Value.Name))
            .OrderBy(d => d.Id, StringComparer.Ordinal)
            .ToArray();

        return new IngredientEvaluation(findings, usedBy);
    }

    /// <summary>
    /// Saves an edited ingredient. The id, the numbers and their source are not the editor's to change and stay as they are on
    /// disk; the editor changes names, units, waste, pantry, category, protein source, aisle and image.
    /// </summary>
    public IngredientResult Save(string id, Ingredient edited, string expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(edited);
        ArgumentNullException.ThrowIfNull(expectedVersion);

        lock (_gate)
        {
            if (Begin(id, expectedVersion, out var kitchen, out var current, out var onDisk) is { } stop)
            {
                return stop;
            }

            var candidate = edited with { Id = id, NutritionPer100gEdible = onDisk.NutritionPer100gEdible, NutritionSource = onDisk.NutritionSource };
            var findings = Evaluate(kitchen, candidate).Findings;
            if (findings.Any(f => f.Severity == Severity.Error))
            {
                return new IngredientResult(IngredientStatus.HasErrors, current, findings);
            }

            return WriteIfChanged(id, current, candidate, IngredientStatus.Saved, findings);
        }
    }

    /// <summary>
    /// A person's choice of one of FdcImporter's candidates (<paramref name="fdcId"/>): its six numbers go into the ingredient
    /// exactly as FDC states them, with the database, the FDC id, the name FDC gave, who checked and today's date.
    /// </summary>
    public IngredientResult ChooseNutrition(string id, long fdcId, string? verifier, string expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(expectedVersion);

        lock (_gate)
        {
            if (Begin(id, expectedVersion, out var kitchen, out var current, out var onDisk) is { } stop)
            {
                return stop;
            }

            if (string.IsNullOrWhiteSpace(verifier))
            {
                return new IngredientResult(IngredientStatus.VerifierMissing, current, []);
            }

            var candidate = Store.ReadSuggestion(id)?.Candidates.FirstOrDefault(c => c.FdcId == fdcId);
            if (candidate is null)
            {
                return new IngredientResult(IngredientStatus.NoSuchCandidate, current, []);
            }

            if (candidate.HasNoNumbers)
            {
                return new IngredientResult(IngredientStatus.CandidateHasNoNumbers, current, []);
            }

            var chosen = onDisk with
            {
                NutritionPer100gEdible = candidate.Per100g,
                NutritionSource = new NutritionSource("usda-fdc", fdcId.ToString(CultureInfo.InvariantCulture), candidate.Description, verifier.Trim(), clock.Today),
            };

            var findings = Evaluate(kitchen, chosen).Findings;
            if (findings.Any(f => f.Severity == Severity.Error))
            {
                return new IngredientResult(IngredientStatus.HasErrors, current, findings);
            }

            return WriteIfChanged(id, current, chosen, IngredientStatus.NutritionChosen, findings);
        }
    }

    /// <summary>Takes the numbers and their source out, so the ingredient is a placeholder again (a choice made by mistake).</summary>
    public IngredientResult ClearNutrition(string id, string expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(expectedVersion);

        lock (_gate)
        {
            if (Begin(id, expectedVersion, out var kitchen, out var current, out var onDisk) is { } stop)
            {
                return stop;
            }

            var cleared = onDisk with
            {
                NutritionPer100gEdible = new NutritionFacts(null, null, null, null, null, null),
                NutritionSource = new NutritionSource(NutritionDbs.Placeholder, null, null, null, null),
            };

            var findings = Evaluate(kitchen, cleared).Findings;
            return WriteIfChanged(id, current, cleared, IngredientStatus.NutritionCleared, findings);
        }
    }

    /// <summary>The steps every change starts with; returns the result to give back when the change cannot go on, else null.</summary>
    private IngredientResult? Begin(string id, string expectedVersion, out Kitchen kitchen, out StoredIngredient current, out Ingredient onDisk)
    {
        kitchen = loader.Load();
        current = null!;
        onDisk = null!;

        if (!kitchen.IsUsable)
        {
            return new IngredientResult(IngredientStatus.DataNotUsable, null, []);
        }

        if (Store.Read(id) is not { Ingredient: { } parsed } stored)
        {
            return new IngredientResult(IngredientStatus.NotFound, null, []);
        }

        current = stored;
        onDisk = parsed;
        return stored.Version == expectedVersion ? null : new IngredientResult(IngredientStatus.Conflict, stored, []);
    }

    private IngredientResult WriteIfChanged(string id, StoredIngredient current, Ingredient changed, IngredientStatus done, IReadOnlyList<Diagnostic> findings)
    {
        var text = IngredientFile.Serialize(changed);
        if (text == current.Text)
        {
            return new IngredientResult(IngredientStatus.Unchanged, current, findings);
        }

        Store.Write(id, text);
        return new IngredientResult(done, Store.Read(id), findings);
    }

    private IngredientEntry EntryOf(Kitchen kitchen, StoredIngredient stored)
    {
        var suggestion = Store.ReadSuggestion(stored.Id);
        if (stored.Ingredient is not { } ingredient)
        {
            return new IngredientEntry(stored.Id, null, null, null, suggestion?.Candidates.Count, 0, 0, 0, false, stored.Problem);
        }

        var evaluation = Evaluate(kitchen, ingredient);
        return new IngredientEntry(
            stored.Id,
            ingredient.Names.GetValueOrDefault(RegionKeys.Default) ?? ingredient.Names.Values.FirstOrDefault() ?? stored.Id,
            ingredient.NutritionSource.Db,
            ingredient.NutritionSource.VerifiedBy,
            suggestion?.Candidates.Count,
            evaluation.Errors,
            evaluation.Warnings,
            evaluation.UsedBy.Count,
            ingredient.Image.Thumb is not null || ingredient.Image.Full is not null,
            null);
    }
}
