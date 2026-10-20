using MamCom.Core.Data;

namespace MamCom.Data.Bundles;

/// <summary>
/// Makes the device keep the thumb pictures of the whole bundle (SPEC §7.5: the 200 px pictures are for offline use). It asks for each
/// one; the service worker answers from the network and keeps the answer, so the second time, and every time offline, costs nothing.
/// The big pictures are not fetched ahead: they are kept the first time they are looked at.
/// </summary>
public sealed class ThumbPreloader(HttpClient http)
{
    /// <summary>The request header the service worker looks for (service-worker.data.js has the same name; a test keeps them equal).</summary>
    public const string PreloadHeader = "X-MamCom-Preload";

    public static string ThumbUrl(string name) => $"{BundleFiles.ImagesFolder}/{Uri.EscapeDataString(name)}";

    /// <summary>The thumb files the bundle names, once each, in name order.</summary>
    public static IReadOnlyList<string> ThumbsOf(DataBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);

        return [.. bundle.Ingredients.Select(i => i.Image.Thumb).Concat(bundle.Dishes.Select(d => d.Image.Thumb))
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];
    }

    /// <summary>Asks for every thumb, a few at a time. A picture that cannot be had (offline, missing) is skipped, not an error.</summary>
    /// <returns>How many came.</returns>
    public async Task<int> PreloadAsync(DataBundle bundle, int parallelism = 4, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(parallelism, 1);

        var fetched = 0;
        await Parallel.ForEachAsync(
            ThumbsOf(bundle),
            new ParallelOptions { MaxDegreeOfParallelism = parallelism, CancellationToken = cancellationToken },
            async (name, token) =>
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, ThumbUrl(name));
                    request.Headers.Add(PreloadHeader, "1");   // tells the service worker this only fills the cache: no need to renew a picture it already has
                    using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode)
                    {
                        await response.Content.ReadAsByteArrayAsync(token).ConfigureAwait(false);   // read it all, so the cache gets all of it
                        Interlocked.Increment(ref fetched);
                    }
                }
                catch (HttpRequestException)
                {
                    // Offline: the next visit tries again.
                }
            }).ConfigureAwait(false);

        return fetched;
    }
}
