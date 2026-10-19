namespace MamCom.Core.Nutrition;

/// <summary>
/// Where the energy of a meal comes from, as fractions of 1 that add up to 1. They are shares of the energy the
/// three macronutrients contribute (4, 9 and 4 kcal per gram), not of the kcal figure on the ingredient data.
/// </summary>
public sealed record EnergyShares(double Protein, double Fat, double Carb);
