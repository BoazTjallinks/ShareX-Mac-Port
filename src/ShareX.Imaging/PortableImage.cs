using SkiaSharp;

namespace ShareX.Imaging;

/// <summary>
/// NOT a literal port. Upstream (ShareX v21.0.0) represents every in-memory image as a
/// <c>System.Drawing.Bitmap</c> backed by GDI+. GDI+/System.Drawing is banned from this codebase
/// (see CLAUDE.md), so PortableImage is the SkiaSharp-backed replacement threaded through the
/// image tools ported in this namespace (ImageCombiner, ImageSplitter, ImageThumbnailer,
/// ImageComparer) and their codecs.
///
/// Pixel contract (fixed, deliberately narrow so every consumer can reason about raw bytes):
///   - Color type: <see cref="SKColorType.Bgra8888"/> (byte order in memory is B, G, R, A).
///   - Alpha type: <see cref="SKAlphaType.Premul"/> (premultiplied alpha).
///   - Color space: sRGB (<see cref="SKColorSpace.CreateSrgb"/>).
///
/// Every PortableImage instance is normalized to this contract on construction. If a source
/// SKBitmap does not already match it (wrong color/alpha type, or a color space other than sRGB),
/// it is deliberately converted - never silently reinterpreted - and the conversion is recorded
/// on <see cref="ColorSpaceConverted"/>/<see cref="ColorSpaceConversionNote"/>. A bitmap with no
/// attached color space profile is treated as already sRGB (this matches how GDI+ and most 8-bit
/// raster tooling upstream treated untagged RGB data; there is no upstream behavior to diverge
/// from here since GDI+ Bitmap has no color-managed pixel format at all).
/// </summary>
public sealed class PortableImage : IDisposable
{
    private readonly SKBitmap _bitmap;
    private bool _disposed;

    /// <summary>
    /// True if constructing this instance required converting the source bitmap's pixel format
    /// and/or color space to match the fixed BGRA8888/premultiplied/sRGB contract.
    /// </summary>
    public bool ColorSpaceConverted { get; }

    /// <summary>
    /// Human-readable record of what was converted and when, or null if the source already
    /// matched the contract. Callers that care about color fidelity (e.g. a decode pipeline
    /// reporting diagnostics to a user) can surface this; it is never swallowed silently.
    /// </summary>
    public string? ColorSpaceConversionNote { get; }

    public PortableImage(SKBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        (_bitmap, ColorSpaceConverted, ColorSpaceConversionNote) = NormalizeToContract(bitmap);
    }

    private PortableImage(SKBitmap normalizedBitmap, bool colorSpaceConverted, string? note)
    {
        _bitmap = normalizedBitmap;
        ColorSpaceConverted = colorSpaceConverted;
        ColorSpaceConversionNote = note;
    }

    public int Width => _bitmap.Width;

    public int Height => _bitmap.Height;

    public SKImageInfo Info => _bitmap.Info;

    /// <summary>The underlying SKBitmap. Callers may draw into it directly via <see cref="CreateCanvas"/>.</summary>
    public SKBitmap Bitmap => _bitmap;

    /// <summary>Creates a new canvas over this image's bitmap. The caller owns and must dispose the canvas.</summary>
    public SKCanvas CreateCanvas() => new(_bitmap);

    /// <summary>Creates a new, empty, contract-conformant image of the given size (transparent black).</summary>
    public static PortableImage Create(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(width <= 0 ? nameof(width) : nameof(height), "Dimensions must be positive.");
        }

