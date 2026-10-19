using System.Text.Json;
using MamCom.Core.Models;
using MamCom.Core.Portions;

namespace MamCom.Core.Tests.Portions;

public class PortionConfigTests
{
    private static readonly PortionConfig Config = PortionConfig.Parse(TestFiles.Config("portion-defaults.json"));

    [Fact]
    public void The_shipped_config_file_holds_the_SPEC_4_1_and_4_2_defaults()
    {
        Config.ServingsBase.ShouldBe(0.4);
        Config.ServingsPerBowl.ShouldBe(0.3);
        Config.AgeModifiers.ShouldBe(new AgeGroupValues(0.6, 0.85, 1.0, 1.0, 0.9));
        Config.MoreProteinFactor.ShouldBe(1.2);
        Config.MoreVegFactor.ShouldBe(1.2);
        Config.GramsRawRicePerBowl.ShouldBe(new BowlSizeValues(35, 40, 50));
        Config.RiceRoundingGrams.ShouldBe(10);
        Config.DefaultBowlsPerMeal.ShouldBe(new AgeGroupValues(1, 1.5, 2.5, 2, 1.5));
        Config.MinBowlsPerMeal.ShouldBe(0.5);
        Config.MaxBowlsPerMeal.ShouldBe(5);
        Config.BowlsStep.ShouldBe(0.5);
    }

    [Fact]
    public void The_shipped_config_file_is_valid()
    {
        Should.NotThrow(Config.Validate);
    }

    [Fact]
    public void Every_age_group_and_bowl_size_has_a_value()
    {
        foreach (var age in Enum.GetValues<AgeGroup>())
        {
            Config.AgeModifiers.For(age).ShouldBeGreaterThan(0);
            Config.DefaultBowlsPerMeal.For(age).ShouldBeGreaterThan(0);
        }

        foreach (var size in Enum.GetValues<BowlSize>())
        {
            Config.GramsRawRicePerBowl.For(size).ShouldBeGreaterThan(0);
        }
    }

    [Fact]
    public void Undefined_age_group_and_bowl_size_are_rejected()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Config.AgeModifiers.For((AgeGroup)99));
        Should.Throw<ArgumentOutOfRangeException>(() => Config.GramsRawRicePerBowl.For((BowlSize)99));
    }

    [Fact]
    public void A_missing_value_fails_to_parse()
    {
        var withoutRounding = TestFiles.Config("portion-defaults.json").Replace("\"riceRoundingGrams\"", "\"somethingElse\"");

        Should.Throw<JsonException>(() => PortionConfig.Parse(withoutRounding));
    }

    [Fact]
    public void A_missing_age_group_fails_to_parse()
    {
        var withoutSenior = TestFiles.Config("portion-defaults.json").Replace("\"senior\": 0.9", "\"elder\": 0.9");

        Should.Throw<JsonException>(() => PortionConfig.Parse(withoutSenior));
    }

    [Fact]
    public void A_null_document_fails_to_parse()
    {
        Should.Throw<JsonException>(() => PortionConfig.Parse("null"));
    }

    public static TheoryData<PortionConfig, string> InvalidConfigs => new()
    {
        { Config with { ServingsBase = -0.1 }, "ServingsBase" },
        { Config with { ServingsBase = double.NaN }, "ServingsBase" },
        { Config with { ServingsPerBowl = 0 }, "ServingsPerBowl" },
        { Config with { ServingsPerBowl = double.PositiveInfinity }, "ServingsPerBowl" },
        { Config with { AgeModifiers = Config.AgeModifiers with { Senior = 0 } }, "AgeModifiers" },
        { Config with { MoreProteinFactor = 0 }, "MoreProteinFactor" },
        { Config with { MoreVegFactor = double.PositiveInfinity }, "MoreVegFactor" },
        { Config with { GramsRawRicePerBowl = Config.GramsRawRicePerBowl with { Small = -1 } }, "GramsRawRicePerBowl" },
        { Config with { RiceRoundingGrams = 0 }, "RiceRoundingGrams" },
        { Config with { MinBowlsPerMeal = 0 }, "MinBowlsPerMeal" },
        { Config with { MaxBowlsPerMeal = 0.1 }, "MaxBowlsPerMeal" },
        { Config with { MaxBowlsPerMeal = double.PositiveInfinity }, "MaxBowlsPerMeal" },
        { Config with { BowlsStep = 0 }, "BowlsStep" },
        { Config with { DefaultBowlsPerMeal = Config.DefaultBowlsPerMeal with { Adult = 6 } }, "DefaultBowlsPerMeal" },
        { Config with { DefaultBowlsPerMeal = Config.DefaultBowlsPerMeal with { Toddler = double.NaN } }, "DefaultBowlsPerMeal" },
    };

    [Theory]
    [MemberData(nameof(InvalidConfigs))]
    public void An_unusable_value_is_rejected_when_the_calculator_is_created(PortionConfig config, string parameterName)
    {
        var error = Should.Throw<ArgumentException>(() => new PortionCalculator(config));

        error.ParamName.ShouldBe(parameterName);
    }
}
