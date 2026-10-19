using MamCom.Core.Models;
using MamCom.FdcImporter.Fdc;
using MamCom.FdcImporter.Ranking;

namespace MamCom.FdcImporter.Tests;

/// <summary>The order is the importer's whole judgement, so every step of it has a case where it alone decides.</summary>
public class CandidateRankerTests
{
    private static FdcFood Food(long id, string description, string dataType = "SR Legacy", double score = 100, bool complete = true) =>
        Foods.Parsed(id, description, dataType, score, complete ? Foods.Complete() : [.. Foods.Complete().Where(n => n.Item1 != 1079)]);

    private static long[] Ids(IEnumerable<FdcFood> foods, string query = "rice raw", int top = 10) =>
        [.. CandidateRanker.Rank(query, foods, top).Select(c => c.FdcId)];

    // ---- the order -----------------------------------------------------------------------------------------------

    [Fact]
    public void Raw_comes_before_unspecified_and_unspecified_before_cooked_whatever_else_they_have()
    {
        var foods = new[]
        {
            Food(1, "Rice, white, cooked", "Foundation", score: 9000),
            Food(2, "Rice, white", "SR Legacy", score: 10),
            Food(3, "Rice, white, raw", "SR Legacy", score: 1, complete: false),
        };

        Ids(foods).ShouldBe([3L, 2L, 1L]);
    }

    [Theory]
    [InlineData("cooked")]
    [InlineData("boiled")]
    [InlineData("fried")]
    [InlineData("roasted")]
    [InlineData("canned")]
    [InlineData("dried")]
    [InlineData("frozen")]
    [InlineData("pickled")]
    [InlineData("smoked")]
    public void A_preparation_word_in_the_description_puts_it_last(string word)
    {
        var foods = new[] { Food(1, $"Pork, {word}", score: 9000), Food(2, "Pork, fresh") };

        Ids(foods, "pork").ShouldBe([2L, 1L]);
    }

    [Fact]
    public void A_word_the_query_asks_for_is_not_held_against_the_food()
    {
        var foods = new[] { Food(1, "Egg, whole, boiled", score: 1), Food(2, "Egg, whole, fried", score: 9000) };

        var ranked = CandidateRanker.Rank("egg boiled", foods, 2);

        ranked.Select(c => c.FdcId).ShouldBe([1L, 2L]);
        ranked[0].Reason.ShouldNotContain("đã chế biến");
        ranked[1].Reason.ShouldContain("'fried'");
    }

    [Fact]
    public void Fewer_missing_nutrients_beats_a_better_data_type()
    {
        var foods = new[]
        {
            Food(1, "Rice, raw", "Foundation", complete: false),
            Food(2, "Rice, raw", "SR Legacy", complete: true),
        };

        Ids(foods).ShouldBe([2L, 1L]);
    }

    [Fact]
    public void Foundation_comes_before_sr_legacy_when_they_are_equally_complete()
    {
        var foods = new[] { Food(1, "Rice, raw", "SR Legacy", score: 9000), Food(2, "Rice, raw", "Foundation", score: 1) };

        Ids(foods).ShouldBe([2L, 1L]);
    }

    [Fact]
    public void More_of_the_query_words_in_the_description_comes_first_when_all_else_is_equal()
    {
        var foods = new[]
        {
            Food(1, "Rice, brown, raw", score: 9000),
            Food(2, "Rice, white, long-grain, raw", score: 1),
        };

        Ids(foods, "rice white long-grain raw").ShouldBe([2L, 1L]);
    }

    [Fact]
    public void Matching_the_query_beats_a_better_data_type()
    {
        var foods = new[]
        {
            Food(1, "Rice, brown, raw", "Foundation", score: 9000),
            Food(2, "Rice, white, raw", "SR Legacy", score: 1),
        };

        Ids(foods, "rice white raw").ShouldBe([2L, 1L]);
    }

