namespace MamCom.FdcImporter.Fdc;

/// <summary>One nutrient of a food in a search result. Values are per 100 g of the food.</summary>
public sealed record FdcNutrient(int NutrientId, string? NutrientName, string? UnitName, double? Value);

/// <summary>A food from <c>foods/search</c>. Only the fields the importer reads.</summary>
/// <param name="DataType">For example <c>Foundation</c> or <c>SR Legacy</c>.</param>
/// <param name="Score">FDC's own relevance score for the query; higher is better.</param>
public sealed record FdcFood(
    long FdcId,
    string Description,
    string DataType,
    string? FoodCategory,
    double? Score,
    IReadOnlyList<FdcNutrient>? FoodNutrients);

/// <summary>What the <c>X-RateLimit-*</c> headers said after a call; null when a header was missing.</summary>
public sealed record RateLimit(int? Limit, int? Remaining);

public sealed record FdcSearchResult(int TotalHits, IReadOnlyList<FdcFood> Foods, RateLimit RateLimit);

/// <summary>A call to FDC failed. <see cref="Status"/> is null when there was no response (network, timeout).</summary>
public class FdcException(System.Net.HttpStatusCode? status, string message, Exception? inner = null) : Exception(message, inner)
{
    public System.Net.HttpStatusCode? Status { get; } = status;
}

/// <summary>HTTP 429: the key is over its request limit.</summary>
/// <param name="retryAfter">How long FDC asked to wait, when it said.</param>
public sealed class FdcRateLimitedException(TimeSpan? retryAfter, RateLimit rateLimit)
    : FdcException(System.Net.HttpStatusCode.TooManyRequests, "FDC returned 429 Too Many Requests.")
{
    public TimeSpan? RetryAfter { get; } = retryAfter;

    public RateLimit RateLimit { get; } = rateLimit;
}

/// <summary>HTTP 401 or 403: the key was not accepted.</summary>
public sealed class FdcAuthException(System.Net.HttpStatusCode status)
    : FdcException(status, $"FDC rejected the API key (HTTP {(int)status}).");
