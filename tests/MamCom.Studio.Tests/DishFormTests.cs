using MamCom.Core.Models;
using MamCom.DataBuilder.Loading;
using MamCom.Studio.Editing;

namespace MamCom.Studio.Tests;

public class DishFormTests
{
    private static readonly string[] Seasons = ["winter", "spring", "summer", "autumn", "all"];

    private static Dish Seed(string id = "rau-muong-luoc")
    {
        using var data = new StudioData();
        return DishFile.Parse(data.ReadText($"dishes/{id}.json"));
    }

    public static TheoryData<string> SeedDishes()
    {
        var ids = new TheoryData<string>();
        using var data = new StudioData();
        foreach (var path in Directory.GetFiles(data.PathOf("dishes"), "*.json"))
        {
            ids.Add(Path.GetFileNameWithoutExtension(path));
        }

        return ids;
    }

    // ---- round trip ---------------------------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(SeedDishes))]
    public void A_seed_dish_goes_through_the_form_and_back_unchanged(string id)
    {
        var dish = Seed(id);

        // Records compare their lists by reference, so the file text is what tells whether anything changed.
        DishFile.Serialize(DishForm.From(dish).ToDish(dish)).ShouldBe(DishFile.Serialize(dish));
    }

    [Fact]
    public void The_form_holds_what_the_dish_has()
    {
        var form = DishForm.From(Seed());

        form.Name.ShouldBe("Rau muống luộc");
        form.Slot.ShouldBe(DishSlot.Vegetable);
        form.Regions.ShouldBe([Region.North, Region.Central, Region.South]);
        form.Seasons.ShouldBe(["summer", "autumn"]);
        form.Spicy.ShouldBe(SpicyLevel.None);
        form.CookingMethod.ShouldBe(CookingMethod.Boil);
        form.ProteinSource.ShouldBe("none");
        form.ServeHot.ShouldBeTrue();
        form.Lines.Count.ShouldBe(2);
        form.Lines[0].IngredientId.ShouldBe("rau-muong");
        form.Lines[0].GramsEdiblePerServing.ShouldBe(100);
        form.Lines[0].Scale.ShouldBe(ScaleKind.Linear);
        form.Lines[0].ConsumedRatio.ShouldBe(1);
        form.Lines[0].Note.ShouldBe("nhặt, rửa sạch");
        form.Lines[1].Note.ShouldBe(string.Empty);
        form.Steps.Count.ShouldBe(2);
        form.Steps[1].ActiveMinutes.ShouldBe(3);
        form.Steps[1].PassiveMinutes.ShouldBe(3);
        form.Steps[1].IngredientIds.ShouldBe(["rau-muong", "muoi"]);
    }

    [Fact]
    public void The_form_is_a_copy_so_editing_it_does_not_change_the_dish_it_came_from()
    {
        var dish = Seed();
        var form = DishForm.From(dish);

        form.Lines[0].GramsEdiblePerServing = 999;
        form.Steps[0].IngredientIds.Add("muoi");
        form.Regions.Clear();
        form.Seasons.Clear();

        dish.Ingredients[0].GramsEdiblePerServing.ShouldBe(100);
        dish.Steps[0].IngredientIds.ShouldBe(["rau-muong"]);
        dish.Regions.Count.ShouldBe(3);
        dish.Seasons.Count.ShouldBe(2);
    }

    // ---- ToDish -------------------------------------------------------------------------------------------------------------

    [Fact]
    public void The_edited_values_end_up_in_the_dish_and_the_rest_comes_from_the_original()
    {
        var original = Seed() with { Status = DishStatus.Validated };
        var form = DishForm.From(original);
        form.Name = "Rau muống luộc chấm xì dầu";
        form.Slot = DishSlot.Main;
        form.Spicy = SpicyLevel.Optional;
        form.CookingMethod = CookingMethod.Steam;
        form.ProteinSource = "heo";
        form.ServeHot = false;
        form.Lines[0].GramsEdiblePerServing = 120;
        form.Lines[0].Scale = ScaleKind.Sublinear;
        form.Lines[0].ConsumedRatio = 0.5;

        var dish = form.ToDish(original);

        dish.Name.ShouldBe("Rau muống luộc chấm xì dầu");
        dish.Slot.ShouldBe(DishSlot.Main);
        dish.Spicy.ShouldBe(SpicyLevel.Optional);
        dish.CookingMethod.ShouldBe(CookingMethod.Steam);
        dish.ProteinSource.ShouldBe("heo");
        dish.ServeHot.ShouldBeFalse();
        dish.Ingredients[0].ShouldBe(new DishIngredient("rau-muong", 120, ScaleKind.Sublinear, 0.5, "nhặt, rửa sạch"));
        dish.Id.ShouldBe(original.Id);
        dish.Status.ShouldBe(DishStatus.Validated);
        dish.Provenance.ShouldBe(original.Provenance);
        dish.Image.ShouldBe(original.Image);
    }

