# Parity and acceptance plan

This document specifies future app verification. The package integrity checks performed while preparing this design do not validate a macOS implementation.

## Reference strategy

Pin both sides to the included ShareX source commit. On a Windows machine or Windows VM that the user makes available, run the corresponding release and collect sanitized fixtures. A Windows VM on Apple Silicon, if used, still needs its own functioning desktop/capture environment; it is not a substitute for the real macOS capture tests. Do not require buying or installing a VM automatically.

Use source-derived fixtures for portable algorithms immediately. For runtime UI/behavior not exercised on Windows, mark `reference_unverified`. Source inspection can resolve many rules but does not prove a Windows screenshot or interactive flow was reproduced. Keep the reference environment details with each fixture: build version, OS, DPI, theme, locale, keyboard layout, codec/library versions, and configuration.

For each feature ID, store implementation paths, test IDs, last result, observed macOS behavior, Windows evidence if available, and exceptions. Status is multi-dimensional: a provider may have passing protocol tests but no successful live upload. Stubs, skips, and inferred assertions do not earn passes.

## Concrete acceptance cases

| ID | Scenario | Required result |
| --- | --- | --- |
| CAP-01 | Region selection on Retina with one-pixel border fixture | Correct physical-pixel bounds and alpha mask; no doubled scale or missing edge. |
| CAP-02 | Selection crosses Retina and non-Retina displays | Explicit composition policy; consistent bounds and no duplicated strip. Store input display transforms. |
| CAP-03 | Monitor left/above primary; rotation and hot-plug | Negative coordinates handled; old region clamps or rejects visibly after layout change. |
| CAP-04 | Active-window hotkey while app is backgrounded | Captures the previously active user window, not ShareX or its overlay. |
| CAP-05 | Permission first use, denied, later granted, later revoked | Correct state and retry path; no empty image reported as success; app bundle owns permission. |
| CAP-06 | ESC, click cancellation, minimal region, no display | No unintended save/upload/history entry and no orphan overlay. |
| CAP-07 | Window/control selection, shadows, transparency | Supported behavior matches fixture; unavailable child-control/alpha behavior disclosed. |
| CAP-08 | Region tools and shortcut variants | Source-specific mouse buttons, nudges, shape removal, last-region switch, masks and commit/cancel work. |
| WF-01 | Beautify + effects + annotate + copy + save | Trace matches pinned source order and clipboard/image represent the correct stage. |
| WF-02 | Copy file + copy file path + copy folder path all enabled | Match source precedence rather than last-writer-wins iteration. |
| WF-03 | Save disabled, upload enabled | A valid encoded upload artifact exists without an unintended permanent screenshot. |
| WF-04 | External action returns a replacement file | Subsequent action/upload reads the replacement with the correct extension; only intended temporary input is removed. |
| WF-05 | Editor, Save As, before-upload and after-capture dialog cancellation | Each dialog follows its source-specific outcome; do not assume every cancellation aborts the entire pipeline. |
| WF-06 | Modify defaults while jobs are queued/running | Each job uses its snapshot; per-group override and default inheritance match reference. |
| WF-07 | Timeout, cancellation, secondary destination, local delete enabled | Outcome and retry/deletion behavior documented; no fabricated success or unintentional duplicate action. |
| EDT-01 | Every new-editor tool and every legacy region tool | Place/edit/remove/undo/redo/export; each supported parameter works, not just tool activation. |
| EDT-02 | All 232 new effects and 51 legacy effects | Known input + parameters + seed → expected decoded pixels/dimensions. Cover neutral/default/extreme valid values. |
| EDT-03 | Crop/cut-out plus undo after text/image/effect edits | Correct canvas coordinates and restored state across multiple operations. |
| EDT-04 | Transparency, sRGB/wide-gamut input, image orientation | Explicit profile and alpha handling; no black fringes or unintended tone shift. |
| EDT-05 | Background removal with fixed model/hash | Same preprocessing and model; CPU reference mask passes declared tolerance; CoreML checked separately. |
| EDT-06 | Cursor, emoji, text and font fallback | Actual rendered output checked; operating-system asset differences listed. |
| UPL-01 | .sxcu all six body modes and destination flags | Method, URL, query, headers, body and result fields match a local recording endpoint. |
| UPL-02 | Every custom syntax function, aliases, nesting, escaping | Expected output/error from the source parser; URL/body encoding applied at the correct layer. |
| UPL-03 | JSONPath, regex groups, XML, empty response and error response | Correct extraction; malformed values fail visibly; regex timeout and XXE protection tested. |
| UPL-04 | Each of 74 registered services | Own protocol fixtures, configuration mapping, success/error path, credential-redacted diagnostics; live status separate. |
| UPL-05 | FTP/FTPS/SFTP and shared-folder file | Transfer, progress, path encoding, collision and retry; certificate/host-key rejection works. |
| UPL-06 | OAuth cancellation, state mismatch, callback timeout, token refresh | No accepted foreign callback; listener closes; tokens remain in Keychain. |
| UPL-07 | Multipart binary data, redirects, large streamed file | Correct bytes and filename; no whole-file buffering; secrets not forwarded to unrelated redirect hosts. |
| MED-01 | 60-second recording with system audio + microphone | Streams exist and are intelligible; inspect timestamp continuity and synchronization markers. |
| MED-02 | Two pauses, resume, then stop | Paused time absent from all tracks; no drifting audio or invalid container finalization. |
| MED-03 | GIF with flat colors and sharp text | Correct dimensions, timing, palette/dither and loop settings; no lossy intermediate artifacts. |
| MED-04 | Abort, full disk, sleep, display unplug, encoder failure | Bounded cleanup; useful failure; recoverable artifact retained when appropriate. |
| MED-05 | Ten-minute recording on target laptop | Bounded memory/queue growth; no accumulating sync drift; measured CPU/storage behavior recorded. |
| SCR-01 | Scrolling fixtures with fixed header, repeated rows, slow content | Alignment/end detection works or produces an explicit adjustable partial result. |
| SCR-02 | Focus loss and target changing during scroll | Stop or recover deliberately; never silently stitch another application's content. |
| AUT-01 | Auto capture interval, stop, sleep and slow upload | No uncontrolled overlap or self-triggering watch-folder loop. |
| SET-01 | Settings import/export, all override groups and unknown keys | Safe data preserved; nonportable values explained; source defaults and numeric enum identities retained. |
| SET-02 | Windows DPAPI-protected credentials | Report reauthentication/Windows export requirement; no silent empty-token import. |
| SET-03 | .sxie, backup archive, path traversal and type metadata | Correct known types; unknown/malicious entries rejected without writing outside import area. |
| HIS-01 | SQLite and legacy history import | Dates, links, deletion links, tags and original paths retained; missing files tolerated. |
| HIS-02 | History edit/delete/export and crash during save | Atomicity and rollback; deleting a row is distinct from deleting a local or remote file. |
| INT-01 | Hotkeys, keyboard layout change, reserved combination | Correct registration/unregistration/conflict display; no need to read unrelated global keystrokes. |
| INT-02 | CLI file/URL, named task/workflow, every HotkeyType command | Same dispatch and parameter handling; paths with quotes/Unicode safe; existing app receives request. |
| INT-03 | Browser native messaging | Correct length framing and allowlisted extension identity; malformed/oversized messages rejected. |
| INT-04 | Clipboard image/file/text/HTML, drag-drop, pin panels | Correct content types and workflow routing; no unintended clipboard mutation by passive viewing. |
| UI-01 | Main menu, settings tabs, editor toolbar and history | Compare Windows reference at matched theme/scale; native-chrome differences documented. |
| UI-02 | Keyboard-only, VoiceOver, Dutch and English, IME | Reachable actions, readable labels, focus behavior and text entry. |
| PKG-01 | Launch moved .app from final install location | No developer checkout/runtime dependency, correct architecture/load paths/permission identity. |
| PKG-02 | Relaunch, login item, upgrade and rollback | Settings/history survive; capture hotkeys remain registered appropriately; previous data restore works. |

