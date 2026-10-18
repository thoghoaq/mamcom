using MamCom.UI.Components;

namespace MamCom.UI.Tests;

public class AppBrandTests : BunitContext
{
    [Fact]
    public void Renders_the_given_name()
    {
        var cut = Render<AppBrand>(parameters => parameters.Add(p => p.Name, "MâmCơm"));

        cut.Find(".app-brand").TextContent.ShouldBe("MâmCơm");
    }
}
