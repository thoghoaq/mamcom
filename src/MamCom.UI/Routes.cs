namespace MamCom.UI;

/// <summary>Where the screens of the app live, relative to the base address. The pages declare the same paths in their <c>@page</c>; a test keeps them equal.</summary>
public static class Routes
{
    public const string Home = "";

    /// <summary>"Nhà mình có ai?": the first screen, and where the household is changed later.</summary>
    public const string Household = "nha-minh";

    /// <summary>"Đi chợ": the shopping list of the tray of the day (SPEC §8.1-4).</summary>
    public const string Shopping = "di-cho";

    /// <summary>The folder of the detail screens: one page for each dish, <c>mon/&lt;dish id&gt;</c>.</summary>
    public const string DishFolder = "mon";

    /// <summary>The detail screen of a dish (SPEC §8.1-3).</summary>
    public static string Dish(string dishId)
    {
        ArgumentException.ThrowIfNullOrEmpty(dishId);

        return $"{DishFolder}/{Uri.EscapeDataString(dishId)}";
    }
}
