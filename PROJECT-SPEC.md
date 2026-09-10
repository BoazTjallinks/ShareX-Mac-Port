# ShareX for macOS — implementation specification

Prepared for Boaz Tjallinks, 10 September 2026. Personal, locally installed application. This package designs the port; it does not contain a working macOS application.

## 1. Product contract

Build a faithful macOS port of **ShareX 21.0.0**, preserving its commands, configurable workflows, tools, destinations, editor behavior, settings, and recognizable interface. Do not turn it into a smaller screenshot app. Development milestones are delivery stages, not permission to remove the remaining scope.

The source of truth is `ShareX/ShareX` tag `v21.0.0`, commit `d2502561f63fc3ff502cacd91514e3f7f2948c74`, published 3 July 2026. It was the latest stable release returned by GitHub during this research. The release, commit, archive hash, and tracked-file hashes are pinned in this package. Later upstream changes must be evaluated separately.

“1:1” means the same available operation, input, options, state transition, and user-visible result wherever macOS provides the required capability. Exact Windows binary execution, every operating-system interaction, and identical encoded bytes are not possible across platforms. Track unavoidable differences separately; never label a disabled control, untested endpoint, or alternate algorithm as exact parity. The user has requested fidelity, not a redesign.

The final product has no mandatory account, hosted server, web dashboard, Docker dependency, subscription layer, telemetry, or product AI assistant. Existing ShareX upload and Analyze Image features may contact providers when the user configures and invokes them. Claude Code is the development tool, not an application dependency.

## 2. Architecture decision

Use **C# on .NET 10 LTS + Avalonia + SkiaSharp**, with a **Swift native component behind an Objective-C/C ABI** for macOS functions. Keep the application and its backend local and primarily in one process. Use child processes for FFmpeg and user-configured external actions.

This recommendation follows the actual release source, rather than a generic preference for native Swift. The main application targets `net9.0-windows10.0.22621.0` and uses Windows Forms. However, `ShareX.ImageEditor` already targets `net9.0` and uses Avalonia, SkiaSharp, and MVVM. Reusing this editor and C# algorithms reduces the amount of behavior that must be recreated.

The editor is **not already completely portable**. Its package references and implementation still include DirectML, Vortice/DirectX, Windows emoji and cursor renderers, and a Windows screen-color picker. Its standalone host also targets Windows. These must be separated or replaced before the editor can run correctly on macOS. `ShareX.UploadersLib` also uses Windows Forms; it cannot simply be referenced unchanged.

| Layer | Proposed implementation | Reason and reuse boundary |
| --- | --- | --- |
| Main UI, settings, history, task dialogs | Avalonia desktop, C#, MVVM | Rebuild WinForms views while preserving grouping, order, labels, defaults, commands, and interactions. |
| New image editor | Port existing `ShareX.ImageEditor` | Retain annotation models, undo/redo, effect algorithms, parameter definitions, view models, and XAML where portable. |
| Legacy editor and capture annotations | C# state/geometry + Avalonia/Skia drawing | Preserve the legacy selection, toolbar, input rules, and tools; do not replace this mode with the new editor and call it done. |
| Workflow engine | C# library, asynchronous tasks and cancellation | Extract behavior from `WorkerTask`, `TaskManager`, `TaskHelpers`, `TaskSettings`, and `UploadManager`. |
| Image model and rendering | SkiaSharp buffers and explicit pixel metadata | Replace GDI+ `Bitmap`, `Graphics`, and Windows-specific resource handling at the boundaries. |
| Screen and window capture | Swift, ScreenCaptureKit, CoreGraphics, AppKit | Capture permissions, display/window identities, still captures, streams, geometry, and overlays. |
| Recording and audio | ScreenCaptureKit + AVFoundation; FFmpeg for compatibility encoding | Native media timing and device access; retain selectable output workflows and encoder options where supported. |
| Uploading | Extracted C# HTTP adapters, FluentFTP, SSH.NET | Keep provider request construction, custom-uploader parsing, signing, and response mapping. Replace UI, icons, credential storage, and platform calls. |
| Settings and presets | Newtonsoft.Json with explicit compatibility converters | Match upstream JSON names, enums, flags, defaults, and approved serialized types. |
| History | Microsoft.Data.Sqlite | Reuse the upstream history schema and support legacy JSON/XML import. |
| Credentials | macOS Keychain through native bridge | Replace Windows DPAPI; settings contain references rather than plaintext secrets. |
| OCR | Apple Vision via native bridge | Local OCR with equivalent workflow; engine/language/output differences are an explicit exception. |
| QR code | ZXing.Net using a portable pixel adapter | Preserve generation/decoding options; remove Windows compatibility bindings. |
| Background removal | Portable ONNX Runtime, same model and preprocessing | CPU baseline; CoreML only when the installed arm64 runtime actually exposes it and fixtures pass. |
| macOS integration | Native bridge + Avalonia native menus | Global hotkeys, permission state, login item, Finder reveal, printing, notifications, clipboard, and Services. |

