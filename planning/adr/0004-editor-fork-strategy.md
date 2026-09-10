# ADR 0004 — Fork the upstream Avalonia editor; replace four files, not the editor

Status: accepted and EXECUTED (fork landed)
Date: 2026-09-10

## Context

PROJECT-SPEC.md section 2 warns: "The existing new editor uses Avalonia but still
has DirectML/DirectX and Windows renderer dependencies. It is not portable
unchanged." Stage 3 owes 20 `EditorTool` values and 232 effect IDs. The question
was how much of `reference/ShareX/ShareX.ImageEditor` survives a Mac port.

## Survey result (measured)

`ShareX.ImageEditor` is 428 `.cs` files and 33 `.axaml` files. Grepping for
`Vortice`, `DirectML`, `System.Windows.Forms`, `System.Drawing`, `DllImport` and
`Win32` matches **five** files:

| File | Lines | Windows dependency | Replacement |
| --- | --- | --- | --- |
| `Core/BackgroundRemoval/BackgroundRemovalService.cs` | 466 | `Vortice.DXGI` adapter enumeration, `Microsoft.ML.OnnxRuntime.DirectML` session creation | Portable ONNX Runtime; CPU baseline, CoreML only after fixture comparison. Same model files, preprocessing and mask postprocessing. |
| `Presentation/Emoji/WindowsEmojiBitmapRenderer.cs` | 319 | Direct2D / DirectWrite / WIC | CoreText glyph rasterisation via the native bridge. Visual differences recorded, not claimed as parity. |
| `Presentation/Rendering/WindowsCursorBitmapRenderer.cs` | 422 | `user32`/`gdi32` cursor handles, reflection onto `System.Windows.Forms.Cursors` | Mac cursor images via the native bridge. |
| `Presentation/Views/ScreenColorPickerWindow.axaml.cs` | 283 | `user32`/`gdi32` screen pixel read | Native `capture.screenshot` region read with explicit colour-space policy. |
| `Hosting/WindowsDesktopWallpaperService.cs` | 150 | `user32` | **Already solved upstream** — `Hosting/MacOSDesktopWallpaperService.cs` exists next to it. |

The remaining 423 files, all 33 XAML views and the whole effect library are
platform-neutral Avalonia + SkiaSharp. The editor also already ships an injection
seam (`Hosting/EditorServices.cs`, `IClipboardService`, `IDesktopWallpaperService`,
`Core/Abstractions/IAnnotationToolbarAdapter`) and per-OS wallpaper services,
i.e. it was written with non-Windows hosting in mind.

## Decision

Fork `ShareX.ImageEditor` into `src/ShareX.Editor` as an attributed derivative,
keeping the upstream directory layout and file names so ancestry stays obvious
and future upstream diffs remain readable. Change only:

1. The four Windows-specific renderer/service files above, each replaced by a
   `Mac…` sibling behind the existing injection seam rather than edited in place
   where the seam allows it.
2. `ShareX.ImageEditor.csproj` → drop `Vortice.Direct2D1`, `Vortice.DXGI` and
   `Microsoft.ML.OnnxRuntime.DirectML`; retarget `net9.0` → `net10.0`; drop the
   `x64;ARM64` Windows `Platforms` list.
3. `Assets/*.cur` (Windows cursor resources) — keep the files, but load them
   through a portable decoder instead of a Win32 cursor handle.

Explicitly NOT done: rewriting the editor, redesigning its XAML, or reducing its
tool/effect set. Effect parameter names, defaults, bounds, ordering, seeds and
serialized IDs stay byte-identical to upstream.

## Consequences

- Stage 3 becomes mostly a build-and-verify exercise plus four real ports, rather
  than a reimplementation of 232 effects. This is the single largest scope
  reduction available and it comes from evidence, not from cutting features.
- The legacy editor (`ShareX.ScreenCaptureLib` region shapes, 51 legacy effects
  in `ShareX.ImageEffectsLib`) is a separate, genuinely WinForms/GDI+ port and
  keeps its full stage-3 cost.
- Emoji and cursor rendering will differ visually from Windows. That is a
  recorded platform difference with its own ledger exceptions; it is not parity.


## Outcome (executed)

The fork landed as `src/ShareX.Editor` and **builds clean on macOS**, keeping the
upstream directory layout, file names, namespace (`ShareX.ImageEditor`) and
assembly name so the 33 `.axaml` files work unchanged and future upstream diffs
stay readable.

What actually had to change, against the five files the survey predicted:

| File | Resolution |
| --- | --- |
| `Core/BackgroundRemoval/BackgroundRemovalService.cs` | Dropped `Vortice.DXGI` adapter enumeration and the DirectML provider. Switched to `Microsoft.ML.OnnxRuntime` (CPU) with an optional CoreML provider that is **queried, not assumed** (`OrtEnv.Instance().GetAvailableProviders()`), and only used when the caller explicitly asks for GPU. Same model files, same preprocessing. CoreML output tolerance is unverified and labelled as such. |
| `Presentation/Rendering/WindowsCursorBitmapRenderer.cs` | Replaced by `MacCursorBitmapRenderer`: cursor shapes drawn with Skia vector paths. Same public surface and caching. |
| `Presentation/Emoji/WindowsEmojiBitmapRenderer.cs` | Replaced by `MacEmojiBitmapRenderer`: Apple Color Emoji through Skia, with upstream's padding constants, size quantisation and bounded cache preserved exactly. |
| `Hosting/WindowsDesktopWallpaperService.cs` | Deleted; upstream already shipped `MacOSDesktopWallpaperService` beside it, and the `OperatingSystem.IsWindows()` branch was removed. |
| `Presentation/Views/ScreenColorPickerWindow.axaml.cs` | Compiled unchanged — its `user32`/`gdi32` P/Invokes are declared but only reached on Windows. Left as-is for now; the native `pixel.colorAt` route replaces them when the picker is wired up. |

### Effect inventory, verified

232 `public override string Id` literals in the fork, matching upstream's 232
exactly — no additions, no omissions, verified by diffing the id sets.

**230 of those compile.** `Rotate3DImageEffect` and `Rotate3DBoxImageEffect` are
wrapped in `/* TODO: SkiaSharp bug … */` in **upstream v21.0.0 itself**, so they
produce no types there either. This is an upstream decision the port inherits,
not a porting loss, and `tests/ShareX.Core.Tests/EditorForkParityTests.cs` pins
both the 230 count and the reason, so the gap cannot quietly become a real
omission later.

`EditorTool` has its 20 values.

### Cost

The whole fork was four real file replacements plus a project file. The survey's
prediction held: this was the single largest scope win available in the project,
and it came from evidence rather than from cutting features.
