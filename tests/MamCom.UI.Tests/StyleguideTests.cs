using System.Xml.Linq;
using MamCom.UI.Components;
using MamCom.UI.Placeholders;

namespace MamCom.UI.Tests;

public class StyleguideTests : BunitContext
{
    private IRenderedComponent<Styleguide> Page() => Render<Styleguide>();

    [Fact]
    public void The_page_shows_the_same_specimen_in_light_and_in_dark()
    {
        var cut = Page();

        var panels = cut.FindAll("section[data-specimen]");
        panels.Select(p => p.GetAttribute("data-theme")).ShouldBe(["light", "dark"]);
        panels.Select(p => p.ClassList.Contains("theme-panel")).ShouldAllBe(x => x);
        panels.ShouldAllBe(p => p.InnerHtml.Length > 1000);   // a real specimen, not an empty frame
    }

    [Fact]
    public void Both_panels_hold_every_colour_every_icon_and_every_placeholder()
    {
        var cut = Page();

        foreach (var theme in new[] { "light", "dark" })
        {
            var panel = cut.Find($"section[data-specimen={theme}]");
            panel.QuerySelectorAll("[data-swatch]").Length.ShouldBe(23);
            panel.QuerySelectorAll(".icon-cell").Length.ShouldBe(IconCatalog.Names.Count);
            panel.QuerySelectorAll("[data-grid=ingredients] .tile").Length.ShouldBe(PlaceholderCatalog.KnownCategories.Count);
            panel.QuerySelectorAll("[data-grid=aisles] .tile").Length.ShouldBe(5);
            panel.QuerySelectorAll("[data-grid=dishes] .tile").Length.ShouldBe(3 * (PlaceholderCatalog.KnownProteinSources.Count + 1));
            panel.QuerySelectorAll("[data-grid=thumbs] .tile--compact").Length.ShouldBe(PlaceholderCatalog.KnownCategories.Count);
        }
    }

    [Fact]
    public void No_two_placeholders_of_the_same_grid_look_alike()
    {
        var cut = Page();
        var panel = cut.Find("section[data-specimen=light]");

        foreach (var grid in new[] { "ingredients", "dishes" })
        {
            var labels = panel.QuerySelectorAll($"[data-grid={grid}] .tile").Select(t => t.GetAttribute("aria-label")).ToArray();
            labels.Distinct().Count().ShouldBe(labels.Length, grid);

            var looks = panel.QuerySelectorAll($"[data-grid={grid}] .tile").Select(t => string.Join(' ', t.ClassList.Order()) + t.QuerySelector("use")!.GetAttribute("href")).ToArray();
            looks.Distinct().Count().ShouldBe(looks.Length, grid);
        }
    }

    [Fact]
    public void Every_field_has_a_label_and_the_ids_of_the_two_panels_do_not_collide()
    {
        var cut = Page();

        var ids = cut.FindAll("[id]").Select(e => e.Id!).ToArray();
        ids.Distinct().Count().ShouldBe(ids.Length);
        foreach (var field in cut.FindAll("input:not([type=checkbox]), select, textarea"))
        {
            cut.FindAll($"label[for={field.Id}]").Count.ShouldBe(1, field.Id);
        }
    }

    [Fact]
    public void A_button_that_is_only_an_icon_says_what_it_does()
    {
        var cut = Page();

        var iconOnly = cut.FindAll("button.button--icon").ToArray();

        iconOnly.Length.ShouldBe(6);   // three in each theme
        iconOnly.ShouldAllBe(b => !string.IsNullOrWhiteSpace(b.GetAttribute("aria-label")));
        iconOnly.ShouldAllBe(b => b.QuerySelector("svg")!.GetAttribute("aria-hidden") == "true");
    }

    [Fact]
    public void The_vietnamese_marks_and_the_sample_sentence_are_on_the_page_with_their_diacritics()
    {
        var text = Page().Markup;

        text.ShouldContain("Cơm tấm sườn bì chả, canh chua cá lóc, rau muống xào tỏi.");
        text.ShouldContain("ắ ằ ẳ ẵ ặ");
        text.ShouldNotContain('�');
    }

    [Fact]
    public void Every_colour_it_names_is_a_token_that_exists()
    {
        var cut = Page();
        var tokens = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "wwwroot", "tokens.css"));

        foreach (var swatch in cut.FindAll("section[data-specimen=light] [data-swatch]"))
        {
            tokens.ShouldContain($"--color-{swatch.GetAttribute("data-swatch")}:", customMessage: swatch.GetAttribute("data-swatch"));
        }
    }

    [Fact]
    public void The_list_of_icons_is_the_list_of_the_sprite()
    {
        XNamespace svg = "http://www.w3.org/2000/svg";
        var sprite = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "wwwroot", "icons", "icons.svg")).Root!
            .Elements(svg + "symbol").Select(s => (string)s.Attribute("id")!).Order(StringComparer.Ordinal).ToArray();

        IconCatalog.Names.Order(StringComparer.Ordinal).ToArray().ShouldBe(sprite);
        IconCatalog.Names.Distinct().Count().ShouldBe(IconCatalog.Names.Count);
    }
}
