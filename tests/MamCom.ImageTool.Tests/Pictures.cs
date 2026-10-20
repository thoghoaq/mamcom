using System.Text;
using SkiaSharp;

namespace MamCom.ImageTool.Tests;

/// <summary>Pictures made in memory for the tests, so no photo is kept in the repo.</summary>
internal static class Pictures
{
    public static readonly SKColor Red = new(255, 0, 0);
    public static readonly SKColor Green = new(0, 255, 0);
    public static readonly SKColor Blue = new(0, 0, 255);
    public static readonly SKColor Yellow = new(255, 255, 0);

    /// <summary>Four quadrants: red top left, green top right, blue bottom left, yellow bottom right, so any turn or mirror shows.</summary>
    public static SKBitmap Quadrants(int width, int height, bool opaque = true)
    {
        var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, opaque ? SKAlphaType.Opaque : SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        using var paint = new SKPaint();
        void Fill(SKColor color, float left, float top, float right, float bottom)
        {
            paint.Color = color;
            canvas.DrawRect(left, top, right - left, bottom - top, paint);
        }

        Fill(Red, 0, 0, width / 2f, height / 2f);
        Fill(Green, width / 2f, 0, width, height / 2f);
        Fill(Blue, 0, height / 2f, width / 2f, height);
        Fill(Yellow, width / 2f, height / 2f, width, height);
        return bitmap;
    }

    public static byte[] Jpeg(int width, int height, int? orientation = null, string? description = null)
    {
        using var bitmap = Quadrants(width, height);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 95);
        var jpeg = data.ToArray();
        return orientation is null && description is null ? jpeg : WithExif(jpeg, orientation ?? 1, description ?? string.Empty);
    }

    public static byte[] WebpPicture(int width, int height)
    {
        using var bitmap = Quadrants(width, height);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Webp, 90);
        return data.ToArray();
    }

    public static byte[] Png(int width, int height, bool withTransparentCorner = false)
    {
        using var bitmap = Quadrants(width, height, opaque: !withTransparentCorner);
        if (withTransparentCorner)
        {
            using var canvas = new SKCanvas(bitmap);
            using var paint = new SKPaint { Color = SKColors.Transparent, BlendMode = SKBlendMode.Src };
            canvas.DrawRect(0, 0, width / 4f, height / 4f, paint);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>
    /// Puts an EXIF block right after the JPEG start marker: the orientation, and a text (the place and the camera of a real
    /// photo would be there) that must not survive.
    /// </summary>
    public static byte[] WithExif(byte[] jpeg, int orientation, string description)
    {
        var text = Encoding.ASCII.GetBytes(description + "\0");

        // TIFF, big endian: header, one IFD of two entries (ImageDescription, Orientation), then the text.
        var tiff = new List<byte> { (byte)'M', (byte)'M', 0, 42, 0, 0, 0, 8 };
        tiff.AddRange([0, 2]);
        var textOffset = 8 + 2 + (2 * 12) + 4;
        tiff.AddRange(Entry(0x010E, 2, text.Length, textOffset));
        tiff.AddRange([0x01, 0x12, 0, 3, 0, 0, 0, 1, 0, (byte)orientation, 0, 0]);
        tiff.AddRange([0, 0, 0, 0]);
        tiff.AddRange(text);

        var payload = new List<byte>(Encoding.ASCII.GetBytes("Exif\0\0"));
        payload.AddRange(tiff);

        var length = payload.Count + 2;
        var segment = new List<byte> { 0xFF, 0xE1, (byte)(length >> 8), (byte)(length & 0xFF) };
        segment.AddRange(payload);

        var result = new List<byte>(jpeg.Length + segment.Count);
        result.AddRange(jpeg.Take(2));
        result.AddRange(segment);
        result.AddRange(jpeg.Skip(2));
        return [.. result];
    }

    private static byte[] Entry(int tag, int type, int count, int value) =>
        [(byte)(tag >> 8), (byte)tag, (byte)(type >> 8), (byte)type, 0, 0, (byte)(count >> 8), (byte)count, 0, 0, (byte)(value >> 8), (byte)value];

    /// <summary>The colour of the picture at the middle of one quarter: 0 top left, 1 top right, 2 bottom left, 3 bottom right.</summary>
    public static SKColor Quarter(SKBitmap bitmap, int quarter)
    {
        var x = (quarter % 2 == 0 ? 1 : 3) * bitmap.Width / 4;
        var y = (quarter / 2 == 0 ? 1 : 3) * bitmap.Height / 4;
        return Nearest(bitmap.GetPixel(x, y));
    }

    /// <summary>The primary the colour is closest to (the encoders blur colours a little).</summary>
    private static SKColor Nearest(SKColor color) =>
        new[] { Red, Green, Blue, Yellow }.MinBy(c => Math.Pow(c.Red - color.Red, 2) + Math.Pow(c.Green - color.Green, 2) + Math.Pow(c.Blue - color.Blue, 2));
}

/// <summary>The structure of a WebP file: a RIFF container of chunks.</summary>
internal static class Webp
{
    /// <summary>The four-letter names of the chunks, in order. Fails the test when the bytes are not a well formed WebP container.</summary>
    public static IReadOnlyList<string> Chunks(byte[] webp)
    {
        Encoding.ASCII.GetString(webp, 0, 4).ShouldBe("RIFF");
        Encoding.ASCII.GetString(webp, 8, 4).ShouldBe("WEBP");
        ((long)BitConverter.ToUInt32(webp, 4) + 8).ShouldBe(webp.Length, "the RIFF size matches the file");

        var names = new List<string>();
        var position = 12;
        while (position < webp.Length)
        {
            names.Add(Encoding.ASCII.GetString(webp, position, 4));
            var size = (int)BitConverter.ToUInt32(webp, position + 4);
            position += 8 + size + (size % 2);
        }

        position.ShouldBe(webp.Length, "the chunks fill the file exactly");
        return names;
    }

    public static SKBitmap Decode(byte[] webp) => SKBitmap.Decode(webp) ?? throw new InvalidDataException("Not a picture.");
}
