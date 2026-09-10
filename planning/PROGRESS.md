# Progress

## Current state

Design/research package complete; macOS application implementation not started.

Source baseline: ShareX v21.0.0, commit d2502561f63fc3ff502cacd91514e3f7f2948c74.

Research inspected the actual source, package references, workflow branches, settings, new and legacy editor/effect code, capture options, registered services, custom parser, OCR, AI providers, history and CLI. Native macOS and Windows GUI execution were not performed in the research environment.

## Decisions

- Full release scope, personal local app, Apple Silicon first.
- C#/.NET 10 + Avalonia + SkiaSharp with a Swift/C ABI bridge; stage 0 may compare supported .NET macOS bindings.
- New editor source is reusable but has remaining Windows dependencies.
- Source is immutable under reference/ShareX; implementation forks go under src/.
- No hosted backend, public release or automatic real provider uploads.
- Exact platform/external differences remain explicit and outside exact-parity claims.

## Next action

Run package verification and restore the source. Inspect the real Mac toolchain. Start stage 0 from docs/BUILD-ROADMAP.md. Create the solution, native project and canonical build/test scripts based on discovered versions.

## Evidence

- inventory/upstream-audit.json: source-derived inventory and tracked-file hashes.
- upstream.lock.json: pinned source and archive hashes.
- planning/package-verification.json: design-package validation only.
- docs/SOURCES.md: official sources and retrieval limits.

## Environment still to discover

Actual macOS version, Xcode/Swift SDK, installed .NET SDK, signing identity, Windows reference-machine availability, and user-owned test-provider credentials. None is fabricated or assumed to have been tested.

## Future handoff fields

When implementation starts, replace this section with: active stage, affected feature IDs, changed files, exact build/test commands and exit results, app output path, permissions granted/needed, known failures, and the next executable action.
