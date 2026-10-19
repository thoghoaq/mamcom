using System.Text.Json.Serialization;
using MamCom.Core.Cooking;
using MamCom.Core.Models;
using MamCom.Core.Nutrition;
using MamCom.Core.Portions;
using MamCom.Core.Spinning;

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
[JsonSerializable(typeof(PortionConfig))]
[JsonSerializable(typeof(NutritionConfig))]
[JsonSerializable(typeof(SpinnerConfig))]
[JsonSerializable(typeof(CookingConfig))]
[JsonSerializable(typeof(SpinHistory))]
[JsonSerializable(typeof(DishPreference))]
[JsonSerializable(typeof(IReadOnlyDictionary<string, DishPreference>))]
public partial class CoreJsonContext : JsonSerializerContext;
