using System.Text.Json;
using MamCom.DataBuilder.Loading;
using MamCom.DataBuilder.Tests.Schemas;

namespace MamCom.DataBuilder.Tests.Validation;

/// <summary>The text of an ingredient file is one fixed format, so an ingredient read and written back is the same bytes.</summary>
public class IngredientFileTests
{
    public static TheoryData<string> SeedIngredients()
    {
        var data = new TheoryData<string>();
        foreach (var (name, _) in DataFiles.Folder("ingredients"))
        {
            data.Add(name);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(SeedIngredients))]
    public void A_seed_ingredient_read_and_written_back_is_the_same_bytes(string name)
    {
        var text = DataFiles.Text("ingredients", name);

        IngredientFile.Serialize(IngredientFile.Parse(text)).ShouldBe(text);
    }

    [Fact]
    public void The_text_is_indented_with_two_spaces_and_ends_with_one_line_feed()
    {
        var text = IngredientFile.Serialize(IngredientFile.Parse(DataFiles.Text("ingredients", "rau-muong")));

        text.ShouldStartWith("{\n  \"id\": \"rau-muong\"");
        text.ShouldEndWith("}\n");
        text.ShouldNotEndWith("\n\n");
        text.ShouldNotContain('\r');
    }

    [Fact]
    public void Vietnamese_is_written_as_it_is_and_nulls_are_written()
    {
        var text = IngredientFile.Serialize(IngredientFile.Parse(DataFiles.Text("ingredients", "rau-muong")));

        text.ShouldContain("Rau muống");
        text.ShouldNotContain(@"\u");
        text.ShouldContain("\"kcal\": null");
    }

    [Fact]
    public void Text_that_is_not_an_ingredient_is_a_json_error()
    {
        Should.Throw<JsonException>(() => IngredientFile.Parse("{ not json"));
        Should.Throw<JsonException>(() => IngredientFile.Parse("null"));
        Should.Throw<JsonException>(() => IngredientFile.Parse("""{ "id": "x" }"""));
    }

    [Fact]
    public void The_arguments_are_checked()
    {
        Should.Throw<ArgumentNullException>(() => IngredientFile.Serialize(null!));
        Should.Throw<ArgumentNullException>(() => IngredientFile.Parse(null!));
    }
}
