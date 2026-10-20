using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using MamCom.Core.Models;
using MamCom.UI.Placeholders;

namespace MamCom.UI.Tests;

public partial class SpriteAndFontTests
{
    private static readonly string Wwwroot = Path.Combine(AppContext.BaseDirectory, "wwwroot");

    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

    private static XElement[] Symbols() =>
        [.. XDocument.Load(Path.Combine(Wwwroot, "icons", "icons.svg")).Root!.Elements(Svg + "symbol")];

    [Fact]
    public void Every_symbol_has_a_unique_id_a_24_pixel_grid_and_something_to_draw()
    {
        var symbols = Symbols();

        symbols.Length.ShouldBeGreaterThan(30);
        symbols.Select(s => (string?)s.Attribute("id")).ShouldAllBe(id => !string.IsNullOrWhiteSpace(id));
        symbols.Select(s => (string)s.Attribute("id")!).Distinct().Count().ShouldBe(symbols.Length);
        symbols.ShouldAllBe(s => (string?)s.Attribute("viewBox") == "0 0 24 24");
        symbols.ShouldAllBe(s => s.Elements().Any());
    }

    [Fact]
    public void No_icon_carries_a_colour_of_its_own_so_it_always_takes_the_colour_of_the_text()
    {
        var text = File.ReadAllText(Path.Combine(Wwwroot, "icons", "icons.svg"));
        var body = text[text.IndexOf("<svg", StringComparison.Ordinal)..];

        // Tabler fills the pips of the dice with currentColor, which is the colour of the text too.
        OwnPaint().Matches(body).Select(m => m.Value).ShouldBeEmpty("an icon paints itself in a colour other than currentColor");
        body.ShouldNotContain("style=");
        HexColour().IsMatch(body).ShouldBeFalse();
    }

    [Fact]
    public void The_sprite_says_where_the_icons_come_from_and_under_which_licence()
    {
        var text = File.ReadAllText(Path.Combine(Wwwroot, "icons", "icons.svg"));

        text.ShouldContain("Tabler Icons");
        text.ShouldContain("MIT");
        text.ShouldContain("THIRD-PARTY-NOTICES.md");
    }

    [Fact]
    public void Every_drawing_the_placeholders_use_is_in_the_sprite()
    {
        var ids = Symbols().Select(s => (string)s.Attribute("id")!).ToHashSet(StringComparer.Ordinal);
        var used = PlaceholderCatalog.KnownCategories.Select(c => PlaceholderCatalog.ForIngredient(c, Aisle.Produce).Icon)
            .Concat(Enum.GetValues<Aisle>().Select(a => PlaceholderCatalog.ForIngredient("loai-moi", a).Icon))
            .Concat(PlaceholderCatalog.KnownProteinSources.Select(p => PlaceholderCatalog.ForDish(DishSlot.Main, p).Icon))
            .Concat(Enum.GetValues<DishSlot>().Select(s => PlaceholderCatalog.ForDish(s, "none").Icon))
            .Distinct()
            .ToArray();

        used.Length.ShouldBeGreaterThan(20);
        used.Where(icon => !ids.Contains(icon)).ShouldBeEmpty("an icon the placeholders use is not in icons.svg");
    }

    [Fact]
    public void The_icons_the_app_screens_need_are_in_the_sprite()
    {
        var ids = Symbols().Select(s => (string)s.Attribute("id")!).ToHashSet(StringComparer.Ordinal);

        foreach (var icon in new[] { "dice", "lock", "lock-open", "check", "x", "plus", "minus", "share", "clock", "flame", "refresh", "chevron-left", "settings", "shopping-cart", "users", "moon", "sun" })
        {
            ids.ShouldContain(icon);
        }
    }

    // ---- the font ------------------------------------------------------------------------------------------------------------

    [Fact]
    public void The_font_faces_point_only_at_files_that_are_there_and_that_are_woff2()
    {
        var css = File.ReadAllText(Path.Combine(Wwwroot, "fonts.css"));
        var files = FontUrl().Matches(css).Select(m => m.Groups[1].Value).ToArray();

        files.Length.ShouldBe(4);
        foreach (var file in files)
        {
            var path = Path.Combine(Wwwroot, file.Replace('/', Path.DirectorySeparatorChar));
            File.Exists(path).ShouldBeTrue(file);
            Encoding.ASCII.GetString(File.ReadAllBytes(path), 0, 4).ShouldBe("wOF2", file);
        }
    }

