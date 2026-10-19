using MamCom.Core;
using MamCom.FdcImporter.Fdc;
using MamCom.FdcImporter.Ranking;
using MamCom.FdcImporter.Suggestions;

namespace MamCom.FdcImporter.Running;

/// <param name="Top">How many candidates to keep per ingredient.</param>
/// <param name="Refresh">Ask again for ingredients that already have a suggestion file.</param>
/// <param name="DelayBetweenRequests">Pause before every call after the first, so the run never bursts.</param>
/// <param name="MaxRequests">Calls allowed in this run, retries included.</param>
/// <param name="MaxRetryWait">The longest wait after a 429 that is worth sitting through.</param>
/// <param name="MaxRetries">How many times one ingredient is retried after a 429.</param>
public sealed record RunOptions(int Top, bool Refresh, TimeSpan DelayBetweenRequests, int MaxRequests, TimeSpan MaxRetryWait, int MaxRetries);

public enum OutcomeKind
{
    Written,
    SkippedExisting,
    NoEnglishName,
    Failed,
}

public sealed record IngredientOutcome(string IngredientId, OutcomeKind Kind, string Message);

/// <param name="StoppedBecause">Why the run ended before the list was done; null when it went through.</param>
public sealed record RunResult(IReadOnlyList<IngredientOutcome> Outcomes, string? StoppedBecause, int RequestsMade);

/// <summary>
/// Asks FDC about each ingredient in turn and writes the ranked suggestions. It stops cleanly, keeping what it wrote,
/// when the key is out of requests, when 429 asks for a long wait, when the key is refused, or at the request cap.
/// </summary>
public sealed class SuggestionRunner(FdcClient client, IClock clock, Func<TimeSpan, CancellationToken, Task> delay)
{
    public async Task<RunResult> RunAsync(
        string dataRoot,
        IReadOnlyList<string> ingredientIds,
        IReadOnlyDictionary<string, string> englishNames,
        RunOptions options,
        Action<IngredientOutcome> report,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);
        ArgumentNullException.ThrowIfNull(ingredientIds);
        ArgumentNullException.ThrowIfNull(englishNames);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(report);

        var outcomes = new List<IngredientOutcome>();
        var requests = 0;
        RateLimit? lastRateLimit = null;

        IngredientOutcome Record(IngredientOutcome outcome)
        {
            outcomes.Add(outcome);
            report(outcome);
            return outcome;
        }

        RunResult Stop(string reason) => new(outcomes, reason, requests);

        foreach (var id in ingredientIds)
        {
            if (!englishNames.TryGetValue(id, out var query))
            {
                Record(new IngredientOutcome(id, OutcomeKind.NoEnglishName, Messages.NoEnglishName(id)));
                continue;
            }

            if (!options.Refresh && SuggestionFile.Exists(dataRoot, id))
            {
                Record(new IngredientOutcome(id, OutcomeKind.SkippedExisting, Messages.SkippedExisting(id)));
                continue;
            }

            var retries = 0;
            FdcSearchResult? result = null;
            while (result is null)
            {
                if (requests >= options.MaxRequests)
                {
                    return Stop(Messages.StoppedMaxRequests(options.MaxRequests));
                }

                if (lastRateLimit is { Remaining: <= 0 })
                {
                    return Stop(Messages.StoppedNoQuota(lastRateLimit.Limit));
                }

                if (requests > 0)
                {
                    await delay(options.DelayBetweenRequests, cancellationToken).ConfigureAwait(false);
                }

                requests++;
                try
                {
                    result = await client.SearchAsync(query, FdcClient.DefaultPageSize, cancellationToken).ConfigureAwait(false);
                }
                catch (FdcRateLimitedException limited)
                {
                    lastRateLimit = limited.RateLimit;

                    // No requests left means the wait is until the quota resets, which is not worth sitting through.
                    if (limited.RateLimit.Remaining is <= 0)
                    {
                        return Stop(Messages.StoppedNoQuota(limited.RateLimit.Limit));
                    }

                    if (retries >= options.MaxRetries || limited.RetryAfter is not { } wait || wait > options.MaxRetryWait)
                    {
                        return Stop(Messages.StoppedRateLimited(limited.RetryAfter));
                    }

                    retries++;
                    await delay(wait, cancellationToken).ConfigureAwait(false);
                }
                catch (FdcAuthException)
                {
                    return Stop(Messages.StoppedAuth());
                }
                catch (FdcException failure)
                {
                    Record(new IngredientOutcome(id, OutcomeKind.Failed, Messages.Failed(id, failure.Message)));
                    break;
                }
            }

            if (result is null)
            {
                continue;   // this ingredient failed; the next one may still work
            }

            lastRateLimit = result.RateLimit;
            var candidates = CandidateRanker.Rank(query, result.Foods, options.Top);
            var suggestions = new NutritionSuggestions(id, query, clock.Today, NutritionSuggestions.UsdaFdc, result.TotalHits, candidates);
            var path = SuggestionFile.Write(dataRoot, suggestions);
            Record(new IngredientOutcome(id, OutcomeKind.Written, Messages.Written(id, path, candidates.Count)));
        }

        return new RunResult(outcomes, null, requests);
    }
}
