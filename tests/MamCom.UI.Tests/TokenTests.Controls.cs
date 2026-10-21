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
    [InlineData(".cook-overview__step {")]
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

    [Theory]
    [InlineData(".app-header__brand {")]
    [InlineData(".app-header__action {")]
    public void The_links_of_the_header_are_at_least_the_tap_size_high(string selector)
    {
        var layout = File.ReadAllText(Path.Combine(Wwwroot, "layout.css"));

        PropertyValue(Rule(layout, selector), "min-height").ShouldBe("var(--tap-size)");
    }

    [Fact]
    public void The_link_to_the_settings_is_a_square_of_at_least_the_tap_size_at_the_far_edge_and_the_header_lets_it_drop_under_the_name()
    {
        var layout = File.ReadAllText(Path.Combine(Wwwroot, "layout.css"));

        PropertyValue(Rule(layout, ".app-header__action {"), "min-width").ShouldBe("var(--tap-size)");
        PropertyValue(Rule(layout, ".app-header__action {"), "margin-inline-start").ShouldBe("auto");
        PropertyValue(Rule(layout, ".app-header {"), "flex-wrap").ShouldBe("wrap");
    }

    [Fact]
    public void When_the_text_is_set_large_the_header_and_the_bar_of_actions_stop_sticking_and_the_buttons_go_under_each_other()
    {
        var layout = File.ReadAllText(Path.Combine(Wwwroot, "layout.css"));

        PropertyValue(Rule(layout, ".app {"), "container-type").ShouldBe("inline-size");
        var narrow = Rule(layout, "@container (max-width: 20rem)");
        PropertyValue(Rule(narrow, ".app-header,\n    .app-actions {"), "position").ShouldBe("static");
        PropertyValue(Rule(narrow, ".app-actions > * {"), "flex-basis").ShouldBe("100%");
        PropertyValue(Rule(narrow, ".app-actions .button {"), "white-space").ShouldBe("normal");
        PropertyValue(Rule(layout, ".app-actions .button {"), "white-space").ShouldBe("nowrap");   // at the usual size a word stays on one line
    }

    [Fact]
    public void When_the_text_is_set_large_a_dish_card_stacks_and_a_row_of_the_shopping_list_gives_up_its_picture()
    {
        PropertyValue(Rule(Base, ".tray {"), "container-type").ShouldBe("inline-size");
        PropertyValue(Rule(Base, ".shop-items {"), "container-type").ShouldBe("inline-size");

        // Both rules ask the list the same question: is there less than 15 times the size of the text across?
        var first = Base.IndexOf("@container (max-width: 15rem)", StringComparison.Ordinal);
        var second = Base.IndexOf("@container (max-width: 15rem)", first + 1, StringComparison.Ordinal);
        first.ShouldBeGreaterThan(0);
        second.ShouldBeGreaterThan(first);

        var cards = Rule(Base[first..], "@container (max-width: 15rem)");
        PropertyValue(Rule(cards, ".dish-card {"), "grid-template-columns").ShouldBe("1fr");
        PropertyValue(Rule(cards, ".dish-card__actions {"), "justify-content").ShouldBe("flex-start");

        var rows = Rule(Base[second..], "@container (max-width: 15rem)");
        PropertyValue(Rule(rows, ".shop-item__label {"), "grid-template-columns").ShouldBe("1.75rem minmax(0, 1fr)");
        PropertyValue(Rule(rows, ".shop-item__label > .picture,\n    .shop-item__label > .tile {"), "display").ShouldBe("none");
    }

    [Fact]
    public void Rows_that_hold_a_word_and_its_number_or_two_buttons_and_a_number_may_wrap_when_they_do_not_fit()
    {
        PropertyValue(Rule(Base, ".stepper {"), "flex-wrap").ShouldBe("wrap");
        PropertyValue(Rule(Base, ".tray-summary__row {"), "flex-wrap").ShouldBe("wrap");
        PropertyValue(Rule(Base, ".tray-history {"), "flex-wrap").ShouldBe("wrap");
    }

    [Fact]
    public void A_button_that_destroys_is_outlined_in_the_colour_of_danger_which_reads_in_the_light_and_the_dark()
    {
        PropertyValue(Rule(Base, ".button--danger {"), "border-color").ShouldBe("var(--color-danger)");
        PropertyValue(Rule(Base, ".button--danger {"), "color").ShouldBe("var(--color-danger)");
        PropertyValue(Rule(Base, ".button--danger:hover:not(:disabled) {"), "background").ShouldBe("var(--color-danger-soft)");
        PropertyValue(Rule(Base, ".button--danger:hover:not(:disabled) {"), "color").ShouldBe("var(--color-danger)");
    }

    [Fact]
    public void Three_choices_in_a_row_are_three_columns_of_equal_width_and_the_row_for_the_whole_meal_is_taller_and_larger()
    {
        PropertyValue(Rule(Base, ".chips--thirds {"), "display").ShouldBe("grid");
        PropertyValue(Rule(Base, ".chips--thirds {"), "grid-template-columns").ShouldBe("repeat(3, minmax(0, 1fr))");
        PropertyValue(Rule(Base, ".rate-row .chips,\n.chips--rate-whole {"), "margin-bottom").ShouldBe("0");
        PropertyValue(Rule(Base, ".chips--rate-whole .chip {"), "font-size").ShouldBe("var(--font-size-large)");
        PropertyValue(Rule(Base, ".chips--rate-whole .chip {"), "min-height").ShouldBe("3.5rem");
    }

    [Fact]
    public void The_chips_of_a_row_of_thirds_are_rounded_rectangles_with_little_padding_so_a_word_that_wraps_does_not_make_a_circle()
    {
        var chip = Rule(Base, ".chips--thirds .chip {");

        PropertyValue(chip, "border-radius").ShouldBe("var(--radius)");
        PropertyValue(chip, "padding-inline").ShouldBe("var(--space-2)");
        PropertyValue(chip, "text-align").ShouldBe("center");
    }

    [Fact]
    public void The_actions_of_a_notice_wrap_and_the_button_that_already_had_a_margin_does_not_add_one_inside_them()
    {
        PropertyValue(Rule(Base, ".notice__actions {"), "flex-wrap").ShouldBe("wrap");
        PropertyValue(Rule(Base, ".notice__actions > .notice__action {"), "margin-top").ShouldBe("0");
    }

    [Fact]
    public void A_row_of_the_blocked_dishes_is_a_target_high_and_the_name_takes_the_room_before_the_button()
    {
        PropertyValue(Rule(Base, ".blocked__item {"), "min-height").ShouldBe("var(--tap-size)");
        PropertyValue(Rule(Base, ".blocked__name {"), "flex").ShouldBe("1");
    }
}
