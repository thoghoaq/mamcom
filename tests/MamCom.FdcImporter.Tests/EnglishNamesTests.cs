using MamCom.FdcImporter.Names;

namespace MamCom.FdcImporter.Tests;

public class EnglishNamesTests
{
    [Fact]
    public void A_file_with_rows_gives_one_search_text_per_id()
    {
        var names = EnglishNames.Parse("id,english\ngao,rice white raw\ntoi,garlic raw\n");

        names["gao"].ShouldBe("rice white raw");
        names["toi"].ShouldBe("garlic raw");
        names.Count.ShouldBe(2);
    }

    [Fact]
    public void Windows_line_ends_a_byte_order_mark_blank_lines_and_spaces_are_tolerated()
    {
        var names = EnglishNames.Parse((char)0xFEFF + "id,english\r\n\r\n gao , rice raw \r\n\r\n");

        names["gao"].ShouldBe("rice raw");
        names.Count.ShouldBe(1);
    }

    [Fact]
    public void A_quoted_field_may_hold_a_comma_and_a_doubled_quote()
    {
        var names = EnglishNames.Parse("id,english\ngao,\"rice, white, raw\"\nsa,\"lemon \"\"grass\"\" raw\"\n");

        names["gao"].ShouldBe("rice, white, raw");
        names["sa"].ShouldBe("lemon \"grass\" raw");
    }

    [Fact]
    public void A_quote_in_the_middle_of_a_field_is_just_a_character()
    {
        var names = EnglishNames.Parse("id,english\ngao,5\" rice\n");

        names["gao"].ShouldBe("5\" rice");
    }

    [Fact]
    public void A_header_only_file_is_an_empty_list()
    {
        EnglishNames.Parse("id,english\n").ShouldBeEmpty();
    }

    [Theory]
    [InlineData("", "empty")]
    [InlineData("\n\n", "empty")]
    [InlineData("name,english\ngao,rice\n", "line 1")]
    [InlineData("id,english,extra\ngao,rice,x\n", "line 1")]
    [InlineData("id\ngao\n", "line 1")]
    [InlineData("id,english\ngao\n", "line 2")]
    [InlineData("id,english\ngao,rice,white\n", "line 2")]
    [InlineData("id,english\n,rice\n", "line 2")]
    [InlineData("id,english\ngao,\n", "line 2")]
    [InlineData("id,english\ngao,  \n", "line 2")]
    [InlineData("id,english\ngao,rice\ngao,rice again\n", "line 3")]
    [InlineData("id,english\ngao,\"rice\n", "line 2")]
    public void A_file_that_is_not_in_the_right_shape_is_rejected_naming_the_line(string csv, string expected)
    {
        Should.Throw<FormatException>(() => EnglishNames.Parse(csv)).Message.ShouldContain(expected);
    }

    [Fact]
    public void A_duplicate_id_is_named()
    {
        Should.Throw<FormatException>(() => EnglishNames.Parse("id,english\ngao,rice\ngao,rice again\n")).Message.ShouldContain("'gao'");
    }

    [Fact]
    public void The_seed_names_file_names_every_seed_ingredient_and_nothing_else()
    {
        using var data = new TempData();
        var names = EnglishNames.Parse(File.ReadAllText(Path.Combine(data.Root, "plan", "ingredient-names-en.csv")));
        var ingredients = Directory.GetFiles(Path.Combine(data.Root, "ingredients"), "*.json").Select(Path.GetFileNameWithoutExtension).Order(StringComparer.Ordinal).ToArray();

        names.Keys.Order(StringComparer.Ordinal).ShouldBe(ingredients);
        names.Values.ShouldAllBe(v => v.Length > 0 && v == v.Trim());
    }
}
