using System.Text.Json;
using MamCom.Core;
using MamCom.Core.Cooking;
using MamCom.Core.Data;
using MamCom.Core.Json;
using MamCom.Core.Models;
using MamCom.Core.Nutrition;
using MamCom.Core.Portions;
using MamCom.Core.Shopping;
using MamCom.Core.Spinning;
using MamCom.UI;
using MamCom.Data;
using MamCom.Data.Bundles;
using MamCom.Data.Device;
using MamCom.Data.State;

namespace MamCom.UI.Tests;

/// <summary>What the screens need to know about the data, set by the test instead of loaded.</summary>
internal sealed class FakeAppData : IAppData
{
    private Action? _changed;

    public BundleLoadStatus? Status { get; set; } = BundleLoadStatus.Loaded;

    public DataBundle? Bundle { get; set; }

    public bool IsLoading => Status is null;

    public bool IsTrialData { get; set; }

    public bool UpdateReady { get; set; }

    public bool IsPersistent { get; set; } = true;

    public int Applied { get; private set; }

    public int Initialized { get; private set; }

    public TaskCompletionSource? ApplyGate { get; set; }

    /// <summary>Taking the update in does not work: it stays waiting.</summary>
    public bool KeepsUpdate { get; set; }

    public int Listeners => _changed?.GetInvocationList().Length ?? 0;

    public event Action? Changed
    {
        add => _changed += value;
        remove => _changed -= value;
    }

    public void Raise() => _changed?.Invoke();

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Initialized++;
        return Task.CompletedTask;
    }

    public async Task ApplyUpdateAsync(CancellationToken cancellationToken = default)
    {
        Applied++;
        if (ApplyGate is not null)
        {
            await ApplyGate.Task;
        }

        UpdateReady = KeepsUpdate;
        Raise();
    }
}

/// <summary>The household the device holds, kept in memory.</summary>
internal sealed class FakeHouseholdStore : IHouseholdStore
{
    public Household? Saved { get; set; }

    public int Saves { get; private set; }

    /// <summary>When set, saving waits for it, so a test can tap twice while the first save is going on.</summary>
    public TaskCompletionSource? SaveGate { get; set; }

    /// <summary>When set, loading waits for it, so a test can look at the screen while the household is still being read.</summary>
    public TaskCompletionSource? LoadGate { get; set; }

    public async ValueTask<Household?> LoadAsync()
    {
        if (LoadGate is not null)
        {
            await LoadGate.Task;
        }

        return Saved;
    }

    public async ValueTask<bool> SaveAsync(Household household)
    {
        Saves++;
        if (SaveGate is not null)
        {
            await SaveGate.Task;
        }

        Saved = household;
        return true;
    }
}

/// <summary>The session the device holds, kept in memory.</summary>
internal sealed class FakeTrayStore : ITrayStore
{
    public TraySession? Saved { get; set; }

    public int Saves { get; private set; }

    public ValueTask<TraySession?> LoadAsync() => ValueTask.FromResult(Saved);

    public ValueTask<bool> SaveAsync(TraySession session)
    {
        Saves++;
        Saved = session;
        return ValueTask.FromResult(true);
    }
}

/// <summary>The ticks of the shopping list, kept in memory.</summary>
internal sealed class FakeShoppingStore : IShoppingStore
{
    public ShoppingState? Saved { get; set; }

    public int Saves { get; private set; }

    public ValueTask<ShoppingState?> LoadAsync() => ValueTask.FromResult(Saved);

    public ValueTask<bool> SaveAsync(ShoppingState state)
    {
        Saves++;
        Saved = state;
        return ValueTask.FromResult(true);
    }
}

/// <summary>The log of what was eaten, kept in memory.</summary>
internal sealed class FakeEatenLogStore : IEatenLogStore
{
    public EatenLog Saved { get; set; } = EatenLog.Empty;

    public int Saves { get; private set; }

    public ValueTask<EatenLog> LoadAsync() => ValueTask.FromResult(Saved);

    public ValueTask<bool> SaveAsync(EatenLog log)
    {
        Saves++;
        Saved = log;
        return ValueTask.FromResult(true);
    }
}

/// <summary>A phone that remembers how long it was asked to buzz.</summary>
internal sealed class FakeDeviceFeatures : IDeviceFeatures
{
    public List<int> Vibrations { get; } = [];

    public bool CanVibrate { get; set; } = true;

    public bool ReducedMotion { get; set; }

    public ValueTask<bool> VibrateAsync(int milliseconds)
    {
        Vibrations.Add(milliseconds);
        return ValueTask.FromResult(CanVibrate);
    }

    public ValueTask<bool> PrefersReducedMotionAsync() => ValueTask.FromResult(ReducedMotion);

    /// <summary>What becomes of a text that is sent.</summary>
    public ShareOutcome ShareResult { get; set; } = ShareOutcome.Shared;

    /// <summary>The texts that were sent.</summary>
    public List<string> Shared { get; } = [];

    /// <summary>When set, sending waits for it, so a test can tap twice while the first send is going on.</summary>
    public TaskCompletionSource? ShareGate { get; set; }

