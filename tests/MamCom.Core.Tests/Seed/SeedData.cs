using System.Text.Json;
using MamCom.Core.Json;
using MamCom.Core.Models;

namespace MamCom.Core.Tests.Seed;

/// <summary>A data file as it is on disk, and what it parses to.</summary>
internal sealed record SeedFile<T>(string FileName, string Text, T Value);

/// <summary>The dev seed under <c>data/ingredients</c> and <c>data/dishes</c> (PLAN 1.8), loaded once.</summary>
internal static class SeedData
{
    public static IReadOnlyList<SeedFile<Ingredient>> IngredientFiles { get; } = Load<Ingredient>("ingredients");

    public static IReadOnlyList<SeedFile<Dish>> DishFiles { get; } = Load<Dish>("dishes");

    public static IReadOnlyList<Ingredient> IngredientList { get; } = IngredientFiles.Select(f => f.Value).ToArray();

    public static IReadOnlyList<Dish> Dishes { get; } = DishFiles.Select(f => f.Value).ToArray();

    public static IReadOnlyDictionary<string, Ingredient> Ingredients { get; } = IngredientList.ToDictionary(i => i.Id);

    public static string Write<T>(T value) => JsonSerializer.Serialize(value, CoreJson.Options) + "\n";

    private static List<SeedFile<T>> Load<T>(string folder) =>
        TestFiles.DataFolder(folder)
            .Select(file => new SeedFile<T>(
                file.FileName,
                file.Text,
                JsonSerializer.Deserialize<T>(file.Text, CoreJson.Options)!))
            .ToList();
}
