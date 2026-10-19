using MamCom.Core.Models;
using MamCom.Core.Shopping;
using static MamCom.Core.Tests.Shopping.ShoppingFixtures;

namespace MamCom.Core.Tests.Shopping;

public class ShoppingTextFormatterTests
{
    private static readonly DateOnly Sunday18Oct = new(2026, 10, 18);

    private static ShoppingTextFormatter Formatter() => new();

    private static ShoppingItem Entry(string name, double quantity, string unit, Aisle aisle = Aisle.Other, bool pantry = false, double? approx = null) =>
        new(name, name, aisle, quantity, unit, 0, pantry, approx);

    // ---- golden: the SPEC §6.2 example --------------------------------------------------------

    [Fact]
    public void North_text_is_exactly_the_SPEC_example()
    {
        var text = Formatter().Format(Build(Region.North), Sunday18Oct, memberCount: 4);

        text.ShouldBe(
            "Đi chợ tối CN 18/10 (4 người)\n" +
            "THỊT CÁ: 3 lạng thịt ba chỉ · 1 con cá lóc (~500g)\n" +
            "RAU: 1 mớ rau muống · 2 quả cà chua · 1 mớ bạc hà\n" +
            "KHÁC: 4 quả trứng");
    }

    [Fact]
    public void South_text_has_grams_and_the_regional_name_and_nothing_else_changes()
    {
        var text = Formatter().Format(Build(Region.South), Sunday18Oct, memberCount: 4);

        text.ShouldBe(
            "Đi chợ tối CN 18/10 (4 người)\n" +
            "THỊT CÁ: 300 g thịt ba rọi · 1 con cá lóc (~500g)\n" +
            "RAU: 1 mớ rau muống · 2 quả cà chua · 1 mớ bạc hà\n" +
            "KHÁC: 4 quả trứng");
    }

    [Fact]
    public void Showing_pantry_items_adds_them_to_their_heading()
    {
        var text = Formatter().Format(Build(Region.North), Sunday18Oct, 4, includePantry: true);

        text.ShouldEndWith("KHÁC: 4 quả trứng · 0,5 kg gạo");
        text.Split('\n').Length.ShouldBe(4);
    }

    // ---- header ------------------------------------------------------------------------------

    [Theory]
    [InlineData(2026, 10, 19, "T2 19/10")]
    [InlineData(2026, 10, 20, "T3 20/10")]
    [InlineData(2026, 10, 21, "T4 21/10")]
    [InlineData(2026, 10, 22, "T5 22/10")]
    [InlineData(2026, 10, 23, "T6 23/10")]
    [InlineData(2026, 10, 24, "T7 24/10")]
    [InlineData(2026, 10, 25, "CN 25/10")]
    [InlineData(2026, 1, 4, "CN 4/1")]    // no leading zeros
    [InlineData(2026, 12, 7, "T2 7/12")]
    public void The_header_names_the_weekday_and_writes_the_date_without_leading_zeros(int year, int month, int day, string expected)
    {
        var header = Formatter().Format([], new DateOnly(year, month, day), 3);

        header.ShouldBe($"Đi chợ tối {expected} (3 người)");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(12)]
    public void The_head_count_is_written_as_given(int count)
    {
        Formatter().Format([], Sunday18Oct, count).ShouldBe($"Đi chợ tối CN 18/10 ({count} người)");
    }

    // ---- items -------------------------------------------------------------------------------

    [Theory]
    [InlineData(3.0, "3")]
    [InlineData(2.5, "2,5")]
    [InlineData(0.5, "0,5")]
    [InlineData(0.75, "0,75")]
    [InlineData(0.333, "0,33")]
    [InlineData(1000.0, "1000")]
    [InlineData(1234.5, "1234,5")]
    [InlineData(300.0, "300")]
    public void Quantities_use_a_decimal_comma_and_no_trailing_zeros(double quantity, string expected)
    {
        var text = Formatter().Format([Entry("Thịt", quantity, "g")], Sunday18Oct, 1);

        text.ShouldEndWith($"KHÁC: {expected} g thịt");
    }

    [Fact]
    public void Names_are_lower_cased_but_keep_their_diacritics()
    {
        var text = Formatter().Format([Entry("RAU Muống Xào", 1, "mớ", Aisle.Produce)], Sunday18Oct, 1);

        text.ShouldEndWith("RAU: 1 mớ rau muống xào");
    }

    [Theory]
    [InlineData(500.0, "(~500g)")]
    [InlineData(333.6, "(~334g)")]
    [InlineData(1200.0, "(~1200g)")]
    public void Approximate_grams_follow_the_name_in_brackets_without_a_space_before_the_g(double grams, string expected)
    {
        var text = Formatter().Format([Entry("Cá", 1, "con", Aisle.MeatAndFish, approx: grams)], Sunday18Oct, 1);

        text.ShouldEndWith($"THỊT CÁ: 1 con cá {expected}");
    }

