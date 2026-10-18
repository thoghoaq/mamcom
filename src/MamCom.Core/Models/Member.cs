namespace MamCom.Core.Models;

/// <param name="BowlsPerMeal">Bowls of rice per meal, 0.5 to 5 in steps of 0.5 (SPEC §4.1).</param>
/// <param name="Avoid">Ingredient ids or categories this person does not eat, for example <c>bo</c> or <c>hai-san</c>.</param>
public sealed record Member(
    string DisplayName,
    AgeGroup AgeGroup,
    double BowlsPerMeal,
    IReadOnlyList<MemberTag> Tags,
    IReadOnlyList<string> Avoid);