    [Fact]
    public void Text_is_trimmed_and_a_blank_note_is_no_note()
    {
        var original = Seed();
        var form = DishForm.From(original);
        form.Name = "  Rau muống luộc  ";
        form.Lines[0].Note = "   ";
        form.Lines[1].Note = "  vừa đủ ";
        form.Steps[0].Text = "\tNhặt rau.  ";

        var dish = form.ToDish(original);

        dish.Name.ShouldBe("Rau muống luộc");
        dish.Ingredients[0].Note.ShouldBeNull();
        dish.Ingredients[1].Note.ShouldBe("vừa đủ");
        dish.Steps[0].Text.ShouldBe("Nhặt rau.");
    }

    [Fact]
    public void Steps_are_numbered_from_one_in_list_order()
    {
        var original = Seed();
        var form = DishForm.From(original);
        form.AddStep();
        form.Steps[2].Text = "Bày ra đĩa.";
        form.MoveStep(2, -2);

        var dish = form.ToDish(original);

        dish.Steps.Select(s => s.Order).ShouldBe([1, 2, 3]);
        dish.Steps[0].Text.ShouldBe("Bày ra đĩa.");
    }

    [Fact]
    public void The_dish_does_not_share_lists_with_the_form()
    {
        var original = Seed();
        var form = DishForm.From(original);
        var dish = form.ToDish(original);

        form.Regions.Clear();
        form.Seasons.Clear();
        form.Steps[0].IngredientIds.Clear();

        dish.Regions.Count.ShouldBe(3);
        dish.Seasons.Count.ShouldBe(2);
        dish.Steps[0].IngredientIds.Count.ShouldBe(1);
    }

    [Fact]
    public void The_arguments_are_checked()
    {
        Should.Throw<ArgumentNullException>(() => DishForm.From(null!));
        Should.Throw<ArgumentNullException>(() => new DishForm().ToDish(null!));
        Should.Throw<ArgumentNullException>(() => new DishForm().ToggleSeason(null!, Seasons));
        Should.Throw<ArgumentNullException>(() => new DishForm().ToggleSeason("all", null!));
        Should.Throw<ArgumentNullException>(() => new DishForm().ToggleStepIngredient(0, null!));
    }

    // ---- regions and seasons ------------------------------------------------------------------------------------------------

    [Fact]
    public void A_region_is_added_and_removed_and_the_regions_stay_in_order()
    {
        var form = new DishForm();

        form.ToggleRegion(Region.South);
        form.ToggleRegion(Region.North);
        form.ToggleRegion(Region.Central);
        form.Regions.ShouldBe([Region.North, Region.Central, Region.South]);

        form.ToggleRegion(Region.Central);
        form.Regions.ShouldBe([Region.North, Region.South]);
    }

    [Fact]
    public void A_season_is_added_and_removed_and_the_seasons_stay_in_the_order_of_the_config()
    {
        var form = new DishForm();

        form.ToggleSeason("autumn", Seasons);
        form.ToggleSeason("winter", Seasons);
        form.ToggleSeason("summer", Seasons);
        form.Seasons.ShouldBe(["winter", "summer", "autumn"]);

        form.ToggleSeason("summer", Seasons);
        form.Seasons.ShouldBe(["winter", "autumn"]);
    }

