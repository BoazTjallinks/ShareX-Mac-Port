using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Avalonia.Threading;
using ShareX.Core.Annotation;
using ShareX.Core.Geometry;
using ShareX.Mac.App.Controls;
using ShareX.Mac.App.ViewModels;

namespace ShareX.Mac.App.Views;

/// <summary>
/// Which upstream region command opened the selector. Upstream keeps these as
/// genuinely different interactions (PROJECT-SPEC.md section 5: "Maintain
/// distinct default/light/transparent interactions from the release source").
/// </summary>
public enum RegionSelectorMode
{
    /// <summary>RectangleRegion — dimmed overlay, guides, full annotation toolbar.</summary>
    Default,

    /// <summary>RectangleLight — reduced interaction: no dimming, no guides, no toolbar.</summary>
    Light,

    /// <summary>
    /// RectangleTransparent — no dimming, so the frozen desktop stays fully
    /// visible while selecting. This is the interaction mode; it is not a claim
    /// that content behind another window can be reconstructed.
    /// </summary>
    Transparent
}

/// <summary>
/// Interactive region selection with ShareX's annotation toolbar.
///
/// Coordinate handling: on macOS an Avalonia device-independent pixel is one
/// AppKit point, and this window covers exactly one display whose origin the
/// caller supplies in CoreGraphics global points. A selection in window DIPs plus
/// that origin is therefore already in global points — no scale arithmetic and no
/// opportunity to mix spaces (PROJECT-SPEC.md section 5).
///
/// The background is a FROZEN still of the display, as upstream's region capture
/// does. That makes blur/pixelate/highlight previews exact, and lets the caller
/// crop the final image from the same still.
/// </summary>
public partial class RegionSelectorWindow : Window
{
    private readonly TaskCompletionSource<PointRect?> _result =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly PointRect _displayBoundsGlobalPoints;
    private readonly RegionSelectorMode _mode;
    private readonly AnnotationSurface _surface = new();
    private readonly RegionSelectorViewModel _viewModel;
    private readonly AnnotationCanvas _canvas;

    private Point? _dragStart;
    private Rect _selection;
    private bool _completed;
    private bool _isDrawingShape;
    private AnnotationShape? _pendingShape;
    private List<AnnotationPoint>? _freehandPoints;
    private int _movingShapeIndex = -1;
    private Point _moveOrigin;

    private const double NudgeStep = 1;
    private const double NudgeStepLarge = 10;

    public RegionSelectorWindow()
        : this(new PointRect(0, 0, 1, 1, CoordinateSpace.CgGlobalPoints),
            RegionSelectorMode.Default)
    {
    }

    /// <param name="backgroundPng">
    /// Frozen still of the display. Optional so the window still works without
    /// one, but effects then render as an explicit placeholder rather than
    /// silently drawing nothing.
    /// </param>
    public RegionSelectorWindow(
        PointRect displayBoundsGlobalPoints,
        RegionSelectorMode mode,
        byte[]? backgroundPng = null)
    {
        InitializeComponent();

        _displayBoundsGlobalPoints = displayBoundsGlobalPoints;
        _mode = mode;

        Width = displayBoundsGlobalPoints.Width;
        Height = displayBoundsGlobalPoints.Height;

        _viewModel = new RegionSelectorViewModel(_surface);
        _viewModel.SurfaceChanged += () => _canvas?.InvalidateVisual();
        DataContext = _viewModel;

        _canvas = new AnnotationCanvas(_surface)
        {
            DimOutsideSelection = mode == RegionSelectorMode.Default,
            ShowGuides = mode == RegionSelectorMode.Default
        };

        if (backgroundPng is { Length: > 0 })
        {
            try
            {
                using var stream = new MemoryStream(backgroundPng);
                _canvas.Background = new Bitmap(stream);
            }
            catch (Exception)
            {
                // A corrupt still must not stop the user selecting a region; the
                // canvas falls back to its explicit no-background rendering.
            }
        }

        CanvasHost.Content = _canvas;

        // Light mode is deliberately the reduced interaction: no toolbar.
        Toolbar.IsVisible = mode != RegionSelectorMode.Light;

        TextEntry.KeyDown += OnTextEntryKeyDown;
        Opened += OnOpened;
    }

