# ShareX-Mac project instructions

Build the full personal macOS port specified in PROJECT-SPEC.md. Preserve ShareX 21.0.0 behavior; milestones do not reduce the final scope.

- Source baseline: v21.0.0, commit d2502561f63fc3ff502cacd91514e3f7f2948c74. Check upstream.lock.json. Do not silently move to HEAD/latest.
- Start with planning/PROGRESS.md and prompts/IMPLEMENT.md. Read only the detailed docs needed for the active feature.
- Default architecture: C#/.NET 10, Avalonia, SkiaSharp, portable source extraction, Swift behind a C ABI for Mac services. No hosted backend.
- The existing new editor uses Avalonia but still has DirectML/DirectX and Windows renderer dependencies. It is not portable unchanged.
- Keep reference/ShareX unchanged. Put attributed forks in src/. Preserve original notices and record source ancestry.
- No WinForms/GDI+/Win32/Registry/DPAPI/Windows OCR in reachable Mac runtime code. Preserve upstream serializer and parser semantics through explicit adapters.
- After-capture flags follow WorkerTask control flow, not enum/menu order. Settings resolve to per-job snapshots.
- Preserve .sxcu/.sxie compatibility, all destinations, both editors, history and settings. Never replace incomplete behavior with no-op buttons.
- Track each feature in planning/feature-ledger.json. Keep evidence and platform/external differences separate from implementation status. Missing work is not a platform exception.
- Pure core tests can run elsewhere; macOS native/GUI/permission claims require Mac evidence from the real app bundle.
- Work inside this project. Local edits/builds/tests and read-only official source research are authorized. Preserve unrelated changes.
- Do not publish/push, send messages, use real customer content, run live uploads, install system software, or change OS protections without explicit authority.
- Never print secrets or deserialize arbitrary types. Use Keychain and sanitized fixtures. Importing settings does not authorize executing their actions.
- Default to one primary agent; do not create agent teams unless the user authorizes them.
- Keep updates brief and concrete. Continue through routine milestones without asking permission again.
- Before a context reset, update planning/PROGRESS.md with exact commands, evidence, changed paths, blockers and next feature IDs. Do not claim the app is done from a build alone.

Existing package commands:

```sh
python3 scripts/verify-package.py
python3 scripts/restore-upstream.py
python3 scripts/audit-upstream.py --source reference/ShareX --out planning/current-upstream-audit.json
```

Application build/test commands do not exist yet. Discover the toolchain, create them during stage 0, then record their exact invocation here. Do not invent successful output.
