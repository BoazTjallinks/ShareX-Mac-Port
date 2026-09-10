# ADR 0001 — Native boundary: Swift behind an Objective-C C ABI

Status: accepted (stage 0)
Date: 2026-09-10

## Context

PROJECT-SPEC.md section 2 proposes a Swift native component behind an
Objective-C/C ABI, and explicitly requires a stage-zero spike before committing:
"If supported .NET macOS bindings prove materially simpler, document an ADR and
use one integration approach."

Measured environment on this machine:

| Item | Value |
| --- | --- |
| macOS | 26.6.2 (build 25G83) |
| CPU | arm64 (Apple Silicon) |
| .NET SDK | 10.0.400, runtime 10.0.11, RID osx-arm64 |
| Swift | 6.3.3 (swiftlang-6.3.3.1.3), target arm64-apple-macosx26.0 |
| Xcode | **not installed** — Command Line Tools only (`/Library/Developer/CommandLineTools`) |
| SDKs present | MacOSX.sdk, MacOSX26.5.sdk, MacOSX26.sdk, MacOSX15.sdk, MacOSX15.4.sdk |
| FFmpeg | /opt/homebrew/bin/ffmpeg |

## Decision

Use one integration approach: **Swift implementation, Objective-C shim exporting
a versioned C ABI, C# `LibraryImport` on the managed side.**

Rejected: .NET macOS bindings (`net10.0-macos`). Reasons, in order of weight:

1. The required surface is ScreenCaptureKit (`SCShareableContent`,
   `SCContentFilter`, `SCStreamConfiguration`, `SCScreenshotManager`, `SCStream`)
   plus AVFoundation asset writing. The .NET macOS binding set does not track
   these at the fidelity this port needs, and any gap would have to be closed
   with hand-written interop anyway — producing exactly the two competing
   integration stacks the roadmap forbids.
2. `net10.0-macos` requires the macOS workload; `dotnet workload list` shows no
   workloads installed, and installing one is a system-level change that needs
   the operator's explicit choice.
3. The high-frequency path (video frames) must stay native regardless. A binding
   layer that marshals `CMSampleBuffer` into managed code would be a performance
   and lifetime hazard, not a simplification.

## The ABI

`native/ShareXMacNative/include/sxm_abi.h` is the single source of truth.
Exported symbols are defined in Objective-C (`src/sxm_abi.m`), not in Swift, so
the ABI does not depend on the underscored `@_cdecl` Swift attribute. Swift
implements behaviour behind `@objc` classes reached through the generated
`ShareXMacNative-Swift.h`.

Shape: `sxm_abi_version`, `sxm_set_event_sink`, `sxm_begin`, `sxm_cancel`,
`sxm_release`, `sxm_copy_blob`. Requests and results are UTF-8 JSON with explicit
byte lengths; binary results (PNG bytes) are fetched through the bounded
`sxm_copy_blob` two-call pattern rather than embedded in JSON.

Contract, enforced on both sides:
- `sxm_begin` copies the request before returning; it either fails immediately
  with no callback, or returns an operation id that completes exactly once.
- Completion payloads are borrowed for the duration of the callback.
- `sxm_cancel` is idempotent and is *not* the terminal acknowledgement.
- `sxm_release` is legal only after the terminal event and is idempotent.
- No Swift error, Objective-C exception or object crosses the boundary; every
  native failure becomes a `sxm_result_code` plus a JSON body.
- Low-rate state events (recording state, hotkeys, display reconfiguration) use
  the separate persistent `sxm_set_event_sink` channel. Media data never travels
  over the ABI.

## Consequences

- Avalonia keeps ownership of the main event loop. The native side never starts
  a second `NSApplication`; AppKit work is dispatched to the existing main queue.
- The dylib builds with Command Line Tools only (see ADR 0002), so no Xcode
  installation is requested from the operator.
- The managed side owns `GCHandle` lifetimes per operation and must guarantee
  exactly one `sxm_release` per successful `sxm_begin`.
