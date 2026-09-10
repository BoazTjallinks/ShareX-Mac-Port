using ShareX.Core.Enums;
using ShareX.Mac.App.ViewModels;

namespace ShareX.Mac.App.Models;

/// <summary>
/// The 22 non-zero <see cref="AfterCaptureTasks"/> flags with upstream's own
/// labels, plus an honest record of which ones this build can actually perform.
///
/// PROJECT-SPEC.md requires all 22 to remain in scope and forbids presenting an
/// unimplemented control as working, so unimplemented entries are shown disabled
/// with the reason rather than hidden.
/// </summary>
public static class AfterCaptureCatalog
{
    public static List<AfterCaptureTaskViewModel> Build(AfterCaptureTasks enabled)
    {
        bool On(AfterCaptureTasks f) => enabled.HasFlag(f);

        return new List<AfterCaptureTaskViewModel>
        {
            new(AfterCaptureTasks.ShowQuickTaskMenu, "Show quick task menu",
                On(AfterCaptureTasks.ShowQuickTaskMenu), false, "Quick task menu is not built yet."),
            new(AfterCaptureTasks.ShowAfterCaptureWindow, "Show after capture window",
                On(AfterCaptureTasks.ShowAfterCaptureWindow), false, "Dialog is not built yet."),
            new(AfterCaptureTasks.BeautifyImage, "Beautify image",
                On(AfterCaptureTasks.BeautifyImage), false, "Image beautifier is not ported yet."),
            new(AfterCaptureTasks.AddImageEffects, "Add image effects",
                On(AfterCaptureTasks.AddImageEffects), false, "Effect library is not ported yet."),
            new(AfterCaptureTasks.AnnotateImage, "Annotate image",
                On(AfterCaptureTasks.AnnotateImage), false, "Editor is not ported yet; enabling this cancels the task."),
            new(AfterCaptureTasks.CopyImageToClipboard, "Copy image to clipboard",
                On(AfterCaptureTasks.CopyImageToClipboard), true),
            new(AfterCaptureTasks.PinToScreen, "Pin to screen",
                On(AfterCaptureTasks.PinToScreen), false, "Pin windows are not built yet."),
            new(AfterCaptureTasks.SendImageToPrinter, "Send image to printer",
                On(AfterCaptureTasks.SendImageToPrinter), false, "Printing is not wired up yet."),
            new(AfterCaptureTasks.SaveImageToFile, "Save image to file",
                On(AfterCaptureTasks.SaveImageToFile), true),
            new(AfterCaptureTasks.SaveImageToFileWithDialog, "Save image to file as...",
                On(AfterCaptureTasks.SaveImageToFileWithDialog), false, "Save panel is not wired up yet."),
            new(AfterCaptureTasks.SaveThumbnailImageToFile, "Save thumbnail image to file",
                On(AfterCaptureTasks.SaveThumbnailImageToFile), false, "Thumbnailer is not wired in yet."),
            new(AfterCaptureTasks.PerformActions, "Perform actions",
                On(AfterCaptureTasks.PerformActions), false, "External actions are not implemented."),
            new(AfterCaptureTasks.CopyFileToClipboard, "Copy file to clipboard",
                On(AfterCaptureTasks.CopyFileToClipboard), true),
            new(AfterCaptureTasks.CopyFilePathToClipboard, "Copy file path to clipboard",
                On(AfterCaptureTasks.CopyFilePathToClipboard), true),
            new(AfterCaptureTasks.CopyFolderPathToClipboard, "Copy folder path to clipboard",
                On(AfterCaptureTasks.CopyFolderPathToClipboard), true),
            new(AfterCaptureTasks.ShowInExplorer, "Show file in Finder",
                On(AfterCaptureTasks.ShowInExplorer), true),
            new(AfterCaptureTasks.ScanQRCode, "Scan QR code",
                On(AfterCaptureTasks.ScanQRCode), false, "QR tool is not wired into the workflow yet."),
            new(AfterCaptureTasks.DoOCR, "Recognize text (OCR)",
                On(AfterCaptureTasks.DoOCR), false, "OCR is not wired into the workflow yet."),
            new(AfterCaptureTasks.AnalyzeImage, "Analyze image",
                On(AfterCaptureTasks.AnalyzeImage), false, "AI analysis is not implemented."),
            new(AfterCaptureTasks.ShowBeforeUploadWindow, "Show before upload window",
                On(AfterCaptureTasks.ShowBeforeUploadWindow), false, "Upload pipeline is not built yet."),
            new(AfterCaptureTasks.UploadImageToHost, "Upload image to host",
                On(AfterCaptureTasks.UploadImageToHost), false, "Uploaders are not ported yet."),
            new(AfterCaptureTasks.DeleteFile, "Delete file locally",
                On(AfterCaptureTasks.DeleteFile), false, "Runs only after a successful upload, which is not built yet.")
        };
    }
}
