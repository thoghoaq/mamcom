using System.Text.Json;
using MamCom.Core.Json;
using MamCom.Core.Models;

namespace MamCom.DataBuilder.Loading;

/// <summary>
/// The text of an ingredient file. Every tool that writes an ingredient (Studio) writes exactly this, so an ingredient that is
/// read and written back without a change is the same bytes and Git sees no diff.
/// </summary>
public static class IngredientFile
{
    /// <summary>Indented with two spaces, LF line endings, nulls written, Vietnamese unescaped, one line feed at the end.</summary>
    public static string Serialize(Ingredient ingredient)
    {
        ArgumentNullException.ThrowIfNull(ingredient);

        return JsonSerializer.Serialize(ingredient, CoreJson.Options) + "\n";
    }

    /// <exception cref="JsonException">The text is not an ingredient: not JSON, a member missing, or an unknown value.</exception>
    public static Ingredient Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return JsonSerializer.Deserialize<Ingredient>(text, CoreJson.Options)
            ?? throw new JsonException("An ingredient file must be an object, not null.");
    }
}
