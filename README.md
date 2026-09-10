# ShareX for macOS — project and Claude Code package

This is the complete researched design package for a personal **ShareX 21.0.0 macOS port**. It includes the pinned Windows source, architecture, implementation catalogues, compatibility rules, build stages, acceptance cases and ready-to-use Claude Code instructions. **It is not a compiled or implemented Mac app.**

Recommended stack: **C# / .NET 10 LTS / Avalonia / SkiaSharp**, with a **Swift native bridge** for macOS capture and system integration. No hosted backend. The recommendation reuses ShareX's existing Avalonia editor and portable C# logic.

## Start in Claude Code

1. Extract this entire project folder on your Mac. Keep its files together.
2. Open a terminal in this folder and start Claude Code. Use `/model` to select the Opus model available in your account. “Opus 5.1” was not verified in Anthropic's official catalogue; see [model guidance](docs/CLAUDE-GUIDANCE.md).
3. Paste the complete prompt from [IMPLEMENT.md](prompts/IMPLEMENT.md), or use this launcher:

```text
Read CLAUDE.md and prompts/IMPLEMENT.md in this project. Execute that
implementation prompt against the supplied specification and pinned source.
Start with environment inspection and stage 0, then continue the complete
ShareX 21.0.0 macOS port. Do not stop at a plan or basic capture demo. Track
feature evidence and explicit platform differences in the supplied ledgers.
```

4. For a later session use [CONTINUE.md](prompts/CONTINUE.md).

You do not have to clone ShareX yourself. The supplied restore script verifies and extracts the included source archive. Claude should inspect existing developer tools and tell you about genuinely missing prerequisites when needed.

## Read the design

| File | Contents |
| --- | --- |
| [PROJECT-SPEC.md](PROJECT-SPEC.md) | Architecture decisions, stack, capture/media/editor design, workflow semantics, data model, migration, UI and packaging. |
| [INTERFACES-AND-DATA.md](docs/INTERFACES-AND-DATA.md) | Module inputs/outputs, native ABI, artifact ownership, settings boundaries and error taxonomy. |
| [FEATURE-CATALOGUE.md](docs/FEATURE-CATALOGUE.md) | Every executable command, after-task flag, editor/region tool, integration variant and additional feature surface, with implementation and acceptance mapping. |
| [EFFECT-CATALOGUE.md](docs/EFFECT-CATALOGUE.md) | All 232 new effect IDs and 51 legacy effects, linked to their implementation. |
| [DESTINATIONS.md](docs/DESTINATIONS.md) | All 74 registered services, two file-uploader routes, custom-uploader functions and per-adapter approach. |
| [SETTINGS-COMPATIBILITY.md](docs/SETTINGS-COMPATIBILITY.md) | 926 source-field candidates, original declarations and per-field port/migration rules; semantic reconciliation requirement. |
| [UPSTREAM-SOURCE-MAP.md](docs/UPSTREAM-SOURCE-MAP.md) | Where the relevant Windows/C# code lives and what can be reused or replaced. |
| [BUILD-ROADMAP.md](docs/BUILD-ROADMAP.md) | Twelve staged deliverables with concrete exit evidence. |
| [PARITY-TEST-PLAN.md](docs/PARITY-TEST-PLAN.md) | Capture, workflow, editor, upload, recording, migration, UI and installation acceptance cases. |
| [MACOS-LIMITATIONS.md](docs/MACOS-LIMITATIONS.md) | OS differences and external-service limits that cannot honestly count as exact parity. |
| [SOURCES.md](docs/SOURCES.md) | Official research references, dates, source provenance and retrieval limits. |

Machine-readable tracking lives in `planning/feature-ledger.json` (**671 entries**) and `planning/settings-ledger.json`. Entries include overlapping feature variants and option values; 671 is a tracking count, not a claim of 671 independent user-facing tools. All application implementation statuses start unfinished.

## Included source

Repository: [ShareX/ShareX](https://github.com/ShareX/ShareX). Release: [v21.0.0](https://github.com/ShareX/ShareX/releases/tag/v21.0.0). Commit: `d2502561f63fc3ff502cacd91514e3f7f2948c74`.

`vendor/ShareX-v21.0.0-source.tar.gz` contains all **3,971 tracked source files** from that commit, including project files, original license notices and assets. It excludes Git history and downloaded dependency binaries/models. The source tree is restored under `reference/ShareX`; implementation forks belong under `src/`.

Optional manual integrity/restore commands, if Python 3 is available:

```sh
python3 scripts/verify-package.py
python3 scripts/restore-upstream.py
```

Restoration refuses to overwrite an existing reference tree. `scripts/audit-upstream.py` reproduces the lexical inventory. A separate semantic settings audit is required during implementation.

## What “1:1” can mean

The full source feature set remains in scope. Some Windows operations have no direct public macOS equivalent; OCR, permissions, hardware encoders and third-party window control also differ. Remote services can disappear independently of either OS. The port must reproduce feasible behavior and openly document these differences. Neither this package nor the final application should claim literal perfect parity while exceptions remain.

The project is large. Claude Code should build it in stages, preserve progress between sessions and use observable evidence. The included source and inventories prevent a changing baseline or a forgotten feature list; they do not eliminate the engineering work.