    public async ValueTask<ShareOutcome> ShareTextAsync(string text)
    {
        Shared.Add(text);
        if (ShareGate is not null)
        {
            await ShareGate.Task;
        }

        return ShareResult;
    }

    /// <summary>Whether the screen can be kept lit.</summary>
    public WakeLockOutcome WakeResult { get; set; } = WakeLockOutcome.Active;

    public int KeepScreenOnCalls { get; private set; }

    public int Releases { get; private set; }

    public bool PrepareResult { get; set; } = true;

    public int Prepares { get; private set; }

    public bool AlertResult { get; set; } = true;

    public int Alerts { get; private set; }

    public ValueTask<WakeLockOutcome> KeepScreenOnAsync()
    {
        KeepScreenOnCalls++;
        return ValueTask.FromResult(WakeResult);
    }

    public ValueTask ReleaseScreenAsync()
    {
        Releases++;
        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> PrepareAlertAsync()
    {
        Prepares++;
        return ValueTask.FromResult(PrepareResult);
    }

    public ValueTask<bool> AlertAsync()
    {
        Alerts++;
        return ValueTask.FromResult(AlertResult);
    }
}

/// <summary>The roll of the dice, held for as long as the test wants: not at all, or until <see cref="Gate"/> is opened.</summary>
internal sealed class FakeSpinPacer : ISpinPacer
{
    public int Waits { get; private set; }

    public TaskCompletionSource? Gate { get; set; }

    public async ValueTask WaitAsync(CancellationToken cancellationToken = default)
    {
        Waits++;
        if (Gate is not null)
        {
            await Gate.Task;
        }
    }
}

internal sealed class FixedClock(DateOnly today) : IClock
{
    private DateTimeOffset? _now;

    public DateOnly Today { get; set; } = today;

    /// <summary>Six in the evening of <see cref="Today"/> unless a test sets the time; a timer test moves it on.</summary>
    public DateTimeOffset Now
    {
        get => _now ?? new(Today.ToDateTime(new TimeOnly(18, 0)), TimeSpan.Zero);
        set => _now = value;
    }
}

/// <summary>The cooking mode as it was left, kept in memory.</summary>
internal sealed class FakeCookingStore : ICookingStore
{
    public CookingState? Saved { get; set; }

    public int Saves { get; private set; }

    public ValueTask<CookingState?> LoadAsync() => ValueTask.FromResult(Saved);

    public ValueTask<bool> SaveAsync(CookingState state)
    {
        Saves++;
        Saved = state;
        return ValueTask.FromResult(true);
    }
}

/// <summary>A ticker that goes off when the test says so.</summary>
internal sealed class FakeTicker : ITicker
{
    private Func<Task>? _onTick;

    public int Starts { get; private set; }

    public TimeSpan? Period { get; private set; }

    public bool Stopped { get; private set; }

    public IDisposable Every(TimeSpan period, Func<Task> onTick)
    {
        Starts++;
        Period = period;
        _onTick = onTick;
        return new Handle(this);
    }

    /// <summary>One tick, as the clock would make it; nothing once the screen stopped it.</summary>
    public Task FireAsync() => _onTick is null || Stopped ? Task.CompletedTask : _onTick();

    /// <summary>A tick that was already on its way when the screen stopped the ticker, as a real timer can make one.</summary>
    public Task FireLateAsync() => _onTick is null ? Task.CompletedTask : _onTick();

    private sealed class Handle(FakeTicker ticker) : IDisposable
    {
        public void Dispose() => ticker.Stopped = true;
    }
}

/// <summary>The real config files of <c>data/config</c>, which the project links next to the tests, and a bundle that carries them.</summary>
internal static class TestConfig
{
    private static string Read(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "config", name));

    public static PortionConfig Portion { get; } = PortionConfig.Parse(Read("portion-defaults.json"));

    public static DataBundle Bundle { get; } = new(
        "v1",
        IsRelease: false,
        new BundleConfig(
            Portion,
            NutritionConfig.Parse(Read("nutrition-defaults.json")),
            SpinnerConfig.Parse(Read("spinner-defaults.json")),
            CookingConfig.Parse(Read("cooking-defaults.json"))),
        [],
        []);

    /// <summary>The frozen seed (ADR-018): twenty dishes and thirty-three ingredients to spin from, with the real config.</summary>
    public static DataBundle SeedBundle => _seedBundle.Value;

    private static readonly Lazy<DataBundle> _seedBundle = new(() => Bundle with
    {
        Ingredients = [.. Seed<Ingredient>("ingredients").OrderBy(i => i.Id, StringComparer.Ordinal)],
        Dishes = [.. Seed<Dish>("dishes").OrderBy(d => d.Id, StringComparer.Ordinal)],
    });

    private static IEnumerable<T> Seed<T>(string folder) =>
        Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "seed", folder), "*.json")
            .Select(file => JsonSerializer.Deserialize<T>(File.ReadAllText(file), CoreJson.Options)!);
}