Sources: [U1], [U2], [U3], [M1], [M2], [M3], [A1], [A2], [O1] in `docs/SOURCES.md`. Architecture and decomposition are engineering recommendations, not upstream claims.

### Alternatives considered

| Alternative | Assessment for this request |
| --- | --- |
| Entire application in Swift/AppKit | Excellent system integration, but requires reimplementing the editor, parsers, provider adapters, effects, and settings. Higher behavioral drift risk for a 1:1 port. |
| C# Avalonia with .NET macOS bindings | Viable alternative to the Swift bridge. If an initial spike proves the selected SDK exposes every required ScreenCaptureKit/audio API cleanly, record an ADR and prefer the smaller proven integration. Do not maintain two native integration stacks. |
| Electron or Tauri | Still requires native capture and system integration, while replacing the existing editor and C# behavior with another stack. No clear benefit for fidelity. |
| WinForms via compatibility software | Does not produce the requested macOS port and does not solve Windows capture API dependencies. |
| Paid WinForms/WPF compatibility frameworks | Not the baseline. Avoid introducing commercial licenses for this personal project. |

### Platform and dependency baseline

Target the user's **Apple Silicon laptop**, runtime identifier `osx-arm64`, with proposed deployment minimum **macOS 15**. Confirm `sw_vers`, architecture, Xcode, Swift, and installed SDKs during setup. This is a design baseline, not a claim about the currently installed macOS version. Intel support is not a first-release gate for this personal laptop; keep portable code clean, but do not promise an untested universal build.

Upstream pins Avalonia **12.0.5**, SkiaSharp **3.119.4**, Newtonsoft.Json **13.0.4**, CommunityToolkit.Mvvm **8.4.2**, FluentFTP **54.2.0**, and SSH.NET **2025.1.0**. Treat these as source-baseline versions, not a blanket recommendation to keep old patches. Start with compatible versions, check official package support/advisories at implementation time, and pin the selected working set. Use .NET 10's current supported patch and the actual installed SDK version in `global.json`; a runtime patch number is not an SDK version.

Remove unconditional Windows RIDs inherited from `Directory.build.props`. Do not copy the upstream build props into the new solution without isolating Windows targets. Initially keep JIT and disable trimming; upstream reflection discovery and JSON compatibility make trimming/AOT premature. Core libraries target plain `net10.0` and contain no WinForms, GDI+, Win32, Windows.Media.Ocr, Registry, or DPAPI runtime dependencies.

## 3. Source and repository layout

The package includes the complete tracked upstream tree as a compressed source archive. It excludes Git history and downloaded third-party binaries. `scripts/restore-upstream.py` verifies and extracts it into `reference/ShareX` without network access or overwriting an existing tree. That tree stays read-only by project convention; forked implementation files belong under `src/`. It is an audit reference, not a second application that should accidentally be built with the port.

The future solution should contain these projects:

