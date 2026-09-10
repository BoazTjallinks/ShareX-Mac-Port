using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using ShareX.Core.Geometry;

namespace ShareX.Mac.App.Views;

/// <summary>
/// Which upstream region command opened the selector. Upstream keeps these as
/// genuinely different interactions (PROJECT-SPEC.md section 5: "Maintain distinct
/// default/light/transparent interactions from the release source"), so the mode
/// is carried through rather than collapsed into one overlay.
/// </summary>
public enum RegionSelectorMode
{
    /// <summary>RectangleRegion — dimmed overlay, guides, readout.</summary>
    Default,

    /// <summary>RectangleLight — reduced interaction: no dimming, no guides.</summary>
    Light,

    /// <summary>
    /// RectangleTransparent — no dimming at all, so the live desktop stays fully
    /// visible while selecting. Note this is the *interaction* mode; it is not a
    /// claim that content behind another window can be reconstructed.
    /// </summary>
    Transparent
}

/// <summary>
/// Interactive region selection. Returns the chosen rectangle in
/// <see cref="CoordinateSpace.CgGlobalPoints"/>, or null when cancelled.
///
/// Coordinate handling: on macOS an Avalonia device-independent pixel is one
/// AppKit point, and this window is positioned to cover exactly one display whose
/// origin the caller supplies in CoreGraphics global points. So a selection
/// expressed in window DIPs plus that origin is already in global points, with no
/// scale arithmetic and no chance of mixing spaces (PROJECT-SPEC.md section 5).
/// </summary>
public partial class RegionSelectorWindow : Window
{
    private readonly TaskCompletionSource<PointRect?> _result =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly PointRect _displayBoundsGlobalPoints;
    private readonly RegionSelectorMode _mode;

    private Point? _dragStart;
    private Rect _selection;
    private bool _completed;

    // Keyboard nudging, as upstream supports.
    private const double NudgeStep = 1;
    private const double NudgeStepLarge = 10;

    public RegionSelectorWindow()
        : this(new PointRect(0, 0, 1, 1, CoordinateSpace.CgGlobalPoints), RegionSelectorMode.Default)
    {
    }

    public RegionSelectorWindow(PointRect displayBoundsGlobalPoints, RegionSelectorMode mode)
    {
        InitializeComponent();

        _displayBoundsGlobalPoints = displayBoundsGlobalPoints;
        _mode = mode;

        Width = displayBoundsGlobalPoints.Width;
        Height = displayBoundsGlobalPoints.Height;

        if (mode != RegionSelectorMode.Default)
        {
            // Light and Transparent do not dim the desktop.
            DimTop.IsVisible = false;
            DimBottom.IsVisible = false;
            DimLeft.IsVisible = false;
            DimRight.IsVisible = false;
        }

        if (mode == RegionSelectorMode.Light)
        {
            GuideH.IsVisible = false;
            GuideV.IsVisible = false;
        }

        ReadoutHint.Text = mode switch
        {
            RegionSelectorMode.Light => "Drag to select · Esc cancels",
            RegionSelectorMode.Transparent => "Transparent mode · drag to select · Esc cancels",
            _ => "Drag to select · arrows nudge · Enter confirms · Esc cancels"
        };

        Opened += OnOpened;
    }

    public Task<PointRect?> SelectionTask => _result.Task;

    private void OnOpened(object? sender, EventArgs e)
    {
        Activate();
        Focus();
        LayoutOverlay();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        PointerPoint point = e.GetCurrentPoint(this);

        // Upstream treats the right button as cancel during selection.
        if (point.Properties.IsRightButtonPressed)
        {
            Complete(null);
            return;
        }

        _dragStart = point.Position;
        _selection = new Rect(point.Position, new Size(0, 0));
        SelectionBorder.IsVisible = true;
        LayoutOverlay();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        Point position = e.GetPosition(this);

        if (_dragStart is { } start)
        {
            _selection = Normalize(start, position);
        }
        else
        {
            // Guides track the pointer until a drag begins.
            GuideH.SetValue(Canvas.TopProperty, position.Y);
            GuideV.SetValue(Canvas.LeftProperty, position.X);
        }

        LayoutOverlay(position);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_dragStart is null)
        {
            return;
        }

        _dragStart = null;
        _selection = Normalize(_selection.TopLeft, _selection.BottomRight);

        // A click without a drag is a cancel, not a zero-pixel capture. Upstream
        // never produces an empty artifact from an accidental click.
        if (_selection.Width < 1 || _selection.Height < 1)
        {
            Complete(null);
            return;
        }

        Complete(ToGlobalPoints(_selection));
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        switch (e.Key)
        {
            case Key.Escape:
                Complete(null);
                e.Handled = true;
                return;

            case Key.Enter:
                if (_selection.Width >= 1 && _selection.Height >= 1)
                {
                    Complete(ToGlobalPoints(_selection));
                }
                else
                {
                    Complete(null);
                }

                e.Handled = true;
                return;
        }

