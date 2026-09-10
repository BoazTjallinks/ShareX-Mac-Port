# Feature-by-feature implementation catalogue

This source-derived catalogue maps each callable command and additional feature surface to implementation behavior. All entries are design requirements; none is claimed implemented. Exact option declarations are indexed separately. Read PROJECT-SPEC.md for shared geometry, threading, workflow, storage and error-handling contracts.

## All 75 executable HotkeyType commands

Source: [ShareX/Enums.cs](https://github.com/ShareX/ShareX/blob/d2502561f63fc3ff502cacd91514e3f7f2948c74/ShareX/Enums.cs); dispatch/orchestration: [ShareX/TaskHelpers.cs](https://github.com/ShareX/ShareX/blob/d2502561f63fc3ff502cacd91514e3f7f2948c74/ShareX/TaskHelpers.cs). The `None` sentinel is preserved in settings and has no executable action.

| ID | User-visible behavior | macOS implementation | Acceptance case |
| --- | --- | --- | --- |
| `command.FileUpload` | FileUpload — Upload one or more files. | Reuse file task preparation, MIME/type routing and per-task destinations; stream bytes without loading entire files. | UPL-04 |
| `command.FolderUpload` | FolderUpload — Upload the selected folder using source-defined traversal and handling. | Port UploadManager folder behavior, ordering and filters; do not assume all folders become ZIP archives. | UPL-04 |
| `command.ClipboardUpload` | ClipboardUpload — Route clipboard image, file, text or URL into its configured workflow. | Map NSPasteboard types into upstream precedence and clipboard settings. | INT-04 |
| `command.ClipboardUploadWithContentViewer` | ClipboardUploadWithContentViewer — Inspect clipboard content before sending. | Rebuild the content viewer and continue/cancel decisions; reuse the same upload dispatcher. | INT-04 |
| `command.UploadText` | UploadText — Enter and upload text. | Preserve encoding, extension, formatting and text-provider options. | UPL-04 |
| `command.UploadURL` | UploadURL — Fetch remote content and upload it. | Separate download and upload errors; stream and limit downloads; preserve name and type behavior. | UPL-07 |
| `command.DragDropUpload` | DragDropUpload — Use the drag/drop target to submit content. | Avalonia file/text/image drop target with the same source routing and task settings. | INT-04 |
| `command.ShortenURL` | ShortenURL — Send a URL to the configured shortener. | Reuse the provider adapter and after-upload URL handling, without accidental recursion. | UPL-04 |
| `command.StopUploads` | StopUploads — Cancel active/queued uploads. | Cancel transport and queued work; preserve known versus uncertain remote outcomes. | WF-07 |
| `command.PrintScreen` | PrintScreen — Capture the complete desktop. | ScreenCaptureKit per-display capture and explicit mixed-scale composition; command does not depend on a physical Print Screen key. | CAP-02 |
| `command.ActiveWindow` | ActiveWindow — Capture the foreground window. | Snapshot the foreground window before ShareX activation; apply supported frame/shadow options. | CAP-04 |
| `command.CustomWindow` | CustomWindow — Capture a window matched by the saved window criterion. | Port title/process matching and selection semantics using native window enumeration. | CAP-07 |
| `command.ActiveMonitor` | ActiveMonitor — Capture the active monitor. | Match the source definition of active monitor and map pointer/focus/display identity on Mac. | CAP-03 |
| `command.RectangleRegion` | RectangleRegion — Open the full region selector. | Port legacy selection/annotation interaction and use native screen frames and permission state. | CAP-08 |
| `command.RectangleLight` | RectangleLight — Open the lightweight region selector. | Preserve this mode's reduced interaction path rather than merely renaming the default overlay. | CAP-08 |
| `command.RectangleTransparent` | RectangleTransparent — Select using the transparent overlay mode. | Reproduce live/transparent selection behavior from the source, independently of window-alpha capture. | CAP-08 |
| `command.CustomRegion` | CustomRegion — Capture a stored rectangle. | Validate saved geometry against current display transforms and screen layout. | CAP-03 |
| `command.LastRegion` | LastRegion — Recapture the previous region. | Persist selection geometry/mask with display context; explicitly handle invalidated layouts. | CAP-03 |
| `command.ScrollingCapture` | ScrollingCapture — Capture content larger than its viewport. | Native scroll actions plus source-aligned delays, overlap estimation, stitching, preview and partial-result handling. | SCR-01 |
| `command.AutoCapture` | AutoCapture — Configure periodic captures. | Rebuild interval/target controls and route timed jobs through task snapshots. | AUT-01 |
| `command.StartAutoCapture` | StartAutoCapture — Start the configured periodic capture. | Reuse the auto-capture controller; prevent duplicate running schedules. | AUT-01 |
| `command.StopAutoCapture` | StopAutoCapture — Stop periodic capture. | Cancel future ticks and handle an in-flight capture deliberately. | AUT-01 |
| `command.ScreenRecorder` | ScreenRecorder — Select a region for video recording. | Native screen/audio capture and the configured recording workflow. | MED-01 |
| `command.ScreenRecorderActiveWindow` | ScreenRecorderActiveWindow — Record the active window. | Resolve the window before showing controls; preserve source crop/follow behavior and report a destroyed target. | MED-01 |
| `command.ScreenRecorderCustomRegion` | ScreenRecorderCustomRegion — Record the configured rectangle. | Translate stored geometry to capture pixels and validate encoder dimensions. | MED-01 |
| `command.StartScreenRecorder` | StartScreenRecorder — Start recording through the source's start command. | Use the same controller and source start/selection logic; do not create a separate recorder. | MED-01 |
| `command.ScreenRecorderGIF` | ScreenRecorderGIF — Select a region and record an animated GIF. | Use a lossless pixel source and configured FPS, palette, dither and looping. | MED-03 |
| `command.ScreenRecorderGIFActiveWindow` | ScreenRecorderGIFActiveWindow — Record an active-window GIF. | Apply active-window target resolution to the GIF path. | MED-03 |
| `command.ScreenRecorderGIFCustomRegion` | ScreenRecorderGIFCustomRegion — Record a configured-region GIF. | Apply saved rectangle and scale rules to the GIF path. | MED-03 |
| `command.StartScreenRecorderGIF` | StartScreenRecorderGIF — Start the configured GIF recording path. | Preserve the start command semantics and share the recorder state machine. | MED-03 |
| `command.StopScreenRecording` | StopScreenRecording — Finalize the active recording. | Stop samples, flush media, finalize once and continue downstream tasks. | MED-02 |
| `command.PauseScreenRecording` | PauseScreenRecording — Pause/resume the recording. | Align video/system/mic timestamps and remove paused intervals from the output timeline. | MED-02 |
| `command.AbortScreenRecording` | AbortScreenRecording — Abort the active recording. | Honor confirmation setting and clean only job-owned temporary media. | MED-04 |
| `command.ColorPicker` | ColorPicker — Pick/edit a color in a tool window. | Port source color models, formats and clipboard outputs with a portable UI. | INT-04 |
| `command.ScreenColorPicker` | ScreenColorPicker — Sample a pixel anywhere on screen. | Native captured pixels with explicit color-space policy, magnifier and formatted copy variants. | CAP-01 |
| `command.Ruler` | Ruler — Measure an on-screen rectangle or distance. | Use the same coordinate transforms as capture and display source-equivalent dimension information. | CAP-01 |
| `command.PinToScreen` | PinToScreen — Open the pin tool. | Port source selection behavior and floating-panel options. | INT-04 |
| `command.PinToScreenFromScreen` | PinToScreenFromScreen — Capture a region into a floating pin. | Capture service followed by an app-owned native/Avalonia floating panel. | INT-04 |
| `command.PinToScreenFromClipboard` | PinToScreenFromClipboard — Pin the clipboard image. | Read image content without mutating clipboard; clone artifact ownership. | INT-04 |
| `command.PinToScreenFromFile` | PinToScreenFromFile — Pin an image file. | Use portable decoding and an independently owned pin artifact. | INT-04 |
| `command.PinToScreenCloseAll` | PinToScreenCloseAll — Close every ShareX pin. | Close only app-owned pin panels and release buffers. | INT-04 |
| `command.ImageEditor` | ImageEditor — Open the configured new or legacy editor. | Retain the editor selector, source default and both editing paths. | EDT-01 |
| `command.ImageBeautifier` | ImageBeautifier — Style an image with background, spacing and frame options. | Port source beautifier settings/rendering and preview/export workflow. | EDT-04 |
| `command.ImageEffects` | ImageEffects — Manage and apply image-effect presets. | Support legacy presets plus source-defined editor effects; preserve identifiers and order. | EDT-02 |
| `command.ImageViewer` | ImageViewer — View images with source navigation and viewing controls. | Rebuild the viewer around portable image buffers and existing commands. | UI-01 |
| `command.BackgroundRemover` | BackgroundRemover — Remove image background with a selected local model. | Reuse model/preprocessing/mask logic; replace DirectML with portable ONNX execution. | EDT-05 |
| `command.ImageComparer` | ImageComparer — Compare selected images. | Port comparison modes, slider/zoom and source controls, reusing new-editor comparer logic where suitable. | EDT-04 |
| `command.ImageCombiner` | ImageCombiner — Combine multiple images. | Port order, orientation, spacing, background and sizing rules from the source options. | EDT-04 |
| `command.ImageSplitter` | ImageSplitter — Split an image into pieces. | Preserve source row/column/size choices, edge policy and output naming. | EDT-04 |
| `command.ImageThumbnailer` | ImageThumbnailer — Generate image thumbnails. | Preserve aspect ratio, dimensions, quality, suffix and conditional resizing. | EDT-04 |
| `command.VideoConverter` | VideoConverter — Convert media using configured options. | Port media arguments and UI; discover actual FFmpeg capabilities on arm64. | MED-04 |
| `command.VideoThumbnailer` | VideoThumbnailer — Create video preview frames/contact sheets. | Use ffprobe/FFmpeg and preserve timestamp, grid, sizing and label rules. | MED-04 |
| `command.AnalyzeImage` | AnalyzeImage — Analyze an image with configured AI provider. | Port OpenAI, Gemini, OpenRouter and legacy provider request workflows, prompts and result copy controls; explicit user credentials. | UPL-04 |
| `command.OCR` | OCR — Recognize text from the source-selected image/region. | Apple Vision with language/scaling/single-line controls and editable result window; disclose engine difference. | CAP-05 |
| `command.QRCode` | QRCode — Open QR generation/reading tool. | Reuse ZXing algorithms with portable pixels and source UI options. | INT-04 |
| `command.QRCodeDecodeFromScreen` | QRCodeDecodeFromScreen — Decode QR content from screen. | Capture then decode using the configured source path and present content without silently opening it. | CAP-01 |
| `command.QRCodeScanRegion` | QRCodeScanRegion — Select and scan a QR region. | Selection overlay plus decoder and result workflow. | CAP-08 |
| `command.HashCheck` | HashCheck — Calculate and compare file hashes. | Port the source algorithm list and streamed hashing; preserve case/format and comparison results. | SET-01 |
| `command.Metadata` | Metadata — Inspect file metadata. | Port the source metadata presentation and supported formats using portable/native readers. | EDT-04 |
| `command.StripMetadata` | StripMetadata — Remove supported metadata. | Preserve source selection/output rules; validate actual metadata removal and image orientation. | EDT-04 |
| `command.IndexFolder` | IndexFolder — Create a directory index. | Reuse IndexerLib traversal, exclusions, templates and HTML/text/XML outputs; handle symlinks and permission errors. | SET-01 |
| `command.ClipboardViewer` | ClipboardViewer — Inspect available clipboard formats. | Expose supported Mac pasteboard content types with text/image/file/HTML previews; preserve unknown types when possible. | INT-04 |
| `command.BorderlessWindow` | BorderlessWindow — Choose a window for borderless mode. | Show capability and explanation; arbitrary third-party Mac title-bar removal is not supported by a general public API. | CAP-07 |
| `command.ActiveWindowBorderless` | ActiveWindowBorderless — Remove borders from the active external window. | Keep command/import identity and explicit platform-blocked result; do not substitute a screenshot proxy. | CAP-07 |
| `command.ActiveWindowTopMost` | ActiveWindowTopMost — Keep the active external window on top. | Record unsupported persistent cross-process level control; raising/floating an own panel is not equivalent. | CAP-07 |
| `command.InspectWindow` | InspectWindow — Inspect window properties. | Expose supported native window/Accessibility/process fields and mark unavailable Win32 properties. | CAP-07 |
| `command.MonitorTest` | MonitorTest — Display monitor test patterns. | Port solid colors, gradients and tearing-pattern options to correctly sized display windows. | CAP-03 |
| `command.DisableHotkeys` | DisableHotkeys — Toggle global shortcut registration. | Unregister/register command bindings and preserve state without quitting. | INT-01 |
| `command.OpenMainWindow` | OpenMainWindow — Show the main ShareX window. | Activate the existing UI and restore source-equivalent window state. | UI-01 |
| `command.OpenScreenshotsFolder` | OpenScreenshotsFolder — Reveal the configured screenshot directory. | Resolve current configured path and open Finder. | INT-02 |
| `command.OpenHistory` | OpenHistory — Open task/file history. | SQLite-backed history table, search/filter and row actions. | HIS-01 |
| `command.OpenImageHistory` | OpenImageHistory — Open visual image history. | Virtualized thumbnails, dates, missing-file handling and source context actions. | HIS-02 |
| `command.ToggleActionsToolbar` | ToggleActionsToolbar — Show/hide the configurable action toolbar. | Port toolbar placement, configuration and command bindings. | UI-01 |
| `command.ToggleTrayMenu` | ToggleTrayMenu — Open/close the menu-bar menu. | Map the source tray-menu command to the app's Mac status-item menu. | UI-01 |
| `command.ExitShareX` | ExitShareX — Quit the application. | Release hotkeys, streams, panels and locks; handle active tasks using source-equivalent decisions. | PKG-02 |

## 22 after-capture flags

Menu/enum order is not execution order. The source value expression is preserved in the raw inventory.

| ID | Behavior | Implementation | Acceptance |
| --- | --- | --- | --- |
| `AfterCaptureTasks.ShowQuickTaskMenu` | ShowQuickTaskMenu — Choose a quick task preset. | Rebuild source quick-task UI; apply selected task snapshot and source cancellation behavior. | WF-01 to WF-07 |
| `AfterCaptureTasks.ShowAfterCaptureWindow` | ShowAfterCaptureWindow — Show the after-capture decision window. | Port decision options and resulting flags before entering the worker pipeline. | WF-01 to WF-07 |
| `AfterCaptureTasks.BeautifyImage` | BeautifyImage — Apply image beautification. | Use source beautifier settings; cancelling can stop the image stage. | WF-01 to WF-07 |
| `AfterCaptureTasks.AddImageEffects` | AddImageEffects — Apply selected effect preset. | Preserve effect order/defaults and null/error behavior. | WF-01 to WF-07 |
| `AfterCaptureTasks.AnnotateImage` | AnnotateImage — Open the configured image editor. | Await continue/cancel; the edited image feeds later stages. | WF-01 to WF-07 |
| `AfterCaptureTasks.CopyImageToClipboard` | CopyImageToClipboard — Copy the current image. | Write correct pasteboard image representations at the source-defined stage. | WF-01 to WF-07 |
| `AfterCaptureTasks.PinToScreen` | PinToScreen — Pin a copy of the current image. | Clone artifact lifetime so the pin survives worker disposal. | WF-01 to WF-07 |
| `AfterCaptureTasks.SendImageToPrinter` | SendImageToPrinter — Print the image. | Map source image/print options to normal macOS print interaction. | WF-01 to WF-07 |
| `AfterCaptureTasks.SaveImageToFile` | SaveImageToFile — Save with the configured name and folder. | Use source encoding, filename pattern and collision policy. | WF-01 to WF-07 |
| `AfterCaptureTasks.SaveImageToFileWithDialog` | SaveImageToFileWithDialog — Save using a path dialog. | Match source cancel/retry behavior; use an approved Mac file dialog. | WF-01 to WF-07 |
| `AfterCaptureTasks.SaveThumbnailImageToFile` | SaveThumbnailImageToFile — Save a thumbnail. | Honor source dependency on prepared/saved image and source dimensions/naming. | WF-01 to WF-07 |
| `AfterCaptureTasks.PerformActions` | PerformActions — Run configured external programs. | Preserve ordering, replacement-output handling and input cleanup; use argument vectors and explicit enabled configuration. | WF-01 to WF-07 |
| `AfterCaptureTasks.CopyFileToClipboard` | CopyFileToClipboard — Copy a file reference. | Use file URL pasteboard types; honor precedence over file/folder path copying. | WF-01 to WF-07 |
| `AfterCaptureTasks.CopyFilePathToClipboard` | CopyFilePathToClipboard — Copy the file path. | Only selected when file-copy precedence does not win; map to local Mac path. | WF-01 to WF-07 |
| `AfterCaptureTasks.CopyFolderPathToClipboard` | CopyFolderPathToClipboard — Copy the containing folder path. | Honor source else-if precedence and file existence dependency. | WF-01 to WF-07 |
| `AfterCaptureTasks.ShowInExplorer` | ShowInExplorer — Reveal the file in its folder. | Select the file in Finder using native workspace APIs. | WF-01 to WF-07 |
| `AfterCaptureTasks.AnalyzeImage` | AnalyzeImage — Open image analysis. | Port AI tool and source file-preparation requirement; never automatically test using real content. | WF-01 to WF-07 |
| `AfterCaptureTasks.ScanQRCode` | ScanQRCode — Read QR content from the prepared image. | Use portable ZXing reader and source result window. | WF-01 to WF-07 |
| `AfterCaptureTasks.DoOCR` | DoOCR — Run OCR through the task pipeline. | Preserve the source call position and file/image dependencies; use Vision with explicit engine difference. | WF-01 to WF-07 |
| `AfterCaptureTasks.ShowBeforeUploadWindow` | ShowBeforeUploadWindow — Show upload confirmation/options. | Suspend preparation for the source-defined dialog decisions. | WF-01 to WF-07 |
| `AfterCaptureTasks.UploadImageToHost` | UploadImageToHost — Send image to selected destination. | Honor image-versus-file destination routing and encoded stream position. | WF-01 to WF-07 |
| `AfterCaptureTasks.DeleteFile` | DeleteFile — Remove the local file according to configured task semantics. | Audit the separate worker completion/failure branch before porting; scope to intended artifact and document any data-protection deviation. | WF-01 to WF-07 |

## 6 after-upload flags

Menu/enum order is not execution order. The source value expression is preserved in the raw inventory.

| ID | Behavior | Implementation | Acceptance |
| --- | --- | --- | --- |
| `AfterUploadTasks.ShowAfterUploadWindow` | ShowAfterUploadWindow — Show the upload-result window. | Port TaskManager result-dialog control flow and copy/open/reupload actions. | WF-01 to WF-07 |
| `AfterUploadTasks.UseURLShortener` | UseURLShortener — Shorten the produced link. | Respect job-type guards and auto-shortening threshold. | WF-01 to WF-07 |
| `AfterUploadTasks.ShareURL` | ShareURL — Send/open the link with selected sharing service. | Preserve source job-type guards and templates. | WF-01 to WF-07 |
| `AfterUploadTasks.CopyURLToClipboard` | CopyURLToClipboard — Copy the resulting formatted link. | Honor shortened/original result, formatting and early-copy settings. | WF-01 to WF-07 |
| `AfterUploadTasks.OpenURL` | OpenURL — Open the resulting link. | Apply configured URL template and call the normal browser-opening service. | WF-01 to WF-07 |
| `AfterUploadTasks.ShowQRCode` | ShowQRCode — Display a QR for the result. | Encode the correct final source-selected URL and show a QR window. | WF-01 to WF-07 |

## New editor — every tool

| ID | Behavior | Implementation | Acceptance |
| --- | --- | --- | --- |
| `editor.Select` | Select — Select and transform annotations. | Reuse hit testing, handles, selection and z-order; preserve input rules. | EDT-01, EDT-03 |
| `editor.Rectangle` | Rectangle — Draw a rectangle. | Reuse geometry, border, fill, corner and rendering options. | EDT-01, EDT-03 |
| `editor.Ellipse` | Ellipse — Draw an ellipse. | Reuse geometry, border/fill and selection transforms. | EDT-01, EDT-03 |
| `editor.Line` | Line — Draw a line. | Reuse endpoint handles, stroke and supported line options. | EDT-01, EDT-03 |
| `editor.Arrow` | Arrow — Draw an arrow. | Reuse arrowhead shape, stroke, endpoints and supported styles. | EDT-01, EDT-03 |
| `editor.Freehand` | Freehand — Draw freehand strokes. | Reuse point collection/smoothing and export rendering. | EDT-01, EDT-03 |
| `editor.Text` | Text — Add editable text. | Reuse text model, styling/alignment and measurement; adapt font/IME services. | EDT-01, EDT-03 |
| `editor.SpeechBalloon` | SpeechBalloon — Add a speech balloon. | Reuse bubble/tail geometry, text layout and styling. | EDT-01, EDT-03 |
| `editor.Step` | Step — Add numbered steps. | Reuse numbering, style and sequence controls. | EDT-01, EDT-03 |
| `editor.Image` | Image — Insert an image. | Preserve source placement, scale, opacity and transform behavior. | EDT-01, EDT-03 |
| `editor.Emoji` | Emoji — Insert an emoji. | Reuse picker/catalogue and annotation model; replace Windows rasterizer with licensed portable/native rendering. | EDT-01, EDT-03 |
| `editor.Cursor` | Cursor — Insert a cursor illustration. | Reuse cursor choices/model; replace OS resource access with licensed assets/native renderer. | EDT-01, EDT-03 |
| `editor.Highlight` | Highlight — Highlight part of the image. | Reuse original effect/color blend and rectangle geometry. | EDT-01, EDT-03 |
| `editor.SmartEraser` | SmartEraser — Erase with the source smart-erasing algorithm. | Retain its sampled-fill/processing behavior; do not replace it with generative inpainting. | EDT-01, EDT-03 |
| `editor.Blur` | Blur — Blur a selected area. | Reuse kernel/radius, edge handling and region geometry. | EDT-01, EDT-03 |
| `editor.Pixelate` | Pixelate — Pixelate a selected area. | Reuse block size, region alignment and alpha handling. | EDT-01, EDT-03 |
| `editor.Magnify` | Magnify — Magnify a selected area. | Reuse source region, scale, border and positioning behavior. | EDT-01, EDT-03 |
| `editor.Spotlight` | Spotlight — Emphasize a selected region. | Reuse outside-region dimming and source blending behavior. | EDT-01, EDT-03 |
| `editor.Crop` | Crop — Crop the image canvas. | Preserve selection, bounds and undo behavior. | EDT-01, EDT-03 |
| `editor.CutOut` | CutOut — Remove an image strip/area using the source tool. | Reuse source cut-out geometry and reflow semantics, not ordinary region erasing. | EDT-01, EDT-03 |

## Region capture and legacy editor — every shape/tool

Preserve this mode separately from the new editor. Exact options and keyboard/mouse behavior come from ShapeManager, RegionCaptureForm and the corresponding shape class.

| ID | Behavior | Implementation | Acceptance |
| --- | --- | --- | --- |
| `region.RegionRectangle` | RegionRectangle — Rectangle capture mask. | Port selection/resize and screenshot crop coordinates. | CAP-08, EDT-01 |
| `region.RegionEllipse` | RegionEllipse — Ellipse capture mask. | Apply an alpha mask to captured pixels with correct clipping. | CAP-08, EDT-01 |
| `region.RegionFreehand` | RegionFreehand — Freehand capture mask. | Retain path selection and rasterize the same region mask. | CAP-08, EDT-01 |
| `region.ToolSelect` | ToolSelect — Select and transform annotations. | Reuse hit testing, handles, selection and z-order; preserve input rules. | CAP-08, EDT-01 |
| `region.DrawingRectangle` | DrawingRectangle — Draw a rectangle. | Reuse geometry, border, fill, corner and rendering options. | CAP-08, EDT-01 |
| `region.DrawingEllipse` | DrawingEllipse — Draw an ellipse. | Reuse geometry, border/fill and selection transforms. | CAP-08, EDT-01 |
| `region.DrawingFreehand` | DrawingFreehand — Draw freehand strokes. | Reuse point collection/smoothing and export rendering. | CAP-08, EDT-01 |
| `region.DrawingFreehandArrow` | DrawingFreehandArrow — Freehand arrow annotation. | Port original point path and arrowhead algorithm. | CAP-08, EDT-01 |
| `region.DrawingLine` | DrawingLine — Draw a line. | Reuse endpoint handles, stroke and supported line options. | CAP-08, EDT-01 |
| `region.DrawingArrow` | DrawingArrow — Draw an arrow. | Reuse arrowhead shape, stroke, endpoints and supported styles. | CAP-08, EDT-01 |
| `region.DrawingTextOutline` | DrawingTextOutline — Outlined text annotation. | Port the legacy outlined text model and stroke/fill settings. | CAP-08, EDT-01 |
| `region.DrawingTextBackground` | DrawingTextBackground — Text with a background. | Port the legacy text box, padding/background and text measurement. | CAP-08, EDT-01 |
| `region.DrawingSpeechBalloon` | DrawingSpeechBalloon — Add a speech balloon. | Reuse bubble/tail geometry, text layout and styling. | CAP-08, EDT-01 |
| `region.DrawingStep` | DrawingStep — Add numbered steps. | Reuse numbering, style and sequence controls. | CAP-08, EDT-01 |
| `region.DrawingMagnify` | DrawingMagnify — Magnify a selected area. | Reuse source region, scale, border and positioning behavior. | CAP-08, EDT-01 |
| `region.DrawingImage` | DrawingImage — Insert an image. | Preserve source placement, scale, opacity and transform behavior. | CAP-08, EDT-01 |
| `region.DrawingImageScreen` | DrawingImageScreen — Insert pixels from the screen. | Run supported source selection and insert the resulting image. | CAP-08, EDT-01 |
| `region.DrawingSticker` | DrawingSticker — Insert a sticker. | Port sticker-pack selection, image insertion and asset handling. | CAP-08, EDT-01 |
| `region.DrawingCursor` | DrawingCursor — Insert a cursor illustration. | Reuse cursor choices/model; replace OS resource access with licensed assets/native renderer. | CAP-08, EDT-01 |
| `region.DrawingSmartEraser` | DrawingSmartEraser — Erase with the source smart-erasing algorithm. | Retain its sampled-fill/processing behavior; do not replace it with generative inpainting. | CAP-08, EDT-01 |
| `region.EffectBlur` | EffectBlur — Blur a selected area. | Reuse kernel/radius, edge handling and region geometry. | CAP-08, EDT-01 |
| `region.EffectPixelate` | EffectPixelate — Pixelate a selected area. | Reuse block size, region alignment and alpha handling. | CAP-08, EDT-01 |
| `region.EffectHighlight` | EffectHighlight — Highlight part of the image. | Reuse original effect/color blend and rectangle geometry. | CAP-08, EDT-01 |
| `region.ToolSpotlight` | ToolSpotlight — Emphasize a selected region. | Reuse outside-region dimming and source blending behavior. | CAP-08, EDT-01 |
| `region.ToolCrop` | ToolCrop — Crop the image canvas. | Preserve selection, bounds and undo behavior. | CAP-08, EDT-01 |
| `region.ToolCutOut` | ToolCutOut — Remove an image strip/area using the source tool. | Reuse source cut-out geometry and reflow semantics, not ordinary region erasing. | CAP-08, EDT-01 |

## Additional feature surfaces

These cover important UI/automation features that are not independent HotkeyType entries.

| ID | Behavior | Implementation | Acceptance |
| --- | --- | --- | --- |
| `capture.WindowMenu` | capture.WindowMenu — Capture a chosen enumerated window. | List source-equivalent window choices through native enumeration and capture the selected stable target. | CAP-07 |
| `capture.MonitorMenu` | capture.MonitorMenu — Capture a chosen display. | List connected display identities and use per-display physical pixels. | CAP-03 |
| `capture.DelayAndCursor` | capture.DelayAndCursor — Configure delayed capture and cursor inclusion. | Preserve delay, cancellation and pointer visibility without showing the overlay in output. | CAP-05 |
| `capture.WindowOptions` | capture.WindowOptions — Configure shadows, transparency and client area. | Map supported options; retain unsupported Windows fields and explain differences. | CAP-07 |
| `capture.OverlayPreferences` | capture.OverlayPreferences — Configure dimming, magnifier, snap/fixed sizes, info and tool behavior. | Port every real RegionCaptureOptions field with corresponding interactive control. | CAP-08 |
| `recording.Options` | recording.Options — Configure source, start, timing, encoding and custom arguments. | Preserve every applicable FFmpeg/recording option with capability detection and lossless GIF path. | MED-01 to MED-05 |
| `workflow.NamedPresets` | workflow.NamedPresets — Create and execute named workflows. | Reuse HotkeysConfig/TaskSettings names and overrides even without a physical key binding. | WF-06 |
| `workflow.QuickPresets` | workflow.QuickPresets — Configure quick-task presets. | Port preset model and selection UI without hardcoding only common tasks. | WF-05 |
| `workflow.DefaultOverrides` | workflow.DefaultOverrides — Inherit or override separate settings groups. | Snapshot resolved source reference groups and preserve group-specific override flags. | WF-06 |
| `workflow.Queue` | workflow.Queue — Display and control the task queue. | Preserve status/progress/result/error and available item actions; bound concurrency. | WF-07 |
| `workflow.Actions` | workflow.Actions — Configure executable actions and arguments. | Port ExternalProgram semantics through safe argv construction; translate paths explicitly. | WF-04 |
| `upload.FiltersAndFallback` | upload.FiltersAndFallback — Select uploaders by filters and secondary destinations. | Reuse source routing and retry settings without duplicating successful side effects. | UPL-04 |
| `upload.WatchFolder` | upload.WatchFolder — Automatically handle configured incoming files. | Port watch filters/debounce/stability and task routing; prevent output-folder upload loops. | AUT-01 |
| `upload.CustomUploaderEditor` | upload.CustomUploaderEditor — Import/edit/export/test .sxcu configurations. | Typed form for all source fields, destination flags, body modes and parser helpers. | UPL-01 |
| `upload.SyntaxTester` | upload.SyntaxTester — Test custom response syntax locally. | Reuse parser and provide sanitized sample responses, validation and user input/output handlers. | UPL-02 |
| `upload.OAuth` | upload.OAuth — Authorize and refresh provider accounts. | Separate provider auth logic from WinForms; Keychain tokens and provider-specific loopback/browser flows. | UPL-06 |
| `upload.ProxyAndLimits` | upload.ProxyAndLimits — Configure proxy, network limits and retries. | Port source proxy/auth/timeouts/concurrency options with redacted logs. | UPL-07 |
| `settings.Application` | settings.Application — Edit all real application settings. | Rebuild grouped typed settings from source fields and WinForms bindings; persist atomically. | SET-01 |
| `settings.Task` | settings.Task — Edit all task/general/image/capture/upload/tools/advanced settings. | Preserve options and override visibility, not just a free-form JSON field. | SET-01 |
| `settings.Hotkeys` | settings.Hotkeys — Configure commands and key combinations. | Store command identity separately from Windows and Mac bindings; detect conflicts. | INT-01 |
| `settings.ImportExportBackup` | settings.ImportExportBackup — Import/export settings and backup archives. | Port backup manifest semantics, unknown-field retention, migration report and safe extraction. | SET-03 |
| `settings.EffectPresets` | settings.EffectPresets — Import/export legacy .sxie and effect presets. | Use allowlisted type mapping and retain order/assets/defaults; reject unsafe external paths/types. | SET-03 |
| `settings.FilenamePatterns` | settings.FilenamePatterns — Expand source filename and text tokens. | Reuse NameParser grammar, timezone/counter/random/window fields and collision rules. | SET-01 |
| `history.Data` | history.Data — Store/import/edit history records. | Keep SQLite columns and original links/tags/date/path semantics with backups. | HIS-01 |
| `history.Actions` | history.Actions — Perform history context actions and exports. | Map each source context action; distinguish history removal, local delete and remote deletion. | HIS-02 |
| `ui.MainAndTray` | ui.MainAndTray — Preserve main UI, toolbar and menu-bar behavior. | Rebuild source organization, recent items, theme and configured click actions where supported. | UI-01 |
| `ui.Notifications` | ui.Notifications — Show completion/error previews and click actions. | Implement source notification click dispatch with native notification or app-owned preview capabilities. | INT-04 |
| `ui.ThemesAndLocalization` | ui.ThemesAndLocalization — Preserve themes and available language selections. | Migrate source resources and verify coverage, including English/Dutch and RTL input. | UI-02 |
| `integration.CLI` | integration.CLI — Invoke files, URLs, HotkeyType names, task/workflow and import switches. | Port the CLI parser and forward to the main app through private local IPC. | INT-02 |
| `integration.BrowserHost` | integration.BrowserHost — Accept browser-extension uploads and shorten requests. | Port native-message framing and install user-scoped host manifests only when enabled. | INT-03 |
| `integration.FinderAndTypes` | integration.FinderAndTypes — Open .sxcu/.sxie and initiate file actions from Finder. | Bundle file types/Services or a documented Finder integration; preserve source command routing. | INT-02 |
| `integration.StartupPortable` | integration.StartupPortable — Run at login and use a custom portable data folder. | Explicit login-item setting, writable data folder, machine-bound Keychain secret policy. | PKG-02 |
| `integration.Updates` | integration.Updates — Display version/update information. | Keep local port update status honest; upstream Windows releases are informational only. | PKG-02 |
| `editor.Commands` | editor.Commands — Preserve new/open/save/save-as/copy/print/pin/upload/continue/cancel. | Map toolbar and document commands to the same worker/artifact services. | EDT-01 |
| `editor.HistoryAndCanvas` | editor.HistoryAndCanvas — Preserve undo/redo, zoom/pan, canvas and image geometry. | Retain source document state and transforms across editor operations. | EDT-03 |
| `editor.EffectCatalogue` | editor.EffectCatalogue — Expose every effect and its parameter UI. | Reuse all 232 source IDs and associated parameter schemas; preserve legacy 51-effect presets separately. | EDT-02 |

## Enumerated options and integration variants

Each entry below is preserved in its own source context. Some are sentinel values, settings or variants of features above, not additional independent tools.

| ID | Behavior | Implementation | Acceptance |
| --- | --- | --- | --- |
| `option.CaptureType.Fullscreen` | Fullscreen — Capture target identity. | Dispatch to the capture service using the source enum identity and explicit native target mapping. | CAP-01 to CAP-08 |
| `option.CaptureType.Monitor` | Monitor — Capture target identity. | Dispatch to the capture service using the source enum identity and explicit native target mapping. | CAP-01 to CAP-08 |
| `option.CaptureType.ActiveMonitor` | ActiveMonitor — Capture target identity. | Dispatch to the capture service using the source enum identity and explicit native target mapping. | CAP-01 to CAP-08 |
| `option.CaptureType.Window` | Window — Capture target identity. | Dispatch to the capture service using the source enum identity and explicit native target mapping. | CAP-01 to CAP-08 |
| `option.CaptureType.ActiveWindow` | ActiveWindow — Capture target identity. | Dispatch to the capture service using the source enum identity and explicit native target mapping. | CAP-01 to CAP-08 |
| `option.CaptureType.Region` | Region — Capture target identity. | Dispatch to the capture service using the source enum identity and explicit native target mapping. | CAP-01 to CAP-08 |
| `option.CaptureType.CustomRegion` | CustomRegion — Capture target identity. | Dispatch to the capture service using the source enum identity and explicit native target mapping. | CAP-01 to CAP-08 |
| `option.CaptureType.LastRegion` | LastRegion — Capture target identity. | Dispatch to the capture service using the source enum identity and explicit native target mapping. | CAP-01 to CAP-08 |
| `option.ScreenRecordStartMethod.Region` | Region — Recording target/start choice. | Resolve the same target and selection flow before beginning native capture. | MED-01 |
| `option.ScreenRecordStartMethod.ActiveWindow` | ActiveWindow — Recording target/start choice. | Resolve the same target and selection flow before beginning native capture. | MED-01 |
| `option.ScreenRecordStartMethod.CustomRegion` | CustomRegion — Recording target/start choice. | Resolve the same target and selection flow before beginning native capture. | MED-01 |
| `option.ScreenRecordStartMethod.LastRegion` | LastRegion — Recording target/start choice. | Resolve the same target and selection flow before beginning native capture. | MED-01 |
| `option.RegionCaptureAction.None` | None — Configured pointer/key action. | Port the source action dispatch, including no-op sentinel and remove-then-cancel distinction. | CAP-08 |
| `option.RegionCaptureAction.CancelCapture` | CancelCapture — Configured pointer/key action. | Port the source action dispatch, including no-op sentinel and remove-then-cancel distinction. | CAP-08 |
| `option.RegionCaptureAction.RemoveShapeCancelCapture` | RemoveShapeCancelCapture — Configured pointer/key action. | Port the source action dispatch, including no-op sentinel and remove-then-cancel distinction. | CAP-08 |
| `option.RegionCaptureAction.RemoveShape` | RemoveShape — Configured pointer/key action. | Port the source action dispatch, including no-op sentinel and remove-then-cancel distinction. | CAP-08 |
| `option.RegionCaptureAction.SwapToolType` | SwapToolType — Configured pointer/key action. | Port the source action dispatch, including no-op sentinel and remove-then-cancel distinction. | CAP-08 |
| `option.RegionCaptureAction.CaptureFullscreen` | CaptureFullscreen — Configured pointer/key action. | Port the source action dispatch, including no-op sentinel and remove-then-cancel distinction. | CAP-08 |
| `option.RegionCaptureAction.CaptureActiveMonitor` | CaptureActiveMonitor — Configured pointer/key action. | Port the source action dispatch, including no-op sentinel and remove-then-cancel distinction. | CAP-08 |
| `option.RegionCaptureAction.CaptureLastRegion` | CaptureLastRegion — Configured pointer/key action. | Port the source action dispatch, including no-op sentinel and remove-then-cancel distinction. | CAP-08 |
| `option.ToastClickAction.CloseNotification` | CloseNotification — Notification/preview click action. | Bind the exact command to source-selected artifact/link; map supported Mac notification UI. | INT-04 |
| `option.ToastClickAction.AnnotateImage` | AnnotateImage — Notification/preview click action. | Bind the exact command to source-selected artifact/link; map supported Mac notification UI. | INT-04 |
| `option.ToastClickAction.CopyImageToClipboard` | CopyImageToClipboard — Notification/preview click action. | Bind the exact command to source-selected artifact/link; map supported Mac notification UI. | INT-04 |
| `option.ToastClickAction.CopyFile` | CopyFile — Notification/preview click action. | Bind the exact command to source-selected artifact/link; map supported Mac notification UI. | INT-04 |
| `option.ToastClickAction.CopyFilePath` | CopyFilePath — Notification/preview click action. | Bind the exact command to source-selected artifact/link; map supported Mac notification UI. | INT-04 |
| `option.ToastClickAction.CopyUrl` | CopyUrl — Notification/preview click action. | Bind the exact command to source-selected artifact/link; map supported Mac notification UI. | INT-04 |
| `option.ToastClickAction.OpenFile` | OpenFile — Notification/preview click action. | Bind the exact command to source-selected artifact/link; map supported Mac notification UI. | INT-04 |
| `option.ToastClickAction.OpenFolder` | OpenFolder — Notification/preview click action. | Bind the exact command to source-selected artifact/link; map supported Mac notification UI. | INT-04 |
| `option.ToastClickAction.OpenUrl` | OpenUrl — Notification/preview click action. | Bind the exact command to source-selected artifact/link; map supported Mac notification UI. | INT-04 |
| `option.ToastClickAction.Upload` | Upload — Notification/preview click action. | Bind the exact command to source-selected artifact/link; map supported Mac notification UI. | INT-04 |
| `option.ToastClickAction.PinToScreen` | PinToScreen — Notification/preview click action. | Bind the exact command to source-selected artifact/link; map supported Mac notification UI. | INT-04 |
| `option.ToastClickAction.DeleteFile` | DeleteFile — Notification/preview click action. | Bind the exact command to source-selected artifact/link; map supported Mac notification UI. | INT-04 |
| `option.NativeMessagingAction.None` | None — Browser host command. | Preserve payload meaning and forward validated requests to the main app. | INT-03 |
| `option.NativeMessagingAction.UploadImage` | UploadImage — Browser host command. | Preserve payload meaning and forward validated requests to the main app. | INT-03 |
| `option.NativeMessagingAction.UploadVideo` | UploadVideo — Browser host command. | Preserve payload meaning and forward validated requests to the main app. | INT-03 |
| `option.NativeMessagingAction.UploadAudio` | UploadAudio — Browser host command. | Preserve payload meaning and forward validated requests to the main app. | INT-03 |
| `option.NativeMessagingAction.UploadText` | UploadText — Browser host command. | Preserve payload meaning and forward validated requests to the main app. | INT-03 |
| `option.NativeMessagingAction.ShortenURL` | ShortenURL — Browser host command. | Preserve payload meaning and forward validated requests to the main app. | INT-03 |
| `option.SupportedLanguage.Automatic` | Automatic — Available UI language entry. | Port actual localized resources, fallback and layout; an enum entry alone is not translation completion. | UI-02 |
| `option.SupportedLanguage.Arabic` | Arabic — Available UI language entry. | Port actual localized resources, fallback and layout; an enum entry alone is not translation completion. | UI-02 |
| `option.SupportedLanguage.Dutch` | Dutch — Available UI language entry. | Port actual localized resources, fallback and layout; an enum entry alone is not translation completion. | UI-02 |
| `option.SupportedLanguage.English` | English — Available UI language entry. | Port actual localized resources, fallback and layout; an enum entry alone is not translation completion. | UI-02 |
| `option.SupportedLanguage.French` | French — Available UI language entry. | Port actual localized resources, fallback and layout; an enum entry alone is not translation completion. | UI-02 |
| `option.SupportedLanguage.German` | German — Available UI language entry. | Port actual localized resources, fallback and layout; an enum entry alone is not translation completion. | UI-02 |
| `option.SupportedLanguage.Hebrew` | Hebrew — Available UI language entry. | Port actual localized resources, fallback and layout; an enum entry alone is not translation completion. | UI-02 |
| `option.SupportedLanguage.Hungarian` | Hungarian — Available UI language entry. | Port actual localized resources, fallback and layout; an enum entry alone is not translation completion. | UI-02 |
| `option.SupportedLanguage.Indonesian` | Indonesian — Available UI language entry. | Port actual localized resources, fallback and layout; an enum entry alone is not translation completion. | UI-02 |
| `option.SupportedLanguage.Italian` | Italian — Available UI language entry. | Port actual localized resources, fallback and layout; an enum entry alone is not translation completion. | UI-02 |
| `option.SupportedLanguage.Japanese` | Japanese — Available UI language entry. | Port actual localized resources, fallback and layout; an enum entry alone is not translation completion. | UI-02 |
| `option.SupportedLanguage.Korean` | Korean — Available UI language entry. | Port actual localized resources, fallback and layout; an enum entry alone is not translation completion. | UI-02 |
| `option.SupportedLanguage.MexicanSpanish` | MexicanSpanish — Available UI language entry. | Port actual localized resources, fallback and layout; an enum entry alone is not translation completion. | UI-02 |
| `option.SupportedLanguage.Persian` | Persian — Available UI language entry. | Port actual localized resources, fallback and layout; an enum entry alone is not translation completion. | UI-02 |
| `option.SupportedLanguage.Polish` | Polish — Available UI language entry. | Port actual localized resources, fallback and layout; an enum entry alone is not translation completion. | UI-02 |
| `option.SupportedLanguage.Portuguese` | Portuguese — Available UI language entry. | Port actual localized resources, fallback and layout; an enum entry alone is not translation completion. | UI-02 |
| `option.SupportedLanguage.PortugueseBrazil` | PortugueseBrazil — Available UI language entry. | Port actual localized resources, fallback and layout; an enum entry alone is not translation completion. | UI-02 |
| `option.SupportedLanguage.Romanian` | Romanian — Available UI language entry. | Port actual localized resources, fallback and layout; an enum entry alone is not translation completion. | UI-02 |
| `option.SupportedLanguage.Russian` | Russian — Available UI language entry. | Port actual localized resources, fallback and layout; an enum entry alone is not translation completion. | UI-02 |
| `option.SupportedLanguage.SimplifiedChinese` | SimplifiedChinese — Available UI language entry. | Port actual localized resources, fallback and layout; an enum entry alone is not translation completion. | UI-02 |
| `option.SupportedLanguage.Spanish` | Spanish — Available UI language entry. | Port actual localized resources, fallback and layout; an enum entry alone is not translation completion. | UI-02 |
| `option.SupportedLanguage.TraditionalChinese` | TraditionalChinese — Available UI language entry. | Port actual localized resources, fallback and layout; an enum entry alone is not translation completion. | UI-02 |
| `option.SupportedLanguage.Turkish` | Turkish — Available UI language entry. | Port actual localized resources, fallback and layout; an enum entry alone is not translation completion. | UI-02 |
| `option.SupportedLanguage.Ukrainian` | Ukrainian — Available UI language entry. | Port actual localized resources, fallback and layout; an enum entry alone is not translation completion. | UI-02 |
| `option.SupportedLanguage.Vietnamese` | Vietnamese — Available UI language entry. | Port actual localized resources, fallback and layout; an enum entry alone is not translation completion. | UI-02 |
| `option.CustomUploaderBody.None` | None — Custom HTTP request body representation. | Reuse CustomUploaderItem encoding and fixture each body mode, including no-body requests. | UPL-01 |
| `option.CustomUploaderBody.MultipartFormData` | MultipartFormData — Custom HTTP request body representation. | Reuse CustomUploaderItem encoding and fixture each body mode, including no-body requests. | UPL-01 |
| `option.CustomUploaderBody.FormURLEncoded` | FormURLEncoded — Custom HTTP request body representation. | Reuse CustomUploaderItem encoding and fixture each body mode, including no-body requests. | UPL-01 |
| `option.CustomUploaderBody.JSON` | JSON — Custom HTTP request body representation. | Reuse CustomUploaderItem encoding and fixture each body mode, including no-body requests. | UPL-01 |
| `option.CustomUploaderBody.XML` | XML — Custom HTTP request body representation. | Reuse CustomUploaderItem encoding and fixture each body mode, including no-body requests. | UPL-01 |
| `option.CustomUploaderBody.Binary` | Binary — Custom HTTP request body representation. | Reuse CustomUploaderItem encoding and fixture each body mode, including no-body requests. | UPL-01 |
| `option.CustomUploaderDestinationType.None` | None — Custom uploader role flag. | Preserve numeric flags and allowed multi-role registration. | UPL-01 |
| `option.CustomUploaderDestinationType.ImageUploader` | ImageUploader — Custom uploader role flag. | Preserve numeric flags and allowed multi-role registration. | UPL-01 |
| `option.CustomUploaderDestinationType.TextUploader` | TextUploader — Custom uploader role flag. | Preserve numeric flags and allowed multi-role registration. | UPL-01 |
| `option.CustomUploaderDestinationType.FileUploader` | FileUploader — Custom uploader role flag. | Preserve numeric flags and allowed multi-role registration. | UPL-01 |
| `option.CustomUploaderDestinationType.URLShortener` | URLShortener — Custom uploader role flag. | Preserve numeric flags and allowed multi-role registration. | UPL-01 |
| `option.CustomUploaderDestinationType.URLSharingService` | URLSharingService — Custom uploader role flag. | Preserve numeric flags and allowed multi-role registration. | UPL-01 |
| `option.FTPProtocol.FTP` | FTP — File-transfer protocol choice. | Use FluentFTP for FTP/FTPS and SSH.NET for SFTP with certificate/host-key verification. | UPL-05 |
| `option.FTPProtocol.FTPS` | FTPS — File-transfer protocol choice. | Use FluentFTP for FTP/FTPS and SSH.NET for SFTP with certificate/host-key verification. | UPL-05 |
| `option.FTPProtocol.SFTP` | SFTP — File-transfer protocol choice. | Use FluentFTP for FTP/FTPS and SSH.NET for SFTP with certificate/host-key verification. | UPL-05 |
| `option.LinkFormatEnum.URL` | URL — Output/clipboard link format. | Port the source template and selected original/thumbnail/short/local URL behavior. | UPL-02 |
| `option.LinkFormatEnum.ForumImage` | ForumImage — Output/clipboard link format. | Port the source template and selected original/thumbnail/short/local URL behavior. | UPL-02 |
| `option.LinkFormatEnum.HTMLImage` | HTMLImage — Output/clipboard link format. | Port the source template and selected original/thumbnail/short/local URL behavior. | UPL-02 |
| `option.LinkFormatEnum.WikiImage` | WikiImage — Output/clipboard link format. | Port the source template and selected original/thumbnail/short/local URL behavior. | UPL-02 |
| `option.LinkFormatEnum.ShortenedURL` | ShortenedURL — Output/clipboard link format. | Port the source template and selected original/thumbnail/short/local URL behavior. | UPL-02 |
| `option.LinkFormatEnum.ForumLinkedImage` | ForumLinkedImage — Output/clipboard link format. | Port the source template and selected original/thumbnail/short/local URL behavior. | UPL-02 |
| `option.LinkFormatEnum.HTMLLinkedImage` | HTMLLinkedImage — Output/clipboard link format. | Port the source template and selected original/thumbnail/short/local URL behavior. | UPL-02 |
| `option.LinkFormatEnum.WikiLinkedImage` | WikiLinkedImage — Output/clipboard link format. | Port the source template and selected original/thumbnail/short/local URL behavior. | UPL-02 |
| `option.LinkFormatEnum.ThumbnailURL` | ThumbnailURL — Output/clipboard link format. | Port the source template and selected original/thumbnail/short/local URL behavior. | UPL-02 |
| `option.LinkFormatEnum.LocalFilePath` | LocalFilePath — Output/clipboard link format. | Port the source template and selected original/thumbnail/short/local URL behavior. | UPL-02 |
| `option.LinkFormatEnum.LocalFilePathUri` | LocalFilePathUri — Output/clipboard link format. | Port the source template and selected original/thumbnail/short/local URL behavior. | UPL-02 |
| `option.FFmpegVideoCodec.libx264` | libx264 — Video/animated-image encoder choice. | Discover encoder presence; preserve source parameters and classify unavailable hardware encoders explicitly. | MED-01 to MED-05 |
| `option.FFmpegVideoCodec.libx265` | libx265 — Video/animated-image encoder choice. | Discover encoder presence; preserve source parameters and classify unavailable hardware encoders explicitly. | MED-01 to MED-05 |
| `option.FFmpegVideoCodec.libvpx` | libvpx — Video/animated-image encoder choice. | Discover encoder presence; preserve source parameters and classify unavailable hardware encoders explicitly. | MED-01 to MED-05 |
| `option.FFmpegVideoCodec.libvpx_vp9` | libvpx_vp9 — Video/animated-image encoder choice. | Discover encoder presence; preserve source parameters and classify unavailable hardware encoders explicitly. | MED-01 to MED-05 |
| `option.FFmpegVideoCodec.libxvid` | libxvid — Video/animated-image encoder choice. | Discover encoder presence; preserve source parameters and classify unavailable hardware encoders explicitly. | MED-01 to MED-05 |
| `option.FFmpegVideoCodec.h264_nvenc` | h264_nvenc — Video/animated-image encoder choice. | Discover encoder presence; preserve source parameters and classify unavailable hardware encoders explicitly. | MED-01 to MED-05 |
| `option.FFmpegVideoCodec.hevc_nvenc` | hevc_nvenc — Video/animated-image encoder choice. | Discover encoder presence; preserve source parameters and classify unavailable hardware encoders explicitly. | MED-01 to MED-05 |
| `option.FFmpegVideoCodec.h264_amf` | h264_amf — Video/animated-image encoder choice. | Discover encoder presence; preserve source parameters and classify unavailable hardware encoders explicitly. | MED-01 to MED-05 |
| `option.FFmpegVideoCodec.hevc_amf` | hevc_amf — Video/animated-image encoder choice. | Discover encoder presence; preserve source parameters and classify unavailable hardware encoders explicitly. | MED-01 to MED-05 |
| `option.FFmpegVideoCodec.h264_qsv` | h264_qsv — Video/animated-image encoder choice. | Discover encoder presence; preserve source parameters and classify unavailable hardware encoders explicitly. | MED-01 to MED-05 |
| `option.FFmpegVideoCodec.hevc_qsv` | hevc_qsv — Video/animated-image encoder choice. | Discover encoder presence; preserve source parameters and classify unavailable hardware encoders explicitly. | MED-01 to MED-05 |
| `option.FFmpegVideoCodec.gif` | gif — Video/animated-image encoder choice. | Discover encoder presence; preserve source parameters and classify unavailable hardware encoders explicitly. | MED-01 to MED-05 |
| `option.FFmpegVideoCodec.libwebp` | libwebp — Video/animated-image encoder choice. | Discover encoder presence; preserve source parameters and classify unavailable hardware encoders explicitly. | MED-01 to MED-05 |
| `option.FFmpegVideoCodec.apng` | apng — Video/animated-image encoder choice. | Discover encoder presence; preserve source parameters and classify unavailable hardware encoders explicitly. | MED-01 to MED-05 |
| `option.FFmpegAudioCodec.libvoaacenc` | libvoaacenc — Audio encoder choice. | Discover actual codec implementation, preserve options, and report unsupported legacy encoder identifiers. | MED-01 |
| `option.FFmpegAudioCodec.libopus` | libopus — Audio encoder choice. | Discover actual codec implementation, preserve options, and report unsupported legacy encoder identifiers. | MED-01 |
| `option.FFmpegAudioCodec.libvorbis` | libvorbis — Audio encoder choice. | Discover actual codec implementation, preserve options, and report unsupported legacy encoder identifiers. | MED-01 |
| `option.FFmpegAudioCodec.libmp3lame` | libmp3lame — Audio encoder choice. | Discover actual codec implementation, preserve options, and report unsupported legacy encoder identifiers. | MED-01 |
| `option.ScreenRecordGIFEncoding.FFmpeg` | FFmpeg — GIF encoding implementation choice. | Port corresponding algorithms where portable; differences in quantization require explicit fixtures, not a silently shared implementation. | MED-03 |
| `option.ScreenRecordGIFEncoding.NET` | NET — GIF encoding implementation choice. | Port corresponding algorithms where portable; differences in quantization require explicit fixtures, not a silently shared implementation. | MED-03 |
| `option.ScreenRecordGIFEncoding.OctreeQuantizer` | OctreeQuantizer — GIF encoding implementation choice. | Port corresponding algorithms where portable; differences in quantization require explicit fixtures, not a silently shared implementation. | MED-03 |
| `option.AIProvider.OpenAI` | OpenAI — Image-analysis provider choice. | Port the corresponding source request/configuration and result UI; remote availability/credentials remain separate. | UPL-04 |
| `option.AIProvider.Gemini` | Gemini — Image-analysis provider choice. | Port the corresponding source request/configuration and result UI; remote availability/credentials remain separate. | UPL-04 |
| `option.AIProvider.OpenRouter` | OpenRouter — Image-analysis provider choice. | Port the corresponding source request/configuration and result UI; remote availability/credentials remain separate. | UPL-04 |
| `option.AIProvider.OpenAILegacy` | OpenAILegacy — Image-analysis provider choice. | Port the corresponding source request/configuration and result UI; remote availability/credentials remain separate. | UPL-04 |
