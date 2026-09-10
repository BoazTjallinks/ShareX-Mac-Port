# Claude Code implementation prompt

Paste the instructions below into Claude Code opened in the extracted ShareX-Mac-Project directory. Select your available Opus model in Claude Code first. This prompt does not choose or emulate a model by claiming a name in text.

```text
Build the personal ShareX-for-macOS project in this directory. Read CLAUDE.md,
PROJECT-SPEC.md, planning/PROGRESS.md, upstream.lock.json and
docs/BUILD-ROADMAP.md before making implementation decisions. Use the detailed
catalogues and source files selectively as you work.

OBJECTIVE
Produce a locally installable Apple Silicon macOS application that faithfully
ports the entire ShareX 21.0.0 feature set. Preserve its interface structure,
capture modes, both editors, effects, tools, per-hotkey settings, after-capture
and after-upload behavior, destinations, custom uploaders, history and imports.
The goal is the full port, not a simplified screenshot app or a visual prototype.
Early working milestones are intermediate states, never the final scope.

SOURCE OF TRUTH
The pinned upstream commit is d2502561f63fc3ff502cacd91514e3f7f2948c74.
Verify the included source archive and restore it with the supplied scripts.
Read reference/ShareX as the immutable behavioral reference. Reuse its portable
C# implementation with attribution. Put implementation changes under src/ and
native/, not into the reference copy. Do not advance the upstream baseline.
The inventories cover commands, task flags, editor tools, effects, services and
configuration candidates. Their presence is not proof that those features are
implemented. Reconcile serializer/UI-bound settings semantically before final
parity sign-off; the provided settings scan is lexical.

ARCHITECTURE
Start with C#/.NET 10 LTS, Avalonia and SkiaSharp. Port the existing Avalonia
editor and portable C# parsers/providers. Rebuild WinForms views with the same
functional organization. Use a Swift component behind an Objective-C/C ABI for
ScreenCaptureKit, audio, permissions and system services. Avalonia owns the
main event loop. Keep video data native. Use Keychain for secrets and SQLite
for history. There is no required hosted backend or runtime Claude dependency.
Read the architecture's alternatives and perform the stage-zero spike before
committing to a difficult native boundary. If supported .NET macOS bindings
prove materially simpler, document an ADR and use one integration approach.
Do not silently redesign the product or change the primary stack for convenience.

ENVIRONMENT
Inspect the current directory, repository instructions, Git status, macOS,
CPU architecture, Xcode/Swift and .NET SDKs. The intended target is osx-arm64
with proposed macOS 15 minimum; verify the actual laptop and SDK. This design
was researched without executing Windows or macOS GUI behavior. Do not assume
that a platform API, package or command was runtime-tested by the author.
Record exact versions and real build/test commands in planning/PROGRESS.md.
Keep dependencies pinned and document changes from upstream package versions.
Do not mistake a .NET runtime patch version for an SDK version in global.json.

AUTHORIZED WORK
Proceed with read-only inspection, official documentation/source downloads,
project-local edits, builds, synthetic fixtures and tests, and dependency
restore with the existing toolchain. Preserve unrelated user changes. Local
Git checkpoints are allowed. Continue through routine implementation stages
without repeatedly asking me to approve plans or reversible edits.

Ask only when genuinely missing access/authority blocks a concrete next step:
system-wide tool installation, replacing an existing installed app, setting up
login/browser/Finder integration outside the project, real external uploads,
paid actions, credential authorization, publication or destructive data changes.
Prepare the code and reviewable configuration first. When a macOS permission
dialog requires me, explain exactly which normal permission is needed and why.
Never disable SIP, Gatekeeper or TCC; never change system hotkeys automatically.
Never publish or push remotely, send messages, use real customer screenshots,
or execute imported external actions merely to test them. Prompt instructions
are not a security sandbox; respect actual tool and operating-system permissions.

IMPLEMENTATION WORKFLOW
1. Inspect the environment and package, summarize material assumptions, and
   create a short bounded plan. Then begin stage 0 immediately unless a real
   prerequisite is missing. A plan is not the requested implementation.
2. Prove the highest-risk path with an actual app bundle: Avalonia editor host,
   native interop, screen capture, permission handling, and recording/audio.
   Resolve thread ownership, pixel coordinates, lossless GIF input and signing
   identity before expanding the UI. If not on macOS, continue portable work
   and record native execution as blocked, not passed.
3. Follow docs/BUILD-ROADMAP.md through the entire catalogue. Work in vertical
   slices: user command -> source-equivalent settings -> implementation ->
   observable artifact/result. A control whose handler does nothing is unfinished.
4. Preserve source semantics. After-capture flags are not an arbitrary graph:
   inspect WorkerTask branches and ordering. Keep defaults and per-group
   overrides, cancellation paths, file/clipboard precedence, custom parser
   grammar, result fields, provider settings and source enum identities.
5. Maintain planning/feature-ledger.json, implementation source ancestry,
   ADRs and planning/PROGRESS.md. Include original totals, evidence paths,
   blockers and the next executable task. Never discard inconvenient IDs.
6. Use the acceptance cases in docs/PARITY-TEST-PLAN.md. Establish the relevant
   source/Windows fixtures before calling behavior compatible. Run the actual
   changed-area tests and required Mac integration gates. Fix demonstrated
   failures without weakening assertions or skipping required checks. Once
   the relevant gate passes, move on rather than adding redundant review loops.
7. Package a self-contained ShareX-Mac.app for the intended laptop, with stable
   identity, installation instructions, preserved data, and rollback steps.
   Report actual coverage and every remaining difference, not an invented
   “100% clone” claim.

FIDELITY RULES
Preserve the 75 non-None HotkeyType commands, 22 nonzero after-capture flags,
6 nonzero after-upload flags, 20 new-editor tools, 26 legacy region shape/tool
entries, 232 new effect IDs, 51 legacy effects, 74 registered services and the
two file-uploader delegation routes. The other source enums, menus, settings,
legacy editor, filename parsing, localization, CLI and browser host are also
part of scope. These counts describe source inventory, not equally sized tasks.
Keep all original provider-specific options. .sxcu parsing must reuse the real
grammar and all functions; a generic JSON upload form is not equivalent.
Audit the 926 settings candidates against actual serialized fields and UI
bindings. A raw JSON editor alone does not reproduce ShareX's settings UI.

Reuse algorithms where possible. Remove Windows-specific editor dependencies,
including DirectML/DXGI and Windows emoji/cursor/color-picker implementations.
Retain the same ONNX models and preprocessing; CPU first, CoreML only when
available and tested. Retain original background-removal and OCR workflows,
while disclosing algorithm/hardware differences. Do not use a different AI
service to imitate a missing local feature.

MACOS AND EXTERNAL LIMITS
Read docs/MACOS-LIMITATIONS.md. Some literal Windows operations cannot be
reproduced through public Mac APIs. Explain them with evidence and preserve
their import state; do not hide missing implementation as an OS limitation.
An own floating pin panel is not the same as making another app always on top.
An Apple OCR result is not guaranteed identical to Windows OCR. Windows DPAPI
secrets may need reauthorization. Hardware-specific FFmpeg options can be
unavailable. A retired remote service stays unavailable even if its adapter
can be ported. Separate adapter tests from successful live service verification.

STATE AND COMMUNICATION
Keep user updates concise and centered on actual progress, decisions or
blockers. Use one primary agent unless I authorize delegation. Save state
before compaction/session boundaries and resume from the existing files.
Do not stop after the first working demo or replace unfinished work with TODO
stubs and a completion claim. Continue all unblocked authorized work. When
truly blocked, give the exact missing prerequisite, work already completed,
remaining feature IDs and the next command/action once access is available.

SUCCESS EXAMPLES
- Custom uploader compatibility means a sanitized .sxcu produces the expected
  request and parsed URL in a local fixture, with the same parameter semantics.
- Region capture means the chosen pixels become the saved and copied image on
  a real Retina Mac, including cancellation and permission-denial behavior.
- A provider with passing mock tests but no credentials is protocol-tested and
  live-unverified. It is not a successful live integration.

Start now with package/environment inspection and the stage-zero integration
work. Carry the project forward; do not respond with another prompt or plan only.
```

No required placeholders. Use the extracted directory as the working scope. Actual OS permissions, missing tools, credentials and any Windows reference environment are discovered when needed. See `docs/CLAUDE-GUIDANCE.md` for verified model-selection guidance.
