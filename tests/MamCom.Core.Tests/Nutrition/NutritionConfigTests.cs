using System.Text.Json;
using MamCom.Core.Nutrition;
using MamCom.Core.Portions;

namespace MamCom.Core.Tests.Nutrition;

public class NutritionConfigTests
{
    private static readonly NutritionConfig Config = NutritionConfig.Parse(TestFiles.Config("nutrition-defaults.json"));

    private static readonly PortionConfig Portions = PortionConfig.Parse(TestFiles.Config("portion-defaults.json"));

    [Fact]
    public void The_shipped_config_file_holds_the_SPEC_6_3_constants()
    {
        Config.KcalPerGram.ShouldBe(new MacroValues(4, 9, 4));
        Config.RecommendedEnergyShare.Protein.ShouldBe(new ShareRange(0.13, 0.20));
        Config.RecommendedEnergyShare.Fat.ShouldBe(new ShareRange(0.20, 0.25));
        Config.RecommendedEnergyShare.Carb.ShouldBe(new ShareRange(0.55, 0.65));
    }

    [Fact]
    public void The_shipped_config_file_is_valid()
    {
        Should.NotThrow(Config.Validate);
    }

    [Fact]
    public void A_missing_value_fails_to_parse()
    {
        var withoutFat = TestFiles.Config("nutrition-defaults.json").Replace("\"fat\": 9", "\"lipid\": 9");

        Should.Throw<JsonException>(() => NutritionConfig.Parse(withoutFat));
    }

    [Fact]
    public void A_null_document_fails_to_parse()
    {
        Should.Throw<JsonException>(() => NutritionConfig.Parse("null"));
    }

    public static TheoryData<NutritionConfig, string> InvalidConfigs => new()
    {
        { Config with { KcalPerGram = Config.KcalPerGram with { Protein = 0 } }, "KcalPerGram" },
        { Config with { KcalPerGram = Config.KcalPerGram with { Fat = -9 } }, "KcalPerGram" },
        { Config with { KcalPerGram = Config.KcalPerGram with { Carb = double.NaN } }, "KcalPerGram" },
        { Config with { KcalPerGram = Config.KcalPerGram with { Carb = double.PositiveInfinity } }, "KcalPerGram" },
        { Config with { RecommendedEnergyShare = Config.RecommendedEnergyShare with { Protein = new ShareRange(0.3, 0.2) } }, "RecommendedEnergyShare" },
        { Config with { RecommendedEnergyShare = Config.RecommendedEnergyShare with { Fat = new ShareRange(-0.1, 0.2) } }, "RecommendedEnergyShare" },
        { Config with { RecommendedEnergyShare = Config.RecommendedEnergyShare with { Carb = new ShareRange(0.5, 1.1) } }, "RecommendedEnergyShare" },
        { Config with { RecommendedEnergyShare = Config.RecommendedEnergyShare with { Protein = new ShareRange(double.NaN, 0.2) } }, "RecommendedEnergyShare" },
        { Config with { RecommendedEnergyShare = Config.RecommendedEnergyShare with { Fat = new ShareRange(0.1, double.NaN) } }, "RecommendedEnergyShare" },
    };

    [Theory]
    [MemberData(nameof(InvalidConfigs))]
    public void An_unusable_value_is_rejected_when_the_calculator_is_created(NutritionConfig config, string parameterName)
    {
        var error = Should.Throw<ArgumentException>(() => new NutritionCalculator(config, Portions));

        error.ParamName.ShouldBe(parameterName);
    }

    [Fact]
    public void Ranges_on_the_edge_of_the_allowed_interval_are_accepted()
    {
        var edge = Config with
        {
            RecommendedEnergyShare = new RecommendedShares(new ShareRange(0, 0), new ShareRange(1, 1), new ShareRange(0.5, 0.5)),
        };

        Should.NotThrow(() => new NutritionCalculator(edge, Portions));
    }

    [Fact]
    public void An_unusable_portion_config_is_rejected_too()
    {
        Should.Throw<ArgumentException>(() => new NutritionCalculator(Config, Portions with { RiceRoundingGrams = 0 }));
    }
}
