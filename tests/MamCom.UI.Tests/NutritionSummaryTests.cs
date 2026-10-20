using MamCom.Core.Models;
using MamCom.UI.Components;

namespace MamCom.UI.Tests;

public class NutritionSummaryTests : BunitContext
{
    private static readonly NutritionFacts Known = new(Kcal: 444, ProteinG: 17.6, FatG: 20, CarbG: 30.2, FiberG: 4, SodiumMg: 1285);

    [Fact]
    public void Shows_energy_with_an_approximation_sign_rounded_to_ten()
    {
        var cut = Render<NutritionSummary>(p => p.Add(x => x.Facts, Known));

        cut.Find(".nutrition__kcal").TextContent.ShouldBe("≈ 440 kcal");
    }

    [Fact]
    public void Shows_every_nutrient_with_its_label()
    {
        var cut = Render<NutritionSummary>(p => p.Add(x => x.Facts, Known));

        var facts = cut.FindAll(".nutrition__fact").Select(f => (f.QuerySelector("dt")!.TextContent, f.QuerySelector("dd")!.TextContent)).ToArray();

        facts.ShouldBe([
            ("Đạm", "≈ 18 g"),
            ("Béo", "≈ 20 g"),
            ("Bột đường", "≈ 30 g"),
            ("Chất xơ", "≈ 4 g"),
            ("Natri", "≈ 1.290 mg"),
        ]);
    }

    [Fact]
    public void Without_facts_every_number_is_unknown()
    {
        var cut = Render<NutritionSummary>();

        cut.Find(".nutrition__kcal").TextContent.ShouldBe("chưa đủ số liệu");
        cut.FindAll(".nutrition__fact dd").ShouldAllBe(dd => dd.TextContent == "chưa đủ số liệu");
    }

    [Fact]
    public void A_nutrient_that_is_unknown_shows_as_unknown_while_the_others_show()
    {
        var cut = Render<NutritionSummary>(p => p.Add(x => x.Facts, Known with { FatG = null }));

        cut.FindAll(".nutrition__fact dd").Select(dd => dd.TextContent).ShouldBe(["≈ 18 g", "chưa đủ số liệu", "≈ 30 g", "≈ 4 g", "≈ 1.290 mg"]);
    }

    [Fact]
    public void Names_the_ingredients_whose_data_is_missing()
    {
        var cut = Render<NutritionSummary>(p => p
            .Add(x => x.Facts, Known)
            .Add(x => x.MissingIngredientNames, ["Cá lóc", "Nước mắm"]));

        cut.Find(".nutrition__missing").TextContent.ShouldBe("Chưa có số liệu dinh dưỡng của: Cá lóc, Nước mắm.");
    }

    [Fact]
    public void Shows_no_missing_note_when_nothing_is_missing()
    {
        var cut = Render<NutritionSummary>(p => p.Add(x => x.Facts, Known));

        cut.FindAll(".nutrition__missing").ShouldBeEmpty();
    }

    [Fact]
    public void The_section_is_named_for_a_screen_reader_by_serving_unless_told_otherwise()
    {
        Render<NutritionSummary>(p => p.Add(x => x.Facts, Known)).Find("section").GetAttribute("aria-label").ShouldBe("Dinh dưỡng một suất");
        Render<NutritionSummary>(p => p.Add(x => x.Facts, Known).Add(x => x.Heading, "Dinh dưỡng trên 100 g")).Find("section").GetAttribute("aria-label").ShouldBe("Dinh dưỡng trên 100 g");
    }

    [Fact]
    public void Always_says_the_numbers_are_estimates()
    {
        var cut = Render<NutritionSummary>(p => p.Add(x => x.Facts, Known));

        cut.Find(".nutrition__note").TextContent.ShouldBe("Các con số chỉ là ước tính.");
    }
}
