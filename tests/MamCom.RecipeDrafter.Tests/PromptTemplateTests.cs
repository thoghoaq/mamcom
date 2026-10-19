using System.Text.RegularExpressions;
using MamCom.RecipeDrafter.Templates;

namespace MamCom.RecipeDrafter.Tests;

public partial class PromptTemplateTests
{
    [GeneratedRegex(@"\{\{\s*([a-z_]+)\s*\}\}")]
    private static partial Regex Place();

    private static Dictionary<string, string> Values(params (string Key, string Value)[] pairs) => pairs.ToDictionary(p => p.Key, p => p.Value);

    [Fact]
    public void Places_are_filled_wherever_they_stand_and_as_often_as_they_appear()
    {
        var text = PromptTemplate.Render("Món {{name}} ({{name}}), loại {{ slot }}.", Values(("name", "Cá kho"), ("slot", "man")));

        text.ShouldBe("Món Cá kho (Cá kho), loại man.");
    }

    [Fact]
    public void Text_without_places_comes_back_as_it_is()
    {
        PromptTemplate.Render("Không có chỗ điền {nào} cả.", Values()).ShouldBe("Không có chỗ điền {nào} cả.");
    }

    [Fact]
    public void A_value_is_not_searched_for_places_of_its_own()
    {
        PromptTemplate.Render("{{a}}", Values(("a", "{{b}}"))).ShouldBe("{{b}}");
    }

    [Fact]
    public void A_place_with_no_value_is_an_error_naming_it()
    {
        Should.Throw<FormatException>(() => PromptTemplate.Render("Món {{name}} và {{slot}}", Values(("name", "x")))).Message.ShouldContain("{{slot}}");
    }

    [Fact]
    public void A_value_with_no_place_is_an_error_naming_it()
    {
        var error = Should.Throw<FormatException>(() => PromptTemplate.Render("Món {{name}}", Values(("name", "x"), ("zeta", "1"), ("alpha", "2"))));

        error.Message.ShouldContain("alpha, zeta");
    }

    [Fact]
    public void The_shipped_system_prompt_has_exactly_the_places_the_readme_documents()
    {
        var places = PlacesOf("system.md");

        places.ShouldBe(["ingredient_catalog", "protein_max_grams", "protein_min_grams", "season_ids"]);
    }

    [Fact]
    public void The_shipped_dish_prompt_has_exactly_the_places_the_readme_documents()
    {
        PlacesOf("dish.md").ShouldBe(["name", "regions", "slot", "slot_label"]);
    }

    [Fact]
    public void Every_place_the_programs_fills_is_documented_in_the_prompt_readme()
    {
        var readme = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "prompts", "README.md"));

        foreach (var place in PlacesOf("system.md").Concat(PlacesOf("dish.md")))
        {
            readme.ShouldContain("{{" + place + "}}");
        }
    }

    [Fact]
    public void The_shipped_prompts_forbid_nutrition_numbers_copying_and_invented_ids()
    {
        var system = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "prompts", "system.md"));

        system.ShouldContain("Không đưa bất kỳ số dinh dưỡng nào");
        system.ShouldContain("Không chép");
        system.ShouldContain("KHÔNG bịa id");
        system.ShouldContain("missingIngredients");
    }

    private static string[] PlacesOf(string file) =>
        [.. Place().Matches(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "prompts", file))).Select(m => m.Groups[1].Value).Distinct().Order(StringComparer.Ordinal)];
}
