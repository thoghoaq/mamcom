namespace MamCom.Core.Tests.Json;

/// <summary>
/// The example documents from SPEC §7.1 and §7.2. Differences from the SPEC text, all deliberate:
/// <c>wasteRatio</c> is written as 0 instead of 0.0 (same number, canonical form) and <c>draftedBy</c> is
/// "llm" because the SPEC shows the choice "llm|human".
/// </summary>
internal static class SpecSamples
{
    public const string Ingredient = """
        {
          "id": "thit-ba-chi",
          "names": { "default": "Thịt ba chỉ", "north": "Thịt ba chỉ", "south": "Thịt ba rọi" },
          "aliases": ["ba rọi", "thịt ba rọi"],
          "category": "thit-heo",
          "aisle": "thit-ca",
          "proteinSource": "heo",
          "marketUnit": {
            "north": { "display": "lạng", "gramsPerUnit": 100, "step": 0.5 },
            "south": { "display": "g", "gramsPerUnit": 1, "step": 50 }
          },
          "wasteRatio": 0,
          "isPantry": false,
          "nutritionPer100gEdible": { "kcal": null, "proteinG": null, "fatG": null, "carbG": null, "fiberG": null, "sodiumMg": null },
          "nutritionSource": { "db": "placeholder", "ref": null, "matchedName": null, "verifiedBy": null, "verifiedAt": null },
          "image": { "thumb": null, "full": null, "source": null, "license": null, "isIllustration": false }
        }
        """;

    public const string Dish = """
        {
          "id": "canh-chua-ca-loc",
          "name": "Canh chua cá lóc",
          "slot": "canh",
          "regions": ["south"],
          "seasons": ["all"],
          "spicy": "optional",
          "cookingMethod": "nau",
          "proteinSource": "ca",
          "serveHot": false,
          "ingredients": [
            { "ingredientId": "ca-loc", "gramsEdiblePerServing": 80, "scale": "linear", "consumedRatio": 1, "note": "cắt khúc" }
          ],
          "steps": [
            { "order": 1, "text": "Làm sạch cá, cắt khúc, ướp chút muối.", "activeMinutes": 10, "passiveMinutes": 0, "ingredientIds": ["ca-loc"] }
          ],
          "status": "draft",
          "provenance": { "draftedBy": "llm", "reviewedBy": null, "reviewedAt": null, "cookTestedAt": null, "measuredTotalMinutes": null },
          "image": { "thumb": null, "full": null, "source": null, "license": null, "isIllustration": false }
        }
        """;
}
