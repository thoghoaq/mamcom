using System.Text;
using System.Text.Json.Nodes;
using MamCom.Core;
using MamCom.Studio.Review;

namespace MamCom.Studio.Tests;

internal sealed class FixedClock(DateOnly today) : IClock
{
    public DateOnly Today { get; set; } = today;

    public DateTimeOffset Now => new(Today.ToDateTime(new TimeOnly(9, 0)), TimeSpan.Zero);
}

/// <summary>A private copy of the repo's data folder (schemas, config, ingredients, seed dishes) for one test to change freely.</summary>
internal sealed class StudioData : IDisposable
{
    public static readonly DateOnly Today = new(2026, 10, 19);

    private static readonly string SourceRoot = Path.Combine(AppContext.BaseDirectory, "data");

    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("mamcom-studio");

    public StudioData()
    {
        Copy(SourceRoot, Root);
        Folder = new DataFolder(Root);
        Store = new DishStore(Folder);
        Loader = new KitchenLoader(Folder);
        Clock = new FixedClock(Today);
        Reviewer = new DishReviewer(Store, Loader, Clock);
        Ingredients = new IngredientStore(Folder);
        Images = new ImageLibrary(Folder);
        IngredientReviewer = new IngredientReviewer(Ingredients, Loader, Clock);
    }

    public string Root => _directory.FullName;

    public DataFolder Folder { get; }

    public DishStore Store { get; }

    public KitchenLoader Loader { get; }

    public FixedClock Clock { get; }

    public DishReviewer Reviewer { get; }

    public IngredientStore Ingredients { get; }

    public ImageLibrary Images { get; }

    public IngredientReviewer IngredientReviewer { get; }

    public string PathOf(string relative) => Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));

    public string ReadText(string relative) => File.ReadAllText(PathOf(relative));

    public JsonObject ReadJson(string relative) => JsonNode.Parse(ReadText(relative))!.AsObject();

    public void WriteText(string relative, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathOf(relative))!);
        File.WriteAllText(PathOf(relative), text, new UTF8Encoding(false));
    }

    public void WriteJson(string relative, JsonObject content) => WriteText(relative, content.ToJsonString());

    public void EditJson(string relative, Action<JsonObject> change)
    {
        var content = ReadJson(relative);
        change(content);
        WriteJson(relative, content);
    }

    public bool Exists(string relative) => File.Exists(PathOf(relative));

    public void Delete(string relative) => File.Delete(PathOf(relative));

    /// <summary>Moves a seed dish into <c>dishes/_drafts</c>, as RecipeDrafter would have left it.</summary>
    public void MakeDraft(string id)
    {
        Directory.CreateDirectory(PathOf("dishes/_drafts"));
        File.Move(PathOf($"dishes/{id}.json"), PathOf($"dishes/_drafts/{id}.json"), overwrite: false);
    }

    /// <summary>Writes a report the way RecipeDrafter does.</summary>
    public void WriteReport(string id, string validation, string problemMessage = "msg")
    {
        WriteText($"dishes/_drafts/{id}.report.json", $$"""
            {
              "dishId": "{{id}}",
              "model": "claude-opus-5-5",
              "draftedAt": "2026-10-19",
              "validation": "{{validation}}",
              "stopReason": "end_turn",
              "problems": [
                { "severity": "warning", "code": "missing-ingredient", "location": "", "message": "{{problemMessage}}" }
              ],
              "missingIngredients": [ { "name": "Nước dừa tươi", "gramsEdiblePerServing": 80, "note": null } ],
              "usage": { "inputTokens": 1, "outputTokens": 1, "cacheCreationInputTokens": 0, "cacheReadInputTokens": 0 }
            }
            """);
    }

    /// <summary>
    /// Writes a suggestion the way FdcImporter does, with test-only numbers: each candidate is its FDC id and the six numbers
    /// (kcal, protein, fat, carb, fibre, sodium) per 100g, a null for one FDC does not state.
    /// </summary>
    public void WriteSuggestion(string ingredientId, params (long FdcId, double?[] Per100g)[] candidates)
    {
        string[] names = ["kcal", "proteinG", "fatG", "carbG", "fiberG", "sodiumMg"];
        var list = new JsonArray();
        var rank = 1;
        foreach (var (fdcId, per100g) in candidates)
        {
            var numbers = new JsonObject();
            var missing = new JsonArray();
            for (var i = 0; i < names.Length; i++)
            {
                numbers[names[i]] = per100g[i];
                if (per100g[i] is null)
                {
                    missing.Add(names[i]);
                }
            }

            list.Add(new JsonObject
            {
                ["rank"] = rank++,
                ["fdcId"] = fdcId,
                ["description"] = $"Test food {fdcId}, raw",
                ["dataType"] = "Foundation",
                ["foodCategory"] = "Test foods",
                ["detailsUrl"] = $"https://fdc.nal.usda.gov/food-details/{fdcId}/nutrients",
                ["per100g"] = numbers,
                ["kcalSource"] = per100g[0] is null ? null : "Energy",
                ["missing"] = missing,
                ["reason"] = "ứng viên thử",
            });
        }

        WriteJson($"_suggestions/nutrition/{ingredientId}.json", new JsonObject
        {
            ["ingredientId"] = ingredientId,
            ["query"] = "test query",
            ["retrievedAt"] = "2026-10-19",
            ["source"] = "usda-fdc",
            ["totalHits"] = 12,
            ["candidates"] = list,
        });
    }

    /// <summary>Puts a file in <c>images/out</c>; the content is one byte, because only the name matters to the checks.</summary>
    public void WriteImage(string name)
    {
        Directory.CreateDirectory(PathOf("images/out"));
        File.WriteAllBytes(PathOf($"images/out/{name}"), [0]);
    }

    /// <summary>Sets one nutrient, per 100g, on every ingredient of a dish so the dish's own value is known. Test-only numbers.</summary>
    public void SetNutrient(string dishId, string nutrient, double per100g)
    {
        foreach (var line in ReadJson($"dishes/{dishId}.json")["ingredients"]!.AsArray())
        {
            EditJson($"ingredients/{line!["ingredientId"]!.GetValue<string>()}.json", ingredient =>
            {
                ingredient["nutritionSource"]!["db"] = "usda-fdc";
                ingredient["nutritionSource"]!["ref"] = "0000000";
                ingredient["nutritionSource"]!["matchedName"] = "test-only entry";
                ingredient["nutritionPer100gEdible"]![nutrient] = per100g;
            });
        }
    }

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
