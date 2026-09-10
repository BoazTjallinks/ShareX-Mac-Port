# Region capture annotation tools — extracted from the pinned source

Source: `reference/ShareX/ShareX.ScreenCaptureLib/Enums.cs` (`ShapeType`) and
`Shapes/ShapeManager.cs` (key handling) @ `d2502561f63f…`.

PROJECT-SPEC.md pins **26 region shape/tool entries**. This is that list, in
upstream declaration order, which is also the toolbar order.

| # | ShapeType | Toolbar meaning | Shortcut(s) |
| --- | --- | --- | --- |
| 1 | `RegionRectangle` | Rectangle region select | `NumPad0` (annotation mode) |
| 2 | `RegionEllipse` | Ellipse region select | — |
| 3 | `RegionFreehand` | Freehand region select | — |
| 4 | `ToolSelect` | Move / resize existing shapes | `M` |
| 5 | `DrawingRectangle` | Draw rectangle | `R`, `NumPad1` |
| 6 | `DrawingEllipse` | Draw ellipse | `E`, `NumPad2` |
| 7 | `DrawingFreehand` | Freehand pen | `F`, `NumPad3` |
| 8 | `DrawingFreehandArrow` | Freehand arrow | — |
| 9 | `DrawingLine` | Line | `L`, `NumPad4` |
| 10 | `DrawingArrow` | Arrow | `A`, `NumPad5` |
| 11 | `DrawingTextOutline` | Text with outline | `O`, `NumPad6` |
| 12 | `DrawingTextBackground` | Text with background | `T` |
| 13 | `DrawingSpeechBalloon` | Speech balloon | `S` |
| 14 | `DrawingStep` | Numbered step counter | `I`, `NumPad7` |
| 15 | `DrawingMagnify` | Magnifier | — |
| 16 | `DrawingImage` | Insert image from file | — |
| 17 | `DrawingImageScreen` | Insert image from screen | — |
| 18 | `DrawingSticker` | Sticker / emoji | — |
| 19 | `DrawingCursor` | Cursor graphic | — |
| 20 | `DrawingSmartEraser` | Smart eraser | — |
| 21 | `EffectBlur` | Blur region | `B`, `NumPad8` |
| 22 | `EffectPixelate` | Pixelate region | `P`, `NumPad9` |
| 23 | `EffectHighlight` | Highlight region | `H` |
| 24 | `ToolSpotlight` | Spotlight | — |
| 25 | `ToolCrop` | Crop (editor mode) | `C` |
| 26 | `ToolCutOut` | Cut out (editor mode) | `X` |

## Mode gating (upstream `ShapeManager.OnKeyDown`)

Not every tool is available in every mode:

- **Annotation mode only**: `Tab` swaps shape type, `NumPad0` returns to
  `RegionRectangle`.
- **`IsAnnotationMode`**: the drawing/effect shortcuts above.
- **`IsEditorMode`**: `C` crop, `X` cut out, `Ctrl+S` save.

So `ToolCrop` and `ToolCutOut` are editor-mode tools, not region-selection tools.

## Editing shortcuts (annotation mode)

| Keys | Action |
| --- | --- |
| `Ctrl+Z` | Undo |
| `Ctrl+Y` | Redo |
| `Ctrl+D` | Duplicate current shape |
| `Ctrl+V` | Paste from clipboard |
| `Home` | Bring shape to front |
| `End` | Send shape to back |
| `PageUp` | Move shape up one |
| `PageDown` | Move shape down one |
| `Tab` | Swap shape type |

## Port notes

- The enum member names and their ORDER are the serialized contract and the
  toolbar order; neither may be changed.
- On macOS the `Ctrl+` combinations become `Cmd+` by platform convention, with
  the upstream bindings kept available as a compatibility profile
  (PROJECT-SPEC.md section 11). Single-letter tool shortcuts stay identical.
- `DrawingCursor` and `DrawingSticker` depend on OS-specific assets; upstream's
  Windows cursor/emoji renderers are replaced (ADR 0004) and the visual result
  will differ. That is a recorded platform difference, not parity.
