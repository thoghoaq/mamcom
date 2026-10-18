using System.Text.Json;
using System.Text.Json.Nodes;
using MamCom.Core.Json;
using MamCom.Core.Models;

namespace MamCom.Core.Tests.Json;

public class CoreJsonTests
{
    private static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, CoreJson.Options)!;

    private static string Write<T>(T value) => JsonSerializer.Serialize(value, CoreJson.Options);

    private static void ShouldRoundTrip<T>(string json)
    {
        var written = Write(Read<T>(json));

        JsonNode.DeepEquals(JsonNode.Parse(written), JsonNode.Parse(json)).ShouldBeTrue(
            $"Round trip changed the document.\nExpected:\n{json}\nActual:\n{written}");
    }

    // ---- SPEC §7.1 / §7.2 samples ------------------------------------------------------------

    [Fact]
    public void Ingredient_sample_from_SPEC_deserializes_into_the_model()
    {
        var ingredient = Read<Ingredient>(SpecSamples.Ingredient);

        ingredient.Id.ShouldBe("thit-ba-chi");
        ingredient.Names["south"].ShouldBe("Thịt ba rọi");
        ingredient.Aliases.ShouldBe(["ba rọi", "thịt ba rọi"]);
        ingredient.Category.ShouldBe("thit-heo");
        ingredient.Aisle.ShouldBe(Aisle.MeatAndFish);
        ingredient.ProteinSource.ShouldBe("heo");
        ingredient.MarketUnit["north"].ShouldBe(new MarketUnit("lạng", 100, 0.5));
        ingredient.MarketUnit["south"].ShouldBe(new MarketUnit("g", 1, 50));
        ingredient.WasteRatio.ShouldBe(0);
        ingredient.IsPantry.ShouldBeFalse();
        ingredient.NutritionPer100gEdible.ShouldBe(new NutritionFacts(null, null, null, null, null, null));
        ingredient.NutritionSource.ShouldBe(new NutritionSource(NutritionDbs.Placeholder, null, null, null, null));
        ingredient.Image.ShouldBe(new ImageRef(null, null, null, null, false));
    }

    [Fact]
    public void Ingredient_sample_from_SPEC_round_trips()
    {
        ShouldRoundTrip<Ingredient>(SpecSamples.Ingredient);
    }

    [Fact]
    public void Dish_sample_from_SPEC_deserializes_into_the_model()
    {
        var dish = Read<Dish>(SpecSamples.Dish);

        dish.Id.ShouldBe("canh-chua-ca-loc");
        dish.Name.ShouldBe("Canh chua cá lóc");
        dish.Slot.ShouldBe(DishSlot.Soup);
        dish.Regions.ShouldBe([Region.South]);
        dish.Seasons.ShouldBe([SeasonIds.All]);
        dish.Spicy.ShouldBe(SpicyLevel.Optional);
        dish.CookingMethod.ShouldBe(CookingMethod.Simmer);
        dish.ProteinSource.ShouldBe("ca");
        dish.ServeHot.ShouldBeFalse();
        dish.Ingredients.Count.ShouldBe(1);
        dish.Ingredients[0].IngredientId.ShouldBe("ca-loc");
        dish.Ingredients[0].GramsEdiblePerServing.ShouldBe(80);
        dish.Ingredients[0].Scale.ShouldBe(ScaleKind.Linear);
        dish.Ingredients[0].ConsumedRatio.ShouldBe(1);
        dish.Ingredients[0].Note.ShouldBe("cắt khúc");
        dish.Steps.Count.ShouldBe(1);
        dish.Steps[0].Order.ShouldBe(1);
        dish.Steps[0].ActiveMinutes.ShouldBe(10);
        dish.Steps[0].PassiveMinutes.ShouldBe(0);
        dish.Steps[0].IngredientIds.ShouldBe(["ca-loc"]);
        dish.Status.ShouldBe(DishStatus.Draft);
        dish.Provenance.DraftedBy.ShouldBe(DraftAuthor.Llm);
        dish.Provenance.ReviewedBy.ShouldBeNull();
        dish.Image.IsIllustration.ShouldBeFalse();
    }

    [Fact]
    public void Dish_sample_from_SPEC_round_trips()
    {
        ShouldRoundTrip<Dish>(SpecSamples.Dish);
    }

    [Fact]
    public void Numbers_and_dates_survive_a_round_trip()
    {
        // test-only values: they exercise number and date handling and are not nutrition data.
        const string json = """
            {
              "id": "test-ingredient",
              "names": { "default": "Test" },
              "aliases": [],
              "category": "test",
              "aisle": "khac",
              "proteinSource": null,
              "marketUnit": { "north": { "display": "bó", "gramsPerUnit": 150.5, "step": 1 } },
              "wasteRatio": 0.25,
              "isPantry": true,
              "nutritionPer100gEdible": { "kcal": 123.5, "proteinG": 1.25, "fatG": 0.5, "carbG": 2, "fiberG": 0.75, "sodiumMg": 10 },
              "nutritionSource": { "db": "test-db", "ref": "42", "matchedName": "Test food", "verifiedBy": "tester", "verifiedAt": "2026-10-18" },
              "image": { "thumb": "t.webp", "full": "f.webp", "source": "own photo", "license": "own", "isIllustration": true }
            }
            """;

        var ingredient = Read<Ingredient>(json);

        ingredient.NutritionPer100gEdible.Kcal.ShouldBe(123.5);
        ingredient.NutritionSource.VerifiedAt.ShouldBe(new DateOnly(2026, 10, 18));
        ingredient.MarketUnit["north"].GramsPerUnit.ShouldBe(150.5);
        ShouldRoundTrip<Ingredient>(json);
    }

    // ---- other persisted and exchanged types -------------------------------------------------

    [Fact]
    public void Household_with_members_round_trips()
    {
        const string json = """
            {
              "region": "south",
              "bowlSize": "medium",
              "members": [
                { "displayName": "Bố", "ageGroup": "adult", "bowlsPerMeal": 3, "tags": ["moreProtein"], "avoid": [] },
                { "displayName": "Bà", "ageGroup": "senior", "bowlsPerMeal": 1.5, "tags": ["noSpicy", "moreVeg"], "avoid": ["bo", "hai-san"] }
              ],
              "availableMinutes": 45
            }
            """;

        var household = Read<Household>(json);

        household.Region.ShouldBe(Region.South);
        household.BowlSize.ShouldBe(BowlSize.Medium);
        household.AvailableMinutes.ShouldBe(45);
        household.Members[1].Tags.ShouldBe([MemberTag.NoSpicy, MemberTag.MoreVeg]);
        household.Members[1].Avoid.ShouldBe(["bo", "hai-san"]);
        ShouldRoundTrip<Household>(json);
    }

    [Fact]
    public void Spin_result_round_trips()
    {
        const string json = """
            {
              "tray": {
                "date": "2026-10-18",
                "slots": [
                  { "slot": "canh", "dishId": "canh-chua-ca-loc", "isLocked": true },
                  { "slot": "man", "dishId": "thit-kho-trung", "isLocked": false },
                  { "slot": "rau", "dishId": "rau-muong-xao-toi", "isLocked": false }
                ]
              },
              "relaxedRules": ["availableMinutes", "distinctProteinSources"]
            }
            """;

        var result = Read<SpinResult>(json);

        result.Tray.Date.ShouldBe(new DateOnly(2026, 10, 18));
        result.Tray.Slots[0].ShouldBe(new SlotResult(DishSlot.Soup, "canh-chua-ca-loc", true));
        result.RelaxedRules.ShouldBe([RelaxedRule.AvailableMinutes, RelaxedRule.DistinctProteinSources]);
        ShouldRoundTrip<SpinResult>(json);
    }

    [Fact]
    public void Shopping_item_round_trips()
    {
        const string json = """
            {
              "ingredientId": "thit-ba-chi",
              "name": "Thịt ba rọi",
              "aisle": "thit-ca",
              "quantity": 3,
              "unitDisplay": "lạng",
              "purchaseGrams": 275.5,
              "isPantry": false
            }
            """;

        Read<ShoppingItem>(json).Aisle.ShouldBe(Aisle.MeatAndFish);
        ShouldRoundTrip<ShoppingItem>(json);
    }

    [Fact]
    public void Cook_plan_round_trips_and_timer_is_optional()
    {
        const string json = """
            {
              "steps": [
                { "startMinute": 0, "dishId": "com", "text": "Vo gạo, bắc nồi cơm.", "activeMinutes": 5, "timerMinutes": 30 },
                { "startMinute": 5, "dishId": "rau-muong-xao-toi", "text": "Xào rau.", "activeMinutes": 4, "timerMinutes": null }
              ],
              "mealTimeMinutes": 35
            }
            """;

        var plan = Read<CookPlan>(json);

        plan.Steps[0].DishId.ShouldBe(CookPlan.RiceDishId);
        plan.Steps[0].TimerMinutes.ShouldBe(30);
        plan.Steps[1].TimerMinutes.ShouldBeNull();
        ShouldRoundTrip<CookPlan>(json);
    }

    // ---- defaults for optional members -------------------------------------------------------

    [Fact]
    public void Dish_ingredient_defaults_consumed_ratio_to_one_and_note_to_null()
    {
        var ingredient = Read<DishIngredient>("""{ "ingredientId": "ca-loc", "gramsEdiblePerServing": 80, "scale": "fixed" }""");

        ingredient.ConsumedRatio.ShouldBe(1);
        ingredient.Note.ShouldBeNull();
    }

    [Fact]
    public void Household_defaults_available_minutes_to_null()
    {
        var household = Read<Household>("""{ "region": "north", "bowlSize": "large", "members": [] }""");

        household.AvailableMinutes.ShouldBeNull();
    }

    // ---- strictness --------------------------------------------------------------------------

    [Fact]
    public void A_missing_mandatory_property_fails()
    {
        var withoutId = JsonNode.Parse(SpecSamples.Dish)!.AsObject();
        withoutId.Remove("id");

        Should.Throw<JsonException>(() => Read<Dish>(withoutId.ToJsonString()));
    }

    [Fact]
    public void Null_for_a_non_nullable_property_fails()
    {
        var nullName = JsonNode.Parse(SpecSamples.Dish)!.AsObject();
        nullName["name"] = null;

        Should.Throw<JsonException>(() => Read<Dish>(nullName.ToJsonString()));
    }

    [Fact]
    public void An_unknown_enum_value_fails()
    {
        var badSlot = SpecSamples.Dish.Replace("\"slot\": \"canh\"", "\"slot\": \"trang-mieng\"");

        Should.Throw<JsonException>(() => Read<Dish>(badSlot));
    }

    [Fact]
    public void A_member_without_tags_or_avoid_fails_rather_than_getting_null_lists()
    {
        Should.Throw<JsonException>(() =>
            Read<Member>("""{ "displayName": "Bé", "ageGroup": "toddler", "bowlsPerMeal": 1 }"""));
    }

    [Fact]
    public void A_numeric_enum_value_fails()
    {
        var numericSlot = SpecSamples.Dish.Replace("\"slot\": \"canh\"", "\"slot\": 0");

        Should.Throw<JsonException>(() => Read<Dish>(numericSlot));
    }

    [Fact]
    public void A_duplicate_property_fails()
    {
        var duplicated = SpecSamples.Dish.Replace("\"name\": \"Canh chua cá lóc\",", "\"name\": \"Canh chua cá lóc\", \"name\": \"Khác\",");

        Should.Throw<JsonException>(() => Read<Dish>(duplicated));
    }

    [Fact]
    public void Unknown_properties_are_ignored_because_the_JSON_Schema_owns_that_check()
    {
        var withExtra = SpecSamples.Dish.Replace("\"id\": \"canh-chua-ca-loc\",", "\"id\": \"canh-chua-ca-loc\", \"surprise\": 1,");

        Read<Dish>(withExtra).Id.ShouldBe("canh-chua-ca-loc");
    }

    // ---- output format -----------------------------------------------------------------------

    [Fact]
    public void Output_is_indented_with_two_spaces_uses_LF_and_writes_nulls()
    {
        var member = new Member("Bà", AgeGroup.Senior, 1.5, [], []);

        Write(member).ShouldBe(
            "{\n" +
            "  \"displayName\": \"Bà\",\n" +
            "  \"ageGroup\": \"senior\",\n" +
            "  \"bowlsPerMeal\": 1.5,\n" +
            "  \"tags\": [],\n" +
            "  \"avoid\": []\n" +
            "}");

        Write(new ImageRef(null, null, null, null, false)).ShouldContain("\"thumb\": null");
    }

    [Fact]
    public void Output_keeps_Vietnamese_text_readable_instead_of_escaping_it()
    {
        var json = Write(new Member("Ông nội", AgeGroup.Senior, 2, [], []));

        json.ShouldContain("Ông nội");
        json.ShouldNotContain("\\u");
    }

    [Fact]
    public void Properties_are_written_in_the_order_the_SPEC_lists_them()
    {
        var dish = Read<Dish>(SpecSamples.Dish);

        var names = JsonNode.Parse(Write(dish))!.AsObject().Select(p => p.Key).ToArray();

        names.ShouldBe(
        [
            "id", "name", "slot", "regions", "seasons", "spicy", "cookingMethod", "proteinSource",
            "serveHot", "ingredients", "steps", "status", "provenance", "image",
        ]);
    }
}
