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
    [InlineData(".dish-card__link {")]
    [InlineData(".back__link {")]
    [InlineData(".shop-item__label {")]
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
    public void A_thumbnail_of_an_ingredient_has_the_shape_of_the_placeholder_it_replaces_so_the_rows_do_not_jump()
    {
        PropertyValue(Rule(Base, "\n.picture--ingredient {"), "aspect-ratio").ShouldBe(PropertyValue(Rule(Base, "\n.tile--ingredient {"), "aspect-ratio"));
    }

    [Fact]
    public void A_tick_that_was_made_strikes_the_words_through_and_is_not_told_by_colour_alone()
    {
        var done = Rule(Base, ".shop-item--done .shop-item__text {");

        PropertyValue(done, "text-decoration").ShouldBe("line-through");
        PropertyValue(done, "color").ShouldBe("var(--color-text-muted)");
    }

    [Fact]
    public void The_row_of_the_shopping_list_is_a_grid_of_a_tick_a_thumbnail_and_the_words_with_the_thumbnail_the_size_of_a_target()
    {
        var row = Rule(Base, ".shop-item__label {");

        PropertyValue(row, "display").ShouldBe("grid");
        PropertyValue(row, "grid-template-columns").ShouldStartWith("1.75rem 3.5rem");
        PropertyValue(Rule(Base, ".shop-item__label > .picture,\n.shop-item__label > .tile {"), "--tile-width").ShouldBe("3.5rem");
    }

    [Fact]
    public void The_bar_of_actions_can_say_what_a_button_just_did_on_a_row_of_its_own()
    {
        var layout = File.ReadAllText(Path.Combine(Wwwroot, "layout.css"));

        PropertyValue(Rule(layout, ".app-actions {"), "flex-wrap").ShouldBe("wrap");
        PropertyValue(Rule(layout, ".app-actions__status {"), "flex").ShouldBe("1 0 100%");
    }

    [Theory]
    [InlineData(".cook__text {", "var(--font-size-cooking)")]
    [InlineData(".cook__dish {", "var(--font-size-large)")]
    [InlineData(".cook__times {", "var(--font-size-large)")]
    [InlineData(".cook__ingredients {", "var(--font-size-large)")]
    [InlineData(".cook__position {", "var(--font-size-large)")]
    [InlineData(".cook-chip__time {", "var(--font-size-large)")]
    [InlineData(".cook-actions .button {", "var(--font-size-large)")]
    public void The_words_of_the_cooking_mode_are_set_in_the_large_sizes_which_are_at_least_22_pixels(string selector, string size)
    {
        PropertyValue(Rule(Base, selector), "font-size").ShouldBe(size);
        var root = Variables(Rule(Tokens, ":root {\n    /* Type."));
        Px(root["--font-size-large"]).ShouldBeGreaterThanOrEqualTo(22);
        Px(root["--font-size-cooking"]).ShouldBeGreaterThanOrEqualTo(22);
    }

    [Fact]
    public void The_halves_of_the_cooking_stage_are_the_left_and_the_right_half_of_it()
    {
        PropertyValue(Rule(Base, ".cook__stage {"), "position").ShouldBe("relative");
        PropertyValue(Rule(Base, ".cook-zone {"), "position").ShouldBe("absolute");
        PropertyValue(Rule(Base, ".cook-zone {"), "inline-size").ShouldBe("50%");
        PropertyValue(Rule(Base, ".cook-zone--back {"), "inset-inline-start").ShouldBe("0");
        PropertyValue(Rule(Base, ".cook-zone--next {"), "inset-inline-end").ShouldBe("0");
    }

    [Fact]
    public void A_timer_that_ran_out_is_a_chip_in_the_accent_with_the_colour_that_reads_on_it_and_the_timer_button_has_a_row_of_its_own()
    {
        var done = Rule(Base, ".cook-chip--done {");

        PropertyValue(done, "background").ShouldBe("var(--color-accent)");
        PropertyValue(done, "color").ShouldBe("var(--color-on-accent)");
        PropertyValue(Rule(Base, ".cook-actions > .cook-actions__timer {"), "flex").ShouldBe("1 0 100%");
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
