using MamCom.Core.Data;

namespace MamCom.DataBuilder.Bundling;

/// <summary>
/// Puts next to the bundle the pictures it names, so the output folder is the whole data package: <c>data-bundle.json</c>,
/// <c>data-version.json</c> and <c>images/</c>. The package is deployed apart from the app (SPEC §7.6). The folder is the tool's
/// own: a picture nothing names any more is removed, so it does not go on being served and cached.
/// </summary>
public static class ImagePackager
{
    /// <summary>The distinct picture files the bundle names (thumb and full), in name order.</summary>
    public static IReadOnlyList<string> NamedBy(DataBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);

        return [.. bundle.Ingredients.Select(i => i.Image).Concat(bundle.Dishes.Select(d => d.Image))
            .SelectMany(image => new[] { image.Thumb, image.Full })
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];
    }

    /// <summary>Copies the pictures from <c>&lt;dataFolder&gt;/images/out</c> to <c>&lt;outFolder&gt;/images</c> and returns their names.</summary>
    /// <exception cref="FileNotFoundException">A named picture is not in <c>images/out</c>; the validator reports it before a bundle is built.</exception>
    public static IReadOnlyList<string> Copy(DataBundle bundle, string dataFolder, string outFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataFolder);
        ArgumentException.ThrowIfNullOrWhiteSpace(outFolder);

        var names = NamedBy(bundle);
        var target = Path.Combine(outFolder, BundleFiles.ImagesFolder);
        Directory.CreateDirectory(target);

        foreach (var stale in Directory.GetFiles(target).Where(f => !names.Contains(Path.GetFileName(f), StringComparer.Ordinal)))
        {
            File.Delete(stale);
        }

        foreach (var name in names)
        {
            File.Copy(Path.Combine(dataFolder, "images", "out", name), Path.Combine(target, name), overwrite: true);
        }

        return names;
    }
}
