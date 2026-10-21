using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using MamCom.Core.Data;
using MamCom.Data.Bundles;

namespace MamCom.Data.Tests;

/// <summary>
/// The service worker is JavaScript, so its rules for the data package (service-worker.data.js) are tested in Node, in tests/js,
/// and this runs them as part of <c>dotnet test</c>. Where Node is not installed the run is skipped and says so; the CI machines have it.
/// </summary>
public partial class ServiceWorkerTests
{
    private static readonly string JsFolder = Path.Combine(AppContext.BaseDirectory, "js");

    [Fact]
    public async Task The_rules_for_the_data_package_pass_their_tests_in_node()
    {
        var versionLine = await Run("--version");
        Assert.SkipWhen(versionLine is null, "Node.js is not installed on this machine, so the service worker tests did not run.");

        // The files are named, not globbed or given as a folder: those work differently in the Node versions in use (20 to 26).
        var tests = Directory.GetFiles(JsFolder, "*.test.mjs").Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal).ToArray();
        tests.ShouldNotBeEmpty();

        var result = await Run(["--test", .. tests]);

        result.ShouldNotBeNull();
        result.ExitCode.ShouldBe(0, "node --test failed:\n" + result.Output);
        result.Output.ShouldContain("pass ");
        result.Output.ShouldContain("fail 0");
    }

    [Fact]
    public void The_names_of_the_data_package_are_the_same_in_the_script_and_in_core()
    {
        var script = File.ReadAllText(Path.Combine(JsFolder, "service-worker.data.js"));

        var files = Files().Match(script);
        files.Success.ShouldBeTrue("service-worker.data.js has no FILES line");
        files.Groups["bundle"].Value.ShouldBe(BundleFiles.Bundle);
        files.Groups["version"].Value.ShouldBe(BundleFiles.Version);
        files.Groups["images"].Value.ShouldBe(BundleFiles.ImagesFolder + "/");
    }

    [Fact]
    public void The_header_that_marks_a_preload_is_the_same_in_the_script_and_in_the_app()
    {
        var script = File.ReadAllText(Path.Combine(JsFolder, "service-worker.data.js"));

        var header = Preload().Match(script);
        header.Success.ShouldBeTrue("service-worker.data.js has no PRELOAD_HEADER line");
        header.Groups["name"].Value.ShouldBe(ThumbPreloader.PreloadHeader);
    }

    [Fact]
    public void The_published_worker_uses_the_data_rules_and_keeps_the_data_out_of_the_precache()
    {
        var worker = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "js", "service-worker.published.js"));

        worker.ShouldContain("importScripts('./service-worker.data.js')");
        worker.ShouldContain("MamComData.belongsToApp(asset.url)");
        worker.ShouldContain("MamComData.classify(event.request.url, self.registration.scope)");
        worker.ShouldContain("MamComData.handle(");
        worker.ShouldContain("MamComData.DATA_CACHE");
    }

    [Fact]
    public void Installing_the_worker_also_warms_the_data_so_the_first_visit_is_enough_for_offline_use()
    {
        var worker = File.ReadAllText(Path.Combine(JsFolder, "service-worker.published.js"));

        var install = worker[worker.IndexOf("async function onInstall", StringComparison.Ordinal)..worker.IndexOf("async function onActivate", StringComparison.Ordinal)];
        install.ShouldContain("MamComData.warm(");
        install.ShouldContain("self.registration.scope");
        install.ShouldContain("Promise.all([precache, warm])");   // beside the app's precache, and both must have finished
    }

    [Fact]
    public void The_start_page_loads_the_device_script_before_blazor_so_the_app_can_buzz_and_ask_about_movement()
    {
        var page = File.ReadAllText(Path.Combine(JsFolder, "index.html"));

        var device = page.IndexOf("<script src=\"device.js\"></script>", StringComparison.Ordinal);
        var blazor = page.IndexOf("_framework/blazor.webassembly", StringComparison.Ordinal);

        device.ShouldBeGreaterThan(0);
        blazor.ShouldBeGreaterThan(device);
    }

    [Fact]
    public void The_start_page_does_not_preload_the_runtime_because_the_worker_answers_for_it_and_chromium_then_warns_that_the_preload_was_not_used()
    {
        var page = File.ReadAllText(Path.Combine(JsFolder, "index.html"));

        page.ShouldNotContain("rel=\"preload\"");
    }

    [Fact]
    public void The_start_page_names_its_own_icon_so_the_browser_does_not_ask_for_a_favicon_that_is_not_there()
    {
        var page = File.ReadAllText(Path.Combine(JsFolder, "index.html"));

        page.ShouldContain("<link rel=\"icon\" type=\"image/png\" sizes=\"192x192\" href=\"icon-192.png\" />");
    }

    private sealed record NodeResult(int ExitCode, string Output);

    /// <summary>Runs node with the arguments; null when there is no node to run.</summary>
    private static async Task<NodeResult?> Run(params string[] arguments)
    {
        var start = new ProcessStartInfo("node") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, WorkingDirectory = JsFolder };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
            var error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
            await process.WaitForExitAsync(TestContext.Current.CancellationToken);
            return new NodeResult(process.ExitCode, await output + await error);
        }
        catch (Win32Exception)
        {
            return null;
        }
    }

    [GeneratedRegex(@"FILES\s*=\s*\{\s*bundle:\s*'(?<bundle>[^']+)',\s*version:\s*'(?<version>[^']+)',\s*images:\s*'(?<images>[^']+)'\s*\}")]
    private static partial Regex Files();

    [GeneratedRegex(@"PRELOAD_HEADER\s*=\s*'(?<name>[^']+)'")]
    private static partial Regex Preload();
}
