# Research sources and provenance

Retrieved/inspected 10 September 2026. Only official documentation and upstream source were used for technical recommendations. The repository was actually cloned and inspected. Web retrieval alone was not used to infer its dependency graph.

## Pinned upstream source

| ID | Source | Evidence used |
| --- | --- | --- |
| U1 | [ShareX official repository](https://github.com/ShareX/ShareX) | Source ownership and repository. |
| U2 | [ShareX 21.0.0 release](https://github.com/ShareX/ShareX/releases/tag/v21.0.0) | Latest stable returned by the release API at research time; publication 3 July 2026. |
| U3 | [Pinned source tree](https://github.com/ShareX/ShareX/tree/d2502561f63fc3ff502cacd91514e3f7f2948c74) | Actual code, project files, settings, tools, workflows, provider registrations, effects and original licenses. |
| U4 | [Official feature overview](https://getsharex.com/) | Cross-check of public feature families; exact inventory comes from source. |
| U5 | [Custom uploader documentation](https://getsharex.com/docs/custom-uploader) | Syntax/body-mode context; exact semantics taken from C# parser implementations. |
| U6 | [ShareX documentation repository](https://github.com/ShareX/sharex.github.io) | Official site provenance; do not confuse similarly named third-party domains with the official site. |

The source archive is the `git archive` output of the pinned commit, not a copy of a moving branch. `upstream.lock.json` contains hashes and the source manifest has one hash per tracked file. It includes 3,971 tracked files. The archive is source only: no Git history, FFmpeg build, model weights or complete restored NuGet dependency cache.

The source contains license notices with differing wording in some file headers and the root license file. Preserve them exactly rather than rewriting their terms. Read `vendor/SHAREX-LICENSE.txt` and the original file notices. This is source reuse, not an assertion that personal use erases upstream attribution. Third-party assets/packages retain their own licenses. No public publishing is included in this project.

## Microsoft and Avalonia

| ID | Source | Evidence used |
| --- | --- | --- |
| M1 | [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core) | .NET 10 is LTS; .NET 9 is approaching end of support. Select and pin a supported SDK at implementation time. |
| M2 | [System.Drawing.Common Windows-only support](https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/6.0/system-drawing-common-windows-only) | GDI+ code cannot be treated as supported portable .NET image code. |
| M3 | [Avalonia macOS integration](https://docs.avaloniaui.net/docs/platform-specific-guides/macos) | Native backend, standard versus macOS TFM, native menus, application identity and bundle-based development. |
| M4 | [.NET P/Invoke](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/pinvoke) | C ABI interop approach. |
| M5 | [Native interop best practices](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/best-practices) | Ownership, marshalling and safe-handle design. |

## Apple and media

| ID | Source | Evidence used |
| --- | --- | --- |
| A1 | [ScreenCaptureKit](https://developer.apple.com/documentation/screencapturekit) | Capture framework and available API families. |
| A2 | [Capture HDR content with ScreenCaptureKit — WWDC24](https://developer.apple.com/videos/play/wwdc2024/10088/) | Screenshots, screen/system/microphone outputs and recording improvements. |
| A3 | [Capturing screen content in macOS](https://developer.apple.com/documentation/screencapturekit/capturing-screen-content-in-macos) | Official sample entry point. |
| A4 | [Recognizing text in images](https://developer.apple.com/documentation/vision/recognizing-text-in-images) | Local Vision OCR entry point; SDK-specific language support must be queried. |
| A5 | [AXUIElement](https://developer.apple.com/documentation/applicationservices/axuielement_h) | Accessibility object model, distinct from arbitrary Win32 window manipulation. |
| A6 | [CGPreflightScreenCaptureAccess](https://developer.apple.com/documentation/coregraphics/cgpreflightscreencaptureaccess()) | Capture authorization check. |
| A7 | [CGRequestScreenCaptureAccess](https://developer.apple.com/documentation/coregraphics/cgrequestscreencaptureaccess()) | User-controlled authorization request. |
| A8 | [SMAppService](https://developer.apple.com/documentation/servicemanagement/smappservice) | Login-item registration API. |
| A9 | [Keychain services](https://developer.apple.com/documentation/security/keychain-services) | Native credential store. |
| O1 | [ONNX Runtime CoreML provider](https://onnxruntime.ai/docs/execution-providers/CoreML-ExecutionProvider.html) | Provider support depends on build/package and supported operators. Do not infer C# package capability from a Python package alone. |
| F1 | [FFmpeg filter documentation](https://ffmpeg.org/ffmpeg-filters.html) | Palette generation/use and media filter mechanisms. Encoder availability remains a runtime-build property. |

Some Apple reference pages are rendered dynamically and returned only their page shell through text retrieval. Their links are provided for SDK verification, not as evidence that every signature or minimum version was tested here. The WWDC transcript and upstream source supplied the substantive capture design context. Public-API mapping and the proposed architecture are engineering analysis.

## Browser and external provider lifecycle

| ID | Source | Evidence used |
| --- | --- | --- |
| B1 | [Chrome native messaging](https://developer.chrome.com/docs/extensions/develop/concepts/native-messaging) | Host manifest, message framing, and allowed extension origins. |
| E1 | [Firebase Dynamic Links deprecation FAQ](https://firebase.google.com/support/dynamic-links-faq) | Service shutdown on 25 August 2025; a source adapter does not prove current service availability. |

## Anthropic guidance

| ID | Source | Evidence used |
| --- | --- | --- |
| C1 | [Claude models overview](https://platform.claude.com/docs/en/models/overview) | Official model naming. “Claude Opus 5.1” was not verified; the page lists Opus 5 and Fable 5.1 as different models. |
| C2 | [Claude Code model configuration](https://code.claude.com/docs/en/model-config) | `/model` picker and `claude --model opus`; aliases resolve according to availability/provider. |
| C3 | [Claude Code best practices](https://code.claude.com/docs/en/best-practices) | Source context, outcome-driven tasks and effective project instructions. |
| C4 | [Claude Code project memory](https://code.claude.com/docs/en/memory) | Concise durable `CLAUDE.md`, progressively loaded project detail. |
| C5 | [Current prompting best practices](https://platform.claude.com/docs/en/build-with-claude/prompt-engineering/claude-prompting-best-practices) | Clear scope, structured inputs and long-running state management. |
| C6 | [Prompting Claude Opus 5](https://platform.claude.com/docs/en/build-with-claude/prompt-engineering/prompting-claude-opus-5) | Concise progress, scope control, avoiding redundant verification/delegation scaffolding. |

The prompt does not hardcode a fictitious model ID or undocumented CLI flags. It provides product acceptance criteria and evidence requirements without repeated “double-check everything” loops. The implementation should consult the guidance for the model actually selected if it changes.
