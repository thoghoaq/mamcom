using MamCom.UI.Components;

namespace MamCom.UI.Tests;

public class AppHeaderTests : BunitContext
{
    [Fact]
    public void The_name_leads_home_and_a_link_leads_to_the_settings()
    {
        var cut = Render<AppHeader>();

        cut.Find("header.app-header").ShouldNotBeNull();
        var brand = cut.Find("a.app-header__brand");
        brand.GetAttribute("href").ShouldBe("/");
        brand.TextContent.ShouldBe("MâmCơm");
        cut.Find("a.app-header__action").GetAttribute("href").ShouldBe(Routes.Settings);
    }

    [Fact]
    public void The_link_to_the_settings_is_an_icon_with_a_name_for_a_screen_reader()
    {
        var link = Render<AppHeader>().Find("a.app-header__action");

        link.GetAttribute("aria-label").ShouldBe("Cài đặt");
        link.TextContent.Trim().ShouldBeEmpty();
        link.QuerySelector("svg use")!.GetAttribute("href").ShouldEndWith("#settings");
        link.QuerySelector("svg")!.GetAttribute("aria-hidden").ShouldBe("true");
    }
}
