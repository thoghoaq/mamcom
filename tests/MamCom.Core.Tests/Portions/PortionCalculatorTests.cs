using MamCom.Core.Models;
using MamCom.Core.Portions;

namespace MamCom.Core.Tests.Portions;

public class PortionCalculatorTests
{
    private const double Tolerance = 1e-9;

    private static readonly PortionConfig Config = PortionConfig.Parse(TestFiles.Config("portion-defaults.json"));

    private static PortionCalculator Calculator() => new(Config);

    private static Member Person(string name, AgeGroup age, double bowls, params MemberTag[] tags) =>
        new(name, age, bowls, tags, []);

    private static Household Home(BowlSize size, params Member[] members) =>
        new(Region.South, size, members);

    // The four-person example from SPEC §4.2, medium bowls.
    private static Member Father => Person("Bố", AgeGroup.Adult, 3);

    private static Member Mother => Person("Mẹ", AgeGroup.Adult, 2);

    private static Member Child => Person("Con 10 tuổi", AgeGroup.Child, 1.5);

    private static Member Grandma => Person("Bà", AgeGroup.Senior, 1.5);

    // ---- SPEC §4.2 example -------------------------------------------------------------------

    [Fact]
    public void Sample_family_gets_the_servings_and_rice_SPEC_gives()
    {
        var result = Calculator().Calculate(Home(BowlSize.Medium, Father, Mother, Child, Grandma));

        // (0.4 + 0.3 × 3) × 1 = 1.3; (0.4 + 0.3 × 2) × 1 = 1.0; 0.85 × 0.85 = 0.7225; 0.85 × 0.9 = 0.765
        result.MemberServings.Count.ShouldBe(4);
        result.MemberServings[0].ShouldBe(1.3, Tolerance);
        result.MemberServings[1].ShouldBe(1.0, Tolerance);
        result.MemberServings[2].ShouldBe(0.7225, Tolerance);
        result.MemberServings[3].ShouldBe(0.765, Tolerance);

        result.BaseServings.ShouldBe(3.7875, Tolerance);
        result.BaseServings.ShouldBe(3.79, 0.01); // SPEC: "3,79 ± 0,01"
        result.ProteinServings.ShouldBe(result.BaseServings, Tolerance);
        result.VegServings.ShouldBe(result.BaseServings, Tolerance);
        result.TotalBowls.ShouldBe(8);
        result.RawRiceGrams.ShouldBe(320); // 8 bowls × 40 g
    }

    // ---- tags --------------------------------------------------------------------------------

    [Fact]
    public void More_protein_raises_only_the_protein_total()
    {
        var home = Home(BowlSize.Medium, Person("Bố", AgeGroup.Adult, 3, MemberTag.MoreProtein), Mother, Child, Grandma);

        var result = Calculator().Calculate(home);

        result.ProteinServings.ShouldBe(3.7875 + 1.3 * 0.2, Tolerance);
        result.VegServings.ShouldBe(3.7875, Tolerance);
        result.BaseServings.ShouldBe(3.7875, Tolerance);
    }

    [Fact]
    public void More_veg_raises_only_the_vegetable_total()
    {
        var home = Home(BowlSize.Medium, Father, Person("Mẹ", AgeGroup.Adult, 2, MemberTag.MoreVeg), Child, Grandma);

        var result = Calculator().Calculate(home);

        result.VegServings.ShouldBe(3.7875 + 1.0 * 0.2, Tolerance);
        result.ProteinServings.ShouldBe(3.7875, Tolerance);
        result.BaseServings.ShouldBe(3.7875, Tolerance);
    }

    [Fact]
    public void Both_tags_on_one_member_each_apply_to_their_own_total()
    {
        var home = Home(BowlSize.Medium, Father, Mother, Child, Person("Bà", AgeGroup.Senior, 1.5, MemberTag.MoreProtein, MemberTag.MoreVeg));

        var result = Calculator().Calculate(home);

        result.ProteinServings.ShouldBe(3.7875 + 0.765 * 0.2, Tolerance);
        result.VegServings.ShouldBe(3.7875 + 0.765 * 0.2, Tolerance);
    }

    [Fact]
    public void Tags_on_several_members_add_up()
    {
        var home = Home(
            BowlSize.Medium,
            Person("Bố", AgeGroup.Adult, 3, MemberTag.MoreProtein),
            Person("Mẹ", AgeGroup.Adult, 2, MemberTag.MoreProtein),
            Child,
            Grandma);

        Calculator().Calculate(home).ProteinServings.ShouldBe(3.7875 + (1.3 + 1.0) * 0.2, Tolerance);
    }

