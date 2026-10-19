namespace MamCom.Core.Tests;

internal static class TestFiles
{
    /// <summary>Text of a file under data/config, copied next to the test assembly by the project file.</summary>
    public static string Config(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "config", fileName));

    /// <summary>Name and text of every JSON file in a data folder (<c>ingredients</c> or <c>dishes</c>), in name order.</summary>
    public static IReadOnlyList<(string FileName, string Text)> DataFolder(string folder) =>
        Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, folder), "*.json")
            .Order(StringComparer.Ordinal)
            .Select(path => (Path.GetFileName(path), File.ReadAllText(path)))
            .ToArray();
}
