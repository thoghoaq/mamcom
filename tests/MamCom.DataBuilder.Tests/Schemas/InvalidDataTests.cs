using System.Text.Json.Nodes;
using MamCom.DataBuilder.Schemas;

namespace MamCom.DataBuilder.Tests.Schemas;

/// <summary>
/// Each case takes a valid data file, breaks one thing, and expects the schema to name that thing: where it is and
/// which keyword failed. A schema that passes everything would pass the seed tests too, so these are the proof it bites.
/// </summary>
public class InvalidDataTests
{
    private sealed record Case(string Schema, string Folder, string File, Action<JsonObject> Break, string Location, string Keyword);

    private static readonly Dictionary<string, Case> Cases = BuildCases();

    public static TheoryData<string> CaseNames => new(Cases.Keys.Order(StringComparer.Ordinal));

    private static Dictionary<string, Case> BuildCases()
    {
        var cases = new Dictionary<string, Case>(StringComparer.Ordinal);

        void Ingredient(string name, string file, Action<JsonObject> change, string location, string keyword) =>
            cases[$"ingredient: {name}"] = new Case("ingredient", "ingredients", file, change, location, keyword);

        void Dish(string name, Action<JsonObject> change, string location, string keyword) =>
            cases[$"dish: {name}"] = new Case("dish", "dishes", "thit-kho-trung", change, location, keyword);

        void Config(string schema, string name, Action<JsonObject> change, string location, string keyword) =>
            cases[$"{schema}: {name}"] = new Case(schema, "config", schema, change, location, keyword);

        // ---- ingredient: shape and ids ----------------------------------------------------------------
        Ingredient("missing wasteRatio", "thit-ba-chi", o => o.Remove("wasteRatio"), "", "required");
        Ingredient("misspelt property", "thit-ba-chi", o => o["wasteRatoi"] = 0, "/wasteRatoi", "additionalProperties");
        Ingredient("uppercase id", "thit-ba-chi", o => o["id"] = "Thit-Ba-Chi", "/id", "pattern");
        Ingredient("id with a diacritic", "thit-ba-chi", o => o["id"] = "thịt-ba-chỉ", "/id", "pattern");
        Ingredient("id with a double hyphen", "thit-ba-chi", o => o["id"] = "thit--ba-chi", "/id", "pattern");
        Ingredient("unknown aisle", "thit-ba-chi", o => o["aisle"] = "meat", "/aisle", "enum");
        Ingredient("duplicate alias", "thit-ba-chi", o => o["aliases"] = new JsonArray("ba rọi", "ba rọi"), "/aliases", "uniqueItems");
        Ingredient("empty alias", "thit-ba-chi", o => o["aliases"] = new JsonArray(""), "/aliases/0", "minLength");

        // ---- ingredient: waste, protein, units ---------------------------------------------------------
        Ingredient("waste of one", "thit-ba-chi", o => o["wasteRatio"] = 1, "/wasteRatio", "exclusiveMaximum");
        Ingredient("negative waste", "thit-ba-chi", o => o["wasteRatio"] = -0.1, "/wasteRatio", "minimum");
        Ingredient("protein source none", "thit-ba-chi", o => o["proteinSource"] = "none", "/proteinSource", "anyOf");
        Ingredient("names without default or regions", "thit-ba-chi", o => o["names"] = new JsonObject { ["north"] = "Thịt ba chỉ" }, "/names", "anyOf");
        Ingredient("name for an unknown region", "thit-ba-chi", o => o["names"]!["east"] = "x", "/names/east", "additionalProperties");
        Ingredient("empty name", "thit-ba-chi", o => o["names"]!["default"] = "", "/names/default", "minLength");
        Ingredient("market unit for one region only", "thit-ba-chi", o => o["marketUnit"] = new JsonObject { ["north"] = o["marketUnit"]!["north"]!.DeepClone() }, "/marketUnit", "anyOf");
        Ingredient("market unit for an unknown region", "thit-ba-chi", o => o["marketUnit"]!["east"] = o["marketUnit"]!["south"]!.DeepClone(), "/marketUnit/east", "additionalProperties");
        Ingredient("zero grams per unit", "thit-ba-chi", o => o["marketUnit"]!["south"]!["gramsPerUnit"] = 0, "/marketUnit/south/gramsPerUnit", "exclusiveMinimum");
        Ingredient("zero step", "thit-ba-chi", o => o["marketUnit"]!["south"]!["step"] = 0, "/marketUnit/south/step", "exclusiveMinimum");
        Ingredient("market unit without a label", "thit-ba-chi", o => o["marketUnit"]!["south"]!.AsObject().Remove("display"), "/marketUnit/south", "required");
        Ingredient("approx grams that is not a boolean", "ca-loc", o => o["marketUnit"]!["default"]!["showApproxGrams"] = "yes", "/marketUnit/default/showApproxGrams", "type");

        // ---- ingredient: nutrition never invented -------------------------------------------------------
        Ingredient("a number under the placeholder source", "ca-loc", o => o["nutritionPer100gEdible"]!["kcal"] = 143, "/nutritionPer100gEdible/kcal", "type");
        Ingredient("a missing nutrient", "ca-loc", o => o["nutritionPer100gEdible"]!.AsObject().Remove("fiberG"), "/nutritionPer100gEdible", "required");
        Ingredient("negative nutrient from a real source", "ca-loc", o => RealSource(o, o2 => o2["nutritionPer100gEdible"]!["fatG"] = -1), "/nutritionPer100gEdible/fatG", "minimum");
        Ingredient("more than 100g of fat in 100g", "ca-loc", o => RealSource(o, o2 => o2["nutritionPer100gEdible"]!["fatG"] = 101), "/nutritionPer100gEdible/fatG", "maximum");
        Ingredient("more energy than pure fat", "ca-loc", o => RealSource(o, o2 => o2["nutritionPer100gEdible"]!["kcal"] = 1000), "/nutritionPer100gEdible/kcal", "maximum");
        Ingredient("unknown nutrition database", "ca-loc", o => o["nutritionSource"]!["db"] = "my-own-table", "/nutritionSource/db", "enum");
        Ingredient("placeholder with a reference", "ca-loc", o => o["nutritionSource"]!["ref"] = "170567", "/nutritionSource/ref", "type");
        Ingredient("real source without a reference", "ca-loc", o => o["nutritionSource"]!["db"] = "usda-fdc", "/nutritionSource/ref", "type");
        Ingredient("verifier without a date", "ca-loc", o => RealSource(o, o2 => o2["nutritionSource"]!["verifiedBy"] = "Mẹ"), "/nutritionSource/verifiedAt", "type");
        Ingredient("date without a verifier", "ca-loc", o => RealSource(o, o2 => o2["nutritionSource"]!["verifiedAt"] = "2026-10-19"), "/nutritionSource/verifiedAt", "type");
        Ingredient("a date that does not exist", "ca-loc", o => RealSource(o, o2 =>
        {
            o2["nutritionSource"]!["verifiedBy"] = "Mẹ";
            o2["nutritionSource"]!["verifiedAt"] = "2026-13-45";
        }), "/nutritionSource/verifiedAt", "format");

        // ---- ingredient: images ------------------------------------------------------------------------
        Ingredient("image without source and license", "ca-loc", o => o["image"]!["thumb"] = "ca-loc-200.webp", "/image/source", "type");
        Ingredient("image without a license", "ca-loc", o =>
        {
            o["image"]!["full"] = "ca-loc-800.webp";
            o["image"]!["source"] = "tự chụp";
        }, "/image/license", "type");
        Ingredient("image with an extra property", "ca-loc", o => o["image"]!["alt"] = "cá", "/image/alt", "additionalProperties");

        // ---- dish: shape, ids, lists --------------------------------------------------------------------
        Dish("unknown slot", o => o["slot"] = "main", "/slot", "enum");
        Dish("reserved id com", o => o["id"] = "com", "/id", "not");
        Dish("empty name", o => o["name"] = "", "/name", "minLength");
        Dish("unknown region", o => o["regions"] = new JsonArray("east"), "/regions/0", "enum");
        Dish("duplicate region", o => o["regions"] = new JsonArray("south", "south"), "/regions", "uniqueItems");
        Dish("no season", o => o["seasons"] = new JsonArray(), "/seasons", "minItems");
        Dish("all with another season", o => o["seasons"] = new JsonArray("all", "summer"), "/seasons", "maxItems");
        Dish("season id that is not kebab-case", o => o["seasons"] = new JsonArray("Summer"), "/seasons/0", "pattern");
        Dish("unknown spicy level", o => o["spicy"] = "hot", "/spicy", "enum");
        Dish("unknown cooking method", o => o["cookingMethod"] = "fry", "/cookingMethod", "enum");
        Dish("protein source with a space", o => o["proteinSource"] = "thit heo", "/proteinSource", "pattern");
        Dish("serveHot as text", o => o["serveHot"] = "yes", "/serveHot", "type");
        Dish("no ingredients", o => o["ingredients"] = new JsonArray(), "/ingredients", "minItems");
        Dish("no steps", o => o["steps"] = new JsonArray(), "/steps", "minItems");

        // ---- dish: ingredient lines --------------------------------------------------------------------
        Dish("zero grams", o => Line(o, 0)["gramsEdiblePerServing"] = 0, "/ingredients/0/gramsEdiblePerServing", "exclusiveMinimum");
        Dish("a kilo per serving", o => Line(o, 0)["gramsEdiblePerServing"] = 1500, "/ingredients/0/gramsEdiblePerServing", "maximum");
        Dish("unknown scale", o => Line(o, 0)["scale"] = "exponential", "/ingredients/0/scale", "enum");
        Dish("consumed more than served", o => Line(o, 0)["consumedRatio"] = 1.5, "/ingredients/0/consumedRatio", "maximum");
        Dish("consumed ratio below one without a note", o =>
        {
            Line(o, 0)["consumedRatio"] = 0.5;
            Line(o, 0)["note"] = null;
        }, "/ingredients/0/note", "type");
        Dish("consumed ratio below one with the note left out", o =>
        {
            Line(o, 0)["consumedRatio"] = 0.5;
            Line(o, 0).Remove("note");
        }, "/ingredients/0", "required");
        Dish("line without an ingredient id", o => Line(o, 0).Remove("ingredientId"), "/ingredients/0", "required");
        Dish("line with an extra property", o => Line(o, 0)["optional"] = true, "/ingredients/0/optional", "additionalProperties");

        // ---- dish: steps -------------------------------------------------------------------------------
        Dish("step with no time at all", o =>
        {
            Step(o, 0)["activeMinutes"] = 0;
            Step(o, 0)["passiveMinutes"] = 0;
        }, "/steps/0", "anyOf");
        Dish("negative hands-on minutes", o => Step(o, 0)["activeMinutes"] = -1, "/steps/0/activeMinutes", "minimum");
        Dish("fractional minutes", o => Step(o, 0)["passiveMinutes"] = 2.5, "/steps/0/passiveMinutes", "type");
        Dish("a day and a half of hands-on time", o => Step(o, 0)["activeMinutes"] = 2000, "/steps/0/activeMinutes", "maximum");
        Dish("step order zero", o => Step(o, 0)["order"] = 0, "/steps/0/order", "minimum");
        Dish("empty step text", o => Step(o, 0)["text"] = "", "/steps/0/text", "minLength");
        Dish("step ingredient with a bad id", o => Step(o, 0)["ingredientIds"] = new JsonArray("Trứng"), "/steps/0/ingredientIds/0", "pattern");
        Dish("step ingredient listed twice", o => Step(o, 0)["ingredientIds"] = new JsonArray("trung-ga", "trung-ga"), "/steps/0/ingredientIds", "uniqueItems");

        // ---- dish: lifecycle ---------------------------------------------------------------------------
        Dish("unknown status", o => o["status"] = "published", "/status", "enum");
        Dish("draft already reviewed by someone", o => o["provenance"]!["reviewedBy"] = "Mẹ", "/provenance/reviewedBy", "type");
        Dish("draft already cook-tested", o => o["provenance"]!["cookTestedAt"] = "2026-10-19", "/provenance/cookTestedAt", "type");
        Dish("validated dish with a measured time", o =>
        {
            o["status"] = "validated";
            o["provenance"]!["measuredTotalMinutes"] = 60;
        }, "/provenance/measuredTotalMinutes", "type");
        Dish("reviewed without a reviewer", o => o["status"] = "reviewed", "/provenance/reviewedBy", "type");
        Dish("reviewed but already cook-tested", o =>
        {
            Reviewed(o);
            o["provenance"]!["cookTestedAt"] = "2026-10-20";
        }, "/provenance/cookTestedAt", "type");
        Dish("cook-tested without a cook-test date", o =>
        {
            Reviewed(o);
            o["status"] = "cooktested";
        }, "/provenance/cookTestedAt", "type");
        Dish("unknown drafter", o => o["provenance"]!["draftedBy"] = "robot", "/provenance/draftedBy", "enum");
        Dish("measured time of zero", o => o["provenance"]!["measuredTotalMinutes"] = 0, "/provenance/measuredTotalMinutes", "minimum");
        Dish("missing provenance", o => o.Remove("provenance"), "", "required");
        Dish("image without a license", o =>
        {
            o["image"]!["thumb"] = "x.webp";
            o["image"]!["source"] = "tự chụp";
        }, "/image/license", "type");

        // ---- config files -------------------------------------------------------------------------------
        Config("portion-defaults", "sublinear exponent above one", o => o["sublinearExponent"] = 1.5, "/sublinearExponent", "maximum");
        Config("portion-defaults", "sublinear exponent of zero", o => o["sublinearExponent"] = 0, "/sublinearExponent", "exclusiveMinimum");
        Config("portion-defaults", "missing age group", o => o["ageModifiers"]!.AsObject().Remove("senior"), "/ageModifiers", "required");
        Config("portion-defaults", "unknown property", o => o["servingsBonus"] = 1, "/servingsBonus", "additionalProperties");
        Config("portion-defaults", "rice id in capitals", o => o["riceIngredientId"] = "Gao", "/riceIngredientId", "pattern");
        Config("portion-defaults", "negative bowl grams", o => o["gramsRawRicePerBowl"]!["small"] = -35, "/gramsRawRicePerBowl/small", "exclusiveMinimum");
        Config("portion-defaults", "more-protein factor below one", o => o["moreProteinFactor"] = 0.5, "/moreProteinFactor", "minimum");
        Config("spinner-defaults", "eleven months", o => o["seasonByMonth"]!.AsArray().RemoveAt(0), "/seasonByMonth", "minItems");
        Config("spinner-defaults", "all as a month season", o => o["seasonByMonth"]![0] = "all", "/seasonByMonth/0", "not");
        Config("spinner-defaults", "zero attempts", o => o["attemptsPerLevel"] = 0, "/attemptsPerLevel", "minimum");
        Config("spinner-defaults", "zero weight", o => o["seasonMatchWeight"] = 0, "/seasonMatchWeight", "exclusiveMinimum");
        Config("spinner-defaults", "a weight of a hundred", o => o["regionMatchWeight"] = 100, "/regionMatchWeight", "maximum");
        Config("spinner-defaults", "fractional history size", o => o["historySize"] = 2.5, "/historySize", "type");
        Config("spinner-defaults", "missing recency weight", o => o["recency"]!.AsObject().Remove("reducedWeight"), "/recency", "required");
        Config("nutrition-defaults", "zero energy per gram", o => o["kcalPerGram"]!["fat"] = 0, "/kcalPerGram/fat", "exclusiveMinimum");
        Config("nutrition-defaults", "share above one", o => o["recommendedEnergyShare"]!["carb"]!["max"] = 1.5, "/recommendedEnergyShare/carb/max", "maximum");
        Config("nutrition-defaults", "missing macro", o => o["kcalPerGram"]!.AsObject().Remove("carb"), "/kcalPerGram", "required");
        Config("cooking-defaults", "empty rice text", o => o["riceStepText"] = "", "/riceStepText", "minLength");
        Config("cooking-defaults", "negative rice minutes", o => o["riceActiveMinutes"] = -1, "/riceActiveMinutes", "minimum");
        Config("cooking-defaults", "fractional rice minutes", o => o["ricePassiveMinutes"] = 30.5, "/ricePassiveMinutes", "type");

        return cases;
    }

