using ShareX.Core.Enums;
using ShareX.Core.Errors;
using ShareX.Core.Workflow;
using Xunit;

namespace ShareX.Core.Tests;

/// <summary>
/// Records every service call in order, so tests can assert the workflow's real
/// control flow rather than just its end state.
/// </summary>
internal sealed class RecordingServices : IWorkflowServices
{
    public List<string> Calls { get; } = new();
    public TaskArtifact? AnnotateResult { get; set; }
    public string? SavePathResult { get; set; }
    public bool AnnotateCalled { get; private set; }

    public Task<OperationOutcome<bool>> CopyImageAsync(string filePath, CancellationToken ct)
    {
        Calls.Add($"CopyImage:{Path.GetFileName(filePath)}");
        return Task.FromResult(OperationOutcome<bool>.Success(true));
    }

    public Task<OperationOutcome<bool>> CopyTextAsync(string text, CancellationToken ct)
    {
        Calls.Add($"CopyText:{text}");
        return Task.FromResult(OperationOutcome<bool>.Success(true));
    }

    public Task<OperationOutcome<bool>> CopyFileAsync(string filePath, CancellationToken ct)
    {
        Calls.Add($"CopyFile:{Path.GetFileName(filePath)}");
        return Task.FromResult(OperationOutcome<bool>.Success(true));
    }

    public Task<OperationOutcome<bool>> RevealInFinderAsync(string filePath, CancellationToken ct)
    {
        Calls.Add("Reveal");
        return Task.FromResult(OperationOutcome<bool>.Success(true));
    }

    public Task<OperationOutcome<bool>> PrintAsync(string filePath, CancellationToken ct)
    {
        Calls.Add("Print");
        return Task.FromResult(OperationOutcome<bool>.Success(true));
    }

    public Task<TaskArtifact?> AnnotateAsync(TaskArtifact artifact, CancellationToken ct)
    {
        Calls.Add("Annotate");
        AnnotateCalled = true;
        return Task.FromResult(AnnotateResult);
    }

    public Task<string?> AskSavePathAsync(string dir, string file, CancellationToken ct)
    {
        Calls.Add("AskSavePath");
        return Task.FromResult(SavePathResult);
    }

    public Task<OperationOutcome<bool>> PinToScreenAsync(TaskArtifact artifact, CancellationToken ct)
    {
        // Records the ownership it was handed, to prove a copy is pinned.
        Calls.Add($"Pin:{artifact.Ownership}");
        return Task.FromResult(OperationOutcome<bool>.Success(true));
    }

    public Task<OperationOutcome<string>> RecognizeTextAsync(string filePath, CancellationToken ct)
    {
        Calls.Add("OCR");
        return Task.FromResult(OperationOutcome<string>.Success("recognised"));
    }

    public Task<OperationOutcome<string>> CreateThumbnailAsync(
        TaskArtifact artifact, string folder, string fileName, TaskSettingsSnapshot settings, CancellationToken ct)
    {
        Calls.Add("Thumbnail");
        return Task.FromResult(OperationOutcome<string>.Success(Path.Combine(folder, "thumb.png")));
    }
}

