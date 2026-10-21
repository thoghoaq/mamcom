using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace MamCom.Data.Tests;

/// <summary>
/// The rules of the host (Vercel, ADR-029): the addresses of the app draw its page, the files are files, and the caching agrees with the data
/// worker (ADR-020): the version file is always asked again, a bundle by its version never.
/// </summary>
public class VercelConfigTests
{
    private static JsonNode Config() =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "js", "vercel.json")))!;

    private static string? HeaderOf(string source, string key) =>
        Config()["headers"]!.AsArray()
            .SingleOrDefault(h => (string?)h!["source"] == source)?["headers"]!.AsArray()
            .SingleOrDefault(x => (string?)x!["key"] == key)?["value"]?.GetValue<string>();

    /// <summary>Whether Vercel would answer <paramref name="path"/> with the page of the app. Its sources are path-to-regexp; this one is a plain group.</summary>
    private static bool DrawsThePage(string path)
    {
        var rewrite = Config()["rewrites"]!.AsArray().Single()!;
        rewrite["destination"]!.GetValue<string>().ShouldBe("/index.html");
        var source = rewrite["source"]!.GetValue<string>();
        return Regex.IsMatch(path, "^" + source + "$");
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/nha-minh")]
    [InlineData("/mon/canh-chua-ca-loc")]
    [InlineData("/cai-dat")]
    public void An_address_of_the_app_draws_the_page(string path)
    {
        DrawsThePage(path).ShouldBeTrue();
    }

    [Theory]
    [InlineData("/data-bundle.json")]
    [InlineData("/data-version.json")]
    [InlineData("/_framework/dotnet.js")]
    [InlineData("/_content/MamCom.UI/base.css")]
    [InlineData("/images/gao-200.webp")]
    [InlineData("/service-worker.js")]
    [InlineData("/manifest.webmanifest")]
    public void A_file_of_the_app_or_of_the_data_is_never_answered_with_the_page(string path)
    {
        DrawsThePage(path).ShouldBeFalse();
    }

    [Fact]
    public void The_version_file_is_always_asked_again_and_a_bundle_named_by_its_version_is_kept()
    {
        HeaderOf("/data-version.json", "Cache-Control").ShouldBe("no-cache");
        HeaderOf("/data-bundle.json", "Cache-Control").ShouldBe("public, max-age=31536000, immutable");
        HeaderOf("/index.html", "Cache-Control").ShouldBe("no-cache");
        HeaderOf("/service-worker(.*).js", "Cache-Control").ShouldBe("no-cache");
    }

    [Fact]
    public void The_files_a_phone_needs_are_sent_with_their_types()
    {
        HeaderOf("/(.*).wasm", "Content-Type").ShouldBe("application/wasm");
        HeaderOf("/(.*).webmanifest", "Content-Type").ShouldBe("application/manifest+json");
    }
}
