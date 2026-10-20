using MamCom.Studio.Review;

namespace MamCom.Studio.Tests;

public class ImageLibraryTests
{
    [Fact]
    public void The_image_files_are_the_pictures_in_the_images_folder_in_name_order()
    {
        using var data = new StudioData();
        data.WriteImage("rau-muong-800.webp");
        data.WriteImage("rau-muong-200.webp");
        data.WriteImage("anh.PNG");
        data.WriteImage("anh2.jpg");
        data.WriteImage("anh3.jpeg");
        data.WriteImage("ghi-chu.txt");
        data.WriteImage(".an.webp");
        data.WriteImage("co khoang trang.webp");

        data.Images.Files().ShouldBe(["anh.PNG", "anh2.jpg", "anh3.jpeg", "rau-muong-200.webp", "rau-muong-800.webp"]);
    }

    [Fact]
    public void Without_an_images_folder_there_are_no_image_files()
    {
        using var data = new StudioData();

        data.Images.Files().ShouldBeEmpty();
    }

    [Theory]
    [InlineData("rau-muong-200.webp", true)]
    [InlineData("a.png", true)]
    [InlineData("A_b-1.JPG", true)]
    [InlineData("a.jpeg", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("a.gif", false)]
    [InlineData("a", false)]
    [InlineData(".webp", false)]
    [InlineData("-a.webp", false)]
    [InlineData("../a.webp", false)]
    [InlineData("a/b.webp", false)]
    [InlineData("a\\b.webp", false)]
    [InlineData("a..b.webp", false)]
    [InlineData("a b.webp", false)]
    [InlineData("a.webp\n", false)]
    [InlineData("tiếng-việt.webp", false)]
    public void Only_a_plain_file_name_of_a_known_picture_type_is_an_image_name(string? name, bool valid)
    {
        ImageLibrary.IsImageName(name).ShouldBe(valid);
    }

    [Fact]
    public void The_path_of_an_image_is_given_only_for_a_file_that_is_there()
    {
        using var data = new StudioData();
        data.WriteImage("rau-muong-200.webp");

        data.Images.PathOf("rau-muong-200.webp").ShouldBe(data.PathOf("images/out/rau-muong-200.webp"));
        data.Images.PathOf("khong-co.webp").ShouldBeNull();
        File.Exists(data.PathOf("config/portion-defaults.json")).ShouldBeTrue();   // two levels up from images/out is the data folder, where this file is
        data.Images.PathOf("../../config/portion-defaults.json").ShouldBeNull();
        data.Images.PathOf("..\\..\\config\\portion-defaults.json").ShouldBeNull();
        data.Images.PathOf("..\\..\\x.webp").ShouldBeNull();
    }
}
