using ShareX.Core.Geometry;

namespace ShareX.Core.Annotation;

/// <summary>A point in the annotation surface's own pixel space.</summary>
public readonly record struct AnnotationPoint(double X, double Y);

/// <summary>
/// One placed annotation. Deliberately a plain data record with no rendering
/// dependency, so the whole shape model - hit testing, z-order, undo/redo - is
/// exercised headlessly in tests (docs/INTERFACES-AND-DATA.md keeps domain types
/// independent of the UI toolkit).
/// </summary>
public sealed record AnnotationShape
{
    public required ShapeType Type { get; init; }

    /// <summary>Bounding rectangle in surface pixels. Always normalised.</summary>
    public required PointRect Bounds { get; init; }

    /// <summary>Freehand path points; empty for non-freehand shapes.</summary>
    public IReadOnlyList<AnnotationPoint> Points { get; init; } = Array.Empty<AnnotationPoint>();

    public string Text { get; init; } = "";

    /// <summary>Step counter value; upstream numbers these from 1 upward.</summary>
    public int StepNumber { get; init; }

    public uint StrokeColor { get; init; } = 0xFFE74C3C;
    public uint FillColor { get; init; } = 0x00000000;
    public double StrokeWidth { get; init; } = 2;
    public int CornerRadius { get; init; }

    /// <summary>Blur/pixelate strength; upstream keeps these per-shape.</summary>
    public int EffectStrength { get; init; } = 15;

    public bool IsEffect => Type is ShapeType.EffectBlur
        or ShapeType.EffectPixelate or ShapeType.EffectHighlight;

    public bool IsRegion => Type is ShapeType.RegionRectangle
        or ShapeType.RegionEllipse or ShapeType.RegionFreehand;

    public bool HitTest(AnnotationPoint point) =>
        point.X >= Bounds.X && point.X <= Bounds.X + Bounds.Width &&
        point.Y >= Bounds.Y && point.Y <= Bounds.Y + Bounds.Height;

    public AnnotationShape MovedBy(double dx, double dy) => this with
    {
        Bounds = Bounds with { X = Bounds.X + dx, Y = Bounds.Y + dy },
        Points = Points.Count == 0
            ? Points
            : Points.Select(p => new AnnotationPoint(p.X + dx, p.Y + dy)).ToList()
    };
}

/// <summary>
/// The shape list plus undo/redo, mirroring upstream's ShapeManager + history.
///
/// Every mutation goes through <see cref="Apply"/>, which snapshots the previous
/// state. That keeps undo correct for compound operations (move, restyle,
/// z-order) without each call site having to remember to record history - the
/// class of bug that makes an editor feel unreliable.
/// </summary>
public sealed class AnnotationSurface
{
    private readonly List<AnnotationShape> _shapes = new();
    private readonly Stack<List<AnnotationShape>> _undo = new();
    private readonly Stack<List<AnnotationShape>> _redo = new();

    /// <summary>Upstream caps history; an unbounded stack would grow without limit.</summary>
    private const int MaxHistory = 100;

    public IReadOnlyList<AnnotationShape> Shapes => _shapes;

    /// <summary>Index into <see cref="Shapes"/>, or -1 when nothing is selected.</summary>
    public int SelectedIndex { get; private set; } = -1;

    public AnnotationShape? Selected =>
        SelectedIndex >= 0 && SelectedIndex < _shapes.Count ? _shapes[SelectedIndex] : null;

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public ShapeType CurrentTool { get; set; } = ShapeType.RegionRectangle;

    /// <summary>Next step-counter value. Upstream starts at 1 and increments per placement.</summary>
    public int NextStepNumber => _shapes.Count(s => s.Type == ShapeType.DrawingStep) + 1;

    private void Apply(Action mutate)
    {
        _undo.Push(new List<AnnotationShape>(_shapes));
        if (_undo.Count > MaxHistory)
        {
            // Drop the oldest entry. Stack has no such operation, so rebuild.
            var kept = _undo.ToArray().Take(MaxHistory).Reverse().ToList();
            _undo.Clear();
            foreach (List<AnnotationShape> entry in kept)
            {
                _undo.Push(entry);
            }
        }

        // Any new edit invalidates the redo branch, as in every editor.
        _redo.Clear();
        mutate();
    }

    public void Add(AnnotationShape shape)
    {
        Apply(() =>
        {
            _shapes.Add(shape);
            SelectedIndex = _shapes.Count - 1;
        });
    }

    public void RemoveAt(int index)
    {
        if (index < 0 || index >= _shapes.Count)
        {
            return;
        }

        Apply(() =>
        {
            _shapes.RemoveAt(index);
            SelectedIndex = -1;
        });
    }

    public void RemoveSelected()
    {
        if (SelectedIndex >= 0)
        {
            RemoveAt(SelectedIndex);
        }
    }

    public void Replace(int index, AnnotationShape shape)
    {
        if (index < 0 || index >= _shapes.Count)
        {
            return;
        }

        Apply(() => _shapes[index] = shape);
    }

    public void Clear()
    {
        if (_shapes.Count == 0)
        {
            return;
        }

        Apply(() =>
        {
            _shapes.Clear();
            SelectedIndex = -1;
        });
    }

    /// <summary>
    /// Topmost shape at the point, matching what the user sees: later shapes are
    /// drawn on top, so hit testing walks the list backwards.
    /// </summary>
    public int HitTest(AnnotationPoint point)
    {
        for (int i = _shapes.Count - 1; i >= 0; i--)
        {
            if (_shapes[i].HitTest(point))
            {
                return i;
            }
        }

        return -1;
    }

    public void Select(int index) =>
        SelectedIndex = index >= -1 && index < _shapes.Count ? index : -1;

    // ---- z-order (upstream Home / End / PageUp / PageDown) ------------------

    public void BringToFront() => MoveSelected(_shapes.Count - 1);

    public void SendToBack() => MoveSelected(0);

    public void MoveUp() => MoveSelected(SelectedIndex + 1);

    public void MoveDown() => MoveSelected(SelectedIndex - 1);

    private void MoveSelected(int target)
    {
        if (SelectedIndex < 0 || target < 0 || target >= _shapes.Count || target == SelectedIndex)
        {
            return;
        }

        int from = SelectedIndex;
        Apply(() =>
        {
            AnnotationShape shape = _shapes[from];
            _shapes.RemoveAt(from);
            _shapes.Insert(target, shape);
            SelectedIndex = target;
        });
    }

    /// <summary>Upstream Ctrl+D: duplicate, offset slightly so it is visible.</summary>
    public void DuplicateSelected(double offset = 10)
    {
        if (Selected is not { } shape)
        {
            return;
        }

        Add(shape.MovedBy(offset, offset));
    }

    // ---- history -------------------------------------------------------------

    public void Undo()
    {
        if (_undo.Count == 0)
        {
            return;
        }

        _redo.Push(new List<AnnotationShape>(_shapes));
        List<AnnotationShape> previous = _undo.Pop();
        _shapes.Clear();
        _shapes.AddRange(previous);
        SelectedIndex = Math.Min(SelectedIndex, _shapes.Count - 1);
    }

    public void Redo()
    {
        if (_redo.Count == 0)
        {
            return;
        }

        _undo.Push(new List<AnnotationShape>(_shapes));
        List<AnnotationShape> next = _redo.Pop();
        _shapes.Clear();
        _shapes.AddRange(next);
        SelectedIndex = Math.Min(SelectedIndex, _shapes.Count - 1);
    }
}