/// <summary>
/// Asserts the after-capture stage against the control flow extracted from the
/// pinned upstream source (planning/workflow-control-flow.md, WorkerTask L572+).
/// These are parity assertions: PROJECT-SPEC.md section 8 forbids inferring the
/// order from the enum or the menu, so each test names the upstream behaviour it
/// pins.
/// </summary>
public sealed class AfterCaptureRunnerTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("sharex-wf-").FullName;

    private TaskArtifact FileArtifact(string name = "shot.png")
    {
        string path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, new byte[] { 1, 2, 3, 4 });
        return new TaskArtifact
        {
            FilePath = path,
            FileName = name,
            Ownership = ArtifactOwnership.PermanentOutput,
            PixelWidth = 10,
            PixelHeight = 10
        };
    }

    private static TaskSettingsSnapshot With(AfterCaptureTasks job) =>
        new() { AfterCaptureJob = job };

    public void Dispose() => Directory.Delete(_dir, true);

    [Fact]
    public async Task DefaultFlags_CopyImageAndSave()
    {
        // Upstream default is CopyImageToClipboard | SaveImageToFile.
        var services = new RecordingServices();
        var runner = new AfterCaptureRunner(services);

        WorkflowResult result = await runner.RunAsync(FileArtifact(), new TaskSettingsSnapshot());

        Assert.True(result.Success);
        Assert.Contains(services.Calls, c => c.StartsWith("CopyImage:"));
        Assert.Contains(result.Stages, s => s.Name == "SaveImageToFile" && s.Ran);
    }

    [Fact]
    public async Task AnnotateRunsBeforeClipboardCopy()
    {
        // Upstream applies beautify/effects/annotate BEFORE copying, so the
        // clipboard holds the EDITED image. Getting this backwards is a classic
        // porting error.
        var services = new RecordingServices();
        string edited = Path.Combine(_dir, "edited.png");
        File.WriteAllBytes(edited, new byte[] { 9 });
        services.AnnotateResult = new TaskArtifact
        {
            FilePath = edited, FileName = "edited.png", Ownership = ArtifactOwnership.OwnedTemporary
        };

        var runner = new AfterCaptureRunner(services);
        await runner.RunAsync(FileArtifact(),
            With(AfterCaptureTasks.AnnotateImage | AfterCaptureTasks.CopyImageToClipboard));

        int annotate = services.Calls.IndexOf("Annotate");
        int copy = services.Calls.FindIndex(c => c.StartsWith("CopyImage:"));

        Assert.True(annotate >= 0 && copy >= 0);
        Assert.True(annotate < copy, "Annotate must run before the clipboard copy.");
        Assert.Equal("CopyImage:edited.png", services.Calls[copy]);
    }

    [Fact]
    public async Task AnnotateCancellation_AbortsTheWholeTask()
    {
        // Upstream returns false from DoAfterCaptureJobs when the editor returns
        // no image, which cancels the task.
        var services = new RecordingServices { AnnotateResult = null };
        var runner = new AfterCaptureRunner(services);

        WorkflowResult result = await runner.RunAsync(FileArtifact(),
            With(AfterCaptureTasks.AnnotateImage | AfterCaptureTasks.CopyImageToClipboard));

        Assert.False(result.Success);
        Assert.True(result.Cancelled);
        Assert.DoesNotContain(services.Calls, c => c.StartsWith("CopyImage:"));
    }

    [Fact]
    public async Task ThreeClipboardFlags_AreMutuallyExclusive_FileWins()
    {
        // Upstream uses an else-if chain: CopyFile beats CopyFilePath beats
        // CopyFolderPath. Enabling all three must copy ONLY the file.
        var services = new RecordingServices();
        var runner = new AfterCaptureRunner(services);

        await runner.RunAsync(FileArtifact(),
            With(AfterCaptureTasks.CopyFileToClipboard
                 | AfterCaptureTasks.CopyFilePathToClipboard
                 | AfterCaptureTasks.CopyFolderPathToClipboard));

        Assert.Contains(services.Calls, c => c.StartsWith("CopyFile:"));
        Assert.DoesNotContain(services.Calls, c => c.StartsWith("CopyText:"));
    }

    [Fact]
    public async Task CopyFilePath_WinsOverFolderPath()
    {
        var services = new RecordingServices();
        var runner = new AfterCaptureRunner(services);

        await runner.RunAsync(FileArtifact(),
            With(AfterCaptureTasks.CopyFilePathToClipboard
                 | AfterCaptureTasks.CopyFolderPathToClipboard));

        string copied = Assert.Single(services.Calls.Where(c => c.StartsWith("CopyText:")));
        Assert.EndsWith("shot.png", copied);
    }

    [Fact]
    public async Task PinToScreen_ReceivesACopy_NotTheLiveArtifact()
    {
        // Upstream pins Image.CloneSafe() so later disposal cannot kill the pin.
        var services = new RecordingServices();
        var runner = new AfterCaptureRunner(services);

        await runner.RunAsync(FileArtifact(), With(AfterCaptureTasks.PinToScreen));

        Assert.Contains($"Pin:{ArtifactOwnership.BorrowedSource}", services.Calls);
    }

    [Fact]
    public async Task UploadWithoutSave_StillEncodes_ButDoesNotClaimASave()
    {
        // Upstream gates the ENCODE on the union including UploadImageToHost, but
        // gates the WRITE only on SaveImageToFile/AnalyzeImage.
        var services = new RecordingServices();
        var runner = new AfterCaptureRunner(services);

        WorkflowResult result = await runner.RunAsync(FileArtifact(),
            With(AfterCaptureTasks.UploadImageToHost));

        Assert.Contains(result.Stages, s => s.Name == "PrepareImage" && s.Ran);
        Assert.DoesNotContain(result.Stages, s => s.Name == "SaveImageToFile" && s.Ran);
    }

    [Fact]
    public async Task NoEncodeFlags_SkipsPrepareImageEntirely()
    {
        var services = new RecordingServices();
        var runner = new AfterCaptureRunner(services);

        WorkflowResult result = await runner.RunAsync(FileArtifact(),
            With(AfterCaptureTasks.CopyImageToClipboard));

        Assert.Contains(result.Stages, s => s.Name == "PrepareImage" && !s.Ran);
    }

    [Fact]
    public async Task Ocr_RunsAfterFileJobs()
    {
        // Upstream calls DoOCR in DoThreadJob AFTER DoFileJobs, not inside the
        // after-capture block.
        var services = new RecordingServices();
        var runner = new AfterCaptureRunner(services);

        await runner.RunAsync(FileArtifact(),
            With(AfterCaptureTasks.DoOCR | AfterCaptureTasks.ShowInExplorer));

        int reveal = services.Calls.IndexOf("Reveal");
        int ocr = services.Calls.IndexOf("OCR");

        Assert.True(reveal >= 0 && ocr >= 0);
        Assert.True(reveal < ocr, "DoOCR must run after the file jobs.");
    }

    [Fact]
    public async Task SaveWithDialogCancelled_DoesNotAbortTheTask()
    {
        // Upstream breaks out of the dialog loop; it does not cancel the task.
        var services = new RecordingServices { SavePathResult = null };
        var runner = new AfterCaptureRunner(services);

        WorkflowResult result = await runner.RunAsync(FileArtifact(),
            With(AfterCaptureTasks.SaveImageToFileWithDialog | AfterCaptureTasks.ShowInExplorer));

        Assert.False(result.Cancelled);
        Assert.Contains(services.Calls, c => c == "Reveal");
    }

    [Fact]
    public async Task DeleteFile_IsNotPartOfTheAfterCaptureStage()
    {
        // Upstream performs DeleteFile in ThreadDoWork's finally, after Dispose
        // and after any upload — never inside DoAfterCaptureJobs.
        var services = new RecordingServices();
        var runner = new AfterCaptureRunner(services);
        TaskArtifact artifact = FileArtifact();

        await runner.RunAsync(artifact, With(AfterCaptureTasks.DeleteFile));

        Assert.True(File.Exists(artifact.FilePath!),
            "The after-capture stage must not delete the file; that happens later.");
    }

    [Fact]
    public async Task NoImage_IsSuccess_NotFailure()
    {
        var services = new RecordingServices();
        var runner = new AfterCaptureRunner(services);

        WorkflowResult result = await runner.RunAsync(
            new TaskArtifact { FileName = "none.png" }, new TaskSettingsSnapshot());

        Assert.True(result.Success);
    }
}
