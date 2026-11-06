using MamCom.UI.Components;

namespace MamCom.UI.Tests;

public class AppBrandTests : BunitContext
{
    [Fact]
    public void Renders_the_given_name()
    {
        var cut = Render<AppBrand>(parameters => parameters.Add(p => p.Name, "Bowl").Add(p => p.Accent, "o"));

        cut.Find(".app-brand").TextContent.Trim().ShouldBe("Bowlo");
        cut.Find(".app-brand__accent").TextContent.ShouldBe("o");
        cut.Find("svg.brand-mark").GetAttribute("aria-hidden").ShouldBe("true");   // the name says it; the bowl is decoration
    }
}
