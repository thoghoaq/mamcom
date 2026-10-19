using System.Text.Json;
using MamCom.Core.Json;
using MamCom.Core.Models;

namespace MamCom.DataBuilder.Loading;

/// <summary>
/// The text of a dish file. Every tool that writes a dish (RecipeDrafter, Studio) writes exactly this, so a dish that is
/// read and written back without a change is the same bytes and Git sees no diff.
/// </summary>
public static class DishFile
{
    /// <summary>Indented with two spaces, LF line endings, nulls written, Vietnamese unescaped, one line feed at the end.</summary>
    public static string Serialize(Dish dish)
    {
        ArgumentNullException.ThrowIfNull(dish);

        return JsonSerializer.Serialize(dish, CoreJson.Options) + "\n";
    }

    /// <exception cref="JsonException">The text is not a dish: not JSON, a member missing, or an unknown value.</exception>
    public static Dish Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return JsonSerializer.Deserialize<Dish>(text, CoreJson.Options)
            ?? throw new JsonException("A dish file must be an object, not null.");
    }
}
