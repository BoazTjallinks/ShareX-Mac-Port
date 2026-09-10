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
// ShareX.HelpersLib/Helpers/ImageHelpers.cs (SplitImage) and
// ShareX.MediaLib/Forms/ImageSplitterForm.cs (output file naming: "<name><index+1>.png").
//
// Porting notes:
//   - Upstream's ImageSplitter is row-count/column-count only (nudRowCount/nudColumnCount in
//     ImageSplitterForm.Designer.cs) - there is no separate "split by target tile size" mode in
//     v21.0.0. SplitBySize() below is an ADDITION, not an upstream port: it only derives a row/
//     column count from a desired tile size and then calls the verbatim-ported SplitImage(), so it
//     cannot diverge from upstream's tiling/remainder behavior. See PORTING-NOTES.md.
//   - Edge/remainder policy for a non-divisible split is preserved exactly: tile size is
//     integer-divided (bmp.Width / columnCount, bmp.Height / rowCount), so a source dimension that
//     is not evenly divisible loses its remainder pixels off the right/bottom edge - upstream never
//     redistributes or pads that remainder, and neither does this port.
//   - Upstream crops each tile via Graphics.DrawImage(dest, destRect, srcRect, GraphicsUnit.Pixel)
//     (an exact, unscaled pixel copy - src and dest rects are always the same size). The Skia
//     translation uses SKBitmap.ExtractSubset, which is the exact-pixel-copy primitive (see
//     PortableImage.CropTo) rather than a canvas draw, since no resampling is involved either way.

using SkiaSharp;

namespace ShareX.Imaging;

public static class ImageSplitter
{
    /// <summary>
    /// Splits <paramref name="image"/> into <paramref name="rowCount"/> x <paramref name="columnCount"/>
    /// tiles, in row-major order (row 0 left-to-right, then row 1, ...) - matching upstream's
    /// nested `for (y) for (x)` loop in ImageHelpers.SplitImage. Tile size is
    /// <c>image.Width / columnCount</c> by <c>image.Height / rowCount</c>; any remainder from a
    /// non-divisible source dimension is dropped (not distributed among tiles), exactly as upstream.
    /// </summary>
    public static List<PortableImage> SplitImage(PortableImage image, int rowCount, int columnCount)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (rowCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(rowCount), rowCount, "Row count must be at least 1.");
        }

        if (columnCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(columnCount), columnCount, "Column count must be at least 1.");
        }

        int tileWidth = image.Width / columnCount;
        int tileHeight = image.Height / rowCount;

        if (tileWidth < 1 || tileHeight < 1)
        {
            throw new ArgumentException($"A {image.Width}x{image.Height} image cannot be split into a {rowCount}x{columnCount} (rows x columns) grid: a tile would be smaller than 1px.");
        }

        var tiles = new List<PortableImage>(rowCount * columnCount);

        for (int y = 0; y < rowCount; y++)
        {
            for (int x = 0; x < columnCount; x++)
            {
                var srcRect = SKRectI.Create(x * tileWidth, y * tileHeight, tileWidth, tileHeight);
                tiles.Add(image.CropTo(srcRect));
            }
        }

        return tiles;
    }

    /// <summary>
    /// NOT an upstream method. Convenience wrapper that derives a row/column count from a desired
    /// tile size and delegates to the verbatim-ported <see cref="SplitImage(PortableImage, int, int)"/>
    /// - see the porting notes above.
    /// </summary>
    public static List<PortableImage> SplitBySize(PortableImage image, int tileWidth, int tileHeight)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (tileWidth < 1 || tileHeight < 1)
        {
            throw new ArgumentOutOfRangeException(tileWidth < 1 ? nameof(tileWidth) : nameof(tileHeight), "Tile dimensions must be at least 1px.");
        }

        int columnCount = Math.Max(1, image.Width / tileWidth);
        int rowCount = Math.Max(1, image.Height / tileHeight);

        return SplitImage(image, rowCount, columnCount);
    }

    /// <summary>
    /// Upstream's output naming from ImageSplitterForm.SplitImage: "&lt;name&gt;&lt;index+1&gt;.png",
    /// 1-based, no separator, always a .png extension regardless of the source format.
    /// </summary>
    public static string GetOutputFileName(string originalFileNameWithoutExtension, int zeroBasedIndex)
    {
        ArgumentNullException.ThrowIfNull(originalFileNameWithoutExtension);

        if (zeroBasedIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(zeroBasedIndex), zeroBasedIndex, "Index must not be negative.");
        }

        return $"{originalFileNameWithoutExtension}{zeroBasedIndex + 1}.png";
    }
}
