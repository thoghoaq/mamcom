using AngleSharp.Dom;
using MamCom.Core.Households;
using MamCom.Core.Models;
using MamCom.Core.Portions;
using MamCom.UI.Components;
using Microsoft.AspNetCore.Components;

namespace MamCom.UI.Tests;

public class HouseholdSetupTests : BunitContext
{
    private readonly List<Household> _done = [];
    private readonly List<Household> _skipped = [];

    private IRenderedComponent<HouseholdSetup> Show(Household? initial = null, bool firstRun = true, bool busy = false) =>
        Render<HouseholdSetup>(p => p
            .Add(s => s.Portion, TestConfig.Portion)
            .Add(s => s.Initial, initial)
            .Add(s => s.IsFirstRun, firstRun)
            .Add(s => s.Busy, busy)
            .Add(s => s.OnDone, EventCallback.Factory.Create<Household>(this, _done.Add))
            .Add(s => s.OnSkip, EventCallback.Factory.Create<Household>(this, _skipped.Add)));

    private static IElement AddChip(IRenderedComponent<HouseholdSetup> cut, string role) =>
        cut.FindAll(".chips")[0].QuerySelectorAll("button").First(b => b.TextContent.Trim() == role);

    private static IReadOnlyList<IElement> Cards(IRenderedComponent<HouseholdSetup> cut) => cut.FindAll(".member-card");

    private static IElement Card(IRenderedComponent<HouseholdSetup> cut, int index) => Cards(cut)[index];

    private static IElement StepButton(IRenderedComponent<HouseholdSetup> cut, int card, int direction) =>
        Card(cut, card).QuerySelectorAll(".stepper button")[direction < 0 ? 0 : 1];

    private static string Bowls(IRenderedComponent<HouseholdSetup> cut, int card) => Card(cut, card).QuerySelector(".stepper__value")!.TextContent.Plain();

    private static IElement Start(IRenderedComponent<HouseholdSetup> cut) => cut.Find(".app-actions .button--primary");

    private static IElement Skip(IRenderedComponent<HouseholdSetup> cut) => cut.Find(".app-actions .button--quiet");

    // ---- the empty screen -------------------------------------------------------------------------------------------------

    [Fact]
    public void The_chips_are_the_roles_of_SPEC_in_its_order()
    {
        var cut = Show();

        cut.FindAll(".chips")[0].QuerySelectorAll("button").Select(b => b.TextContent.Trim())
            .ShouldBe(["Bố", "Mẹ", "Ông", "Bà", "Con trai", "Con gái", "Bé"]);
    }

    [Fact]
    public void Nobody_is_there_at_first_so_it_says_so_and_cannot_be_finished_but_can_be_skipped()
    {
        var cut = Show();

        Cards(cut).ShouldBeEmpty();
        cut.Markup.ShouldContain("Chưa có ai. Chạm vào một người ở trên.");
        cut.FindAll(".household-summary").ShouldBeEmpty();
        Start(cut).HasAttribute("disabled").ShouldBeTrue();
        Start(cut).TextContent.ShouldBe("Bắt đầu");
        Skip(cut).TextContent.ShouldBe("Bỏ qua");
        Skip(cut).HasAttribute("disabled").ShouldBeFalse();
    }

    // ---- adding and removing ----------------------------------------------------------------------------------------------

    [Fact]
    public void Tapping_a_chip_adds_a_card_with_the_name_the_age_and_the_usual_bowls()
    {
        var cut = Show();

        AddChip(cut, "Bà").Click();

        var card = Card(cut, 0);
        card.QuerySelector("input")!.GetAttribute("value").ShouldBe("Bà");
        card.QuerySelectorAll("select option").Select(o => o.TextContent).ShouldBe(["2–5 tuổi", "6–11 tuổi", "12–17 tuổi", "18–59 tuổi", "Từ 60 tuổi"]);
        card.QuerySelectorAll("select option").Single(o => o.HasAttribute("selected")).TextContent.ShouldBe("Từ 60 tuổi");
        Bowls(cut, 0).ShouldBe("1,5 chén");
        cut.FindAll(".member-card").Count.ShouldBe(1);
        cut.Markup.ShouldNotContain("Chưa có ai");
        Start(cut).HasAttribute("disabled").ShouldBeFalse();
    }

