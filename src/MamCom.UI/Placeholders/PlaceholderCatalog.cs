using System.Globalization;
using MamCom.Core.Models;

namespace MamCom.UI.Placeholders;

public enum PlaceholderKind
{
    Ingredient,
    Dish,
}

/// <summary>What stands in for the picture of one ingredient or dish: where it looks like (tone), what it draws (icon) and what it is called.</summary>
/// <param name="Tone">The colour family, a modifier of the <c>tile</c> class in base.css: <c>aisle-meat</c>, <c>slot-soup</c>…</param>
/// <param name="Icon">The id of a symbol in <c>icons/icons.svg</c>.</param>
/// <param name="Name">The words for the type, shown under the drawing and read to a screen reader, for example "Thịt heo" or "Món mặn · thịt bò".</param>
public sealed record Placeholder(PlaceholderKind Kind, string Tone, string Icon, string Name);

/// <summary>
/// Chooses the placeholder for a picture that does not exist yet (SPEC §7.5). There is no one picture for everything: an
/// ingredient is drawn by its <c>category</c> (a carrot, a pig, a mushroom…) in the colour of its aisle, and a dish by its
/// protein source (a fish, an egg…) in the colour of its part of the meal. A value the catalog does not know yet falls back to
/// the drawing of the aisle or of the part of the meal, never to a picture shared by everything. Only fields the data already
/// has are read.
/// </summary>
public static class PlaceholderCatalog
{
    private static readonly IReadOnlyDictionary<string, string> CategoryIcons = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["rau-cu"] = "carrot",
        ["rau-la"] = "leaf",
        ["qua"] = "apple",
        ["thit-heo"] = "pig",
        ["thit-bo"] = "cow",
        ["thit-ga"] = "meat",
        ["ca"] = "fish",
        ["hai-san"] = "shrimp",
        ["trung"] = "egg",
        ["dau-hu"] = "tofu",
        ["nam"] = "mushroom",
        ["gia-vi"] = "salt",
        ["ngu-coc"] = "wheat",
    };

    private static readonly IReadOnlyDictionary<string, string> ProteinIcons = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["heo"] = "pig",
        ["bo"] = "cow",
        ["ga"] = "meat",
        ["ca"] = "fish",
        ["tom"] = "shrimp",
        ["trung"] = "egg",
        [ProteinSources.Vegetarian] = "seedling",
    };

    /// <summary>The categories that have a drawing of their own, in the order the styleguide shows them.</summary>
    public static IReadOnlyList<string> KnownCategories { get; } = [.. CategoryIcons.Keys];

    /// <summary>The protein sources that have a drawing of their own.</summary>
    public static IReadOnlyList<string> KnownProteinSources { get; } = [.. ProteinIcons.Keys];

    public static Placeholder ForIngredient(Ingredient ingredient)
    {
        ArgumentNullException.ThrowIfNull(ingredient);

        return ForIngredient(ingredient.Category, ingredient.Aisle);
    }

    public static Placeholder ForIngredient(string category, Aisle aisle)
    {
        ArgumentNullException.ThrowIfNull(category);

        var tone = AisleTone(aisle);
        return CategoryIcons.TryGetValue(category, out var icon)
            ? new Placeholder(PlaceholderKind.Ingredient, tone, icon, Text("Placeholder_Cat_" + Key(category)))
            : new Placeholder(PlaceholderKind.Ingredient, tone, AisleIcon(aisle), Text("Placeholder_Aisle_" + Key(AisleId(aisle))));
    }

    public static Placeholder ForDish(Dish dish)
    {
        ArgumentNullException.ThrowIfNull(dish);

        return ForDish(dish.Slot, dish.ProteinSource);
    }

    public static Placeholder ForDish(DishSlot slot, string? proteinSource)
    {
        var tone = SlotTone(slot);
        var slotName = Text("Placeholder_Slot_" + Key(SlotId(slot)));

        return proteinSource is not null && ProteinIcons.TryGetValue(proteinSource, out var icon)
            ? new Placeholder(
                PlaceholderKind.Dish,
                tone,
                icon,
                string.Format(CultureInfo.InvariantCulture, Strings.Placeholder_DishFormat, slotName, Text("Placeholder_Protein_" + Key(proteinSource))))
            : new Placeholder(PlaceholderKind.Dish, tone, SlotIcon(slot), slotName);
    }

    /// <summary>What a screen reader says for a tile: it is not a photo, and what it stands for.</summary>
    public static string Describe(Placeholder placeholder)
    {
        ArgumentNullException.ThrowIfNull(placeholder);

        return string.Format(CultureInfo.InvariantCulture, Strings.Placeholder_AriaFormat, placeholder.Name.ToLowerInvariant());
    }

    private static string AisleTone(Aisle aisle) => aisle switch
    {
        Aisle.MeatAndFish => "aisle-meat",
        Aisle.Produce => "aisle-produce",
        Aisle.EggsAndTofu => "aisle-egg",
        Aisle.DryGoodsAndSpices => "aisle-dry",
        Aisle.Other => "aisle-other",
        _ => throw new ArgumentOutOfRangeException(nameof(aisle), aisle, null),
    };

    private static string AisleIcon(Aisle aisle) => aisle switch
    {
        Aisle.MeatAndFish => "steak",
        Aisle.Produce => "basket",
        Aisle.EggsAndTofu => "egg-cracked",
        Aisle.DryGoodsAndSpices => "bottle",
        Aisle.Other => "package",
        _ => throw new ArgumentOutOfRangeException(nameof(aisle), aisle, null),
    };

    private static string AisleId(Aisle aisle) => aisle switch
    {
        Aisle.MeatAndFish => "thit-ca",
        Aisle.Produce => "rau-cu-qua",
        Aisle.EggsAndTofu => "trung-dau-phu",
        Aisle.DryGoodsAndSpices => "kho-gia-vi",
        Aisle.Other => "khac",
        _ => throw new ArgumentOutOfRangeException(nameof(aisle), aisle, null),
    };

    private static string SlotTone(DishSlot slot) => slot switch
    {
        DishSlot.Soup => "slot-soup",
        DishSlot.Main => "slot-main",
        DishSlot.Vegetable => "slot-veg",
        _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, null),
    };

    private static string SlotIcon(DishSlot slot) => slot switch
    {
        DishSlot.Soup => "soup",
        DishSlot.Main => "tools-kitchen-2",
        DishSlot.Vegetable => "salad",
        _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, null),
    };

    private static string SlotId(DishSlot slot) => slot switch
    {
        DishSlot.Soup => "canh",
        DishSlot.Main => "man",
        DishSlot.Vegetable => "rau",
        _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, null),
    };

    /// <summary>The id as it is written in a resource name: letters, digits and underscores.</summary>
    private static string Key(string id) => id.Replace('-', '_');

    private static string Text(string resourceName) =>
        Strings.ResourceManager.GetString(resourceName, CultureInfo.InvariantCulture)
        ?? throw new InvalidOperationException($"There is no string '{resourceName}' in Strings.vi.resx.");
}
