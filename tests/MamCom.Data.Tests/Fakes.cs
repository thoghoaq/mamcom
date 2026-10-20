using MamCom.Core.Data;
using MamCom.DataBuilder.Bundling;
using MamCom.DataBuilder.Loading;
using MamCom.DataBuilder.Validation;
using Microsoft.JSInterop;

namespace MamCom.Data.Tests;

/// <summary>Real bundles, built from the frozen seed by the real DataBuilder, and versions of them that differ.</summary>
internal static class Bundles
{
    private static readonly Lazy<DataBundle> SeedBundle = new(() =>
        BundleBuilder.Build(DataLoader.Load(Path.Combine(AppContext.BaseDirectory, "data")), ValidationMode.Dev));

    /// <summary>The seed as the app would get it.</summary>
    public static DataBundle A => SeedBundle.Value;

    /// <summary>The same data with one dish renamed, so it has another version.</summary>
    public static DataBundle B => WithDishName("Rau muống luộc, bản mới");

    public static DataBundle C => WithDishName("Rau muống luộc, bản thứ ba");

    public static DataBundle WithDishName(string name) =>
        (A with { Dishes = [.. A.Dishes.Select(d => d.Id == "rau-muong-luoc" ? d with { Name = name } : d)] }).WithComputedVersion();

    /// <summary>A bundle whose version is not the hash of its content: it was damaged or edited by hand.</summary>
    public static DataBundle Damaged => B with { Version = A.Version };

    public static DataBundle WithThumbs(params string[] thumbs)
    {
        var dishes = A.Dishes.ToArray();
        for (var i = 0; i < thumbs.Length; i++)
        {
            dishes[i] = dishes[i] with { Image = dishes[i].Image with { Thumb = thumbs[i], Full = null, Source = "test", License = "CC0" } };
        }

        return (A with { Dishes = dishes }).WithComputedVersion();
    }
}

/// <summary>A web server that answers what it is told to, remembers what it was asked, and can go offline.</summary>
internal sealed class FakeServer : HttpMessageHandler
{
    private readonly Dictionary<string, Func<HttpResponseMessage>> _routes = new(StringComparer.Ordinal);
    private readonly object _lock = new();

    public List<string> Requests { get; } = [];

    public List<HttpRequestMessage> Seen { get; } = [];

    public bool Offline { get; set; }

    /// <summary>When set, every request waits this long (or until it is cancelled).</summary>
    public TimeSpan? Delay { get; set; }

    public HttpClient Client() => new(this) { BaseAddress = new Uri("https://mamcom.test/") };

    public FakeServer Serve(string pathAndQuery, string body, string contentType = "application/json")
    {
        _routes[pathAndQuery] = () =>
        {
            // An empty content type means the server names none, as some hosts do for a file they do not have.
            var content = contentType.Length == 0 ? new StringContent(body) : new StringContent(body, System.Text.Encoding.UTF8, contentType);
            if (contentType.Length == 0)
            {
                content.Headers.ContentType = null;
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = content };
        };
        return this;
    }

    public FakeServer ServeStatus(string pathAndQuery, System.Net.HttpStatusCode status)
    {
        _routes[pathAndQuery] = () => new HttpResponseMessage(status);
        return this;
    }

    public FakeServer Publish(DataBundle bundle)
    {
        Serve(BundleFiles.Version, BundleVersionInfo.Of(bundle).ToJson());
        return Serve($"{BundleFiles.Bundle}?v={bundle.Version}", bundle.ToJson());
    }

    /// <summary>Serves the bundle at its own URL only, as a cache would after the server is gone.</summary>
    public FakeServer Cache(DataBundle bundle)
    {
        Serve($"{BundleFiles.Bundle}?v={bundle.Version}", bundle.ToJson());
        return this;
    }

    public FakeServer Forget(string pathAndQuery)
    {
        _routes.Remove(pathAndQuery);
        return this;
    }

    public int Count(string pathAndQuery) => Requests.Count(r => r == pathAndQuery);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var key = request.RequestUri!.PathAndQuery.TrimStart('/');
        lock (_lock)
        {
            Requests.Add(key);   // the preloader asks in parallel, and a List is not safe for that
            Seen.Add(request);
        }

