using System.Text.RegularExpressions;

namespace MamCom.UI.Tests;

internal static partial class TestText
{
    /// <summary>The text with each non-breaking space written as a plain one, so a test can read the sentence it expects.</summary>
    public static string Plain(this string text) => text.Replace(' ', ' ');

    /// <summary>The words of the text as a reader sees them: non-breaking spaces as plain ones, any run of white space (a line break in the markup) as one space, no space at either end.</summary>
    public static string Words(this string text) => Whitespace().Replace(text.Plain(), " ").Trim();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
