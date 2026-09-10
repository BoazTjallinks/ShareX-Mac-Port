# WorkerTask control flow — characterized from the pinned source

Source of truth: `reference/ShareX/ShareX/WorkerTask.cs` at commit
`d2502561f63fc3ff502cacd91514e3f7f2948c74`. Line numbers below refer to that file.

PROJECT-SPEC.md section 8 forbids inferring this order from the enum or the menu.
This document is the extracted real order. `src/ShareX.Core`'s workflow runner
implements *this*, and `tests/ShareX.Core.Tests` asserts against *this*.

## Top level — `ThreadDoWork()` (L304)

```
CreateTaskReferenceHelper()
try {
    StopRequested = !DoThreadJob()            // L308
    OnImageReady()                            // L310  — fires even when stopped
    if (!StopRequested) {
        if (Info.IsUploadJob && IsUploadAllowed())  DoUploadJob()
        else                                        Info.Result.IsURLExpected = false
    }
}
finally {
    KeepImage = Image != null && GeneralSettings.ShowToastNotificationAfterTaskCompleted
    Dispose()
    // Undo an early clipboard copy that turned out to lead nowhere:
    if (EarlyURLCopied && (StopRequested || Result == null || Result.URL is empty)
        && Clipboard.ContainsText())  Clipboard.Clear()
    // DELETION IS HERE — outside DoAfterCaptureJobs, after Dispose:
    if ((Job == Job || (Job == FileUpload && Advanced.UseAfterCaptureTasksDuringFileUpload))
        && AfterCaptureJob.HasFlag(DeleteFile) && FilePath exists)  File.Delete(FilePath)
}
if (!StopRequested && Result != null && Result.IsURLExpected && !Result.IsError) {
    if (Result.URL is empty) AddErrorMessage("URL is empty")
    else                     DoAfterUploadJobs()
}
```

Consequences the runner must preserve:
- `AfterCaptureTasks.DeleteFile` is **not** part of the after-capture stage. It runs
  in the `finally`, after upload, after `Dispose()`, and only for `TaskJob.Job` or
  for `FileUpload` when `UseAfterCaptureTasksDuringFileUpload` is set.
- `OnImageReady()` fires before the upload decision and regardless of `StopRequested`.
- `DoAfterUploadJobs()` is outside the `try/finally` and is gated on four conditions.

## `DoThreadJob()` (L520)

```
1. if (IsUploadJob && Advanced.AutoClearClipboard)  Clipboard.Clear()
2. if (Job == Download || DownloadUpload) {
       downloadResult = DownloadFromURL(upload: Job == DownloadUpload)
       if (!downloadResult) return false
       if (Job == Download)  return true          // download-only stops here
   }
3. if (Job == Job) {
       if (!DoAfterCaptureJobs()) return false
       DoFileJobs()
   }
   else if (Job == TextUpload && Text non-empty)  DoTextJobs()
   else if (Job == FileUpload && Advanced.UseAfterCaptureTasksDuringFileUpload)  DoFileJobs()
4. if (AfterCaptureJob.HasFlag(DoOCR))  DoOCR()        // LATE — after file jobs
5. if (IsUploadJob && Data != null && Data.CanSeek)  Data.Position = 0
return true
```

`DoOCR` sits at step 4, not inside the after-capture block, even though `DoOCR`
also participates in the "does this need an encoded stream" test in step 3.

## `DoAfterCaptureJobs()` (L572) — exact order

Returns `false` (cancelling the whole task) where noted.

| # | Flag | Behaviour | Cancels on null image |
| --- | --- | --- | --- |
| 0 | — | `if (Image == null) return true` — no image means success, not failure | — |
| 1 | `BeautifyImage` | `Image = BeautifyImage(Image, settings)` | yes |
| 2 | `AddImageEffects` | `Image = ApplyImageEffects(Image, ImageSettingsReference)` | yes (logs "resulted empty image") |
| 3 | `AnnotateImage` | `Image = AnnotateImage(Image, null, settings, true)` — opens the editor | yes |
| 4 | `CopyImageToClipboard` | copy the **already beautified/effected/annotated** image | — |
| 5 | `PinToScreen` | pins a `CloneSafe()` **copy**, not the live image | — |
| 6 | `SendImageToPrinter` | `PrintImage(Image)` | — |
| 7 | — | `Info.Metadata.Image = Image` | — |

Then, **only if** `AfterCaptureJob` has any of
`SaveImageToFile | SaveImageToFileWithDialog | DoOCR | UploadImageToHost | AnalyzeImage`:

```
imageData = PrepareImage(Image, settings)          // encode
Data      = imageData.ImageStream
FileName  = ChangeExtension(FileName, imageData.ImageFormat)

8.  if (SaveImageToFile || AnalyzeImage) {
        folder   = GetScreenshotsFolder(settings, metadata)
        filePath = HandleExistsFile(folder, FileName, settings)   // FileExistAction
        if (filePath non-empty) { FilePath = filePath; imageData.Write(FilePath) }
    }
9.  if (SaveImageToFileWithDialog) {
        loop { show save dialog; on OK write and set FilePath; on cancel break }
    }
10. if (SaveThumbnailImageToFile) {
        base on FilePath when set, else FileName + screenshots folder
        ThumbnailFilePath = CreateThumbnail(Image, folder, name, settings)
    }
```

Contract points:
- An upload can require an encoded artifact **even when `SaveImageToFile` is off** —
  the encode is gated on the union above, the write is gated on `SaveImageToFile`
  or `AnalyzeImage`.
- `AnalyzeImage` forces a real file to exist, because step 8's condition includes it.
- `PinToScreen` gets a clone, so later disposal of `Image` does not kill the pin.

