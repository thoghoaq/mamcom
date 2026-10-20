using System.Text;
using System.Text.Json.Nodes;
using MamCom.DataBuilder.Cli;
using MamCom.DataBuilder.Loading;
using MamCom.DataBuilder.Tests.Schemas;
using MamCom.DataBuilder.Validation;

namespace MamCom.DataBuilder.Tests;

/// <summary>
/// A scratch copy of the test data folder (the frozen seed under the live schemas and config) that a test can break one file at a time. Deleted with the object.
/// Numbers a test writes into it are test-only, not real data.
/// </summary>
internal sealed class TempData : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("mamcom-data");

    public TempData()
    {
        Copy(DataFiles.Root, Root);
    }

    public string Root => _directory.FullName;

    /// <summary>A sibling folder for build output.</summary>
    public string Out => Path.Combine(Path.GetDirectoryName(Root)!, Path.GetFileName(Root) + "-out");

    public string PathOf(string folder, string name) => Path.Combine(Root, folder, name + ".json");

    public JsonObject Read(string folder, string name) => JsonNode.Parse(File.ReadAllText(PathOf(folder, name)))!.AsObject();

    public void Write(string folder, string name, JsonObject content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathOf(folder, name))!);
        File.WriteAllText(PathOf(folder, name), content.ToJsonString(), new UTF8Encoding(false));
    }

    public void Edit(string folder, string name, Action<JsonObject> change)
    {
        var content = Read(folder, name);
        change(content);
        Write(folder, name, content);
    }

    public void WriteText(string folder, string name, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathOf(folder, name))!);
        File.WriteAllText(PathOf(folder, name), text, new UTF8Encoding(false));
    }

    public void Delete(string folder, string name) => File.Delete(PathOf(folder, name));

    /// <summary>Copies a file under a new name, optionally changing it; the id inside is left alone unless the change sets it.</summary>
    public void CopyFile(string folder, string from, string to, Action<JsonObject>? change = null)
    {
        var content = Read(folder, from);
        change?.Invoke(content);
        Write(folder, to, content);
    }

    public void WriteImage(string name)
    {
        Directory.CreateDirectory(Path.Combine(Root, "images", "out"));
        File.WriteAllBytes(Path.Combine(Root, "images", "out", name), [0]);
    }

    public LoadedData Load() => DataLoader.Load(Root);

    public ValidationReport Validate(ValidationMode mode = ValidationMode.Dev) => DataValidator.Validate(Load(), mode);

    /// <summary>Runs the command line against this folder; returns the exit code and what it printed.</summary>
    public (int Code, string Output, string Error) Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = CommandLine.Run(args, output, error);
        return (code, output.ToString(), error.ToString());
    }

    /// <summary>Gives every ingredient a real-looking source and every dish a review, so a release build has nothing to refuse.</summary>
    public void MakeReleasable()
    {
        foreach (var path in Directory.GetFiles(Path.Combine(Root, "ingredients"), "*.json"))
        {
            var name = Path.GetFileNameWithoutExtension(path);
            Edit("ingredients", name, ingredient =>
            {
                ingredient["nutritionSource"]!["db"] = "usda-fdc";
                ingredient["nutritionSource"]!["ref"] = "0000000";
                ingredient["nutritionSource"]!["matchedName"] = "test-only entry";
            });
        }

        foreach (var path in Directory.GetFiles(Path.Combine(Root, "dishes"), "*.json"))
        {
            var name = Path.GetFileNameWithoutExtension(path);
            Edit("dishes", name, dish =>
            {
                dish["status"] = "reviewed";
                dish["provenance"]!["reviewedBy"] = "test-only";
                dish["provenance"]!["reviewedAt"] = "2026-10-19";
            });
        }
    }

    /// <summary>Sets one nutrient, per 100g, on every ingredient of a dish so the dish's own value is known.</summary>
    public void SetNutrient(string dishId, string nutrient, double per100g)
    {
        foreach (var line in Read("dishes", dishId)["ingredients"]!.AsArray())
        {
            Edit("ingredients", line!["ingredientId"]!.GetValue<string>(), ingredient =>
            {
                ingredient["nutritionSource"]!["db"] = "usda-fdc";
                ingredient["nutritionSource"]!["ref"] = "0000000";
                ingredient["nutritionSource"]!["matchedName"] = "test-only entry";
                ingredient["nutritionPer100gEdible"]![nutrient] = per100g;
            });
        }
    }

    public void Dispose()
    {
        _directory.Delete(recursive: true);
        var outFolder = new DirectoryInfo(Out);
        if (outFolder.Exists)
        {
            outFolder.Delete(recursive: true);
        }
    }

    private static void Copy(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var file in Directory.GetFiles(from))
        {
            File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
        }

        foreach (var folder in Directory.GetDirectories(from))
        {
            Copy(folder, Path.Combine(to, Path.GetFileName(folder)));
        }
    }
}
