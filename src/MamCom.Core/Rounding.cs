namespace MamCom.Core;

internal static class Rounding
{
    /// <summary>
    /// Rounds up to a multiple. Rounding the quotient to 9 places first stops binary noise such as
    /// 3 × 0.1 = 0.30000000000000004 from jumping to the next multiple.
    /// </summary>
    public static double CeilToMultiple(double value, double multiple) =>
        Math.Ceiling(Math.Round(value / multiple, 9)) * multiple;
}