        if (Delay is { } delay)
        {
            await Task.Delay(delay, cancellationToken);
        }

        if (Offline)
        {
            throw new HttpRequestException("The server cannot be reached.");
        }

        return _routes.TryGetValue(key, out var answer) ? answer() : new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
    }
}

/// <summary>A browser's localStorage and storage manager, as far as MamCom.Data uses them.</summary>
internal sealed class FakeBrowser : IJSRuntime
{
    public Dictionary<string, string> Items { get; } = new(StringComparer.Ordinal);

    public List<string> Calls { get; } = [];

    /// <summary>The browser refuses every write, as a private window does.</summary>
    public bool RefuseWrites { get; set; }

    /// <summary>The browser refuses a write of this key only, as a full quota would for a big value.</summary>
    public string? RefuseKey { get; set; }

    public bool RefuseReads { get; set; }

    /// <summary>What <c>navigator.storage.persist()</c> answers; null when the browser has no storage manager.</summary>
    public bool? PersistAnswer { get; set; }

    /// <summary>What <c>mamcomDevice.vibrate</c> answers; null when the page has no device.js.</summary>
    public bool? VibrateAnswer { get; set; }

    /// <summary>What <c>mamcomDevice.prefersReducedMotion</c> answers; null when the page has no device.js.</summary>
    public bool? ReducedMotionAnswer { get; set; }

    /// <summary>The lengths, in milliseconds, of the vibrations that were asked for.</summary>
    public List<int> Vibrations { get; } = [];

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
    {
        Calls.Add(identifier);
        switch (identifier)
        {
            case "localStorage.setItem":
                if (RefuseWrites || (string)args![0]! == RefuseKey)
                {
                    throw new JSException("QuotaExceededError");
                }

                Items[(string)args![0]!] = (string)args[1]!;
                return ValueTask.FromResult(default(TValue)!);

            case "localStorage.getItem":
                if (RefuseReads)
                {
                    throw new JSException("SecurityError");
                }

                return ValueTask.FromResult((TValue)(object?)Items.GetValueOrDefault((string)args![0]!)!);

            case "localStorage.removeItem":
                Items.Remove((string)args![0]!);
                return ValueTask.FromResult(default(TValue)!);

            case "mamcomDevice.vibrate":
                Vibrations.Add((int)args![0]!);
                return VibrateAnswer is { } vibrated ? ValueTask.FromResult((TValue)(object)vibrated) : throw new JSException("mamcomDevice is not defined");

            case "mamcomDevice.prefersReducedMotion":
                return ReducedMotionAnswer is { } reduced ? ValueTask.FromResult((TValue)(object)reduced) : throw new JSException("mamcomDevice is not defined");

            case "navigator.storage.persist":
                return PersistAnswer is { } answer ? ValueTask.FromResult((TValue)(object)answer) : throw new JSException("storage is not defined");

            default:
                throw new InvalidOperationException($"Unexpected call {identifier}.");
        }
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => InvokeAsync<TValue>(identifier, args);
}

/// <summary>A storage that remembers what it was asked.</summary>
internal sealed class SpyStorage : MamCom.Data.Storage.IKeyValueStorage
{
    private readonly MamCom.Data.Storage.MemoryKeyValueStorage _inner = new();

    public int PersistenceRequests { get; private set; }

    /// <summary>Runs when the app asks the browser to keep the data: the first thing it does after a successful load.</summary>
    public Action? OnPersistenceRequested { get; set; }

    public bool PersistAnswer { get; set; } = true;

    public bool Durable { get; set; } = true;

    public bool IsPersistent => Durable;

    public ValueTask<string?> GetAsync(string key) => _inner.GetAsync(key);

    public async ValueTask<bool> SetAsync(string key, string value)
    {
        await _inner.SetAsync(key, value);
        return Durable;
    }

    public ValueTask RemoveAsync(string key) => _inner.RemoveAsync(key);

    public ValueTask<bool> RequestPersistenceAsync()
    {
        PersistenceRequests++;
        OnPersistenceRequested?.Invoke();
        return ValueTask.FromResult(PersistAnswer);
    }
}
