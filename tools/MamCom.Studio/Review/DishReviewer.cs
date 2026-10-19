using MamCom.Core;
using MamCom.Core.Models;
using MamCom.Core.Nutrition;
using MamCom.DataBuilder.Loading;
using MamCom.DataBuilder.Validation;

namespace MamCom.Studio.Review;

public enum ReviewStatus
{
    Saved,
    Unchanged,
    Approved,
    Reopened,
    NotFound,

    /// <summary>The file changed on disk after it was read, so what the person saw is not what is there.</summary>
    Conflict,

    /// <summary>Only a draft or a validated dish can be edited.</summary>
    NotEditable,

    /// <summary>Only a draft or a validated dish can be approved.</summary>
    NotApprovable,

    /// <summary>Only a reviewed dish can be reopened.</summary>
    NotReopenable,

    ReviewerMissing,

    /// <summary>The dish breaks a rule; the findings say which.</summary>
    HasErrors,

    /// <summary>A draft cannot become a dish because <c>dishes/&lt;id&gt;.json</c> already exists.</summary>
    FinalExists,

    /// <summary>The data folder cannot be read well enough to check a dish (see <see cref="Kitchen.Problems"/>).</summary>
    DataNotUsable,
}

/// <param name="Stored">The file as it is after the call; the current one on <see cref="ReviewStatus.Conflict"/> and <see cref="ReviewStatus.HasErrors"/>; null when there is none.</param>
/// <param name="Findings">What the checks found, for <see cref="ReviewStatus.HasErrors"/> and for the calls that wrote a file.</param>
public sealed record ReviewResult(ReviewStatus Status, StoredDish? Stored, IReadOnlyList<Diagnostic> Findings);

/// <summary>What the machine says about a dish: the findings of the checks and the nutrition Core adds up.</summary>
/// <param name="Nutrition">One serving; null when it cannot be added up (see <paramref name="NutritionProblem"/>).</param>
public sealed record Evaluation(IReadOnlyList<Diagnostic> Findings, DishNutrition? Nutrition, string? NutritionProblem)
{
    public int Errors => Findings.Count(f => f.Severity == Severity.Error);

    public int Warnings => Findings.Count(f => f.Severity == Severity.Warning);

    public bool HasErrors => Errors > 0;
}

/// <param name="Name">Null when the file is not a dish (see <paramref name="Problem"/>).</param>
/// <param name="Note">What RecipeDrafter said about the draft; null for a dish that was not drafted or has no note.</param>
public sealed record DishEntry(
    DishLocation Location,
    string Id,
    string? Name,
    DishSlot? Slot,
    DishStatus? Status,
    int Errors,
    int Warnings,
    string? Problem,
    DraftNote? Note);

/// <param name="FailedDrafts">Ids RecipeDrafter tried and failed, which have no draft and no dish: the next run of it tries them again.</param>
public sealed record DishCatalog(Kitchen Kitchen, IReadOnlyList<DishEntry> Entries, IReadOnlyList<string> FailedDrafts);

/// <summary>
/// What a person does in Studio with dishes: look at them, edit them, approve them. The rules live here: a dish is only saved
/// when the checks find no error (and is then <c>validated</c>), only a person's call to <see cref="Approve"/> sets
/// <c>reviewed</c> and writes who and when, and a file is never written over a version the person has not seen.
/// </summary>
public sealed class DishReviewer(DishStore store, KitchenLoader loader, IClock clock)
{
    private readonly object _gate = new();

    public DishStore Store { get; } = store;

    public Kitchen LoadKitchen() => loader.Load();

    /// <summary>Every dish file, with how it stands against the checks. Sorted by id, dishes before drafts of the same id.</summary>
    public DishCatalog Catalog()
    {
        var kitchen = loader.Load();
        var entries = new List<DishEntry>();

        foreach (var location in new[] { DishLocation.Data, DishLocation.Draft })
        {
            foreach (var id in Store.Ids(location))
            {
                if (Store.Read(location, id) is { } stored)
                {
                    entries.Add(EntryOf(kitchen, stored));
                }
            }
        }

        var failed = Store.ReportIds()
            .Where(id => !Store.Exists(DishLocation.Data, id) && !Store.Exists(DishLocation.Draft, id))
            .Where(id => Store.ReadNote(id) is { Validation: DraftNote.Failed })
            .ToArray();

        // OrderBy is stable, so a dish stays ahead of a draft with the same id (the dishes were added first).
        return new DishCatalog(kitchen, [.. entries.OrderBy(e => e.Id, StringComparer.Ordinal)], failed);
    }

    /// <summary>The checks and the nutrition for a dish as it would be written to the file of <paramref name="location"/>.</summary>
    public static Evaluation Evaluate(Kitchen kitchen, DishLocation location, Dish dish)
    {
        ArgumentNullException.ThrowIfNull(kitchen);
        ArgumentNullException.ThrowIfNull(dish);

        var findings = kitchen.IsUsable
            ? DishCheck.Check(DataFolder.RelativeFile(location, dish.Id), DishFile.Serialize(dish), dish, kitchen.Data, kitchen.Schemas!)
            : [];

        DishNutrition? nutrition = null;
        string? problem = null;
        if (kitchen.Nutrition is { } calculator)
        {
            try
            {
                nutrition = calculator.ForDish(dish, kitchen.Ingredients);
            }
            catch (InvalidOperationException ex)
            {
                problem = ex.Message;
            }
        }

        return new Evaluation(findings, nutrition, problem);
    }

