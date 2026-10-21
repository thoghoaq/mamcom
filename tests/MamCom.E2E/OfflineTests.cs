using Microsoft.Playwright;

namespace MamCom.E2E;

/// <summary>SPEC §6.1 and ADR-020: after one visit with a connection the app, its bundle of dishes and the tray of the day open with none.</summary>
public class OfflineTests(SiteFixture site)
{
    // True when the cache of the app's data worker holds the bundle: the first visit is not under the worker's control yet, so it fetches the bundle itself when it installs.
    private const string BundleIsCached = """
        async () => {
            for (const name of await caches.keys()) {
                const cache = await caches.open(name);
                if ((await cache.keys()).some(request => request.url.includes('data-bundle.json'))) return true;
            }
            return false;
        }
        """;

    [Fact]
    public async Task After_one_visit_with_a_connection_the_app_and_the_tray_of_the_day_open_with_none()
    {
        await using var app = await site.OpenAsync();
        await app.StartWithTwoAdultsAsync();
        var names = await app.DishNamesAsync();

        // The workers install in the background; the page they take over is the next one, so it is opened once more while the connection is there.
        await app.Page.WaitForFunctionAsync("() => navigator.serviceWorker.ready.then(() => true)");
        await app.Page.WaitForFunctionAsync(BundleIsCached);
        await app.Page.ReloadAsync();
        await app.WaitForTrayAsync();
        await app.Page.WaitForFunctionAsync("() => !!navigator.serviceWorker.controller");

        await app.Context.SetOfflineAsync(true);
        await app.Page.ReloadAsync();

        await app.WaitForTrayAsync();
        (await app.DishNamesAsync()).ShouldBe(names);

        // And a page of the app that was never opened works too: the start page of the worker answers for every address of the app.
        await app.GotoAsync("cai-dat");
        await app.Heading("Cài đặt").WaitForAsync();
    }
}