    [Fact]
    public void Plural_and_singular_count_as_the_same_word()
    {
        var foods = new[] { Food(1, "Tomato, red, ripe, raw", score: 1), Food(2, "Carrot, raw", score: 9000) };

        var ranked = CandidateRanker.Rank("tomatoes", foods, 2);

        ranked[0].FdcId.ShouldBe(1);
        ranked[0].Reason.ShouldContain("khớp 1/1 từ khoá");
        ranked[1].Reason.ShouldContain("khớp 0/1 từ khoá");
    }

    [Theory]
    [InlineData("carrots", "Carrot")]      // plain s
    [InlineData("cherries", "Cherry")]     // ies and y
    [InlineData("tomatoes", "Tomato")]     // oes and o
    [InlineData("carrot", "Carrots")]      // the other way round
    public void A_plural_matches_its_singular_in_either_direction(string queryWord, string descriptionWord)
    {
        var foods = new[] { Food(1, $"{descriptionWord}, raw", score: 1), Food(2, "Zucchini, raw", score: 9000) };

        var ranked = CandidateRanker.Rank($"{queryWord} raw", foods, 2);

        ranked[0].FdcId.ShouldBe(1);
        ranked[0].Reason.ShouldContain("khớp 2/2 từ khoá");
        ranked[1].Reason.ShouldContain("khớp 1/2 từ khoá");
    }

    [Fact]
    public void Fdc_relevance_score_decides_among_otherwise_equal_foods_and_the_lower_id_breaks_the_last_tie()
    {
        var foods = new[]
        {
            Food(30, "Rice, raw", score: 50),
            Food(10, "Rice, raw", score: 50),
            Food(20, "Rice, raw", score: 80),
        };

        Ids(foods).ShouldBe([20L, 10L, 30L]);
    }

    [Fact]
    public void A_missing_score_counts_as_zero()
    {
        var foods = new[]
        {
            Foods.Parsed(1, "Rice, raw", "SR Legacy", score: 0, nutrients: Foods.Complete()) with { Score = null },
            Foods.Parsed(2, "Rice, raw", "SR Legacy", score: 5, nutrients: Foods.Complete()),
        };

        Ids(foods).ShouldBe([2L, 1L]);
    }

    [Fact]
    public void The_result_does_not_depend_on_the_order_the_foods_arrive_in()
    {
        var foods = new[]
        {
            Food(1, "Rice, white, cooked"),
            Food(2, "Rice, white, raw", "Foundation"),
            Food(3, "Rice, white, raw", "SR Legacy"),
            Food(4, "Rice, brown, raw", "SR Legacy", complete: false),
            Food(5, "Rice, noodles, dried"),
        };
        var expected = Ids(foods);

        for (var shift = 1; shift < foods.Length; shift++)
        {
            Ids([.. foods.Skip(shift), .. foods.Take(shift)]).ShouldBe(expected);
        }

        Ids(foods.Reverse()).ShouldBe(expected);
    }

    // ---- how many ----------------------------------------------------------------------------------------------------

    [Fact]
    public void Only_the_top_few_are_kept_and_numbered_from_one()
    {
        var foods = Enumerable.Range(1, 8).Select(i => Food(i, "Rice, raw", score: 100 - i)).ToArray();

        var ranked = CandidateRanker.Rank("rice", foods, 3);

        ranked.Select(c => c.Rank).ShouldBe([1, 2, 3]);
        ranked.Select(c => c.FdcId).ShouldBe([1L, 2L, 3L]);
    }

    [Fact]
    public void Fewer_foods_than_asked_for_gives_what_there_is_and_none_gives_nothing()
    {
        CandidateRanker.Rank("rice", [Food(1, "Rice, raw")], 5).Count.ShouldBe(1);
        CandidateRanker.Rank("rice", [], 5).ShouldBeEmpty();
    }

