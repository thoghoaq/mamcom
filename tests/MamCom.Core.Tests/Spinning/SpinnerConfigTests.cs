using System.Text.Json;
using MamCom.Core.Models;
using MamCom.Core.Spinning;
using static MamCom.Core.Tests.Spinning.SpinnerFixtures;

namespace MamCom.Core.Tests.Spinning;

public class SpinnerConfigTests
{
    [Fact]
    public void The_shipped_config_file_holds_the_SPEC_5_values()
    {
        Config.SecondMainMinBaseServings.ShouldBe(5);                       // §5.1
        Config.AttemptsPerLevel.ShouldBe(50);                               // §5.5 step 3
        Config.HistorySize.ShouldBe(5);                                     // §5.5 step 6
        Config.Recency.ShouldBe(new RecencyConfig(2, 6, 0.3));              // §5.2 rule 4, §5.4
        Config.Preference.ShouldBe(new PreferenceWeights(2.0, 1.0, 0.2));   // §5.4
        Config.SeasonMatchWeight.ShouldBe(1.3);
        Config.RegionMatchWeight.ShouldBe(1.2);
        Config.MaxFriedDishes.ShouldBe(1);                                  // §5.3 rule 3
        Config.SeasonByMonth.Count.ShouldBe(12);
    }

    [Fact]
    public void The_shipped_config_file_is_valid()
    {
        Should.NotThrow(Config.Validate);
    }

    [Fact]
    public void A_missing_value_fails_to_parse()
    {
        var withoutAttempts = TestFiles.Config("spinner-defaults.json").Replace("\"attemptsPerLevel\"", "\"tries\"");

        Should.Throw<JsonException>(() => SpinnerConfig.Parse(withoutAttempts));
    }

    [Fact]
    public void A_null_document_fails_to_parse()
    {
        Should.Throw<JsonException>(() => SpinnerConfig.Parse("null"));
    }

    public static TheoryData<SpinnerConfig, string> InvalidConfigs => new()
    {
        { Config with { SecondMainMinBaseServings = 0 }, "SecondMainMinBaseServings" },
        { Config with { SecondMainMinBaseServings = double.NaN }, "SecondMainMinBaseServings" },
        { Config with { AttemptsPerLevel = 0 }, "AttemptsPerLevel" },
        { Config with { HistorySize = 0 }, "HistorySize" },
        { Config with { Recency = new RecencyConfig(-1, 6, 0.3) }, "Recency" },
        { Config with { Recency = new RecencyConfig(5, 4, 0.3) }, "Recency" },
        { Config with { Recency = new RecencyConfig(2, 6, 0) }, "Recency" },
        { Config with { Preference = new PreferenceWeights(0, 1, 0.2) }, "Preference" },
        { Config with { Preference = new PreferenceWeights(2, -1, 0.2) }, "Preference" },
        { Config with { Preference = new PreferenceWeights(2, 1, double.NaN) }, "Preference" },
        { Config with { SeasonMatchWeight = 0 }, "SeasonMatchWeight" },
        { Config with { RegionMatchWeight = double.PositiveInfinity }, "RegionMatchWeight" },
        { Config with { MaxFriedDishes = -1 }, "MaxFriedDishes" },
        { Config with { SeasonByMonth = ["winter"] }, "SeasonByMonth" },
        { Config with { SeasonByMonth = [.. Config.SeasonByMonth.Take(11), " "] }, "SeasonByMonth" },
    };

    [Theory]
    [MemberData(nameof(InvalidConfigs))]
    public void An_unusable_value_is_rejected_when_the_spinner_is_created(SpinnerConfig config, string parameterName)
    {
        var error = Should.Throw<ArgumentException>(() => Spinner(config: config));

        error.ParamName.ShouldBe(parameterName);
    }

    [Fact]
    public void Values_on_the_edge_of_the_allowed_range_are_accepted()
    {
        var edge = Config with
        {
            Recency = new RecencyConfig(0, 0, 1),
            MaxFriedDishes = 0,
            AttemptsPerLevel = 1,
            HistorySize = 1,
        };

        Should.NotThrow(() => Spinner(config: edge));
    }

    [Fact]
    public void Every_season_id_in_the_shipped_map_is_a_known_word_and_all_four_seasons_appear()
    {
        Config.SeasonByMonth.Distinct().Order().ShouldBe(["autumn", "spring", "summer", "winter"]);
        Config.SeasonByMonth.ShouldNotContain(SeasonIds.All);
    }
}

public class SpinHistoryTests
{
    private static SpinResult Result(string mainId) => new(
        new MealTray(Today, [new SlotResult(DishSlot.Main, mainId, false)]), [], []);

    [Fact]
    public void Empty_history_has_no_items()
    {
        SpinHistory.Empty.Items.ShouldBeEmpty();
    }

    [Fact]
    public void Pushing_puts_the_newest_first_and_keeps_the_old_ones_in_order()
    {
        var history = SpinHistory.Empty.Push(Result("a"), 5).Push(Result("b"), 5).Push(Result("c"), 5);

        history.Items.Select(r => r.Tray.Slots[0].DishId).ShouldBe(["c", "b", "a"]);
    }

    [Fact]
    public void Only_the_most_recent_trays_are_kept()
    {
        var history = SpinHistory.Empty;
        foreach (var id in new[] { "a", "b", "c", "d", "e", "f", "g" })
        {
            history = history.Push(Result(id), Config.HistorySize);
        }

        history.Items.Select(r => r.Tray.Slots[0].DishId).ShouldBe(["g", "f", "e", "d", "c"]);
    }

    [Fact]
    public void Pushing_does_not_change_the_history_it_was_called_on()
    {
        var before = SpinHistory.Empty.Push(Result("a"), 5);

        var after = before.Push(Result("b"), 5);

        before.Items.Count.ShouldBe(1);
        after.Items.Count.ShouldBe(2);
    }

    [Fact]
    public void A_capacity_of_one_keeps_only_the_latest()
    {
        SpinHistory.Empty.Push(Result("a"), 1).Push(Result("b"), 1).Items.Single().Tray.Slots[0].DishId.ShouldBe("b");
    }

    [Fact]
    public void Capacity_below_one_and_a_null_result_are_rejected()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => SpinHistory.Empty.Push(Result("a"), 0));
        Should.Throw<ArgumentNullException>(() => SpinHistory.Empty.Push(null!, 5));
    }
}
