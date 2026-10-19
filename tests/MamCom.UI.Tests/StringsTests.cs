using System.Globalization;
using System.Reflection;
using System.Resources;

namespace MamCom.UI.Tests;

public class StringsTests
{
    [Fact]
    public void The_strings_are_the_neutral_resource_of_the_ui_assembly_so_any_machine_culture_finds_them()
    {
        var before = CultureInfo.CurrentUICulture;
        try
        {
            foreach (var culture in new[] { "vi-VN", "en-US", "de-DE", "ja-JP" })
            {
                CultureInfo.CurrentUICulture = new CultureInfo(culture);
                Strings.Nutrition_Unknown.ShouldBe("chưa đủ số liệu", culture);
            }
        }
        finally
        {
            CultureInfo.CurrentUICulture = before;
        }
    }

    [Fact]
    public void Every_string_is_written_with_its_diacritics()
    {
        var manager = new ResourceManager("MamCom.UI.Strings", typeof(Strings).Assembly);
        var names = typeof(Strings).GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(string))
            .Select(p => p.Name)
            .ToArray();

        names.ShouldNotBeEmpty();
        foreach (var name in names)
        {
            var value = manager.GetString(name, CultureInfo.InvariantCulture);
            value.ShouldNotBeNullOrWhiteSpace(name);
            value.ShouldNotContain('�', name);   // the replacement character: the file was saved in the wrong encoding
        }
    }
}
