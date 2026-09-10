#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

// Ported from ShareX v21.0.0 (commit d2502561f63fc3ff502cacd91514e3f7f2948c74), file ShareX/Enums.cs.
//
// Porting notes:
//   - Every member name and every explicit numeric/flag value is preserved verbatim.
//   - [Description(...)] attributes did not compile standalone here (they depended on
//     ShareX.HelpersLib / System.ComponentModel usage patterns from the WinForms app) and
//     have been stripped from the enum members. Where the upstream file carried literal
//     description text inline (SupportedLanguage), that text is preserved losslessly below
//     in UpstreamEnumDescriptions instead of being dropped.
//   - Members upstream marked "// Localized" (TaskJob-adjacent enums, HotkeyType, etc.) do not
//     carry literal description text in Enums.cs itself -- their display strings are resolved
//     from upstream .resx resource files that are out of scope for this port and were not read
//     to produce this file. Nothing is invented here for them.
//   - HotkeyType's upstream [Category(EnumExtensions.HotkeyType_Category_*)] attributes
//     reference string constants defined in ShareX.HelpersLib.EnumExtensions (not part of
//     Enums.cs), so no literal text is available to preserve; the attributes are stripped and
//     the original grouping is preserved instead as comments to keep the ordering legible.
//   - StartupState is ported in its non-MicrosoftStore form only. The omitted variant is the
//     `#if MicrosoftStore` branch, whose members reference Windows.ApplicationModel.StartupTaskState
//     (Windows Store API, not applicable to this macOS port).
using System;
using System.Collections.Generic;

namespace ShareX.Core.Enums
{
    public enum ShareXBuild
    {
        Debug,
        Release,
        Steam,
        MicrosoftStore,
        Unknown
    }

    public enum TaskJob
    {
        Job,
        DataUpload,
        FileUpload,
        TextUpload,
        ShortenURL,
        ShareURL,
        Download,
        DownloadUpload
    }

    public enum TaskStatus
    {
        InQueue,
        Preparing,
        Working,
        Stopping,
        Stopped,
        Failed,
        Completed,
        History
    }

    [Flags]
    public enum AfterCaptureTasks // Localized
    {
        None = 0,
        ShowQuickTaskMenu = 1,
        ShowAfterCaptureWindow = 1 << 1,
        BeautifyImage = 1 << 2,
        AddImageEffects = 1 << 3,
        AnnotateImage = 1 << 4,
        CopyImageToClipboard = 1 << 5,
        PinToScreen = 1 << 6,
        SendImageToPrinter = 1 << 7,
        SaveImageToFile = 1 << 8,
        SaveImageToFileWithDialog = 1 << 9,
        SaveThumbnailImageToFile = 1 << 10,
        PerformActions = 1 << 11,
        CopyFileToClipboard = 1 << 12,
        CopyFilePathToClipboard = 1 << 13,
        CopyFolderPathToClipboard = 1 << 14,
        ShowInExplorer = 1 << 15,
        AnalyzeImage = 1 << 16,
        ScanQRCode = 1 << 17,
        DoOCR = 1 << 18,
        ShowBeforeUploadWindow = 1 << 19,
        UploadImageToHost = 1 << 20,
        DeleteFile = 1 << 21
    }

    [Flags]
    public enum AfterUploadTasks // Localized
    {
        None = 0,
        ShowAfterUploadWindow = 1,
        UseURLShortener = 1 << 1,
        ShareURL = 1 << 2,
        CopyURLToClipboard = 1 << 3,
        OpenURL = 1 << 4,
        ShowQRCode = 1 << 5
    }

    public enum CaptureType
    {
        Fullscreen,
        Monitor,
        ActiveMonitor,
        Window,
        ActiveWindow,
        Region,
        CustomRegion,
        LastRegion
    }

    public enum ScreenRecordStartMethod
    {
        Region,
        ActiveWindow,
        CustomRegion,
        LastRegion
    }

