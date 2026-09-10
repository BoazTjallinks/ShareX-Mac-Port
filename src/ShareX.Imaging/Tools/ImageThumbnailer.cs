#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

// Ported from ShareX v21.0.0 (commit d2502561f63fc3ff502cacd91514e3f7f2948c74). Upstream actually
// ships TWO different "thumbnail" algorithms and this file ports both - see
// PORTING-NOTES.md ("ImageThumbnailer: two upstream algorithms, one options type") for why they
// are kept distinct rather than merged:
//
//   1. CreateProportionalThumbnail <- ShareX/TaskHelpers.cs (CreateThumbnail) +
//      ShareX.ImageEffectsLib/Manipulations/Resize.cs + ImageHelpers.ApplyAspectRatio/ResizeImage.
//      This is the automatic post-capture "save thumbnail" task: aspect-ratio preserving, gated by
//      ThumbnailCheckSize ("only if larger"), suffix ThumbnailName, always JPEG.
//   2. CreateCoverThumbnail <- ShareX.HelpersLib/Helpers/ImageHelpers.cs (CreateThumbnail(Bitmap,
//      int, int, InterpolationMode)), used by the manual ShareX.MediaLib.ImageThumbnailerForm batch
//      tool. This one does NOT preserve source aspect ratio - it center-crops to exactly fill the
//      requested width x height (a "cover" thumbnail), with no size gate and no suffix policy of
//      its own (ImageThumbnailerForm applies its own "$filename" substitution, ported here as
//      ApplyFilenameToken).

using SkiaSharp;

namespace ShareX.Imaging;

