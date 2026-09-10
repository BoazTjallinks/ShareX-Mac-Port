# Module interfaces and data contracts

These are proposed implementation contracts, not compiled application code. Confirm concrete API/SDK signatures during stage 0. Keep the domain types independent of WinForms, AppKit and Avalonia so the same fixtures can exercise them without a GUI.

## Service ownership

| Interface | Inputs | Result | Lifetime / failure rule |
| --- | --- | --- | --- |
| `ICommandDispatcher` | Stable ShareX command ID, optional arguments, workflow ID | Job ID or structured invalid-command result | CLI, hotkey, toolbar and menu all use this dispatcher. |
| `ITaskSettingsResolver` | Default settings plus selected workflow | Immutable `TaskSettingsSnapshot` | Preserve source group overrides and capture resolved values before work starts. |
| `ICaptureService` | `CaptureRequest`, cancellation | `CaptureArtifact` | Native target selection/permissions; cancellation creates no successful artifact. |
| `IRegionSelector` | Display snapshots, source region options | Selection mask/geometry or cancellation | Owns overlays only for selection duration; excludes them from capture. |
| `IRecordingService` | `RecordingRequest` | `RecordingSession` with state/events | One owner for streams/encoder; pause/resume/stop/abort idempotence and valid transitions. |
| `IImageEditorService` | Owned image, editor mode/options, task context | Edited image plus Continue/Cancel/Standalone outcome | Closing a window is not automatically the same outcome for every source dialog. |
| `IImageEffectRunner` | Ordered preset, image, deterministic seed/context | New owned image | Returns original only when algorithm contract permits; manages intermediate buffers. |
| `IWorkflowRunner` | Artifact/input, task snapshot | `TaskResult` and stage trace | Encodes source control flow; never infers order by sorting enum flags. |
| `IUploaderRegistry` | Source provider enum or custom role | Adapter and option schema | Explicit registration replaces unsafe reflection where practical, while preserving IDs. |
| `IUploadAdapter` | Stream/payload descriptor, provider config, credential reference | `UploadResult` | Cancellation/progress/unknown outcome distinguished; preserve thumbnail/deletion links. |
| `ICustomUploaderEvaluator` | `.sxcu`, input metadata, response context | Request plan / parsed result | Exact source grammar; side-effectful input/output functions call an injected dialog service. |
| `ICredentialStore` | Opaque credential reference | Secret in memory only when needed | Keychain; no plaintext in JSON, logs or source control. |
| `IHistoryStore` | Source-compatible history record/query | Rows / persistence outcome | SQLite transaction boundaries and schema version independent of UI. |
| `IClipboardService` | Typed representations / read request | Clipboard content snapshot/result | Preserve image/text/file/HTML distinctions and source precedence. |
| `IExternalActionRunner` | Enabled action, explicit argv, scoped artifact | Exit result and optional replacement artifact | No shell interpolation; cancel only the action's process tree. |
| `IPlatformCapabilities` | Feature ID and current context | Available / permission-needed / denied / unsupported | Permission state is not cached permanently; recheck after relevant OS events. |

## Domain records

```csharp
// Contract sketch: define referenced records as part of implementation.
public interface ICaptureService
{
    Task<CaptureArtifact> CaptureAsync(
        CaptureRequest request, CancellationToken cancellationToken);
}

public interface IWorkflowRunner
{
    Task<TaskResult> RunAsync(
        TaskInput input,
        TaskSettingsSnapshot settings,
        IProgress<TaskEvent> progress,
        CancellationToken cancellationToken);
}

public interface IUploadAdapter
{
    string StableId { get; }
    Task<UploadResult> UploadAsync(
        UploadRequest request,
        IProgress<TransferProgress> progress,
        CancellationToken cancellationToken);
}
```

`CaptureRequest` carries the command/capture kind, resolved display/window identity or global selection geometry, delay, cursor choice, supported window options and output policy. `CaptureArtifact` owns pixel storage and immutable metadata. Keep point bounds, pixel bounds and transform identifiers as different fields/types rather than a single ambiguous rectangle.

