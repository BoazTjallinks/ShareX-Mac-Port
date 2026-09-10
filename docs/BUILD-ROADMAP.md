# Build roadmap

The scope is the entire pinned release. Stages make the work reviewable and resumable. Routine progression does not need a fresh plan-approval question. A blocked provider or unavailable test machine does not justify abandoning independent work.

| Stage | Deliverable | Source focus | Exit evidence |
| --- | --- | --- | --- |
| 0 — Feasibility | Real arm64 app bundle; working native interop; new editor opens a fixture; one still capture; recording with system/microphone audio; permission denial handled | Project files, editor hosting, native boundaries | Toolchain versions, bundle launch, captured files, audio-track inspection, source dependency audit. Resolve event-loop ownership and native codec path before broad UI work. |
| 1 — Core extraction | Portable command IDs, settings snapshots, uploader parser, filename parser, image/clipboard abstractions, task results | Enums, TaskSettings, WorkerTask, custom syntax, NameParser | Deterministic parser and inheritance fixtures; no reachable Windows-only dependencies in core. |
| 2 — Daily capture path | Full/monitor/window/region capture; global hotkey; save/copy; main/tray UI; history | CaptureHelpers, Screenshot, RegionCaptureForm, HotkeyManager | Retina and mixed-display checks; ESC leaves no job; permissions identify actual app; history row and clipboard contents match output. |
| 3 — Editor parity | All new editor tools and all 232 effect IDs; legacy selection/annotation and editor mode | ShareX.ImageEditor, ScreenCaptureLib shapes, legacy effects | Input/undo/redo tests; deterministic image fixtures; both editor-selection modes work; platform-rendered differences listed. |
| 4 — Workflows | All 22 after-capture and 6 after-upload flags, dialogs, quick presets, per-hotkey overrides, queue/cancel/retry | WorkerTask, TaskManager, UploadManager, task dialogs | Recorded traces for competing flags and cancellation; no duplicate external side effects; error/deletion semantics explicit. |
| 5 — Custom and core uploaders | Complete .sxcu compatibility; shared folder; FTP/FTPS/SFTP; selected HTTP/storage adapters | CustomUploader and UploadersLib | Local request-oracle fixtures; host-key/certificate checks; useful first personal upload workflow. This does not finish the provider catalogue. |
| 6 — Full destination catalogue | All 74 service registrations plus file-uploader routes; every configuration field | UploaderFactory, service files, UploadersConfig | Each adapter has request/response tests and an honest live status. Retired or credential-dependent services remain visible in coverage. |
| 7 — Recording and media | All recording commands, pause/resume/audio, GIF/APNG/WebP and available codecs, video conversion/thumbnailing | ScreenRecording, FFmpegOptions, MediaLib | Long recording, sync markers, abort/recovery, bounded memory/storage, codec discovery, cancellation. |
| 8 — Advanced capture | Scroll capture, auto capture, masks, pin behavior, control detection and supported capture preferences | ScrollingCaptureManager/options, region options, PinToScreen | Browser and nonbrowser scrolling fixtures; repeated headers/end detection; partial capture; screen removal and permission revocation. |
| 9 — Remaining tools | OCR, AI analysis, QR, hash, metadata, indexer, compare/combine/split/thumbnail/viewer/beautifier, ruler/color picker/monitor test/window inspector | Tools, HelpersLib, IndexerLib | Input/output fixtures per tool; network tools use synthetic content and explicit configuration. |
| 10 — Settings and integration | Source settings reconciliation; backup/import/export; localization; CLI; browser host; Finder actions/login item | SettingManager, configs, CLI, IntegrationHelpers, resources | Roslyn/serialization/UI binding audit; import report; roundtrip fixtures; browser framing/origin checks; login-item lifecycle. |
| 11 — Local delivery | Stable self-contained .app, install/rebuild instructions, final coverage and exception report | Packaging and all prior evidence | Launch on user's Mac; daily end-to-end path; targeted regression suite; data backup/rollback; no omitted or falsely completed catalogue entries. |

## First-session work

Discover rather than assume: `sw_vers`, `uname -m`, `xcodebuild -version`, `xcrun swift --version`, `dotnet --info`, `git status`, and the repository instructions. Check that the full Xcode/SDK needed by the native target is available. Install nothing system-wide without the operator's explicit choice.

Run package verification and restore the included source. Record actual SDK, dependency, application identity, and permission-test configuration in `planning/PROGRESS.md`. Create the future solution under `src/`, not under `reference/ShareX`. Reuse source through attributed forks and focused changes. Begin the integration spike immediately after a brief plan.

The first stage is intentionally demanding: native capture, Avalonia hosting, interop, and recording are the main architectural risks. A pretty main window alone does not resolve them. If the proposed Swift bridge loses to supported .NET macOS bindings in a concrete spike, record the evidence and use one approach consistently. Do not keep competing integration frameworks indefinitely.

## Dependencies and progress

Stages 1–4 establish the main causal path. Providers, independent effects, and individual tools can then be developed separately if later delegation is authorized. Default to one owner. Each stage updates the feature ledger, adds only the checks needed for the changed behavior, and records build commands and evidence.

Use repository-local checkpoints. Existing user changes must be preserved. If there is no Git repository, initialize a local one; commits are local checkpoints, and no remote is created or pushed. If the user later supplies an external repository, follow its instruction files and branch conventions.

## Resuming

Read `CLAUDE.md`, `planning/PROGRESS.md`, relevant ADRs, and the affected feature IDs. Inspect the current diff and the latest check output. Continue the next unfinished stage, including outstanding work from earlier stages. Do not re-audit every untouched file or rerun every successful test after every small edit.

## Effort and cost

This project includes capture, media, image editing, hundreds of effects/settings, migrations, and many remote integrations. It is not a reliable one-evening or one-session build. Establish any time/token estimate only after stage 0 and the first implemented vertical slice. Maintain cost awareness through bounded tasks and reusable scripts rather than dropping difficult features. Public release engineering is outside this personal-install scope.
