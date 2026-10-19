using MamCom.DataBuilder.Schemas;

namespace MamCom.DataBuilder.Tests.Schemas;

/// <summary>The data folder copied next to the test assembly, laid out like the repo's <c>data</c> folder.</summary>
internal static class DataFiles
{
    public static readonly string Root = Path.Combine(AppContext.BaseDirectory, "data");

    public static readonly SchemaSet Schemas = SchemaSet.Load(Path.Combine(Root, "schemas"));

    /// <summary>Name (file name without extension) and text of every JSON file in a data folder, in name order.</summary>
    public static IReadOnlyList<(string Name, string Text)> Folder(string folder) =>
        Directory.GetFiles(Path.Combine(Root, folder), "*.json")
            .Order(StringComparer.Ordinal)
            .Select(path => (Path.GetFileNameWithoutExtension(path), File.ReadAllText(path)))
            .ToArray();

    public static string Text(string folder, string name) => File.ReadAllText(Path.Combine(Root, folder, name + ".json"));
}