| Path | Ownership and purpose |
| --- | --- |
| `src/ShareX.Mac.App` | Avalonia startup, windows, menus, composition root, view models. |
| `src/ShareX.Core` | Task models, settings snapshots, workflow runner, command registry, capability model. |
| `src/ShareX.Compatibility` | Legacy settings/preset/history/CLI import, enum mappings, safe type binder. |
| `src/ShareX.Imaging` | Portable image abstraction, codecs, legacy effects, compositing. |
| `src/ShareX.Editor` | Attributed fork of the upstream Avalonia editor with macOS service injection. |
| `src/ShareX.Uploaders` | Portable uploader implementations and provider configuration models. |
| `src/ShareX.Platform.Mac` | C# adapters and P/Invoke bindings; owns native SafeHandles and callback lifetimes. |
| `native/ShareXMacNative` | Swift implementation and Objective-C/C exports, built by Xcode. |
| `src/ShareX.Tools` | Portable tools, media orchestration, indexing, hashes, metadata. |
| `src/ShareX.Cli` | Command argument compatibility and authenticated local forwarding to running app. |
| `src/ShareX.NativeMessagingHost` | Browser framing and forwarding; no screen capture permission identity of its own. |
| `tests/ShareX.Core.Tests` | Workflow order, settings inheritance, filename/parser and history tests. |
| `tests/ShareX.Compatibility.Tests` | Sanitized Windows fixtures, effects and uploader differential tests. |
| `tests/ShareX.Mac.IntegrationTests` | Real macOS capture, permissions, clipboard, audio, lifecycle. |
| `tests/ShareX.UI.Tests` | Main UI and editor interactions; actual app bundle accessibility checks. |
| `scripts/` | Repeatable build, package, inventory, and verification commands. |
| `planning/` | Parity ledger, progress, decisions, evidence, and unresolved differences. |

Reuse original code with attribution. Do not treat a namespace such as `System.Windows.Input` alone as proof of a Windows dependency; inspect its referenced assembly and actual runtime behavior. Conversely, a plain `net9.0` target does not prove platform independence.

## 4. Native boundary and process model

Avalonia owns the application's main event loop. The bridge attaches to the existing application and dispatches AppKit work to the main thread. It must not start a second `NSApplication` loop or synchronously block the UI while waiting for an asynchronous callback.

Expose a small, versioned C ABI from an Objective-C shim around Swift. Use fixed-width integers, opaque handles, UTF-8, byte lengths, explicit error codes, and explicit release functions. Do not P/Invoke Swift-mangled symbols, marshal Swift objects, or pass exceptions across the boundary. Use supported C exports rather than making an underscored Swift export attribute an undocumented requirement.

Proposed operations: query capabilities/permissions; enumerate displays and windows; begin/cancel screenshot; begin/pause/resume/stop/abort recording; register/unregister hotkey; read/write Keychain item; run OCR; reveal/print file; manage pin panel; inspect accessible element; request a user-authorized scroll action. UI-owned Avalonia operations should remain in Avalonia rather than being duplicated natively.

Requests have IDs and complete at most once with either a result, cancellation, or structured failure. Events may arrive on native queues; copy event data before returning and marshal view updates to the UI thread. Keep delegates rooted until unregister acknowledgement, and stop callbacks before releasing handles. Cancellation must be idempotent. Report SDK availability and permission failures as distinct conditions.

Images use an immutable artifact descriptor: dimensions, row stride, pixel format, alpha convention, color space/profile, scale, display geometry, capture timestamp, and content storage. For ordinary screenshots, a PNG or bounded shared buffer can cross the boundary. High-frequency video frames stay native; JSON and repeated managed allocations must not carry the recording stream.

Record in the main app's identity so users grant permission to the actual application. Native messaging and CLI clients forward requests through a private Unix-domain socket owned by the user, with restricted filesystem permissions and a per-session token. The normal app has no HTTP listener. OAuth callbacks are temporary loopback listeners tied to one authorization attempt, state/PKCE, a short timeout, and immediate shutdown.

## 5. Capture and geometry

Implement all capture entry points in `docs/FEATURE-CATALOGUE.md`: full desktop, chosen monitor, active monitor, chosen window, active window, custom window match, region, light region, transparent region, remembered region, configured region, scrolling capture, scheduled capture, and every recording variant.

Use ScreenCaptureKit's shareable content, filters, screenshot APIs, and streams as appropriate to the supported SDK. Select the intended foreground window **before** presenting an overlay or activating ShareX. Store stable display identity and current geometry; window numbers may expire. Exclude ShareX overlays and recording controls through the capture filter rather than racing window hide/show calls where possible.

