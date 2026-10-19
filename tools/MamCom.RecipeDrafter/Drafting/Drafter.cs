using System.Globalization;
using System.Text.Json;
using MamCom.Core;
using MamCom.Core.Models;
using MamCom.DataBuilder.Loading;
using MamCom.DataBuilder.Schemas;
using MamCom.RecipeDrafter.Model;
using MamCom.RecipeDrafter.Plan;
using MamCom.RecipeDrafter.Templates;

namespace MamCom.RecipeDrafter.Drafting;

/// <summary>The two prompt files: the instructions shared by every dish, and the part that is each dish's own.</summary>
public sealed record PromptSet(string SystemTemplate, string DishTemplate)
{
    public const string SystemFileName = "system.md";
    public const string DishFileName = "dish.md";
}

/// <param name="Only">Draft only these dish ids; null means every dish in the list.</param>
/// <param name="Limit">Draft at most this many dishes in this run; null means no limit.</param>
/// <param name="Redo">Draft again the dishes that already have a draft. A dish a person has taken over is never redone.</param>
public sealed record DrafterOptions(IReadOnlyList<string>? Only, int? Limit, bool Redo);

public enum DraftOutcomeKind
{
    Drafted,
    SkippedFinal,
    SkippedDraft,
    Failed,
}

/// <param name="Validation">For a drafted dish: <c>valid</c>, <c>warnings</c> or <c>invalid</c>.</param>
public sealed record DishOutcome(string DishId, DraftOutcomeKind Kind, string? Validation, string Message);

/// <param name="StoppedBecause">Why the run ended early for a reason that needs attention (a refused key, a rate limit, an account with no credit left); null otherwise.</param>
/// <param name="LimitReached">The run stopped at <c>--limit</c>; there may be more dishes to draft.</param>
public sealed record DraftRunResult(IReadOnlyList<DishOutcome> Outcomes, string? StoppedBecause, bool LimitReached, int ModelCalls, TokenUsage Usage);

