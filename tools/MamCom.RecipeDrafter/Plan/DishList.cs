using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MamCom.Core.Models;

namespace MamCom.RecipeDrafter.Plan;

/// <summary>One dish the owner wants drafted: what <c>data/plan/dish-list.csv</c> says about it.</summary>
/// <param name="Id">Kebab-case ASCII id, the name of the draft file.</param>
public sealed record DishRequest(string Id, string Name, DishSlot Slot, IReadOnlyList<Region> Regions);

/// <summary>
/// <c>data/plan/dish-list.csv</c> (SPEC §7.4 step 1): columns <c>name,slot,regions</c> and an optional <c>id</c>.
/// <c>slot</c> is <c>canh</c>, <c>man</c> or <c>rau</c>; <c>regions</c> are <c>north</c>, <c>central</c>, <c>south</c>
/// joined by <c>;</c>, or empty. Without an <c>id</c> column the id is the name with the diacritics removed.
/// </summary>
public static partial class DishList
{
    public const string RelativePath = "plan/dish-list.csv";

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex KebabCase();

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NotAlphanumeric();

    /// <exception cref="FormatException">The header is wrong, a row is malformed, or two rows give the same id. The message names the line.</exception>
    public static IReadOnlyList<DishRequest> Parse(string csv)
    {
        ArgumentNullException.ThrowIfNull(csv);

        var dishes = new List<DishRequest>();
        var lineOfId = new Dictionary<string, int>(StringComparer.Ordinal);
        var lineNumber = 0;
        var hasIdColumn = false;
        var sawHeader = false;

        foreach (var rawLine in csv.TrimStart((char)0xFEFF).Split('\n'))
        {
            lineNumber++;
            var line = rawLine.TrimEnd('\r');
            if (line.Length == 0)
            {
                continue;
            }

            var fields = SplitFields(line, lineNumber);
            if (!sawHeader)
            {
                hasIdColumn = fields.Count == 4 && fields[3] == "id";
                var plain = fields.Count == 3;
                if (!(plain || hasIdColumn) || fields[0] != "name" || fields[1] != "slot" || fields[2] != "regions")
                {
                    throw new FormatException($"line {lineNumber}: the header must be \"name,slot,regions\" or \"name,slot,regions,id\".");
                }

                sawHeader = true;
                continue;
            }

            if (fields.Count != (hasIdColumn ? 4 : 3))
            {
                throw new FormatException($"line {lineNumber}: expected {(hasIdColumn ? 4 : 3)} fields, found {fields.Count}.");
            }

            var name = fields[0].Trim();
            if (name.Length == 0)
            {
                throw new FormatException($"line {lineNumber}: the name must not be empty.");
            }

            var id = hasIdColumn && fields[3].Trim().Length > 0 ? fields[3].Trim() : Slugify(name);
            if (!KebabCase().IsMatch(id) || id.Length > 64)
            {
                throw new FormatException($"line {lineNumber}: the id '{id}' must be kebab-case ASCII of at most 64 characters; add an id column.");
            }

            if (id == CookPlan.RiceDishId)
            {
                throw new FormatException($"line {lineNumber}: the id '{id}' is reserved for rice.");
            }

            if (lineOfId.TryGetValue(id, out var first))
            {
                throw new FormatException($"line {lineNumber}: the id '{id}' is already used on line {first}.");
            }

            lineOfId[id] = lineNumber;
            dishes.Add(new DishRequest(id, name, ParseSlot(fields[1].Trim(), lineNumber), ParseRegions(fields[2], lineNumber)));
        }

        if (!sawHeader)
        {
            throw new FormatException("the file is empty; it needs the header \"name,slot,regions\".");
        }

        return dishes;
    }

    /// <summary>The name without diacritics, lower case, words joined by hyphens: "Canh chua cá lóc" becomes "canh-chua-ca-loc".</summary>
    public static string Slugify(string name)
    {
        var decomposed = name.Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
        var plain = new StringBuilder();
        foreach (var c in decomposed.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark))
        {
            plain.Append(char.ToLowerInvariant(c));
        }

        return NotAlphanumeric().Replace(plain.ToString(), "-").Trim('-');
    }

    private static DishSlot ParseSlot(string text, int lineNumber) => text switch
    {
        "canh" => DishSlot.Soup,
        "man" => DishSlot.Main,
        "rau" => DishSlot.Vegetable,
        _ => throw new FormatException($"line {lineNumber}: the slot '{text}' must be canh, man or rau."),
    };

    private static List<Region> ParseRegions(string text, int lineNumber)
    {
        var regions = new List<Region>();
        foreach (var part in text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var region = part switch
            {
                "north" => Region.North,
                "central" => Region.Central,
                "south" => Region.South,
                _ => throw new FormatException($"line {lineNumber}: the region '{part}' must be north, central or south."),
            };

            if (regions.Contains(region))
            {
                throw new FormatException($"line {lineNumber}: the region '{part}' is listed twice.");
            }

            regions.Add(region);
        }

        return regions;
    }

    private static List<string> SplitFields(string line, int lineNumber)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    field.Append(c);
                }
            }
            else if (c == '"' && field.Length == 0)
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                fields.Add(field.ToString());
                field.Clear();
            }
            else
            {
                field.Append(c);
            }
        }

        if (inQuotes)
        {
            throw new FormatException($"line {lineNumber}: a quoted field is not closed.");
        }

        fields.Add(field.ToString());
        return fields;
    }
}
