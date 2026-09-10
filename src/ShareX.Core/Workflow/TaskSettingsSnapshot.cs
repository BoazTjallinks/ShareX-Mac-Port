using ShareX.Core.Enums;

namespace ShareX.Core.Workflow;

/// <summary>
/// Immutable, resolved settings for ONE job.
///
/// PROJECT-SPEC.md section 8: settings are resolved into a snapshot *before*
/// execution, and later edits cannot mutate a running task. The resolution rules
/// themselves are characterised in planning/task-settings-resolution.md.
/// </summary>
public sealed record TaskSettingsSnapshot
{
    /// <summary>Upstream default: CopyImageToClipboard | SaveImageToFile.</summary>
    public AfterCaptureTasks AfterCaptureJob { get; init; } =
        AfterCaptureTasks.CopyImageToClipboard | AfterCaptureTasks.SaveImageToFile;

    /// <summary>Upstream default: CopyURLToClipboard.</summary>
    public AfterUploadTasks AfterUploadJob { get; init; } = AfterUploadTasks.CopyURLToClipboard;

    public TaskJob Job { get; init; } = TaskJob.Job;

    /// <summary>Root of the screenshots folder; the dated subfolder is derived.</summary>
    public string ScreenshotsFolder { get; init; } = "";

    /// <summary>Upstream's default filename pattern is %y-%mo-%d_%h-%mi-%s.</summary>
    public string FileNamePattern { get; init; } = "%y-%mo-%d_%h-%mi-%s";

    public FileExistAction FileExistAction { get; init; } = FileExistAction.Ask;

    /// <summary>Description shown in the task list; falls back to the command name.</summary>
    public string Description { get; init; } = "";

    public bool ShowCursor { get; init; }

    public int ThumbnailWidth { get; init; } = 200;
    public int ThumbnailHeight { get; init; }
    public string ThumbnailName { get; init; } = "-thumbnail";
    public bool ThumbnailCheckSize { get; init; }
}
