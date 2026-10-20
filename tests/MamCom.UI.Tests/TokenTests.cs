using System.Globalization;
using System.Text.RegularExpressions;

namespace MamCom.UI.Tests;

/// <summary>
/// The look of the app is decided in tokens.css (CLAUDE.md: no colour or size in a component). These tests read that file the way a
/// browser would and hold it to what the project promises: dark mode that reads as well as light, text that meets WCAG contrast,
/// and a dark palette that is the same whether it comes from the system or from the data-theme attribute.
/// </summary>
public partial class TokenTests
{
    private static readonly string Wwwroot = Path.Combine(AppContext.BaseDirectory, "wwwroot");

    private static readonly string Tokens = File.ReadAllText(Path.Combine(Wwwroot, "tokens.css"));

    /// <summary>The text between the braces of the rule that starts with <paramref name="selector"/>.</summary>
    private static string Rule(string css, string selector)
    {
        var start = css.IndexOf(selector, StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0, $"no rule '{selector}' in tokens.css");
        var open = css.IndexOf('{', start);
        var depth = 0;
        for (var i = open; i < css.Length; i++)
        {
            depth += css[i] == '{' ? 1 : css[i] == '}' ? -1 : 0;
            if (depth == 0)
            {
                return css[(open + 1)..i];
            }
        }

        throw new InvalidOperationException($"rule '{selector}' is not closed");
    }

