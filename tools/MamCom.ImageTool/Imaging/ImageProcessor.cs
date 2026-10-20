using SkiaSharp;

namespace MamCom.ImageTool.Imaging;

/// <summary>One size of a picture: its pixels and the WebP file.</summary>
public sealed record Rendition(int Width, int Height, byte[] Webp);

/// <param name="SourceWidth">Pixels of the original after it was turned the right way up (a phone photo may be stored on its side).</param>
public sealed record ProcessedImage(int SourceWidth, int SourceHeight, Rendition Thumb, Rendition Full);

/// <summary>
/// Turns an original photo into the two WebP files the app ships (SPEC §7.5): a thumb whose longer side is 200 px and a full
/// picture whose longer side is 800 px. The picture is turned the right way up first, because a phone stores a photo as the
/// sensor saw it with the way it was held in EXIF, and the EXIF is not kept: encoding the pixels again writes none of it, not
/// the place the photo was taken. Nothing is cropped and nothing is made bigger.
/// </summary>
public static class ImageProcessor
{
    public const int ThumbPixels = 200;
    public const int FullPixels = 800;
    public const int WebpQuality = 80;

    /// <exception cref="InvalidDataException">The bytes are not a picture that Skia can read.</exception>
    public static ProcessedImage Process(byte[] original)
    {
        ArgumentNullException.ThrowIfNull(original);

        using var data = SKData.CreateCopy(original);
        using var codec = SKCodec.Create(data) ?? throw new InvalidDataException("Not a picture Skia can read.");
        using var decoded = SKBitmap.Decode(codec) ?? throw new InvalidDataException("The picture could not be decoded.");
        using var upright = TurnUpright(decoded, codec.EncodedOrigin);

        return new ProcessedImage(upright.Width, upright.Height, Render(upright, ThumbPixels), Render(upright, FullPixels));
    }

    /// <summary>The size a picture of <paramref name="width"/> × <paramref name="height"/> gets when its longer side may be <paramref name="maxPixels"/>: never bigger than it is.</summary>
    public static (int Width, int Height) Fit(int width, int height, int maxPixels)
    {
        var longer = Math.Max(width, height);
        if (longer <= maxPixels)
        {
            return (width, height);
        }

        var scale = (double)maxPixels / longer;
        return (Math.Max(1, (int)Math.Round(width * scale)), Math.Max(1, (int)Math.Round(height * scale)));
    }

    private static Rendition Render(SKBitmap upright, int maxPixels)
    {
        var (width, height) = Fit(upright.Width, upright.Height, maxPixels);

        using var sized = width == upright.Width && height == upright.Height
            ? upright.Copy()
            : upright.Resize(new SKImageInfo(width, height, upright.ColorType, upright.AlphaType), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear))
                ?? throw new InvalidDataException("The picture could not be resized.");

        using var image = SKImage.FromBitmap(sized);
        using var encoded = image.Encode(SKEncodedImageFormat.Webp, WebpQuality)
            ?? throw new InvalidDataException("The picture could not be written as WebP.");

        return new Rendition(width, height, encoded.ToArray());
    }

    /// <summary>Applies the EXIF orientation (the eight ways a camera may have been held) to the pixels.</summary>
    private static SKBitmap TurnUpright(SKBitmap bitmap, SKEncodedOrigin origin)
    {
        if (origin == SKEncodedOrigin.TopLeft)
        {
            return bitmap.Copy();
        }

        float w = bitmap.Width, h = bitmap.Height;

        // x' = a*x + b*y + tx, y' = c*x + d*y + ty, the same table Skia uses for SkEncodedOrigin.
        var (a, b, tx, c, d, ty) = origin switch
        {
            SKEncodedOrigin.TopRight => (-1f, 0f, w, 0f, 1f, 0f),
            SKEncodedOrigin.BottomRight => (-1f, 0f, w, 0f, -1f, h),
            SKEncodedOrigin.BottomLeft => (1f, 0f, 0f, 0f, -1f, h),
            SKEncodedOrigin.LeftTop => (0f, 1f, 0f, 1f, 0f, 0f),
            SKEncodedOrigin.RightTop => (0f, -1f, h, 1f, 0f, 0f),
            SKEncodedOrigin.RightBottom => (0f, -1f, h, -1f, 0f, w),
            SKEncodedOrigin.LeftBottom => (0f, 1f, 0f, -1f, 0f, w),
            _ => (1f, 0f, 0f, 0f, 1f, 0f),
        };

        var swapsSides = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var turned = new SKBitmap(new SKImageInfo(swapsSides ? bitmap.Height : bitmap.Width, swapsSides ? bitmap.Width : bitmap.Height, bitmap.ColorType, bitmap.AlphaType));

        using var canvas = new SKCanvas(turned);
        canvas.Clear(SKColors.Transparent);
        canvas.SetMatrix(new SKMatrix(a, b, tx, c, d, ty, 0, 0, 1));
        canvas.DrawBitmap(bitmap, 0, 0);

        return turned;
    }
}