Coordinate conversion is a named subsystem. AppKit points, capture pixel rectangles, CoreGraphics display coordinates, and Avalonia device-independent pixels must never be mixed implicitly. Handle nonzero and negative monitor origins, Retina/non-Retina combinations, rotated screens, fractional UI scaling, and display hot-plug. For desktop captures spanning mixed-scale screens, explicitly define a composition scale and preserve per-display transforms; there is no universal single physical pixel scale for the virtual desktop. Test boundary rounding with one-pixel fixtures.

The selection overlay preserves rectangle/ellipse/freehand masks, resize handles, magnifier, pixel color readout, dimming, snap sizes, fixed dimensions, last region, keyboard nudging, tool switching, pointer buttons, region annotations, confirmation, and cancellation. Maintain distinct default/light/transparent interactions from the release source. “Transparent region” is an interaction mode and is not a promise that arbitrary content underneath a third-party window can be reconstructed.

For window shadows/transparency, use supported filter/configuration properties and document the exact result. Windows' black/white repaint trick, child HWND enumeration, client-area geometry, and desktop/taskbar manipulation do not map directly to macOS. Accessibility can supply exposed element geometry; custom-rendered controls may not expose anything useful. Show permission or support state instead of fabricating selection targets.

Scrolling capture is a separate pipeline: select a target; capture an initial viewport; advance with a permission-checked accessibility action or scroll event; wait for settling; capture overlap; estimate translation; reject poor matches; remove repeated fixed content when detected; stitch; preview; allow manual adjustment. Preserve configurable delays, offsets, attempts, crop controls, duplicate/end detection, and partial-result handling. Cap height, memory, iteration count, and duration. Keep the target stable; stop on focus loss or major content changes. A browser-specific full-page capture path may supplement this but does not replace general scrolling capture. No universal scrolling guarantee is possible for every application.

Auto capture reuses the same dispatcher and task snapshot. Preserve region/window choice, interval, destination, start/stop commands, and upstream timing behavior where observed. Prevent accidental overlapping jobs and upload loops. On display removal or permission revocation, stop with a visible explanation.

## 6. Recording, GIF, codecs, and media tools

ScreenCaptureKit supplies screen, system audio, and—on the chosen supported API surface—microphone samples. Use AVFoundation for muxing/encoding and media timing. The output choices and settings remain ShareX-like: frame rate, region/window, cursor, start delay, automatic start, fixed duration, pause/resume, stop, abort, codec, bitrate/quality, GIF palette/dither, and custom FFmpeg options.

Support three explicit paths: native H.264/HEVC recording for routine use; a quality-preserving temporary capture followed by FFmpeg for additional formats/options; and a validated advanced FFmpeg path when the user's requested options require it. The phase-zero spike must select a proven lossless capture/intermediate route for GIF or pixel-sensitive output. Do not silently feed GIF generation with a lossy H.264 intermediate. If a high-volume intermediate is needed, estimate storage first, stream to disk, and clean it after successful completion. An unavailable codec remains unavailable, not a silently substituted encoder.

Keep video and all audio on a common timeline. Pause excludes the paused interval for both media types. Handle different audio sample rates and channels, synchronize microphone/system-audio starts, avoid double playback feedback, and preserve timestamp monotonicity. Constrain buffering and honor encoder backpressure. Stop should flush and finalize exactly once; abort follows the configured confirmation behavior and explicitly disposes of only this job's temporary output. On failure, preserve recoverable captures and report their location.

Use FFmpeg/ffprobe as pinned, known local tools. Discover actual encoder support, rather than assuming a codec is compiled in. C# `ProcessStartInfo.ArgumentList` builds argument vectors with no shell interpolation. Support paths containing spaces, quotes, Unicode, and leading hyphens safely. Never send a screenshot path through `sh -c`. The source's `gdigrab`, DirectShow, NVENC, AMF, and Quick Sync options are retained during import and reported as incompatible when absent. VideoToolbox is a named macOS alternative, not equivalent hardware-preset semantics.

The converter and video thumbnailer retain their own source options, output naming, sizing, timestamps, grids, quality controls, and cancellation. Include GIF, APNG, and animated WebP when corresponding encoders are available. FFmpeg and ONNX binaries/models are not included in this design package; implementation must record origin, exact versions, hashes, licenses, and supported architectures.

