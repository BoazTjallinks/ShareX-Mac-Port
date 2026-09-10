# TaskSettings resolution — characterized from the pinned source

Source: `reference/ShareX/ShareX/TaskSettings.cs` @ `d2502561f63f…`.
PROJECT-SPEC.md section 8 requires resolving inherited defaults plus
workflow-specific overrides into an **immutable snapshot before execution**, and
lists the exact override groups. This is the extracted semantics.

## The override groups (13, exactly as the source declares them)

| Guard field | Governs | Default |
| --- | --- | --- |
| `UseDefaultAfterCaptureJob` | `AfterCaptureJob` | true |
| `UseDefaultAfterUploadJob` | `AfterUploadJob` | true |
| `UseDefaultDestinations` | `ImageDestination`, `ImageFileDestination`, `TextDestination`, `TextFileDestination`, `FileDestination`, `URLShortenerDestination`, `URLSharingServiceDestination` (7 fields, all or nothing) | true |
| `UseDefaultGeneralSettings` | `GeneralSettings` | true |
| `UseDefaultImageSettings` | `ImageSettings` | true |
| `UseDefaultCaptureSettings` | `CaptureSettings` | true |
| `UseDefaultUploadSettings` | `UploadSettings` | true |
| `UseDefaultActions` | `ExternalPrograms` | true |
| `UseDefaultToolsSettings` | `ToolsSettings` | true |
| `UseDefaultAdvancedSettings` | `AdvancedSettings` | true |
| `OverrideFTP` | `FTPIndex` | false |
| `OverrideCustomUploader` | `CustomUploaderIndex` | false |
| `OverrideScreenshotsFolder` | `ScreenshotsFolder` | false |

Plus `WatchFolderEnabled` / `WatchFolderList`, which participate in
`IsUsingDefaultSettings` but are not a copy-from-default group.

`IsUsingDefaultSettings` is the conjunction of all ten `UseDefault*` flags,
the negation of the three `Override*` flags, and `!WatchFolderEnabled`.

## `GetSafeTaskSettings(taskSettings)` — the snapshot factory

```
if (taskSettings.IsUsingDefaultSettings && DefaultTaskSettings != null) {
    safe = DefaultTaskSettings.Copy()          // wholesale copy of the defaults
    safe.Description = taskSettings.Description
    safe.Job         = taskSettings.Job        // only these two survive from the workflow
} else {
    safe = taskSettings.Copy()
    safe.SetDefaultSettings()                  // per-group fill-in, see below
}
safe.TaskSettingsReference = taskSettings      // back-pointer, NOT serialized
return safe
```

`SetDefaultSettings()` copies `DefaultTaskSettings.Copy()` group by group, but only
where the corresponding `UseDefault*` guard is true. Note it takes a *fresh copy of
the defaults per call* and assigns the group object by reference into the snapshot.

## The `…Reference` properties — the subtle part

`ImageSettingsReference`, `CaptureSettingsReference` and `ToolsSettingsReference`
are `[JsonIgnore]` computed properties:

```
get => UseDefaultXSettings ? Program.DefaultTaskSettings.XSettings
                           : TaskSettingsReference.XSettings;
```

They deliberately return the **live** settings object, not the snapshot's copy:

- `DoAfterCaptureJobs` step 2 applies image effects from `ImageSettingsReference`,
  so effect presets are read live at execution time.
- `DoFileJobs` step 4 reads `ToolsSettingsReference.AIOptions` live.
- Capture options are read through `CaptureSettingsReference`.

Consequence for the port: `TaskSettingsSnapshot` must carry **two** things per
these three groups — the frozen copy used for value semantics, and a resolved live
reference used exactly where upstream uses `…Reference`. Freezing everything would
change behaviour (a preset edited between capture and effect application would no
longer be picked up); freezing nothing would violate the spec's requirement that
"later settings edits cannot mutate a running upload".

The safe reading, and what this port implements: resolve the `…Reference` groups
once at snapshot construction (so a *running* task is stable) but resolve them
from the live default/workflow object rather than from the snapshot's own copy, so
the identity of the source matches upstream. Any divergence found by fixture
testing is recorded against the ledger, not silently accepted.

## `Cleanup()`

Before serialization, every group whose `UseDefault*` guard is true is set to
`null` so the JSON does not persist a redundant copy of the defaults. A loader
must therefore tolerate `null` groups and re-materialise them from the defaults —
a missing group is not an invalid file.

## Snapshot contract for `src/ShareX.Core`

- `TaskSettingsSnapshot` is immutable, created before the task starts, and carries
  the resolved workflow id/description, `Job`, the 13 resolved groups, the three
  live-reference groups, and the originating settings version.
- It never contains plaintext credentials — only credential references.
- Editing settings while a task runs must not change that task's snapshot.
- `Description` falls back to the localized description of `Job` when empty
  (`ToString()` in the source).
