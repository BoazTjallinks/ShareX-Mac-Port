using Avalonia.Controls;
using Avalonia.Threading;
using ShareX.Core.Enums;
using ShareX.Core.Errors;
using ShareX.Core.Geometry;
using ShareX.Mac.App.Support;
using ShareX.Mac.App.Views;
using ShareX.Core.Workflow;
using ShareX.Platform.Mac;

namespace ShareX.Mac.App.Services;

public sealed record CommandRun(
    HotkeyType Command,
    bool Handled,
    bool Cancelled,
    string Message,
    string? FilePath = null,
    int PixelWidth = 0,
    int PixelHeight = 0,
    double Scale = 1,
    IReadOnlyList<WorkflowStage>? Stages = null);

/// <summary>
/// Dispatches ShareX commands. This is the single entry point every trigger uses
/// (rail button, menu item, hotkey, CLI), matching ICommandDispatcher in
/// docs/INTERFACES-AND-DATA.md.
///
/// Commands that are not implemented yet return <c>Handled = false</c> with a
/// clear message. They deliberately do NOT fail silently: PROJECT-SPEC.md forbids
/// "a control whose handler does nothing".
/// </summary>
public sealed class CaptureCommandService
{
    private readonly MacCaptureService _capture;
    private readonly Func<Window?> _ownerWindow;
    private readonly AfterCaptureRunner _afterCapture;

    /// <summary>
    /// Resolved settings for the next job. Upstream resolves these into an
    /// immutable snapshot before the task starts (PROJECT-SPEC.md section 8), so
    /// changing this between jobs cannot affect one already running.
    /// </summary>
    public TaskSettingsSnapshot Settings { get; set; } = new();

    /// <summary>
    /// Remembered region for <see cref="HotkeyType.LastRegion"/>. Stored with its
    /// coordinate space so it can be validated against the current layout rather
    /// than replayed blindly.
    /// </summary>
    public PointRect? LastRegion { get; private set; }

    public CaptureCommandService(
        MacCaptureService capture, IWorkflowServices workflowServices, Func<Window?> ownerWindow)
    {
        _capture = capture;
        _ownerWindow = ownerWindow;
        _afterCapture = new AfterCaptureRunner(workflowServices);
    }

    public async Task<CommandRun> ExecuteAsync(HotkeyType command, CancellationToken ct = default)
    {
        switch (command)
        {
            case HotkeyType.PrintScreen:
                return await CaptureTargetAsync(command, CaptureTarget.AllDisplays(), ct)
                    .ConfigureAwait(true);

            case HotkeyType.ActiveMonitor:
                return await CaptureTargetAsync(command, CaptureTarget.ActiveDisplay(), ct)
                    .ConfigureAwait(true);

            case HotkeyType.ActiveWindow:
                return await CaptureTargetAsync(command, CaptureTarget.ActiveWindow(), ct)
                    .ConfigureAwait(true);

            case HotkeyType.RectangleRegion:
                return await CaptureRegionAsync(command, RegionSelectorMode.Default, ct)
                    .ConfigureAwait(true);

            case HotkeyType.RectangleLight:
                return await CaptureRegionAsync(command, RegionSelectorMode.Light, ct)
                    .ConfigureAwait(true);

            case HotkeyType.RectangleTransparent:
                return await CaptureRegionAsync(command, RegionSelectorMode.Transparent, ct)
                    .ConfigureAwait(true);

            case HotkeyType.LastRegion:
                if (LastRegion is not { } remembered)
                {
                    return new CommandRun(command, Handled: true, Cancelled: true,
                        "No previous region has been selected yet.");
                }

                // Validate the remembered geometry against the CURRENT layout
                // instead of replaying it blindly (PROJECT-SPEC.md section 5).
                if (!await RegionStillValidAsync(remembered, ct).ConfigureAwait(true))
                {
                    return new CommandRun(command, Handled: true, Cancelled: true,
                        "The remembered region no longer fits the current display layout.");
                }

                return await CaptureTargetAsync(command, CaptureTarget.Region(remembered), ct)
                    .ConfigureAwait(true);

            default:
                return new CommandRun(command, Handled: false, Cancelled: false,
                    $"{command} is not implemented yet in this build.");
        }
    }