        if (_mode == RegionSelectorMode.Light)
        {
            return;
        }

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
            // Nothing selected yet: start a 1x1 selection at the centre so the
            // keyboard alone can drive a selection.
            _selection = new Rect(Width / 2, Height / 2, 1, 1);
            SelectionBorder.IsVisible = true;
        }
        else if (resize)
        {
            _selection = new Rect(
                _selection.X,
                _selection.Y,
                Math.Max(1, _selection.Width + dx),
                Math.Max(1, _selection.Height + dy));
        }
        else
        {
            _selection = new Rect(_selection.X + dx, _selection.Y + dy, _selection.Width, _selection.Height);
        }

        _selection = Clamp(_selection);
        LayoutOverlay();
        e.Handled = true;
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        // Closing the overlay any other way is a cancellation, never a success.
        Complete(null);
    }

    // ------------------------------------------------------------------ helpers

    private static Rect Normalize(Point a, Point b) =>
        new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    private Rect Clamp(Rect rect)
    {
        double x = Math.Clamp(rect.X, 0, Math.Max(0, Width - 1));
        double y = Math.Clamp(rect.Y, 0, Math.Max(0, Height - 1));
        double w = Math.Clamp(rect.Width, 1, Width - x);
        double h = Math.Clamp(rect.Height, 1, Height - y);
        return new Rect(x, y, w, h);
    }

    /// <summary>
    /// Window DIPs → CoreGraphics global points. Exact on macOS because one DIP is
    /// one point and the window covers exactly the supplied display.
    /// </summary>
    private PointRect ToGlobalPoints(Rect selection) => new(
        _displayBoundsGlobalPoints.X + selection.X,
        _displayBoundsGlobalPoints.Y + selection.Y,
        selection.Width,
        selection.Height,
        CoordinateSpace.CgGlobalPoints);

    private void LayoutOverlay(Point? pointer = null)
    {
        bool hasSelection = _selection.Width >= 1 && _selection.Height >= 1;

        if (hasSelection)
        {
            SelectionBorder.IsVisible = true;
            SelectionBorder.Width = _selection.Width;
            SelectionBorder.Height = _selection.Height;
            SelectionBorder.SetValue(Canvas.LeftProperty, _selection.X);
            SelectionBorder.SetValue(Canvas.TopProperty, _selection.Y);

            GuideH.IsVisible = false;
            GuideV.IsVisible = false;
        }
        else if (_mode == RegionSelectorMode.Default)
        {
            GuideH.IsVisible = true;
            GuideV.IsVisible = true;
            GuideH.Width = Width;
            GuideV.Height = Height;
        }

        if (_mode == RegionSelectorMode.Default)
        {
            LayoutDimming(hasSelection);
        }

        // Readout follows the selection, or the pointer before a drag.
        double readoutX = hasSelection ? _selection.X : pointer?.X ?? 12;
        double readoutY = hasSelection ? _selection.Y - 42 : (pointer?.Y ?? 12) + 16;
        if (readoutY < 4)
        {
            readoutY = _selection.Bottom + 8;
        }

        Readout.SetValue(Canvas.LeftProperty, Math.Clamp(readoutX, 4, Math.Max(4, Width - 220)));
        Readout.SetValue(Canvas.TopProperty, Math.Clamp(readoutY, 4, Math.Max(4, Height - 48)));

        PointRect global = ToGlobalPoints(hasSelection ? _selection : new Rect(pointer ?? default, new Size(0, 0)));
        ReadoutSize.Text = hasSelection
            ? $"{_selection.Width:0} × {_selection.Height:0} pt   at ({global.X:0}, {global.Y:0})"
            : $"({global.X:0}, {global.Y:0})";
    }

    private void LayoutDimming(bool hasSelection)
    {
        if (!hasSelection)
        {
            DimTop.SetValue(Canvas.LeftProperty, 0d);
            DimTop.SetValue(Canvas.TopProperty, 0d);
            DimTop.Width = Width;
            DimTop.Height = Height;
            DimBottom.Height = 0;
            DimLeft.Height = 0;
            DimRight.Height = 0;
            return;
        }

        DimTop.SetValue(Canvas.LeftProperty, 0d);
        DimTop.SetValue(Canvas.TopProperty, 0d);
        DimTop.Width = Width;
        DimTop.Height = Math.Max(0, _selection.Y);

        DimBottom.SetValue(Canvas.LeftProperty, 0d);
        DimBottom.SetValue(Canvas.TopProperty, _selection.Bottom);
        DimBottom.Width = Width;
        DimBottom.Height = Math.Max(0, Height - _selection.Bottom);

        DimLeft.SetValue(Canvas.LeftProperty, 0d);
        DimLeft.SetValue(Canvas.TopProperty, _selection.Y);
        DimLeft.Width = Math.Max(0, _selection.X);
        DimLeft.Height = _selection.Height;

        DimRight.SetValue(Canvas.LeftProperty, _selection.Right);
        DimRight.SetValue(Canvas.TopProperty, _selection.Y);
        DimRight.Width = Math.Max(0, Width - _selection.Right);
        DimRight.Height = _selection.Height;
    }

    private void Complete(PointRect? selection)
    {
        if (_completed)
        {
            return;
        }

        _completed = true;
        _result.TrySetResult(selection);

        // Let the overlay disappear before the caller captures, so it cannot be
        // in the frame even if the native filter were bypassed.
        Dispatcher.UIThread.Post(Close);
    }
}