    public enum HotkeyType // Localized
    {
        None,
        // Upload
        FileUpload,
        FolderUpload,
        ClipboardUpload,
        ClipboardUploadWithContentViewer,
        UploadText,
        UploadURL,
        DragDropUpload,
        ShortenURL,
        StopUploads,
        // Screen capture
        PrintScreen,
        ActiveWindow,
        CustomWindow,
        ActiveMonitor,
        RectangleRegion,
        RectangleLight,
        RectangleTransparent,
        CustomRegion,
        LastRegion,
        ScrollingCapture,
        AutoCapture,
        StartAutoCapture,
        StopAutoCapture,
        // Screen record
        ScreenRecorder,
        ScreenRecorderActiveWindow,
        ScreenRecorderCustomRegion,
        StartScreenRecorder,
        ScreenRecorderGIF,
        ScreenRecorderGIFActiveWindow,
        ScreenRecorderGIFCustomRegion,
        StartScreenRecorderGIF,
        StopScreenRecording,
        PauseScreenRecording,
        AbortScreenRecording,
        // Tools
        ColorPicker,
        ScreenColorPicker,
        Ruler,
        PinToScreen,
        PinToScreenFromScreen,
        PinToScreenFromClipboard,
        PinToScreenFromFile,
        PinToScreenCloseAll,
        ImageEditor,
        ImageBeautifier,
        ImageEffects,
        ImageViewer,
        BackgroundRemover,
        ImageComparer,
        ImageCombiner,
        ImageSplitter,
        ImageThumbnailer,
        VideoConverter,
        VideoThumbnailer,
        AnalyzeImage,
        OCR,
        QRCode,
        QRCodeDecodeFromScreen,
        QRCodeScanRegion,
        HashCheck,
        Metadata,
        StripMetadata,
        IndexFolder,
        ClipboardViewer,
        BorderlessWindow,
        ActiveWindowBorderless,
        ActiveWindowTopMost,
        InspectWindow,
        MonitorTest,
        // Other
        DisableHotkeys,
        OpenMainWindow,
        OpenScreenshotsFolder,
        OpenHistory,
        OpenImageHistory,
        ToggleActionsToolbar,
        ToggleTrayMenu,
        ExitShareX
    }

    public enum ToastClickAction // Localized
    {
        CloseNotification,
        AnnotateImage,
        CopyImageToClipboard,
        CopyFile,
        CopyFilePath,
        CopyUrl,
        OpenFile,
        OpenFolder,
        OpenUrl,
        Upload,
        PinToScreen,
        DeleteFile
    }

    public enum ThumbnailViewClickAction // Localized
    {
        Default,
        Select,
        OpenImageViewer,
        OpenFile,
        OpenFolder,
        OpenURL,
        EditImage
    }

    public enum FileExistAction // Localized
    {
        Ask,
        Overwrite,
        UniqueName,
        Cancel
    }

    public enum ImagePreviewVisibility // Localized
    {
        Show, Hide, Automatic
    }

    public enum ImagePreviewLocation // Localized
    {
        Side, Bottom
    }

    public enum ThumbnailTitleLocation // Localized
    {
        Top, Bottom
    }

    public enum RegionCaptureType
    {
        Default, Light, Transparent
    }

    public enum ScreenTearingTestMode
    {
        VerticalLines,
        HorizontalLines
    }

    // Ported alongside the other members of this file's #if !MicrosoftStore branch.
    // Omitted variant: `#if MicrosoftStore` StartupState, whose members are assigned from
    // Windows.ApplicationModel.StartupTaskState (Disabled/DisabledByUser/Enabled/
    // DisabledByPolicy/EnabledByPolicy) -- a Windows Store API with no macOS equivalent.
    public enum StartupState
    {
        Disabled,
        DisabledByUser,
        Enabled,
        DisabledByPolicy,
        EnabledByPolicy
    }

