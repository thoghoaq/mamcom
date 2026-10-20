using System.Text.Json.Nodes;
using MamCom.Core.Models;
using MamCom.DataBuilder.Loading;
using MamCom.DataBuilder.Schemas;
using MamCom.DataBuilder.Validation;

namespace MamCom.LiveData.Tests;

/// <summary>
/// What must stay true of the real <c>data/</c> folder whatever people do to it in Studio. These are the only tests that read
/// it; they do not assume how many dishes there are, which are reviewed, or which ingredients have nutrition. ADR-018.
/// </summary>
public class LiveDataTests
{
    private static readonly string Root = Path.Combine(AppContext.BaseDirectory, "data");

    private static string[] Files(string relative, string pattern = "*.json") =>
        Directory.Exists(Path.Combine(Root, relative))
            ? [.. Directory.GetFiles(Path.Combine(Root, relative), pattern).Order(StringComparer.Ordinal)]
            : [];

    private static string Name(string path) => Path.GetFileName(path);

    [Fact]
    public void The_live_data_loads_and_validates_without_an_error()
    {
        var report = DataValidator.Validate(DataLoader.Load(Root), ValidationMode.Dev);

        var errors = report.Diagnostics.Where(d => d.Severity == Severity.Error).Select(d => $"{d.File} {d.Location}: {d.Message}").ToArray();
        errors.ShouldBeEmpty("DataBuilder validate must be clean on the real data:\n" + string.Join("\n", errors));
    }

    [Fact]
    public void Every_live_dish_is_in_the_canonical_format_so_git_shows_only_what_changed()
    {
        var notCanonical = Files("dishes")
            .Where(path =>
            {
                var text = File.ReadAllText(path);
                return DishFile.Serialize(DishFile.Parse(text)) != text;
            })
            .Select(Name)
            .ToArray();

        notCanonical.ShouldBeEmpty("These dish files are not written the way Studio writes them: " + string.Join(", ", notCanonical));
    }

    [Fact]
    public void Every_live_ingredient_is_in_the_canonical_format()
    {
        var notCanonical = Files("ingredients")
            .Where(path =>
            {
                var text = File.ReadAllText(path);
                return IngredientFile.Serialize(IngredientFile.Parse(text)) != text;
            })
            .Select(Name)
            .ToArray();

        notCanonical.ShouldBeEmpty("These ingredient files are not written the way Studio writes them: " + string.Join(", ", notCanonical));
    }

    [Fact]
    public void A_live_ingredient_with_a_real_nutrition_source_says_who_confirmed_it_and_when()
    {
        var unconfirmed = Files("ingredients")
            .Select(path => (Name: Name(path), Source: IngredientFile.Parse(File.ReadAllText(path)).NutritionSource))
            .Where(i => i.Source.Db != NutritionDbs.Placeholder && (string.IsNullOrWhiteSpace(i.Source.VerifiedBy) || i.Source.VerifiedAt is null))
            .Select(i => i.Name)
            .ToArray();

        unconfirmed.ShouldBeEmpty("Numbers from a database need a person's confirmation (verifiedBy, verifiedAt): " + string.Join(", ", unconfirmed));
    }

    [Fact]
    public void Every_live_suggestion_satisfies_its_schema_and_is_about_the_ingredient_of_its_file_name()
    {
        var schemas = SchemaSet.Load(Path.Combine(Root, "schemas"));
        var problems = new List<string>();

        foreach (var path in Files("_suggestions/nutrition"))
        {
            var text = File.ReadAllText(path);
            var id = Path.GetFileNameWithoutExtension(path);
            problems.AddRange(schemas.Validate("nutrition-suggestion", text).Select(i => $"{Name(path)} {i.Location}: {i.Message}"));
            if (JsonNode.Parse(text)?["ingredientId"]?.GetValue<string>() != id)
            {
                problems.Add($"{Name(path)}: ingredientId is not '{id}'");
            }

            if (!File.Exists(Path.Combine(Root, "ingredients", id + ".json")))
            {
                problems.Add($"{Name(path)}: there is no ingredient '{id}'");
            }
        }

        problems.ShouldBeEmpty(string.Join("\n", problems));
    }

    [Fact]
    public void Every_live_draft_report_satisfies_its_schema_and_belongs_to_its_draft()
    {
        var schemas = SchemaSet.Load(Path.Combine(Root, "schemas"));
        var problems = new List<string>();

        foreach (var path in Files("dishes/_drafts", "*.report.json"))
        {
            var text = File.ReadAllText(path);
            var id = Name(path)[..^".report.json".Length];
            problems.AddRange(schemas.Validate("draft-report", text).Select(i => $"{Name(path)} {i.Location}: {i.Message}"));
            if (JsonNode.Parse(text)?["dishId"]?.GetValue<string>() != id)
            {
                problems.Add($"{Name(path)}: dishId is not '{id}'");
            }
        }

        problems.ShouldBeEmpty(string.Join("\n", problems));
    }

    [Fact]
    public void No_live_draft_is_already_reviewed_because_only_Studio_promotes_a_draft()
    {
        var promoted = Files("dishes/_drafts")
            .Where(path => !Name(path).EndsWith(".report.json", StringComparison.Ordinal))
            .Select(path => (Name: Name(path), Dish: DishFile.Parse(File.ReadAllText(path))))
            .Where(d => d.Dish.Status is DishStatus.Reviewed or DishStatus.CookTested)
            .Select(d => d.Name)
            .ToArray();

        promoted.ShouldBeEmpty("A draft must be approved in Studio, which moves it to data/dishes: " + string.Join(", ", promoted));
    }
}
