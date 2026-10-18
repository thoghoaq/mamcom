namespace MamCom.Core.Tests;

public class SeededRandomSourceTests
{
    // Reference values computed independently with a Python SplitMix64 implementation.
    [Theory]
    [InlineData(0UL, new[] { 0xE220A8397B1DCDAFUL, 0x6E789E6AA1B965F4UL, 0x06C45D188009454FUL })]
    [InlineData(42UL, new[] { 0xBDD732262FEB6E95UL, 0x28EFE333B266F103UL, 0x47526757130F9F52UL })]
    public void NextUInt64_matches_the_SplitMix64_reference_sequence(ulong seed, ulong[] expected)
    {
        var random = new SeededRandomSource(seed);

        var actual = expected.Select(_ => random.NextUInt64()).ToArray();

        actual.ShouldBe(expected);
    }

    [Fact]
    public void Same_seed_gives_the_same_sequence()
    {
        var first = new SeededRandomSource(2026);
        var second = new SeededRandomSource(2026);

        for (var i = 0; i < 1000; i++)
        {
            first.NextInt(97).ShouldBe(second.NextInt(97));
            first.NextDouble().ShouldBe(second.NextDouble());
        }
    }

    [Fact]
    public void Different_seeds_give_different_sequences()
    {
        var first = new SeededRandomSource(1);
        var second = new SeededRandomSource(2);

        var firstValues = Enumerable.Range(0, 20).Select(_ => first.NextUInt64()).ToArray();
        var secondValues = Enumerable.Range(0, 20).Select(_ => second.NextUInt64()).ToArray();

        firstValues.ShouldNotBe(secondValues);
    }

    [Fact]
    public void NextDouble_stays_in_the_half_open_unit_interval_and_is_centred()
    {
        var random = new SeededRandomSource(7);
        var values = Enumerable.Range(0, 10_000).Select(_ => random.NextDouble()).ToArray();

        values.ShouldAllBe(v => v >= 0.0 && v < 1.0);
        values.Average().ShouldBe(0.5, tolerance: 0.02);
    }

    [Fact]
    public void NextInt_covers_every_value_in_range_and_never_leaves_it()
    {
        var random = new SeededRandomSource(11);
        var seen = new HashSet<int>();

        for (var i = 0; i < 10_000; i++)
        {
            var value = random.NextInt(7);
            value.ShouldBeInRange(0, 6);
            seen.Add(value);
        }

        seen.Count.ShouldBe(7);
    }

    [Fact]
    public void NextInt_is_close_to_uniform()
    {
        var random = new SeededRandomSource(13);
        var counts = new int[3];

        for (var i = 0; i < 30_000; i++)
        {
            counts[random.NextInt(3)]++;
        }

        counts.ShouldAllBe(c => c > 9_500 && c < 10_500);
    }

    [Fact]
    public void NextInt_of_one_is_always_zero()
    {
        var random = new SeededRandomSource(5);

        Enumerable.Range(0, 100).ShouldAllBe(_ => random.NextInt(1) == 0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NextInt_rejects_a_non_positive_bound(int maxExclusive)
    {
        var random = new SeededRandomSource(5);

        Should.Throw<ArgumentOutOfRangeException>(() => random.NextInt(maxExclusive));
    }
}
