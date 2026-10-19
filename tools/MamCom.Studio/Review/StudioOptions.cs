namespace MamCom.Studio.Review;

/// <summary>Settings of Studio, from the <c>Studio</c> section of the configuration (appsettings, user-secrets, environment).</summary>
public sealed class StudioOptions
{
    public const string Section = "Studio";

    /// <summary>The <c>data</c> folder to work on. Empty: the nearest <c>data</c> folder (with <c>schemas</c> inside) above the current folder.</summary>
    public string? DataFolder { get; set; }

    /// <summary>The name written to <c>reviewedBy</c> when a dish is approved. Empty: the reviewer types it in.</summary>
    public string? Reviewer { get; set; }
}
