using SkiaSharp;
using ShareX.Core.Annotation;
using ShareX.Core.Geometry;

namespace ShareX.Mac.App.Services;

/// <summary>
/// Burns annotations into the captured image.
///
/// Without this the toolbar would only ever be a preview, which is the exact
/// "control whose handler does nothing" failure PROJECT-SPEC.md forbids. The
/// source is the frozen still shown behind the overlay, so what is rasterised
/// here is what the user saw.
///
/// Coordinates: shape bounds are in overlay DIPs, which on macOS are AppKit
/// points relative to the display's origin. The still is in physical pixels, so
/// every coordinate is multiplied by the composition scale, and the crop is
/// applied in the same space.
/// </summary>
public static class AnnotationRasterizer
{
    /// <summary>
    /// Crops <paramref name="framePng"/> to <paramref name="selectionInDisplayPoints"/>
    /// and draws the annotations on top. Returns PNG bytes, or null when the
    /// source cannot be decoded.
    /// </summary>
    public static byte[]? Render(
        byte[] framePng,
        PointRect selectionInDisplayPoints,
        IReadOnlyList<AnnotationShape> shapes,
        double scale)
    {
        using SKBitmap? frame = SKBitmap.Decode(framePng);
        if (frame is null)
        {
            return null;
        }

        // Crop rectangle in the still's pixel space.
        var crop = SKRectI.Create(
            (int)Math.Round(selectionInDisplayPoints.X * scale),
            (int)Math.Round(selectionInDisplayPoints.Y * scale),
            (int)Math.Round(selectionInDisplayPoints.Width * scale),
            (int)Math.Round(selectionInDisplayPoints.Height * scale));

        // Clamp to the frame: a selection that starts off-screen must not read
        // outside the bitmap.
        crop = SKRectI.Intersect(crop, new SKRectI(0, 0, frame.Width, frame.Height));
        if (crop.Width <= 0 || crop.Height <= 0)
        {
            return null;
        }

        var info = new SKImageInfo(crop.Width, crop.Height,
            SKColorType.Bgra8888, SKAlphaType.Premul);

        using var surface = SKSurface.Create(info);
        if (surface is null)
        {
            return null;
        }

        SKCanvas canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        // 1. the cropped screen content
        canvas.DrawBitmap(frame, crop,
            new SKRect(0, 0, crop.Width, crop.Height));

        // 2. shift into crop-local pixel space so every shape below can be drawn
        //    with the same transform.
        canvas.Save();
        canvas.Translate((float)(-selectionInDisplayPoints.X * scale),
            (float)(-selectionInDisplayPoints.Y * scale));
        canvas.Scale((float)scale);

        foreach (AnnotationShape shape in shapes.Where(s => s.IsEffect))
        {
            DrawEffect(canvas, frame, shape, scale);
        }

        foreach (AnnotationShape shape in shapes.Where(s => !s.IsEffect && !s.IsRegion))
        {
            DrawShape(canvas, frame, shape, scale);
        }

        canvas.Restore();

        using SKImage image = surface.Snapshot();
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static SKColor ToColor(uint argb) => new(
        (byte)((argb >> 16) & 0xFF), (byte)((argb >> 8) & 0xFF),
        (byte)(argb & 0xFF), (byte)((argb >> 24) & 0xFF));

    private static SKRect ToRect(PointRect r) =>
        new((float)r.X, (float)r.Y, (float)(r.X + r.Width), (float)(r.Y + r.Height));

    private static void DrawEffect(SKCanvas canvas, SKBitmap frame, AnnotationShape shape, double scale)
    {
        SKRect target = ToRect(shape.Bounds);

        switch (shape.Type)
        {
            case ShapeType.EffectHighlight:
            {
                using var paint = new SKPaint
                {
                    Color = ToColor(shape.StrokeColor).WithAlpha(90),
                    IsAntialias = true
                };
                canvas.DrawRect(target, paint);
                return;
            }

            case ShapeType.EffectBlur:
            {
                // A real Gaussian blur here, unlike the overlay's cheap
                // multi-draw approximation. The saved image is therefore at least
                // as obscured as the preview suggested, never less — which is the
                // safe direction for a tool people use to hide information.
                float sigma = Math.Clamp(shape.EffectStrength / 2f, 2f, 60f);
                using var blur = new SKPaint
                {
                    ImageFilter = SKImageFilter.CreateBlur(sigma, sigma),
                    IsAntialias = true
                };

                canvas.Save();
                canvas.ClipRect(target);
                DrawSourceRegion(canvas, frame, target, scale, blur);
                canvas.Restore();
                return;
            }

            case ShapeType.EffectPixelate:
            {
                int block = Math.Max(2, shape.EffectStrength);
                canvas.Save();
                canvas.ClipRect(target);

                // Downsample then upscale with nearest-neighbour: a true mosaic.
                int smallWidth = Math.Max(1, (int)(target.Width / block));
                int smallHeight = Math.Max(1, (int)(target.Height / block));

                var sourceRect = SKRectI.Create(
                    (int)(target.Left * scale), (int)(target.Top * scale),
                    Math.Max(1, (int)(target.Width * scale)),
                    Math.Max(1, (int)(target.Height * scale)));
                sourceRect = SKRectI.Intersect(sourceRect,
                    new SKRectI(0, 0, frame.Width, frame.Height));

                if (sourceRect.Width > 0 && sourceRect.Height > 0)
                {
                    using var small = new SKBitmap(smallWidth, smallHeight);
                    using (var temp = new SKCanvas(small))
                    {
                        temp.DrawBitmap(frame, sourceRect,
                            new SKRect(0, 0, smallWidth, smallHeight));
                    }

                    // Nearest-neighbour on the upscale is what makes it a mosaic
                    // rather than a smooth blur.
                    using var nearest = new SKPaint { IsAntialias = false };
                    using SKImage smallImage = SKImage.FromBitmap(small);
                    canvas.DrawImage(smallImage, target,
                        new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None), nearest);
                }

                canvas.Restore();
                return;
            }
        }
    }