public static class ImageThumbnailer
{
    /// <summary>
    /// Ported from ShareX/TaskHelpers.cs CreateThumbnail + ShareX.ImageEffectsLib.Resize.Apply +
    /// ImageHelpers.ApplyAspectRatio/ResizeImage. Returns null when upstream would have produced no
    /// thumbnail at all: both ThumbnailWidth and ThumbnailHeight are &lt;= 0, or ThumbnailCheckSize
    /// is set and the source is not larger than the requested box in both dimensions ("only if
    /// larger" - this never upscales, because when the gate fails no image is produced at all,
    /// matching upstream exactly rather than clamping to the original size).
    /// </summary>
    public static PortableImage? CreateProportionalThumbnail(PortableImage source, ImageThumbnailerOptions options)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);

        if (options.ThumbnailWidth <= 0 && options.ThumbnailHeight <= 0)
        {
            return null;
        }

        if (options.ThumbnailCheckSize &&
            !(source.Width > options.ThumbnailWidth && source.Height > options.ThumbnailHeight))
        {
            return null;
        }

        (int width, int height) = ApplyAspectRatio(options.ThumbnailWidth, options.ThumbnailHeight, source.Width, source.Height);

        // Upstream's Resize effect is constructed with Mode defaulting to ResizeMode.ResizeAll here
        // (TaskHelpers calls `new Resize(width, height)`, which does not set Mode), so once the gate
        // above has passed, upstream always resizes to the aspect-fit size with no further "is it
        // actually bigger" check inside Resize.Apply. This mirrors that: no additional gate here.
        return source.ResizeTo(width, height, ImageInterpolationMode.HighQualityBicubic.ToSamplingOptions());
    }

    /// <summary>
    /// Upstream's automatic-thumbnail output file name: "&lt;name&gt;&lt;ThumbnailName&gt;.jpg"
    /// (ShareX/TaskHelpers.cs CreateThumbnail: <c>Path.GetFileNameWithoutExtension(fileName) +
    /// taskSettings.ImageSettings.ThumbnailName + ".jpg"</c>).
    /// </summary>
    public static string GetProportionalThumbnailFileName(string originalFileNameWithoutExtension, ImageThumbnailerOptions options)
    {
        ArgumentNullException.ThrowIfNull(originalFileNameWithoutExtension);
        ArgumentNullException.ThrowIfNull(options);

        return $"{originalFileNameWithoutExtension}{options.ThumbnailName}.jpg";
    }

    /// <summary>
    /// Ported from ShareX.HelpersLib.ImageHelpers.CreateThumbnail(Bitmap, int, int,
    /// InterpolationMode): a center-cropped "cover" thumbnail that exactly fills width x height,
    /// discarding the source's aspect ratio (the crop rectangle is chosen so the discarded margin is
    /// centered). Used by the manual ImageThumbnailerForm batch tool.
    /// </summary>
    public static PortableImage CreateCoverThumbnail(PortableImage source, int width, int height,
        ImageInterpolationMode interpolationMode = ImageInterpolationMode.HighQualityBicubic)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(width <= 0 ? nameof(width) : nameof(height), "Target dimensions must be positive.");
        }

        double srcRatio = (double)source.Width / source.Height;
        double dstRatio = (double)width / height;
        int w, h;

        if (srcRatio >= dstRatio)
        {
            w = srcRatio >= 1 ? (int)(source.Height * dstRatio) : (int)(source.Width / srcRatio * dstRatio);
            h = source.Height;
        }
        else
        {
            w = source.Width;
            h = srcRatio >= 1 ? (int)(source.Height / dstRatio * srcRatio) : (int)(source.Height * srcRatio / dstRatio);
        }

        int x = (source.Width - w) / 2;
        int y = (source.Height - h) / 2;

        SKImageInfo destInfo = PortableImage.ContractInfo(width, height);
        var bmpResult = new SKBitmap(destInfo);

        using (SKCanvas canvas = new SKCanvas(bmpResult))
        using (SKImage skImage = SKImage.FromBitmap(source.Bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            SKRect srcRect = SKRect.Create(x, y, w, h);
            SKRect destRect = SKRect.Create(0, 0, width, height);

            // Upstream sets ImageAttributes.SetWrapMode(WrapMode.TileFlipXY) here so GDI+'s
            // high-quality interpolation kernel mirrors at the crop edge instead of clamping. Skia's
            // plain DrawImage(image, srcRect, destRect, ...) clamps at the edge instead; this is a
            // documented, minor divergence limited to sub-pixel filtering right at the crop boundary
            // - see PORTING-NOTES.md ("Cover-thumbnail edge sampling").
            canvas.DrawImage(skImage, srcRect, destRect, interpolationMode.ToSamplingOptions());
        }

        return new PortableImage(bmpResult);
    }

    /// <summary>
    /// Upstream's manual-tool filename templating (ImageThumbnailerForm.btnGenerate_Click):
    /// <c>outputFileName.Replace("$filename", fileName)</c>, then forced to a .jpg extension.
    /// </summary>
    public static string ApplyFilenameToken(string outputFileNameTemplate, string originalFileNameWithoutExtension)
    {
        ArgumentNullException.ThrowIfNull(outputFileNameTemplate);
        ArgumentNullException.ThrowIfNull(originalFileNameWithoutExtension);

        string replaced = outputFileNameTemplate.Replace("$filename", originalFileNameWithoutExtension);
        return Path.ChangeExtension(replaced, "jpg");
    }

    /// <summary>
    /// Ported from ShareX.HelpersLib.ImageHelpers.ApplyAspectRatio(int, int, Bitmap): if either
    /// requested dimension is 0, it is computed from the source's aspect ratio; if both are
    /// non-zero, both are used as-is (the result may not preserve aspect ratio in that case,
    /// exactly as upstream).
    /// </summary>
    internal static (int width, int height) ApplyAspectRatio(int requestedWidth, int requestedHeight, int sourceWidth, int sourceHeight)
    {
        int newWidth, newHeight;

        if (requestedWidth == 0)
        {
            newWidth = (int)Math.Round((float)requestedHeight / sourceHeight * sourceWidth);
            newHeight = requestedHeight;
        }
        else if (requestedHeight == 0)
        {
            newWidth = requestedWidth;
            newHeight = (int)Math.Round((float)requestedWidth / sourceWidth * sourceHeight);
        }
        else
        {
            newWidth = requestedWidth;
            newHeight = requestedHeight;
        }

        return (Math.Max(1, newWidth), Math.Max(1, newHeight));
    }
}
