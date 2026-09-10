using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShareX.Core.Annotation;

namespace ShareX.Mac.App.ViewModels;

/// <summary>One toolbar entry. Separators are entries too, so the toolbar's
/// grouping stays data-driven rather than hard-coded in XAML.</summary>
public sealed partial class ShapeToolViewModel : ObservableObject
{
    public ShapeToolViewModel(ShapeToolInfo info, string glyph)
    {
        Info = info;
        Glyph = glyph;
    }

    private ShapeToolViewModel()
    {
        Info = null!;
        Glyph = "";
        IsSeparator = true;
    }

    public static ShapeToolViewModel Separator() => new();

    public ShapeToolInfo Info { get; }
    public string Glyph { get; }
    public bool IsSeparator { get; }

    public ShapeType Type => Info.Type;
    public bool IsEnabled => !IsSeparator && Info.Implemented;

    public string Tooltip => IsSeparator
        ? ""
        : Info.Implemented
            ? string.IsNullOrEmpty(Info.Shortcut) ? Info.Label : $"{Info.Label} ({Info.Shortcut})"
            : $"{Info.Label} — {Info.Note}";

    [ObservableProperty] private bool _isActive;
}

public sealed class PaletteEntryViewModel
{
    public PaletteEntryViewModel(string name, uint argb)
    {
        Name = name;
        Argb = argb;
        Brush = new SolidColorBrush(Color.FromArgb(
            (byte)((argb >> 24) & 0xFF), (byte)((argb >> 16) & 0xFF),
            (byte)((argb >> 8) & 0xFF), (byte)(argb & 0xFF)));
    }

    public string Name { get; }
    public uint Argb { get; }
    public IBrush Brush { get; }
}

/// <summary>
/// Toolbar state for the region selector. Owns the tool list, the current style
/// and the undo/redo commands; the window owns input and rendering.
/// </summary>
public sealed partial class RegionSelectorViewModel : ObservableObject
{
    private readonly AnnotationSurface _surface;

    public RegionSelectorViewModel(AnnotationSurface surface)
    {
        _surface = surface;

        // Grouped the way upstream's toolbar groups them: region tools, the
        // select tool, drawing tools, effect tools, then editor tools.
        var glyphs = new Dictionary<ShapeType, string>
        {
            [ShapeType.RegionRectangle] = "▭",
            [ShapeType.RegionEllipse] = "◯",
            [ShapeType.RegionFreehand] = "✎",
            [ShapeType.ToolSelect] = "➤",
            [ShapeType.DrawingRectangle] = "▢",
            [ShapeType.DrawingEllipse] = "○",
            [ShapeType.DrawingFreehand] = "〰",
            [ShapeType.DrawingFreehandArrow] = "↝",
            [ShapeType.DrawingLine] = "╱",
            [ShapeType.DrawingArrow] = "↗",
            [ShapeType.DrawingTextOutline] = "A",
            [ShapeType.DrawingTextBackground] = "🅰",
            [ShapeType.DrawingSpeechBalloon] = "💬",
            [ShapeType.DrawingStep] = "①",
            [ShapeType.DrawingMagnify] = "🔍",
            [ShapeType.DrawingImage] = "🖼",
            [ShapeType.DrawingImageScreen] = "🖥",
            [ShapeType.DrawingSticker] = "😀",
            [ShapeType.DrawingCursor] = "🖱",
            [ShapeType.DrawingSmartEraser] = "🧽",
            [ShapeType.EffectBlur] = "▨",
            [ShapeType.EffectPixelate] = "▦",
            [ShapeType.EffectHighlight] = "🖍",
            [ShapeType.ToolSpotlight] = "🔆",
            [ShapeType.ToolCrop] = "⬚",
            [ShapeType.ToolCutOut] = "✂"
        };

        ShapeType[] separatorsBefore =
        {
            ShapeType.ToolSelect,
            ShapeType.DrawingRectangle,
            ShapeType.EffectBlur,
            ShapeType.ToolCrop
        };

        foreach (ShapeToolInfo info in ShapeCatalog.All)
        {
            if (separatorsBefore.Contains(info.Type))
            {
                Tools.Add(ShapeToolViewModel.Separator());
            }

            Tools.Add(new ShapeToolViewModel(info, glyphs[info.Type]));
        }

        SetActive(ShapeType.RegionRectangle);
    }