    public enum TaskViewMode // Localized
    {
        ListView,
        ThumbnailView
    }

    public enum NativeMessagingAction
    {
        None,
        UploadImage,
        UploadVideo,
        UploadAudio,
        UploadText,
        ShortenURL
    }

    public enum NotificationSound
    {
        Capture,
        TaskCompleted,
        ActionCompleted,
        Error
    }

    public enum UpdateChannel // Localized
    {
        Release,
        PreRelease,
        Dev
    }

    public enum SupportedLanguage
    {
        Automatic, // Localized
        Arabic,
        Dutch,
        English,
        French,
        German,
        Hebrew,
        Hungarian,
        Indonesian,
        Italian,
        Japanese,
        Korean,
        MexicanSpanish,
        Persian,
        Polish,
        Portuguese,
        PortugueseBrazil,
        Romanian,
        Russian,
        SimplifiedChinese,
        Spanish,
        TraditionalChinese,
        Turkish,
        Ukrainian,
        Vietnamese
    }

    /// <summary>
    /// Carries the literal display text that upstream attached to enum members via
    /// [Description(...)] in ShareX/Enums.cs (v21.0.0), for enums where that attribute
    /// supplied real inline text rather than delegating to a resource file. Currently this is
    /// only SupportedLanguage; other "// Localized" enums in this file resolve their display
    /// text from upstream .resx resources that this port does not carry, so no text is
    /// fabricated for them here.
    /// </summary>
    public static class UpstreamEnumDescriptions
    {
        /// <summary>
        /// Exact strings from the [Description(...)] attributes on ShareX.SupportedLanguage
        /// members in ShareX/Enums.cs (v21.0.0). SupportedLanguage.Automatic is intentionally
        /// absent: upstream marks it "// Localized" instead of giving it a literal Description.
        /// </summary>
        public static readonly IReadOnlyDictionary<SupportedLanguage, string> SupportedLanguageDescriptions =
            new Dictionary<SupportedLanguage, string>
            {
                [SupportedLanguage.Arabic] = "العربية (Arabic)",
                [SupportedLanguage.Dutch] = "Nederlands (Dutch)",
                [SupportedLanguage.English] = "English",
                [SupportedLanguage.French] = "Français (French)",
                [SupportedLanguage.German] = "Deutsch (German)",
                [SupportedLanguage.Hebrew] = "עִברִית (Hebrew)",
                [SupportedLanguage.Hungarian] = "Magyar (Hungarian)",
                [SupportedLanguage.Indonesian] = "Bahasa Indonesia (Indonesian)",
                [SupportedLanguage.Italian] = "Italiano (Italian)",
                [SupportedLanguage.Japanese] = "日本語 (Japanese)",
                [SupportedLanguage.Korean] = "한국어 (Korean)",
                [SupportedLanguage.MexicanSpanish] = "Español mexicano (Mexican Spanish)",
                [SupportedLanguage.Persian] = "فارسی (Persian)",
                [SupportedLanguage.Polish] = "Polski (Polish)",
                [SupportedLanguage.Portuguese] = "Português (Portuguese)",
                [SupportedLanguage.PortugueseBrazil] = "Português-Brasil (Portuguese-Brazil)",
                [SupportedLanguage.Romanian] = "Română (Romanian)",
                [SupportedLanguage.Russian] = "Русский (Russian)",
                [SupportedLanguage.SimplifiedChinese] = "简体中文 (Simplified Chinese)",
                [SupportedLanguage.Spanish] = "Español (Spanish)",
                [SupportedLanguage.TraditionalChinese] = "繁體中文 (Traditional Chinese)",
                [SupportedLanguage.Turkish] = "Türkçe (Turkish)",
                [SupportedLanguage.Ukrainian] = "Українська (Ukrainian)",
                [SupportedLanguage.Vietnamese] = "Tiếng Việt (Vietnamese)"
            };
    }
}
