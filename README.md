# ShareX-Mac-Port

An **unofficial, work-in-progress macOS port of [ShareX](https://github.com/ShareX/ShareX) 21.0.0.**

> ShareX is created by the **ShareX Team** (Jaex, McoreD and contributors) and is
> licensed GPL-3.0. All credit for ShareX belongs to them. This repository is a
> derivative port — it is not official, not affiliated with, and not endorsed by
> the ShareX project. Please direct ShareX issues to
> [upstream](https://github.com/ShareX/ShareX/issues), not here.
>
> Full attribution and provenance: **[NOTICE.md](NOTICE.md)**.

---

## Status: stage 0 complete — it builds, installs, and captures.

This is an honest status table, not a roadmap of intentions. Nothing is listed as
working unless it was actually executed on a real machine.

| Area | State | Evidence |
| --- | --- | --- |
| Native bridge (Swift + Objective-C C ABI over ScreenCaptureKit) | **works** | `--selftest` below |
| Upstream enum port (75 commands, 22 after-capture, 6 after-upload flags, …) | **ported**, values verified against the pinned source | 90 passing tests |
| Managed interop (`LibraryImport` over the C ABI) | **works** from the real bundle | `--selftest` |
| `.app` packaging, signing, install to /Applications | **works** | `scripts/build-app.sh`, `scripts/install-app.sh` |
| Screen capture at runtime | **works** — real Retina capture to disk | 3024×1964 px from a 1512×982 pt display @2× |
| Main window (ShareX command structure, Mac chrome) | **renders**; command handlers not wired yet | `src/ShareX.Mac.App` |
| Recording pipeline | **compiles**, full state machine; runtime **not** demonstrated | `native/…/SXMRecordingSession.swift` |
| Editors, 232+51 effects, 74 uploaders, settings UI, history, CLI | **not implemented** | — |

Self-test from the installed bundle:

```
$ /Applications/ShareX-Mac.app/Contents/MacOS/ShareX-Mac --selftest
PASS  capabilities.get: 30 ms   os 26.6.2, arm64, abi 1, com.tjallinks.sharexmac
PASS  permissions.get         screenRecording granted
PASS  displays.list   : 1     #1 1512x982 pt @2x origin (0,0) space=CgGlobalPoints
PASS  windows.list    : 25
PASS  media.encoders          h264 yes, hevc yes, prores4444 NO, pngSequence yes
PASS  capture         : 130 ms 3024x1964 px, scale 2
RESULT: all checks passed.
```

The upstream feature ledger (`planning/feature-ledger.json`) tracks 671 catalogue
entries. Almost all of them are still `not_started`. This README will not claim
otherwise.

## What this port is trying to be

A faithful port, not a smaller screenshot app: the same commands, the same
workflow semantics, both editors, the full destination catalogue, `.sxcu`/`.sxie`
compatibility, and the same settings — rendered with native macOS chrome rather
than transplanted WinForms metrics. See [`PROJECT-SPEC.md`](PROJECT-SPEC.md) and
[`planning/adr/`](planning/adr/) for the decisions and their reasoning.

Where macOS genuinely cannot reproduce a Windows behaviour, the difference is
written down (see [`docs/MACOS-LIMITATIONS.md`](docs/MACOS-LIMITATIONS.md)) rather
than hidden behind a disabled button.

## Requirements

- Apple Silicon Mac, macOS 15 or later (developed on macOS 26.6)
- .NET SDK 10.0.4xx
- Xcode Command Line Tools (full Xcode is **not** required)
- FFmpeg, for the media features that need it

## Building

```sh
# 1. verify and unpack the pinned upstream source into reference/ShareX
python3 scripts/verify-package.py
python3 scripts/restore-upstream.py

# 2. native bridge (Swift + Objective-C, Command Line Tools only)
./native/ShareXMacNative/build.sh

# 3. managed solution and tests
dotnet build ShareX-Mac.sln
dotnet test  tests/ShareX.Core.Tests/ShareX.Core.Tests.csproj

# 4. (optional) a stable local signing identity, so macOS keeps the
#    Screen Recording permission across rebuilds
./scripts/create-signing-identity.sh

# 5. assemble and install the app bundle
./packaging/make-icns.sh
./scripts/build-app.sh
./scripts/install-app.sh
```

`reference/ShareX` is a read-only extraction of the pinned upstream source. It is
git-ignored and must never be edited; ports live under `src/` and `native/`.

## Repository layout

| Path | Contents |
| --- | --- |
| `native/ShareXMacNative/` | Swift implementation behind a versioned C ABI (`include/sxm_abi.h`) |
| `src/ShareX.Core/` | Portable domain: upstream enums, error taxonomy, coordinate spaces |
| `src/ShareX.Platform.Mac/` | Managed bindings for the native bridge |
| `src/ShareX.Imaging/` | Portable image abstraction and the legacy effect library |
| `src/ShareX.Mac.App/` | Avalonia application host |
| `planning/` | ADRs, the feature ledger, and behaviour extracted from the pinned source |
| `docs/` | Feature/effect/destination catalogues and the parity test plan |
| `reference/` | Read-only upstream source (git-ignored, restored by script) |

Two documents in `planning/` are worth reading before changing behaviour, because
they record semantics that are easy to get wrong by guessing:

- [`planning/workflow-control-flow.md`](planning/workflow-control-flow.md) — the real
  execution order inside `WorkerTask`, extracted from the pinned source.
- [`planning/task-settings-resolution.md`](planning/task-settings-resolution.md) — how
  the 13 settings override groups resolve into a per-job snapshot.

## Licence

GPL-3.0, the same as upstream ShareX. See [`LICENSE`](LICENSE) and
[`NOTICE.md`](NOTICE.md).