    private async Task<CommandRun> CaptureRegionAsync(
        HotkeyType command, RegionSelectorMode mode, CancellationToken ct)
    {
        OperationOutcome<IReadOnlyList<DisplayInfo>> displays =
            await _capture.ListDisplaysAsync(ct).ConfigureAwait(true);

        if (!displays.IsSuccess)
        {
            return Failure(command, displays.Error!);
        }

        // Select on the display holding the pointer, as upstream does.
        DisplayInfo target =
            displays.Value.FirstOrDefault(d => d.IsActive)
            ?? displays.Value.FirstOrDefault(d => d.IsMain)
            ?? displays.Value.FirstOrDefault()
            ?? throw new InvalidOperationException("displays.list returned no displays.");

        PointRect? selection = await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var selector = new RegionSelectorWindow(target.BoundsGlobalPoints, mode);

            // Position the overlay over the chosen display. One Avalonia DIP is
            // one AppKit point on macOS, so the display's point origin is also the
            // window position in DIPs.
            selector.Position = new Avalonia.PixelPoint(
                (int)Math.Round(target.BoundsGlobalPoints.X * target.Scale),
                (int)Math.Round(target.BoundsGlobalPoints.Y * target.Scale));

            Window? owner = _ownerWindow();
            if (owner is not null)
            {
                selector.Show(owner);
            }
            else
            {
                selector.Show();
            }

            return await selector.SelectionTask.ConfigureAwait(true);
        }).ConfigureAwait(true);

        if (selection is not { } region)
        {
            return new CommandRun(command, Handled: true, Cancelled: true,
                "Region selection cancelled.");
        }

        // Give the compositor a moment to actually remove the overlay before the
        // frame is grabbed. The native filter already excludes our windows; this
        // just avoids any visible flash in the result.
        await Task.Delay(60, ct).ConfigureAwait(true);

        LastRegion = region;
        return await CaptureTargetAsync(command, CaptureTarget.Region(region), ct)
            .ConfigureAwait(true);
    }

    private async Task<bool> RegionStillValidAsync(PointRect region, CancellationToken ct)
    {
        OperationOutcome<IReadOnlyList<DisplayInfo>> displays =
            await _capture.ListDisplaysAsync(ct).ConfigureAwait(true);

        return displays.IsSuccess
               && displays.Value.Any(d => d.BoundsGlobalPoints.Intersects(region));
    }

    private async Task<CommandRun> CaptureTargetAsync(
        HotkeyType command, CaptureTarget target, CancellationToken ct)
    {
        DateTime now = DateTime.Now;
        string folder = AppPaths.EnsureDirectory(AppPaths.ScreenshotsFolderFor(now));
        string fileName = $"{now:yyyy-MM-dd_HH-mm-ss}.png";
        string path = Path.Combine(folder, fileName);

        OperationOutcome<CaptureResult> outcome = await _capture
            .CaptureAsync(target, showsCursor: false, outputPath: path, ct: ct)
            .ConfigureAwait(true);

        if (!outcome.IsSuccess)
        {
            return Failure(command, outcome.Error!);
        }

        CaptureResult result = outcome.Value;
        string written = result.OutputPath ?? path;

        // A successful call that produced no bytes is a failure, not a success.
        if (!File.Exists(written) || new FileInfo(written).Length == 0)
        {
            return new CommandRun(command, Handled: true, Cancelled: false,
                "The capture reported success but no image bytes were written.");
        }

        // The capture is only the first half of the job. Upstream then runs the
        // after-capture stage, which is what actually copies to the clipboard,
        // reveals in Finder and so on.
        var artifact = new TaskArtifact
        {
            FilePath = written,
            FileName = Path.GetFileName(written),
            Ownership = ArtifactOwnership.PermanentOutput,
            PixelWidth = result.PixelWidth,
            PixelHeight = result.PixelHeight,
            Scale = result.CompositionScale
        };

        // Snapshot taken now, so a settings change mid-flight cannot affect it.
        TaskSettingsSnapshot snapshot = Settings;
        WorkflowResult workflow = await _afterCapture
            .RunAsync(artifact, snapshot, ct)
            .ConfigureAwait(true);

        if (workflow.Cancelled)
        {
            return new CommandRun(command, Handled: true, Cancelled: true,
                workflow.Summary, Stages: workflow.Stages);
        }

        string ran = string.Join(", ",
            workflow.Stages.Where(s => s.Ran).Select(s => s.Name));
        string summary = string.IsNullOrEmpty(ran)
            ? $"Saved {fileName}"
            : $"Saved {fileName} — {ran}";

        return new CommandRun(command, Handled: true, Cancelled: false,
            summary, workflow.Artifact?.FilePath ?? written,
            result.PixelWidth, result.PixelHeight, result.CompositionScale, workflow.Stages);
    }

    private static CommandRun Failure(HotkeyType command, TaskError error)
    {
        string message = error.Kind switch
        {
            TaskErrorKind.PermissionRequired =>
                "Screen Recording permission is required. macOS has stored a decision for "
                + "ShareX-Mac, so grant it in System Settings → Privacy & Security → "
                + "Screen & System Audio Recording, then relaunch.",
            TaskErrorKind.PermissionDenied =>
                "Screen Recording is denied for ShareX-Mac. Enable it in System Settings → "
                + "Privacy & Security → Screen & System Audio Recording.",
            TaskErrorKind.TargetDisappeared =>
                "The capture target no longer exists.",
            TaskErrorKind.UserCancelled => "Cancelled.",
            _ => $"{error.Kind}: {error.Message}"
        };

        return new CommandRun(command, Handled: true,
            Cancelled: error.Kind == TaskErrorKind.UserCancelled, message);
    }
}