    [Fact]
    public void The_no_spicy_tag_does_not_change_portions()
    {
        var plain = Calculator().Calculate(Home(BowlSize.Medium, Father));
        var tagged = Calculator().Calculate(Home(BowlSize.Medium, Person("Bố", AgeGroup.Adult, 3, MemberTag.NoSpicy)));

        // PortionResult holds a list, so it compares by reference; compare the numbers.
        tagged.MemberServings.ShouldBe(plain.MemberServings);
        tagged.BaseServings.ShouldBe(plain.BaseServings);
        tagged.ProteinServings.ShouldBe(plain.ProteinServings);
        tagged.VegServings.ShouldBe(plain.VegServings);
        tagged.TotalBowls.ShouldBe(plain.TotalBowls);
        tagged.RawRiceGrams.ShouldBe(plain.RawRiceGrams);
    }

    // ---- every constant comes from the config ------------------------------------------------

    // Every constant differs from the shipped file and from the others, so hard-coding a value or
    // wiring one config field into the wrong place changes the result. All expected numbers are
    // worked out by hand from SPEC §4.2 (test-only constants, not production data).
    private static readonly PortionConfig DistinctConfig = Config with
    {
        ServingsBase = 0.5,
        ServingsPerBowl = 0.25,
        AgeModifiers = new AgeGroupValues(0.7, 0.8, 1.1, 0.95, 0.85),
        MoreProteinFactor = 1.3,
        MoreVegFactor = 1.1,
        GramsRawRicePerBowl = new BowlSizeValues(33, 44, 57),
        RiceRoundingGrams = 25,
    };

    private static Household DistinctHome(BowlSize size) => Home(
        size,
        Person("Bố", AgeGroup.Adult, 3, MemberTag.MoreProtein),               // (0.5 + 0.25 × 3) × 0.95 = 1.1875
        Person("Mẹ", AgeGroup.Adult, 2, MemberTag.MoreVeg),                   // (0.5 + 0.25 × 2) × 0.95 = 0.95
        Person("Con", AgeGroup.Child, 1.5),                                   // (0.5 + 0.25 × 1.5) × 0.8 = 0.7
        Person("Teen", AgeGroup.Teen, 2.5),                                   // (0.5 + 0.25 × 2.5) × 1.1 = 1.2375
        Person("Bé", AgeGroup.Toddler, 1),                                    // (0.5 + 0.25 × 1) × 0.7 = 0.525
        Person("Bà", AgeGroup.Senior, 1.5, MemberTag.MoreProtein, MemberTag.MoreVeg)); // 0.875 × 0.85 = 0.74375

    [Fact]
    public void Servings_use_the_configured_constants_and_each_tag_its_own_factor()
    {
        var result = new PortionCalculator(DistinctConfig).Calculate(DistinctHome(BowlSize.Medium));

        result.MemberServings.ShouldBe([1.1875, 0.95, 0.7, 1.2375, 0.525, 0.74375], Tolerance);
        result.BaseServings.ShouldBe(5.34375, Tolerance);
        // protein: members Bố and Bà get × 1.3, i.e. base + 0.3 × (1.1875 + 0.74375)
        result.ProteinServings.ShouldBe(5.923125, Tolerance);
        // vegetables: members Mẹ and Bà get × 1.1, i.e. base + 0.1 × (0.95 + 0.74375)
        result.VegServings.ShouldBe(5.513125, Tolerance);
        result.TotalBowls.ShouldBe(11.5);
    }

    [Theory]
    [InlineData(BowlSize.Small, 400)]   // 11.5 × 33 = 379.5 g, up to a multiple of 25
    [InlineData(BowlSize.Medium, 525)]  // 11.5 × 44 = 506 g
    [InlineData(BowlSize.Large, 675)]   // 11.5 × 57 = 655.5 g
    public void Rice_uses_the_configured_grams_and_rounding_step(BowlSize size, double expectedGrams)
    {
        new PortionCalculator(DistinctConfig).Calculate(DistinctHome(size)).RawRiceGrams.ShouldBe(expectedGrams);
    }

    [Fact]
    public void A_real_excess_far_below_a_gram_is_still_rounded_up()
    {
        // 8 bowls × 40.0000000125 g = 320.0000001 g, which is 1e-7 g above a multiple of 10: not noise.
        var config = Config with { GramsRawRicePerBowl = new BowlSizeValues(35, 40.0000000125, 50) };
        var home = Home(BowlSize.Medium, Father, Mother, Child, Grandma);

        new PortionCalculator(config).Calculate(home).RawRiceGrams.ShouldBe(330);
    }

    // ---- bowl size and rice rounding ---------------------------------------------------------

