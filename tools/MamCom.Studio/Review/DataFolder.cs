using System.Text.RegularExpressions;

namespace MamCom.Studio.Review;

public enum DishLocation
{
    /// <summary><c>data/dishes/&lt;id&gt;.json</c>: the dishes of the app.</summary>
    Data,

    /// <summary><c>data/dishes/_drafts/&lt;id&gt;.json</c>: drafts written by RecipeDrafter, not read by the build.</summary>
    Draft,
}

/// <summary>Where the files of the <c>data</c> folder are. Dish ids become file names, so only kebab-case ASCII ids are accepted.</summary>
public sealed partial class DataFolder
{
    private const string DraftFolder = "_drafts";

    public DataFolder(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        Root = Path.GetFullPath(root);
    }

    public string Root { get; }

    public string SchemaFolder => Path.Combine(Root, "schemas");

    /// <summary>True for an id that is safe as a file name: kebab-case ASCII, like every dish id in the data.</summary>
    public static bool IsValidId(string? id) => id is not null && IdPattern().IsMatch(id);

    public string FolderOf(DishLocation location) =>
        location == DishLocation.Data ? Path.Combine(Root, "dishes") : Path.Combine(Root, "dishes", DraftFolder);

    /// <exception cref="ArgumentException">The id is not a valid dish id.</exception>
    public string DishPath(DishLocation location, string id) => Path.Combine(FolderOf(location), CheckedId(id) + ".json");

    /// <exception cref="ArgumentException">The id is not a valid dish id.</exception>
    public string ReportPath(string id) => Path.Combine(FolderOf(DishLocation.Draft), CheckedId(id) + ".report.json");

    /// <summary>Where the ingredient files are.</summary>
    public string IngredientFolder => Path.Combine(Root, "ingredients");

    /// <summary>Where FdcImporter leaves its suggestions of nutrition values, one file per ingredient.</summary>
    public string SuggestionFolder => Path.Combine(Root, "_suggestions", "nutrition");

    /// <summary>Where the images of the app are (<c>data/images/out</c>); an ingredient or a dish names a file in it.</summary>
    public string ImageFolder => Path.Combine(Root, "images", "out");

    /// <exception cref="ArgumentException">The id is not a valid ingredient id.</exception>
    public string IngredientPath(string id) => Path.Combine(IngredientFolder, CheckedId(id) + ".json");

    /// <exception cref="ArgumentException">The id is not a valid ingredient id.</exception>
    public string SuggestionPath(string id) => Path.Combine(SuggestionFolder, CheckedId(id) + ".json");

    /// <summary>The path of an ingredient file under the data folder, with forward slashes, as the validator names files.</summary>
    public static string RelativeIngredientFile(string id) => $"ingredients/{id}.json";

    /// <summary>The path of a dish file under the data folder, with forward slashes, as the validator names files.</summary>
    public static string RelativeFile(DishLocation location, string id) =>
        location == DishLocation.Data ? $"dishes/{id}.json" : $"dishes/{DraftFolder}/{id}.json";

    /// <summary>
    /// The configured folder, or else the nearest folder named <c>data</c> that has a <c>schemas</c> folder inside, looking
    /// from <paramref name="startFolder"/> upwards.
    /// </summary>
    /// <exception cref="DirectoryNotFoundException">There is no such folder.</exception>
    public static DataFolder Locate(string? configured, string startFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(startFolder);

        if (!string.IsNullOrWhiteSpace(configured))
        {
            var chosen = Path.GetFullPath(configured, startFolder);
            return Directory.Exists(chosen)
                ? new DataFolder(chosen)
                : throw new DirectoryNotFoundException($"The configured data folder '{chosen}' does not exist.");
        }

        for (var folder = new DirectoryInfo(Path.GetFullPath(startFolder)); folder is not null; folder = folder.Parent)
        {
            var candidate = Path.Combine(folder.FullName, "data");
            if (Directory.Exists(Path.Combine(candidate, "schemas")))
            {
                return new DataFolder(candidate);
            }
        }

        throw new DirectoryNotFoundException($"No data folder (with a schemas folder inside) was found at or above '{startFolder}'. Set Studio:DataFolder.");
    }

    private static string CheckedId(string id) =>
        IsValidId(id) ? id : throw new ArgumentException($"'{id}' is not a valid dish id.", nameof(id));

    // \z, not $: "$" would also accept an id that ends with a line feed.
    [GeneratedRegex(@"^[a-z0-9]+(-[a-z0-9]+)*\z")]
    private static partial Regex IdPattern();
}