`TaskInput` is a tagged union of image, file, text, URL, download request or share request. `TaskSettingsSnapshot` includes the resolved destinations and settings groups plus the originating workflow/version, but not embedded plaintext credentials. `TaskResult` includes final state, artifact references, provider outcome, warnings and stage errors. `UploadResult` preserves original URL, shortened URL, thumbnail URL, deletion URL/token reference, provider ID and sanitized remote error; no generic single `url` string should discard those fields.

An artifact is distinct from a filename. A capture can live only in memory, a temporary encode can exist without permanent Save Image, and a saved file can be replaced by an action. Track ownership explicitly: external user file, permanent output, owned temporary file, or borrowed source. Cleanup rules cannot infer ownership from a path prefix alone.

## Native ABI sketch

Use a small exported C interface from an Objective-C translation unit. Swift implementation details remain private. This sketch intentionally sends only low-rate control/completion data; it is not a video transport.

```c
#include <stdint.h>

typedef void (*sxm_completion_fn)(
    uint64_t operation_id,
    int32_t result_code,
    const uint8_t *utf8_result_json,
    uint64_t result_length,
    void *user_context);

uint32_t sxm_abi_version(void);

int32_t sxm_begin(
    const uint8_t *utf8_request_json,
    uint64_t request_length,
    sxm_completion_fn completion,
    void *user_context,
    uint64_t *out_operation_id);

int32_t sxm_cancel(uint64_t operation_id);
int32_t sxm_release(uint64_t operation_id);
```

Define versioned operation names and schemas for capture, recorder control, OCR, hotkey registration, capabilities, and system integrations. `sxm_begin` copies request data before returning. It returns either an immediate error with no callback, or a valid operation ID that will complete once asynchronously. Completion data is borrowed for the callback duration and must be copied before returning. Managed delegates/context remain alive until completion/unregistration acknowledgement. A cancellation request is not that acknowledgement. Release is legal only after the terminal event and must be idempotent.

Recording's stream events use a separate persistent subscription/session contract, not repeated completion of the one-shot callback. Low-rate state/progress events are bounded and coalesced; media data remains in native queues and native encoders. Keychain results need a separate bounded sensitive-data path with deliberate disposal and zero logging; do not mix secret values into generic diagnostic JSON.

Unknown ABI versions, operations and fields fail deterministically or use a documented forward-compatible rule. Request size, image dimensions, output locations and memory allocation are bounded. All native errors become structured domain errors; no exception crosses the ABI. AppKit operations dispatch to the existing main event loop, while capture/encoding work stays off the UI thread. Integration tests cover callback-before/after-cancel races, shutdown and repeat release.

## Configuration and history schema boundaries

Use compatibility DTOs for the source JSON and separate native settings where needed. For example, original Windows hotkey data remains available for export while `MacBinding` stores physical/logical key and modifiers. Original Windows screenshot folders remain in import metadata until the user maps them. An unavailable provider keeps its source configuration and explicit availability status.

Reuse the upstream History table, adding separate Mac-specific metadata/tables only through a versioned migration. Do not change an imported original database in place. Keep dates timezone-aware internally and preserve the original represented timestamp for roundtrips when interpretation is ambiguous. Remote deletion credentials are sensitive even when packaged as URL query strings; redact them from logs and shareable reports.

## Error taxonomy

Distinguish: `UserCancelled`, `PermissionRequired`, `PermissionDenied`, `UnsupportedCapability`, `TargetDisappeared`, `InvalidConfiguration`, `InvalidInput`, `CredentialRequired`, `RemoteRejected`, `RemoteOutcomeUnknown`, `LocalIOFailure`, `EncoderFailure`, and `InternalFailure`. UI copy can be friendlier, but these cases must remain distinguishable in tests and task traces. Unavailable features and failed network requests must not return successful empty artifacts.