    private static JsonObject Line(JsonObject dish, int index) => dish["ingredients"]![index]!.AsObject();

    private static JsonObject Step(JsonObject dish, int index) => dish["steps"]![index]!.AsObject();

    /// <summary>Makes the ingredient look like it has a real source, so numbers and verification are allowed.</summary>
    private static void RealSource(JsonObject ingredient, Action<JsonObject> then)
    {
        ingredient["nutritionSource"]!["db"] = "usda-fdc";
        ingredient["nutritionSource"]!["ref"] = "0000000";
        ingredient["nutritionSource"]!["matchedName"] = "test-only entry";
        then(ingredient);
    }

    private static void Reviewed(JsonObject dish)
    {
        dish["status"] = "reviewed";
        dish["provenance"]!["reviewedBy"] = "Mẹ";
        dish["provenance"]!["reviewedAt"] = "2026-10-19";
    }

    private static string Broken(Case c)
    {
        var root = JsonNode.Parse(DataFiles.Text(c.Folder, c.File))!.AsObject();
        c.Break(root);
        return root.ToJsonString();
    }

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void The_schema_names_what_is_wrong(string caseName)
    {
        var c = Cases[caseName];

        var issues = DataFiles.Schemas.Validate(c.Schema, Broken(c));

        issues.ShouldContain(
            i => i.Location == c.Location && i.Keyword == c.Keyword,
            $"expected [{c.Keyword}] at '{c.Location}', got:{Environment.NewLine}"
            + string.Join(Environment.NewLine, issues.Select(i => $"  '{i.Location}' [{i.Keyword}] {i.Message}")));
    }

