using System.Text;
using MamCom.ImageTool.Imaging;
using SkiaSharp;

namespace MamCom.ImageTool.Tests;

public class ImageProcessorTests
{
    [Theory]
    [InlineData(1600, 1200, 200, 200, 150)]
    [InlineData(1600, 1200, 800, 800, 600)]
    [InlineData(1200, 1600, 800, 600, 800)]
    [InlineData(1000, 1000, 200, 200, 200)]
    [InlineData(4000, 3000, 800, 800, 600)]
    [InlineData(3001, 2000, 800, 800, 533)]   // 800 × 2000 / 3001 = 533.1
    [InlineData(1000, 667, 800, 800, 534)]   // 800 × 667 / 1000 = 533.6: rounded, not cut
    [InlineData(2000, 3001, 800, 533, 800)]   // 800 × 2000 / 3001 = 533.1: rounded, not rounded up (and the width is the shorter side here)
    [InlineData(1000, 1, 200, 200, 1)]   // never a side of zero pixels
    [InlineData(1, 1000, 200, 1, 200)]   // not on the width either
    public void The_longer_side_gets_the_size_and_the_shape_stays(int width, int height, int max, int expectedWidth, int expectedHeight)
    {
        ImageProcessor.Fit(width, height, max).ShouldBe((expectedWidth, expectedHeight));
    }

    [Theory]
    [InlineData(150, 100)]
    [InlineData(200, 200)]
    [InlineData(640, 480)]
    [InlineData(800, 600)]
    public void A_picture_is_never_made_bigger(int width, int height)
    {
        var (fitWidth, fitHeight) = ImageProcessor.Fit(width, height, ImageProcessor.FullPixels);

        fitWidth.ShouldBeLessThanOrEqualTo(width);
        fitHeight.ShouldBeLessThanOrEqualTo(height);
    }

    [Fact]
    public void The_sizes_and_the_quality_are_the_ones_of_the_spec()
    {
        ImageProcessor.ThumbPixels.ShouldBe(200);
        ImageProcessor.FullPixels.ShouldBe(800);
        ImageProcessor.WebpQuality.ShouldBe(80);
    }

    [Fact]
    public void A_big_landscape_photo_becomes_a_thumb_and_a_full_picture_of_the_right_size()
    {
        var made = ImageProcessor.Process(Pictures.Jpeg(1600, 1200));

        (made.SourceWidth, made.SourceHeight).ShouldBe((1600, 1200));
        (made.Thumb.Width, made.Thumb.Height).ShouldBe((200, 150));
        (made.Full.Width, made.Full.Height).ShouldBe((800, 600));
        made.Thumb.Webp.Length.ShouldBeLessThan(made.Full.Webp.Length);
    }

    [Fact]
    public void The_files_are_webp_of_exactly_the_size_that_is_said()
    {
        var made = ImageProcessor.Process(Pictures.Jpeg(1600, 1200));

        foreach (var rendition in new[] { made.Thumb, made.Full })
        {
            Webp.Chunks(rendition.Webp).ShouldNotBeEmpty();
            using var decoded = Webp.Decode(rendition.Webp);
            (decoded.Width, decoded.Height).ShouldBe((rendition.Width, rendition.Height));
        }
    }

    [Fact]
    public void A_small_original_keeps_its_size_for_both_files()
    {
        var made = ImageProcessor.Process(Pictures.Jpeg(120, 90));

        (made.Thumb.Width, made.Thumb.Height).ShouldBe((120, 90));
        (made.Full.Width, made.Full.Height).ShouldBe((120, 90));
    }

    [Fact]
    public void A_png_works_too_and_keeps_what_is_transparent()
    {
        var made = ImageProcessor.Process(Pictures.Png(400, 400, withTransparentCorner: true));

        using var full = Webp.Decode(made.Full.Webp);
        full.GetPixel(10, 10).Alpha.ShouldBe((byte)0);
        full.GetPixel(300, 300).Alpha.ShouldBe((byte)255);
    }

    // ---- what the photo says about itself ---------------------------------------------------------------------------------