    /// <summary>Saves an edited dish. Id, status, provenance and image are not the editor's to change and are kept as they are on disk.</summary>
    public ReviewResult Save(DishLocation location, string id, Dish edited, string expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(edited);
        ArgumentNullException.ThrowIfNull(expectedVersion);

        lock (_gate)
        {
            var kitchen = loader.Load();
            if (!kitchen.IsUsable)
            {
                return new ReviewResult(ReviewStatus.DataNotUsable, null, []);
            }

            var current = Store.Read(location, id);
            if (current is null)
            {
                return new ReviewResult(ReviewStatus.NotFound, null, []);
            }

            if (current.Version != expectedVersion)
            {
                return new ReviewResult(ReviewStatus.Conflict, current, []);
            }

            if (current.Dish is not { Status: DishStatus.Draft or DishStatus.Validated } onDisk)
            {
                return new ReviewResult(ReviewStatus.NotEditable, current, []);
            }

            var candidate = edited with { Id = id, Status = DishStatus.Validated, Provenance = onDisk.Provenance, Image = onDisk.Image };
            var findings = Evaluate(kitchen, location, candidate).Findings;
            if (findings.Any(f => f.Severity == Severity.Error))
            {
                return new ReviewResult(ReviewStatus.HasErrors, current, findings);
            }

            var text = DishFile.Serialize(candidate);
            if (text == current.Text)
            {
                return new ReviewResult(ReviewStatus.Unchanged, current, findings);
            }

            Store.Write(location, id, text);
            return new ReviewResult(ReviewStatus.Saved, Store.Read(location, id), findings);
        }
    }

    /// <summary>
    /// A person's approval: <c>reviewed</c>, with the reviewer's name and today's date, and nothing else about the dish changed.
    /// A draft becomes a dish: it is written to <c>dishes/</c> and the draft and its note are removed.
    /// </summary>
    public ReviewResult Approve(DishLocation location, string id, string? reviewer, string expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(expectedVersion);

        lock (_gate)
        {
            var kitchen = loader.Load();
            if (!kitchen.IsUsable)
            {
                return new ReviewResult(ReviewStatus.DataNotUsable, null, []);
            }

            var current = Store.Read(location, id);
            if (current is null)
            {
                return new ReviewResult(ReviewStatus.NotFound, null, []);
            }

            if (current.Version != expectedVersion)
            {
                return new ReviewResult(ReviewStatus.Conflict, current, []);
            }

            if (current.Dish is not { Status: DishStatus.Draft or DishStatus.Validated } onDisk)
            {
                return new ReviewResult(ReviewStatus.NotApprovable, current, []);
            }

            if (string.IsNullOrWhiteSpace(reviewer))
            {
                return new ReviewResult(ReviewStatus.ReviewerMissing, current, []);
            }

            var approved = onDisk with
            {
                Status = DishStatus.Reviewed,
                Provenance = onDisk.Provenance with { ReviewedBy = reviewer.Trim(), ReviewedAt = clock.Today },
            };

            var findings = Evaluate(kitchen, DishLocation.Data, approved).Findings;
            if (findings.Any(f => f.Severity == Severity.Error))
            {
                return new ReviewResult(ReviewStatus.HasErrors, current, findings);
            }

            if (location == DishLocation.Draft && Store.Exists(DishLocation.Data, id))
            {
                return new ReviewResult(ReviewStatus.FinalExists, current, findings);
            }

            Store.Write(DishLocation.Data, id, DishFile.Serialize(approved));
            if (location == DishLocation.Draft)
            {
                Store.DeleteDraft(id);
            }

            return new ReviewResult(ReviewStatus.Approved, Store.Read(DishLocation.Data, id), findings);
        }
    }

    /// <summary>Takes a reviewed dish back to <c>draft</c> so it can be edited: the reviewer and date are cleared.</summary>
    public ReviewResult Reopen(DishLocation location, string id, string expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(expectedVersion);

        lock (_gate)
        {
            var current = Store.Read(location, id);
            if (current is null)
            {
                return new ReviewResult(ReviewStatus.NotFound, null, []);
            }

            if (current.Version != expectedVersion)
            {
                return new ReviewResult(ReviewStatus.Conflict, current, []);
            }

            if (current.Dish is not { Status: DishStatus.Reviewed } onDisk)
            {
                return new ReviewResult(ReviewStatus.NotReopenable, current, []);
            }

            var reopened = onDisk with
            {
                Status = DishStatus.Draft,
                Provenance = onDisk.Provenance with { ReviewedBy = null, ReviewedAt = null },
            };

            Store.Write(location, id, DishFile.Serialize(reopened));
            return new ReviewResult(ReviewStatus.Reopened, Store.Read(location, id), []);
        }
    }

    private DishEntry EntryOf(Kitchen kitchen, StoredDish stored)
    {
        var note = stored.Location == DishLocation.Draft ? Store.ReadNote(stored.Id) : null;
        if (stored.Dish is not { } dish)
        {
            return new DishEntry(stored.Location, stored.Id, null, null, null, 0, 0, stored.Problem, note);
        }

        var evaluation = Evaluate(kitchen, stored.Location, dish);
        return new DishEntry(stored.Location, stored.Id, dish.Name, dish.Slot, dish.Status, evaluation.Errors, evaluation.Warnings, null, note);
    }
}