    private static void DrawSourceRegion(
        SKCanvas canvas, SKBitmap frame, SKRect target, double scale, SKPaint paint)
    {
        var sourceRect = SKRectI.Create(
            (int)(target.Left * scale), (int)(target.Top * scale),
            Math.Max(1, (int)(target.Width * scale)),
            Math.Max(1, (int)(target.Height * scale)));
        sourceRect = SKRectI.Intersect(sourceRect, new SKRectI(0, 0, frame.Width, frame.Height));

        if (sourceRect.Width > 0 && sourceRect.Height > 0)
        {
            canvas.DrawBitmap(frame, sourceRect, target, paint);
        }
    }

    private static void DrawShape(SKCanvas canvas, SKBitmap frame, AnnotationShape shape, double scale)
    {
        SKRect r = ToRect(shape.Bounds);

        using var stroke = new SKPaint
        {
            Color = ToColor(shape.StrokeColor),
            StrokeWidth = (float)shape.StrokeWidth,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round
        };

        using var fill = new SKPaint
        {
            Color = ToColor(shape.FillColor),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        bool hasFill = (shape.FillColor >> 24) != 0;

        switch (shape.Type)
        {
            case ShapeType.DrawingRectangle:
                if (hasFill)
                {
                    canvas.DrawRoundRect(r, shape.CornerRadius, shape.CornerRadius, fill);
                }

                canvas.DrawRoundRect(r, shape.CornerRadius, shape.CornerRadius, stroke);
                break;

            case ShapeType.DrawingEllipse:
                if (hasFill)
                {
                    canvas.DrawOval(r, fill);
                }

                canvas.DrawOval(r, stroke);
                break;

            case ShapeType.DrawingLine:
                canvas.DrawLine(r.Left, r.Top, r.Right, r.Bottom, stroke);
                break;

            case ShapeType.DrawingArrow:
                canvas.DrawLine(r.Left, r.Top, r.Right, r.Bottom, stroke);
                DrawArrowHead(canvas, stroke, new SKPoint(r.Left, r.Top),
                    new SKPoint(r.Right, r.Bottom), shape.StrokeWidth);
                break;

            case ShapeType.DrawingFreehand:
            case ShapeType.DrawingFreehandArrow:
            {
                if (shape.Points.Count < 2)
                {
                    break;
                }

                using var path = new SKPath();
                path.MoveTo((float)shape.Points[0].X, (float)shape.Points[0].Y);
                for (int i = 1; i < shape.Points.Count; i++)
                {
                    path.LineTo((float)shape.Points[i].X, (float)shape.Points[i].Y);
                }

                canvas.DrawPath(path, stroke);

                if (shape.Type == ShapeType.DrawingFreehandArrow)
                {
                    AnnotationPoint a = shape.Points[^2];
                    AnnotationPoint b = shape.Points[^1];
                    DrawArrowHead(canvas, stroke,
                        new SKPoint((float)a.X, (float)a.Y),
                        new SKPoint((float)b.X, (float)b.Y), shape.StrokeWidth);
                }

                break;
            }

            case ShapeType.DrawingTextOutline:
                DrawText(canvas, shape, r, withBackground: false);
                break;

            case ShapeType.DrawingTextBackground:
                DrawText(canvas, shape, r, withBackground: true);
                break;

            case ShapeType.DrawingSpeechBalloon:
                DrawBalloon(canvas, shape, r, stroke);
                break;

            case ShapeType.DrawingStep:
                DrawStep(canvas, shape, r);
                break;

            case ShapeType.DrawingMagnify:
            {
                float zoom = 2.5f;
                var srcCentre = new SKRect(
                    r.MidX - (r.Width / (2 * zoom)), r.MidY - (r.Height / (2 * zoom)),
                    r.MidX + (r.Width / (2 * zoom)), r.MidY + (r.Height / (2 * zoom)));

                using var clip = new SKPath();
                clip.AddOval(r);

                canvas.Save();
                canvas.ClipPath(clip);
                DrawSourceRegion(canvas, frame, srcCentre, scale, new SKPaint { IsAntialias = true });
                canvas.Restore();
                canvas.DrawOval(r, stroke);
                break;
            }

            case ShapeType.ToolSpotlight:
            {
                // Darkens everything except the region. Drawn as four rectangles
                // rather than an inverse clip so the result is exact at the edges.
                using var dim = new SKPaint { Color = new SKColor(0, 0, 0, 150) };
                SKRect bounds = canvas.LocalClipBounds;

                canvas.DrawRect(new SKRect(bounds.Left, bounds.Top, bounds.Right, r.Top), dim);
                canvas.DrawRect(new SKRect(bounds.Left, r.Bottom, bounds.Right, bounds.Bottom), dim);
                canvas.DrawRect(new SKRect(bounds.Left, r.Top, r.Left, r.Bottom), dim);
                canvas.DrawRect(new SKRect(r.Right, r.Top, bounds.Right, r.Bottom), dim);
                canvas.DrawRect(r, stroke);
                break;
            }
        }
    }

    private static void DrawArrowHead(
        SKCanvas canvas, SKPaint stroke, SKPoint from, SKPoint to, double width)
    {
        double angle = Math.Atan2(to.Y - from.Y, to.X - from.X);
        double length = Math.Max(10, width * 5);
        const double spread = Math.PI / 7;

        using var head = new SKPath();
        head.MoveTo(to);
        head.LineTo(
            (float)(to.X - (length * Math.Cos(angle - spread))),
            (float)(to.Y - (length * Math.Sin(angle - spread))));
        head.LineTo(
            (float)(to.X - (length * Math.Cos(angle + spread))),
            (float)(to.Y - (length * Math.Sin(angle + spread))));
        head.Close();

        using var solid = new SKPaint
        {
            Color = stroke.Color, Style = SKPaintStyle.Fill, IsAntialias = true
        };
        canvas.DrawPath(head, solid);
    }

    private static void DrawText(SKCanvas canvas, AnnotationShape shape, SKRect r, bool withBackground)
    {
        if (string.IsNullOrEmpty(shape.Text))
        {
            return;
        }

        float size = (float)Math.Max(12, shape.StrokeWidth * 8);
        using var font = new SKFont(SKTypeface.Default, size);
        using var paint = new SKPaint { Color = ToColor(shape.StrokeColor), IsAntialias = true };

        float width = font.MeasureText(shape.Text);
        SKFontMetrics metrics = font.Metrics;
        float baseline = r.Top - metrics.Ascent;

        if (withBackground)
        {
            using var background = new SKPaint
            {
                Color = (shape.FillColor >> 24) == 0
                    ? new SKColor(255, 255, 255, 220)
                    : ToColor(shape.FillColor),
                IsAntialias = true
            };

            canvas.DrawRoundRect(
                new SKRect(r.Left - 4, r.Top - 2, r.Left + width + 4,
                    r.Top + (metrics.Descent - metrics.Ascent) + 2),
                3, 3, background);
        }

        canvas.DrawText(shape.Text, r.Left, baseline, font, paint);
    }

    private static void DrawBalloon(SKCanvas canvas, AnnotationShape shape, SKRect r, SKPaint stroke)
    {
        float radius = Math.Min(12, Math.Min(r.Width, r.Height) / 4);

        using var body = new SKPaint
        {
            Color = (shape.FillColor >> 24) == 0
                ? new SKColor(255, 255, 255, 230)
                : ToColor(shape.FillColor),
            IsAntialias = true
        };

        canvas.DrawRoundRect(r, radius, radius, body);
        canvas.DrawRoundRect(r, radius, radius, stroke);

        using var tail = new SKPath();
        tail.MoveTo(r.Left + (r.Width * 0.2f), r.Bottom);
        tail.LineTo(r.Left + (r.Width * 0.12f), r.Bottom + Math.Min(24, r.Height * 0.4f));
        tail.LineTo(r.Left + (r.Width * 0.38f), r.Bottom);
        tail.Close();

        canvas.DrawPath(tail, body);
        canvas.DrawPath(tail, stroke);

        if (!string.IsNullOrEmpty(shape.Text))
        {
            using var font = new SKFont(SKTypeface.Default,
                (float)Math.Max(12, shape.StrokeWidth * 7));
            using var text = new SKPaint { Color = SKColors.Black, IsAntialias = true };
            canvas.DrawText(shape.Text, r.Left + 8,
                r.Top + 6 - font.Metrics.Ascent, font, text);
        }
    }

    private static void DrawStep(SKCanvas canvas, AnnotationShape shape, SKRect r)
    {
        float diameter = Math.Max(24, Math.Min(r.Width, r.Height));
        var centre = new SKPoint(r.Left + (diameter / 2), r.Top + (diameter / 2));

        using var fill = new SKPaint
        {
            Color = ToColor(shape.StrokeColor), IsAntialias = true
        };
        using var ring = new SKPaint
        {
            Color = SKColors.White, Style = SKPaintStyle.Stroke,
            StrokeWidth = 2, IsAntialias = true
        };

        canvas.DrawCircle(centre, diameter / 2, fill);
        canvas.DrawCircle(centre, diameter / 2, ring);

        string label = shape.StepNumber.ToString();
        using var font = new SKFont(
            SKTypeface.FromFamilyName(null, SKFontStyle.Bold), diameter * 0.55f);
        using var paint = new SKPaint { Color = SKColors.White, IsAntialias = true };

        float width = font.MeasureText(label);
        canvas.DrawText(label, centre.X - (width / 2),
            centre.Y - ((font.Metrics.Ascent + font.Metrics.Descent) / 2), font, paint);
    }
}