        SKImageInfo info = ContractInfo(width, height);
        var bitmap = new SKBitmap(info);
        bitmap.Erase(SKColors.Transparent);
        return new PortableImage(bitmap, colorSpaceConverted: false, note: null);
    }

    /// <summary>Deep-copies this image. The clone owns an independent pixel buffer.</summary>
    public PortableImage Clone()
    {
        SKBitmap? copy = _bitmap.Copy();
        if (copy is null)
        {
            throw new InvalidOperationException("SkiaSharp failed to copy the image.");
        }

        return new PortableImage(copy, colorSpaceConverted: false, note: null);
    }

    /// <summary>
    /// Crops to an exact pixel-for-pixel subset (no resampling). Equivalent to upstream's
    /// GDI+ crop-by-Rectangle usages (e.g. Graphics.DrawImage with GraphicsUnit.Pixel source rects).
    /// </summary>
    public PortableImage CropTo(SKRectI rect)
    {
        if (rect.Left < 0 || rect.Top < 0 || rect.Right > Width || rect.Bottom > Height || rect.Width <= 0 || rect.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rect), rect, $"Crop rectangle must have positive size and lie within the {Width}x{Height} source image.");
        }

        var cropped = new SKBitmap();
        if (!_bitmap.ExtractSubset(cropped, rect))
        {
            cropped.Dispose();
            throw new InvalidOperationException($"SkiaSharp failed to extract subset {rect} from a {Width}x{Height} image.");
        }

        return new PortableImage(cropped, colorSpaceConverted: false, note: null);
    }

    /// <summary>
    /// Resizes to an exact target size using the given sampling quality.
    /// This is the portable replacement for upstream's
    /// <c>Graphics.DrawImage(..., InterpolationMode)</c> pattern; see
    /// <see cref="ImageInterpolationModeExtensions.ToSamplingOptions"/> for the mapping from
    /// upstream's <see cref="ImageInterpolationMode"/> enum to <see cref="SKSamplingOptions"/>.
    /// </summary>
    public PortableImage ResizeTo(int width, int height, SKSamplingOptions quality)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(width <= 0 ? nameof(width) : nameof(height), "Target dimensions must be positive.");
        }

        SKImageInfo destInfo = ContractInfo(width, height);
        SKBitmap? resized = _bitmap.Resize(destInfo, quality);
        if (resized is null)
        {
            throw new InvalidOperationException($"SkiaSharp failed to resize a {Width}x{Height} image to {width}x{height}.");
        }

        return new PortableImage(resized, colorSpaceConverted: false, note: null);
    }

    /// <summary>Copies out the raw BGRA8888 premultiplied pixel bytes (row-major, <see cref="SKImageInfo.RowBytes"/> stride).</summary>
    public byte[] ReadPixels() => _bitmap.Bytes;

    /// <summary>Zero-copy view over the live pixel buffer. Valid only until the next mutation/dispose.</summary>
    public ReadOnlySpan<byte> ReadPixelSpan() => _bitmap.GetPixelSpan();

    /// <summary>
    /// Overwrites the entire pixel buffer. <paramref name="pixels"/> must already be BGRA8888
    /// premultiplied data of exactly <see cref="SKImageInfo.BytesSize"/> length for this image -
    /// this method does not reinterpret or convert; it is a raw buffer replacement.
    /// </summary>
    public void WritePixels(ReadOnlySpan<byte> pixels)
    {
        if (pixels.Length != _bitmap.ByteCount)
        {
            throw new ArgumentException($"Expected {_bitmap.ByteCount} bytes for a {Width}x{Height} BGRA8888 image, got {pixels.Length}.", nameof(pixels));
        }

        Span<byte> destination = _bitmap.GetPixelSpan();
        pixels.CopyTo(destination);
        _bitmap.NotifyPixelsChanged();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _bitmap.Dispose();
        _disposed = true;
    }

    internal static SKImageInfo ContractInfo(int width, int height) =>
        new(width, height, SKColorType.Bgra8888, SKAlphaType.Premul, SKColorSpace.CreateSrgb());

    /// <summary>
    /// Ensures <paramref name="source"/> matches the fixed BGRA8888/premultiplied/sRGB contract,
    /// converting deliberately (and recording that it happened) if it does not. Takes ownership
    /// of <paramref name="source"/>: if a conversion is needed, the original is disposed and a new
    /// bitmap is returned; if not, <paramref name="source"/> itself is returned unchanged.
    /// </summary>
    internal static (SKBitmap bitmap, bool converted, string? note) NormalizeToContract(SKBitmap source)
    {
        bool formatMatches = source.ColorType == SKColorType.Bgra8888 && source.AlphaType == SKAlphaType.Premul;

        // A bitmap with no attached profile is treated as already sRGB: GDI+'s Bitmap type (what
        // upstream used everywhere) has no color-managed pixel format at all, so "untagged" is the
        // upstream baseline, not a divergence.
        bool declaresNonSrgbColorSpace = source.ColorSpace is not null && !source.ColorSpace.IsSrgb;

        if (formatMatches && !declaresNonSrgbColorSpace)
        {
            return (source, false, null);
        }

        SKColorType sourceColorType = source.ColorType;
        SKAlphaType sourceAlphaType = source.AlphaType;
        int width = source.Width;
        int height = source.Height;

        SKImageInfo targetInfo = ContractInfo(width, height);
        var converted = new SKBitmap(targetInfo);

        // Deliberate conversion: draw the source through a color-managed canvas so Skia performs
        // the color space transform (rather than us reinterpreting the source bytes as if they
        // were already sRGB, which would silently shift colors).
        using (SKImage sourceImage = SKImage.FromBitmap(source))
        using (SKCanvas canvas = new SKCanvas(converted))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.DrawImage(sourceImage, 0, 0);
        }

        source.Dispose();

        string note = declaresNonSrgbColorSpace
            ? $"Source declared a non-sRGB color space; converted deliberately to sRGB at {DateTime.UtcNow:O}."
            : $"Source pixel format was {sourceColorType}/{sourceAlphaType}; converted to BGRA8888/Premul at {DateTime.UtcNow:O}.";

        return (converted, true, note);
    }
}
