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

// Ported from ShareX v21.0.0 (commit d2502561f63fc3ff502cacd91514e3f7f2948c74),
// file ShareX.ScreenCaptureLib/Enums.cs.

namespace ShareX.Core.Annotation;

/// <summary>
/// The 26 region capture shape/tool entries. Member names and declaration ORDER
/// are the serialized contract and the toolbar order; neither may change.
/// See planning/region-annotation-tools.md.
/// </summary>
public enum ShapeType // Localized
{
    RegionRectangle,
    RegionEllipse,
    RegionFreehand,
    ToolSelect,
    DrawingRectangle,
    DrawingEllipse,
    DrawingFreehand,
    DrawingFreehandArrow,
    DrawingLine,
    DrawingArrow,
    DrawingTextOutline,
    DrawingTextBackground,
    DrawingSpeechBalloon,
    DrawingStep,
    DrawingMagnify,
    DrawingImage,
    DrawingImageScreen,
    DrawingSticker,
    DrawingCursor,
    DrawingSmartEraser,
    EffectBlur,
    EffectPixelate,
    EffectHighlight,
    ToolSpotlight,
    ToolCrop,
    ToolCutOut
}

/// <summary>Which upstream mode a tool belongs to (ShapeManager.OnKeyDown).</summary>
[Flags]
public enum ShapeAvailability
{
    None = 0,
    Region = 1 << 0,
    Annotation = 1 << 1,
    Editor = 1 << 2
}

/// <summary>
/// Toolbar metadata: upstream label, keyboard shortcut and mode gating, extracted
/// from ShapeManager's key handling rather than invented.
/// </summary>
public sealed record ShapeToolInfo(
    ShapeType Type,
    string Label,
    string Shortcut,
    ShapeAvailability Availability,
    bool Implemented,
    string? Note = null);

public static class ShapeCatalog
{
    /// <summary>
    /// All 26 tools in upstream order. <see cref="ShapeToolInfo.Implemented"/>
    /// records what this build can actually draw; an unimplemented tool stays
    /// visible and disabled with a reason rather than being hidden, because
    /// PROJECT-SPEC.md forbids presenting incomplete behaviour as working.
    /// </summary>
    public static IReadOnlyList<ShapeToolInfo> All { get; } = new List<ShapeToolInfo>
    {
        new(ShapeType.RegionRectangle, "Rectangle region", "0", ShapeAvailability.Region, true),
        new(ShapeType.RegionEllipse, "Ellipse region", "", ShapeAvailability.Region, true),
        new(ShapeType.RegionFreehand, "Freehand region", "", ShapeAvailability.Region, true),
        new(ShapeType.ToolSelect, "Select and move", "M",
            ShapeAvailability.Annotation | ShapeAvailability.Editor, true),
        new(ShapeType.DrawingRectangle, "Rectangle", "R",
            ShapeAvailability.Annotation | ShapeAvailability.Editor, true),
        new(ShapeType.DrawingEllipse, "Ellipse", "E",
            ShapeAvailability.Annotation | ShapeAvailability.Editor, true),
        new(ShapeType.DrawingFreehand, "Freehand", "F",
            ShapeAvailability.Annotation | ShapeAvailability.Editor, true),
        new(ShapeType.DrawingFreehandArrow, "Freehand arrow", "",
            ShapeAvailability.Annotation | ShapeAvailability.Editor, true),
        new(ShapeType.DrawingLine, "Line", "L",
            ShapeAvailability.Annotation | ShapeAvailability.Editor, true),
        new(ShapeType.DrawingArrow, "Arrow", "A",
            ShapeAvailability.Annotation | ShapeAvailability.Editor, true),
        new(ShapeType.DrawingTextOutline, "Text (outline)", "O",
            ShapeAvailability.Annotation | ShapeAvailability.Editor, true),
        new(ShapeType.DrawingTextBackground, "Text (background)", "T",
            ShapeAvailability.Annotation | ShapeAvailability.Editor, true),
        new(ShapeType.DrawingSpeechBalloon, "Speech balloon", "S",
            ShapeAvailability.Annotation | ShapeAvailability.Editor, true),
        new(ShapeType.DrawingStep, "Step counter", "I",
            ShapeAvailability.Annotation | ShapeAvailability.Editor, true),
        new(ShapeType.DrawingMagnify, "Magnify", "",
            ShapeAvailability.Annotation | ShapeAvailability.Editor, true),
        new(ShapeType.DrawingImage, "Image from file", "",
            ShapeAvailability.Annotation | ShapeAvailability.Editor, false,
            "Image insertion is not wired up yet."),
        new(ShapeType.DrawingImageScreen, "Image from screen", "",
            ShapeAvailability.Annotation | ShapeAvailability.Editor, false,
            "Image insertion is not wired up yet."),
        new(ShapeType.DrawingSticker, "Sticker", "",
            ShapeAvailability.Annotation | ShapeAvailability.Editor, false,
            "Sticker/emoji assets are not ported yet."),
        new(ShapeType.DrawingCursor, "Cursor", "",
            ShapeAvailability.Annotation | ShapeAvailability.Editor, false,
            "Windows cursor rendering is replaced on macOS and not implemented yet."),
        new(ShapeType.DrawingSmartEraser, "Smart eraser", "",
            ShapeAvailability.Annotation | ShapeAvailability.Editor, false,
            "Smart eraser is not ported yet."),
        new(ShapeType.EffectBlur, "Blur", "B",
            ShapeAvailability.Annotation | ShapeAvailability.Editor, true),
        new(ShapeType.EffectPixelate, "Pixelate", "P",
            ShapeAvailability.Annotation | ShapeAvailability.Editor, true),
        new(ShapeType.EffectHighlight, "Highlight", "H",
            ShapeAvailability.Annotation | ShapeAvailability.Editor, true),
        new(ShapeType.ToolSpotlight, "Spotlight", "",
            ShapeAvailability.Annotation | ShapeAvailability.Editor, true),
        new(ShapeType.ToolCrop, "Crop", "C", ShapeAvailability.Editor, false,
            "Crop belongs to editor mode, which is not built yet."),
        new(ShapeType.ToolCutOut, "Cut out", "X", ShapeAvailability.Editor, false,
            "Cut out belongs to editor mode, which is not built yet.")
    };

    public static ShapeToolInfo Get(ShapeType type) => All.First(t => t.Type == type);

    /// <summary>
    /// Resolves a single-letter or digit shortcut, exactly as upstream's
    /// ShapeManager.OnKeyDown does. Returns null when the key is not a tool key.
    /// </summary>
    public static ShapeType? FromShortcut(char key) => char.ToUpperInvariant(key) switch
    {
        'M' => ShapeType.ToolSelect,
        'R' or '1' => ShapeType.DrawingRectangle,
        'E' or '2' => ShapeType.DrawingEllipse,
        'F' or '3' => ShapeType.DrawingFreehand,
        'L' or '4' => ShapeType.DrawingLine,
        'A' or '5' => ShapeType.DrawingArrow,
        'O' or '6' => ShapeType.DrawingTextOutline,
        'T' => ShapeType.DrawingTextBackground,
        'S' => ShapeType.DrawingSpeechBalloon,
        'I' or '7' => ShapeType.DrawingStep,
        'B' or '8' => ShapeType.EffectBlur,
        'P' or '9' => ShapeType.EffectPixelate,
        'H' => ShapeType.EffectHighlight,
        'C' => ShapeType.ToolCrop,
        'X' => ShapeType.ToolCutOut,
        '0' => ShapeType.RegionRectangle,
        _ => null
    };
}
