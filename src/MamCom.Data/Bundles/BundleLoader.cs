using System.Net;
using System.Text.Json;
using MamCom.Core.Data;
using MamCom.Data.Storage;

namespace MamCom.Data.Bundles;

public enum BundleLoadStatus
{
    Loaded,

    /// <summary>The device has no copy of the data yet and cannot reach the server. Open the app once with a connection.</summary>
    NeedsNetwork,

    /// <summary>The server answers but has no bundle: the data was never published.</summary>
    NotPublished,

    /// <summary>A bundle came, but it is not a bundle, or its version is not the hash of its content (damaged on the way).</summary>
    Invalid,
}

/// <param name="IsNewVersion">The bundle is not the one the device held before: the first load, or an update that has just been taken in.</param>
public sealed record BundleLoadResult(BundleLoadStatus Status, DataBundle? Bundle, bool IsNewVersion);

public enum BundleUpdateStatus
{
    /// <summary>The server has the version the device holds.</summary>
    UpToDate,

    /// <summary>A newer version is downloaded and kept; the next <see cref="BundleLoader.LoadAsync"/> uses it.</summary>
    Ready,

    /// <summary>No answer from the server (offline, a slow line, a server error). Nothing changed.</summary>
    Unreachable,

    /// <summary>The server answered with something unusable. Nothing changed.</summary>
    Failed,
}

public sealed record BundleUpdateResult(BundleUpdateStatus Status, string? Version);

/// <param name="VersionTimeout">How long to wait for the few bytes of <c>data-version.json</c> before treating the server as out of reach.</param>
/// <param name="BundleTimeout">How long to wait for the bundle, which is larger.</param>
public sealed record BundleLoaderOptions(TimeSpan VersionTimeout, TimeSpan BundleTimeout)
{
    public static BundleLoaderOptions Default { get; } = new(TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(60));
}

/// <summary>
/// Gets the data bundle for the app (SPEC §7.6, ADR-020) and keeps it fresh without a new release of the app.
/// <para>
/// A version of the bundle is one immutable URL, <c>data-bundle.json?v=&lt;version&gt;</c>, which the service worker answers from its
/// cache, so a device that has seen a version has it offline. The loader remembers the version it uses. Opening the app takes it from
/// the cache at once, with no waiting for the network. Only a device that has no copy yet asks the server which version is
/// newest. <see cref="CheckForUpdateAsync"/> asks <c>data-version.json</c> (a few bytes), downloads a newer bundle in the
/// background and keeps it as pending, and the next load uses it. Nothing about the data changes while a meal is being planned.
/// </para>
/// </summary>
public sealed class BundleLoader(HttpClient http, IKeyValueStorage storage, BundleLoaderOptions? options = null)
{
    public const string CurrentKey = "mamcom.data.current";
    public const string PendingKey = "mamcom.data.pending";

    private readonly BundleLoaderOptions _options = options ?? BundleLoaderOptions.Default;

    private enum FetchStatus
    {
        Ok,
        Unreachable,
        NotFound,
        Invalid,
    }

    private readonly record struct Fetched<T>(FetchStatus Status, T? Value)
        where T : class;

    public static string BundleUrl(string version) => $"{BundleFiles.Bundle}?v={Uri.EscapeDataString(version)}";

    public async Task<BundleLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        var current = await storage.GetAsync(CurrentKey).ConfigureAwait(false);
        var pending = await storage.GetAsync(PendingKey).ConfigureAwait(false);

        // What the device remembers first: a version it has seen is in the cache and costs no network.
        foreach (var version in new[] { pending, current }.Where(v => !string.IsNullOrEmpty(v)).Distinct(StringComparer.Ordinal))
        {
            var got = await FetchBundleAsync(version!, cancellationToken).ConfigureAwait(false);
            if (got.Status == FetchStatus.Ok)
            {
                return await RememberAsync(got.Value!, current).ConfigureAwait(false);
            }

            if (got.Status == FetchStatus.Invalid && version == pending)
            {
                await storage.RemoveAsync(PendingKey).ConfigureAwait(false);   // a bad download is not tried again
            }
        }

        // Nothing remembered, or none of it usable: ask the server which version is newest.
        var info = await FetchVersionAsync(cancellationToken).ConfigureAwait(false);
        if (info.Status != FetchStatus.Ok)
        {
            return Failed(info.Status);
        }

