using System.Diagnostics;
using System.Reflection;
using MamCom.DataBuilder.Cli;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

[assembly: AssemblyFixture(typeof(MamCom.E2E.SiteFixture))]

namespace MamCom.E2E;

/// <summary>
/// The app as a person gets it, once for the whole run: the published Blazor app with a bundle made from the frozen seed (ADR-018) next to it,
/// served over HTTP from a folder in the temp directory, and a browser to open it with (PLAN 3.9, SPEC §10).
/// </summary>
/// <remarks>
/// <c>MAMCOM_E2E_SITE</c> names a folder that is already published and has a bundle, and skips both steps (for working on the tests).
/// <c>MAMCOM_E2E_BROWSER</c> names the browser: <c>chromium</c> (the one <c>playwright install</c> fetches), <c>msedge</c> or <c>chrome</c> (the one on the
/// machine). Without it the three are tried in that order, so a machine that never ran <c>playwright install</c> still has a browser.
/// </remarks>
public sealed class SiteFixture : IAsyncLifetime
{
    private readonly List<string> _browserFailures = [];
    private string? _temp;
    private WebApplication? _server;
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    /// <summary>The width of the phone every test opens the app on, in CSS pixels, and its height (SPEC §10: 360×740).</summary>
    public const int PhoneWidth = 360;

    public const int PhoneHeight = 740;

    /// <summary>The address of the site, without a trailing slash.</summary>
    public string BaseUrl { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        var site = Environment.GetEnvironmentVariable("MAMCOM_E2E_SITE");
        if (string.IsNullOrWhiteSpace(site))
        {
            _temp = Path.Combine(Path.GetTempPath(), "mamcom-e2e-" + Guid.NewGuid().ToString("N"));
            site = await PrepareSiteAsync(_temp);
        }

        _server = StartServer(site);
        BaseUrl = _server.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First().TrimEnd('/');

        _playwright = await Playwright.CreateAsync();
        _browser = await LaunchAsync(_playwright);
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.CloseAsync();
        }

        _playwright?.Dispose();

        if (_server is not null)
        {
            await _server.StopAsync();
            await _server.DisposeAsync();
        }

        if (_temp is not null && Directory.Exists(_temp))
        {
            try
            {
                Directory.Delete(_temp, recursive: true);
            }
            catch (IOException)
            {
                // a browser may still hold a file for a moment; the temp folder is not worth failing a run for
            }
        }
    }

    /// <summary>A new browser context of a phone, 360×740, with a page in it. Each one starts with no storage, as a first visit does.</summary>
    public async Task<AppSession> OpenAsync(ColorScheme colorScheme = ColorScheme.Light)
    {
        if (_browser is null)
        {
            var reason = "No browser to run the end-to-end tests with: " + string.Join("; ", _browserFailures);
            if (Environment.GetEnvironmentVariable("CI") is { Length: > 0 })
            {
                throw new InvalidOperationException(reason);
            }

            Assert.Skip(reason + ". Run `playwright install chromium` or install Edge or Chrome.");
        }

        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = PhoneWidth, Height = PhoneHeight },
            DeviceScaleFactor = 2,
            IsMobile = true,
            HasTouch = true,
            Locale = "vi-VN",
            TimezoneId = "Asia/Ho_Chi_Minh",
            ColorScheme = colorScheme,
            BaseURL = BaseUrl,
        });
        context.SetDefaultTimeout(20_000);

        return new AppSession(context, await context.NewPageAsync(), BaseUrl);
    }

    private async Task<IBrowser?> LaunchAsync(IPlaywright playwright)
    {
        var requested = Environment.GetEnvironmentVariable("MAMCOM_E2E_BROWSER");
        string?[] choices = string.IsNullOrWhiteSpace(requested)
            ? [null, "msedge", "chrome"]
            : [requested is "chromium" ? null : requested];

        foreach (var channel in choices)
        {
            try
            {
                return await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Channel = channel, Headless = true });
            }
            catch (PlaywrightException exception)
            {
                _browserFailures.Add($"{channel ?? "chromium"}: {exception.Message.Split('\n')[0]}");
            }
        }

        return null;
    }

    /// <summary>Publishes the app, copies it, and puts a bundle of the seed beside it. The published output may hold a bundle of the real data from a dev run; it goes.</summary>
    private static async Task<string> PrepareSiteAsync(string temp)
    {
        var root = RepositoryRoot();
        var configuration = typeof(SiteFixture).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration ?? "Debug";
        var published = Path.Combine(temp, "publish");
        var site = Path.Combine(temp, "site");

        // Not --no-build: only a publish that builds resolves the fingerprints in index.html. The project reference of this project has built
        // the app in the configuration the tests were built in, so this is an incremental build and takes seconds.
        await RunAsync("dotnet", ["publish", Path.Combine(root, "src", "MamCom.Web", "MamCom.Web.csproj"), "--configuration", configuration, "--output", published, "--nologo"]);

        CopyDirectory(Path.Combine(published, "wwwroot"), site);
        foreach (var stale in Directory.GetFiles(site, "data-*"))
        {
            File.Delete(stale);
        }

        var images = Path.Combine(site, "images");
        if (Directory.Exists(images))
        {
            Directory.Delete(images, recursive: true);
        }

        using var output = new StringWriter();
        var code = CommandLine.Run(["build", "--data", Path.Combine(AppContext.BaseDirectory, "data"), "--out", site], output, output);
        if (code != 0)
        {
            throw new InvalidOperationException("DataBuilder could not make the bundle of the seed:" + Environment.NewLine + output);
        }

        File.Delete(Path.Combine(site, "data-report.md"));   // the report is not part of what is served (ADR-020)

        // The host keeps its config to itself and answers 404 for it (Vercel, ADR-029); so does this server, or a worker that tries to keep
        // the file would pass here and fail to install on the real site.
        File.Delete(Path.Combine(site, "vercel.json"));
        return site;
    }

    private static WebApplication StartServer(string site)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = site, WebRootPath = site });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        var app = builder.Build();
        var files = new PhysicalFileProvider(site);
        var types = new FileExtensionContentTypeProvider();
        types.Mappings[".webmanifest"] = "application/manifest+json";
        var options = new StaticFileOptions { FileProvider = files, ContentTypeProvider = types, ServeUnknownFileTypes = true };

        app.UseStaticFiles(options);

        // The routes of the app are drawn in the browser: a path that is no file is the page of the app, as Azure Static Web Apps will do it.
        app.MapFallback(async context =>
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.SendFileAsync(files.GetFileInfo("index.html"));
        });

        app.Start();
        return app;
    }

    private static async Task RunAsync(string program, IReadOnlyList<string> arguments)
    {
        var start = new ProcessStartInfo(program) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment["DOTNET_NOLOGO"] = "true";
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "true";

        using var process = Process.Start(start) ?? throw new InvalidOperationException($"Could not start {program}.");
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(6));
        await process.WaitForExitAsync(timeout.Token);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"{program} {string.Join(' ', arguments)} failed ({process.ExitCode}):{Environment.NewLine}{await output}{await errors}");
        }
    }

    private static string RepositoryRoot()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "MamCom.sln")))
            {
                return folder.FullName;
            }
        }

        throw new InvalidOperationException("MamCom.sln was not found above " + AppContext.BaseDirectory);
    }

    private static void CopyDirectory(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var file in Directory.GetFiles(from))
        {
            File.Copy(file, Path.Combine(to, Path.GetFileName(file)), overwrite: true);
        }

        foreach (var folder in Directory.GetDirectories(from))
        {
            CopyDirectory(folder, Path.Combine(to, Path.GetFileName(folder)));
        }
    }
}