## `DoFileJobs()` (L720) — requires `FilePath` to exist on disk

```
if (FilePath is empty || !File.Exists(FilePath))  return    // whole method is a no-op

1. if (PerformActions && ExternalPrograms != null) {
       for each active action:
           modifiedPath = action.Run(FilePath)
           if (modifiedPath non-empty) {
               isFileModified = true
               FilePath = modifiedPath
               Data?.Dispose()
               action.DeletePendingInputFile()
           }
       if (isFileModified) {
           FileName = ChangeFileNameExtension(originalFileName, extension of new FilePath)
           LoadFileStream()          // stream and filename BOTH updated before anything downstream
       }
   }
2. CLIPBOARD PRECEDENCE — mutually exclusive if/else-if chain, in this order:
       CopyFileToClipboard
   else CopyFilePathToClipboard
   else CopyFolderPathToClipboard
3. if (ShowInExplorer)  OpenFolderWithFile(FilePath)
4. if (AnalyzeImage && DataType == Image)   AI dialog, modal
5. if (ScanQRCode  && DataType == Image)    QR dialog, modal
```

The three clipboard flags are `else if`, so setting all three copies only the file.
Menu order is irrelevant.

## `DoTextJobs()` (L794)

```
if (Advanced.TextTaskSaveAsFile) {
    filePath = HandleExistsFile(GetScreenshotsFolder(settings), FileName, settings)
    if non-empty { FilePath = filePath; CreateDirectoryFromFilePath; File.WriteAllText(UTF8) }
}
Data = new MemoryStream(UTF8 bytes of Text)     // ALWAYS, regardless of the flag
```

## `DoUploadJob()` (L370)

```
1. large-file warning (Settings.ShowLargeFileSizeWarning > 0 and Data.Length over it)
   → modal Yes/No, "don't show again" persists 0, No calls Stop()
2. if (StopRequested) return
3. WaitUploadersConfig(); Status = Working
4. if (AfterCaptureTasks.ShowBeforeUploadWindow)  cancelUpload = dialog != OK
      ^ note: this is an AFTER-CAPTURE flag consumed inside the upload stage
5. if (cancelUpload) { Result.IsURLExpected = false; return }
6. OnUploadStarted()
7. isError = DoUpload(Data, FileName)
8. if (isError && Settings.MaxUploadFailRetry > 0)
       for retry = 1 .. MaxUploadFailRetry, while !StopRequested && isError:
           isError = DoUpload(Data, FileName, retry)
9. if (!isError) OnUploadCompleted()
```

Retry re-runs only the upload call, never the capture/file chain.

## `UploadData()` early URL copy (L903)

`EarlyURLCopied` is set when `AfterUploadTasks.CopyURLToClipboard` **and**
`Advanced.EarlyCopyURL` are both on and the uploader raises `EarlyURLCopyRequested`.
The `finally` in `ThreadDoWork` clears the clipboard again if the task then failed.

Filename handling before upload, in order: `RemoveBidiControlCharacters(fileName)`,
then `ReplaceReservedCharacters(fileName, "_")` when
`UploadSettings.FileUploadReplaceProblematicCharacters`.

## `DoAfterUploadJobs()` (L814) — exact order

Whole body is wrapped in `try/catch`; an exception becomes an error message, not a crash.

```
1. if (UploadSettings.URLRegexReplace)
       Result.URL = Regex.Replace(Result.URL, pattern, replacement)
2. if (Advanced.ResultForceHTTPS)  Result.ForceHTTPS()
3. SHORTEN — if (Job != ShareURL && (UseURLShortener || Job == ShortenURL ||
                (Advanced.AutoShortenURLLength > 0 && Result.URL.Length > that)))
       result = ShortenURL(Result.URL)
       Result.ShortenedURL = result.ShortenedURL ; Result.Errors += result.Errors
4. SHARE   — if (Job != ShortenURL && (ShareURL flag || Job == ShareURL))
       result = ShareURL(Result.ToString())        // uses the post-shorten string
       Result.Errors += result.Errors
       if (Job == ShareURL) Result.IsURLExpected = false
5. if (CopyURLToClipboard)
       txt = Advanced.ClipboardContentFormat is set
             ? UploadInfoParser().Parse(Info, ClipboardContentFormat)
             : Result.ToString()
       if (txt non-empty) Clipboard.CopyText(txt)
6. if (OpenURL)
       result = Advanced.OpenURLFormat is set
                ? UploadInfoParser().Parse(Info, OpenURLFormat)
                : Result.ToString()
       OpenURL(result)
7. if (ShowQRCode)  threadWorker.InvokeAsync(() => new QRCodeForm(Result.ToString()).Show())
```

Shorten strictly precedes share, which strictly precedes copy — so a copied URL is
the shortened one. `Result.ToString()` (not `Result.URL`) is what share/copy/open
consume, i.e. it already reflects the shortened URL.

## Implementation notes for the Mac port

- Every dialog stage (`AnnotateImage`, `SaveImageToFileWithDialog`,
  `ShowBeforeUploadWindow`, `AnalyzeImage`, `ScanQRCode`, the large-file warning)
  can suspend or cancel the task. They are injected services, not inline UI, so the
  runner stays testable headlessly.
- `Stop()`/cancellation must be checked at the same points the source checks
  `StopRequested`, not at arbitrary boundaries.
- External actions replace both the stream and the filename before any downstream
  stage observes them (step 1 of `DoFileJobs`). A runner that copies the path to the
  clipboard before re-loading the stream is wrong.
- No stage may run twice for one task; retry is scoped to `DoUpload` only.