    public Task<PointRect?> SelectionTask => _result.Task;

    /// <summary>Annotations the user placed, for burning into the captured image.</summary>
    public IReadOnlyList<AnnotationShape> Annotations => _surface.Shapes;

    /// <summary>Confirmed selection, or null when cancelled.</summary>
    public PointRect? Selection { get; private set; }

    /// <summary>
    /// Walks the visual ancestry to see whether the event started inside the
    /// toolbar (or the text box), so a click on a control is not also
    /// interpreted as the start of a shape.
    /// </summary>
    private bool IsWithinToolbar(Visual? source)
    {
        for (Visual? current = source; current is not null; current = current.GetVisualParent())
        {
            if (ReferenceEquals(current, Toolbar) || ReferenceEquals(current, TextEntry))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsAnnotationTool =>
        _surface.CurrentTool is not (ShapeType.RegionRectangle
            or ShapeType.RegionEllipse or ShapeType.RegionFreehand);

    private void OnOpened(object? sender, EventArgs e)
    {
        Activate();
        Focus();
        UpdateReadout(null);
        PositionToolbar();
    }

    // ------------------------------------------------------------------ input

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        // Clicks on the toolbar belong to the toolbar, not to a drawing gesture.
        if (IsWithinToolbar(e.Source as Visual))
        {
            return;
        }

        PointerPoint point = e.GetCurrentPoint(this);

        // Upstream treats the right button as cancel during selection.
        if (point.Properties.IsRightButtonPressed)
        {
            Complete(null);
            return;
        }

        CommitPendingText();
        Point position = point.Position;

        if (_surface.CurrentTool == ShapeType.ToolSelect)
        {
            int hit = _surface.HitTest(new AnnotationPoint(position.X, position.Y));
            _surface.Select(hit);
            _movingShapeIndex = hit;
            _moveOrigin = position;
            _canvas.InvalidateVisual();
            return;
        }

        if (IsAnnotationTool)
        {
            BeginShape(position);
            return;
        }

        _dragStart = position;
        _selection = new Rect(position, new Size(0, 0));
        _canvas.Selection = _selection;
        _canvas.InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        Point position = e.GetPosition(this);
        _canvas.Pointer = position;

        if (_movingShapeIndex >= 0 && _surface.Shapes.Count > _movingShapeIndex)
        {
            double dx = position.X - _moveOrigin.X;
            double dy = position.Y - _moveOrigin.Y;
            _moveOrigin = position;

            _surface.Replace(_movingShapeIndex, _surface.Shapes[_movingShapeIndex].MovedBy(dx, dy));
            _canvas.InvalidateVisual();
            return;
        }

        if (_isDrawingShape)
        {
            UpdateShape(position);
            return;
        }

        if (_dragStart is { } start)
        {
            _selection = Normalize(start, position);
            _canvas.Selection = _selection;
        }

        _canvas.InvalidateVisual();
        UpdateReadout(position);
        PositionToolbar();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_movingShapeIndex >= 0)
        {
            _movingShapeIndex = -1;
            return;
        }

        if (_isDrawingShape)
        {
            FinishShape();
            return;
        }

        if (_dragStart is null)
        {
            return;
        }

        _dragStart = null;
        _selection = Normalize(_selection.TopLeft, _selection.BottomRight);

        // A click with no drag is a cancel, not a zero-pixel capture.
        if (_selection.Width < 1 || _selection.Height < 1)
        {
            Complete(null);
            return;
        }

        _canvas.Selection = _selection;
        _canvas.InvalidateVisual();
        UpdateReadout(null);
        PositionToolbar();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (TextEntry.IsVisible && TextEntry.IsFocused)
        {
            return;
        }

        bool command = e.KeyModifiers.HasFlag(KeyModifiers.Meta);

        switch (e.Key)
        {
            case Key.Escape:
                Complete(null);
                e.Handled = true;
                return;

            case Key.Enter:
                Confirm();
                e.Handled = true;
                return;

            // macOS uses Cmd where upstream uses Ctrl (PROJECT-SPEC.md section 11
            // permits the platform-equivalent modifier).
            case Key.Z when command && e.KeyModifiers.HasFlag(KeyModifiers.Shift):
            case Key.Y when command:
                _surface.Redo();
                _canvas.InvalidateVisual();
                e.Handled = true;
                return;

            case Key.Z when command:
                _surface.Undo();
                _canvas.InvalidateVisual();
                e.Handled = true;
                return;

            case Key.D when command:
                _surface.DuplicateSelected();
                _canvas.InvalidateVisual();
                e.Handled = true;
                return;

            case Key.Delete or Key.Back:
                _surface.RemoveSelected();
                _canvas.InvalidateVisual();
                e.Handled = true;
                return;

            case Key.Home:
                _surface.BringToFront();
                _canvas.InvalidateVisual();
                e.Handled = true;
                return;

            case Key.End:
                _surface.SendToBack();
                _canvas.InvalidateVisual();
                e.Handled = true;
                return;

            case Key.PageUp:
                _surface.MoveUp();
                _canvas.InvalidateVisual();
                e.Handled = true;
                return;

            case Key.PageDown:
                _surface.MoveDown();
                _canvas.InvalidateVisual();
                e.Handled = true;
                return;

            case Key.Tab:
                CycleTool();
                e.Handled = true;
                return;
        }

        if (_mode == RegionSelectorMode.Light)
        {
            return;
        }

        // Single-letter and digit tool shortcuts, straight from the upstream table.
        if (!command && e.KeySymbol is { Length: 1 } symbol
            && ShapeCatalog.FromShortcut(symbol[0]) is { } tool
            && ShapeCatalog.Get(tool).Implemented)
        {
            _viewModel.SetActive(tool);
            e.Handled = true;
            return;
        }

        NudgeSelection(e);
    }

