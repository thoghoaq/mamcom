using MamCom.Core.Households;
using MamCom.Core.Models;
using MamCom.Core.Portions;

namespace MamCom.Core.Tests.Households;

public class HouseholdEditorTests
{
    private static readonly PortionConfig Config = PortionConfig.Parse(TestFiles.Config("portion-defaults.json"));

    private static HouseholdEditor Editor() => new(Config);

    private static string Name(MemberRole role) => role switch
    {
        MemberRole.Father => "Bố",
        MemberRole.Mother => "Mẹ",
        MemberRole.Grandfather => "Ông",
        MemberRole.Grandmother => "Bà",
        MemberRole.Son => "Con trai",
        MemberRole.Daughter => "Con gái",
        _ => "Bé",
    };

    private static DraftMember AddFather(HouseholdEditor editor) => editor.Add(MemberRole.Father, "Bố")!;

    // ---- roles ------------------------------------------------------------------------------------------------------------

    [Fact]
    public void The_chips_come_in_the_order_of_SPEC_and_none_is_missing()
    {
        MemberRoles.InChipOrder.ShouldBe(
            [MemberRole.Father, MemberRole.Mother, MemberRole.Grandfather, MemberRole.Grandmother, MemberRole.Son, MemberRole.Daughter, MemberRole.Baby]);
        MemberRoles.InChipOrder.Order().ShouldBe(Enum.GetValues<MemberRole>().Order());
    }

    [Theory]
    [InlineData(MemberRole.Father, AgeGroup.Adult)]
    [InlineData(MemberRole.Mother, AgeGroup.Adult)]
    [InlineData(MemberRole.Grandfather, AgeGroup.Senior)]
    [InlineData(MemberRole.Grandmother, AgeGroup.Senior)]
    [InlineData(MemberRole.Son, AgeGroup.Child)]
    [InlineData(MemberRole.Daughter, AgeGroup.Child)]
    [InlineData(MemberRole.Baby, AgeGroup.Toddler)]
    public void A_role_starts_with_the_age_group_that_fits_it(MemberRole role, AgeGroup expected)
    {
        role.DefaultAgeGroup().ShouldBe(expected);
    }