        var fresh = await FetchBundleAsync(info.Value!.Version, cancellationToken).ConfigureAwait(false);
        return fresh.Status == FetchStatus.Ok ? await RememberAsync(fresh.Value!, current).ConfigureAwait(false) : Failed(fresh.Status);
    }

    public async Task<BundleUpdateResult> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        var info = await FetchVersionAsync(cancellationToken).ConfigureAwait(false);
        if (info.Status != FetchStatus.Ok)
        {
            return new BundleUpdateResult(info.Status == FetchStatus.Unreachable ? BundleUpdateStatus.Unreachable : BundleUpdateStatus.Failed, null);
        }

        var current = await storage.GetAsync(CurrentKey).ConfigureAwait(false);
        var pending = await storage.GetAsync(PendingKey).ConfigureAwait(false);
        var newest = info.Value!.Version;

        if (newest == current)
        {
            if (pending is not null)
            {
                await storage.RemoveAsync(PendingKey).ConfigureAwait(false);   // the server went back to what the device holds
            }

            return new BundleUpdateResult(BundleUpdateStatus.UpToDate, current);
        }

        if (newest == pending)
        {
            return new BundleUpdateResult(BundleUpdateStatus.Ready, pending);
        }

        var got = await FetchBundleAsync(newest, cancellationToken).ConfigureAwait(false);
        if (got.Status != FetchStatus.Ok)
        {
            return new BundleUpdateResult(got.Status == FetchStatus.Unreachable ? BundleUpdateStatus.Unreachable : BundleUpdateStatus.Failed, null);
        }

        var version = got.Value!.Version;   // what came, which is what counts if the server moved on in between
        if (version == current)
        {
            return new BundleUpdateResult(BundleUpdateStatus.UpToDate, current);
        }

        await storage.SetAsync(PendingKey, version).ConfigureAwait(false);
        return new BundleUpdateResult(BundleUpdateStatus.Ready, version);
    }

    private async Task<BundleLoadResult> RememberAsync(DataBundle bundle, string? previous)
    {
        await storage.SetAsync(CurrentKey, bundle.Version).ConfigureAwait(false);
        if (await storage.GetAsync(PendingKey).ConfigureAwait(false) == bundle.Version)
        {
            await storage.RemoveAsync(PendingKey).ConfigureAwait(false);
        }

        return new BundleLoadResult(BundleLoadStatus.Loaded, bundle, IsNewVersion: bundle.Version != previous);
    }

    private static BundleLoadResult Failed(FetchStatus status) => new(
        status switch
        {
            FetchStatus.NotFound => BundleLoadStatus.NotPublished,
            FetchStatus.Invalid => BundleLoadStatus.Invalid,
            _ => BundleLoadStatus.NeedsNetwork,
        },
        null,
        IsNewVersion: false);

    private async Task<Fetched<DataBundle>> FetchBundleAsync(string version, CancellationToken cancellationToken)
    {
        var (status, text) = await GetTextAsync(BundleUrl(version), _options.BundleTimeout, cancellationToken).ConfigureAwait(false);
        if (status != FetchStatus.Ok)
        {
            return new(status, null);
        }

        try
        {
            var bundle = DataBundle.Parse(text!);
            return string.IsNullOrWhiteSpace(bundle.Version) || !DataBundle.TextMatchesVersion(text!, bundle.Version)
                ? new(FetchStatus.Invalid, null)
                : new(FetchStatus.Ok, bundle);
        }
        catch (JsonException)
        {
            return new(FetchStatus.Invalid, null);
        }
    }

    private async Task<Fetched<BundleVersionInfo>> FetchVersionAsync(CancellationToken cancellationToken)
    {
        var (status, text) = await GetTextAsync(BundleFiles.Version, _options.VersionTimeout, cancellationToken).ConfigureAwait(false);
        if (status != FetchStatus.Ok)
        {
            return new(status, null);
        }

        try
        {
            return new(FetchStatus.Ok, BundleVersionInfo.Parse(text!));
        }
        catch (JsonException)
        {
            return new(FetchStatus.Invalid, null);
        }
    }

    /// <summary>
    /// One GET. A refused connection, a timeout and a server error are all "unreachable": try again later. A 404, and a 200 that is
    /// an HTML page (a host that answers every unknown path with its start page) or has nothing in it (the development server of
    /// Blazor does that for a path it has no file for), are "not found". Cancelling through
    /// <paramref name="cancellationToken"/> is not an outcome: it throws.
    /// </summary>
    private async Task<(FetchStatus Status, string? Text)> GetTextAsync(string url, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(timeout);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };   // revalidate; the service worker answers versioned URLs from its cache anyway
            using var response = await http.SendAsync(request, limit.Token).ConfigureAwait(false);

            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
            {
                return (FetchStatus.NotFound, null);
            }

            if (!response.IsSuccessStatusCode)
            {
                return (FetchStatus.Unreachable, null);
            }

            if (response.Content.Headers.ContentType?.MediaType == "text/html")
            {
                return (FetchStatus.NotFound, null);
            }

            var text = await response.Content.ReadAsStringAsync(limit.Token).ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(text) ? (FetchStatus.NotFound, null) : (FetchStatus.Ok, text);
        }
        catch (HttpRequestException)
        {
            return (FetchStatus.Unreachable, null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return (FetchStatus.Unreachable, null);   // the timeout
        }
    }
}
