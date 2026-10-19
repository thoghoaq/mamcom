using MamCom.Core.Models;

namespace MamCom.Core.Tests.Models;

public class NutritionFactsTests
{
    private static NutritionFacts A => TestData.Facts(1, 2, 3, 4, 5, 6);

    private static NutritionFacts B => TestData.Facts(10, 20, 30, 40, 50, 60);

    [Fact]
    public void Zero_has_every_nutrient_at_zero_and_is_complete()
    {
        NutritionFacts.Zero.ShouldBe(TestData.Facts(0, 0, 0, 0, 0, 0));
        NutritionFacts.Zero.IsComplete().ShouldBeTrue();
    }

    [Fact]
    public void Addition_sums_nutrient_by_nutrient()
    {
        (A + B).ShouldBe(TestData.Facts(11, 22, 33, 44, 55, 66));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void An_unknown_nutrient_on_either_side_stays_unknown_and_the_others_still_add(int unknownIndex)
    {
        var withGap = Blank(A, unknownIndex);

        var left = withGap + B;
        var right = B + withGap;

        Values(left).Select((v, i) => (v, i)).ShouldAllBe(x => x.i == unknownIndex ? x.v == null : x.v != null);
        Values(right).ShouldBe(Values(left));
        Values(left).Where((v, i) => i != unknownIndex).ShouldBe(Values(A + B).Where((v, i) => i != unknownIndex));
    }

    [Fact]
    public void Times_multiplies_known_nutrients_and_keeps_unknown_ones_unknown()
    {
        A.Times(2.5).ShouldBe(TestData.Facts(2.5, 5, 7.5, 10, 12.5, 15));
        A.Times(0).ShouldBe(NutritionFacts.Zero);
        Blank(A, 1).Times(2).ProteinG.ShouldBeNull();
        Blank(A, 1).Times(2).Kcal.ShouldBe(2);
    }

    [Fact]
    public void IsComplete_is_false_as_soon_as_one_nutrient_is_unknown()
    {
        A.IsComplete().ShouldBeTrue();
        TestData.Unknown.IsComplete().ShouldBeFalse();
        for (var i = 0; i < 6; i++)
        {
            Blank(A, i).IsComplete().ShouldBeFalse();
        }
    }

    [Fact]
    public void Addition_rejects_null_operands()
    {
        Should.Throw<ArgumentNullException>(() => A + null!);
        Should.Throw<ArgumentNullException>(() => null! + A);
    }

    private static double?[] Values(NutritionFacts f) => [f.Kcal, f.ProteinG, f.FatG, f.CarbG, f.FiberG, f.SodiumMg];

    private static NutritionFacts Blank(NutritionFacts f, int index) => index switch
    {
        0 => f with { Kcal = null },
        1 => f with { ProteinG = null },
        2 => f with { FatG = null },
        3 => f with { CarbG = null },
        4 => f with { FiberG = null },
        5 => f with { SodiumMg = null },
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
}
