using MamCom.Core.Cooking;
using MamCom.Core.Data;
using MamCom.Core.Models;
using MamCom.Core.Nutrition;
using MamCom.Core.Portions;
using MamCom.Core.Spinning;
using MamCom.Data;
using MamCom.Data.Bundles;
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
}
