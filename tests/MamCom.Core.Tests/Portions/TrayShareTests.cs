using MamCom.Core.Models;
using MamCom.Core.Portions;
using MamCom.Core.Details;
using MamCom.Core.Nutrition;
using static MamCom.Core.Tests.Spinning.SpinnerFixtures;

namespace MamCom.Core.Tests.Portions;

public class TrayShareTests
{
    private static readonly TrayShareConfig Shares = new(SameSlotTotal: 1.2, WithoutVegetable: 1.2);

    [Fact]
    public void A_tray_of_one_dish_of_each_kind_cooks_every_dish_to_its_full_servings()
    {
        DishSlot[] three = [DishSlot.Soup, DishSlot.Main, DishSlot.Vegetable];

        three.Select(slot => TrayShares.For(slot, three, Shares)).ShouldBe([1, 1, 1]);
        TrayShares.For(DishSlot.Main, three, null).ShouldBe(1);
        TrayShares.For(DishSlot.Main, null, Shares).ShouldBe(1);
    }

    [Fact]
    public void Two_mains_share_a_little_more_than_one_main_and_a_tray_without_vegetables_cooks_the_others_larger()
    {
        DishSlot[] four = [DishSlot.Soup, DishSlot.Main, DishSlot.Main, DishSlot.Vegetable];
        DishSlot[] two = [DishSlot.Soup, DishSlot.Main];

        TrayShares.For(DishSlot.Main, four, Shares).ShouldBe(0.6, 1e-12);
        TrayShares.For(DishSlot.Soup, four, Shares).ShouldBe(1);
        TrayShares.For(DishSlot.Vegetable, four, Shares).ShouldBe(1);
        TrayShares.For(DishSlot.Soup, two, Shares).ShouldBe(1.2, 1e-12);
        TrayShares.For(DishSlot.Main, two, Shares).ShouldBe(1.2, 1e-12);
        TrayShares.For(DishSlot.Main, [DishSlot.Main], Shares).ShouldBe(1);   // one dish alone is not the tray of two
    }

    [Fact]
    public void The_shipped_config_shares_out_and_a_dish_of_two_mains_is_cooked_for_fewer_servings()
    {
        var shipped = PortionConfig.Parse(TestFiles.Config("portion-defaults.json"));
        shipped.TrayShares.ShouldBe(Shares);

        var builder = new DishDetailBuilder(shipped, NutritionConfig.Parse(TestFiles.Config("nutrition-defaults.json")));
        var alone = builder.Build(Find("ga-ran"), SmallHome(), Ingredients());
        var ofFour = builder.Build(Find("ga-ran"), SmallHome(), Ingredients(), [DishSlot.Soup, DishSlot.Main, DishSlot.Main, DishSlot.Vegetable]);

        ofFour.Servings.ShouldBe(alone.Servings * 0.6, 1e-9);
        ofFour.Ingredients[0].GramsEdible.ShouldBe(alone.Ingredients[0].GramsEdible * 0.6, 1e-9);
        ofFour.PerMember[0].Servings.ShouldBe(alone.PerMember[0].Servings * 0.6, 1e-9);
    }

    [Fact]
    public void A_factor_that_is_not_positive_is_refused()
    {
        Should.Throw<ArgumentException>(() => new TrayShareConfig(0, 1.2).Validate());
        Should.Throw<ArgumentException>(() => new TrayShareConfig(1.2, double.NaN).Validate());
    }
}
