using System.Text.Json;
using MamCom.Core.Cooking;

namespace MamCom.Core.Tests.Cooking;

public class CookingConfigTests
{
    private static readonly CookingConfig Config = CookingConfig.Parse(TestFiles.Config("cooking-defaults.json"));

    [Fact]
    public void The_shipped_config_file_holds_the_SPEC_5_6_rice_times()
    {
        Config.RiceActiveMinutes.ShouldBe(5);    // vo gạo 5 phút
        Config.RicePassiveMinutes.ShouldBe(30);  // nồi cơm 30 phút
        Config.RiceStepText.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void The_shipped_config_file_is_valid()
    {
        Should.NotThrow(Config.Validate);
    }

    [Fact]
    public void A_missing_value_fails_to_parse()
    {
        var broken = TestFiles.Config("cooking-defaults.json").Replace("\"riceActiveMinutes\"", "\"cơm\"");

        Should.Throw<JsonException>(() => CookingConfig.Parse(broken));
    }

    [Fact]
    public void A_null_document_fails_to_parse()
    {
        Should.Throw<JsonException>(() => CookingConfig.Parse("null"));
    }

    public static TheoryData<CookingConfig, string> InvalidConfigs => new()
    {
        { Config with { RiceActiveMinutes = -1 }, "RiceActiveMinutes" },
        { Config with { RicePassiveMinutes = -1 }, "RicePassiveMinutes" },
        { Config with { RiceStepText = "" }, "RiceStepText" },
        { Config with { RiceStepText = "   " }, "RiceStepText" },
    };

    [Theory]
    [MemberData(nameof(InvalidConfigs))]
    public void An_unusable_value_is_rejected_when_the_scheduler_is_created(CookingConfig config, string parameterName)
    {
        Should.Throw<ArgumentException>(() => new CookPlanScheduler(config)).ParamName.ShouldBe(parameterName);
    }

    [Fact]
    public void Zero_minutes_for_rice_are_allowed()
    {
        Should.NotThrow(() => new CookPlanScheduler(Config with { RiceActiveMinutes = 0, RicePassiveMinutes = 0 }));
    }
}