    [Fact]
    public void No_exif_and_no_text_of_the_original_is_left_in_either_file()
    {
        const string Secret = "SECRET-PLACE-10.77N-106.69E";
        var original = Pictures.Jpeg(1000, 750, orientation: 1, description: Secret);
        Encoding.ASCII.GetString(original).ShouldContain(Secret);   // the original really carries it

        var made = ImageProcessor.Process(original);

        foreach (var rendition in new[] { made.Thumb, made.Full })
        {
            var chunks = Webp.Chunks(rendition.Webp);
            chunks.ShouldNotContain("EXIF");
            chunks.ShouldNotContain("XMP ");
            Encoding.ASCII.GetString(rendition.Webp).ShouldNotContain(Secret);
            Encoding.ASCII.GetString(rendition.Webp).ShouldNotContain("Exif");
        }
    }

    // Where the four corners of the picture (top left, top right, bottom left, bottom right) show, for each EXIF orientation:
    // from the EXIF specification, not from the code under test. The stored picture is red, green / blue, yellow.
    [Theory]
    [InlineData(1, false, "R", "G", "B", "Y")]   // as stored
    [InlineData(2, false, "G", "R", "Y", "B")]   // mirrored left to right
    [InlineData(3, false, "Y", "B", "G", "R")]   // turned half way
    [InlineData(4, false, "B", "Y", "R", "G")]   // mirrored top to bottom
    [InlineData(5, true, "R", "B", "G", "Y")]    // transposed
    [InlineData(6, true, "B", "R", "Y", "G")]    // turned a quarter clockwise (a phone held upright)
    [InlineData(7, true, "Y", "G", "B", "R")]    // transposed the other way
    [InlineData(8, true, "G", "Y", "R", "B")]    // turned a quarter the other way
    public void A_photo_stored_on_its_side_is_turned_the_right_way_up(int orientation, bool sidesSwap, string topLeft, string topRight, string bottomLeft, string bottomRight)
    {
        var made = ImageProcessor.Process(Pictures.Jpeg(400, 200, orientation, description: "x"));

        (made.SourceWidth, made.SourceHeight).ShouldBe(sidesSwap ? (200, 400) : (400, 200));
        using var full = Webp.Decode(made.Full.Webp);
        (full.Width, full.Height).ShouldBe(sidesSwap ? (200, 400) : (400, 200));
        new[] { 0, 1, 2, 3 }.Select(q => Name(Pictures.Quarter(full, q))).ShouldBe([topLeft, topRight, bottomLeft, bottomRight]);
    }

    [Fact]
    public void The_thumb_is_turned_the_same_way_as_the_full_picture()
    {
        var made = ImageProcessor.Process(Pictures.Jpeg(1600, 800, orientation: 6, description: "x"));

        (made.Thumb.Width, made.Thumb.Height).ShouldBe((100, 200));
        (made.Full.Width, made.Full.Height).ShouldBe((400, 800));
        using var thumb = Webp.Decode(made.Thumb.Webp);
        new[] { 0, 1, 2, 3 }.Select(q => Name(Pictures.Quarter(thumb, q))).ShouldBe(["B", "R", "Y", "G"]);
    }

    // ---- what is not a picture ---------------------------------------------------------------------------------------------

    [Fact]
    public void Bytes_that_are_not_a_picture_are_refused()
    {
        Should.Throw<InvalidDataException>(() => ImageProcessor.Process(Encoding.UTF8.GetBytes("this is not a picture")));
        Should.Throw<InvalidDataException>(() => ImageProcessor.Process([]));
    }

    [Fact]
    public void A_picture_cut_off_in_the_middle_is_refused_or_decoded_but_never_crashes()
    {
        var jpeg = Pictures.Jpeg(800, 600);
        var cut = jpeg.Take(jpeg.Length / 3).ToArray();

        var outcome = Record.Exception(() => ImageProcessor.Process(cut));

        (outcome is null or InvalidDataException).ShouldBeTrue();
    }

    [Fact]
    public void The_argument_is_checked()
    {
        Should.Throw<ArgumentNullException>(() => ImageProcessor.Process(null!));
    }

    private static string Name(SKColor color) =>
        color == Pictures.Red ? "R" : color == Pictures.Green ? "G" : color == Pictures.Blue ? "B" : color == Pictures.Yellow ? "Y" : color.ToString();
}
