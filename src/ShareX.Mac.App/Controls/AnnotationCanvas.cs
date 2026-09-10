using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using ShareX.Core.Annotation;
using ShareX.Core.Geometry;

namespace ShareX.Mac.App.Controls;

/// <summary>
/// Renders the frozen screen, the annotation shapes and the selection chrome.
///
/// The screen is FROZEN behind the overlay, the way upstream's region capture
/// works: a still of the display is shown, the user selects and annotates on top
/// of it, and the final image is cropped from that same still. That makes the
/// blur/pixelate/highlight effects previewable exactly as they will be saved,
/// and removes any race between the overlay and a moving desktop.
/// </summary>
public sealed class AnnotationCanvas : Control
{
    private readonly AnnotationSurface _surface;

    /// <summary>Frozen screen content, in window coordinates (1 DIP = 1 point).</summary>
    public Bitmap? Background { get; set; }

    /// <summary>Current selection rectangle in window DIPs, or null.</summary>
    public Rect? Selection { get; set; }

    /// <summary>Shape being dragged out right now, drawn as a live preview.</summary>
    public AnnotationShape? Pending { get; set; }

    public Point? Pointer { get; set; }
    public bool ShowGuides { get; set; } = true;
    public bool DimOutsideSelection { get; set; } = true;

    public AnnotationCanvas(AnnotationSurface surface) => _surface = surface;

    public override void Render(DrawingContext context)
    {
        Rect full = new(Bounds.Size);

        // 1. the frozen screen
        if (Background is { } background)
        {
            context.DrawImage(background, full);
        }
        else
        {
            // Without a still we must not pretend to show the desktop; a flat
            // scrim makes it obvious the background is unavailable.
            context.FillRectangle(new SolidColorBrush(Color.FromArgb(160, 20, 20, 20)), full);
        }

        // 2. effect shapes read from the frozen background, so they must be
        //    drawn before the dimming and before the drawing shapes.
        foreach (AnnotationShape shape in _surface.Shapes.Where(s => s.IsEffect))
        {
            DrawEffect(context, shape);
        }

        // 3. dim everything outside the selection
        if (DimOutsideSelection && Selection is { } selection)
        {
            DrawDimming(context, full, selection);
        }
        else if (DimOutsideSelection)
        {
            context.FillRectangle(new SolidColorBrush(Color.FromArgb(90, 0, 0, 0)), full);
        }

        // 4. drawing shapes
        foreach (AnnotationShape shape in _surface.Shapes.Where(s => !s.IsEffect && !s.IsRegion))
        {
            DrawShape(context, shape);
        }

        // 5. the live preview of whatever is being dragged out
        if (Pending is { } pending)
        {
            if (pending.IsEffect)
            {
                DrawEffect(context, pending);
            }
            else
            {
                DrawShape(context, pending);
            }
        }

        // 6. selection outline and handles
        if (Selection is { } sel)
        {
            var pen = new Pen(Brushes.White, 1);
            context.DrawRectangle(null, pen, sel);
        }

        if (_surface.Selected is { } selectedShape)
        {
            DrawHandles(context, ToRect(selectedShape.Bounds));
        }

        // 7. crosshair guides, only before a selection exists
        if (ShowGuides && Selection is null && Pointer is { } pointer)
        {
            var guide = new Pen(new SolidColorBrush(Color.FromArgb(128, 255, 255, 255)), 1);
            context.DrawLine(guide, new Point(0, pointer.Y), new Point(Bounds.Width, pointer.Y));
            context.DrawLine(guide, new Point(pointer.X, 0), new Point(pointer.X, Bounds.Height));
        }
    }

    private static Rect ToRect(PointRect r) => new(r.X, r.Y, r.Width, r.Height);

    private static Color ToColor(uint argb) => Color.FromArgb(
        (byte)((argb >> 24) & 0xFF), (byte)((argb >> 16) & 0xFF),
        (byte)((argb >> 8) & 0xFF), (byte)(argb & 0xFF));

