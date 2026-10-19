using MamCom.Core.Models;

namespace MamCom.Core.Portions;

/// <summary>Turns a household into serving totals and a rice amount (SPEC §4.2).</summary>
public sealed class PortionCalculator
{
    private readonly PortionConfig _config;

    /// <exception cref="ArgumentException">The config has a value the formulas cannot use.</exception>
    public PortionCalculator(PortionConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.Validate();
        _config = config;
    }

    /// <summary>The bowls a new member of <paramref name="ageGroup"/> starts with.</summary>
    public double DefaultBowlsFor(AgeGroup ageGroup) => _config.DefaultBowlsPerMeal.For(ageGroup);

    /// <summary>An empty household gives zero servings and zero rice.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A member's bowls per meal is outside the configured range.</exception>
    public PortionResult Calculate(Household household)
    {
        ArgumentNullException.ThrowIfNull(household);

        var memberServings = new double[household.Members.Count];
        double baseServings = 0, proteinServings = 0, vegServings = 0, totalBowls = 0;

        for (var i = 0; i < memberServings.Length; i++)
        {
            var member = household.Members[i];
            ValidateBowls(member);

            var servings = (_config.ServingsBase + _config.ServingsPerBowl * member.BowlsPerMeal)
                * _config.AgeModifiers.For(member.AgeGroup);

            memberServings[i] = servings;
            baseServings += servings;
            proteinServings += servings * (member.Tags.Contains(MemberTag.MoreProtein) ? _config.MoreProteinFactor : 1);
            vegServings += servings * (member.Tags.Contains(MemberTag.MoreVeg) ? _config.MoreVegFactor : 1);
            totalBowls += member.BowlsPerMeal;
        }

        var rawRiceGrams = CeilToMultiple(
            totalBowls * _config.GramsRawRicePerBowl.For(household.BowlSize),
            _config.RiceRoundingGrams);

        return new PortionResult(memberServings, baseServings, proteinServings, vegServings, totalBowls, rawRiceGrams);
    }

    private void ValidateBowls(Member member)
    {
        // Written so NaN fails the range check as well.
        if (!(member.BowlsPerMeal >= _config.MinBowlsPerMeal && member.BowlsPerMeal <= _config.MaxBowlsPerMeal))
        {
            throw new ArgumentOutOfRangeException(
                nameof(member),
                member.BowlsPerMeal,
                $"Bowls per meal for '{member.DisplayName}' must be between {_config.MinBowlsPerMeal} and {_config.MaxBowlsPerMeal}.");
        }
    }

    /// <summary>Rounds up to a multiple. The rounding to 9 places stops 320.00000000000006 from becoming 330.</summary>
    private static double CeilToMultiple(double value, double multiple) =>
        Math.Ceiling(Math.Round(value / multiple, 9)) * multiple;
}
