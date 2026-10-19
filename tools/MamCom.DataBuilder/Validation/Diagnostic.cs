namespace MamCom.DataBuilder.Validation;

public enum Severity
{
    Warning,
    Error,
}

/// <summary>Development builds let placeholder data and unreviewed dishes through; release builds do not and treat warnings as errors.</summary>
public enum ValidationMode
{
    Dev,
    Release,
}

/// <summary>One finding about the data.</summary>
/// <param name="Code">Stable id of the rule, see <see cref="DiagnosticCodes"/>.</param>
/// <param name="File">Path of the file under the data folder, with forward slashes; empty when no file is to blame.</param>
/// <param name="Location">JSON Pointer inside the file; empty for the file as a whole.</param>
public sealed record Diagnostic(Severity Severity, string Code, string File, string Location, string Message);

public static class DiagnosticCodes
{
    // Reading the data folder
    public const string DataFolder = "data-folder";
    public const string SchemaMissing = "schema-missing";
    public const string Schema = "schema";
    public const string Parse = "parse";
    public const string ConfigMissing = "config-missing";
    public const string ConfigInvalid = "config-invalid";

    // Errors across files (SPEC 7.3)
    public const string FileName = "file-name";
    public const string DuplicateId = "duplicate-id";
    public const string UnknownIngredient = "unknown-ingredient";
    public const string UnknownSeason = "unknown-season";
    public const string StepOrder = "step-order";
    public const string DuplicateLine = "duplicate-line";
    public const string ImageMissing = "image-missing";
    public const string ImagePath = "image-path";
    public const string RiceMissing = "rice-missing";

    // Release only
    public const string ReleasePlaceholder = "release-placeholder";
    public const string ReleaseStatus = "release-status";

    // Warnings (errors in a release build)
    public const string KcalRange = "kcal-range";
    public const string ProteinRange = "protein-range";
    public const string SodiumHigh = "sodium-high";
    public const string UnusedIngredient = "unused-ingredient";
    public const string LineNotInSteps = "line-not-in-steps";
    public const string StepIngredientNotInRecipe = "step-ingredient-not-in-recipe";
    public const string ApproxOnWeightUnit = "approx-on-weight-unit";
}
