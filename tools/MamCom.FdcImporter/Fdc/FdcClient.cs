using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace MamCom.FdcImporter.Fdc;

/// <summary>
/// The one FoodData Central call the importer needs: <c>foods/search</c> over Foundation and SR Legacy foods. The API key
/// travels in the <c>X-Api-Key</c> header, never in the URL, so it cannot end up in a log or an error message.
/// </summary>
public sealed class FdcClient
{
    public static readonly Uri DefaultBaseAddress = new("https://api.nal.usda.gov/fdc/v1/");

    public const int DefaultPageSize = 25;

    private static readonly JsonSerializerOptions ReadOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly string _apiKey;

    public FdcClient(HttpClient http, string apiKey)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        _http = http;
        _apiKey = apiKey;
        _http.BaseAddress ??= DefaultBaseAddress;
    }

    /// <exception cref="FdcRateLimitedException">HTTP 429.</exception>
    /// <exception cref="FdcAuthException">HTTP 401 or 403.</exception>
    /// <exception cref="FdcException">Any other failure, including a body that is not a search result.</exception>
    public async Task<FdcSearchResult> SearchAsync(string query, int pageSize = DefaultPageSize, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        // Foundation and SR Legacy are the per-100g tables of raw foods; branded and survey foods are products and dishes.
        var path = $"foods/search?query={Uri.EscapeDataString(query)}&dataType=Foundation&dataType={Uri.EscapeDataString("SR Legacy")}&pageSize={pageSize.ToString(CultureInfo.InvariantCulture)}";
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Api-Key", _apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new FdcException(null, $"Could not reach FDC: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new FdcException(null, "FDC did not answer in time.", ex);
        }

        using (response)
        {
            var rateLimit = ReadRateLimit(response);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throw new FdcRateLimitedException(ReadRetryAfter(response), rateLimit);
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                throw new FdcAuthException(response.StatusCode);
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new FdcException(response.StatusCode, $"FDC returned HTTP {(int)response.StatusCode} for the query '{query}'.");
            }

            return await ReadBodyAsync(response, rateLimit, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<FdcSearchResult> ReadBodyAsync(HttpResponseMessage response, RateLimit rateLimit, CancellationToken cancellationToken)
    {
        try
        {
            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var parsed = await JsonSerializer.DeserializeAsync<SearchBody>(body, ReadOptions, cancellationToken).ConfigureAwait(false)
                ?? throw new JsonException("The body is null.");

            return new FdcSearchResult(parsed.TotalHits, parsed.Foods ?? [], rateLimit);
        }
        catch (JsonException ex)
        {
            throw new FdcException(response.StatusCode, $"FDC sent something that is not a search result: {ex.Message}", ex);
        }
    }

    private static RateLimit ReadRateLimit(HttpResponseMessage response) =>
        new(ReadInt(response, "X-RateLimit-Limit"), ReadInt(response, "X-RateLimit-Remaining"));

    private static int? ReadInt(HttpResponseMessage response, string header) =>
        response.Headers.TryGetValues(header, out var values)
        && int.TryParse(values.FirstOrDefault(), NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    private static TimeSpan? ReadRetryAfter(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta)
        {
            return delta < TimeSpan.Zero ? TimeSpan.Zero : delta;
        }

        if (retryAfter?.Date is { } date)
        {
            var wait = date - DateTimeOffset.UtcNow;
            return wait < TimeSpan.Zero ? TimeSpan.Zero : wait;
        }

        return null;
    }

    private sealed record SearchBody(int TotalHits, List<FdcFood>? Foods);
}
