using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using MamCom.Core.Cooking;
using MamCom.Core.Json;
using MamCom.Core.Models;
using MamCom.Core.Nutrition;
using MamCom.Core.Portions;
using MamCom.Core.Spinning;

namespace MamCom.Core.Data;

/// <summary>The config files that travel with the content, so the app never needs the <c>data</c> folder.</summary>
public sealed record BundleConfig(PortionConfig Portion, NutritionConfig Nutrition, SpinnerConfig Spinner, CookingConfig Cooking);

/// <summary>
/// Everything the app reads at run time, built from <c>data/</c> by MamCom.DataBuilder and shipped as
/// <c>data-bundle.json</c> (SPEC §7.6).
/// </summary>
/// <param name="Version">First <see cref="VersionLength"/> hex digits of the SHA-256 of the bundle written with an empty version, so equal content always has the same version.</param>
/// <param name="IsRelease">True for a release build (every dish reviewed, no placeholder data); false for a development build the app should label as such.</param>
/// <param name="Ingredients">In id order.</param>
/// <param name="Dishes">In id order.</param>
public sealed record DataBundle(
    string Version,
    bool IsRelease,
    BundleConfig Config,
    IReadOnlyList<Ingredient> Ingredients,
    IReadOnlyList<Dish> Dishes)
{
    public const int VersionLength = 16;

    /// <exception cref="JsonException">The text is not a bundle: not JSON, a member missing, or an unknown value.</exception>
    public static DataBundle Parse(string json) =>
        JsonSerializer.Deserialize(json, CoreJsonContext.Default.DataBundle)
        ?? throw new JsonException("data-bundle.json must be an object, not null.");

    /// <summary>The bundle as written to <c>data-bundle.json</c>, ending with a line feed.</summary>
    public string ToJson() =>
        JsonSerializer.Serialize(this, (JsonTypeInfo<DataBundle>)CoreJson.Options.GetTypeInfo(typeof(DataBundle))) + "\n";

    /// <summary>The version this content should have, whatever <see cref="Version"/> says now.</summary>
    public string ComputeVersion()
    {
        var bytes = Encoding.UTF8.GetBytes((this with { Version = string.Empty }).ToJson());
        return Convert.ToHexStringLower(SHA256.HashData(bytes))[..VersionLength];
    }

    public DataBundle WithComputedVersion() => this with { Version = ComputeVersion() };

    /// <summary>True when <see cref="Version"/> is the hash of the content: the file was not edited by hand. A method, not a property, so it is never written to the JSON.</summary>
    public bool VersionMatchesContent() => string.Equals(Version, ComputeVersion(), StringComparison.Ordinal);

    /// <summary>
    /// True when the text of a <c>data-bundle.json</c>, as it came, is the one its version was made from: the hash of the text with the
    /// version left empty. The text is hashed as it came, not as this app would write it again, so a bundle made before the app learned a
    /// field it can do without (ADR-028) is still whole: written again it would have the new field, and another hash.
    /// </summary>
    public static bool TextMatchesVersion(string text, string version)
    {
        ArgumentNullException.ThrowIfNull(text);

        var stamp = $"\"version\": \"{version}\"";
        var at = string.IsNullOrEmpty(version) ? -1 : text.IndexOf(stamp, StringComparison.Ordinal);
        if (at < 0)
        {
            return false;
        }

        var unstamped = string.Concat(text.AsSpan(0, at), "\"version\": \"\"", text.AsSpan(at + stamp.Length));
        return string.Equals(version, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(unstamped)))[..VersionLength], StringComparison.Ordinal);
    }
}
