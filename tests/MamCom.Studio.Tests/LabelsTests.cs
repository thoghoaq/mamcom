using MamCom.Core.Models;
using MamCom.DataBuilder.Validation;
using MamCom.Studio.Review;

namespace MamCom.Studio.Tests;

public class LabelsTests
{
    private static void EveryValueHasItsOwnWord<T>(Func<T, string> label) where T : struct, Enum
    {
        var words = Enum.GetValues<T>().Select(label).ToArray();

        words.ShouldAllBe(w => !string.IsNullOrWhiteSpace(w));
        words.Distinct().Count().ShouldBe(words.Length, $"every {typeof(T).Name} needs a different word");
    }

    [Fact] public void Every_status_has_a_word() => EveryValueHasItsOwnWord<DishStatus>(Labels.Status);

    [Fact] public void Every_slot_has_a_word() => EveryValueHasItsOwnWord<DishSlot>(Labels.Slot);

    [Fact] public void Every_region_has_a_word() => EveryValueHasItsOwnWord<Region>(Labels.Region);

    [Fact] public void Every_spicy_level_has_a_word() => EveryValueHasItsOwnWord<SpicyLevel>(Labels.Spicy);

    [Fact] public void Every_cooking_method_has_a_word() => EveryValueHasItsOwnWord<CookingMethod>(Labels.Method);

    [Fact] public void Every_scale_has_a_word() => EveryValueHasItsOwnWord<ScaleKind>(Labels.Scale);

    [Fact] public void Every_author_has_a_word() => EveryValueHasItsOwnWord<DraftAuthor>(Labels.Author);

    [Fact] public void Every_severity_has_a_word() => EveryValueHasItsOwnWord<Severity>(Labels.Severity);

    [Fact] public void Every_review_outcome_has_its_own_message() => EveryValueHasItsOwnWord<ReviewStatus>(Labels.Review);

    [Fact] public void Every_aisle_has_a_word() => EveryValueHasItsOwnWord<Aisle>(Labels.Aisle);

    [Fact] public void Every_ingredient_outcome_has_its_own_message() => EveryValueHasItsOwnWord<IngredientStatus>(Labels.IngredientReview);

    [Fact]
    public void The_keys_of_a_region_keyed_object_have_words_and_an_unknown_key_is_shown_as_it_is()
    {
        Labels.Key("default").ShouldBe("Mặc định (mọi vùng)");
        Labels.Key("north").ShouldBe("Bắc");
        Labels.Key("central").ShouldBe("Trung");
        Labels.Key("south").ShouldBe("Nam");
        Labels.Key("west").ShouldBe("west");
    }

    [Theory]
    [InlineData("placeholder", "Chưa có nguồn")]
    [InlineData("usda-fdc", "USDA FoodData Central")]
    [InlineData("vn-fct-2017", "Bảng thành phần thực phẩm Việt Nam 2017")]
    [InlineData("asean-fcd", "ASEAN Food Composition Database")]
    [InlineData("khac", "khac")]
    public void A_nutrition_database_has_a_word_or_is_shown_as_it_is(string db, string expected) => Labels.Db(db).ShouldBe(expected);

    [Theory]
    [InlineData(IngredientStatus.Saved, "ok")]
    [InlineData(IngredientStatus.NutritionChosen, "ok")]
    [InlineData(IngredientStatus.NutritionCleared, "ok")]
    [InlineData(IngredientStatus.Unchanged, "info")]
    [InlineData(IngredientStatus.Conflict, "danger")]
    [InlineData(IngredientStatus.HasErrors, "danger")]
    [InlineData(IngredientStatus.VerifierMissing, "danger")]
    [InlineData(IngredientStatus.NoSuchCandidate, "danger")]
    [InlineData(IngredientStatus.CandidateHasNoNumbers, "danger")]
    [InlineData(IngredientStatus.DataNotUsable, "danger")]
    public void An_ingredient_message_looks_like_what_happened(IngredientStatus status, string tone) => Labels.IngredientTone(status).ShouldBe(tone);

