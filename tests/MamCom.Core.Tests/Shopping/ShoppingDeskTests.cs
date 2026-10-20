using System.Text.Json;
using MamCom.Core.Json;
using MamCom.Core.Models;
using MamCom.Core.Shopping;
using static MamCom.Core.Tests.Shopping.ShoppingFixtures;

namespace MamCom.Core.Tests.Shopping;

public class ShoppingDeskTests
{
    private static readonly DateOnly Sunday = new(2026, 10, 18);

    private static readonly MealTray TheTray = new(
        Sunday,
        [
            new SlotResult(DishSlot.Main, "thit-kho-trung", false),
            new SlotResult(DishSlot.Vegetable, "rau-muong-xao", false),
            new SlotResult(DishSlot.Soup, "canh-chua-ca-loc", true),
        ]);

    private static ShoppingDesk Desk() => new(PortionSettings);

    private static ShoppingSheet Sheet(Region region = Region.North) => Desk().Sheet(Family(region), TheTray, Tray, Catalog());

    private static ShoppingState State(string key, bool pantry = false, params string[] ticked) => new(key, ticked, pantry);

    private static string Key => ShoppingDesk.TrayKeyOf(TheTray);

    // ---- naming a tray -----------------------------------------------------------------------

    [Fact]
    public void A_tray_is_named_by_its_day_and_its_dishes_in_the_order_of_the_slots()
    {
        Key.ShouldBe("2026-10-18|thit-kho-trung,rau-muong-xao,canh-chua-ca-loc");
    }

    [Fact]
    public void Another_day_or_another_dish_or_another_order_is_another_tray()
    {
        var slots = TheTray.Slots;

        ShoppingDesk.TrayKeyOf(TheTray with { Date = Sunday.AddDays(1) }).ShouldNotBe(Key);
        ShoppingDesk.TrayKeyOf(TheTray with { Slots = [slots[0], slots[1]] }).ShouldNotBe(Key);
        ShoppingDesk.TrayKeyOf(TheTray with { Slots = [slots[1], slots[0], slots[2]] }).ShouldNotBe(Key);
        ShoppingDesk.TrayKeyOf(TheTray with { Slots = [slots[0], slots[1], slots[2] with { IsLocked = false }] }).ShouldBe(Key);   // a lock is not part of what is bought
    }

    [Fact]
    public void An_empty_tray_has_a_name_too()
    {
        ShoppingDesk.TrayKeyOf(new MealTray(Sunday, [])).ShouldBe("2026-10-18|");
    }

    // ---- the list ----------------------------------------------------------------------------

    [Fact]
    public void The_sheet_is_the_shopping_list_of_the_tray_for_the_household_with_the_day_and_the_head_count()
    {
        var sheet = Sheet();

        sheet.Date.ShouldBe(Sunday);
        sheet.MemberCount.ShouldBe(4);
        sheet.Items.Select(i => i.IngredientId).ShouldBe(Build(Region.North).Select(i => i.IngredientId));
        sheet.Items.Single(i => i.IngredientId == "thit-ba-chi").UnitDisplay.ShouldBe("lạng");
        Sheet(Region.South).Items.Single(i => i.IngredientId == "thit-ba-chi").UnitDisplay.ShouldBe("g");
    }

    [Fact]
    public void A_household_without_members_has_a_sheet_with_nothing_to_buy_and_no_head_count()
    {
        var sheet = Desk().Sheet(new Household(Region.North, BowlSize.Medium, []), TheTray, Tray, Catalog());

        sheet.MemberCount.ShouldBe(0);
        sheet.Items.ShouldBeEmpty();
    }

    [Fact]
    public void An_ingredient_missing_from_the_catalog_is_a_data_error()
    {
        Should.Throw<InvalidOperationException>(() => Desk().Sheet(Family(Region.North), TheTray, Tray, TestData.Catalog(Rice)));
    }

    // ---- opening the screen ------------------------------------------------------------------

    [Fact]
    public void A_screen_opened_with_nothing_saved_starts_with_no_ticks_and_the_pantry_hidden()
    {
        var state = Desk().Open(null, TheTray, Sheet());

        state.TrayKey.ShouldBe(Key);
        state.CheckedIngredientIds.ShouldBeEmpty();
        state.ShowPantry.ShouldBeFalse();
    }

    [Fact]
    public void The_ticks_saved_for_this_tray_are_there_again_in_the_order_they_were_made()
    {
        var saved = State(Key, true, "ca-chua", "thit-ba-chi", "trung");

        var state = Desk().Open(saved, TheTray, Sheet());

        state.CheckedIngredientIds.ShouldBe(["ca-chua", "thit-ba-chi", "trung"]);
        state.ShowPantry.ShouldBeTrue();
        state.TrayKey.ShouldBe(Key);
    }

    [Fact]
    public void A_tick_of_something_that_is_no_longer_on_the_list_is_dropped()
    {
        var saved = State(Key, false, "thit-ba-chi", "khong-con-nua");

        Desk().Open(saved, TheTray, Sheet()).CheckedIngredientIds.ShouldBe(["thit-ba-chi"]);
    }

