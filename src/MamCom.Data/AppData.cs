using MamCom.Core.Data;
using MamCom.Data.Bundles;
using MamCom.Data.Storage;

namespace MamCom.Data;

/// <summary>What the screens need to know about the data of the app. <see cref="AppData"/> is the one implementation; a test stands in with another.</summary>
public interface IAppData
{
    /// <summary>How the first load ended; null while it is still going.</summary>
    BundleLoadStatus? Status { get; }

    DataBundle? Bundle { get; }

    /// <summary>True until the first load has an answer.</summary>
    bool IsLoading { get; }

    /// <summary>The bundle in use is a development one, not a release (SPEC §7.6): the app says so.</summary>
    bool IsTrialData { get; }

    /// <summary>A newer bundle is downloaded and waits.</summary>
    bool UpdateReady { get; }

    /// <summary>False when the browser keeps nothing between visits (known once the data has loaded).</summary>
    bool IsPersistent { get; }

    /// <summary>Something on the screen may need to be drawn again.</summary>
    event Action? Changed;

    /// <summary>Loads once; any screen may call it.</summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>Takes in the bundle that was downloaded, now instead of at the next visit.</summary>
    Task ApplyUpdateAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// The data of the app as the screens see it: which bundle is loaded, whether a newer one is waiting, and whether the device
/// keeps what it is told. One per app. Opening the app loads the bundle the device holds (or fetches it the first time) and the
/// first screen can draw. Only then, in the background, does it look for news and keep the thumb pictures, so neither delays
/// anything.
/// </summary>
public sealed class AppData(BundleLoader loader, ThumbPreloader thumbs, IKeyValueStorage storage) : IAppData
{
    private Task? _initialization;

    public BundleLoadResult? LoadResult { get; private set; }

    public DataBundle? Bundle => LoadResult?.Bundle;

    public BundleLoadStatus? Status => LoadResult?.Status;

    public bool IsTrialData => Bundle is { IsRelease: false };

    /// <summary>A newer bundle is downloaded and waits; <see cref="ApplyUpdateAsync"/> takes it in, or the next visit does.</summary>
    public bool UpdateReady { get; private set; }

    /// <summary>False when the browser keeps nothing between visits (known once the data has loaded).</summary>
    public bool IsPersistent => storage.IsPersistent;

    /// <summary>True until the first load has an answer.</summary>
    public bool IsLoading => LoadResult is null;

    /// <summary>The look for news and the keeping of thumbs that follow a successful load. Nothing waits for it; a test does.</summary>
    public Task BackgroundWork { get; private set; } = Task.CompletedTask;

    /// <summary>Something on the screen may need to be drawn again.</summary>
    public event Action? Changed;

    /// <summary>Loads once, and returns when the bundle is loaded or known to be unavailable. A second call returns the same work, so any screen may call it.</summary>
    public Task InitializeAsync(CancellationToken cancellationToken = default) => _initialization ??= InitializeCoreAsync(cancellationToken);

    /// <summary>Takes in the bundle that was downloaded, now instead of at the next visit.</summary>
    public async Task ApplyUpdateAsync(CancellationToken cancellationToken = default)
    {
        var result = await loader.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (result.Status == BundleLoadStatus.Loaded)
        {
            LoadResult = result;
            UpdateReady = false;
            Changed?.Invoke();
        }
    }

    private async Task InitializeCoreAsync(CancellationToken cancellationToken)
    {
        LoadResult = await loader.LoadAsync(cancellationToken).ConfigureAwait(false);
        Changed?.Invoke();

        if (LoadResult.Bundle is { } bundle)
        {
            BackgroundWork = KeepFreshAsync(bundle, cancellationToken);
        }
    }

    private async Task KeepFreshAsync(DataBundle bundle, CancellationToken cancellationToken)
    {
        try
        {
            // The device is going to depend on this copy: ask the browser to keep it (best effort), say whether it will.
            await storage.RequestPersistenceAsync().ConfigureAwait(false);
            Changed?.Invoke();

            var update = await loader.CheckForUpdateAsync(cancellationToken).ConfigureAwait(false);
            if (update.Status == BundleUpdateStatus.Ready)
            {
                UpdateReady = true;
                Changed?.Invoke();
            }

            if (update.Status != BundleUpdateStatus.Unreachable)
            {
                await thumbs.PreloadAsync(bundle, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // The page is going away.
        }
    }
}
