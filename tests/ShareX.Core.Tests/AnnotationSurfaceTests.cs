using ShareX.Core.Annotation;
using ShareX.Core.Geometry;
using Xunit;

namespace ShareX.Core.Tests;

public sealed class ShapeCatalogTests
{
    [Fact]
    public void Catalog_HasExactlyTheTwentySixUpstreamTools()
    {
        // PROJECT-SPEC.md pins 26 region shape/tool entries.
        Assert.Equal(26, ShapeCatalog.All.Count);
        Assert.Equal(26, Enum.GetValues<ShapeType>().Length);
    }

    [Fact]
    public void CatalogOrder_MatchesTheEnumDeclarationOrder()
    {
        // The order is the toolbar order and part of the contract.
        ShapeType[] enumOrder = Enum.GetValues<ShapeType>();
        ShapeType[] catalogOrder = ShapeCatalog.All.Select(t => t.Type).ToArray();
        Assert.Equal(enumOrder, catalogOrder);
    }

    [Theory]
    // Transcribed from ShapeManager.OnKeyDown in the pinned source.
    [InlineData('M', ShapeType.ToolSelect)]
    [InlineData('R', ShapeType.DrawingRectangle)]
    [InlineData('1', ShapeType.DrawingRectangle)]
    [InlineData('E', ShapeType.DrawingEllipse)]
    [InlineData('F', ShapeType.DrawingFreehand)]
    [InlineData('L', ShapeType.DrawingLine)]
    [InlineData('A', ShapeType.DrawingArrow)]
    [InlineData('O', ShapeType.DrawingTextOutline)]
    [InlineData('T', ShapeType.DrawingTextBackground)]
    [InlineData('S', ShapeType.DrawingSpeechBalloon)]
    [InlineData('I', ShapeType.DrawingStep)]
    [InlineData('B', ShapeType.EffectBlur)]
    [InlineData('P', ShapeType.EffectPixelate)]
    [InlineData('H', ShapeType.EffectHighlight)]
    [InlineData('C', ShapeType.ToolCrop)]
    [InlineData('X', ShapeType.ToolCutOut)]
    [InlineData('0', ShapeType.RegionRectangle)]
    public void Shortcuts_MatchUpstream(char key, ShapeType expected)
        => Assert.Equal(expected, ShapeCatalog.FromShortcut(key));

    [Fact]
    public void UnknownKey_IsNotATool() => Assert.Null(ShapeCatalog.FromShortcut('Q'));

    [Fact]
    public void CropAndCutOut_AreEditorModeOnly()
    {
        // Upstream gates these behind IsEditorMode, not annotation mode.
        Assert.Equal(ShapeAvailability.Editor, ShapeCatalog.Get(ShapeType.ToolCrop).Availability);
        Assert.Equal(ShapeAvailability.Editor, ShapeCatalog.Get(ShapeType.ToolCutOut).Availability);
    }
}

public sealed class AnnotationSurfaceTests
{
    private static AnnotationShape Rect(double x, double y, double w = 10, double h = 10,
        ShapeType type = ShapeType.DrawingRectangle) => new()
    {
        Type = type,
        Bounds = new PointRect(x, y, w, h, CoordinateSpace.DisplayPixels)
    };

    [Fact]
    public void AddSelectsTheNewShape()
    {
        var surface = new AnnotationSurface();
        surface.Add(Rect(0, 0));
        Assert.Single(surface.Shapes);
        Assert.Equal(0, surface.SelectedIndex);
    }

    [Fact]
    public void Undo_RestoresThePreviousState()
    {
        var surface = new AnnotationSurface();
        surface.Add(Rect(0, 0));
        surface.Add(Rect(20, 20));
        Assert.Equal(2, surface.Shapes.Count);

        surface.Undo();
        Assert.Single(surface.Shapes);

        surface.Undo();
        Assert.Empty(surface.Shapes);
    }

    [Fact]
    public void Redo_ReappliesAnUndoneChange()
    {
        var surface = new AnnotationSurface();
        surface.Add(Rect(0, 0));
        surface.Undo();
        Assert.Empty(surface.Shapes);

        surface.Redo();
        Assert.Single(surface.Shapes);
    }

