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
// ShareX.ImageEditor/Presentation/Rendering/WindowsCursorBitmapRenderer.cs
// (commit d2502561f63fc3ff502cacd91514e3f7f2948c74), which loaded system cursor
// bitmaps through user32/gdi32 (LoadCursor / GetIconInfo / GetDIBits) and, as a
// last resort, reflected onto System.Windows.Forms.Cursors.
//
// None of that exists on macOS, and extracting Apple's system cursor artwork is
// not something this port does. The cursor shapes are therefore DRAWN with Skia
// from vector paths.
//
// RECORDED PLATFORM DIFFERENCE (see planning/adr/0004-editor-fork-strategy.md):
// the rendered cursor will not be pixel-identical to the Windows system cursor,
// nor to the macOS system cursor. The annotation's semantics - which cursor type
// is placed, at what size, with what hotspot - are preserved; its exact pixels
// are not, and this is not claimed as parity.

using Avalonia.Media.Imaging;
using ShareX.ImageEditor.Core.Annotations;
using SkiaSharp;

namespace ShareX.ImageEditor.Presentation.Rendering
{
    public static class MacCursorBitmapRenderer
    {
        private static readonly Dictionary<CursorType, SKBitmap?> AnnotationBitmapCache = new();
        private static readonly Dictionary<(CursorType CursorType, int PreviewSize), Bitmap?> PreviewBitmapCache = new();
        private static readonly object CacheLock = new();

        private const int AnnotationSize = 32;

        /// <summary>
        /// Bitmap used when a cursor annotation is placed on the canvas.
        /// Cached per cursor type, as upstream did.
        /// </summary>
        public static SKBitmap? CreateAnnotationBitmap(CursorType cursorType)
        {
            lock (CacheLock)
            {
                if (AnnotationBitmapCache.TryGetValue(cursorType, out SKBitmap? cached))
                {
                    return cached?.Copy();
                }

                SKBitmap? rendered = Render(cursorType, AnnotationSize);
                AnnotationBitmapCache[cursorType] = rendered;
                return rendered?.Copy();
            }
        }

        /// <summary>Small bitmap for the toolbar's cursor-type picker.</summary>
        public static Bitmap? GetPreviewBitmap(CursorType cursorType, int previewSize = 28)
        {
            lock (CacheLock)
            {
                (CursorType cursorType, int previewSize) key = (cursorType, previewSize);
                if (PreviewBitmapCache.TryGetValue(key, out Bitmap? cached))
                {
                    return cached;
                }

                Bitmap? preview = null;
                using (SKBitmap? skia = Render(cursorType, previewSize))
                {
                    if (skia is not null)
                    {
                        using SKImage image = SKImage.FromBitmap(skia);
                        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
                        using var stream = new MemoryStream(data.ToArray());
                        preview = new Bitmap(stream);
                    }
                }

                PreviewBitmapCache[key] = preview;
                return preview;
            }
        }

        private static SKBitmap? Render(CursorType cursorType, int size)
        {
            if (size <= 0)
            {
                return null;
            }

            var bitmap = new SKBitmap(size, size, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Transparent);

            using var fill = new SKPaint
            {
                Color = SKColors.White, Style = SKPaintStyle.Fill, IsAntialias = true
            };
            using var outline = new SKPaint
            {
                Color = SKColors.Black, Style = SKPaintStyle.Stroke,
                StrokeWidth = Math.Max(1f, size / 16f), IsAntialias = true,
                StrokeJoin = SKStrokeJoin.Miter
            };

            float s = size;

            switch (cursorType)
            {
                case CursorType.IBeam:
                    DrawIBeam(canvas, s, fill, outline);
                    break;

                case CursorType.Cross:
                    DrawCrosshair(canvas, s, outline);
                    break;

                case CursorType.Hand:
                    DrawHand(canvas, s, fill, outline);
                    break;

                case CursorType.SizeNS:
                case CursorType.PanNorth:
                case CursorType.PanSouth:
                case CursorType.NoMoveVert:
                    DrawDoubleArrow(canvas, s, vertical: true, fill, outline);
                    break;

                case CursorType.SizeWE:
                case CursorType.PanEast:
                case CursorType.PanWest:
                case CursorType.NoMoveHoriz:
                    DrawDoubleArrow(canvas, s, vertical: false, fill, outline);
                    break;

                case CursorType.SizeAll:
                case CursorType.NoMove2D:
                    DrawDoubleArrow(canvas, s, vertical: true, fill, outline);
                    DrawDoubleArrow(canvas, s, vertical: false, fill, outline);
                    break;

                case CursorType.SizeNESW:
                case CursorType.SizeNWSE:
                case CursorType.PanNE:
                case CursorType.PanNW:
                case CursorType.PanSE:
                case CursorType.PanSW:
                    DrawDiagonalArrow(canvas, s,
                        cursorType is CursorType.SizeNESW or CursorType.PanNE or CursorType.PanSW,
                        fill, outline);
                    break;

                case CursorType.No:
                    DrawNo(canvas, s, outline);
                    break;

                case CursorType.Help:
                    DrawPointer(canvas, s, fill, outline);
                    DrawQuestionMark(canvas, s);
                    break;

                case CursorType.WaitCursor:
                case CursorType.AppStarting:
                    DrawWait(canvas, s, fill, outline);
                    break;

                default:
                    // Arrow, Default, HSplit, VSplit, UpArrow and anything added
                    // upstream later: the standard pointer is the honest generic
                    // shape rather than an empty bitmap.
                    DrawPointer(canvas, s, fill, outline);
                    break;
            }

            return bitmap;
        }