    [Theory]
    [InlineData("ingredient: misspelt property")]
    [InlineData("ingredient: uppercase id")]
    [InlineData("ingredient: waste of one")]
    [InlineData("ingredient: unknown aisle")]
    [InlineData("ingredient: protein source none")]
    [InlineData("ingredient: market unit for an unknown region")]
    [InlineData("ingredient: a number under the placeholder source")]
    [InlineData("dish: unknown slot")]
    [InlineData("dish: reserved id com")]
    [InlineData("dish: step with no time at all")]
    [InlineData("dish: all with another season")]
    [InlineData("portion-defaults: sublinear exponent above one")]
    [InlineData("spinner-defaults: eleven months")]
    public void One_mistake_is_one_issue_not_a_list_of_the_conditions_around_it(string caseName)
    {
        // The validator lists every node that failed, also inside `if`, `not` and the branches of `anyOf`
        // that went the right way; only the failure that made the file invalid may reach the report.
        var c = Cases[caseName];

        var issues = DataFiles.Schemas.Validate(c.Schema, Broken(c));

        issues.Select(i => (i.Location, i.Keyword)).ShouldBe([(c.Location, c.Keyword)]);
    }

    [Fact]
    public void A_failed_anyOf_carries_what_each_branch_said_so_the_message_can_be_acted_on()
    {
        var root = JsonNode.Parse(DataFiles.Text("ingredients", "thit-ba-chi"))!.AsObject();
        root["names"] = new JsonObject { ["north"] = "Thịt ba chỉ" };

        var issue = DataFiles.Schemas.Validate("ingredient", root.ToJsonString()).ShouldHaveSingleItem();

        issue.Keyword.ShouldBe("anyOf");
        issue.Message.ShouldContain("'default'");
        issue.Message.ShouldContain("'central'");
        issue.Message.ShouldContain("'south'");
    }