/// <summary>
/// Drafts the dishes of the owner's list one at a time and writes each draft as soon as it exists, so a run that is
/// stopped can simply be run again: dishes with a file or a draft are skipped. The model sees the ingredient list and
/// writes only what it may decide; the program checks the result against the dish schema and the DataBuilder rules and
/// writes the verdict next to the draft.
/// </summary>
public sealed class Drafter(IRecipeModel model, IClock clock)
{
    public async Task<DraftRunResult> RunAsync(
        string dataRoot,
        LoadedData data,
        SchemaSet schemas,
        IReadOnlyList<DishRequest> dishes,
        PromptSet prompts,
        DrafterOptions options,
        Action<DishOutcome> report,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(schemas);
        ArgumentNullException.ThrowIfNull(dishes);
        ArgumentNullException.ThrowIfNull(prompts);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(report);

        var config = data.Config ?? throw new InvalidOperationException("The config files could not be loaded, so nothing can be drafted.");
        var validation = data.Validation ?? throw new InvalidOperationException("The validation thresholds could not be loaded, so nothing can be drafted.");
        var ingredients = data.Ingredients.Select(f => f.Value).OrderBy(i => i.Id, StringComparer.Ordinal).ToArray();

        var schema = RecipeSchema.Build(
            ingredients.Select(i => i.Id),
            config.Spinner.SeasonByMonth.Append(SeasonIds.All).Distinct().Order(StringComparer.Ordinal),
            ingredients.Select(i => i.ProteinSource).OfType<string>().Append(ProteinSources.None).Append(ProteinSources.Vegetarian).Distinct().Order(StringComparer.Ordinal));

        var system = PromptTemplate.Render(prompts.SystemTemplate, new Dictionary<string, string>
        {
            ["ingredient_catalog"] = string.Join("\n", ingredients.Select(i => Messages.Catalog(i.Id, NamesOf(i), i.Category, i.ProteinSource))),
            ["season_ids"] = string.Join(", ", config.Spinner.SeasonByMonth.Distinct()),
            ["protein_min_grams"] = validation.MainProteinGramsPerServing.Min.ToString("0.##", CultureInfo.InvariantCulture),
            ["protein_max_grams"] = validation.MainProteinGramsPerServing.Max.ToString("0.##", CultureInfo.InvariantCulture),
        });

        var only = options.Only?.ToHashSet(StringComparer.Ordinal);
        var outcomes = new List<DishOutcome>();
        var total = new TokenUsage(0, 0, 0, 0);
        var calls = 0;

        foreach (var dish in dishes.Where(d => only is null || only.Contains(d.Id)))
        {
            if (File.Exists(DraftFiles.FinalPath(dataRoot, dish.Id)))
            {
                Record(new DishOutcome(dish.Id, DraftOutcomeKind.SkippedFinal, null, Messages.SkippedFinal(dish.Id)));
                continue;
            }

            if (!options.Redo && File.Exists(DraftFiles.DraftPath(dataRoot, dish.Id)))
            {
                Record(new DishOutcome(dish.Id, DraftOutcomeKind.SkippedDraft, null, Messages.SkippedDraft(dish.Id)));
                continue;
            }

            if (options.Limit is { } limit && calls >= limit)
            {
                return new DraftRunResult(outcomes, null, LimitReached: true, calls, total);
            }

            var user = PromptTemplate.Render(prompts.DishTemplate, new Dictionary<string, string>
            {
                ["name"] = dish.Name,
                ["slot"] = JsonName(dish.Slot),
                ["slot_label"] = Messages.SlotLabel(dish.Slot),
                ["regions"] = dish.Regions.Count == 0 ? Messages.NoRegion() : string.Join(", ", dish.Regions.Select(JsonName)),
            });

            ModelReply reply;
            calls++;
            try
            {
                reply = await model.DraftAsync(new ModelRequest(system, user, schema), cancellationToken).ConfigureAwait(false);
            }
            catch (ModelException ex) when (ex.Kind == ModelFailure.Auth)
            {
                return new DraftRunResult(outcomes, Messages.StoppedAuth(), false, calls, total);
            }
            catch (ModelException ex) when (ex.Kind == ModelFailure.RateLimited)
            {
                return new DraftRunResult(outcomes, Messages.StoppedRateLimited(), false, calls, total);
            }
            catch (ModelException ex) when (ex.Kind == ModelFailure.OutOfCredit)
            {
                return new DraftRunResult(outcomes, Messages.StoppedOutOfCredit(), false, calls, total);
            }
            catch (ModelException ex)
            {
                Fail(dish, string.Empty, new TokenUsage(0, 0, 0, 0), "model-error", Messages.ModelCallFailed(ex.Message));
                continue;
            }

            total = Add(total, reply.Usage);

            if (reply.StopReason == "refusal")
            {
                Fail(dish, reply.StopReason, reply.Usage, "model-refusal", Messages.ModelRefused(reply.RefusalCategory));
                continue;
            }

            if (reply.StopReason == "max_tokens")
            {
                Fail(dish, reply.StopReason, reply.Usage, "model-truncated", Messages.ModelCutOff(AnthropicRecipeModel.MaxTokens));
                continue;
            }

            if (reply.Text is null)
            {
                Fail(dish, reply.StopReason, reply.Usage, "model-empty", Messages.ModelNoText());
                continue;
            }

            LlmRecipe recipe;
            try
            {
                recipe = LlmRecipe.Parse(reply.Text);
            }
            catch (JsonException ex)
            {
                Fail(dish, reply.StopReason, reply.Usage, "model-bad-answer", Messages.ModelBadAnswer(ex.Message));
                continue;
            }

            Dish draft;
            try
            {
                draft = DraftComposer.Compose(dish, recipe);
            }
            catch (FormatException ex)
            {
                Fail(dish, reply.StopReason, reply.Usage, "model-bad-value", Messages.ModelValueRefused(ex.Message));
                continue;
            }

            var text = DraftComposer.Serialize(draft);
            var problems = DraftChecker.Check(text, draft, data, schemas).ToList();
            var missing = recipe.MissingIngredients
                .Select(m => new MissingIngredient(m.Name, m.GramsEdiblePerServing, string.IsNullOrWhiteSpace(m.Note) ? null : m.Note.Trim()))
                .ToArray();
            problems.AddRange(missing.Select(m => new DraftProblem("warning", "missing-ingredient", string.Empty, Messages.MissingIngredient(m.Name, m.GramsEdiblePerServing))));

            var errors = problems.Count(p => p.Severity == "error");
            var warnings = problems.Count - errors;
            var status = errors > 0 ? DraftReport.Invalid : warnings > 0 ? DraftReport.Warnings : DraftReport.Valid;

            var draftPath = DraftFiles.DraftPath(dataRoot, dish.Id);
            DraftFiles.WriteAtomically(draftPath, text);
            DraftFiles.WriteAtomically(
                DraftFiles.ReportPath(dataRoot, dish.Id),
                DraftFiles.SerializeReport(new DraftReport(dish.Id, reply.Model, clock.Today, status, reply.StopReason, problems, missing, reply.Usage)));

            Record(new DishOutcome(dish.Id, DraftOutcomeKind.Drafted, status, Messages.Drafted(dish.Id, status, draftPath, errors, warnings)));
        }

        return new DraftRunResult(outcomes, null, LimitReached: false, calls, total);

        void Fail(DishRequest dish, string stopReason, TokenUsage usage, string code, string message)
        {
            DraftFiles.WriteAtomically(
                DraftFiles.ReportPath(dataRoot, dish.Id),
                DraftFiles.SerializeReport(new DraftReport(
                    dish.Id, model.Name, clock.Today, DraftReport.Failed, stopReason, [new DraftProblem("error", code, string.Empty, message)], [], usage)));

            Record(new DishOutcome(dish.Id, DraftOutcomeKind.Failed, DraftReport.Failed, Messages.Failed(dish.Id, message)));
        }

        void Record(DishOutcome outcome)
        {
            outcomes.Add(outcome);
            report(outcome);
        }
    }

    /// <summary>Every name the ingredient goes by, the default first, so the model can match "cá lóc" and "cá quả" to one id.</summary>
    private static string NamesOf(Ingredient ingredient) =>
        string.Join(" / ", new[] { RegionKeys.Default, "north", "central", "south" }
            .Select(key => ingredient.Names.GetValueOrDefault(key))
            .OfType<string>()
            .Distinct());

    private static TokenUsage Add(TokenUsage a, TokenUsage b) => new(
        a.InputTokens + b.InputTokens,
        a.OutputTokens + b.OutputTokens,
        a.CacheCreationInputTokens + b.CacheCreationInputTokens,
        a.CacheReadInputTokens + b.CacheReadInputTokens);

    private static string JsonName<T>(T value) where T : struct, Enum =>
        JsonSerializer.Serialize(value, MamCom.Core.Json.CoreJson.Options).Trim('"');
}
