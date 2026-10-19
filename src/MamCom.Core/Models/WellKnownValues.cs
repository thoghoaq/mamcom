namespace MamCom.Core.Models;

/// <summary>Values SPEC gives a fixed meaning to inside otherwise data-defined string sets (protein sources).</summary>
public static class ProteinSources
{
    /// <summary>No protein source; never counts as a duplicate.</summary>
    public const string None = "none";

    /// <summary>Vegetarian protein (chay); never counts as a duplicate.</summary>
    public const string Vegetarian = "chay";
}

public static class SeasonIds
{
    /// <summary>The dish suits every season.</summary>
    public const string All = "all";
}

public static class NutritionDbs
{
    /// <summary>Nutrition values not yet taken from a referenced source. Dev only; release builds fail on it.</summary>
    public const string Placeholder = "placeholder";
}

public static class RegionKeys
{
    /// <summary>Key of a region-keyed object (<c>names</c>, <c>marketUnit</c>) that applies to any region without a key of its own.</summary>
    public const string Default = "default";
}