    private void DrawDimming(DrawingContext context, Rect full, Rect selection)
    {
        var dim = new SolidColorBrush(Color.FromArgb(110, 0, 0, 0));

        context.FillRectangle(dim, new Rect(0, 0, full.Width, selection.Y));
        context.FillRectangle(dim, new Rect(0, selection.Bottom, full.Width,
            Math.Max(0, full.Height - selection.Bottom)));
        context.FillRectangle(dim, new Rect(0, selection.Y, selection.X, selection.Height));
        context.FillRectangle(dim, new Rect(selection.Right, selection.Y,
            Math.Max(0, full.Width - selection.Right), selection.Height));
    }

    /// <summary>
    /// Blur / pixelate / highlight, sampled from the frozen background so the
    /// preview is what gets saved.
    /// </summary>
    private void DrawEffect(DrawingContext context, AnnotationShape shape)
    {
        Rect target = ToRect(shape.Bounds);
        if (target.Width < 1 || target.Height < 1)
        {
            return;
        }

        switch (shape.Type)
        {
            case ShapeType.EffectHighlight:
                // Translucent colour wash, so the underlying pixels stay legible.
                context.FillRectangle(
                    new SolidColorBrush(ToColor(shape.StrokeColor), 0.35), target);
                return;

            case ShapeType.EffectPixelate when Background is { } source:
            {
                // Draw the region repeatedly at block granularity: sampling one
                // source block into one destination block is a real mosaic, not
                // a blur approximation.
                int block = Math.Max(2, shape.EffectStrength);
                for (double y = target.Y; y < target.Bottom; y += block)
                {
                    for (double x = target.X; x < target.Right; x += block)
                    {
                        double w = Math.Min(block, target.Right - x);
                        double h = Math.Min(block, target.Bottom - y);

                        // One source pixel stretched over the block.
                        var src = new Rect(x, y, 1, 1);
                        context.DrawImage(source, src, new Rect(x, y, w, h));
                    }
                }

                return;
            }

            case ShapeType.EffectBlur when Background is { } blurSource:
            {
                // Avalonia's DrawingContext has no blur filter, so this is a
                // box-blur approximation: the region is redrawn several times at
                // small offsets with low opacity. Recorded as an approximation
                // rather than presented as a true Gaussian blur; the saved image
                // is produced by the same routine, so preview and output agree.
                int radius = Math.Clamp(shape.EffectStrength / 3, 1, 12);
                using (context.PushOpacity(0.2))
                {
                    for (int dx = -radius; dx <= radius; dx += Math.Max(1, radius / 3))
                    {
                        for (int dy = -radius; dy <= radius; dy += Math.Max(1, radius / 3))
                        {
                            var src = new Rect(
                                target.X + dx, target.Y + dy, target.Width, target.Height);
                            context.DrawImage(blurSource, src, target);
                        }
                    }
                }

                return;
            }

            default:
                // No background to sample: show the region explicitly rather
                // than silently drawing nothing.
                context.FillRectangle(
                    new SolidColorBrush(Color.FromArgb(150, 40, 40, 40)), target);
                context.DrawRectangle(null,
                    new Pen(new SolidColorBrush(Color.FromArgb(200, 255, 149, 0)), 1), target);
                return;
        }
    }