    [Fact]
    public void The_same_chip_twice_gives_two_people_with_different_names()
    {
        var cut = Show();

        AddChip(cut, "Con trai").Click();
        AddChip(cut, "Con trai").Click();

        Cards(cut).Select(c => c.QuerySelector("input")!.GetAttribute("value")).ShouldBe(["Con trai", "Con trai 2"]);
    }

    [Fact]
    public void A_card_is_removed_with_its_button_which_names_the_person()
    {
        var cut = Show();
        AddChip(cut, "Bố").Click();
        AddChip(cut, "Mẹ").Click();

        var remove = Card(cut, 0).QuerySelectorAll(".member-card__head button")[0];
        remove.GetAttribute("aria-label").ShouldBe("Bỏ Bố");
        remove.Click();

        Cards(cut).Count.ShouldBe(1);
        Card(cut, 0).QuerySelector("input")!.GetAttribute("value").ShouldBe("Mẹ");
    }

    [Fact]
    public void Removing_the_last_person_brings_back_the_empty_screen_and_stops_the_start_button()
    {
        var cut = Show();
        AddChip(cut, "Bố").Click();

        Card(cut, 0).QuerySelectorAll(".member-card__head button")[0].Click();

        Cards(cut).ShouldBeEmpty();
        cut.Markup.ShouldContain("Chưa có ai");
        Start(cut).HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public void A_full_household_cannot_grow_and_says_so()
    {
        var cut = Show();
        for (var i = 0; i < HouseholdEditor.MaxMembers; i++)
        {
            AddChip(cut, "Bé").Click();
        }

        Cards(cut).Count.ShouldBe(HouseholdEditor.MaxMembers);
        cut.FindAll(".chips")[0].QuerySelectorAll("button").ShouldAllBe(b => b.HasAttribute("disabled"));
        cut.Markup.ShouldContain("Nhà đã đủ người");

        Card(cut, 0).QuerySelectorAll(".member-card__head button")[0].Click();

        cut.Markup.ShouldNotContain("Nhà đã đủ người");
        cut.FindAll(".chips")[0].QuerySelectorAll("button").ShouldAllBe(b => !b.HasAttribute("disabled"));
    }

    // ---- the stepper on a card --------------------------------------------------------------------------------------------

    [Fact]
    public void The_stepper_of_a_card_changes_that_persons_bowls_only()
    {
        var cut = Show();
        AddChip(cut, "Bố").Click();
        AddChip(cut, "Mẹ").Click();

        StepButton(cut, 0, +1).Click();
        StepButton(cut, 0, +1).Click();
        StepButton(cut, 1, -1).Click();

        Bowls(cut, 0).ShouldBe("3 chén");
        Bowls(cut, 1).ShouldBe("1,5 chén");
    }

    [Fact]
    public void The_stepper_stops_at_the_smallest_number_of_bowls_and_the_button_is_off()
    {
        var cut = Show();
        AddChip(cut, "Bé").Click();   // 1 bowl

        StepButton(cut, 0, -1).Click();   // 0.5
        StepButton(cut, 0, -1).Click();   // nothing: the smallest is 0.5

        Bowls(cut, 0).ShouldBe("0,5 chén");
        StepButton(cut, 0, -1).HasAttribute("disabled").ShouldBeTrue();
        StepButton(cut, 0, +1).HasAttribute("disabled").ShouldBeFalse();
    }

    [Fact]
    public void The_stepper_stops_at_the_largest_number_of_bowls_and_the_button_is_off()
    {
        var cut = Show();
        AddChip(cut, "Bố").Click();   // 2 bowls
        for (var i = 0; i < 8; i++)
        {
            if (!StepButton(cut, 0, +1).HasAttribute("disabled"))
            {
                StepButton(cut, 0, +1).Click();
            }
        }

        Bowls(cut, 0).ShouldBe("5 chén");
        StepButton(cut, 0, +1).HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public void The_buttons_of_a_stepper_use_the_step_of_the_config_and_name_the_person()
    {
        var cut = Show();
        AddChip(cut, "Mẹ").Click();

        StepButton(cut, 0, -1).GetAttribute("aria-label").ShouldBe("Bớt 0,5 chén của Mẹ");
        StepButton(cut, 0, +1).GetAttribute("aria-label").ShouldBe("Thêm 0,5 chén cho Mẹ");
    }

    // ---- age, name --------------------------------------------------------------------------------------------------------

    [Fact]
    public void Another_age_group_moves_the_usual_bowls_along()
    {
        var cut = Show();
        AddChip(cut, "Con trai").Click();   // child, 1.5

        Card(cut, 0).QuerySelector("select")!.Change("Teen");

        Bowls(cut, 0).ShouldBe("2,5 chén");
        Card(cut, 0).QuerySelectorAll("select option").Single(o => o.HasAttribute("selected")).TextContent.ShouldBe("12–17 tuổi");
    }

    [Fact]
    public void Bowls_the_person_chose_are_not_moved_by_the_age_group()
    {
        var cut = Show();
        AddChip(cut, "Con trai").Click();
        StepButton(cut, 0, +1).Click();   // 2 bowls: not the 1.5 of a child

        Card(cut, 0).QuerySelector("select")!.Change("Teen");

        Bowls(cut, 0).ShouldBe("2 chén");
    }

    [Fact]
    public void A_value_that_is_not_an_age_group_changes_nothing()
    {
        var cut = Show();
        AddChip(cut, "Bố").Click();

        Card(cut, 0).QuerySelector("select")!.Change("Martian");

        Card(cut, 0).QuerySelectorAll("select option").Single(o => o.HasAttribute("selected")).TextContent.ShouldBe("18–59 tuổi");
    }

    [Fact]
    public void A_person_is_renamed_in_the_box_of_the_card_and_the_buttons_follow()
    {
        var cut = Show();
        AddChip(cut, "Con trai").Click();

        Card(cut, 0).QuerySelector("input")!.Change("Con 10 tuổi");

        Card(cut, 0).QuerySelector("input")!.GetAttribute("value").ShouldBe("Con 10 tuổi");
        StepButton(cut, 0, +1).GetAttribute("aria-label").ShouldBe("Thêm 0,5 chén cho Con 10 tuổi");
        Card(cut, 0).QuerySelectorAll(".member-card__head button")[0].GetAttribute("aria-label").ShouldBe("Bỏ Con 10 tuổi");
    }

    [Theory]
    [InlineData("")]
    [InlineData("    ")]
    public void A_blank_name_is_put_back_and_the_box_shows_the_old_name_again(string typed)
    {
        var cut = Show();
        AddChip(cut, "Bố").Click();

        Card(cut, 0).QuerySelector("input")!.Change(typed);

        Card(cut, 0).QuerySelector("input")!.GetAttribute("value").ShouldBe("Bố");
    }

    [Fact]
    public void A_name_another_person_has_gets_a_number_and_the_box_shows_it()
    {
        var cut = Show();
        AddChip(cut, "Bố").Click();
        AddChip(cut, "Mẹ").Click();

        Card(cut, 1).QuerySelector("input")!.Change("Bố");

        Card(cut, 1).QuerySelector("input")!.GetAttribute("value").ShouldBe("Bố 2");
    }

    [Fact]
    public void A_name_with_spaces_around_it_is_shown_without_them()
    {
        var cut = Show();
        AddChip(cut, "Bố").Click();

        Card(cut, 0).QuerySelector("input")!.Change("  Ba  ");

        Card(cut, 0).QuerySelector("input")!.GetAttribute("value").ShouldBe("Ba");
    }

    [Fact]
    public void The_box_of_a_name_takes_no_more_than_the_longest_name()
    {
        var cut = Show();
        AddChip(cut, "Bố").Click();

        Card(cut, 0).QuerySelector("input")!.GetAttribute("maxlength").ShouldBe(HouseholdEditor.MaxNameLength.ToString());
    }

    [Fact]
    public void The_boxes_of_names_are_numbered_from_one_for_a_screen_reader()
    {
        var cut = Show();
        AddChip(cut, "Bố").Click();
        AddChip(cut, "Mẹ").Click();

        Cards(cut).Select(c => c.QuerySelector("input")!.GetAttribute("aria-label")).ShouldBe(["Tên của người thứ 1", "Tên của người thứ 2"]);
    }

    [Fact]
    public void The_summary_is_announced_when_it_changes()
    {
        var cut = Show();
        AddChip(cut, "Bố").Click();

        cut.Find(".household-summary").GetAttribute("role").ShouldBe("status");
    }

    // ---- the summary ------------------------------------------------------------------------------------------------------

    [Fact]
    public void The_summary_says_how_many_servings_and_how_much_rice_the_household_needs()
    {
        var cut = Show();
        AddChip(cut, "Bố").Click();
        AddChip(cut, "Mẹ").Click();

        // two adults with 2 bowls each: 2 × (0.4 + 0.6) = 2 servings; 4 bowls × 40 g = 160 g
        cut.Find(".household-summary").TextContent.Plain().ShouldBe("Mỗi bữa nấu cho ≈ 2 suất, gạo ≈ 160 g.");
    }

    [Fact]
    public void The_summary_follows_the_stepper()
    {
        var cut = Show();
        AddChip(cut, "Bố").Click();
        StepButton(cut, 0, +1).Click();   // 2.5 bowls: 0.4 + 0.75 = 1.15 servings; 100 g rice
        StepButton(cut, 0, +1).Click();   // 3 bowls: 1.3 servings; 120 g

        cut.Find(".household-summary").TextContent.Plain().ShouldBe("Mỗi bữa nấu cho ≈ 1,3 suất, gạo ≈ 120 g.");
    }

    [Fact]
    public void A_number_and_its_unit_stay_on_one_line()
    {
        var cut = Show();
        AddChip(cut, "Bố").Click();
        AddChip(cut, "Mẹ").Click();

        Card(cut, 0).QuerySelector(".stepper__value")!.TextContent.ShouldBe("2 chén");
        cut.Find(".household-summary").TextContent.ShouldBe("Mỗi bữa nấu cho ≈ 2 suất, gạo ≈ 160 g.");
    }

    // ---- the four-person family of SPEC §4.2, made with taps --------------------------------------------------------------

    [Fact]
    public void The_four_person_family_of_SPEC_is_made_in_a_handful_of_taps_and_gets_the_servings_and_rice_SPEC_gives()
    {
        var cut = Show();
        var taps = 0;

        AddChip(cut, "Bố").Click(); taps++;
        AddChip(cut, "Mẹ").Click(); taps++;
        AddChip(cut, "Con trai").Click(); taps++;
        AddChip(cut, "Bà").Click(); taps++;
        StepButton(cut, 0, +1).Click(); taps++;
        StepButton(cut, 0, +1).Click(); taps++;   // Bố eats 3 bowls; the others have the usual number
        Card(cut, 2).QuerySelector("input")!.Change("Con 10 tuổi"); taps++;
        cut.Find(".household-summary").TextContent.Plain().ShouldBe("Mỗi bữa nấu cho ≈ 3,8 suất, gạo ≈ 320 g.");
        Start(cut).Click(); taps++;

        var household = _done.ShouldHaveSingleItem();
        var result = new PortionCalculator(TestConfig.Portion).Calculate(household);
        result.BaseServings.ShouldBe(3.79, 0.01);
        result.RawRiceGrams.ShouldBe(320);
        household.Members.Select(m => m.DisplayName).ShouldBe(["Bố", "Mẹ", "Con 10 tuổi", "Bà"]);
        taps.ShouldBeLessThanOrEqualTo(10);   // far inside 60 seconds for a person who knows their family
        _skipped.ShouldBeEmpty();
    }

    // ---- the region -------------------------------------------------------------------------------------------------------

    [Fact]
    public void The_region_is_asked_in_one_row_of_three_and_starts_in_the_north()
    {
        var cut = Show();

        var radios = cut.FindAll("[role=radiogroup] button");
        radios.Select(r => r.TextContent.Trim()).ShouldBe(["Bắc", "Trung", "Nam"]);
        radios.Select(r => r.GetAttribute("aria-checked")).ShouldBe(["true", "false", "false"]);
        cut.Find("[role=radiogroup]").GetAttribute("aria-labelledby").ShouldBe("household-region-label");
        cut.Find("#household-region-label").TextContent.ShouldBe("Nhà mình ở miền nào?");
    }

    [Fact]
    public void Choosing_a_region_checks_that_one_only_and_goes_into_the_household()
    {
        var cut = Show();
        AddChip(cut, "Bố").Click();

        cut.FindAll("[role=radiogroup] button")[2].Click();

        cut.FindAll("[role=radiogroup] button").Select(r => r.GetAttribute("aria-checked")).ShouldBe(["false", "false", "true"]);
        Start(cut).Click();
        _done.Single().Region.ShouldBe(Region.South);
    }

    // ---- finishing and skipping -------------------------------------------------------------------------------------------

    [Fact]
    public void Finishing_hands_over_the_household_with_the_default_bowl_size_and_nothing_to_avoid()
    {
        var cut = Show();
        AddChip(cut, "Bố").Click();
        AddChip(cut, "Bé").Click();

        Start(cut).Click();

        var household = _done.Single();
        household.Region.ShouldBe(Region.North);
        household.BowlSize.ShouldBe(BowlSize.Medium);
        household.Members.Select(m => (m.DisplayName, m.AgeGroup, m.BowlsPerMeal)).ShouldBe([("Bố", AgeGroup.Adult, 2), ("Bé", AgeGroup.Toddler, 1)]);
        household.Members.ShouldAllBe(m => m.Tags.Count == 0 && m.Avoid.Count == 0);
    }

    [Fact]
    public void Skipping_hands_over_two_grown_ups_with_the_usual_bowls_and_the_region_that_was_chosen()
    {
        var cut = Show();
        cut.FindAll("[role=radiogroup] button")[1].Click();   // Trung

        Skip(cut).Click();

        var household = _skipped.ShouldHaveSingleItem();
        household.Region.ShouldBe(Region.Central);
        household.BowlSize.ShouldBe(BowlSize.Medium);
        household.Members.Select(m => (m.DisplayName, m.AgeGroup, m.BowlsPerMeal)).ShouldBe([("Bố", AgeGroup.Adult, 2), ("Mẹ", AgeGroup.Adult, 2)]);
        _done.ShouldBeEmpty();
    }

    [Fact]
    public void Skipping_keeps_the_region_and_bowl_size_the_household_already_had()
    {
        var cut = Show(Saved(), firstRun: true);   // South and large bowls

        Skip(cut).Click();

        var household = _skipped.Single();
        household.Region.ShouldBe(Region.South);
        household.BowlSize.ShouldBe(BowlSize.Large);
    }

    [Fact]
    public void Skipping_ignores_the_people_already_added()
    {
        var cut = Show();
        AddChip(cut, "Bà").Click();

        Skip(cut).Click();

        _skipped.Single().Members.Select(m => m.DisplayName).ShouldBe(["Bố", "Mẹ"]);
    }

    [Fact]
    public void The_first_time_says_what_skipping_does()
    {
        Show().Markup.ShouldContain("Bỏ qua thì app tính cho hai người lớn. Sửa lại được sau.");
    }

    [Fact]
    public void The_start_button_does_nothing_when_nobody_was_added()
    {
        var cut = Show();

        Start(cut).Click();

        _done.ShouldBeEmpty();
    }

    // ---- changing a household that was saved ------------------------------------------------------------------------------

    private static Household Saved() => new(
        Region.South,
        BowlSize.Large,
        [
            new Member("Ba", AgeGroup.Senior, 3, [MemberTag.MoreVeg], ["hai-san"]),
            new Member("Mẹ", AgeGroup.Adult, 2, [], []),
        ]);

    [Fact]
    public void A_saved_household_is_shown_as_it_was_with_a_save_button_and_no_skip()
    {
        var cut = Show(Saved(), firstRun: false);

        Cards(cut).Select(c => c.QuerySelector("input")!.GetAttribute("value")).ShouldBe(["Ba", "Mẹ"]);
        Bowls(cut, 0).ShouldBe("3 chén");
        cut.FindAll("[role=radiogroup] button").Select(r => r.GetAttribute("aria-checked")).ShouldBe(["false", "false", "true"]);
        Start(cut).TextContent.ShouldBe("Lưu");
        cut.FindAll(".button--quiet").Where(b => b.TextContent == "Bỏ qua").ShouldBeEmpty();
        cut.Markup.ShouldNotContain("Bỏ qua thì app");
    }

    [Fact]
    public void Saving_a_changed_household_keeps_what_the_screen_does_not_ask_about()
    {
        var cut = Show(Saved(), firstRun: false);
        AddChip(cut, "Bé").Click();

        Start(cut).Click();

        var household = _done.Single();
        household.Region.ShouldBe(Region.South);
        household.BowlSize.ShouldBe(BowlSize.Large);
        household.Members.Count.ShouldBe(3);
        household.Members[0].Tags.ShouldBe([MemberTag.MoreVeg]);
        household.Members[0].Avoid.ShouldBe(["hai-san"]);
    }

    [Fact]
    public void A_saved_household_that_cannot_be_used_starts_the_screen_empty()
    {
        var broken = new Household(Region.North, BowlSize.Medium, [new Member("Bố", AgeGroup.Adult, 99, [], [])]);

        var cut = Show(broken);

        Cards(cut).ShouldBeEmpty();
    }

    // ---- while it is being saved ------------------------------------------------------------------------------------------

    [Fact]
    public void While_it_is_being_saved_neither_button_works()
    {
        var cut = Show(busy: true);
        AddChip(cut, "Bố").Click();

        Start(cut).HasAttribute("disabled").ShouldBeTrue();
        Skip(cut).HasAttribute("disabled").ShouldBeTrue();
    }

    // ---- everything can be reached ----------------------------------------------------------------------------------------

    [Fact]
    public void Every_button_has_a_name_and_every_field_has_a_label()
    {
        var cut = Show();
        AddChip(cut, "Bố").Click();
        AddChip(cut, "Bé").Click();

        foreach (var button in cut.FindAll("button"))
        {
            var name = button.GetAttribute("aria-label") ?? button.TextContent.Trim();
            name.ShouldNotBeNullOrWhiteSpace(button.OuterHtml);
        }

        foreach (var field in cut.FindAll("input, select"))
        {
            var id = field.GetAttribute("id");
            var labelled = field.HasAttribute("aria-label") || (id is not null && cut.FindAll($"label[for='{id}']").Count == 1);
            labelled.ShouldBeTrue(field.OuterHtml);
        }
    }

    [Fact]
    public void The_ids_of_the_cards_do_not_repeat_so_each_label_belongs_to_its_own_select()
    {
        var cut = Show();
        AddChip(cut, "Bố").Click();
        AddChip(cut, "Mẹ").Click();

        var ids = cut.FindAll("select").Select(s => s.GetAttribute("id")).ToArray();

        ids.Distinct().Count().ShouldBe(2);
    }

    [Fact]
    public void The_group_of_chips_is_named_and_the_chips_are_buttons_that_do_not_submit_anything()
    {
        var cut = Show();

        cut.FindAll("[role=group]")[0].GetAttribute("aria-labelledby").ShouldBe("household-add-label");
        cut.Find("#household-add-label").TextContent.ShouldBe("Thêm người");
        cut.FindAll("button").ShouldAllBe(b => b.GetAttribute("type") == "button");
    }
}
