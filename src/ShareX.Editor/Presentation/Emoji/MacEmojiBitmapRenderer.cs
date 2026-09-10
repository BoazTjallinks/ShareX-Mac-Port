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

// macOS replacement for ShareX v21.0.0's
// ShareX.ImageEditor/Presentation/Emoji/WindowsEmojiBitmapRenderer.cs
// (commit d2502561f63fc3ff502cacd91514e3f7f2948c74), which rasterised emoji with
// Direct2D + DirectWrite + WIC against the "Segoe UI Emoji" font.
//
// Here the glyphs come from the system emoji font (Apple Color Emoji) through
// Skia. The public surface, the size rules, the padding constants and the
// caching behaviour are preserved exactly, so callers are unchanged.
//
// RECORDED PLATFORM DIFFERENCE (planning/adr/0004-editor-fork-strategy.md):
// Apple Color Emoji and Segoe UI Emoji are different typefaces. The same emoji
// will therefore look different from the Windows build. That is a font
// difference, not a defect, and it is not claimed as visual parity. Apple's
// emoji artwork is used only as an installed system font is normally used; no
// glyph assets are extracted or redistributed.

using Avalonia.Media.Imaging;
using ShareX.ImageEditor.Presentation.Rendering;
using SkiaSharp;

namespace ShareX.ImageEditor.Presentation.Emoji;

public static class MacEmojiBitmapRenderer
{
    // Same values as upstream, so layout and cache pressure match.
    private const int PreviewPadding = 6;
    private const int StickerPadding = 14;
    private const int MaxStickerCacheEntries = 256;

    private static readonly Dictionary<string, Bitmap?> PreviewCache = new();
    private static readonly Dictionary<string, SKBitmap?> StickerCache = new();
    private static readonly object SyncRoot = new();

    /// <summary>
    /// Resolved once: Skia needs a typeface that actually carries the glyph, and
    /// on macOS that is "Apple Color Emoji". Falling back to the default typeface
    /// would silently render tofu boxes.
    /// </summary>
    private static readonly Lazy<SKTypeface> EmojiTypeface = new(() =>
        SKFontManager.Default.MatchFamily("Apple Color Emoji")
        ?? SKFontManager.Default.MatchCharacter("😀"[0])
        ?? SKTypeface.Default);

    public static Bitmap? RenderPreviewBitmap(string unicodeSequence, int size)
    {
        if (string.IsNullOrWhiteSpace(unicodeSequence) || size <= 0)
        {
            return null;
        }

        string cacheKey = $"{unicodeSequence}:{size}";

        lock (SyncRoot)
        {
            if (PreviewCache.TryGetValue(cacheKey, out Bitmap? cached))
            {
                return cached;
            }

            using SKBitmap? bitmap = RenderSquareBitmap(unicodeSequence, size, PreviewPadding);
            if (bitmap is null)
            {
                PreviewCache[cacheKey] = null;
                return null;
            }

            Bitmap preview = BitmapConversionHelpers.ToAvaloniBitmap(bitmap);
            PreviewCache[cacheKey] = preview;
            return preview;
        }
    }

    public static SKBitmap? RenderStickerBitmap(string unicodeSequence, int size = 160)
    {
        if (string.IsNullOrWhiteSpace(unicodeSequence) || size <= 0)
        {
            return null;
        }

        return RenderStickerBitmapCore(unicodeSequence, size);
    }

    public static SKBitmap? RenderInteractiveStickerBitmap(string unicodeSequence, int size = 160)
    {
        if (string.IsNullOrWhiteSpace(unicodeSequence) || size <= 0)
        {
            return null;
        }

        return RenderStickerBitmapCore(unicodeSequence, GetInteractiveStickerSize(size));
    }

    /// <summary>Upstream's size quantisation, preserved verbatim.</summary>
    public static int GetInteractiveStickerSize(int size)
    {
        size = Math.Max(1, size);

        if (size <= 64)
        {
            return size;
        }

        int step = size <= 128 ? 4 : size <= 256 ? 8 : 12;
        return Math.Max(64, (int)Math.Round(size / (double)step) * step);
    }

    private static SKBitmap? RenderStickerBitmapCore(string unicodeSequence, int size)
    {
        string cacheKey = $"{unicodeSequence}:{size}";

        lock (SyncRoot)
        {
            if (StickerCache.TryGetValue(cacheKey, out SKBitmap? cached))
            {
                return cached?.Copy();
            }

            SKBitmap? bitmap = RenderSquareBitmap(unicodeSequence, size, StickerPadding);

            // Bounded cache, as upstream has: sticker bitmaps are large.
            if (StickerCache.Count >= MaxStickerCacheEntries)
            {
                foreach (SKBitmap? entry in StickerCache.Values)
                {
                    entry?.Dispose();
                }

                StickerCache.Clear();
            }

            StickerCache[cacheKey] = bitmap;
            return bitmap?.Copy();
        }
    }

    private static SKBitmap? RenderSquareBitmap(string unicodeSequence, int size, int padding)
    {
        int inner = Math.Max(1, size - (padding * 2));

        var bitmap = new SKBitmap(size, size, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        using var font = new SKFont(EmojiTypeface.Value, inner);
        using var paint = new SKPaint { IsAntialias = true };

        // Colour emoji fonts carry their own colour, so no colour is set on the
        // paint; setting one would tint or flatten the glyph.
        font.Subpixel = true;

        SKRect bounds = default;
        float advance = font.MeasureText(unicodeSequence, out bounds, paint);

        if (advance <= 0 || bounds.Width <= 0 || bounds.Height <= 0)
        {
            // No glyph for this sequence: return null rather than an empty
            // bitmap, so the caller can fall back instead of drawing nothing.
            bitmap.Dispose();
            return null;
        }

        // Centre the glyph's ink box inside the square.
        float x = ((size - bounds.Width) / 2f) - bounds.Left;
        float y = ((size - bounds.Height) / 2f) - bounds.Top;

        canvas.DrawText(unicodeSequence, x, y, font, paint);
        return bitmap;
    }
}