    [Fact]
    public void All_seasons_excludes_the_single_ones_and_the_other_way_round()
    {
        var form = new DishForm();
        form.ToggleSeason("summer", Seasons);
        form.ToggleSeason("autumn", Seasons);

        form.ToggleSeason("all", Seasons);
        form.Seasons.ShouldBe(["all"]);

        form.ToggleSeason("winter", Seasons);
        form.Seasons.ShouldBe(["winter"]);

        form.ToggleSeason("winter", Seasons);
        form.Seasons.ShouldBeEmpty();
    }

    [Fact]
    public void A_season_the_config_does_not_list_goes_last()
    {
        var form = new DishForm();

        form.ToggleSeason("monsoon", Seasons);
        form.ToggleSeason("winter", Seasons);

        form.Seasons.ShouldBe(["winter", "monsoon"]);
    }

    // ---- lines --------------------------------------------------------------------------------------------------------------

    [Fact]
    public void A_new_line_has_a_gram_to_start_from_and_can_be_removed()
    {
        var form = DishForm.From(Seed());

        form.AddLine("hanh-la");

        form.Lines.Count.ShouldBe(3);
        form.Lines[2].IngredientId.ShouldBe("hanh-la");
        form.Lines[2].GramsEdiblePerServing.ShouldBe(1);
        form.Lines[2].Scale.ShouldBe(ScaleKind.Linear);
        form.Lines[2].ConsumedRatio.ShouldBe(1);

        form.RemoveLine(0);
        form.Lines.Select(l => l.IngredientId).ShouldBe(["muoi", "hanh-la"]);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    [InlineData(99)]
    public void Removing_a_line_that_is_not_there_does_nothing(int index)
    {
        var form = DishForm.From(Seed());

        form.RemoveLine(index);

        form.Lines.Count.ShouldBe(2);
    }

    // ---- steps --------------------------------------------------------------------------------------------------------------

    [Fact]
    public void A_new_step_takes_a_minute_of_work_and_can_be_removed()
    {
        var form = DishForm.From(Seed());

        form.AddStep();

        form.Steps.Count.ShouldBe(3);
        form.Steps[2].ActiveMinutes.ShouldBe(1);
        form.Steps[2].PassiveMinutes.ShouldBe(0);
        form.Steps[2].Text.ShouldBe(string.Empty);
        form.Steps[2].IngredientIds.ShouldBeEmpty();

        form.RemoveStep(0);
        form.Steps.Count.ShouldBe(2);
        form.Steps[0].Text.ShouldStartWith("Luộc rau");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void Removing_a_step_that_is_not_there_does_nothing(int index)
    {
        var form = DishForm.From(Seed());

        form.RemoveStep(index);

        form.Steps.Count.ShouldBe(2);
    }

    [Fact]
    public void A_step_moves_up_and_down()
    {
        var form = DishForm.From(Seed());
        var first = form.Steps[0].Text;
        var second = form.Steps[1].Text;

        form.MoveStep(0, 1);
        form.Steps.Select(s => s.Text).ShouldBe([second, first]);

        form.MoveStep(1, -1);
        form.Steps.Select(s => s.Text).ShouldBe([first, second]);
    }

    [Theory]
    [InlineData(0, -1)]
    [InlineData(1, 1)]
    [InlineData(-1, 1)]
    [InlineData(2, -1)]
    [InlineData(0, 5)]
    public void A_move_off_either_end_or_from_a_place_that_is_not_there_does_nothing(int index, int by)
    {
        var form = DishForm.From(Seed());
        var before = form.Steps.Select(s => s.Text).ToArray();

        form.MoveStep(index, by);

        form.Steps.Select(s => s.Text).ShouldBe(before);
    }

    [Fact]
    public void An_ingredient_is_added_to_a_step_and_removed_from_it()
    {
        var form = DishForm.From(Seed());

        form.ToggleStepIngredient(0, "muoi");
        form.Steps[0].IngredientIds.ShouldBe(["rau-muong", "muoi"]);

        form.ToggleStepIngredient(0, "rau-muong");
        form.Steps[0].IngredientIds.ShouldBe(["muoi"]);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void Toggling_an_ingredient_of_a_step_that_is_not_there_does_nothing(int index)
    {
        var form = DishForm.From(Seed());

        form.ToggleStepIngredient(index, "muoi");

        form.Steps.SelectMany(s => s.IngredientIds).Count().ShouldBe(3);
    }
}
