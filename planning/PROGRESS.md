# Progress

Last updated: 2026-09-10 (stage 0 COMPLETE — gate passed from the installed bundle)

## Current state

**Stage 0 (feasibility / native boundary) is complete.** The gate was passed from
a signed app bundle installed at `/Applications/ShareX-Mac.app`, not from
`dotnet run`.

## Stage 0 evidence

`/Applications/ShareX-Mac.app/Contents/MacOS/ShareX-Mac --selftest`, 2026-09-10:

```
running from bundle : True
bundle path         : /Applications/ShareX-Mac.app
runtime             : .NET 10.0.11

PASS  capabilities.get: 30 ms   os 26.6.2, arm64, abi 1, com.tjallinks.sharexmac
PASS  permissions.get         screenRecording granted, accessibility granted, microphone granted
PASS  displays.list   : 1     #1 1512x982 pt @2x origin (0,0) space=CgGlobalPoints main active
PASS  windows.list    : 25
PASS  media.encoders          h264 yes, hevc yes, prores4444 NO, pngSequence yes
PASS  capture         : 130 ms 3024x1964 px, scale 2, source (0,0) 1512x982 CgGlobalPoints
      file          : ~/Pictures/ShareX/2026-09/selftest_2026-09-10_13-34-50.png (1,717,371 bytes)
RESULT: all checks passed.
```

Independently verified with `sips`: the PNG is 3024x1964, 8-bit RGBA,
non-interlaced. A 1512x982 point display at backing scale 2 gives exactly
3024x1964 physical pixels, so Retina scale is handled without doubling or loss.

The `.app` is signed by `ShareX-Mac Local`, hardened runtime, `Mach-O thin
(arm64)`, and `codesign --verify --strict` reports "valid on disk" and
"satisfies its Designated Requirement".

**Done and verified on this machine:**
- Package verification passed (`scripts/verify-package.py` → `"result": "passed"`, 3971 source files, commit `d2502561f63fc3ff502cacd91514e3f7f2948c74`).
- Upstream source restored to `reference/ShareX` (3971 verified files). Tree is immutable by convention and git-ignored.
- Local git repository initialised (no remote, no push). First checkpoint: "Design package baseline".
- Native bridge **builds and links**: `native/ShareXMacNative/build.sh` → `libShareXMacNative.dylib` (arm64).
- ADRs 0001 (native boundary), 0002 (build toolchain), 0003 (dependency pins) written.

**Done and verified since:**
- Managed solution builds clean (0 warnings, 0 errors); 90 tests pass.
- `ShareX-Mac.app` assembles, signs, installs to `/Applications`, and launches
  with its main window.
- A real ScreenCaptureKit capture reaches disk at correct Retina resolution.
- Native recording pipeline compiles with a full state machine (runtime recording
  is NOT yet demonstrated).

**Not yet done:** everything from stage 2 onward — the command handlers behind the
UI, both editors, effects, uploaders, settings, history, CLI.

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
# -> native/ShareXMacNative/build/libShareXMacNative.dylib (arm64)

# managed solution and tests
dotnet build ShareX-Mac.sln -v q                                    # 0 warnings, 0 errors
dotnet test tests/ShareX.Core.Tests/ShareX.Core.Tests.csproj        # 44 passed
dotnet test tests/ShareX.Platform.Mac.Tests/...csproj               # 46 passed, 3 skipped

# stable local signing identity (login keychain only; one-time)
./scripts/create-signing-identity.sh

# app bundle: native -> publish -> layout -> deep sign -> verify
./packaging/make-icns.sh
./scripts/build-app.sh
# -> build/ShareX-Mac.app  (109 MB, self-contained, arm64)

# install / uninstall
./scripts/install-app.sh
./scripts/install-app.sh --uninstall

# the stage-0 integration gate, from the real bundle
/Applications/ShareX-Mac.app/Contents/MacOS/ShareX-Mac --selftest
```

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
