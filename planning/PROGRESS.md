# Progress

Last updated: 2026-09-10 (stage 0 in progress)

## Current state

Stage 0 (feasibility / native boundary) partially complete.

**Done and verified on this machine:**
- Package verification passed (`scripts/verify-package.py` → `"result": "passed"`, 3971 source files, commit `d2502561f63fc3ff502cacd91514e3f7f2948c74`).
- Upstream source restored to `reference/ShareX` (3971 verified files). Tree is immutable by convention and git-ignored.
- Local git repository initialised (no remote, no push). First checkpoint: "Design package baseline".
- Native bridge **builds and links**: `native/ShareXMacNative/build.sh` → `libShareXMacNative.dylib` (arm64).
- ADRs 0001 (native boundary), 0002 (build toolchain), 0003 (dependency pins) written.

**Not yet done:** managed solution, app bundle, any capture executed at runtime, any UI.

## Verified environment (measured, not assumed)

| Item | Value |
| --- | --- |
| macOS | 26.6.2 (build 25G83) |
| Architecture | arm64 |
| .NET SDK | 10.0.400 (runtime 10.0.11), RID osx-arm64 |
| .NET workloads | none installed |
| Swift | 6.3.3, default target arm64-apple-macosx26.0 |
| Xcode | **not installed** (Command Line Tools only) |
| CLT SDK frameworks | ScreenCaptureKit, AVFoundation, Vision, CoreMedia, CoreImage, AppKit all present |
| FFmpeg | 9.0.1 at /opt/homebrew/bin/ffmpeg |
| Code-signing identity | **none** (`security find-identity -v -p codesigning` → 0 valid identities) |

## Exact commands that work today

```sh
# package integrity + source restore
python3 scripts/verify-package.py
python3 scripts/restore-upstream.py

# native library (Command Line Tools only, no Xcode)
./native/ShareXMacNative/build.sh
# → native/ShareXMacNative/build/libShareXMacNative.dylib (arm64)
```

Managed build/test/package commands do not exist yet. They will be recorded here
verbatim once the solution lands; nothing is invented in advance.

## Decisions this session

- ADR 0001: one integration approach — Swift behind an Objective-C C ABI, bound
  by C# `LibraryImport`. .NET macOS bindings rejected with reasons.
- ADR 0002: build the dylib with `xcrun swiftc`/`clang`, not `xcodebuild`;
  Xcode is not installed and installing it is the operator's decision.
- ADR 0003: `Microsoft.Data.Sqlite` moved 10.0.9 → 10.0.11 (upstream pin pulls a
  package with a high-severity advisory). All other upstream pins kept.
- User instruction (2026-09-10): the UI must be a 1:1 copy of ShareX's. Upstream
  WinForms layout is therefore being extracted mechanically into
  `planning/ui-layout/*.json` rather than re-designed by hand.

## Open findings that affect later stages

| Finding | Stage | Consequence |
| --- | --- | --- |
| No code-signing identity on this Mac | 0 / 11 | Ad-hoc signing works but the TCC (Screen Recording) grant is tied to the code hash, so it re-prompts after every rebuild. A stable self-signed code-signing certificate in the login keychain would fix it — needs the operator's decision. |
| Installed FFmpeg 9.0.1 has **no WebP encoder** (`--enable-libwebp` absent) | 7 | Animated WebP output is unavailable with this binary. GIF and APNG encoders are present; VideoToolbox H.264/HEVC/ProRes are present. Recorded as an external limitation, not a platform one. |
| No full Xcode | 0 | Native builds use CLT; asset catalogs and `xcodebuild` packaging are unavailable, so the `.app` is assembled by script. |

## Next executable actions

1. Land the managed solution (`ShareX-Mac.sln`) with `ShareX.Core` (ported upstream
   enums, error taxonomy, coordinate-space types) and `ShareX.Platform.Mac`
   (P/Invoke over `native/ShareXMacNative/include/sxm_abi.h`).
2. Assemble `ShareX-Mac.app` (script), ad-hoc sign, launch, and execute a real
   `capture.screenshot` through the native bridge from inside the bundle — this
   is the stage-0 exit gate that a `dotnet run` cannot satisfy.
3. Extract upstream WinForms layout to `planning/ui-layout/` and build the
   Avalonia MainForm against it.

## Feature ledger

`planning/feature-ledger.json` still reports 671 entries at `not_started`. No
entry has been marked implemented; nothing has been demonstrated at runtime yet.
