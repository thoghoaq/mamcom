namespace MamCom.Core.Tests;

internal static class TestFiles
{
    /// <summary>Text of a file under data/config, copied next to the test assembly by the project file.</summary>
    public static string Config(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "config", fileName));
}