## 7. Editor and effects

Preserve both the default Avalonia editor and the user-selectable legacy editor. The new editor has 20 `EditorTool` values and **232 source-defined effect IDs** in the audited release. The legacy effect library has **51 concrete effect files**, and region capture has 26 shape/tool enum entries. Every entry appears in the inventories; do not collapse these to “basic editing.”

Reuse portable annotation/effect algorithms directly. Replace Windows cursor/emoji rendering with an injectable renderer. A native macOS glyph fallback can differ visually; record this. Preserve existing redistributable bundled assets and font licenses. Do not extract proprietary Windows fonts or icon resources from the operating system.

Support selection, z-order, move/resize/rotate where present, undo/redo, zoom/pan, clipboard insert, canvas and image sizing, crop/cut-out, export, continue-after-capture, cancellation, print, save/save-as, pin, and upload. Preserve each toolbar's actions and options, including text, speech balloon, numbered steps, arrows/freehand arrows, image insertion from file/screen, stickers/emoji, cursor, highlight, smart eraser, blur, pixelation, magnification, and spotlight in their respective modes.

Effect parameter names, defaults, bounds, ordering, seed behavior, and serialized IDs are contracts. The effect catalogue links every type to the original implementation. For deterministic Skia effects, compare decoded image buffers using the same color space, alpha rules, library versions, and seeds. Text rasterization and platform-dependent rendering need documented tolerances and visual evidence. A saved PNG is a flattened output; any private editable document format must be clearly distinguished from ShareX-compatible export. It is not required to invent a new project format to finish the port.

Background removal keeps the same supported ONNX model files, preprocessing, normalization, tensor layouts, resizing, and mask postprocessing. Replace DirectML adapter enumeration and execution-provider selection. CPU operation is mandatory. Optional CoreML acceleration only earns parity after model/provider compatibility and output tolerance checks. A model download requires an explicit user action and displays its source and size. Using Apple's unrelated segmentation model as a silent replacement would change results.

## 8. Workflow semantics

After-capture options are flags, **not an arbitrary reorderable workflow graph**. Preserve upstream ordering and dependencies. The menu order alone is not the execution order. For example, the source performs beautification/effects/annotation before copying the image; file creation can occur before external actions; copying a file takes precedence over copying its path, which takes precedence over copying the folder path. OCR is invoked later in `Prepare`, and final deletion appears in a separate task section. Characterize these branches from the pinned source rather than implementing a guessed linear list.

All 22 nonzero after-capture flags and 6 nonzero after-upload flags are in scope. Dialog-based stages can suspend or cancel a task. An upload may require an encoded artifact even when permanent Save Image is disabled. Applying an external action that replaces a file must update the stream and filename before subsequent operations. Handle independent image, text, file, URL-shortening, URL-sharing, download, and download-then-upload jobs.

Resolve inherited default settings plus workflow-specific overrides into an immutable task snapshot before execution. Later settings edits cannot mutate a running upload. Preserve separate override groups: after-capture, after-upload, destinations, general, image, capture, upload, actions, tools, advanced, screenshot path, FTP/custom uploader index, and watch folders.

The runner records state and outcome per stage, supports queue cancellation, bounded concurrency, retry policy, and secondary-upload destinations. Do not restart an entire completed chain after a network error. A timeout after sending an upload has an unknown remote result; only retry automatically when the adapter's semantics justify it. Avoid duplicate uploads and duplicate external actions. Actual upstream failure/deletion semantics must be recorded; if protecting a user's only file requires an intentional deviation, document it and keep that exception out of exact-parity claims.

## 9. Uploads and custom uploaders

Preserve the 74 registered uploader services and both enum-level “use file uploader” routes. Include all configured providers, URL shorteners, URL-sharing services, SMTP email, shared folders, FTP, FTPS, SFTP, and HTTP-based storage. `docs/DESTINATIONS.md` gives a row and source file for each. Provider presence in source is not proof that its remote service still operates.

