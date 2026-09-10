namespace ShareX.Imaging.Codecs;

/// <summary>
/// NOT an upstream file. Hand-rolled, dependency-free BMP writer used because SkiaSharp/Skia does
/// not provide a working native BMP encoder (see PORTING-NOTES.md, "BMP encoding"). Writes an
/// uncompressed BITMAPFILEHEADER + 40-byte BITMAPINFOHEADER (BI_RGB), 32 bits/pixel, bottom-up row
/// order - the same layout GDI+ produces for a 32bpp <c>Bitmap.Save(..., ImageFormat.Bmp)</c>.
/// </summary>
internal static class BmpEncoder
{
    private const int FileHeaderSize = 14;
    private const int InfoHeaderSize = 40;
    private const int BytesPerPixel = 4;

    public static void Encode(PortableImage image, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(stream);

        int width = image.Width;
        int height = image.Height;
        int rowBytes = width * BytesPerPixel; // always a multiple of 4, so BMP's row padding rule is moot at 32bpp
        long pixelDataSize = (long)rowBytes * height;
        long fileSize = FileHeaderSize + InfoHeaderSize + pixelDataSize;

        using var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true);

        // BITMAPFILEHEADER
        writer.Write((byte)'B');
        writer.Write((byte)'M');
        writer.Write((uint)fileSize);
        writer.Write((uint)0); // reserved1/reserved2
        writer.Write((uint)(FileHeaderSize + InfoHeaderSize)); // pixel data offset

        // BITMAPINFOHEADER
        writer.Write((uint)InfoHeaderSize);
        writer.Write(width);
        writer.Write(height); // positive height => bottom-up row order
        writer.Write((ushort)1); // color planes
        writer.Write((ushort)32); // bits per pixel
        writer.Write((uint)0); // BI_RGB (no compression)
        writer.Write((uint)pixelDataSize);
        writer.Write(2835); // ~72 DPI, pixels per meter
        writer.Write(2835);
        writer.Write((uint)0); // palette colors used (none, true color)
        writer.Write((uint)0); // important colors (all)

        ReadOnlySpan<byte> source = image.ReadPixelSpan(); // top-down rows, BGRA8888 premultiplied, RowBytes stride
        int sourceStride = image.Info.RowBytes;
        var row = new byte[rowBytes];

        // BMP stores rows bottom-up. Also convert premultiplied -> straight alpha per pixel: this
        // library's fixed pixel contract is premultiplied, but the de facto convention for the 4th
        // byte of a 32bpp BI_RGB BMP (in the tools that read it at all) is straight alpha, not
        // premultiplied - writing premultiplied values there would make partially transparent pixels
        // look correct only against a black background. See PORTING-NOTES.md.
        for (int y = height - 1; y >= 0; y--)
        {
            ReadOnlySpan<byte> srcRow = source.Slice(y * sourceStride, rowBytes);

            for (int x = 0; x < width; x++)
            {
                int i = x * BytesPerPixel;
                byte b = srcRow[i];
                byte g = srcRow[i + 1];
                byte r = srcRow[i + 2];
                byte a = srcRow[i + 3];

                if (a == 0)
                {
                    row[i] = 0;
                    row[i + 1] = 0;
                    row[i + 2] = 0;
                }
                else if (a == 255)
                {
                    row[i] = b;
                    row[i + 1] = g;
                    row[i + 2] = r;
                }
                else
                {
                    // Valid premultiplied data always has channel <= alpha, so this stays in [0,255].
                    row[i] = (byte)Math.Min(255, b * 255 / a);
                    row[i + 1] = (byte)Math.Min(255, g * 255 / a);
                    row[i + 2] = (byte)Math.Min(255, r * 255 / a);
                }

                row[i + 3] = a;
            }

            writer.Write(row);
        }

        writer.Flush();
    }
}
