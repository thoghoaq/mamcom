using System.Text.RegularExpressions;

namespace MamCom.Studio.Review;

/// <summary>
/// The pictures of the app: the files in <c>data/images/out</c> that an ingredient or a dish can name as its picture. Studio only
/// lists and shows them; making them from the original photos is the job of ImageTool.
/// </summary>
public sealed partial class ImageLibrary(DataFolder folder)
{
    private static readonly string[] Extensions = [".webp", ".png", ".jpg", ".jpeg"];

    public DataFolder Folder { get; } = folder;

    /// <summary>The names of the picture files, in name order.</summary>
    public IReadOnlyList<string> Files() =>
        !Directory.Exists(Folder.ImageFolder)
            ? []
            : [.. Directory.GetFiles(Folder.ImageFolder)
                .Select(Path.GetFileName)
                .OfType<string>()
                .Where(IsImageName)
                .Order(StringComparer.Ordinal)];

    /// <summary>The path of a picture by its name, or null when the name is not a plain image file name or there is no such file.</summary>
    public string? PathOf(string name)
    {
        if (!IsImageName(name))
        {
            return null;
        }

        var path = Path.Combine(Folder.ImageFolder, name);
        return File.Exists(path) ? path : null;
    }

    /// <summary>True for a plain file name of a picture: letters, digits, dot, dash and underscore, no path, a known extension.</summary>
    public static bool IsImageName(string? name) =>
        name is not null
        && ImageNamePattern().IsMatch(name)
        && !name.Contains("..", StringComparison.Ordinal)   // the validator refuses it too
        && Extensions.Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase);

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._-]*\z")]
    private static partial Regex ImageNamePattern();
}
