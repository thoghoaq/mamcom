using Microsoft.Playwright;

namespace MamCom.E2E;

public class SmokeTests(SiteFixture site)
{
    [Fact]
    public async Task A_first_visit_loads_the_app_and_its_bundle_and_opens_the_first_screen()
    {
        await using var app = await site.OpenAsync();

        await app.GotoAsync("/");

        try
        {
            await app.Heading("Nhà mình có ai?").WaitForAsync();
        }
        catch (TimeoutException)
        {
            throw new InvalidOperationException("The first screen did not appear. " + string.Join(" | ", app.Problems) + " BODY: " + (await app.Page.InnerTextAsync("body")));
        }

        app.Path.ShouldBe("/nha-minh");
        (await app.Page.TitleAsync()).ShouldBe("Nhà mình có ai?");
        (await app.Page.EvaluateAsync<string>("document.documentElement.lang")).ShouldBe("vi");
    }

    [Fact]
    public async Task The_site_serves_the_bundle_of_the_seed_and_not_a_bundle_of_the_real_data()
    {
        await using var app = await site.OpenAsync();

        var version = await app.Page.APIRequest.GetAsync($"{site.BaseUrl}/data-version.json");

        version.Ok.ShouldBeTrue();
        (await version.TextAsync()).ShouldContain("\"isRelease\": false");
    }
}