    public ObservableCollection<ShapeToolViewModel> Tools { get; } = new();

    /// <summary>Upstream's annotation palette colours.</summary>
    public ObservableCollection<PaletteEntryViewModel> Palette { get; } = new()
    {
        new("Red", 0xFFE74C3C),
        new("Orange", 0xFFF39C12),
        new("Yellow", 0xFFF1C40F),
        new("Green", 0xFF2ECC71),
        new("Blue", 0xFF3498DB),
        new("Purple", 0xFF9B59B6),
        new("Black", 0xFF000000),
        new("White", 0xFFFFFFFF)
    };

    [ObservableProperty] private uint _strokeColor = 0xFFE74C3C;
    [ObservableProperty] private int _strokeWidth = 3;
    [ObservableProperty] private int _effectStrength = 15;
    [ObservableProperty] private string _hintText =
        "Drag to select · pick a tool to annotate · Enter confirms · Esc cancels";

    /// <summary>Raised when the surface changed and the canvas must repaint.</summary>
    public event Action? SurfaceChanged;

    public void NotifyChanged() => SurfaceChanged?.Invoke();

    [RelayCommand]
    private void SelectTool(ShapeToolViewModel? tool)
    {
        if (tool is null || tool.IsSeparator || !tool.IsEnabled)
        {
            return;
        }

        SetActive(tool.Type);
    }

    public void SetActive(ShapeType type)
    {
        _surface.CurrentTool = type;

        foreach (ShapeToolViewModel tool in Tools)
        {
            tool.IsActive = !tool.IsSeparator && tool.Type == type;
        }

        ShapeToolInfo info = ShapeCatalog.Get(type);
        HintText = info.Type switch
        {
            ShapeType.ToolSelect =>
                "Click a shape to select it, drag to move, Delete removes it",
            ShapeType.DrawingTextOutline or ShapeType.DrawingTextBackground
                or ShapeType.DrawingSpeechBalloon =>
                $"{info.Label}: drag a box, type, Enter commits",
            _ when info.Type.ToString().StartsWith("Effect") =>
                $"{info.Label}: drag over the area to obscure it",
            _ => $"{info.Label}: drag to draw · Enter confirms the capture · Esc cancels"
        };

        NotifyChanged();
    }

    [RelayCommand]
    private void SelectColor(PaletteEntryViewModel? entry)
    {
        if (entry is null)
        {
            return;
        }

        StrokeColor = entry.Argb;

        // Restyling a selected shape is an edit, so it goes through Replace and
        // is undoable like any other change.
        if (_surface.Selected is { } selected && _surface.SelectedIndex >= 0)
        {
            _surface.Replace(_surface.SelectedIndex, selected with { StrokeColor = entry.Argb });
            NotifyChanged();
        }
    }

    partial void OnStrokeWidthChanged(int value)
    {
        if (_surface.Selected is { } selected && _surface.SelectedIndex >= 0)
        {
            _surface.Replace(_surface.SelectedIndex, selected with { StrokeWidth = value });
            NotifyChanged();
        }
    }

    partial void OnEffectStrengthChanged(int value)
    {
        if (_surface.Selected is { IsEffect: true } selected && _surface.SelectedIndex >= 0)
        {
            _surface.Replace(_surface.SelectedIndex, selected with { EffectStrength = value });
            NotifyChanged();
        }
    }

    [RelayCommand]
    private void Undo()
    {
        _surface.Undo();
        NotifyChanged();
    }

    [RelayCommand]
    private void Redo()
    {
        _surface.Redo();
        NotifyChanged();
    }

    [RelayCommand]
    private void Clear()
    {
        _surface.Clear();
        NotifyChanged();
    }
}