    private void NudgeSelection(KeyEventArgs e)
    {
        double step = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? NudgeStepLarge : NudgeStep;
        bool resize = e.KeyModifiers.HasFlag(KeyModifiers.Alt);

        double dx = e.Key switch { Key.Left => -step, Key.Right => step, _ => 0 };
        double dy = e.Key switch { Key.Up => -step, Key.Down => step, _ => 0 };

        if (dx == 0 && dy == 0)
        {
            return;
        }

        if (_selection.Width < 1 || _selection.Height < 1)
        {
            _selection = new Rect(Width / 2, Height / 2, 1, 1);
        }
        else if (resize)
        {
            _selection = new Rect(_selection.X, _selection.Y,
                Math.Max(1, _selection.Width + dx), Math.Max(1, _selection.Height + dy));
        }
        else
        {
            _selection = new Rect(_selection.X + dx, _selection.Y + dy,
                _selection.Width, _selection.Height);
        }

        _selection = Clamp(_selection);
        _canvas.Selection = _selection;
        _canvas.InvalidateVisual();
        UpdateReadout(null);
        e.Handled = true;
    }

    private void CycleTool()
    {
        List<ShapeType> usable = ShapeCatalog.All
            .Where(t => t.Implemented)
            .Select(t => t.Type)
            .ToList();

        int index = usable.IndexOf(_surface.CurrentTool);
        _viewModel.SetActive(usable[(index + 1) % usable.Count]);
    }

    // ------------------------------------------------------------ shape input