    private static SortedDictionary<string, string> Variables(string block) =>
        new(Declaration().Matches(block).ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value.Trim(), StringComparer.Ordinal), StringComparer.Ordinal);

    private static SortedDictionary<string, string> Light => Variables(Rule(Tokens, ":root,\n[data-theme=\"light\"]"));

    private static SortedDictionary<string, string> DarkBySystem => Variables(Rule(Rule(Tokens, "@media (prefers-color-scheme: dark)"), ":root:not([data-theme=\"light\"])"));

    private static SortedDictionary<string, string> DarkByAttribute => Variables(Rule(Tokens, "[data-theme=\"dark\"] {"));

    private static SortedDictionary<string, string> Theme(string name) => name == "light" ? Light : DarkByAttribute;

    // ---- the two modes -------------------------------------------------------------------------------------------------------

    [Fact]
    public void The_dark_palette_is_the_same_from_the_system_and_from_the_attribute()
    {
        DarkBySystem.Count.ShouldBeGreaterThan(30);
        DarkBySystem.ShouldBe(DarkByAttribute);
    }

    [Fact]
    public void Light_and_dark_define_exactly_the_same_variables()
    {
        Light.Keys.ShouldBe(DarkByAttribute.Keys);
        Light.Keys.ShouldBe(DarkBySystem.Keys);
    }

    [Fact]
    public void Every_colour_is_a_plain_hex_value_so_a_test_can_measure_it()
    {
        foreach (var theme in new[] { Light, DarkByAttribute })
        {
            foreach (var (name, value) in theme.Where(v => v.Key.StartsWith("--color-", StringComparison.Ordinal)))
            {
                HexValue().IsMatch(value).ShouldBeTrue($"{name}: {value}");
            }
        }
    }

    [Fact]
    public void Dark_is_really_dark_and_light_is_really_light()
    {
        Luminance(Light["--color-bg"]).ShouldBeGreaterThan(0.8);
        Luminance(DarkByAttribute["--color-bg"]).ShouldBeLessThan(0.05);
        Luminance(Light["--color-text"]).ShouldBeLessThan(0.05);
        Luminance(DarkByAttribute["--color-text"]).ShouldBeGreaterThan(0.6);
    }

    [Fact]
    public void An_element_can_force_either_theme_and_tell_the_browser_so_form_controls_follow()
    {
        Rule(Tokens, ":root,\n[data-theme=\"light\"]").ShouldContain("color-scheme: light;");
        Rule(Tokens, "[data-theme=\"dark\"] {").ShouldContain("color-scheme: dark;");
        Rule(Rule(Tokens, "@media (prefers-color-scheme: dark)"), ":root:not([data-theme=\"light\"])").ShouldContain("color-scheme: dark;");
    }

    // ---- contrast (WCAG 2.2) -------------------------------------------------------------------------------------------------

    public static TheoryData<string, string, string, double> Pairs()
    {
        var data = new TheoryData<string, string, string, double>();
        foreach (var theme in new[] { "light", "dark" })
        {
            // Text, 4.5:1.
            foreach (var background in new[] { "bg", "surface", "surface-raised" })
            {
                data.Add(theme, "text", background, 4.5);
                data.Add(theme, "text-muted", background, 4.5);
            }

            foreach (var background in new[] { "bg", "surface" })
            {
                data.Add(theme, "accent", background, 4.5);   // a link, and the label of an outline button
                data.Add(theme, "accent-strong", background, 4.5);
            }

            data.Add(theme, "on-accent", "accent", 4.5);   // the primary button
            data.Add(theme, "on-accent", "accent-strong", 4.5);   // and when it is hovered
            data.Add(theme, "accent-strong", "accent-soft", 4.5);   // an outline button when it is hovered

            // The words of a badge, and the words of a notice.
            foreach (var tone in new[] { "leaf", "warning", "danger", "info" })
            {
                data.Add(theme, tone, tone + "-soft", 4.5);
                data.Add(theme, "text", tone + "-soft", 4.5);
            }

            // The edge of a field, of a quiet button and of a chip is what tells you it is there: 3:1 (WCAG 1.4.11).
            foreach (var background in new[] { "bg", "surface", "surface-raised" })
            {
                data.Add(theme, "border-strong", background, 3);
            }

            // What marks the focus, and what draws a part of a picture, 3:1.
            data.Add(theme, "focus", "bg", 3);
            data.Add(theme, "focus", "surface", 3);
            foreach (var tone in new[] { "slot-soup", "slot-main", "slot-veg", "aisle-meat", "aisle-produce", "aisle-egg", "aisle-dry", "aisle-other" })
            {
                data.Add(theme, tone, tone + "-soft", 3);   // the drawing of a placeholder on its tile
                data.Add(theme, "text", tone + "-soft", 4.5);   // the words of a placeholder on its tile
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public void Colours_that_are_read_together_meet_the_contrast_the_job_needs(string theme, string foreground, string background, double needed)
    {
        var colours = Theme(theme);

        var ratio = Contrast(colours[$"--color-{foreground}"], colours[$"--color-{background}"]);

        ratio.ShouldBeGreaterThanOrEqualTo(needed, $"{theme}: {foreground} {colours[$"--color-{foreground}"]} on {background} {colours[$"--color-{background}"]} is {ratio:0.00}:1, needs {needed}:1");
    }

    // ---- what a component may write -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData("base.css")]
    [InlineData("layout.css")]
    public void A_stylesheet_of_components_writes_no_colour_of_its_own(string file)
    {
        var css = File.ReadAllText(Path.Combine(Wwwroot, file));

        HexColour().IsMatch(css).ShouldBeFalse($"{file} has a hex colour; use a variable of tokens.css");
        FunctionColour().IsMatch(css).ShouldBeFalse($"{file} has an rgb(), hsl() or named colour function; use a variable of tokens.css");
    }

    [Theory]
    [InlineData("base.css")]
    [InlineData("layout.css")]
    public void A_stylesheet_of_components_writes_no_font_size_of_its_own(string file)
    {
        var css = File.ReadAllText(Path.Combine(Wwwroot, file));

        var sizes = FontSize().Matches(css).Select(m => m.Groups[1].Value.Trim()).ToArray();

        sizes.ShouldAllBe(size => size.StartsWith("var(--font-size-", StringComparison.Ordinal) || size == "inherit", $"{file}: {string.Join(", ", sizes)}");
    }

    [Fact]
    public void The_smallest_text_is_16px_the_base_is_18px_and_cooking_is_at_least_22px()
    {
        var root = Variables(Rule(Tokens, ":root {\n    /* Type."));

        Px(root["--font-size-small"]).ShouldBeGreaterThanOrEqualTo(16);
        Px(root["--font-size-base"]).ShouldBe(18);
        Px(root["--font-size-cooking"]).ShouldBeGreaterThanOrEqualTo(22);
        double.Parse(root["--line-height"], CultureInfo.InvariantCulture).ShouldBeGreaterThanOrEqualTo(1.5);
        double.Parse(root["--line-height-tight"], CultureInfo.InvariantCulture).ShouldBeGreaterThanOrEqualTo(1.5);
        Px(root["--tap-size"]).ShouldBeGreaterThanOrEqualTo(48);
    }

    [Fact]
    public void The_safe_area_of_a_phone_is_in_the_tokens_and_the_frame_of_the_app_keeps_clear_of_it()
    {
        var root = Variables(Rule(Tokens, ":root {\n    /* Type."));
        foreach (var side in new[] { "top", "right", "bottom", "left" })
        {
            root[$"--safe-{side}"].ShouldBe($"env(safe-area-inset-{side}, 0px)");
        }

        // Each part of the frame keeps clear of the sides it touches: the header the top, the actions the bottom, all three the left and right.
        var layout = File.ReadAllText(Path.Combine(Wwwroot, "layout.css"));
        var padding = (string selector) => PropertyValue(Rule(layout, selector), "padding");
        foreach (var (selector, sides) in new[] { (".app-header {", new[] { "top", "right", "left" }), (".app-main {", ["right", "left"]), (".app-actions {", ["bottom", "right", "left"]) })
        {
            var value = padding(selector);
            foreach (var side in sides)
            {
                value.ShouldContain($"var(--safe-{side})", customMessage: $"{selector} padding keeps clear of the {side}");
            }

            value.Split("var(--safe-").Length.ShouldBe(sides.Length + 1, $"{selector} padding names exactly the sides it touches");
        }
    }

    private static string PropertyValue(string block, string property) =>
        Regex.Match(block, property + @":\s*([^;]+);").Groups[1].Value;

    // ---- the arithmetic ------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("#000000", "#ffffff", 21.0)]
    [InlineData("#ffffff", "#ffffff", 1.0)]
    [InlineData("#767676", "#ffffff", 4.54)]   // the grey at the edge of AA on white
    [InlineData("#777777", "#ffffff", 4.48)]
    public void The_contrast_ratio_is_the_one_of_WCAG(string foreground, string background, double expected)
    {
        Contrast(foreground, background).ShouldBe(expected, 0.01);
        Contrast(background, foreground).ShouldBe(expected, 0.01);
    }

    private static double Contrast(string a, string b)
    {
        var (light, dark) = Luminance(a) >= Luminance(b) ? (Luminance(a), Luminance(b)) : (Luminance(b), Luminance(a));
        return (light + 0.05) / (dark + 0.05);
    }

    private static double Luminance(string hex)
    {
        double Channel(int offset)
        {
            var value = int.Parse(hex.AsSpan(1 + offset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
            return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(0)) + (0.7152 * Channel(2)) + (0.0722 * Channel(4));
    }

    private static double Px(string rem) =>
        rem.EndsWith("rem", StringComparison.Ordinal)
            ? double.Parse(rem[..^3], CultureInfo.InvariantCulture) * 16
            : double.Parse(rem[..^2], CultureInfo.InvariantCulture);

    [GeneratedRegex(@"(--[a-z0-9-]+):\s*([^;]+);")]
    private static partial Regex Declaration();

    [GeneratedRegex(@"^#[0-9a-f]{6}$")]
    private static partial Regex HexValue();

    [GeneratedRegex(@"#[0-9a-fA-F]{3,8}\b")]
    private static partial Regex HexColour();

    [GeneratedRegex(@"\b(?:rgb|rgba|hsl|hsla|oklch|oklab|color-mix)\(", RegexOptions.IgnoreCase)]
    private static partial Regex FunctionColour();

    [GeneratedRegex(@"font-size:\s*([^;]+);")]
    private static partial Regex FontSize();
}
