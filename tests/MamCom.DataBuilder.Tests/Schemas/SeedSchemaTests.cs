using MamCom.DataBuilder.Schemas;

namespace MamCom.DataBuilder.Tests.Schemas;

/// <summary>The data that ships must satisfy its schemas (PLAN 2.1).</summary>
public class SeedSchemaTests
{
    private static readonly string[] ConfigNames = ["cooking-defaults", "nutrition-defaults", "portion-defaults", "spinner-defaults", "validation-defaults"];

    private static string Describe(IEnumerable<SchemaIssue> issues) =>
        string.Join(Environment.NewLine, issues.Select(i => $"  {i.Location} [{i.Keyword}] {i.Message}"));

    [Fact]
    public void There_is_a_schema_for_ingredients_dishes_and_each_config_file()
    {
        DataFiles.Schemas.Names.ShouldBe(["cooking-defaults", "dish", "draft-report", "ingredient", "nutrition-defaults", "nutrition-suggestion", "portion-defaults", "spinner-defaults", "validation-defaults"]);
    }

    [Fact]
    public void Every_seed_ingredient_is_valid()
    {
        var files = DataFiles.Folder("ingredients");
        files.ShouldNotBeEmpty();

        foreach (var (name, text) in files)
        {
            var issues = DataFiles.Schemas.Validate("ingredient", text);
            issues.ShouldBeEmpty($"ingredients/{name}.json:{Environment.NewLine}{Describe(issues)}");
        }
    }

    [Fact]
    public void Every_seed_dish_is_valid()
    {
        var files = DataFiles.Folder("dishes");
        files.ShouldNotBeEmpty();

        foreach (var (name, text) in files)
        {
            var issues = DataFiles.Schemas.Validate("dish", text);
            issues.ShouldBeEmpty($"dishes/{name}.json:{Environment.NewLine}{Describe(issues)}");
        }
    }

    [Fact]
    public void Every_config_file_is_valid_and_has_a_schema()
    {
        DataFiles.Folder("config").Select(f => f.Name).ShouldBe(ConfigNames);

        foreach (var (name, text) in DataFiles.Folder("config"))
        {
            var issues = DataFiles.Schemas.Validate(name, text);
            issues.ShouldBeEmpty($"config/{name}.json:{Environment.NewLine}{Describe(issues)}");
        }
    }

    [Fact]
    public void An_unknown_schema_name_is_an_error_not_a_pass()
    {
        Should.Throw<ArgumentException>(() => DataFiles.Schemas.Validate("recipe", "{}"));
    }

    [Fact]
    public void A_folder_without_schemas_loads_as_an_empty_set()
    {
        var empty = Directory.CreateTempSubdirectory("mamcom-schemas");
        try
        {
            SchemaSet.Load(empty.FullName).Names.ShouldBeEmpty();
        }
        finally
        {
            empty.Delete(recursive: true);
        }
    }

    [Fact]
    public void A_schema_file_that_is_not_json_schema_is_reported_by_name()
    {
        var folder = Directory.CreateTempSubdirectory("mamcom-schemas");
        try
        {
            File.WriteAllText(Path.Combine(folder.FullName, "broken.schema.json"), "{ not json");

            var error = Should.Throw<InvalidOperationException>(() => SchemaSet.Load(folder.FullName));
            error.Message.ShouldContain("broken.schema.json");
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }
}