    [Fact]
    public void Bad_arguments_are_refused()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => CandidateRanker.Rank("rice", [], 0));
        Should.Throw<ArgumentException>(() => CandidateRanker.Rank(" ", [], 3));
        Should.Throw<ArgumentNullException>(() => CandidateRanker.Rank("rice", null!, 3));
    }

    // ---- what a candidate says --------------------------------------------------------------------------------------------

    [Fact]
    public void A_candidate_carries_fdcs_numbers_unchanged_and_a_link_to_check_them()
    {
        var food = Foods.Parsed(168877, "Rice, white, long-grain, regular, raw, enriched", "SR Legacy", 1067.9667, Foods.Complete(kcal: 365, protein: 7.13, fat: 0.66, carb: 80, fiber: 1.3, sodium: 5));

        var candidate = CandidateRanker.Rank("rice", [food], 1).Single();

        candidate.Rank.ShouldBe(1);
        candidate.FdcId.ShouldBe(168877);
        candidate.Description.ShouldBe("Rice, white, long-grain, regular, raw, enriched");
        candidate.DataType.ShouldBe("SR Legacy");
        candidate.FoodCategory.ShouldBe("Test category");
        candidate.DetailsUrl.ShouldBe("https://fdc.nal.usda.gov/food-details/168877/nutrients");
        candidate.Per100g.ShouldBe(new NutritionFacts(365, 7.13, 0.66, 80, 1.3, 5));
        candidate.Missing.ShouldBeEmpty();
        candidate.KcalSource.ShouldBe("Energy");
    }

    [Fact]
    public void The_reason_says_the_data_type_the_state_the_completeness_and_the_match()
    {
        var candidate = CandidateRanker.Rank("rice raw", [Food(1, "Rice, raw", "Foundation")], 1).Single();

        candidate.Reason.ShouldBe("Foundation Foods (số liệu phân tích mới của USDA); mô tả có 'raw': nguyên liệu sống; đủ 6 chất; khớp 2/2 từ khoá.");
    }

    [Fact]
    public void The_reason_names_what_is_missing_in_vietnamese()
    {
        var food = Foods.Parsed(1, "Rice, raw", "SR Legacy", 1, [(1008, "Energy", "KCAL", 100), (1003, "Protein", "G", 5)]);

        var candidate = CandidateRanker.Rank("rice", [food], 1).Single();

        candidate.Missing.ShouldBe(["fatG", "carbG", "fiberG", "sodiumMg"]);
        candidate.Reason.ShouldContain("thiếu: béo, bột đường, chất xơ, natri");
        candidate.Reason.ShouldContain("SR Legacy (bảng cũ của USDA)");
    }

    [Fact]
    public void The_reason_warns_about_a_cooked_description_and_says_when_energy_is_from_an_atwater_factor()
    {
        var food = Foods.Parsed(1, "Rice, cooked", "Foundation", 1, [(2048, "Energy (Atwater Specific Factors)", "KCAL", 130)]);

        var reason = CandidateRanker.Rank("rice", [food], 1).Single().Reason;

        reason.ShouldContain("mô tả có 'cooked': đã chế biến, cần cân nhắc");
        reason.ShouldContain("năng lượng lấy từ 'Energy (Atwater Specific Factors)'");
    }

    [Fact]
    public void A_description_that_says_nothing_about_preparation_is_said_to_say_nothing()
    {
        CandidateRanker.Rank("salt", [Food(1, "Salt, table")], 1).Single().Reason.ShouldContain("mô tả không nói sống hay chín");
    }

    [Fact]
    public void Words_are_compared_in_lower_case_and_not_inside_other_words()
    {
        // "raw" must not be found inside "strawberries", and "Raw" counts.
        var foods = new[] { Food(1, "Strawberries, canned"), Food(2, "Strawberries"), Food(3, "RAW strawberries") };

        var ranked = CandidateRanker.Rank("strawberries", foods, 3);

        ranked.Select(c => c.FdcId).ShouldBe([3L, 2L, 1L]);
    }

    [Fact]
    public void Common_filler_words_in_the_query_are_not_counted()
    {
        var reason = CandidateRanker.Rank("rice with the of raw", [Food(1, "Rice, raw")], 1).Single().Reason;

        reason.ShouldContain("khớp 2/2 từ khoá");
    }
}