    [Theory]
    [InlineData(BowlSize.Small, 280)]   // 8 bowls × 35 g
    [InlineData(BowlSize.Medium, 320)]  // 8 bowls × 40 g
    [InlineData(BowlSize.Large, 400)]   // 8 bowls × 50 g
    public void Bowl_size_sets_the_raw_rice_per_bowl(BowlSize size, double expectedGrams)
    {
        var result = Calculator().Calculate(Home(size, Father, Mother, Child, Grandma));

        result.RawRiceGrams.ShouldBe(expectedGrams);
    }

    [Theory]
    [InlineData(BowlSize.Small, 1.0, 3, 110)]   // 3 bowls × 35 g = 105 g rounds up to 110
    [InlineData(BowlSize.Small, 2.5, 3, 270)]   // 7.5 bowls × 35 g = 262.5 g rounds up to 270
    [InlineData(BowlSize.Large, 0.5, 1, 30)]    // 25 g rounds up to 30
    [InlineData(BowlSize.Medium, 0.5, 1, 20)]   // 20 g is already a multiple of 10 and stays
    [InlineData(BowlSize.Medium, 5.0, 1, 200)]  // 200 g stays
    public void Rice_is_rounded_up_to_a_multiple_of_ten(BowlSize size, double bowlsEach, int headcount, double expectedGrams)
    {
        var members = Enumerable.Range(0, headcount).Select(i => Person($"Người {i}", AgeGroup.Adult, bowlsEach)).ToArray();

        var result = Calculator().Calculate(Home(size, members));

        result.RawRiceGrams.ShouldBe(expectedGrams);
    }

    [Fact]
    public void Rice_rounding_ignores_floating_point_noise_just_above_a_multiple()
    {
        // 3 × 0.1 is 0.30000000000000004 in binary; a naive ceil of it divided by 0.1 would jump to 0.4.
        var config = Config with
        {
            GramsRawRicePerBowl = new BowlSizeValues(0.1, 0.1, 0.1),
            RiceRoundingGrams = 0.1,
        };
        var home = Home(BowlSize.Medium, Person("A", AgeGroup.Adult, 1), Person("B", AgeGroup.Adult, 1), Person("C", AgeGroup.Adult, 1));

        new PortionCalculator(config).Calculate(home).RawRiceGrams.ShouldBe(0.3, Tolerance);
    }

    // ---- age groups --------------------------------------------------------------------------

    // With 2 bowls the bowl term is 0.4 + 0.3 × 2 = 1, so a single member's servings equal the age modifier.
    [Theory]
    [InlineData(AgeGroup.Toddler, 0.6)]
    [InlineData(AgeGroup.Child, 0.85)]
    [InlineData(AgeGroup.Teen, 1.0)]
    [InlineData(AgeGroup.Adult, 1.0)]
    [InlineData(AgeGroup.Senior, 0.9)]
    public void Age_group_scales_a_members_servings(AgeGroup age, double expectedServings)
    {
        var result = Calculator().Calculate(Home(BowlSize.Medium, Person("Một người", age, 2)));

        result.MemberServings.ShouldBe([expectedServings], Tolerance);
    }

    [Theory]
    [InlineData(AgeGroup.Toddler, 1.0)]
    [InlineData(AgeGroup.Child, 1.5)]
    [InlineData(AgeGroup.Teen, 2.5)]
    [InlineData(AgeGroup.Adult, 2.0)]
    [InlineData(AgeGroup.Senior, 1.5)]
    public void Default_bowls_follow_SPEC_4_1(AgeGroup age, double expectedBowls)
    {
        Calculator().DefaultBowlsFor(age).ShouldBe(expectedBowls);
    }

    // ---- slots -------------------------------------------------------------------------------

    [Fact]
    public void Each_slot_uses_its_own_serving_total()
    {
        var home = Home(
            BowlSize.Medium,
            Person("Bố", AgeGroup.Adult, 3, MemberTag.MoreProtein),
            Person("Mẹ", AgeGroup.Adult, 2, MemberTag.MoreVeg));

        var result = Calculator().Calculate(home);

        result.ServingsFor(DishSlot.Soup).ShouldBe(result.BaseServings);
        result.ServingsFor(DishSlot.Main).ShouldBe(result.ProteinServings);
        result.ServingsFor(DishSlot.Vegetable).ShouldBe(result.VegServings);
        result.ProteinServings.ShouldNotBe(result.VegServings);
        result.ProteinServings.ShouldBeGreaterThan(result.BaseServings);
        result.VegServings.ShouldBeGreaterThan(result.BaseServings);
    }

    [Fact]
    public void An_undefined_slot_is_rejected()
    {
        var result = Calculator().Calculate(Home(BowlSize.Medium, Father));

        Should.Throw<ArgumentOutOfRangeException>(() => result.ServingsFor((DishSlot)99));
    }