    private void BeginShape(Point position)
    {
        _isDrawingShape = true;

        bool freehand = _surface.CurrentTool
            is ShapeType.DrawingFreehand or ShapeType.DrawingFreehandArrow;

        _freehandPoints = freehand
            ? new List<AnnotationPoint> { new(position.X, position.Y) }
            : null;

        _pendingShape = new AnnotationShape
        {
            Type = _surface.CurrentTool,
            Bounds = new PointRect(position.X, position.Y, 1, 1, CoordinateSpace.DisplayPixels),
            Points = _freehandPoints ?? (IReadOnlyList<AnnotationPoint>)Array.Empty<AnnotationPoint>(),
            StrokeColor = _viewModel.StrokeColor,
            StrokeWidth = _viewModel.StrokeWidth,
            EffectStrength = _viewModel.EffectStrength,
            StepNumber = _surface.NextStepNumber
        };

        _canvas.Pending = _pendingShape;
        _canvas.InvalidateVisual();
    }

    private void UpdateShape(Point position)
    {
        if (_pendingShape is not { } shape)
        {
            return;
        }

        if (_freehandPoints is not null)
        {
            _freehandPoints.Add(new AnnotationPoint(position.X, position.Y));

            double minX = _freehandPoints.Min(p => p.X);
            double minY = _freehandPoints.Min(p => p.Y);
            double maxX = _freehandPoints.Max(p => p.X);
            double maxY = _freehandPoints.Max(p => p.Y);

            _pendingShape = shape with
            {
                Points = new List<AnnotationPoint>(_freehandPoints),
                Bounds = new PointRect(minX, minY,
                    Math.Max(1, maxX - minX), Math.Max(1, maxY - minY),
                    CoordinateSpace.DisplayPixels)
            };
        }
        else
        {
            // Line and arrow keep their true direction: their bounds are the
            // drag rectangle, and the renderer draws corner-to-corner.
            Rect box = new(shape.Bounds.X, shape.Bounds.Y, 1, 1);
            Rect dragged = _surface.CurrentTool is ShapeType.DrawingLine
                or ShapeType.DrawingArrow
                ? new Rect(box.TopLeft, position)
                : Normalize(box.TopLeft, position);

            _pendingShape = shape with
            {
                Bounds = new PointRect(dragged.X, dragged.Y,
                    Math.Max(1, dragged.Width), Math.Max(1, dragged.Height),
                    CoordinateSpace.DisplayPixels)
            };
        }

        _canvas.Pending = _pendingShape;
        _canvas.InvalidateVisual();
    }

    private void FinishShape()
    {
        _isDrawingShape = false;
        _canvas.Pending = null;

        if (_pendingShape is not { } shape)
        {
            return;
        }

        _pendingShape = null;
        _freehandPoints = null;

        // Too small to be intentional: discard rather than leaving a speck.
        if (shape.Bounds.Width < 3 && shape.Bounds.Height < 3
            && shape.Points.Count < 3)
        {
            _canvas.InvalidateVisual();
            return;
        }

        _surface.Add(shape);

        if (shape.Type is ShapeType.DrawingTextOutline
            or ShapeType.DrawingTextBackground or ShapeType.DrawingSpeechBalloon)
        {
            ShowTextEntry(shape);
        }

        _canvas.InvalidateVisual();
    }

    // --------------------------------------------------------------- text entry

    private void ShowTextEntry(AnnotationShape shape)
    {
        TextEntry.Text = "";
        TextEntry.Margin = new Thickness(shape.Bounds.X, shape.Bounds.Y, 0, 0);
        TextEntry.Width = Math.Max(180, shape.Bounds.Width);
        TextEntry.IsVisible = true;
        Dispatcher.UIThread.Post(() => TextEntry.Focus());
    }

    private void OnTextEntryKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                CommitPendingText();
                e.Handled = true;
                return;

