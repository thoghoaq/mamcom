using System.Text.Json.Serialization;
using MamCom.Core.Models;

namespace MamCom.Core.Json;

/// <summary>
/// Source-generated JSON metadata for every Core type that is stored or exchanged. Source generation keeps
/// serialization trim-safe for the WebAssembly build. Use <see cref="CoreJson.Options"/> to read and write.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    IndentSize = 2,
    NewLine = "\n",
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    AllowDuplicateProperties = false)]
[JsonSerializable(typeof(Household))]
[JsonSerializable(typeof(Member))]
[JsonSerializable(typeof(Ingredient))]
[JsonSerializable(typeof(Dish))]
[JsonSerializable(typeof(MealTray))]
[JsonSerializable(typeof(SpinResult))]
[JsonSerializable(typeof(ShoppingItem))]
[JsonSerializable(typeof(NutritionFacts))]
[JsonSerializable(typeof(CookPlan))]
public partial class CoreJsonContext : JsonSerializerContext;