    // ---- the valid side of the conditional rules: they must not reject what they are meant to allow ----

    [Fact]
    public void An_ingredient_with_a_real_verified_source_and_a_declared_image_is_valid()
    {
        var root = JsonNode.Parse(DataFiles.Text("ingredients", "ca-loc"))!.AsObject();
        RealSource(root, o =>
        {
            o["nutritionPer100gEdible"] = new JsonObject
            {
                ["kcal"] = 97, ["proteinG"] = 18.2, ["fatG"] = 2.7, ["carbG"] = 0, ["fiberG"] = null, ["sodiumMg"] = 51,
            };
            o["nutritionSource"]!["verifiedBy"] = "Mẹ";
            o["nutritionSource"]!["verifiedAt"] = "2026-10-19";
            o["image"] = new JsonObject
            {
                ["thumb"] = "ca-loc-200.webp", ["full"] = "ca-loc-800.webp", ["source"] = "tự chụp", ["license"] = "CC0", ["isIllustration"] = false,
            };
        });

        DataFiles.Schemas.Validate("ingredient", root.ToJsonString()).ShouldBeEmpty();
    }

    [Fact]
    public void A_reviewed_dish_and_a_cook_tested_dish_with_their_marks_are_valid()
    {
        var reviewed = JsonNode.Parse(DataFiles.Text("dishes", "thit-kho-trung"))!.AsObject();
        Reviewed(reviewed);
        DataFiles.Schemas.Validate("dish", reviewed.ToJsonString()).ShouldBeEmpty();

        reviewed["status"] = "cooktested";
        reviewed["provenance"]!["cookTestedAt"] = "2026-10-20";
        reviewed["provenance"]!["measuredTotalMinutes"] = 95;
        DataFiles.Schemas.Validate("dish", reviewed.ToJsonString()).ShouldBeEmpty();
    }