        private static void DrawPointer(SKCanvas canvas, float s, SKPaint fill, SKPaint outline)
        {
            using var path = new SKPath();
            path.MoveTo(s * 0.18f, s * 0.08f);
            path.LineTo(s * 0.18f, s * 0.78f);
            path.LineTo(s * 0.37f, s * 0.60f);
            path.LineTo(s * 0.50f, s * 0.90f);
            path.LineTo(s * 0.62f, s * 0.84f);
            path.LineTo(s * 0.49f, s * 0.55f);
            path.LineTo(s * 0.70f, s * 0.52f);
            path.Close();

            canvas.DrawPath(path, fill);
            canvas.DrawPath(path, outline);
        }

        private static void DrawIBeam(SKCanvas canvas, float s, SKPaint fill, SKPaint outline)
        {
            float x = s * 0.5f;
            canvas.DrawLine(x, s * 0.12f, x, s * 0.88f, outline);
            canvas.DrawLine(s * 0.34f, s * 0.12f, s * 0.66f, s * 0.12f, outline);
            canvas.DrawLine(s * 0.34f, s * 0.88f, s * 0.66f, s * 0.88f, outline);
        }

        private static void DrawCrosshair(SKCanvas canvas, float s, SKPaint outline)
        {
            canvas.DrawLine(s * 0.5f, s * 0.06f, s * 0.5f, s * 0.94f, outline);
            canvas.DrawLine(s * 0.06f, s * 0.5f, s * 0.94f, s * 0.5f, outline);
        }

        private static void DrawHand(SKCanvas canvas, float s, SKPaint fill, SKPaint outline)
        {
            using var path = new SKPath();
            path.AddRoundRect(new SKRect(s * 0.34f, s * 0.30f, s * 0.66f, s * 0.88f), s * 0.10f, s * 0.10f);
            canvas.DrawPath(path, fill);
            canvas.DrawPath(path, outline);

            // Index finger.
            using var finger = new SKPath();
            finger.AddRoundRect(new SKRect(s * 0.44f, s * 0.10f, s * 0.56f, s * 0.36f), s * 0.06f, s * 0.06f);
            canvas.DrawPath(finger, fill);
            canvas.DrawPath(finger, outline);
        }

        private static void DrawDoubleArrow(
            SKCanvas canvas, float s, bool vertical, SKPaint fill, SKPaint outline)
        {
            float head = s * 0.16f;

            if (vertical)
            {
                canvas.DrawLine(s * 0.5f, s * 0.18f, s * 0.5f, s * 0.82f, outline);
                Triangle(canvas, fill, outline,
                    new SKPoint(s * 0.5f, s * 0.08f),
                    new SKPoint(s * 0.5f - head, s * 0.26f),
                    new SKPoint(s * 0.5f + head, s * 0.26f));
                Triangle(canvas, fill, outline,
                    new SKPoint(s * 0.5f, s * 0.92f),
                    new SKPoint(s * 0.5f - head, s * 0.74f),
                    new SKPoint(s * 0.5f + head, s * 0.74f));
            }
            else
            {
                canvas.DrawLine(s * 0.18f, s * 0.5f, s * 0.82f, s * 0.5f, outline);
                Triangle(canvas, fill, outline,
                    new SKPoint(s * 0.08f, s * 0.5f),
                    new SKPoint(s * 0.26f, s * 0.5f - head),
                    new SKPoint(s * 0.26f, s * 0.5f + head));
                Triangle(canvas, fill, outline,
                    new SKPoint(s * 0.92f, s * 0.5f),
                    new SKPoint(s * 0.74f, s * 0.5f - head),
                    new SKPoint(s * 0.74f, s * 0.5f + head));
            }
        }

        private static void DrawDiagonalArrow(
            SKCanvas canvas, float s, bool neSw, SKPaint fill, SKPaint outline)
        {
            canvas.Save();
            canvas.RotateDegrees(neSw ? -45 : 45, s / 2, s / 2);
            DrawDoubleArrow(canvas, s, vertical: true, fill, outline);
            canvas.Restore();
        }

        private static void DrawNo(SKCanvas canvas, float s, SKPaint outline)
        {
            using var thick = new SKPaint
            {
                Color = SKColors.Red, Style = SKPaintStyle.Stroke,
                StrokeWidth = Math.Max(2f, s / 8f), IsAntialias = true
            };

            canvas.DrawCircle(s / 2, s / 2, s * 0.36f, thick);
            canvas.DrawLine(s * 0.25f, s * 0.75f, s * 0.75f, s * 0.25f, thick);
        }

        private static void DrawQuestionMark(SKCanvas canvas, float s)
        {
            using var font = new SKFont(SKTypeface.FromFamilyName(null, SKFontStyle.Bold), s * 0.5f);
            using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
            canvas.DrawText("?", s * 0.58f, s * 0.5f, font, paint);
        }

        private static void DrawWait(SKCanvas canvas, float s, SKPaint fill, SKPaint outline)
        {
            // A spinner-like ring: the closest honest static rendering of a busy
            // cursor without animating.
            using var arc = new SKPaint
            {
                Color = SKColors.Black, Style = SKPaintStyle.Stroke,
                StrokeWidth = Math.Max(2f, s / 9f), IsAntialias = true,
                StrokeCap = SKStrokeCap.Round
            };

            var bounds = new SKRect(s * 0.18f, s * 0.18f, s * 0.82f, s * 0.82f);
            using var path = new SKPath();
            path.AddArc(bounds, -90, 270);
            canvas.DrawPath(path, arc);
        }

        private static void Triangle(
            SKCanvas canvas, SKPaint fill, SKPaint outline, SKPoint a, SKPoint b, SKPoint c)
        {
            using var path = new SKPath();
            path.MoveTo(a);
            path.LineTo(b);
            path.LineTo(c);
            path.Close();

            canvas.DrawPath(path, fill);
            canvas.DrawPath(path, outline);
        }
    }
}
