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

// Ported from ShareX v21.0.0 (commit d2502561f63fc3ff502cacd91514e3f7f2948c74), files
// ShareX.HelpersLib/Helpers/ImageHelpers.cs (CombineImages) and
// ShareX.HelpersLib/Extensions/Extensions.cs (Combine(), the Rectangle-union helper).
//
// Porting notes:
//   - The row/column layout algorithm (position accumulation, per-window max-size tracking for
//     WrapAfter, and the three-way alignment switch) is preserved instruction-for-instruction.
//   - Rectangle.Union(imageRects) becomes SKRectI.Union folded over the same array (SkiaSharp has
//     no IEnumerable<SKRectI>.Combine() extension, so the loop is inlined here).
//   - Upstream draws each source bitmap into its destination Rectangle unscaled
//     (Graphics.DrawImage(image, imageRects[i]), where imageRects[i] always has the image's native
//     size - only its position varies). The Skia translation therefore uses SKCanvas.DrawImage with
//     an explicit SKSamplingOptions(SKFilterMode.Nearest): since source and destination rects are
//     always the same size, no resampling ever actually occurs, but the sampling option is still
//     specified explicitly per the task brief rather than left as an implicit default.
//   - AutoFillBackground reads the bottom-right pixel of the *first* image (image.GetPixel(Width-1,
//     Height-1)) as the fill color, exactly as upstream does - this is a deliberately naive choice
//     upstream made (not our approximation) and is preserved as-is.

using SkiaSharp;

namespace ShareX.Imaging;

public static class ImageCombiner
{
    /// <summary>
    /// Combines <paramref name="images"/> into one image per <paramref name="options"/>. Ported
    /// from ShareX.HelpersLib.ImageHelpers.CombineImages(List&lt;Bitmap&gt;, ...).
    /// </summary>
    public static PortableImage Combine(IReadOnlyList<PortableImage> images, ImageCombinerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(images);

        if (images.Count == 0)
        {
            throw new ArgumentException("At least one image is required.", nameof(images));
        }

        options ??= new ImageCombinerOptions();

        int count = images.Count;
        var rects = new SKRectI[count];
        int posX = 0, posY = 0;
        int currentSize = 0;

        for (int i = 0; i < count; i++)
        {
            PortableImage image = images[i];
            int offsetX = 0, offsetY = 0;

            if (options.Orientation == Orientation.Horizontal)
            {
                if (options.WrapAfter > 0)
                {
                    if (i % options.WrapAfter == 0)
                    {
                        if (i > 0)
                        {
                            posX = 0;
                            posY += currentSize + options.Space;
                        }

                        currentSize = MaxOverWindow(images, i, options.WrapAfter, useHeight: true);
                    }
                }
                else if (i == 0)
                {
                    currentSize = MaxDimension(images, useHeight: true);
                }

                offsetY = options.Alignment switch
                {
                    ImageCombinerAlignment.Center => (currentSize / 2) - (image.Height / 2),
                    ImageCombinerAlignment.RightOrBottom => currentSize - image.Height,
                    _ => 0
                };

                rects[i] = SKRectI.Create(posX + offsetX, posY + offsetY, image.Width, image.Height);
                posX += image.Width + options.Space;
            }
            else
            {
                if (options.WrapAfter > 0)
                {
                    if (i % options.WrapAfter == 0)
                    {
                        if (i > 0)
                        {
                            posX += currentSize + options.Space;
                            posY = 0;
                        }

                        currentSize = MaxOverWindow(images, i, options.WrapAfter, useHeight: false);
                    }
                }
                else if (i == 0)
                {
                    currentSize = MaxDimension(images, useHeight: false);
                }

                offsetX = options.Alignment switch
                {
                    ImageCombinerAlignment.Center => (currentSize / 2) - (image.Width / 2),
                    ImageCombinerAlignment.RightOrBottom => currentSize - image.Width,
                    _ => 0
                };

                rects[i] = SKRectI.Create(posX + offsetX, posY + offsetY, image.Width, image.Height);
                posY += image.Height + options.Space;
            }
        }

        SKRectI totalRect = rects[0];
        for (int i = 1; i < count; i++)
        {
            totalRect = SKRectI.Union(totalRect, rects[i]);
        }

        PortableImage result = PortableImage.Create(totalRect.Width, totalRect.Height);
        using SKCanvas canvas = result.CreateCanvas();

        if (options.AutoFillBackground)
        {
            PortableImage first = images[0];
            SKColor backgroundColor = first.Bitmap.GetPixel(first.Width - 1, first.Height - 1);
            canvas.Clear(backgroundColor);
        }

        var samplingOptions = new SKSamplingOptions(SKFilterMode.Nearest);

        for (int i = 0; i < count; i++)
        {
            using SKImage skImage = SKImage.FromBitmap(images[i].Bitmap);
            SKRectI r = rects[i];
            SKRect destRect = SKRect.Create(r.Left - totalRect.Left, r.Top - totalRect.Top, r.Width, r.Height);
            canvas.DrawImage(skImage, destRect, samplingOptions);
        }

        return result;
    }

    private static int MaxDimension(IReadOnlyList<PortableImage> images, bool useHeight)
    {
        int max = 0;

        foreach (PortableImage image in images)
        {
            int value = useHeight ? image.Height : image.Width;
            if (value > max)
            {
                max = value;
            }
        }

        return max;
    }

    private static int MaxOverWindow(IReadOnlyList<PortableImage> images, int startIndex, int windowSize, bool useHeight)
    {
        int max = 0;
        int end = Math.Min(images.Count, startIndex + windowSize);

        for (int i = startIndex; i < end; i++)
        {
            int value = useHeight ? images[i].Height : images[i].Width;
            if (value > max)
            {
                max = value;
            }
        }

        return max;
    }
}