    [Fact]
    public void A_consumed_ratio_below_one_with_a_note_is_valid()
    {
        var dish = JsonNode.Parse(DataFiles.Text("dishes", "ca-thu-chien"))!.AsObject();
        var oil = dish["ingredients"]!.AsArray().Single(l => l!["ingredientId"]!.GetValue<string>() == "dau-an")!.AsObject();
        oil["consumedRatio"] = 0.4;
        oil["note"] = "test-only: share of frying oil eaten";

        DataFiles.Schemas.Validate("dish", dish.ToJsonString()).ShouldBeEmpty();
    }

    [Fact]
    public void An_ingredient_with_names_and_units_for_all_three_regions_and_no_default_is_valid()
    {
        var root = JsonNode.Parse(DataFiles.Text("ingredients", "thit-ba-chi"))!.AsObject();
        root["names"] = new JsonObject { ["north"] = "Thịt ba chỉ", ["central"] = "Thịt ba chỉ", ["south"] = "Thịt ba rọi" };
        var units = root["marketUnit"]!.AsObject();
        units["central"] = units["default"]!.DeepClone();
        units.Remove("default");

        DataFiles.Schemas.Validate("ingredient", root.ToJsonString()).ShouldBeEmpty();
    }

    // ---- text that is not a clean JSON object ------------------------------------------------------------------

