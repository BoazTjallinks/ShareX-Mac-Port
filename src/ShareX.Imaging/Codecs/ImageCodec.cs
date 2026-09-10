using SkiaSharp;

namespace ShareX.Imaging.Codecs;

/// <summary>
/// NOT a literal port. Upstream loads images via <c>System.Drawing.Image.FromStream</c> /
/// <c>Bitmap.Save(..., ImageFormat, EncoderParameters)</c> (GDI+ codecs), which are banned on this
/// platform. This is the SkiaSharp-backed replacement: decode from any container Skia understands,
/// encode to the four formats this library implements (PNG, JPEG, WebP, BMP - see
/// <see cref="ImageCodecFormat"/> for why this list differs from upstream's serialized
/// <see cref="EImageFormat"/> enum).
/// </summary>
public static class ImageCodec
{
    /// <summary>
    /// Decodes an image from a stream. If the source declares a color space other than sRGB, it is
    /// converted deliberately (never silently reinterpreted) by <see cref="PortableImage"/>'s
    /// constructor, which also records that the conversion happened - see
    /// <see cref="PortableImage.ColorSpaceConverted"/>.
    /// </summary>
    public static PortableImage Decode(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        SKBitmap? bitmap = SKBitmap.Decode(stream);
        if (bitmap is null)
        {
            throw new InvalidDataException("SkiaSharp could not decode the given stream as an image (unrecognized or corrupt container).");
        }

        // PortableImage's constructor is the single place that normalizes to the BGRA8888/
        // premultiplied/sRGB contract and records any color-space conversion; decoding here
        // intentionally does not force a target SKImageInfo so the source's declared color space
        // (if any) survives long enough for that normalization step to see and report it.
        return new PortableImage(bitmap);
    }

    /// <summary>Decodes an image from an in-memory byte array. See <see cref="Decode(Stream)"/>.</summary>
    public static PortableImage Decode(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        SKBitmap? bitmap = SKBitmap.Decode(data);
        if (bitmap is null)
        {
            throw new InvalidDataException("SkiaSharp could not decode the given bytes as an image (unrecognized or corrupt container).");
        }

        return new PortableImage(bitmap);
    }

    /// <summary>Encodes to an in-memory buffer. <paramref name="quality"/> is 0-100 and is ignored by PNG/BMP (lossless).</summary>
    public static byte[] Encode(PortableImage image, ImageCodecFormat format, int quality = 100)
    {
        using var stream = new MemoryStream();
        Encode(image, stream, format, quality);
        return stream.ToArray();
    }

    /// <summary>Encodes to a stream. <paramref name="quality"/> is 0-100 and is ignored by PNG/BMP (lossless).</summary>
    public static void Encode(PortableImage image, Stream stream, ImageCodecFormat format, int quality = 100)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(stream);

        quality = Math.Clamp(quality, 0, 100);

        switch (format)
        {
            case ImageCodecFormat.Png:
                // PNG is lossless; SkiaSharp's numeric parameter for Png encoding does not map to
                // upstream's PNGBitDepth concept (Automatic/24-bit/32-bit) - that policy lives above
                // this codec layer (e.g. in a caller choosing whether to flatten alpha first).
                if (!image.Bitmap.Encode(stream, SKEncodedImageFormat.Png, 100))
                {
                    throw new InvalidOperationException("SkiaSharp failed to encode PNG.");
                }

                break;

            case ImageCodecFormat.Jpeg:
                // JPEG has no alpha channel; SkiaSharp's Jpeg encoder drops it, matching upstream's
                // GDI+ JPEG codec (callers that need a specific background must flatten first, e.g.
                // via ImageThumbnailer's FillBackground step).
                if (!image.Bitmap.Encode(stream, SKEncodedImageFormat.Jpeg, quality))
                {
                    throw new InvalidOperationException("SkiaSharp failed to encode JPEG.");
                }

                break;

            case ImageCodecFormat.WebP:
                if (!image.Bitmap.Encode(stream, SKEncodedImageFormat.Webp, quality))
                {
                    throw new InvalidOperationException("SkiaSharp failed to encode WebP.");
                }

                break;

            case ImageCodecFormat.Bmp:
                // SkiaSharp/Skia does not ship a native BMP encoder (SKBitmap.Encode with
                // SKEncodedImageFormat.Bmp reliably fails on desktop Skia builds - BMP is decode-only
                // there). BmpEncoder hand-writes an uncompressed 32bpp BITMAPINFOHEADER file directly
                // from the pixel buffer. See PORTING-NOTES.md ("BMP encoding").
                BmpEncoder.Encode(image, stream);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, null);
        }
    }
}
