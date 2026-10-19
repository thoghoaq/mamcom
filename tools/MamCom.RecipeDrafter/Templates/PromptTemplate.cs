using System.Text;
using System.Text.RegularExpressions;

namespace MamCom.RecipeDrafter.Templates;

/// <summary>
/// A prompt file with <c>{{name}}</c> places to fill. It is strict so a typo in a prompt cannot reach the model as
/// literal braces: every place must have a value, and every value must have a place.
/// </summary>
public static partial class PromptTemplate
{
    [GeneratedRegex(@"\{\{\s*([a-z_]+)\s*\}\}")]
    private static partial Regex Place();

    /// <exception cref="FormatException">A place has no value, or a value has no place. The message names it.</exception>
    public static string Render(string template, IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(values);

        var used = new HashSet<string>(StringComparer.Ordinal);
        var rendered = new StringBuilder();
        var position = 0;

        foreach (Match match in Place().Matches(template))
        {
            var key = match.Groups[1].Value;
            if (!values.TryGetValue(key, out var value))
            {
                throw new FormatException($"the prompt has a place {{{{{key}}}}} that nothing fills.");
            }

            rendered.Append(template, position, match.Index - position).Append(value);
            position = match.Index + match.Length;
            used.Add(key);
        }

        rendered.Append(template, position, template.Length - position);

        var unused = values.Keys.Where(k => !used.Contains(k)).Order(StringComparer.Ordinal).ToArray();
        if (unused.Length > 0)
        {
            throw new FormatException($"the prompt has no place for: {string.Join(", ", unused)}.");
        }

        return rendered.ToString();
    }
}
