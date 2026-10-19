using System.Text.Json;
using MamCom.Core.Models;
using MamCom.DataBuilder.Validation;

namespace MamCom.DataBuilder.Tests.Validation;

public class ValidationConfigTests
{
    private static string Shipped => File.ReadAllText(Path.Combine(Tests.Schemas.DataFiles.Root, "config", "validation-defaults.json"));

    private static readonly ValueRange Fine = new(10, 20);

    private static ValidationConfig Config(
        ValueRange? canh = null, ValueRange? man = null, ValueRange? rau = null, ValueRange? protein = null, double sodium = 1000) =>
        new(new SlotRanges(canh ?? Fine, man ?? Fine, rau ?? Fine), protein ?? Fine, sodium);

    [Fact]
    public void The_shipped_file_has_the_numbers_of_the_spec()
    {
        var config = ValidationConfig.Parse(Shipped);

        config.KcalPerServing.Canh.ShouldBe(new ValueRange(30, 250));
        config.KcalPerServing.Man.ShouldBe(new ValueRange(120, 550));
        config.KcalPerServing.Rau.ShouldBe(new ValueRange(20, 220));
        config.MainProteinGramsPerServing.ShouldBe(new ValueRange(50, 150));
        config.MaxSodiumMgPerServing.ShouldBe(1000);
        Should.NotThrow(config.Validate);
    }

    [Fact]
    public void Each_slot_gets_its_own_range()
    {
        var config = Config(canh: new ValueRange(1, 2), man: new ValueRange(3, 4), rau: new ValueRange(5, 6));

        config.KcalPerServing.For(DishSlot.Soup).ShouldBe(new ValueRange(1, 2));
        config.KcalPerServing.For(DishSlot.Main).ShouldBe(new ValueRange(3, 4));
        config.KcalPerServing.For(DishSlot.Vegetable).ShouldBe(new ValueRange(5, 6));
        Should.Throw<ArgumentOutOfRangeException>(() => config.KcalPerServing.For((DishSlot)99));
    }

    [Theory]
    [InlineData(9.9, false)]
    [InlineData(10.0, true)]
    [InlineData(15.0, true)]
    [InlineData(20.0, true)]
    [InlineData(20.1, false)]
    public void A_range_includes_both_of_its_ends(double value, bool inside)
    {
        Fine.Contains(value).ShouldBe(inside);
    }

    // ---- reading -------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{ not json")]
    [InlineData("{}")]
    public void Text_that_is_not_the_config_is_a_JSON_error(string json)
    {
        Should.Throw<JsonException>(() => ValidationConfig.Parse(json));
    }

    [Fact]
    public void An_unknown_member_is_an_error_not_ignored()
    {
        var json = Shipped.Replace("\"maxSodiumMgPerServing\": 1000", "\"maxSodiumMgPerServing\": 1000,\n  \"maxSugar\": 5");

        Should.Throw<JsonException>(() => ValidationConfig.Parse(json));
    }

    [Fact]
    public void A_missing_member_is_an_error_not_a_default()
    {
        var json = Shipped.Replace("\"maxSodiumMgPerServing\"", "\"maxSodium\"");

        Should.Throw<JsonException>(() => ValidationConfig.Parse(json));
    }

    [Fact]
    public void A_member_written_twice_is_an_error()
    {
        var json = Shipped.Replace("\"maxSodiumMgPerServing\": 1000", "\"maxSodiumMgPerServing\": 1000,\n  \"maxSodiumMgPerServing\": 2000");

        Should.Throw<JsonException>(() => ValidationConfig.Parse(json));
    }

    [Fact]
    public void Member_names_are_case_sensitive()
    {
        var json = Shipped.Replace("\"min\"", "\"Min\"");

        Should.Throw<JsonException>(() => ValidationConfig.Parse(json));
    }

    // ---- checking the values -------------------------------------------------------------------------------------------

    [Fact]
    public void A_range_whose_ends_are_equal_is_allowed()
    {
        Should.NotThrow(Config(canh: new ValueRange(50, 50)).Validate);
    }

    [Fact]
    public void A_minimum_of_zero_is_allowed()
    {
        Should.NotThrow(Config(rau: new ValueRange(0, 5)).Validate);
    }

    [Theory]
    [InlineData("KcalPerServing", 0)]
    [InlineData("KcalPerServing", 1)]
    [InlineData("KcalPerServing", 2)]
    [InlineData("MainProteinGramsPerServing", 3)]
    public void A_range_with_its_minimum_above_its_maximum_is_rejected_and_names_the_field(string field, int which)
    {
        var bad = new ValueRange(30, 20);
        var config = which switch
        {
            0 => Config(canh: bad),
            1 => Config(man: bad),
            2 => Config(rau: bad),
            _ => Config(protein: bad),
        };

        Should.Throw<ArgumentException>(config.Validate).ParamName.ShouldBe(field);
    }

    [Theory]
    [InlineData(-1.0, 5.0)]
    [InlineData(double.NaN, 5.0)]
    [InlineData(1.0, double.NaN)]
    [InlineData(1.0, double.PositiveInfinity)]
    public void A_range_with_a_negative_or_non_finite_end_is_rejected(double min, double max)
    {
        Should.Throw<ArgumentException>(Config(canh: new ValueRange(min, max)).Validate);
        Should.Throw<ArgumentException>(Config(protein: new ValueRange(min, max)).Validate);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-5.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void A_sodium_limit_that_is_not_a_positive_number_is_rejected(double sodium)
    {
        Should.Throw<ArgumentException>(Config(sodium: sodium).Validate).ParamName.ShouldBe("MaxSodiumMgPerServing");
    }
}