            case Key.Escape:
                // Cancels the TEXT, not the capture: upstream keeps these
                // separate, and losing a whole selection to a stray Esc while
                // typing would be worse than useless.
                TextEntry.IsVisible = false;
                _surface.RemoveSelected();
                _canvas.InvalidateVisual();
                Focus();
                e.Handled = true;
                return;
        }
    }

    private void CommitPendingText()
    {
        if (!TextEntry.IsVisible)
        {
            return;
        }

        string text = TextEntry.Text ?? "";
        TextEntry.IsVisible = false;

        if (_surface.Selected is { } selected && _surface.SelectedIndex >= 0)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                // An empty text shape would be an invisible artifact.
                _surface.RemoveSelected();
            }
            else
            {
                _surface.Replace(_surface.SelectedIndex, selected with { Text = text });
            }
        }

        _canvas.InvalidateVisual();
        Focus();
    }

    // ------------------------------------------------------------------ layout

    private void UpdateReadout(Point? pointer)
    {
        bool hasSelection = _selection.Width >= 1 && _selection.Height >= 1;
        PointRect global = ToGlobalPoints(hasSelection
            ? _selection
            : new Rect(pointer ?? default, new Size(0, 0)));

        ReadoutText.Text = hasSelection
            ? $"{_selection.Width:0} × {_selection.Height:0} pt   at ({global.X:0}, {global.Y:0})"
            : $"({global.X:0}, {global.Y:0})";

        double x = hasSelection ? _selection.X : pointer?.X ?? 12;
        double y = hasSelection ? _selection.Y - 30 : (pointer?.Y ?? 12) + 18;
        if (y < 4)
        {
            y = _selection.Bottom + 8;
        }

        Readout.Margin = new Thickness(
            Math.Clamp(x, 4, Math.Max(4, Width - 240)),
            Math.Clamp(y, 4, Math.Max(4, Height - 40)), 0, 0);
    }

    /// <summary>
    /// Keeps the toolbar on screen and off the selection, so it never hides the
    /// area the user is working on.
    /// </summary>
    private void PositionToolbar()
    {
        if (!Toolbar.IsVisible)
        {
            return;
        }

        double toolbarHeight = Toolbar.Bounds.Height > 0 ? Toolbar.Bounds.Height : 110;
        double toolbarWidth = Toolbar.Bounds.Width > 0 ? Toolbar.Bounds.Width : 640;

        double x = Math.Clamp(_selection.X, 8, Math.Max(8, Width - toolbarWidth - 8));
        double below = _selection.Bottom + 12;
        double above = _selection.Y - toolbarHeight - 12;

        double y = below + toolbarHeight <= Height ? below
            : above >= 0 ? above
            : Math.Max(8, Height - toolbarHeight - 8);

        if (_selection.Width < 1)
        {
            x = Math.Max(8, (Width - toolbarWidth) / 2);
            y = Math.Max(8, Height - toolbarHeight - 24);
        }

        Toolbar.Margin = new Thickness(x, y, 0, 0);
    }

    // ------------------------------------------------------------------ result

    private static Rect Normalize(Point a, Point b) =>
        new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    private Rect Clamp(Rect rect)
    {
        double x = Math.Clamp(rect.X, 0, Math.Max(0, Width - 1));
        double y = Math.Clamp(rect.Y, 0, Math.Max(0, Height - 1));
        return new Rect(x, y,
            Math.Clamp(rect.Width, 1, Width - x),
            Math.Clamp(rect.Height, 1, Height - y));
    }

    /// <summary>
    /// Window DIPs → CoreGraphics global points. Exact on macOS: one DIP is one
    /// point and the window covers exactly the supplied display.
    /// </summary>
    private PointRect ToGlobalPoints(Rect selection) => new(
        _displayBoundsGlobalPoints.X + selection.X,
        _displayBoundsGlobalPoints.Y + selection.Y,
        selection.Width, selection.Height,
        CoordinateSpace.CgGlobalPoints);

    private void Confirm()
    {
        CommitPendingText();

        if (_selection.Width < 1 || _selection.Height < 1)
        {
            Complete(null);
            return;
        }

        Complete(ToGlobalPoints(_selection));
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        // Closing any other way is a cancellation, never a success.
        Complete(null);
    }

    private void Complete(PointRect? selection)
    {
        if (_completed)
        {
            return;
        }

        _completed = true;
        Selection = selection;
        _result.TrySetResult(selection);

        Dispatcher.UIThread.Post(Close);
    }
}