    [Fact]
    public void Items_without_approximate_grams_have_no_brackets()
    {
        var text = Formatter().Format([Entry("Cá", 1, "con", Aisle.MeatAndFish)], Sunday18Oct, 1);

        text.ShouldNotContain("(~");
    }

    // ---- headings ----------------------------------------------------------------------------

    [Fact]
    public void Eggs_dry_goods_and_the_rest_share_one_KHAC_line_in_aisle_order()
    {
        var items = new[]
        {
            Entry("Khác", 1, "cái", Aisle.Other),
            Entry("Muối", 1, "gói", Aisle.DryGoodsAndSpices),
            Entry("Trứng", 4, "quả", Aisle.EggsAndTofu),
        };

        var text = Formatter().Format(items.OrderBy(i => i.Aisle).ToArray(), Sunday18Oct, 2);

        text.Split('\n').Skip(1).ShouldBe(["KHÁC: 4 quả trứng · 1 gói muối · 1 cái khác"]);
    }

    [Fact]
    public void Headings_come_out_in_aisle_order_whatever_the_order_of_the_list()
    {
        var items = new[]
        {
            Entry("Trứng", 4, "quả", Aisle.EggsAndTofu),
            Entry("Rau", 1, "mớ", Aisle.Produce),
            Entry("Thịt", 1, "lạng", Aisle.MeatAndFish),
        };

        var lines = Formatter().Format(items, Sunday18Oct, 2).Split('\n').Skip(1).Select(l => l.Split(':')[0]).ToArray();

        lines.ShouldBe(["THỊT CÁ", "RAU", "KHÁC"]);
    }

    [Fact]
    public void Items_inside_an_aisle_keep_the_order_of_the_list()
    {
        var items = new[] { Entry("B", 1, "x", Aisle.Produce), Entry("A", 1, "x", Aisle.Produce) };

        Formatter().Format(items, Sunday18Oct, 1).ShouldEndWith("RAU: 1 x b · 1 x a");
    }

    // ---- nothing to say ----------------------------------------------------------------------

    [Fact]
    public void An_empty_list_gives_only_the_header()
    {
        Formatter().Format([], Sunday18Oct, 4).ShouldBe("Đi chợ tối CN 18/10 (4 người)");
    }

    [Fact]
    public void A_list_of_only_pantry_items_gives_only_the_header_unless_pantry_is_shown()
    {
        var items = new[] { Entry("Gạo", 1, "kg", Aisle.DryGoodsAndSpices, pantry: true) };

        Formatter().Format(items, Sunday18Oct, 4).ShouldBe("Đi chợ tối CN 18/10 (4 người)");
        Formatter().Format(items, Sunday18Oct, 4, includePantry: true).ShouldContain("KHÁC: 1 kg gạo");
    }

    [Fact]
    public void The_text_has_no_trailing_newline_and_no_carriage_returns()
    {
        var text = Formatter().Format(Build(Region.North), Sunday18Oct, 4);

        text.ShouldNotEndWith("\n");
        text.ShouldNotContain("\r");
    }

    // ---- labels ------------------------------------------------------------------------------

    [Fact]
    public void Every_word_comes_from_the_labels()
    {
        var labels = new ShareTextLabels(
            Shopping: "Shop",
            Meal: "lunch",
            People: "people",
            Weekdays: ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"],
            Headings: new AisleHeadings("MEAT", "VEG", "EGGS", "DRY", "OTHER"));
        var items = new[]
        {
            Entry("Thịt", 1, "kg", Aisle.MeatAndFish),
            Entry("Trứng", 1, "quả", Aisle.EggsAndTofu),
            Entry("Muối", 1, "gói", Aisle.DryGoodsAndSpices),
        };

        var text = new ShoppingTextFormatter(labels).Format(items, Sunday18Oct, 5);

        text.ShouldBe("Shop lunch Sun 18/10 (5 people)\nMEAT: 1 kg thịt\nEGGS: 1 quả trứng\nDRY: 1 gói muối");
    }

    [Fact]
    public void Weekday_labels_must_have_seven_entries()
    {
        var labels = ShareTextLabels.Vietnamese with { Weekdays = ["CN", "T2"] };

        Should.Throw<ArgumentException>(() => new ShoppingTextFormatter(labels));
    }

    [Fact]
    public void Undefined_aisles_are_rejected_by_the_headings()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => ShareTextLabels.Vietnamese.Headings.For((Aisle)99));
    }

    [Fact]
    public void Null_items_are_rejected()
    {
        Should.Throw<ArgumentNullException>(() => Formatter().Format(null!, Sunday18Oct, 1));
    }
}
