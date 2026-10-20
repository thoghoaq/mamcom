using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MamCom.Core.Models;
using MamCom.DataBuilder.Loading;

namespace MamCom.Studio.Review;

/// <summary>An ingredient file as it is on disk right now.</summary>
/// <param name="Text">The file text, as read.</param>
/// <param name="Version">Identifies this exact text, so a save can tell that the file changed meanwhile.</param>
/// <param name="Ingredient">The parsed ingredient; null when the file is not an ingredient (see <paramref name="Problem"/>).</param>
public sealed record StoredIngredient(string Id, string Text, string Version, Ingredient? Ingredient, string? Problem)
{
    public string File => DataFolder.RelativeIngredientFile(Id);
}

/// <summary>Reads and writes ingredient files and reads what FdcImporter suggested. The only place in Studio that touches them.</summary>
public sealed partial class IngredientStore(DataFolder folder)
{
    private static readonly string[] ImageExtensions = [".webp", ".png", ".jpg", ".jpeg"];

    public DataFolder Folder { get; } = folder;

    /// <summary>The ids of the ingredient files, in id order. Temporary files and files with odd names are not ingredients.</summary>
    public IReadOnlyList<string> Ids() => IdsIn(Folder.IngredientFolder);

    /// <summary>The ids FdcImporter has a suggestion for, in id order.</summary>
    public IReadOnlyList<string> SuggestionIds() => IdsIn(Folder.SuggestionFolder);

    public bool Exists(string id) => DataFolder.IsValidId(id) && File.Exists(Folder.IngredientPath(id));

    /// <summary>The ingredient file, or null when there is none (or the id cannot be an ingredient id).</summary>
    public StoredIngredient? Read(string id)
    {
        if (!Exists(id))
        {
            return null;
        }

        var text = File.ReadAllText(Folder.IngredientPath(id));
        try
        {
            return new StoredIngredient(id, text, StoredDish.VersionOf(text), IngredientFile.Parse(text), null);
        }
        catch (JsonException ex)
        {
            return new StoredIngredient(id, text, StoredDish.VersionOf(text), null, ex.Message);
        }
    }

    /// <summary>What FdcImporter suggested for an ingredient; null when there is no suggestion, it cannot be read, or it is about another ingredient.</summary>
    public NutritionSuggestion? ReadSuggestion(string id)
    {
        if (!DataFolder.IsValidId(id) || !File.Exists(Folder.SuggestionPath(id)))
        {
            return null;
        }

        return NutritionSuggestion.TryParse(File.ReadAllText(Folder.SuggestionPath(id))) is { } suggestion && suggestion.IngredientId == id
            ? suggestion
            : null;
    }

    /// <summary>The names of the image files of <c>data/images/out</c>, in name order; what an ingredient can name as its picture.</summary>
    public IReadOnlyList<string> ImageFiles() =>
        !Directory.Exists(Folder.ImageFolder)
            ? []
            : [.. Directory.GetFiles(Folder.ImageFolder)
                .Select(Path.GetFileName)
                .OfType<string>()
                .Where(IsImageName)
                .Order(StringComparer.Ordinal)];

    /// <summary>The path of an image file by its name, or null when the name is not a plain image file name or there is no such file.</summary>
    public string? ImagePath(string name)
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
        && ImageExtensions.Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase);

    /// <summary>Writes through a temporary file and a rename, so a crash leaves the old file or the new one, never half of one.</summary>
    public void Write(string id, string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var path = Folder.IngredientPath(id);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.Move(temporary, path, overwrite: true);
    }

    private static IReadOnlyList<string> IdsIn(string path) =>
        !Directory.Exists(path)
            ? []
            : [.. Directory.GetFiles(path, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .OfType<string>()
                .Where(DataFolder.IsValidId)
                .Order(StringComparer.Ordinal)];

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._-]*\z")]
    private static partial Regex ImageNamePattern();
}
