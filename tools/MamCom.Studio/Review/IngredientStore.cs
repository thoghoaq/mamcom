using System.Text;
using System.Text.Json;
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
public sealed class IngredientStore(DataFolder folder)
{
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
}
