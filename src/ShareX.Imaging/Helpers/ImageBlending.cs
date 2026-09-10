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

// Ported from ShareX v21.0.0 (commit d2502561f63fc3ff502cacd91514e3f7f2948c74), file
// ShareX.HelpersLib/Helpers/ImageHelpers.cs (FillBackground(Image, Brush) - the solid-color
// overload used via FillBackground(Image, Color)).
//
// Porting notes:
//   - Upstream: allocate an empty same-size 32bppArgb bitmap, Graphics.FillRectangle the solid
//     color, then Graphics.DrawImage the source on top at native size/position (default GDI+
//     compositing mode is SourceOver). That is exactly what this does with SKCanvas.Clear +
//     SKCanvas.DrawImage: since PortableImage's fixed contract is premultiplied alpha and Skia's
//     default canvas blend mode is SrcOver operating on premultiplied pixels, a semi-transparent
//     source pixel blends toward the fill color correctly (see the alpha compositing unit tests) -
//     no color-fringing "unpremultiplied blended as premultiplied" bug is possible here because the
//     pixel data never leaves the premultiplied representation between decode and composite.

using SkiaSharp;

namespace ShareX.Imaging;

public static class ImageBlending
{
    /// <summary>
    /// Composites <paramref name="source"/> over an opaque <paramref name="backgroundColor"/> fill,
    /// at the source's native size. Ported from ImageHelpers.FillBackground(Image, Color).
    /// </summary>
    public static PortableImage FillBackground(PortableImage source, SKColor backgroundColor)
    {
        ArgumentNullException.ThrowIfNull(source);

        PortableImage result = PortableImage.Create(source.Width, source.Height);
        using SKCanvas canvas = result.CreateCanvas();
        canvas.Clear(backgroundColor);

        using SKImage image = SKImage.FromBitmap(source.Bitmap);
        canvas.DrawImage(image, 0, 0);

        return result;
    }
}
