using System.Globalization;
using MamCom.Core.Models;

namespace MamCom.Core.Shopping;

/// <summary>
/// Writes the shopping list as the plain text people send over Zalo (SPEC §6.2):
/// a header line, then one line per heading with the items joined by " · ".
/// </summary>
public sealed class ShoppingTextFormatter
{
    private const string ItemSeparator = " · ";

    private readonly ShareTextLabels _labels;

    public ShoppingTextFormatter(ShareTextLabels? labels = null)
    {
        _labels = labels ?? ShareTextLabels.Vietnamese;
        if (_labels.Weekdays.Count != 7)
        {
            throw new ArgumentException("Weekdays needs seven entries, Sunday first.", nameof(labels));
        }
    }

    /// <summary>
    /// Lines are separated by LF with no trailing newline. When nothing is left to buy only the header is returned.
    /// Numbers use a decimal comma and names are lower-cased, as in the SPEC example.
    /// </summary>
    /// <param name="date">The day of the meal; written as weekday and day/month without leading zeros.</param>
    /// <param name="memberCount">The head count in the header.</param>
    /// <param name="includePantry">Whether pantry items are listed; the app's toggle for "show what I already have".</param>
    public string Format(IReadOnlyList<ShoppingItem> items, DateOnly date, int memberCount, bool includePantry = false)
    {
        ArgumentNullException.ThrowIfNull(items);

        var lines = new List<string> { Header(date, memberCount) };

        var byHeading = ShoppingGroups.ByAisle(items, includePantry)
            .GroupBy(group => _labels.Headings.For(group.Aisle));

        foreach (var heading in byHeading)
        {
            var text = string.Join(ItemSeparator, heading.SelectMany(group => group.Items).Select(Describe));
            lines.Add($"{heading.Key}: {text}");
        }

        return string.Join("\n", lines);
    }

    private string Header(DateOnly date, int memberCount) =>
        $"{_labels.Shopping} {_labels.Meal} {_labels.Weekdays[(int)date.DayOfWeek]} {date.Day}/{date.Month} ({memberCount} {_labels.People})";

    private static string Describe(ShoppingItem item)
    {
        var text = $"{Number(item.Quantity)} {item.UnitDisplay} {item.Name.ToLowerInvariant()}";
        return item.ApproxGrams is { } grams ? $"{text} (~{Number(Math.Round(grams))}g)" : text;
    }

    /// <summary>Up to two decimals, no trailing zeros, decimal comma: 3, 2,5, 0,75.</summary>
    private static string Number(double value) =>
        Math.Round(value, 2).ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ',');
}