    [Fact]
    public void NewEdit_DiscardsTheRedoBranch()
    {
        var surface = new AnnotationSurface();
        surface.Add(Rect(0, 0));
        surface.Undo();
        Assert.True(surface.CanRedo);

        surface.Add(Rect(50, 50));
        Assert.False(surface.CanRedo);
    }

    [Fact]
    public void HitTest_ReturnsTheTopmostShape()
    {
        // Later shapes draw on top, so hit testing must prefer them.
        var surface = new AnnotationSurface();
        surface.Add(Rect(0, 0, 100, 100));
        surface.Add(Rect(0, 0, 100, 100));

        Assert.Equal(1, surface.HitTest(new AnnotationPoint(50, 50)));
    }

    [Fact]
    public void HitTest_MissReturnsMinusOne()
    {
        var surface = new AnnotationSurface();
        surface.Add(Rect(0, 0, 10, 10));
        Assert.Equal(-1, surface.HitTest(new AnnotationPoint(500, 500)));
    }

    [Fact]
    public void BringToFrontAndSendToBack_ReorderTheSelection()
    {
        var surface = new AnnotationSurface();
        surface.Add(Rect(0, 0));
        surface.Add(Rect(10, 10));
        surface.Add(Rect(20, 20));

        surface.Select(0);
        surface.BringToFront();
        Assert.Equal(2, surface.SelectedIndex);
        Assert.Equal(0, surface.Shapes[2].Bounds.X);

        surface.SendToBack();
        Assert.Equal(0, surface.SelectedIndex);
        Assert.Equal(0, surface.Shapes[0].Bounds.X);
    }

    [Fact]
    public void ZOrderChange_IsUndoable()
    {
        var surface = new AnnotationSurface();
        surface.Add(Rect(0, 0));
        surface.Add(Rect(10, 10));
        surface.Select(0);
        surface.BringToFront();

        surface.Undo();
        Assert.Equal(0, surface.Shapes[0].Bounds.X);
    }

    [Fact]
    public void DuplicateSelected_OffsetsTheCopy()
    {
        var surface = new AnnotationSurface();
        surface.Add(Rect(0, 0));
        surface.DuplicateSelected(offset: 10);

        Assert.Equal(2, surface.Shapes.Count);
        Assert.Equal(10, surface.Shapes[1].Bounds.X);
        Assert.Equal(10, surface.Shapes[1].Bounds.Y);
    }

    [Fact]
    public void StepNumbers_StartAtOneAndIncrement()
    {
        // Upstream numbers step counters from 1.
        var surface = new AnnotationSurface();
        Assert.Equal(1, surface.NextStepNumber);

        surface.Add(Rect(0, 0, type: ShapeType.DrawingStep));
        Assert.Equal(2, surface.NextStepNumber);

        // Non-step shapes must not advance the counter.
        surface.Add(Rect(0, 0));
        Assert.Equal(2, surface.NextStepNumber);
    }

    [Fact]
    public void MovedBy_TranslatesBoundsAndFreehandPoints()
    {
        var shape = Rect(0, 0, 10, 10, ShapeType.DrawingFreehand) with
        {
            Points = new[] { new AnnotationPoint(1, 2), new AnnotationPoint(3, 4) }
        };

        AnnotationShape moved = shape.MovedBy(5, 7);

        Assert.Equal(5, moved.Bounds.X);
        Assert.Equal(7, moved.Bounds.Y);
        Assert.Equal(new AnnotationPoint(6, 9), moved.Points[0]);
        Assert.Equal(new AnnotationPoint(8, 11), moved.Points[1]);
    }

    [Fact]
    public void EffectShapes_AreClassifiedAsEffects()
    {
        Assert.True(Rect(0, 0, type: ShapeType.EffectBlur).IsEffect);
        Assert.True(Rect(0, 0, type: ShapeType.EffectPixelate).IsEffect);
        Assert.True(Rect(0, 0, type: ShapeType.EffectHighlight).IsEffect);
        Assert.False(Rect(0, 0, type: ShapeType.DrawingRectangle).IsEffect);
    }
}