    // ---- edges -------------------------------------------------------------------------------

    [Fact]
    public void An_empty_household_gives_zeros()
    {
        var result = Calculator().Calculate(Home(BowlSize.Medium));

        result.MemberServings.ShouldBeEmpty();
        result.BaseServings.ShouldBe(0);
        result.ProteinServings.ShouldBe(0);
        result.VegServings.ShouldBe(0);
        result.TotalBowls.ShouldBe(0);
        result.RawRiceGrams.ShouldBe(0);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(5.0)]
    public void Bowls_at_the_ends_of_the_range_are_accepted(double bowls)
    {
        Should.NotThrow(() => Calculator().Calculate(Home(BowlSize.Medium, Person("A", AgeGroup.Adult, bowls))));
    }

    [Theory]
    [InlineData(0.4)]
    [InlineData(5.5)]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Bowls_outside_the_range_are_rejected(double bowls)
    {
        var home = Home(BowlSize.Medium, Person("A", AgeGroup.Adult, bowls));

        Should.Throw<ArgumentOutOfRangeException>(() => Calculator().Calculate(home));
    }

    [Fact]
    public void Null_arguments_are_rejected()
    {
        Should.Throw<ArgumentNullException>(() => new PortionCalculator(null!));
        Should.Throw<ArgumentNullException>(() => Calculator().Calculate(null!));
    }

    // ---- properties over many households -----------------------------------------------------

    [Fact]
    public void Totals_stay_consistent_for_many_random_households()
    {
        var random = new SeededRandomSource(1234);
        var calculator = Calculator();

        for (var run = 0; run < 500; run++)
        {
            var household = RandomHousehold(random);
            var result = calculator.Calculate(household);

            result.BaseServings.ShouldBe(result.MemberServings.Sum(), Tolerance);
            result.ProteinServings.ShouldBeGreaterThanOrEqualTo(result.BaseServings - Tolerance);
            result.VegServings.ShouldBeGreaterThanOrEqualTo(result.BaseServings - Tolerance);
            result.TotalBowls.ShouldBe(household.Members.Sum(m => m.BowlsPerMeal), Tolerance);

            var exactGrams = result.TotalBowls * Config.GramsRawRicePerBowl.For(household.BowlSize);
            (result.RawRiceGrams % 10).ShouldBe(0, Tolerance);
            result.RawRiceGrams.ShouldBeGreaterThanOrEqualTo(exactGrams - Tolerance);
            result.RawRiceGrams.ShouldBeLessThan(exactGrams + 10);
        }
    }

    [Fact]
    public void Member_order_does_not_change_the_totals()
    {
        var random = new SeededRandomSource(99);
        var calculator = Calculator();

        for (var run = 0; run < 200; run++)
        {
            var household = RandomHousehold(random);
            var reversed = household with { Members = household.Members.Reverse().ToArray() };

            var original = calculator.Calculate(household);
            var other = calculator.Calculate(reversed);

            other.BaseServings.ShouldBe(original.BaseServings, Tolerance);
            other.ProteinServings.ShouldBe(original.ProteinServings, Tolerance);
            other.VegServings.ShouldBe(original.VegServings, Tolerance);
            other.RawRiceGrams.ShouldBe(original.RawRiceGrams);
        }
    }

    [Fact]
    public void Adding_a_member_never_lowers_any_total()
    {
        var random = new SeededRandomSource(7);
        var calculator = Calculator();

        for (var run = 0; run < 200; run++)
        {
            var household = RandomHousehold(random);
            var bigger = household with { Members = [.. household.Members, RandomMember(random)] };

            var before = calculator.Calculate(household);
            var after = calculator.Calculate(bigger);

            after.BaseServings.ShouldBeGreaterThan(before.BaseServings);
            after.ProteinServings.ShouldBeGreaterThan(before.ProteinServings);
            after.VegServings.ShouldBeGreaterThan(before.VegServings);
            after.RawRiceGrams.ShouldBeGreaterThanOrEqualTo(before.RawRiceGrams);
        }
    }

    private static Household RandomHousehold(IRandomSource random)
    {
        var members = Enumerable.Range(0, 1 + random.NextInt(8)).Select(_ => RandomMember(random)).ToArray();
        return Home(Enum.GetValues<BowlSize>()[random.NextInt(3)], members);
    }

    private static Member RandomMember(IRandomSource random)
    {
        var tags = Enum.GetValues<MemberTag>().Where(_ => random.NextInt(3) == 0).ToArray();
        return Person("Thành viên", Enum.GetValues<AgeGroup>()[random.NextInt(5)], 0.5 * (1 + random.NextInt(10)), tags);
    }
}
