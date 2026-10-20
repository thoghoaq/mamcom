namespace MamCom.UI.Components;

/// <summary>The ids of the symbols in <c>icons/icons.svg</c>, so a page can list them. A test keeps this list and the sprite the same.</summary>
public static class IconCatalog
{
    public static IReadOnlyList<string> Names { get; } =
    [
        // What stands in for a missing picture.
        "carrot", "leaf", "apple", "pig", "cow", "meat", "fish", "shrimp", "egg", "tofu", "mushroom", "salt", "wheat",
        "steak", "basket", "egg-cracked", "bottle", "package", "soup", "tools-kitchen-2", "salad", "seedling",

        // The app.
        "dice", "lock", "lock-open", "check", "x", "plus", "minus", "share", "clock", "flame", "refresh", "chevron-left",
        "settings", "shopping-cart", "users", "moon", "sun",
    ];
}
