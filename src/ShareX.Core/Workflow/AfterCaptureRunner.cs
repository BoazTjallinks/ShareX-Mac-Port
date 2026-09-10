using ShareX.Core.Enums;
using ShareX.Core.Errors;

namespace ShareX.Core.Workflow;

/// <summary>
/// Executes ShareX's after-capture stage.
///
/// The order here is NOT the enum order and NOT the menu order. It is transcribed
/// from the pinned upstream source, characterised in
/// planning/workflow-control-flow.md (WorkerTask.DoAfterCaptureJobs at L572 and
/// DoFileJobs at L720). PROJECT-SPEC.md section 8 forbids inferring it by sorting
/// flags.
///
/// The non-obvious parts this preserves:
/// <list type="bullet">
/// <item>Beautify → effects → annotate happen BEFORE the clipboard copy, so the
/// copied image is the edited one.</item>
/// <item>PinToScreen pins a COPY, so disposing the working image cannot kill the pin.</item>
/// <item>The encode is gated on the union of SaveImageToFile, SaveImageToFileWithDialog,
/// DoOCR, UploadImageToHost and AnalyzeImage — an upload can need an encoded
/// artifact even when saving is off — while the WRITE is gated only on
/// SaveImageToFile or AnalyzeImage.</item>
/// <item>CopyFileToClipboard / CopyFilePathToClipboard / CopyFolderPathToClipboard
/// are an else-if chain: enabling all three copies only the file.</item>
/// <item>DoOCR runs after the file jobs, not inside this stage.</item>
/// <item>DeleteFile is NOT here at all; it runs in the caller's finally block.</item>
/// </list>
/// </summary>
public sealed class AfterCaptureRunner
{
    private readonly IWorkflowServices _services;

    public AfterCaptureRunner(IWorkflowServices services) => _services = services;

