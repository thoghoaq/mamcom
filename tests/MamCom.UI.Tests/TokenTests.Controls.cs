namespace MamCom.UI.Tests;

public partial class TokenTests
{
    // A property, not a field: the fields of a partial class in two files are set in no promised order, and this one needs Wwwroot.
    private static string Base => File.ReadAllText(Path.Combine(Wwwroot, "base.css"));

    [Theory]
    [InlineData(".button {")]
    [InlineData(".chip {")]
    [InlineData(".input,\n.select,\n.textarea {")]
    [InlineData(".check {")]
    [InlineData(".household-home a {")]
    public void A_control_the_finger_has_to_hit_is_at_least_the_tap_size_high(string selector)
    {
        PropertyValue(Rule(Base, selector), "min-height").ShouldBe("var(--tap-size)");
    }

    [Fact]
    public void A_dish_card_leaves_up_and_down_to_the_page_so_a_swipe_along_it_reaches_the_card()
    {
        PropertyValue(Rule(Base, ".dish-card {"), "touch-action").ShouldBe("pan-y");
    }

    [Fact]
    public void The_rolling_of_the_cards_stops_for_a_person_who_asked_for_less_movement()
    {
        var reduced = Rule(Base, "@media (prefers-reduced-motion: reduce)");

        PropertyValue(Rule(reduced, ".dish-card--rolling {"), "animation").ShouldBe("none");
        PropertyValue(Rule(Base, ".dish-card--rolling {"), "animation").ShouldStartWith("dish-roll");
    }

    [Fact]
    public void A_picture_of_a_dish_has_the_shape_of_the_placeholder_it_replaces_so_the_cards_do_not_jump()
    {
        PropertyValue(Rule(Base, "\n.picture {"), "aspect-ratio").ShouldBe(PropertyValue(Rule(Base, "\n.tile--dish {"), "aspect-ratio"));
        PropertyValue(Rule(Base, "\n.picture {"), "inline-size").ShouldBe(PropertyValue(Rule(Base, "\n.tile {"), "inline-size"));
    }

    [Fact]
    public void The_number_of_bowls_is_set_in_the_large_size_and_stands_out()
    {
        var rule = Rule(Base, ".stepper__value {");

        PropertyValue(rule, "font-size").ShouldBe("var(--font-size-large)");
        PropertyValue(rule, "font-weight").ShouldBe("var(--font-weight-strong)");
    }

    [Fact]
    public void A_chip_that_is_chosen_is_told_apart_by_more_than_colour_alone()
    {
        var chosen = Rule(Base, ".chip[aria-checked=\"true\"],\n.chip[aria-pressed=\"true\"] {");

        PropertyValue(chosen, "border-color").ShouldBe("var(--color-accent)");
        PropertyValue(chosen, "background").ShouldBe("var(--color-accent-soft)");
        PropertyValue(chosen, "color").ShouldBe("var(--color-accent-strong)");
        PropertyValue(chosen, "font-weight").ShouldBe("var(--font-weight-strong)");
        PropertyValue(Rule(Base, ".chip {"), "border").ShouldContain("var(--color-border-strong)");   // the rest have the strong outline, the chosen one the accent
    }
}