Extract service metadata/configuration away from WinForms `TabPage`, `Icon`, and dialog dependencies. Keep request/authentication/result logic where portable. Preserve provider-specific options such as folder/bucket/album, visibility, region/endpoint, path style, link format, deletion token, chunking, overwrite behavior, and sharing permissions. Retain credential references and the provider's original identity during migration.

Custom uploader support is an essential compatibility feature. Preserve `.sxcu`, destination flags, HTTP methods, URL and query parameters, headers, six body modes, form field name, content data, and output URL/thumbnail/deletion/error templates. Reuse `ShareXSyntaxParser`, `ShareXCustomUploaderSyntaxParser`, `CustomUploaderItem`, and every concrete custom function. Preserve escaping, nested functions, case-insensitive dispatch, aliases, parameter validation, JSONPath, XML, regex, base64, input/output boxes, header selection, random selection, filename, input, response, and response URL behavior. Do not replace this grammar with simple string substitution.

Retain Newtonsoft.Json semantics where needed, but do not permit arbitrary type construction from untrusted JSON. Resolve `.sxie` effect types through an explicit allowlist and legacy-name mapping. Regex evaluation needs bounded execution; XML parsing must not resolve external entities. These restrictions are compatibility/security design choices; identify any affected unsupported expression rather than falsely succeeding.

Use deterministic local HTTP fixtures first: compare semantic request method, path, headers, query encoding, form fields, bytes, and parsed result with Windows output. Multipart boundaries and timestamps may be normalized; payload bytes may not. Live provider checks require the user's test account and synthetic files. Never reuse upstream developer credentials or assume public client secrets grant authorization for a different app registration. Browser authorization, scopes, redirect URI registration, and token renewal are adapter-specific.

Remote availability has its own status. Firebase Dynamic Links is a concrete example: its source enum and adapter exist, but Google documents shutdown on 25 August 2025. Keep imported configuration understandable and report the provider as unavailable; do not claim to make the retired service work. Other providers remain `live_unverified` until individually checked. A provider's documented API change is not a macOS limitation and needs its own compatibility decision.

## 10. Settings, history, and migration

Store application data beneath `~/Library/Application Support/ShareX-Mac/`; use `~/Library/Caches/ShareX-Mac/` for expendable cache and a user-selected screenshots directory, initially `~/Pictures/ShareX/`. Ask through normal macOS file dialogs when access is needed. Honor a configured portable-data directory if writable, but keep Keychain credentials machine/user-bound and describe that difference.

Preserve the source settings filenames and structure through an explicit import/export layer: `ApplicationConfig.json`, `UploadersConfig.json`, `HotkeysConfig.json`, `.sxcu`, `.sxie`, ShareX backup archives, and history formats. Preserve unknown fields when safe. Incomplete import returns a per-field report containing preserved, translated, credential-required, unsupported, and invalid values. Never silently reset an entire import because one Windows-only field exists.

`inventory/upstream-audit.json` contains 926 lexically discovered public configuration-field/property candidates with original declarations and source locations. This is deliberately called a candidate index: properties can be transient, nested, or ignored by the serializer. Before final parity sign-off, use Roslyn/reflection and source UI bindings to reconcile the real serialized and editable settings schema. Build typed editors for real settings; a raw JSON text area is not equivalent to ShareX's settings UI.

Windows DPAPI-encrypted values cannot generally be decrypted on this Mac. Support a deliberate plaintext export from the user's Windows context where the source provides it, or ask the user to reauthorize the relevant provider. Never promise magic migration of protected tokens. New local credentials go to Keychain; exports omit secrets by default and require an explicit secret-inclusive choice when needed. Do not expose secrets in examples, fixtures, diagnostics, or commits.

Use the existing SQLite `History` columns: Id, FileName, FilePath, DateTime, Type, Host, URL, ThumbnailURL, DeletionURL, ShortenedURL, Tags. Keep imported dates/URLs/tags, separate the original Windows path from any user-selected local remapping, and tolerate missing files. Import older JSON/XML through source-aware converters. Version any additional schema separately and do not destructively alter the user's original database. Back up before migration and make restoring the previous application-data directory sufficient for rollback.

File patterns retain `%` tokens, random/counter behavior, window/process metadata, timezone handling, truncation, and collision policies. Convert drive-letter/UNC paths only through explicit mappings. Retain original `Keys` values for Windows settings alongside the macOS shortcut binding; do not reinterpret integer bitmasks as Mac virtual key codes.