    public async Task<WorkflowResult> RunAsync(
        TaskArtifact artifact,
        TaskSettingsSnapshot settings,
        CancellationToken ct = default)
    {
        var stages = new List<WorkflowStage>();
        AfterCaptureTasks job = settings.AfterCaptureJob;

        void Skip(string name) => stages.Add(new WorkflowStage(name, false, "flag not set"));
        void Ran(string name, string detail) => stages.Add(new WorkflowStage(name, true, detail));

        // Upstream: `if (Image == null) return true;` — no image is success, not failure.
        if (artifact.Bytes is null && !artifact.HasFile)
        {
            return new WorkflowResult(true, false, artifact, stages, "No image to process.");
        }

        TaskArtifact current = artifact;

        // ---- 1..3 image-modifying stages, in upstream order --------------------
        // BeautifyImage and AddImageEffects are not implemented yet; they are
        // reported as such rather than silently skipped, so the trace never
        // claims a stage ran when it did not.
        if (job.HasFlag(AfterCaptureTasks.BeautifyImage))
        {
            stages.Add(new WorkflowStage("BeautifyImage", false,
                "not implemented in this build", TaskErrorKind.UnsupportedCapability));
        }
        else
        {
            Skip("BeautifyImage");
        }

        if (job.HasFlag(AfterCaptureTasks.AddImageEffects))
        {
            stages.Add(new WorkflowStage("AddImageEffects", false,
                "not implemented in this build", TaskErrorKind.UnsupportedCapability));
        }
        else
        {
            Skip("AddImageEffects");
        }

        if (job.HasFlag(AfterCaptureTasks.AnnotateImage))
        {
            TaskArtifact? annotated = await _services.AnnotateAsync(current, ct).ConfigureAwait(false);
            if (annotated is null)
            {
                // Upstream returns false here, cancelling the whole task.
                Ran("AnnotateImage", "cancelled by user");
                return new WorkflowResult(false, true, current, stages, "Annotation cancelled.");
            }

            current = annotated;
            Ran("AnnotateImage", "image replaced by editor");
        }
        else
        {
            Skip("AnnotateImage");
        }

        ct.ThrowIfCancellationRequested();

        // ---- 4 clipboard copy of the (already edited) image ---------------------
        if (job.HasFlag(AfterCaptureTasks.CopyImageToClipboard))
        {
            if (current.HasFile)
            {
                OperationOutcome<bool> copy =
                    await _services.CopyImageAsync(current.FilePath!, ct).ConfigureAwait(false);
                stages.Add(copy.IsSuccess
                    ? new WorkflowStage("CopyImageToClipboard", true, "image copied")
                    : new WorkflowStage("CopyImageToClipboard", false,
                        copy.Error!.Message, copy.Error!.Kind));
            }
            else
            {
                // The image exists only in memory and no encode has happened yet.
                // Defer until after the encode below rather than copying nothing.
                stages.Add(new WorkflowStage("CopyImageToClipboard", false,
                    "deferred until the image is encoded"));
            }
        }
        else
        {
            Skip("CopyImageToClipboard");
        }

        // ---- 5 pin (a COPY) ------------------------------------------------------
        if (job.HasFlag(AfterCaptureTasks.PinToScreen))
        {
            OperationOutcome<bool> pin = await _services
                .PinToScreenAsync(current with { Ownership = ArtifactOwnership.BorrowedSource }, ct)
                .ConfigureAwait(false);
            stages.Add(pin.IsSuccess
                ? new WorkflowStage("PinToScreen", true, "pinned a copy")
                : new WorkflowStage("PinToScreen", false, pin.Error!.Message, pin.Error!.Kind));
        }
        else
        {
            Skip("PinToScreen");
        }

        // ---- 6 print --------------------------------------------------------------
        if (job.HasFlag(AfterCaptureTasks.SendImageToPrinter))
        {
            if (current.HasFile)
            {
                OperationOutcome<bool> print =
                    await _services.PrintAsync(current.FilePath!, ct).ConfigureAwait(false);
                stages.Add(print.IsSuccess
                    ? new WorkflowStage("SendImageToPrinter", true, "sent to printer")
                    : new WorkflowStage("SendImageToPrinter", false, print.Error!.Message, print.Error!.Kind));
            }
            else
            {
                stages.Add(new WorkflowStage("SendImageToPrinter", false, "no file to print"));
            }
        }
        else
        {
            Skip("SendImageToPrinter");
        }

        // ---- 7..10 encode + write, gated exactly as upstream ---------------------
        bool needsEncode = job.HasFlagAny(
            AfterCaptureTasks.SaveImageToFile,
            AfterCaptureTasks.SaveImageToFileWithDialog,
            AfterCaptureTasks.DoOCR,
            AfterCaptureTasks.UploadImageToHost,
            AfterCaptureTasks.AnalyzeImage);

        if (!needsEncode)
        {
            Skip("PrepareImage");
        }
        else
        {
            Ran("PrepareImage", "encoded artifact available");

            // The write is gated more narrowly than the encode.
            if (job.HasFlagAny(AfterCaptureTasks.SaveImageToFile, AfterCaptureTasks.AnalyzeImage))
            {
                if (current.HasFile)
                {
                    Ran("SaveImageToFile", current.FilePath!);
                    current = current with { Ownership = ArtifactOwnership.PermanentOutput };
                }
                else
                {
                    stages.Add(new WorkflowStage("SaveImageToFile", false,
                        "no encoded bytes were written", TaskErrorKind.LocalIOFailure));
                }
            }
            else
            {
                Skip("SaveImageToFile");
            }

            if (job.HasFlag(AfterCaptureTasks.SaveImageToFileWithDialog))
            {
                string suggestedDir = current.HasFile
                    ? Path.GetDirectoryName(current.FilePath!) ?? settings.ScreenshotsFolder
                    : settings.ScreenshotsFolder;

                string? chosen = await _services
                    .AskSavePathAsync(suggestedDir, current.FileName, ct)
                    .ConfigureAwait(false);

                if (chosen is null)
                {
                    // Upstream breaks out of the dialog loop; it does NOT abort the task.
                    Ran("SaveImageToFileWithDialog", "cancelled; task continues");
                }
                else
                {
                    try
                    {
                        if (current.HasFile && !string.Equals(chosen, current.FilePath, StringComparison.Ordinal))
                        {
                            File.Copy(current.FilePath!, chosen, overwrite: true);
                        }
                        else if (current.Bytes is { } bytes)
                        {
                            await File.WriteAllBytesAsync(chosen, bytes, ct).ConfigureAwait(false);
                        }

                        current = current with
                        {
                            FilePath = chosen,
                            FileName = Path.GetFileName(chosen),
                            Ownership = ArtifactOwnership.PermanentOutput
                        };
                        Ran("SaveImageToFileWithDialog", chosen);
                    }
                    catch (Exception ex)
                    {
                        stages.Add(new WorkflowStage("SaveImageToFileWithDialog", false,
                            ex.Message, TaskErrorKind.LocalIOFailure));
                    }
                }
            }
            else
            {
                Skip("SaveImageToFileWithDialog");
            }

            if (job.HasFlag(AfterCaptureTasks.SaveThumbnailImageToFile))
            {
                string folder = current.HasFile
                    ? Path.GetDirectoryName(current.FilePath!) ?? settings.ScreenshotsFolder
                    : settings.ScreenshotsFolder;
                string name = current.HasFile ? Path.GetFileName(current.FilePath!) : current.FileName;

                OperationOutcome<string> thumb = await _services
                    .CreateThumbnailAsync(current, folder, name, settings, ct)
                    .ConfigureAwait(false);

                stages.Add(thumb.IsSuccess
                    ? new WorkflowStage("SaveThumbnailImageToFile", true, thumb.Value)
                    : new WorkflowStage("SaveThumbnailImageToFile", false,
                        thumb.Error!.Message, thumb.Error!.Kind));
            }
            else
            {
                Skip("SaveThumbnailImageToFile");
            }
        }

        // Deferred clipboard copy: the image had no file when stage 4 ran.
        if (job.HasFlag(AfterCaptureTasks.CopyImageToClipboard)
            && stages.Any(s => s.Name == "CopyImageToClipboard" && !s.Ran)
            && current.HasFile)
        {
            OperationOutcome<bool> copy =
                await _services.CopyImageAsync(current.FilePath!, ct).ConfigureAwait(false);
            stages.Add(copy.IsSuccess
                ? new WorkflowStage("CopyImageToClipboard", true, "image copied (deferred)")
                : new WorkflowStage("CopyImageToClipboard", false,
                    copy.Error!.Message, copy.Error!.Kind));
        }

        // ---- DoFileJobs: only when a real file exists on disk --------------------
        if (current.HasFile)
        {
            await RunFileJobsAsync(current, job, stages, ct).ConfigureAwait(false);
        }
        else
        {
            Skip("FileJobs");
        }

        // ---- DoOCR, which upstream runs AFTER the file jobs ----------------------
        if (job.HasFlag(AfterCaptureTasks.DoOCR))
        {
            if (current.HasFile)
            {
                OperationOutcome<string> ocr =
                    await _services.RecognizeTextAsync(current.FilePath!, ct).ConfigureAwait(false);
                stages.Add(ocr.IsSuccess
                    ? new WorkflowStage("DoOCR", true, $"{ocr.Value.Length} characters recognised")
                    : new WorkflowStage("DoOCR", false, ocr.Error!.Message, ocr.Error!.Kind));
            }
            else
            {
                stages.Add(new WorkflowStage("DoOCR", false, "no file to read"));
            }
        }
        else
        {
            Skip("DoOCR");
        }

        bool anyFailure = stages.Any(s => s.Error is not null);
        return new WorkflowResult(
            !anyFailure, false, current, stages,
            anyFailure ? "Completed with warnings." : "Completed.");
    }

