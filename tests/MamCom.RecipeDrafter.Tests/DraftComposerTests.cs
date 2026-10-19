using System.Text.Json;
using System.Text.Json.Nodes;
using MamCom.Core.Models;
using MamCom.RecipeDrafter.Drafting;

namespace MamCom.RecipeDrafter.Tests;

public class DraftComposerTests
{
    private static Dish Compose(Action<JsonObject>? change = null, Plan.DishRequest? request = null) =>
        DraftComposer.Compose(request ?? Answers.FishMain, LlmRecipe.Parse(Answers.Recipe(change).ToJsonString()));

    // ---- what comes from the owner and what from the model ------------------------------------------------------------

    [Fact]
    public void The_name_slot_regions_and_id_are_the_owners_not_the_models()
    {
        var dish = Compose(request: new Plan.DishRequest("canh-bau", "Canh bầu nấu tôm", DishSlot.Soup, [Region.South, Region.Central]));

        dish.Id.ShouldBe("canh-bau");
        dish.Name.ShouldBe("Canh bầu nấu tôm");
        dish.Slot.ShouldBe(DishSlot.Soup);
        dish.Regions.ShouldBe([Region.South, Region.Central]);
    }

    [Fact]
    public void What_the_model_decides_is_taken_over_as_it_is()
    {
        var dish = Compose(r =>
        {
            r["seasons"] = new JsonArray("summer", "autumn");
            r["spicy"] = "required";
            r["cookingMethod"] = "xao";
            r["proteinSource"] = "heo";
            r["serveHot"] = true;
        });

        dish.Seasons.ShouldBe(["summer", "autumn"]);
        dish.Spicy.ShouldBe(SpicyLevel.Required);
        dish.CookingMethod.ShouldBe(CookingMethod.StirFry);
        dish.ProteinSource.ShouldBe("heo");
        dish.ServeHot.ShouldBeTrue();
    }

    [Fact]
    public void A_draft_is_always_a_draft_by_an_llm_with_no_review_no_cook_test_and_no_image()
    {
        var dish = Compose();

        dish.Status.ShouldBe(DishStatus.Draft);
        dish.Provenance.ShouldBe(new Provenance(DraftAuthor.Llm, null, null, null, null));
        dish.Image.ShouldBe(new ImageRef(null, null, null, null, false));
    }

    [Fact]
    public void The_model_has_no_way_to_set_the_status_or_anything_else_that_is_not_its_to_decide()
    {
        foreach (var member in new[] { "status", "provenance", "image", "id", "name", "slot", "regions", "kcal" })
        {
            var json = Answers.Recipe(r => r[member] = "x").ToJsonString();

            Should.Throw<JsonException>(() => LlmRecipe.Parse(json), member);
        }
    }

    [Fact]
    public void A_member_written_twice_in_the_answer_is_an_error_not_a_silent_last_one_wins()
    {
        var json = Answers.Recipe().ToJsonString().Replace("\"spicy\":\"optional\"", "\"spicy\":\"optional\",\"spicy\":\"required\"");

        json.ShouldContain("\"spicy\":\"required\"");
        Should.Throw<JsonException>(() => LlmRecipe.Parse(json));
    }

    // ---- lines and steps -----------------------------------------------------------------------------------------------------

    [Fact]
    public void Lines_keep_their_grams_scale_and_note_and_a_blank_note_becomes_null()
    {
        var dish = Compose();

        dish.Ingredients.Count.ShouldBe(7);
        dish.Ingredients[0].ShouldBe(new DishIngredient("ca-loc", 120, ScaleKind.Linear, 1, "cắt khúc"));
        dish.Ingredients[1].ShouldBe(new DishIngredient("nuoc-mam", 8, ScaleKind.Sublinear, 1, null));
        dish.Ingredients[4].GramsEdiblePerServing.ShouldBe(0.5);
    }

    [Fact]
    public void A_note_of_only_spaces_is_no_note_and_other_notes_are_trimmed()
    {
        var dish = Compose(r =>
        {
            r["ingredients"]![1]!["note"] = "   ";
            r["ingredients"]![0]!["note"] = "  cắt khúc  ";
        });

        dish.Ingredients[1].Note.ShouldBeNull();
        dish.Ingredients[0].Note.ShouldBe("cắt khúc");
    }

    [Fact]
    public void Steps_are_numbered_from_one_in_the_order_the_model_wrote_them()
    {
        var dish = Compose();

        dish.Steps.Select(s => s.Order).ShouldBe([1, 2, 3]);
        dish.Steps[0].Text.ShouldBe("Cắt cá thành khúc, ướp với nước mắm, hành tím và tiêu.");
        dish.Steps[0].ActiveMinutes.ShouldBe(8);
        dish.Steps[0].PassiveMinutes.ShouldBe(15);
        dish.Steps[0].IngredientIds.ShouldBe(["ca-loc", "nuoc-mam", "hanh-tim", "hat-tieu"]);
        dish.Steps[2].PassiveMinutes.ShouldBe(35);
    }

    [Fact]
    public void Missing_ingredients_are_not_part_of_the_dish()
    {
        var dish = Compose(r => r["missingIngredients"] = new JsonArray(Answers.Missing("Cải ngọt", 80)));

        dish.Ingredients.Select(l => l.IngredientId).ShouldNotContain("cai-ngot");
        dish.Ingredients.Count.ShouldBe(7);
    }

    // ---- values the dish model refuses ----------------------------------------------------------------------------------------

    [Theory]
    [InlineData("spicy", "hot")]
    [InlineData("cookingMethod", "fry")]
    public void A_value_the_dish_model_does_not_know_is_refused_with_a_format_error(string member, string value)
    {
        Should.Throw<FormatException>(() => Compose(r => r[member] = value));
    }

    [Fact]
    public void An_unknown_scale_is_refused()
    {
        Should.Throw<FormatException>(() => Compose(r => r["ingredients"]![0]!["scale"] = "exponential"));
    }

    // ---- the file --------------------------------------------------------------------------------------------------------------

    [Fact]
    public void The_file_text_is_canonical_indented_json_ending_with_one_line_feed()
    {
        var text = DraftComposer.Serialize(Compose());

        text.ShouldStartWith("{\n  \"id\": \"ca-loc-kho-to\"");
        text.ShouldEndWith("}\n");
        text.ShouldNotEndWith("\n\n");
        text.ShouldNotContain('\r');
        text.ShouldContain("Cá lóc kho tộ");   // not "Cá lóc kho tộ"
        text.ShouldContain("\"status\": \"draft\"");
        text.ShouldContain("\"draftedBy\": \"llm\"");
    }

    [Fact]
    public void The_file_reads_back_to_the_same_dish()
    {
        var dish = Compose();

        var again = JsonSerializer.Deserialize<Dish>(DraftComposer.Serialize(dish), MamCom.Core.Json.CoreJson.Options)!;

        DraftComposer.Serialize(again).ShouldBe(DraftComposer.Serialize(dish));
    }

    [Fact]
    public void The_file_satisfies_the_dish_schema()
    {
        using var data = new TempData();

        data.Schemas.Validate("dish", DraftComposer.Serialize(Compose())).ShouldBeEmpty();
    }
}
