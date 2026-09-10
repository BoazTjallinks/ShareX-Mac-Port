# Explicit parity limits

These are constraints or risks to validate, not permission to remove features quietly. API assessments are based on public macOS capabilities and inspected Windows source; test the actual supported SDK and OS. References are in `SOURCES.md`.

| ID | Windows behavior / dependency | macOS treatment | Classification |
| --- | --- | --- | --- |
| MAC-01 | Windows Forms, GDI+, Win32 handles | Rebuild UI in Avalonia and use Skia/native services. A new target framework alone is insufficient. | Required port work |
| MAC-02 | Global screenshot access without macOS TCC flow | Request Screen Recording permission through supported mechanisms. Handle denial/revocation and OS-controlled reminders. | Unavoidable OS flow |
| MAC-03 | Win32 child-control detection and client-area bounds | Use exposed Accessibility geometry where available; custom-rendered or protected controls may be unavailable. | Conditional capability |
| MAC-04 | Remove borders/title bar of arbitrary third-party window | No equivalent general public cross-process style mutation. Keep feature status explanatory. A screenshot proxy is not the original window. | Platform-blocked for arbitrary windows |
| MAC-05 | Make arbitrary active third-party window topmost | Own pin panels can float; raising another app is not a persistent level change. Do not pretend these are equivalent. | Platform-blocked for arbitrary windows |
| MAC-06 | Win32 transparency repaint tricks and exact Windows shadows | Supported macOS window capture can differ in alpha, shadows and decorations. Characterize actual result. | Capture-rendering difference |
| MAC-07 | Auto-hide Windows taskbar and desktop icons | Dock/menu-bar/desktop-filter behavior is a separate Mac adaptation; avoid globally changing Dock/Finder state to imitate Windows. | OS-specific mapping |
| MAC-08 | Print Screen and Windows virtual-key values | Editable Mac bindings, preserved original command IDs, conflict detection. Do not reinterpret key integers. | Input mapping |
| MAC-09 | Windows Media OCR and installed OCR languages | Apple Vision engine/language list. Same tool workflow, potentially different recognition output. | Algorithm/OS difference |
| MAC-10 | DirectML/DXGI background-removal devices | Portable ONNX CPU baseline; CoreML when supported and tested with the same model. | Hardware/provider mapping |
| MAC-11 | Windows emoji/font/cursor rendering | Portable or native rendering with licensed assets. Glyph shape and metrics may differ. | Rendering difference |
| MAC-12 | gdigrab/DirectShow, NVENC/AMF/QSV encoders | Native capture and available software/VideoToolbox encoders; show original incompatible options during import. | Hardware/API mapping |
| MAC-13 | Arbitrary protected content, secure desktops, hidden/offscreen windows | Respect OS filtering and DRM. Do not claim that screen-capture permission overrides protection. | Platform/content restriction |
| MAC-14 | Scrolling any arbitrary application | Best-effort capture with target permission, overlap detection, preview and partial-result reporting. | Target-dependent behavior |
| MAC-15 | Windows Registry startup, Explorer shell actions and named pipes | Login item/Services or Finder action, file associations, browser manifest files, private Unix socket. | OS integration mapping |
| MAC-16 | Windows DPAPI-encrypted secrets | Reauthorize or use an explicit user export from the original Windows context. Store local credentials in Keychain. | Credential migration limit |
| MAC-17 | Windows paths, UNC mounts, process metadata and case rules | Preserve originals, require mappings, use Mac metadata and mounted-volume paths. | Data mapping |
| MAC-18 | Windows setup/portable/Steam/Store installers and auto-updater | Local Mac bundle and local rebuild/install. Preserve informative update state; never run Windows update payloads. | Distribution scope adaptation |
| MAC-19 | Windows toast UI, taskbar progress and tray button gestures | Native notifications or an app-owned preview; Dock/menu-bar progress; supported pointer gestures. | UI adaptation |
| MAC-20 | Exact Windows pixels for cross-platform text/encoded media | Reuse algorithms; compare decoded results and documented tolerances. Do not assert universal byte equality. | Output representation difference |
| EXT-01 | Firebase Dynamic Links adapter | Upstream registration retained, but Google documents shutdown on 25 August 2025. Clearly unavailable. | External service discontinued |
| EXT-02 | Other provider accounts/APIs/OAuth registrations | Implement/source-map adapter; validate live operation separately using authorized synthetic data. | External availability/credentials |

Import must preserve intent and explain the mapping. Disabled feature controls need a visible reason. A missing implementation is `unimplemented`, not `platform-blocked`; require source/API evidence for that classification. Record any user-approved intentional deviation with the affected feature IDs, rationale, original behavior and replacement behavior.

A final claim should read “ShareX 21.0.0 port for macOS, with the following documented differences,” followed by the actual list. Calling it an exact complete clone despite platform-blocked features would be misleading.
