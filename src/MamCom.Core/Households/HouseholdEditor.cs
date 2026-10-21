using MamCom.Core.Models;
using MamCom.Core.Portions;

namespace MamCom.Core.Households;

/// <summary>One person on the first screen. <see cref="Id"/> is stable while the screen is open, so the page can tell the cards apart.</summary>
/// <param name="Tags">Kept as they were when an existing household is edited; the first screen does not ask for them (SPEC §8.1-1).</param>
/// <param name="Avoid">Kept as they were; the first screen does not ask for them.</param>
public sealed record DraftMember(
    int Id,
    string Name,
    AgeGroup AgeGroup,
    double BowlsPerMeal,
    IReadOnlyList<MemberTag> Tags,
    IReadOnlyList<string> Avoid);

/// <summary>
/// The household as it is being made on the first screen (SPEC §8.1-1): people are added by role, each with the question that
/// matters, "how many bowls of rice?", and nothing else is asked. Every bowl value stays on the grid and within the range of
/// <see cref="PortionConfig"/>, so what comes out is always something <see cref="PortionCalculator"/> accepts.
/// </summary>
public sealed class HouseholdEditor
{
    /// <summary>The most people a household can have. A limit of the screen, not of the maths: twelve cards are already a long page.</summary>
    public const int MaxMembers = 12;

    /// <summary>The longest name kept. Longer text is cut, so a card never has to wrap a paragraph.</summary>
    public const int MaxNameLength = 30;

    /// <summary>The region a new household starts with when the person does not choose (SPEC §13 leaves the launch region open).</summary>
    public const Region DefaultRegion = Region.North;

    private readonly PortionConfig _config;
    private readonly List<DraftMember> _members = [];
    private int _nextId = 1;

    public HouseholdEditor(PortionConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.Validate();
        _config = config;
    }

    public IReadOnlyList<DraftMember> Members => _members;

    public Region Region { get; set; } = DefaultRegion;

    public BowlSize BowlSize { get; set; } = BowlSize.Medium;

    /// <summary>The stoves of the kitchen; null until the household says (the config then has the usual number, ADR-028).</summary>
    public int? Stoves { get; set; }

    public bool CanAdd => _members.Count < MaxMembers;

    public bool IsComplete => _members.Count > 0;

    /// <summary>The household the person is starting from when they skip: a father and a mother, with the usual bowls.</summary>
    public static HouseholdEditor Default(PortionConfig config, Func<MemberRole, string> nameOf)
    {
        var editor = new HouseholdEditor(config);
        editor.Add(MemberRole.Father, nameOf(MemberRole.Father));
        editor.Add(MemberRole.Mother, nameOf(MemberRole.Mother));
        return editor;
    }

    /// <summary>
    /// Starts from a household that was saved, keeping tags and things to avoid. Returns null when it is not usable (see
    /// <see cref="IsUsable"/>), for example after the file was edited by hand.
    /// </summary>
    public static HouseholdEditor? From(Household household, PortionConfig config)
    {
        ArgumentNullException.ThrowIfNull(household);

        if (!IsUsable(household, config))
        {
            return null;
        }

        var editor = new HouseholdEditor(config) { Region = household.Region, BowlSize = household.BowlSize, Stoves = household.Stoves };
        foreach (var member in household.Members)
        {
            editor._members.Add(new DraftMember(editor._nextId++, member.DisplayName.Trim(), member.AgeGroup, member.BowlsPerMeal, member.Tags, member.Avoid));
        }

        return editor;
    }

    /// <summary>
    /// Whether a household that came back from storage can be used as it is: one to <see cref="MaxMembers"/> people, each with a
    /// name and a bowl count the calculator accepts, and a region and bowl size that exist.
    /// </summary>
    public static bool IsUsable(Household household, PortionConfig config)
    {
        ArgumentNullException.ThrowIfNull(household);
        ArgumentNullException.ThrowIfNull(config);

        return household.Members is { Count: > 0 and <= MaxMembers }
            && Enum.IsDefined(household.Region)
            && Enum.IsDefined(household.BowlSize)
            && household.Stoves is null or >= 1
            && household.Members.All(m =>
                m is not null
                && !string.IsNullOrWhiteSpace(m.DisplayName)
                && Enum.IsDefined(m.AgeGroup)
                && m.Tags is not null
                && m.Avoid is not null
                && m.BowlsPerMeal >= config.MinBowlsPerMeal   // written so NaN fails both
                && m.BowlsPerMeal <= config.MaxBowlsPerMeal);
    }

