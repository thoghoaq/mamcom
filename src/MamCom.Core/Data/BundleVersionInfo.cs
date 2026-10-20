using System.Text.Json;
using MamCom.Core.Json;

namespace MamCom.Core.Data;

/// <summary>The names the data package has on the server, shared by the tool that writes it, the app that loads it and the service worker (a test keeps the JavaScript copy equal).</summary>
public static class BundleFiles
{
    /// <summary>The bundle itself. The app asks for <c>data-bundle.json?v=&lt;version&gt;</c>, so a version is one immutable URL.</summary>
    public const string Bundle = "data-bundle.json";

    /// <summary>A few bytes naming the version the server has now, so the app can tell whether there is something new without downloading the bundle.</summary>
    public const string Version = "data-version.json";

    /// <summary>The thumb and full pictures the bundle names, copied from <c>data/images/out</c>.</summary>
    public const string ImagesFolder = "images";
}

/// <summary>What <c>data-version.json</c> says (SPEC §7.6): which bundle the server has now.</summary>
/// <param name="Version">The <see cref="DataBundle.Version"/> of the bundle next to it.</param>
/// <param name="IsRelease">The <see cref="DataBundle.IsRelease"/> of the bundle next to it.</param>
public sealed record BundleVersionInfo(string Version, bool IsRelease)
{
    public static BundleVersionInfo Of(DataBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);

        return new BundleVersionInfo(bundle.Version, bundle.IsRelease);
    }

    /// <exception cref="JsonException">The text is not a version file, or names no version.</exception>
    public static BundleVersionInfo Parse(string json)
    {
        var info = JsonSerializer.Deserialize(json, CoreJsonContext.Default.BundleVersionInfo)
            ?? throw new JsonException("data-version.json must be an object, not null.");

        return string.IsNullOrWhiteSpace(info.Version)
            ? throw new JsonException("data-version.json names no version.")
            : info;
    }

    /// <summary>The file as written, ending with a line feed.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, CoreJsonContext.Default.BundleVersionInfo) + "\n";
}