    private void DrawShape(DrawingContext context, AnnotationShape shape)
    {
        Rect r = ToRect(shape.Bounds);
        var stroke = new Pen(new SolidColorBrush(ToColor(shape.StrokeColor)), shape.StrokeWidth);
        IBrush? fill = (shape.FillColor >> 24) == 0
            ? null
            : new SolidColorBrush(ToColor(shape.FillColor));

        switch (shape.Type)
        {
            case ShapeType.DrawingRectangle:
                if (shape.CornerRadius > 0)
                {
                    context.DrawRectangle(fill, stroke, r, shape.CornerRadius, shape.CornerRadius);
                }
                else
                {
                    context.DrawRectangle(fill, stroke, r);
                }

                break;

            case ShapeType.DrawingEllipse:
                context.DrawEllipse(fill, stroke, r.Center,
                    r.Width / 2, r.Height / 2);
                break;

            case ShapeType.DrawingLine:
                context.DrawLine(stroke, r.TopLeft, r.BottomRight);
                break;

            case ShapeType.DrawingArrow:
                DrawArrow(context, stroke, r.TopLeft, r.BottomRight, shape.StrokeWidth);
                break;

            case ShapeType.DrawingFreehand:
            case ShapeType.DrawingFreehandArrow:
                DrawPolyline(context, stroke, shape);
                if (shape.Type == ShapeType.DrawingFreehandArrow && shape.Points.Count >= 2)
                {
                    AnnotationPoint a = shape.Points[^2];
                    AnnotationPoint b = shape.Points[^1];
                    DrawArrowHead(context, stroke, new Point(a.X, a.Y), new Point(b.X, b.Y),
                        shape.StrokeWidth);
                }

                break;

            case ShapeType.DrawingTextOutline:
                DrawText(context, shape, r, withBackground: false);
                break;

            case ShapeType.DrawingTextBackground:
                DrawText(context, shape, r, withBackground: true);
                break;

            case ShapeType.DrawingSpeechBalloon:
                DrawSpeechBalloon(context, shape, r, stroke, fill);
                break;

            case ShapeType.DrawingStep:
                DrawStep(context, shape, r);
                break;

            case ShapeType.DrawingMagnify when Background is { } source:
            {
                // Draws a zoomed copy of the region's centre, clipped to a circle.
                double zoom = 2.5;
                var srcRect = new Rect(
                    r.Center.X - (r.Width / (2 * zoom)),
                    r.Center.Y - (r.Height / (2 * zoom)),
                    r.Width / zoom, r.Height / zoom);

                var clip = new EllipseGeometry(r);
                using (context.PushGeometryClip(clip))
                {
                    context.DrawImage(source, srcRect, r);
                }

                context.DrawEllipse(null, stroke, r.Center, r.Width / 2, r.Height / 2);
                break;
            }

            case ShapeType.ToolSpotlight:
            {
                // Inverse of a highlight: darken everything except this region.
                var dim = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0));
                context.FillRectangle(dim, new Rect(0, 0, Bounds.Width, r.Y));
                context.FillRectangle(dim, new Rect(0, r.Bottom, Bounds.Width,
                    Math.Max(0, Bounds.Height - r.Bottom)));
                context.FillRectangle(dim, new Rect(0, r.Y, r.X, r.Height));
                context.FillRectangle(dim, new Rect(r.Right, r.Y,
                    Math.Max(0, Bounds.Width - r.Right), r.Height));
                context.DrawRectangle(null, stroke, r);
                break;
            }