    [Fact]
    public void The_font_covers_both_weights_in_both_scripts_Vietnamese_needs_and_shows_text_at_once()
    {
        var css = File.ReadAllText(Path.Combine(Wwwroot, "fonts.css"));

        FontFace().Matches(css).Count.ShouldBe(4);
        css.ShouldContain("font-weight: 400");
        css.ShouldContain("font-weight: 600");
        css.ShouldContain("be-vietnam-pro-vietnamese-400-normal.woff2");
        css.ShouldContain("be-vietnam-pro-vietnamese-600-normal.woff2");
        FontFace().Matches(css).ShouldAllBe(m => m.Value.Contains("font-display: swap", StringComparison.Ordinal));
        FontFace().Matches(css).ShouldAllBe(m => m.Value.Contains("font-family: \"Be Vietnam Pro\"", StringComparison.Ordinal));
    }

    [Fact]
    public void Each_face_says_the_weight_its_file_name_says_and_the_four_files_are_the_four_faces()
    {
        var css = File.ReadAllText(Path.Combine(Wwwroot, "fonts.css"));

        var faces = FontFace().Matches(css)
            .Select(m => (
                Weight: Regex.Match(m.Value, @"font-weight:\s*(\d+);").Groups[1].Value,
                File: FontUrl().Match(m.Value).Groups[1].Value))
            .ToArray();

        faces.Select(f => f.File).Order(StringComparer.Ordinal).ShouldBe(
        [
            "fonts/be-vietnam-pro-latin-400-normal.woff2",
            "fonts/be-vietnam-pro-latin-600-normal.woff2",
            "fonts/be-vietnam-pro-vietnamese-400-normal.woff2",
            "fonts/be-vietnam-pro-vietnamese-600-normal.woff2",
        ]);
        faces.ShouldAllBe(f => f.File.Contains($"-{f.Weight}-normal", StringComparison.Ordinal));
    }

    [Fact]
    public void The_vietnamese_faces_cover_the_marks_and_the_latin_faces_the_plain_letters()
    {
        var css = File.ReadAllText(Path.Combine(Wwwroot, "fonts.css"));

        foreach (var face in FontFace().Matches(css).Select(m => m.Value))
        {
            var range = Regex.Match(face, @"unicode-range:\s*([^;]+);").Groups[1].Value;
            if (face.Contains("-vietnamese-", StringComparison.Ordinal))
            {
                range.ShouldContain("U+1EA0-1EF9");   // ạ ả ấ ầ ẩ ẫ ậ ắ ằ ẳ ẵ ặ ẹ ẻ ẽ ế ề ể ễ ệ … ỹ
                range.ShouldContain("U+01A0-01A1");   // ơ
                range.ShouldContain("U+01AF-01B0");   // ư
                range.ShouldContain("U+0110-0111");   // đ
            }
            else
            {
                range.ShouldContain("U+0000-00FF");
            }
        }
    }

    [Fact]
    public void The_tokens_name_the_font_the_faces_define()
    {
        File.ReadAllText(Path.Combine(Wwwroot, "tokens.css")).ShouldContain("--font-family: \"Be Vietnam Pro\"");
    }

    [Fact]
    public void The_licence_of_the_font_travels_with_the_font_files()
    {
        var licence = File.ReadAllText(Path.Combine(Wwwroot, "fonts", "OFL.txt"));

        licence.ShouldContain("SIL OPEN FONT LICENSE Version 1.1");
        licence.ShouldContain("Be Vietnam Pro Project Authors");
    }

    [GeneratedRegex("(?:fill|stroke)=\"(?!currentColor\")[^\"]*\"")]
    private static partial Regex OwnPaint();

    [GeneratedRegex(@"#[0-9a-fA-F]{3,8}\b")]
    private static partial Regex HexColour();

    [GeneratedRegex("url\\(\"([^\"]+)\"\\)")]
    private static partial Regex FontUrl();

    [GeneratedRegex(@"@font-face\s*\{[^}]*\}")]
    private static partial Regex FontFace();
}