    [Fact]
    public void The_ticks_of_another_tray_are_not_shown_but_the_pantry_switch_is_kept()
    {
        var saved = State("2026-10-17|cu-khac", true, "thit-ba-chi");

        var state = Desk().Open(saved, TheTray, Sheet());

        state.CheckedIngredientIds.ShouldBeEmpty();
        state.TrayKey.ShouldBe(Key);
        state.ShowPantry.ShouldBeTrue();
    }

    [Fact]
    public void A_state_that_cannot_be_used_is_the_same_as_none()
    {
        var broken = new ShoppingState(null!, null!, true);

        var state = Desk().Open(broken, TheTray, Sheet());

        state.CheckedIngredientIds.ShouldBeEmpty();
        state.ShowPantry.ShouldBeFalse();   // nothing in it can be trusted, the switch included
    }

    // ---- ticking -----------------------------------------------------------------------------

    [Fact]
    public void Tapping_an_item_ticks_it_and_tapping_again_takes_the_tick_off()
    {
        var desk = Desk();
        var state = State(Key, true);

        var ticked = desk.Toggle(desk.Toggle(state, "ca-loc"), "rau-muong");
        var back = desk.Toggle(ticked, "ca-loc");

        ticked.CheckedIngredientIds.ShouldBe(["ca-loc", "rau-muong"]);
        back.CheckedIngredientIds.ShouldBe(["rau-muong"]);
        back.TrayKey.ShouldBe(Key);
        back.ShowPantry.ShouldBeTrue();
        state.CheckedIngredientIds.ShouldBeEmpty();   // the state it was given is not changed
    }

