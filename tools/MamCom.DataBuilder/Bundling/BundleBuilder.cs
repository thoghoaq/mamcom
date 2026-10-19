using MamCom.Core.Data;
using MamCom.DataBuilder.Loading;
using MamCom.DataBuilder.Validation;

namespace MamCom.DataBuilder.Bundling;

/// <summary>Turns the loaded data into the bundle the app reads (SPEC §7.6).</summary>
public static class BundleBuilder
{
    /// <summary>
    /// Every ingredient and dish, in id order, with the four config files and the content version. The result does not
    /// depend on file order or on when it is built. Checking the data is the caller's job (<see cref="DataValidator"/>).
    /// </summary>
    /// <exception cref="InvalidOperationException">The data has no usable config, so there is nothing to bundle.</exception>
    public static DataBundle Build(LoadedData data, ValidationMode mode)
    {
        ArgumentNullException.ThrowIfNull(data);
        var config = data.Config ?? throw new InvalidOperationException("The config files could not be loaded, so no bundle can be built.");

        return new DataBundle(
            Version: string.Empty,
            IsRelease: mode == ValidationMode.Release,
            Config: config,
            Ingredients: data.Ingredients.Select(f => f.Value).OrderBy(i => i.Id, StringComparer.Ordinal).ToArray(),
            Dishes: data.Dishes.Select(f => f.Value).OrderBy(d => d.Id, StringComparer.Ordinal).ToArray())
            .WithComputedVersion();
    }
}