    [Fact]
    public void Text_that_is_not_json_is_one_issue_not_an_exception()
    {
        var issues = DataFiles.Schemas.Validate("dish", "{ \"id\": ");

        var issue = issues.ShouldHaveSingleItem();
        issue.Keyword.ShouldBe("json");
        issue.Location.ShouldBe(string.Empty);
    }

    [Fact]
    public void A_property_written_twice_is_an_issue_because_the_last_one_would_silently_win()
    {
        var text = DataFiles.Text("ingredients", "gao").Replace("\"isPantry\": true,", "\"isPantry\": true,\n  \"isPantry\": false,");

        DataFiles.Schemas.Validate("ingredient", text).ShouldContain(i => i.Keyword == "json");
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("\"gao\"")]
    [InlineData("42")]
    public void A_file_that_is_not_an_object_is_invalid(string text)
    {
        DataFiles.Schemas.Validate("ingredient", text).ShouldContain(i => i.Location == string.Empty && i.Keyword == "type");
    }

    [Fact]
    public void Several_problems_in_one_file_are_all_reported()
    {
        var root = JsonNode.Parse(DataFiles.Text("dishes", "thit-kho-trung"))!.AsObject();
        root["slot"] = "main";
        root["spicy"] = "hot";
        Step(root, 1)["order"] = 0;

        var issues = DataFiles.Schemas.Validate("dish", root.ToJsonString());

        issues.Select(i => i.Location).ShouldBe(["/slot", "/spicy", "/steps/1/order"], ignoreOrder: true);
    }

    [Fact]
    public void Every_case_breaks_a_file_that_was_valid_before()
    {
        // Guards the table itself: a case that only fails because its base file was already broken proves nothing.
        foreach (var c in Cases.Values.DistinctBy(c => (c.Schema, c.File)))
        {
            DataFiles.Schemas.Validate(c.Schema, DataFiles.Text(c.Folder, c.File)).ShouldBeEmpty($"{c.Folder}/{c.File}");
        }
    }
}
