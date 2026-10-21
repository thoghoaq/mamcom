using System.Text.Json.Nodes;

namespace MamCom.Data.Tests;

/// <summary>
/// The rules of the host (Azure Static Web Apps, ADR-029): the addresses of the app draw its page, the files are files, and the caching agrees
/// with the data worker (ADR-020): the version file is always asked again, a bundle by its version never.
/// </summary>
public class StaticWebAppConfigTests
{
    private static JsonNode Config() =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "js", "staticwebapp.config.json")))!;

    private static string? CacheOf(string route) =>
        Config()["routes"]!.AsArray().SingleOrDefault(r => (string?)r!["route"] == route)?["headers"]?["cache-control"]?.GetValue<string>();

    [Fact]
    public void An_address_of_the_app_draws_the_page_and_the_files_of_the_app_and_the_data_are_never_answered_with_it()
    {
        var fallback = Config()["navigationFallback"]!;
        var exclude = fallback["exclude"]!.AsArray().Select(e => e!.GetValue<string>()).ToArray();

        fallback["rewrite"]!.GetValue<string>().ShouldBe("/index.html");
        exclude.ShouldContain("/_framework/*");
        exclude.ShouldContain("/_content/*");
        exclude.ShouldContain("/images/*");
        exclude.ShouldContain(e => e.StartsWith("*.{", StringComparison.Ordinal) && e.Contains("json") && e.Contains("wasm") && e.Contains("webp"));
    }

    [Fact]
    public void The_version_file_is_always_asked_again_and_a_bundle_named_by_its_version_is_kept()
    {
        CacheOf("/data-version.json").ShouldBe("no-cache");
        CacheOf("/data-bundle.json").ShouldBe("public, max-age=31536000, immutable");
        CacheOf("/index.html").ShouldBe("no-cache");
        CacheOf("/service-worker*.js").ShouldBe("no-cache");
    }

    [Fact]
    public void The_files_a_phone_needs_are_sent_with_their_types()
    {
        var types = Config()["mimeTypes"]!;

        types[".wasm"]!.GetValue<string>().ShouldBe("application/wasm");
        types[".webmanifest"]!.GetValue<string>().ShouldBe("application/manifest+json");
    }
}