    /// <summary>Adds a person with the role's age group and the usual bowls for it. A name already taken gets a number: "Con trai 2".</summary>
    /// <returns>The new person, or null when the household is full.</returns>
    public DraftMember? Add(MemberRole role, string baseName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseName);

        if (!CanAdd)
        {
            return null;
        }

        var age = role.DefaultAgeGroup();
        var member = new DraftMember(_nextId++, FreeName(Clean(baseName)), age, _config.DefaultBowlsPerMeal.For(age), [], []);
        _members.Add(member);
        return member;
    }

    public bool Remove(int id) => _members.RemoveAll(m => m.Id == id) > 0;

    /// <summary>Takes <paramref name="steps"/> steps off (negative) or puts them on, stopping at the smallest and largest number of bowls.</summary>
    /// <returns>The bowls now, or null when there is no such person.</returns>
    public double? StepBowls(int id, int steps)
    {
        var index = IndexOf(id);
        if (index < 0)
        {
            return null;
        }

        var bowls = OnGrid(_members[index].BowlsPerMeal + steps * _config.BowlsStep);
        _members[index] = _members[index] with { BowlsPerMeal = bowls };
        return bowls;
    }

    public bool CanStep(int id, int direction)
    {
        var index = IndexOf(id);
        if (index < 0)
        {
            return false;
        }

        var bowls = _members[index].BowlsPerMeal;
        return direction < 0 ? bowls > _config.MinBowlsPerMeal : bowls < _config.MaxBowlsPerMeal;
    }

    /// <summary>
    /// Changes the age group. Bowls the person has not touched (still the usual number for the old group) follow to the usual number
    /// for the new group; bowls they chose themselves stay.
    /// </summary>
    public bool SetAgeGroup(int id, AgeGroup ageGroup)
    {
        var index = IndexOf(id);
        if (index < 0 || !Enum.IsDefined(ageGroup))
        {
            return false;
        }

        var member = _members[index];
        var untouched = member.BowlsPerMeal == _config.DefaultBowlsPerMeal.For(member.AgeGroup);
        _members[index] = member with
        {
            AgeGroup = ageGroup,
            BowlsPerMeal = untouched ? _config.DefaultBowlsPerMeal.For(ageGroup) : member.BowlsPerMeal,
        };
        return true;
    }

    /// <summary>Renames a person. Blank text keeps the old name; a name another person has gets a number.</summary>
    public bool Rename(int id, string? name)
    {
        var index = IndexOf(id);
        var clean = Clean(name);
        if (index < 0 || clean.Length == 0)
        {
            return false;
        }

        var others = _members.Where(m => m.Id != id).Select(m => m.Name);
        _members[index] = _members[index] with { Name = FreeName(clean, others) };
        return true;
    }

    /// <exception cref="InvalidOperationException">Nobody has been added: a household needs at least one person.</exception>
    public Household ToHousehold()
    {
        if (!IsComplete)
        {
            throw new InvalidOperationException("A household needs at least one person.");
        }

        return new Household(
            Region,
            BowlSize,
            [.. _members.Select(m => new Member(m.Name, m.AgeGroup, m.BowlsPerMeal, m.Tags, m.Avoid))],
            Stoves: Stoves);
    }

    private int IndexOf(int id) => _members.FindIndex(m => m.Id == id);

    private static string Clean(string? name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        return trimmed.Length <= MaxNameLength ? trimmed : trimmed[..MaxNameLength].TrimEnd();
    }

    private string FreeName(string name) => FreeName(name, _members.Select(m => m.Name));

    private static string FreeName(string name, IEnumerable<string> taken)
    {
        var used = new HashSet<string>(taken, StringComparer.OrdinalIgnoreCase);
        if (!used.Contains(name))
        {
            return name;
        }

        for (var n = 2; ; n++)
        {
            var suffix = " " + n;
            var candidate = (name.Length + suffix.Length <= MaxNameLength ? name : name[..(MaxNameLength - suffix.Length)].TrimEnd()) + suffix;
            if (!used.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    /// <summary>The number of bowls clamped to the range and set on the grid, so repeated steps never drift.</summary>
    private double OnGrid(double bowls)
    {
        var clamped = Math.Clamp(bowls, _config.MinBowlsPerMeal, _config.MaxBowlsPerMeal);
        var onGrid = Math.Round((clamped - _config.MinBowlsPerMeal) / _config.BowlsStep, MidpointRounding.AwayFromZero) * _config.BowlsStep + _config.MinBowlsPerMeal;
        return Math.Min(onGrid, _config.MaxBowlsPerMeal);
    }
}