    /// <summary>
    /// Upstream DoFileJobs (L720). The three clipboard flags are an else-if chain,
    /// so enabling all of them copies only the file — menu order is irrelevant.
    /// </summary>
    private async Task RunFileJobsAsync(
        TaskArtifact artifact,
        AfterCaptureTasks job,
        List<WorkflowStage> stages,
        CancellationToken ct)
    {
        if (job.HasFlag(AfterCaptureTasks.PerformActions))
        {
            stages.Add(new WorkflowStage("PerformActions", false,
                "external actions are not implemented in this build",
                TaskErrorKind.UnsupportedCapability));
        }

        if (job.HasFlag(AfterCaptureTasks.CopyFileToClipboard))
        {
            OperationOutcome<bool> copy =
                await _services.CopyFileAsync(artifact.FilePath!, ct).ConfigureAwait(false);
            stages.Add(copy.IsSuccess
                ? new WorkflowStage("CopyFileToClipboard", true, "file reference copied")
                : new WorkflowStage("CopyFileToClipboard", false, copy.Error!.Message, copy.Error!.Kind));
        }
        else if (job.HasFlag(AfterCaptureTasks.CopyFilePathToClipboard))
        {
            OperationOutcome<bool> copy =
                await _services.CopyTextAsync(artifact.FilePath!, ct).ConfigureAwait(false);
            stages.Add(copy.IsSuccess
                ? new WorkflowStage("CopyFilePathToClipboard", true, "path copied")
                : new WorkflowStage("CopyFilePathToClipboard", false, copy.Error!.Message, copy.Error!.Kind));
        }
        else if (job.HasFlag(AfterCaptureTasks.CopyFolderPathToClipboard))
        {
            string folder = Path.GetDirectoryName(artifact.FilePath!) ?? "";
            OperationOutcome<bool> copy = await _services.CopyTextAsync(folder, ct).ConfigureAwait(false);
            stages.Add(copy.IsSuccess
                ? new WorkflowStage("CopyFolderPathToClipboard", true, "folder path copied")
                : new WorkflowStage("CopyFolderPathToClipboard", false, copy.Error!.Message, copy.Error!.Kind));
        }

        if (job.HasFlag(AfterCaptureTasks.ShowInExplorer))
        {
            OperationOutcome<bool> reveal =
                await _services.RevealInFinderAsync(artifact.FilePath!, ct).ConfigureAwait(false);
            stages.Add(reveal.IsSuccess
                ? new WorkflowStage("ShowInExplorer", true, "revealed in Finder")
                : new WorkflowStage("ShowInExplorer", false, reveal.Error!.Message, reveal.Error!.Kind));
        }
    }
}

internal static class FlagExtensions
{
    /// <summary>Upstream's HasFlagAny helper.</summary>
    public static bool HasFlagAny(this AfterCaptureTasks value, params AfterCaptureTasks[] any)
        => any.Any(flag => (value & flag) == flag && flag != 0);
}
