using System.Text;
using System.Text.Json.Nodes;
using MamCom.DataBuilder.Loading;
using MamCom.DataBuilder.Validation;

namespace MamCom.ImageTool.Tests;

/// <summary>A private copy of the repo's data folder (schemas, config, ingredients, seed dishes) for one test to change freely.</summary>
internal sealed class ImageData : IDisposable
{
    private static readonly string SourceRoot = Path.Combine(AppContext.BaseDirectory, "data");

    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("mamcom-imagetool");

    public ImageData()
    {
        Copy(SourceRoot, Root);
    }

    public string Root => _directory.FullName;

    public string PathOf(string relative) => Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));

    public string ReadText(string relative) => File.ReadAllText(PathOf(relative));

    public bool Exists(string relative) => File.Exists(PathOf(relative));

    public void WriteText(string relative, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathOf(relative))!);
        File.WriteAllText(PathOf(relative), text, new UTF8Encoding(false));
    }

    public void EditJson(string relative, Action<JsonObject> change)
    {
        var content = JsonNode.Parse(ReadText(relative))!.AsObject();
        change(content);
        WriteText(relative, content.ToJsonString());
    }

    /// <summary>Puts an original in <c>images/raw</c>.</summary>
    public string AddOriginal(string fileName, byte[] bytes)
    {
        Directory.CreateDirectory(PathOf("images/raw"));
        var path = PathOf($"images/raw/{fileName}");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    /// <summary>The files in <c>images/out</c>, by name; empty when there is no folder.</summary>
    public string[] OutFiles() =>
        Directory.Exists(PathOf("images/out"))
            ? [.. Directory.GetFiles(PathOf("images/out")).Select(f => Path.GetFileName(f)).Order(StringComparer.Ordinal)]
            : [];

    /// <summary>What <c>DataBuilder validate</c> would say about the whole folder.</summary>
    public IReadOnlyList<Diagnostic> Validate() => DataValidator.Validate(DataLoader.Load(Root), ValidationMode.Dev).Diagnostics;

    public void Dispose() => _directory.Delete(recursive: true);

    private static void Copy(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var file in Directory.GetFiles(from))
        {
            File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
        }

        foreach (var directory in Directory.GetDirectories(from))
        {
            Copy(directory, Path.Combine(to, Path.GetFileName(directory)));
        }
    }
}
