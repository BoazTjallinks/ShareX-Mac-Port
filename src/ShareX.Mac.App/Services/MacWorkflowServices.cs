using ShareX.Core.Errors;
using ShareX.Core.Workflow;
using ShareX.Platform.Mac;

namespace ShareX.Mac.App.Services;

/// <summary>
/// Binds the portable workflow runner to real macOS services.
///
/// Anything not implemented yet returns an explicit
/// <see cref="TaskErrorKind.UnsupportedCapability"/> rather than a fake success,
/// so the workflow trace never claims a stage did something it did not
/// (PROJECT-SPEC.md: "Never replace incomplete behavior with no-op buttons").
/// </summary>
public sealed class MacWorkflowServices : IWorkflowServices
{
    private readonly MacClipboardService _clipboard;

    public MacWorkflowServices(MacClipboardService clipboard) => _clipboard = clipboard;

    public Task<OperationOutcome<bool>> CopyImageAsync(string filePath, CancellationToken ct)
        => _clipboard.CopyImageFileAsync(filePath, ct);

    public Task<OperationOutcome<bool>> CopyTextAsync(string text, CancellationToken ct)
        => _clipboard.CopyTextAsync(text, ct);

    public Task<OperationOutcome<bool>> CopyFileAsync(string filePath, CancellationToken ct)
        => _clipboard.CopyFileAsync(filePath, ct);

    public Task<OperationOutcome<bool>> RevealInFinderAsync(string filePath, CancellationToken ct)
        => _clipboard.RevealAsync(filePath, ct);

    public Task<OperationOutcome<bool>> PrintAsync(string filePath, CancellationToken ct)
        => Task.FromResult(Unsupported<bool>("Printing is not implemented in this build."));

    public Task<TaskArtifact?> AnnotateAsync(TaskArtifact artifact, CancellationToken ct)
        // Returning null means "cancelled", which upstream treats as aborting the
        // task. That is the honest outcome while the editor does not exist: it
        // must not silently pass the unedited image through as if annotated.
        => Task.FromResult<TaskArtifact?>(null);

    public Task<string?> AskSavePathAsync(string dir, string fileName, CancellationToken ct)
        => Task.FromResult<string?>(null);

    public Task<OperationOutcome<bool>> PinToScreenAsync(TaskArtifact artifact, CancellationToken ct)
        => Task.FromResult(Unsupported<bool>("Pin to screen is not implemented in this build."));

    public Task<OperationOutcome<string>> RecognizeTextAsync(string filePath, CancellationToken ct)
        => Task.FromResult(Unsupported<string>("OCR is not wired into the workflow yet."));

    public Task<OperationOutcome<string>> CreateThumbnailAsync(
        TaskArtifact artifact, string folder, string fileName,
        TaskSettingsSnapshot settings, CancellationToken ct)
        => Task.FromResult(Unsupported<string>("Thumbnail generation is not wired in yet."));

    private static OperationOutcome<T> Unsupported<T>(string message)
        => OperationOutcome<T>.Failure(new TaskError(TaskErrorKind.UnsupportedCapability, message));
}