## 11. UI parity

Keep ShareX's main command groups and functional density: Capture, Upload, Workflows, Tools, after-capture/after-upload choices, destinations, task/application/hotkey settings, history/image history, and the actions toolbar. Match the pinned WinForms layout and resources for the main app and the pinned XAML for the new editor. Match themes, menu order, labels, option visibility, defaults, tooltip/help content, and shortcut display wherever possible.

Native macOS title bars, system dialogs, menu-bar placement, permission prompts, and equivalent Cmd shortcuts are explicitly allowed platform adaptations. Preserve original shortcuts as a selectable compatibility profile when registerable; provide editable Mac defaults and report conflicts. Do not hijack macOS screenshot shortcuts or mutate system preferences automatically. A MacBook has no built-in Print Screen key, so the command identity and its physical binding are separate.

Implement the menu-bar icon, queue progress, recent items, configurable click actions where the input device exposes them, close-to-background behavior, and explicit Quit. Pin windows must support upstream opacity/zoom/click-through/close options and work across Spaces as far as macOS allows. ShareX can control its own floating panels; that does not imply it can make another application's window always on top.

Inventory English strings and migrate upstream localized resources, including Dutch. Preserve all supported-language entries in the coverage register and qualify any incomplete translation. No localization-complete claim based solely on an enum. Native language dialogs may follow the OS. Use accessibility names and keyboard navigation for every actionable control, and validate editor focus and text entry with macOS input methods.

The implementation must collect Windows reference screenshots and interaction traces from the pinned release where available. This design was source-audited on Linux; neither Windows nor macOS GUI behavior was executed here. Source-derived expected behavior is not the same evidence as a runtime comparison.

## 12. Packaging and local installation

Produce a conventional self-contained `ShareX-Mac.app` with stable bundle identifier such as `com.tjallinks.sharexmac`, valid Info.plist, icon assets with preserved licenses, native libraries, .NET runtime, and any explicitly bundled helpers. The exact identifier is a proposed local default and may be changed before permission testing; keep it stable afterward.

Run development permission tests from the real app bundle, not only `dotnet run`. Prefer a stable local signing identity when available. Ad-hoc local signing can be used for initial development, but rebuild/signature/path changes may cause renewed permission prompts; promise no automatic TCC persistence. A paid Developer ID/notarization and public update service are not prerequisites for this personal build. If later distribution is requested, handle it as a separate task.

The default application is not Mac App Store sandboxed because full workflow, arbitrary external actions, and file integration are central to the request. This does not remove macOS privacy controls. Package/sign nested binaries correctly, inspect arm64 architecture and load paths, and validate launch after moving the bundle to the intended stable location. Do not disable Gatekeeper, SIP, TCC, or other OS protections.

Provide `build-mac`, `test`, `package-mac`, and `smoke-mac` scripts after discovering the selected toolchain. They should use a repo-local output directory and normal exit codes. Document required Xcode/.NET installation separately; do not run arbitrary installers or `curl | sh`. Automatic updates remain visibly unavailable for the local port until an update source exists; an upstream update notice must never download a Windows installer as a Mac update.

## 13. Completion and evidence

Read `docs/BUILD-ROADMAP.md` for staged implementation and `docs/PARITY-TEST-PLAN.md` for checks. There are two different completion claims:

1. **Faithful macOS port with documented differences:** all feasible in-scope operations implemented and verified; every OS or external-service exception recorded; no hidden omissions.
2. **Literal complete 1:1 clone:** not a defensible claim while those differences exist. Do not report it.

Maintain counts for implemented, automated-tested, Mac-manually-tested, Windows-reference-checked, platform-blocked, external-unavailable, credentials-needed, and unverified. Preserve the original denominator. A button, stub, passing build, or skipped test does not count as feature parity. No elapsed-time or single-prompt completion guarantee is made; this is a substantial multi-stage engineering project.

The practical first working milestone is capture region → annotate → copy and save, launched by a global hotkey from a stable Mac app bundle. Continue from there to the complete catalogue. The goal remains the full port.