    [Fact]
    public void An_ingredient_status_that_is_not_in_the_enum_is_refused()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Labels.Aisle((Aisle)99));
        Should.Throw<ArgumentOutOfRangeException>(() => Labels.IngredientReview((IngredientStatus)99));
    }

    [Fact]
    public void The_words_are_the_ones_of_the_kitchen()
    {
        Labels.Status(DishStatus.Reviewed).ShouldBe("Đã duyệt");
        Labels.Slot(DishSlot.Soup).ShouldBe("Canh");
        Labels.Slot(DishSlot.Main).ShouldBe("Món mặn");
        Labels.Slot(DishSlot.Vegetable).ShouldBe("Rau");
        Labels.Region(Region.South).ShouldBe("Nam");
        Labels.Method(CookingMethod.Braise).ShouldBe("Kho");
        Labels.Spicy(SpicyLevel.Optional).ShouldBe("Cay tuỳ chọn");
        Labels.Severity(Severity.Error).ShouldBe("Lỗi");
        Labels.Severity(Severity.Warning).ShouldBe("Cảnh báo");
    }

    [Fact]
    public void A_value_that_is_not_in_the_enum_is_refused()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Labels.Status((DishStatus)99));
        Should.Throw<ArgumentOutOfRangeException>(() => Labels.Slot((DishSlot)99));
        Should.Throw<ArgumentOutOfRangeException>(() => Labels.Region((Region)99));
        Should.Throw<ArgumentOutOfRangeException>(() => Labels.Spicy((SpicyLevel)99));
        Should.Throw<ArgumentOutOfRangeException>(() => Labels.Method((CookingMethod)99));
        Should.Throw<ArgumentOutOfRangeException>(() => Labels.Scale((ScaleKind)99));
        Should.Throw<ArgumentOutOfRangeException>(() => Labels.Author((DraftAuthor)99));
        Should.Throw<ArgumentOutOfRangeException>(() => Labels.Severity((Severity)99));
        Should.Throw<ArgumentOutOfRangeException>(() => Labels.Review((ReviewStatus)99));
    }

    [Theory]
    [InlineData("spring", "Xuân")]
    [InlineData("all", "Quanh năm")]
    [InlineData("monsoon", "monsoon")]
    public void A_season_has_a_word_or_is_shown_as_it_is(string id, string expected) => Labels.Season(id).ShouldBe(expected);

    [Theory]
    [InlineData("none", "Không có")]
    [InlineData("chay", "Đạm chay")]
    [InlineData("heo", "Heo")]
    [InlineData("tom", "Tôm")]
    [InlineData("nhim", "nhim")]
    public void A_protein_source_has_a_word_or_is_shown_as_it_is(string id, string expected) => Labels.Protein(id).ShouldBe(expected);

    [Theory]
    [InlineData("valid", "Hợp lệ")]
    [InlineData("warnings", "Có cảnh báo")]
    [InlineData("invalid", "Vi phạm luật")]
    [InlineData("failed", "Soạn lỗi")]
    [InlineData("something-new", "something-new")]
    public void What_the_drafter_said_has_a_word_or_is_shown_as_it_is(string validation, string expected) =>
        Labels.NoteValidation(validation).ShouldBe(expected);

    [Theory]
    [InlineData(ReviewStatus.Saved, "ok")]
    [InlineData(ReviewStatus.Approved, "ok")]
    [InlineData(ReviewStatus.Reopened, "ok")]
    [InlineData(ReviewStatus.CookTested, "ok")]
    [InlineData(ReviewStatus.CookTestUndone, "ok")]
    [InlineData(ReviewStatus.Unchanged, "info")]
    [InlineData(ReviewStatus.Conflict, "danger")]
    [InlineData(ReviewStatus.HasErrors, "danger")]
    [InlineData(ReviewStatus.ReviewerMissing, "danger")]
    [InlineData(ReviewStatus.NotFound, "danger")]
    [InlineData(ReviewStatus.NotCookTestable, "danger")]
    [InlineData(ReviewStatus.NotCookTested, "danger")]
    [InlineData(ReviewStatus.MinutesMissing, "danger")]
    [InlineData(ReviewStatus.CookDateInFuture, "danger")]
    public void A_message_looks_like_what_happened(ReviewStatus status, string tone) => Labels.ReviewTone(status).ShouldBe(tone);
}
