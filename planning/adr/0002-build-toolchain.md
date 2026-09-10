# ADR 0002 — Build the native library with Command Line Tools, not Xcode

Status: accepted (stage 0)
Date: 2026-09-10

## Context

PROJECT-SPEC.md section 3 says `native/ShareXMacNative` is "built by Xcode".
Xcode is not installed on this machine; only the Command Line Tools are
(`xcode-select -p` → `/Library/Developer/CommandLineTools`, and `xcodebuild`
errors out). Installing Xcode is a multi-gigabyte system-level change that
requires the operator's explicit decision.

## Decision

Build the native library with `xcrun swiftc` + `xcrun clang` from
`native/ShareXMacNative/build.sh`, with no `.xcodeproj` and no `xcodebuild`.

Verified: the Command Line Tools SDK
(`/Library/Developer/CommandLineTools/SDKs/MacOSX.sdk`) contains
ScreenCaptureKit, AVFoundation, Vision, CoreMedia, CoreImage and AppKit, which
is the entire framework surface this port needs.

Pipeline:
1. `swiftc -wmo -emit-object -emit-objc-header-path …` compiles all Swift sources
   into one object file and generates `ShareXMacNative-Swift.h`.
2. `clang -fobjc-arc -c src/sxm_abi.m` compiles the ABI shim against that header.
3. `clang -dynamiclib` links both into `libShareXMacNative.dylib` with
   `-install_name @rpath/libShareXMacNative.dylib` and an rpath to
   `/usr/lib/swift` (the OS-provided Swift runtime, macOS 10.14.4+).

Deployment target `arm64-apple-macos15.0`, matching the spec's proposed minimum,
even though the host runs macOS 26.6.

## Consequences

- No Xcode dependency for a clean rebuild on this laptop.
- A future universal (arm64 + x86_64) build means adding a second `-target` pass
  and `lipo`; the script is structured for that but does not promise it, since
  an x86_64 build cannot be runtime-tested here.
- `xcodebuild`-specific features (asset catalogs, entitlement provisioning
  profiles) are unavailable; the app bundle is assembled by script instead, and
  code signing uses `codesign` directly.