    [Fact]
    public void A_role_that_does_not_exist_is_refused()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => ((MemberRole)99).DefaultAgeGroup());
    }

    // ---- adding and removing ----------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(MemberRole.Father, 2)]
    [InlineData(MemberRole.Grandmother, 1.5)]
    [InlineData(MemberRole.Son, 1.5)]
    [InlineData(MemberRole.Baby, 1)]
    public void A_new_person_starts_with_the_usual_bowls_of_the_age_group_from_the_config(MemberRole role, double bowls)
    {
        var member = Editor().Add(role, Name(role))!;

        member.BowlsPerMeal.ShouldBe(bowls);
        member.AgeGroup.ShouldBe(role.DefaultAgeGroup());
        member.Name.ShouldBe(Name(role));
        member.Tags.ShouldBeEmpty();
        member.Avoid.ShouldBeEmpty();
    }

    [Fact]
    public void The_usual_bowls_come_from_the_config_and_not_from_the_code()
    {
        var config = Config with { DefaultBowlsPerMeal = new AgeGroupValues(1, 1, 1, 3, 1) };

        new HouseholdEditor(config).Add(MemberRole.Father, "Bố")!.BowlsPerMeal.ShouldBe(3);
    }

    [Fact]
    public void A_new_household_is_northern_with_medium_bowls_and_has_nobody()
    {
        var editor = Editor();

        editor.Region.ShouldBe(Region.North);
        editor.BowlSize.ShouldBe(BowlSize.Medium);
        editor.Members.ShouldBeEmpty();
        editor.IsComplete.ShouldBeFalse();
        editor.CanAdd.ShouldBeTrue();
    }

    [Fact]
    public void A_name_that_is_taken_gets_a_number_and_a_freed_name_can_be_used_again()
    {
        var editor = Editor();
        var first = editor.Add(MemberRole.Son, "Con trai")!;
        var second = editor.Add(MemberRole.Son, "Con trai")!;
        var third = editor.Add(MemberRole.Son, "con TRAI")!;   // the same name in other letters

        first.Name.ShouldBe("Con trai");
        second.Name.ShouldBe("Con trai 2");
        third.Name.ShouldBe("con TRAI 3");

        editor.Remove(second.Id);
        editor.Add(MemberRole.Son, "Con trai")!.Name.ShouldBe("Con trai 2");
    }

    [Fact]
    public void A_name_is_trimmed_and_cut_to_the_longest_a_card_takes()
    {
        var editor = Editor();

        editor.Add(MemberRole.Father, "  Bố  ")!.Name.ShouldBe("Bố");
        var long30 = editor.Add(MemberRole.Mother, new string('a', 45))!;

        long30.Name.ShouldBe(new string('a', HouseholdEditor.MaxNameLength));
    }

    [Fact]
    public void A_numbered_name_still_fits_in_the_longest_a_card_takes()
    {
        var editor = Editor();
        var name = new string('b', HouseholdEditor.MaxNameLength);
        editor.Add(MemberRole.Father, name);

        var second = editor.Add(MemberRole.Father, name)!;

        second.Name.Length.ShouldBeLessThanOrEqualTo(HouseholdEditor.MaxNameLength);
        second.Name.ShouldEndWith(" 2");
        second.Name.ShouldNotBe(name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_name_is_refused(string name)
    {
        Should.Throw<ArgumentException>(() => Editor().Add(MemberRole.Father, name));
    }

    [Fact]
    public void People_have_ids_of_their_own_that_are_never_given_again()
    {
        var editor = Editor();
        var a = AddFather(editor);
        var b = AddFather(editor);
        editor.Remove(b.Id).ShouldBeTrue();
        var c = AddFather(editor);

        new[] { a.Id, b.Id, c.Id }.Distinct().Count().ShouldBe(3);
        editor.Remove(b.Id).ShouldBeFalse();   // already gone
        editor.Members.Select(m => m.Id).ShouldBe([a.Id, c.Id]);
    }

    [Fact]
    public void The_household_stops_growing_at_the_limit()
    {
        var editor = Editor();
        for (var i = 0; i < HouseholdEditor.MaxMembers; i++)
        {
            editor.Add(MemberRole.Baby, "Bé").ShouldNotBeNull();
        }

        editor.CanAdd.ShouldBeFalse();
        editor.Add(MemberRole.Baby, "Bé").ShouldBeNull();
        editor.Members.Count.ShouldBe(HouseholdEditor.MaxMembers);

        editor.Remove(editor.Members[0].Id);
        editor.CanAdd.ShouldBeTrue();
    }

    // ---- bowls ------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Stepping_changes_the_bowls_by_the_step_of_the_config()
    {
        var editor = Editor();
        var id = AddFather(editor).Id;

        editor.StepBowls(id, +1).ShouldBe(2.5);
        editor.StepBowls(id, -1).ShouldBe(2);
        editor.StepBowls(id, +3).ShouldBe(3.5);
        editor.Members[0].BowlsPerMeal.ShouldBe(3.5);
    }

    [Fact]
    public void The_step_is_the_one_in_the_config()
    {
        var editor = new HouseholdEditor(Config with { BowlsStep = 0.25 });
        var id = editor.Add(MemberRole.Father, "Bố")!.Id;

        editor.StepBowls(id, +1).ShouldBe(2.25);
    }

    [Fact]
    public void Stepping_stops_at_the_smallest_and_the_largest_number_of_bowls()
    {
        var editor = Editor();
        var id = AddFather(editor).Id;

        editor.StepBowls(id, -100).ShouldBe(Config.MinBowlsPerMeal);
        editor.CanStep(id, -1).ShouldBeFalse();
        editor.CanStep(id, +1).ShouldBeTrue();

        editor.StepBowls(id, +100).ShouldBe(Config.MaxBowlsPerMeal);
        editor.CanStep(id, +1).ShouldBeFalse();
        editor.CanStep(id, -1).ShouldBeTrue();
    }

    [Fact]
    public void A_largest_number_of_bowls_that_is_not_on_the_grid_is_never_passed()
    {
        var config = Config with
        {
            MinBowlsPerMeal = 0.5,
            MaxBowlsPerMeal = 3.4,
            BowlsStep = 1,
            DefaultBowlsPerMeal = new AgeGroupValues(1.5, 1.5, 1.5, 1.5, 1.5),
        };
        var editor = new HouseholdEditor(config);
        var id = editor.Add(MemberRole.Father, "Bố")!.Id;

        editor.StepBowls(id, +2).ShouldBe(3.4);   // 3.5 is the next point on the grid, and it is above the largest
        editor.CanStep(id, +1).ShouldBeFalse();
    }

    [Fact]
    public void Going_up_and_down_many_times_does_not_drift_off_the_grid()
    {
        var editor = Editor();
        var id = AddFather(editor).Id;

        for (var i = 0; i < 200; i++)
        {
            editor.StepBowls(id, i % 3 == 0 ? +1 : -1);
        }

        var bowls = editor.Members[0].BowlsPerMeal;
        ((bowls - Config.MinBowlsPerMeal) / Config.BowlsStep).ShouldBe(Math.Round((bowls - Config.MinBowlsPerMeal) / Config.BowlsStep), 1e-12);
        bowls.ShouldBeInRange(Config.MinBowlsPerMeal, Config.MaxBowlsPerMeal);
    }

    [Fact]
    public void Stepping_or_asking_about_someone_who_is_not_there_does_nothing()
    {
        var editor = Editor();

        editor.StepBowls(42, +1).ShouldBeNull();
        editor.CanStep(42, +1).ShouldBeFalse();
        editor.CanStep(42, -1).ShouldBeFalse();
    }

    // ---- age group and name -----------------------------------------------------------------------------------------------

    [Fact]
    public void Bowls_nobody_touched_follow_the_new_age_group()
    {
        var editor = Editor();
        var id = editor.Add(MemberRole.Son, "Con trai")!.Id;   // child, 1.5

        editor.SetAgeGroup(id, AgeGroup.Teen).ShouldBeTrue();

        editor.Members[0].AgeGroup.ShouldBe(AgeGroup.Teen);
        editor.Members[0].BowlsPerMeal.ShouldBe(2.5);
    }

    [Fact]
    public void Bowls_the_person_chose_stay_when_the_age_group_changes()
    {
        var editor = Editor();
        var id = editor.Add(MemberRole.Son, "Con trai")!.Id;
        editor.StepBowls(id, +3);   // 3, not the 1.5 of a child

        editor.SetAgeGroup(id, AgeGroup.Teen);

        editor.Members[0].BowlsPerMeal.ShouldBe(3);
    }

    [Fact]
    public void An_age_group_that_does_not_exist_or_a_person_who_is_not_there_changes_nothing()
    {
        var editor = Editor();
        var id = AddFather(editor).Id;

        editor.SetAgeGroup(id, (AgeGroup)99).ShouldBeFalse();
        editor.SetAgeGroup(42, AgeGroup.Teen).ShouldBeFalse();

        editor.Members[0].AgeGroup.ShouldBe(AgeGroup.Adult);
    }

    [Fact]
    public void Renaming_trims_the_name_and_blank_text_keeps_the_old_one()
    {
        var editor = Editor();
        var id = AddFather(editor).Id;

        editor.Rename(id, "  Ba  ").ShouldBeTrue();
        editor.Members[0].Name.ShouldBe("Ba");

        editor.Rename(id, "   ").ShouldBeFalse();
        editor.Rename(id, null).ShouldBeFalse();
        editor.Members[0].Name.ShouldBe("Ba");
        editor.Rename(42, "Ai").ShouldBeFalse();
    }

    [Fact]
    public void A_person_may_keep_their_own_name_but_not_take_another_persons()
    {
        var editor = Editor();
        var father = AddFather(editor);
        var mother = editor.Add(MemberRole.Mother, "Mẹ")!;

        editor.Rename(father.Id, "Bố");
        editor.Members[0].Name.ShouldBe("Bố");

        editor.Rename(mother.Id, "bố");
        editor.Members[1].Name.ShouldBe("bố 2");
    }

    // ---- the household that comes out -------------------------------------------------------------------------------------

    [Fact]
    public void Nothing_comes_out_until_someone_is_added()
    {
        Should.Throw<InvalidOperationException>(() => Editor().ToHousehold());
    }

    [Fact]
    public void The_household_has_the_region_the_bowl_size_and_everyone_in_order()
    {
        var editor = Editor();
        editor.Region = Region.South;
        editor.BowlSize = BowlSize.Large;
        editor.Add(MemberRole.Father, "Bố");
        editor.Add(MemberRole.Baby, "Bé");

        var household = editor.ToHousehold();

        household.Region.ShouldBe(Region.South);
        household.BowlSize.ShouldBe(BowlSize.Large);
        household.AvailableMinutes.ShouldBeNull();   // asked for at each spin, not kept
        household.Members.Select(m => (m.DisplayName, m.AgeGroup, m.BowlsPerMeal)).ShouldBe(
            [("Bố", AgeGroup.Adult, 2), ("Bé", AgeGroup.Toddler, 1)]);
        household.Members.ShouldAllBe(m => m.Tags.Count == 0 && m.Avoid.Count == 0);
    }

    [Fact]
    public void The_four_person_family_of_SPEC_made_with_a_few_taps_gets_the_servings_and_rice_SPEC_gives()
    {
        var editor = Editor();
        var father = editor.Add(MemberRole.Father, "Bố")!;
        editor.Add(MemberRole.Mother, "Mẹ");
        var child = editor.Add(MemberRole.Son, "Con 10 tuổi")!;
        editor.Add(MemberRole.Grandmother, "Bà");
        editor.StepBowls(father.Id, +2);   // 3 bowls; everyone else has the usual number

        var result = new PortionCalculator(Config).Calculate(editor.ToHousehold());

        child.AgeGroup.ShouldBe(AgeGroup.Child);
        result.BaseServings.ShouldBe(3.79, 0.01);
        result.RawRiceGrams.ShouldBe(320);
    }

    // ---- skipping ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void Skipping_starts_from_a_father_and_a_mother_with_the_usual_bowls()
    {
        var editor = HouseholdEditor.Default(Config, Name);

        editor.Members.Select(m => (m.Name, m.AgeGroup, m.BowlsPerMeal)).ShouldBe([("Bố", AgeGroup.Adult, 2), ("Mẹ", AgeGroup.Adult, 2)]);
        editor.Region.ShouldBe(Region.North);
        editor.BowlSize.ShouldBe(BowlSize.Medium);
        editor.IsComplete.ShouldBeTrue();
        new PortionCalculator(Config).Calculate(editor.ToHousehold()).RawRiceGrams.ShouldBe(160);
    }

    // ---- a household that was saved ---------------------------------------------------------------------------------------

    private static Household Saved(params Member[] members) => new(Region.Central, BowlSize.Small, members);

    private static Member Person(string name = "Bố", double bowls = 2, AgeGroup age = AgeGroup.Adult) => new(name, age, bowls, [MemberTag.MoreVeg], ["hai-san"]);

    [Fact]
    public void A_saved_household_comes_back_with_its_tags_and_what_it_avoids()
    {
        var editor = HouseholdEditor.From(Saved(Person("Bố", 3), Person("Mẹ")), Config);

        editor.ShouldNotBeNull();
        editor.Region.ShouldBe(Region.Central);
        editor.BowlSize.ShouldBe(BowlSize.Small);
        editor.Members.Select(m => (m.Name, m.BowlsPerMeal)).ShouldBe([("Bố", 3), ("Mẹ", 2)]);

        var back = editor.ToHousehold();
        back.Members.ShouldAllBe(m => m.Tags.SequenceEqual(new[] { MemberTag.MoreVeg }) && m.Avoid.SequenceEqual(new[] { "hai-san" }));
    }

    [Fact]
    public void A_saved_name_with_spaces_around_it_comes_back_without_them()
    {
        var editor = HouseholdEditor.From(Saved(Person("  Bố  ")), Config)!;

        editor.Members[0].Name.ShouldBe("Bố");
    }

    [Fact]
    public void A_saved_household_can_be_edited_and_new_people_get_new_ids()
    {
        var editor = HouseholdEditor.From(Saved(Person("Bố"), Person("Mẹ")), Config)!;

        var added = editor.Add(MemberRole.Baby, "Bé")!;

        editor.Members.Select(m => m.Id).Distinct().Count().ShouldBe(3);
        added.Id.ShouldBeGreaterThan(editor.Members[1].Id);
        editor.Members[0].Name.ShouldBe("Bố");
    }

    [Theory]
    [MemberData(nameof(Unusable))]
    public void A_saved_household_that_is_not_usable_is_not_loaded(Household household)
    {
        HouseholdEditor.IsUsable(household, Config).ShouldBeFalse();
        HouseholdEditor.From(household, Config).ShouldBeNull();
    }

    public static TheoryData<Household> Unusable() => new()
    {
        Saved(),
        new Household(Region.North, BowlSize.Medium, [.. Enumerable.Range(0, HouseholdEditor.MaxMembers + 1).Select(i => Person("P" + i))]),
        Saved(Person(bowls: 0)),
        Saved(Person(bowls: 0.4)),
        Saved(Person(bowls: 5.5)),
        Saved(Person(bowls: double.NaN)),
        Saved(Person(bowls: double.PositiveInfinity)),
        Saved(Person(name: "")),
        Saved(Person(name: "   ")),
        Saved(Person(age: (AgeGroup)99)),
        new Household((Region)99, BowlSize.Medium, [Person()]),
        new Household(Region.North, (BowlSize)99, [Person()]),
        Saved(new Member("Bố", AgeGroup.Adult, 2, null!, [])),
        Saved(new Member("Bố", AgeGroup.Adult, 2, [], null!)),
    };

    [Fact]
    public void A_saved_household_at_the_edges_of_what_is_allowed_is_usable()
    {
        var edges = Saved(
            Person("Nhỏ nhất", Config.MinBowlsPerMeal),
            Person("Lớn nhất", Config.MaxBowlsPerMeal));
        var full = new Household(Region.South, BowlSize.Large, [.. Enumerable.Range(0, HouseholdEditor.MaxMembers).Select(i => Person("P" + i))]);

        HouseholdEditor.IsUsable(edges, Config).ShouldBeTrue();
        HouseholdEditor.IsUsable(full, Config).ShouldBeTrue();
    }

    [Fact]
    public void The_arguments_are_checked()
    {
        Should.Throw<ArgumentNullException>(() => new HouseholdEditor(null!));
        Should.Throw<ArgumentException>(() => new HouseholdEditor(Config with { BowlsStep = 0 }));
        Should.Throw<ArgumentNullException>(() => HouseholdEditor.From(null!, Config));
        Should.Throw<ArgumentNullException>(() => HouseholdEditor.IsUsable(Saved(Person()), null!));
        Should.Throw<ArgumentNullException>(() => HouseholdEditor.IsUsable(null!, Config));
    }
}
