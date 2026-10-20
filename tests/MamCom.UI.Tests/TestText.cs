namespace MamCom.UI.Tests;

internal static class TestText
{
    /// <summary>The text with each non-breaking space written as a plain one, so a test can read the sentence it expects.</summary>
    public static string Plain(this string text) => text.Replace(' ', ' ');
}
