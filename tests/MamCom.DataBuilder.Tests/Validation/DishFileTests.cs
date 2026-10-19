using System.Text.Json;
using MamCom.DataBuilder.Loading;
using MamCom.DataBuilder.Tests.Schemas;

namespace MamCom.DataBuilder.Tests.Validation;

/// <summary>The text of a dish file is one fixed format, so a dish read and written back is the same bytes.</summary>
public class DishFileTests
{
    public static TheoryData<string> SeedDishes()
    {
        var data = new TheoryData<string>();
        foreach (var (name, _) in DataFiles.Folder("dishes"))
        {
            data.Add(name);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(SeedDishes))]
    public void A_seed_dish_read_and_written_back_is_the_same_bytes(string name)
    {
        var text = DataFiles.Text("dishes", name);

        DishFile.Serialize(DishFile.Parse(text)).ShouldBe(text);
    }

    [Fact]
    public void The_text_is_indented_with_two_spaces_and_ends_with_one_line_feed()
    {
        var text = DishFile.Serialize(DishFile.Parse(DataFiles.Text("dishes", "rau-muong-luoc")));

        text.ShouldStartWith("{\n  \"id\": \"rau-muong-luoc\"");
        text.ShouldEndWith("}\n");
        text.ShouldNotEndWith("\n\n");
        text.ShouldNotContain('\r');
    }

    [Fact]
    public void Vietnamese_is_written_as_it_is_and_nulls_are_written()
    {
        var text = DishFile.Serialize(DishFile.Parse(DataFiles.Text("dishes", "rau-muong-luoc")));

        text.ShouldContain("Rau muống luộc");
        text.ShouldNotContain(@"\u");
        text.ShouldContain("\"reviewedBy\": null");
    }

    [Fact]
    public void Text_that_is_not_a_dish_is_a_json_error()
    {
        Should.Throw<JsonException>(() => DishFile.Parse("{ not json"));
        Should.Throw<JsonException>(() => DishFile.Parse("null"));
        Should.Throw<JsonException>(() => DishFile.Parse("""{ "id": "x" }"""));
    }

    [Fact]
    public void The_arguments_are_checked()
    {
        Should.Throw<ArgumentNullException>(() => DishFile.Serialize(null!));
        Should.Throw<ArgumentNullException>(() => DishFile.Parse(null!));
    }
}