## Performance targets

These are proposed acceptance budgets, not measured claims. Establish stage-zero baselines on the actual laptop and record any justified changes. Target warm region overlay visibility within 250 ms; ordinary capture-to-clipboard within 500 ms excluding dialogs/OCR/upload; responsive editing at approximately 60 Hz for ordinary screenshots; a 10-minute 1080p30 recording without growing frame queues and audio-marker drift exceeding 100 ms. Large effects, scrolling stitches, high-resolution recordings and background models need progress/cancellation and bounded memory even when they exceed interactive latency budgets.

For pixel comparisons, exact equality is appropriate only for deterministic paths with matched library versions, seeds, color configuration and no font/OS dependence. Define tolerance with the fixture; do not inflate tolerance merely to hide a failure. Compare decoded media and timing rather than compressed file hashes when encoding metadata or implementation differs.

## Test execution boundaries

Pure C# tests and local HTTP fixtures can run without capture permissions. Apple capture/audio, AppKit, hotkeys, Keychain, screen geometry, signing and UI checks require a Mac. Permission dialogs and protected-content limitations require manual observations. GUI tests run from the real application bundle; headless CI is not proof of screen-capture correctness.

The design package does not invent build commands for projects that do not exist yet. Once the solution and native project are created, record the exact `dotnet test`, native test and packaging commands in `CLAUDE.md` and scripts. Run changed-area checks and the relevant integration gates; repeat or broaden them only to address a failure, dependency change, or concrete remaining risk.

## Final report

List counts separately for commands, task flags, editor tools, effects, settings, and destinations. Include original totals and every unresolved ID. Attach evidence paths, OS/toolchain, install path, known limitations, and rollback instructions. A feature with an unavailable external provider remains in the denominator and is reported as such; it does not disappear from the release's scope.