    [Fact]
    public void An_id_is_compared_exactly()
    {
        var desk = Desk();

        desk.Toggle(State(Key, false, "ca-loc"), "CA-LOC").CheckedIngredientIds.ShouldBe(["ca-loc", "CA-LOC"]);
        desk.Toggle(State(Key, false, "ca-loc", "CA-LOC"), "ca-loc").CheckedIngredientIds.ShouldBe(["CA-LOC"]);   // taking one off leaves the other
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void A_blank_id_is_refused(string id)
    {
        Should.Throw<ArgumentException>(() => Desk().Toggle(State(Key), id));
    }

    [Fact]
    public void The_pantry_switch_changes_only_itself()
    {
        var state = State(Key, false, "ca-loc");

        var on = Desk().SetShowPantry(state, true);

        on.ShowPantry.ShouldBeTrue();
        on.CheckedIngredientIds.ShouldBe(["ca-loc"]);
        on.TrayKey.ShouldBe(Key);
        Desk().SetShowPantry(on, false).ShowPantry.ShouldBeFalse();
    }

    // ---- what is on the screen ---------------------------------------------------------------

    [Fact]
    public void The_pantry_items_are_left_out_of_the_groups_until_the_switch_is_on()
    {
        var sheet = Sheet();

        var hidden = Desk().Groups(sheet, State(Key, false));
        var shown = Desk().Groups(sheet, State(Key, true));

        hidden.SelectMany(g => g.Items).Select(i => i.IngredientId).ShouldNotContain("gao");
        hidden.Select(g => g.Aisle).ShouldBe([Aisle.MeatAndFish, Aisle.Produce, Aisle.EggsAndTofu]);
        shown.SelectMany(g => g.Items).Select(i => i.IngredientId).ShouldContain("gao");
        shown.Select(g => g.Aisle).ShouldBe([Aisle.MeatAndFish, Aisle.Produce, Aisle.EggsAndTofu, Aisle.DryGoodsAndSpices]);
    }

    [Fact]
    public void Progress_counts_the_ticked_items_among_those_on_the_screen()
    {
        var sheet = Sheet();

        var progress = Desk().Progress(sheet, State(Key, false, "thit-ba-chi", "trung"));

        progress.ShouldBe(new ShoppingProgress(2, 6));   // pork, fish, greens, tomato, taro stem, egg
    }

    [Fact]
    public void A_pantry_item_that_is_hidden_counts_neither_in_the_total_nor_in_the_ticks()
    {
        var sheet = Sheet();

        Desk().Progress(sheet, State(Key, false, "gao")).ShouldBe(new ShoppingProgress(0, 6));
        Desk().Progress(sheet, State(Key, true, "gao")).ShouldBe(new ShoppingProgress(1, 7));
    }

    [Fact]
    public void Nothing_to_buy_is_zero_of_zero()
    {
        var sheet = Desk().Sheet(new Household(Region.North, BowlSize.Medium, []), TheTray, Tray, Catalog());

        Desk().Progress(sheet, State(Key)).ShouldBe(new ShoppingProgress(0, 0));
    }

    // ---- the text for Zalo -------------------------------------------------------------------

    [Fact]
    public void The_text_is_the_example_of_the_spec_and_ticks_do_not_change_it()
    {
        var sheet = Sheet();
        const string expected =
            "Đi chợ tối CN 18/10 (4 người)\n"
            + "THỊT CÁ: 3 lạng thịt ba chỉ · 1 con cá lóc (~500g)\n"
            + "RAU: 1 mớ rau muống · 2 quả cà chua · 1 mớ bạc hà\n"
            + "KHÁC: 4 quả trứng";

        Desk().ShareText(sheet, State(Key)).ShouldBe(expected);
        Desk().ShareText(sheet, State(Key, false, "thit-ba-chi", "trung")).ShouldBe(expected);
    }

    [Fact]
    public void The_pantry_items_are_in_the_text_only_when_the_switch_is_on()
    {
        var sheet = Sheet();

        Desk().ShareText(sheet, State(Key, false)).ShouldNotContain("gạo");
        Desk().ShareText(sheet, State(Key, true)).ShouldEndWith("KHÁC: 4 quả trứng · 0,5 kg gạo");
    }

    [Fact]
    public void The_head_count_and_the_day_of_the_text_are_those_of_the_sheet()
    {
        var sheet = Sheet() with { MemberCount = 6, Date = new DateOnly(2026, 11, 3) };

        Desk().ShareText(sheet, State(Key)).ShouldStartWith("Đi chợ tối T3 3/11 (6 người)");
    }

    [Fact]
    public void The_words_of_the_text_can_be_given()
    {
        var english = ShareTextLabels.Vietnamese with { Shopping = "Shop", Meal = "dinner", People = "people" };

        var text = new ShoppingDesk(PortionSettings, english).ShareText(Sheet(), State(Key));

        text.ShouldStartWith("Shop dinner CN 18/10 (4 people)");
    }

    // ---- the state as it is kept -------------------------------------------------------------

    [Fact]
    public void A_state_survives_being_written_and_read_back()
    {
        var state = State(Key, true, "ca-loc", "trung");

        var json = JsonSerializer.Serialize(state, CoreJsonContext.Default.ShoppingState);
        var back = JsonSerializer.Deserialize(json, CoreJsonContext.Default.ShoppingState)!;

        back.TrayKey.ShouldBe(Key);
        back.CheckedIngredientIds.ShouldBe(["ca-loc", "trung"]);
        back.ShowPantry.ShouldBeTrue();
        json.ShouldNotContain("isUsable", Case.Insensitive);
    }

    [Fact]
    public void Only_a_state_with_a_tray_for_its_ticks_and_no_blank_or_repeated_id_can_be_used()
    {
        ShoppingState.Empty.IsUsable.ShouldBeTrue();
        State(Key, true, "a", "b").IsUsable.ShouldBeTrue();
        State(string.Empty, true).IsUsable.ShouldBeTrue();            // the switch alone, no tray yet
        State(string.Empty, false, "a").IsUsable.ShouldBeFalse();     // ticks that belong to nothing
        State(Key, false, "a", " ").IsUsable.ShouldBeFalse();
        State(Key, false, "a", "a").IsUsable.ShouldBeFalse();
        new ShoppingState(null!, [], false).IsUsable.ShouldBeFalse();
        new ShoppingState(Key, null!, false).IsUsable.ShouldBeFalse();
    }

    // ---- bad input ---------------------------------------------------------------------------

    [Fact]
    public void Null_arguments_are_refused()
    {
        var desk = Desk();
        var sheet = Sheet();
        var state = State(Key);

        Should.Throw<ArgumentNullException>(() => new ShoppingDesk(null!));
        Should.Throw<ArgumentNullException>(() => ShoppingDesk.TrayKeyOf(null!));
        Should.Throw<ArgumentNullException>(() => desk.Sheet(null!, TheTray, Tray, Catalog()));
        Should.Throw<ArgumentNullException>(() => desk.Sheet(Family(Region.North), null!, Tray, Catalog()));
        Should.Throw<ArgumentNullException>(() => desk.Open(null, null!, sheet));
        Should.Throw<ArgumentNullException>(() => desk.Open(null, TheTray, null!));
        Should.Throw<ArgumentNullException>(() => desk.Toggle(null!, "a"));
        Should.Throw<ArgumentNullException>(() => desk.Toggle(state, null!));
        Should.Throw<ArgumentNullException>(() => desk.SetShowPantry(null!, true));
        Should.Throw<ArgumentNullException>(() => desk.Groups(null!, state));
        Should.Throw<ArgumentNullException>(() => desk.Groups(sheet, null!));
        Should.Throw<ArgumentNullException>(() => desk.Progress(null!, state));
        Should.Throw<ArgumentNullException>(() => desk.Progress(sheet, null!));
        Should.Throw<ArgumentNullException>(() => desk.ShareText(null!, state));
        Should.Throw<ArgumentNullException>(() => desk.ShareText(sheet, null!));
    }

    [Fact]
    public void A_portion_config_the_formulas_cannot_use_is_refused()
    {
        Should.Throw<ArgumentException>(() => new ShoppingDesk(PortionSettings with { ServingsPerBowl = 0 }));
    }
}
