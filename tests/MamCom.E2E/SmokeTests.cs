using Microsoft.Playwright;

namespace MamCom.E2E;

public class SmokeTests
{
    [Fact]
    public async Task Playwright_driver_starts()
    {
        using var playwright = await Playwright.CreateAsync();

        playwright.ShouldNotBeNull();
    }
}
