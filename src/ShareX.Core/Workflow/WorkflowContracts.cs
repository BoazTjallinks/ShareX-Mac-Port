using ShareX.Core.Enums;
using ShareX.Core.Errors;

namespace ShareX.Core.Workflow;

/// <summary>
/// How an artifact is owned, which decides whether cleanup may delete it.
/// docs/INTERFACES-AND-DATA.md: "Cleanup rules cannot infer ownership from a path
/// prefix alone."
/// </summary>
public enum ArtifactOwnership
{
    /// <summary>A file the user already had. Never deleted by a task.</summary>
    ExternalUserFile,

    /// <summary>A permanent output this task created in the screenshots folder.</summary>
    PermanentOutput,

    /// <summary>A temporary file this task created and may remove.</summary>
    OwnedTemporary,

    /// <summary>Borrowed from another stage; this task does not own its lifetime.</summary>
    BorrowedSource
}

/// <summary>
/// The thing a task is working on. An artifact is deliberately distinct from a
/// filename: a capture can live only in memory, an encode can exist without a
/// permanent save, and an external action can replace the file underneath.
/// </summary>
public sealed record TaskArtifact
{
    public byte[]? Bytes { get; init; }
    public string? FilePath { get; init; }
    public string FileName { get; init; } = "";
    public ArtifactOwnership Ownership { get; init; } = ArtifactOwnership.OwnedTemporary;
    public int PixelWidth { get; init; }
    public int PixelHeight { get; init; }
    public double Scale { get; init; } = 1;

    public bool HasFile => !string.IsNullOrEmpty(FilePath) && File.Exists(FilePath);
}

/// <summary>One executed stage, for the trace the acceptance tests assert on.</summary>
public sealed record WorkflowStage(
    string Name,
    bool Ran,
    string Detail,
    TaskErrorKind? Error = null);

public sealed record WorkflowResult(
    bool Success,
    bool Cancelled,
    TaskArtifact? Artifact,
    IReadOnlyList<WorkflowStage> Stages,
    string Summary,
    TaskError? Error = null);

/// <summary>
/// Platform services the runner needs. Injected so the whole workflow can be
/// exercised headlessly in tests without AppKit or a GUI.
/// </summary>
public interface IWorkflowServices
{
    Task<OperationOutcome<bool>> CopyImageAsync(string filePath, CancellationToken ct);
    Task<OperationOutcome<bool>> CopyTextAsync(string text, CancellationToken ct);
    Task<OperationOutcome<bool>> CopyFileAsync(string filePath, CancellationToken ct);
    Task<OperationOutcome<bool>> RevealInFinderAsync(string filePath, CancellationToken ct);
    Task<OperationOutcome<bool>> PrintAsync(string filePath, CancellationToken ct);

    /// <summary>Opens the annotation editor. Returns null when the user cancels.</summary>
    Task<TaskArtifact?> AnnotateAsync(TaskArtifact artifact, CancellationToken ct);

    /// <summary>Shows the save dialog. Returns null when the user cancels.</summary>
    Task<string?> AskSavePathAsync(string suggestedDirectory, string suggestedFileName, CancellationToken ct);

    /// <summary>Pins a COPY of the image to the screen.</summary>
    Task<OperationOutcome<bool>> PinToScreenAsync(TaskArtifact artifact, CancellationToken ct);

    /// <summary>Runs OCR and returns the recognised text.</summary>
    Task<OperationOutcome<string>> RecognizeTextAsync(string filePath, CancellationToken ct);

    /// <summary>Writes a thumbnail next to the output; returns its path.</summary>
    Task<OperationOutcome<string>> CreateThumbnailAsync(
        TaskArtifact artifact, string folder, string fileName, TaskSettingsSnapshot settings, CancellationToken ct);
}