            default:
                // Only reached for tools the catalog marks unimplemented, whose
                // toolbar buttons are disabled, so nothing can be placed here.
                break;
        }
    }

    private static void DrawPolyline(DrawingContext context, Pen pen, AnnotationShape shape)
    {
        if (shape.Points.Count < 2)
        {
            return;
        }

        var geometry = new StreamGeometry();
        using (StreamGeometryContext path = geometry.Open())
        {
            path.BeginFigure(new Point(shape.Points[0].X, shape.Points[0].Y), false);
            for (int i = 1; i < shape.Points.Count; i++)
            {
                path.LineTo(new Point(shape.Points[i].X, shape.Points[i].Y));
            }

            path.EndFigure(false);
        }

        context.DrawGeometry(null, pen, geometry);
    }

    private static void DrawArrow(DrawingContext context, Pen pen, Point from, Point to, double width)
    {
        context.DrawLine(pen, from, to);
        DrawArrowHead(context, pen, from, to, width);
    }

    private static void DrawArrowHead(DrawingContext context, Pen pen, Point from, Point to, double width)
    {
        double angle = Math.Atan2(to.Y - from.Y, to.X - from.X);
        double length = Math.Max(10, width * 5);
        const double spread = Math.PI / 7;

        var left = new Point(
            to.X - (length * Math.Cos(angle - spread)),
            to.Y - (length * Math.Sin(angle - spread)));
        var right = new Point(
            to.X - (length * Math.Cos(angle + spread)),
            to.Y - (length * Math.Sin(angle + spread)));

        var head = new StreamGeometry();
        using (StreamGeometryContext path = head.Open())
        {
            path.BeginFigure(to, true);
            path.LineTo(left);
            path.LineTo(right);
            path.EndFigure(true);
        }

        context.DrawGeometry(pen.Brush, null, head);
    }

    private void DrawText(DrawingContext context, AnnotationShape shape, Rect r, bool withBackground)
    {
        if (string.IsNullOrEmpty(shape.Text))
        {
            // An empty text shape still shows its box while being placed, so the
            // user can see where the text will go.
            context.DrawRectangle(null,
                new Pen(new SolidColorBrush(ToColor(shape.StrokeColor)) { Opacity = 0.6 }, 1), r);
            return;
        }

        double fontSize = Math.Max(12, shape.StrokeWidth * 8);
        var text = new FormattedText(shape.Text, System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, fontSize,
            new SolidColorBrush(ToColor(shape.StrokeColor)));

        if (withBackground)
        {
            var background = (shape.FillColor >> 24) == 0
                ? new SolidColorBrush(Color.FromArgb(220, 255, 255, 255))
                : new SolidColorBrush(ToColor(shape.FillColor));

            context.FillRectangle(background,
                new Rect(r.X - 4, r.Y - 2, text.Width + 8, text.Height + 4), 3);
        }

        context.DrawText(text, r.TopLeft);
    }

    private void DrawSpeechBalloon(DrawingContext context, AnnotationShape shape, Rect r,
        Pen stroke, IBrush? fill)
    {
        double radius = Math.Min(12, Math.Min(r.Width, r.Height) / 4);
        IBrush body = fill ?? new SolidColorBrush(Color.FromArgb(230, 255, 255, 255));

        context.DrawRectangle(body, stroke, r, radius, radius);

        // Tail on the lower-left, as upstream draws it.
        var tail = new StreamGeometry();
        using (StreamGeometryContext path = tail.Open())
        {
            path.BeginFigure(new Point(r.X + (r.Width * 0.2), r.Bottom), true);
            path.LineTo(new Point(r.X + (r.Width * 0.12), r.Bottom + Math.Min(24, r.Height * 0.4)));
            path.LineTo(new Point(r.X + (r.Width * 0.38), r.Bottom));
            path.EndFigure(true);
        }

        context.DrawGeometry(body, stroke, tail);

        if (!string.IsNullOrEmpty(shape.Text))
        {
            var text = new FormattedText(shape.Text,
                System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                Typeface.Default, Math.Max(12, shape.StrokeWidth * 7), Brushes.Black);
            context.DrawText(text, new Point(r.X + 8, r.Y + 6));
        }
    }

    private static void DrawStep(DrawingContext context, AnnotationShape shape, Rect r)
    {
        double diameter = Math.Max(24, Math.Min(r.Width, r.Height));
        var box = new Rect(r.X, r.Y, diameter, diameter);
        var fill = new SolidColorBrush(ToColor(shape.StrokeColor));

        context.DrawEllipse(fill, new Pen(Brushes.White, 2), box.Center,
            diameter / 2, diameter / 2);

        var label = new FormattedText(shape.StepNumber.ToString(),
            System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold),
            diameter * 0.55, Brushes.White);

        context.DrawText(label, new Point(
            box.Center.X - (label.Width / 2), box.Center.Y - (label.Height / 2)));
    }

    private static void DrawHandles(DrawingContext context, Rect r)
    {
        const double size = 7;
        var brush = Brushes.White;
        var pen = new Pen(Brushes.Black, 1);

        foreach (Point p in new[]
        {
            r.TopLeft, new Point(r.Center.X, r.Y), r.TopRight,
            new Point(r.X, r.Center.Y), new Point(r.Right, r.Center.Y),
            r.BottomLeft, new Point(r.Center.X, r.Bottom), r.BottomRight
        })
        {
            context.DrawRectangle(brush, pen,
                new Rect(p.X - (size / 2), p.Y - (size / 2), size, size));
        }
    }
}
