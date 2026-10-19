using System.Text;
using System.Text.Json;
using MamCom.DataBuilder.Loading;

namespace MamCom.Studio.Review;

/// <summary>Reads and writes dish files. It is the only place in Studio that touches dish files.</summary>
public sealed class DishStore(DataFolder folder)
{
    public DataFolder Folder { get; } = folder;

    /// <summary>The ids of the dish files of a location, in id order. Reports, temporary files and files with odd names are not dishes.</summary>
    public IReadOnlyList<string> Ids(DishLocation location)
    {
        var path = Folder.FolderOf(location);
        return !Directory.Exists(path)
            ? []
            : [.. Directory.GetFiles(path, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .OfType<string>()
                .Where(DataFolder.IsValidId)
                .Order(StringComparer.Ordinal)];
    }

    /// <summary>The ids that have a report from RecipeDrafter, in id order.</summary>
    public IReadOnlyList<string> ReportIds()
    {
        var path = Folder.FolderOf(DishLocation.Draft);
        const string suffix = ".report.json";
        return !Directory.Exists(path)
            ? []
            : [.. Directory.GetFiles(path, "*" + suffix)
                .Select(file => Path.GetFileName(file)[..^suffix.Length])
                .Where(DataFolder.IsValidId)
                .Order(StringComparer.Ordinal)];
    }

    public bool Exists(DishLocation location, string id) => DataFolder.IsValidId(id) && File.Exists(Folder.DishPath(location, id));

    /// <summary>The dish file, or null when there is none (or the id cannot be a dish id).</summary>
    public StoredDish? Read(DishLocation location, string id)
    {
        if (!Exists(location, id))
        {
            return null;
        }

        var text = File.ReadAllText(Folder.DishPath(location, id));
        try
        {
            return new StoredDish(location, id, text, StoredDish.VersionOf(text), DishFile.Parse(text), null);
        }
        catch (JsonException ex)
        {
            return new StoredDish(location, id, text, StoredDish.VersionOf(text), null, ex.Message);
        }
    }

    /// <summary>The note RecipeDrafter left for a draft; null when there is none or it cannot be read.</summary>
    public DraftNote? ReadNote(string id)
    {
        if (!DataFolder.IsValidId(id) || !File.Exists(Folder.ReportPath(id)))
        {
            return null;
        }

        return DraftNote.TryParse(File.ReadAllText(Folder.ReportPath(id)));
    }

    /// <summary>Writes through a temporary file and a rename, so a crash leaves the old file or the new one, never half of one.</summary>
    public void Write(DishLocation location, string id, string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var path = Folder.DishPath(location, id);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.Move(temporary, path, overwrite: true);
    }

    /// <summary>Removes the draft file and its note, once the dish lives in <c>dishes/</c>.</summary>
    public void DeleteDraft(string id)
    {
        File.Delete(Folder.DishPath(DishLocation.Draft, id));
        File.Delete(Folder.ReportPath(id));
    }
}
