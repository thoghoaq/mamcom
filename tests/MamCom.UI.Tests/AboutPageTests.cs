using System.Reflection;
using MamCom.Data;
using MamCom.UI.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace MamCom.UI.Tests;

public class AboutPageTests : BunitContext
{
    private readonly FakeAppData _data = new() { Bundle = TestConfig.SeedBundle };

    public AboutPageTests()
    {
        Services.AddSingleton<IAppData>(_data);
    }

    [Fact]
    public void The_page_is_the_route_the_settings_link_to()
    {
        typeof(AboutPage).GetCustomAttribute<RouteAttribute>()!.Template.ShouldBe("/" + Routes.About);
        Routes.About.ShouldBe("gioi-thieu");
    }

    [Fact]
    public void It_says_the_numbers_are_estimates_where_they_come_from_and_that_the_data_stays_on_the_device()
    {
        var cut = Render<AboutPage>();
        var bundle = TestConfig.SeedBundle;
        var usda = bundle.Ingredients.Count(i => i.NutritionSource.Db == "usda-fdc");
        var none = bundle.Ingredients.Count(i => i.NutritionSource.Db == "placeholder");

        cut.Find("[data-disclaimer]").TextContent.ShouldBe("Số liệu ước tính, không thay tư vấn y tế.");
        cut.Find("[data-source-counts]").TextContent.ShouldStartWith($"Trong {bundle.Ingredients.Count} nguyên liệu của app: {usda} lấy số từ USDA, 0 từ Bảng thành phần thực phẩm Việt Nam, {none} chưa có số liệu");
        cut.FindAll("h2").Select(h => h.TextContent).ShouldBe(["Số liệu là ước tính", "Nguồn số liệu dinh dưỡng", "Quyền riêng tư", "Phông chữ và biểu tượng"]);
        cut.Markup.ShouldContain("Viện Dinh dưỡng");
        cut.Markup.ShouldContain("chỉ lưu trên máy này");
        _data.Initialized.ShouldBe(1);
    }
}
