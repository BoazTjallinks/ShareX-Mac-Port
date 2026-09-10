using ShareX.Core.Enums;
using Xunit;
using TaskStatus = ShareX.Core.Enums.TaskStatus;

namespace ShareX.Core.Tests
{
    /// <summary>
    /// Asserts the ported enum values against a table hand-derived from
    /// reference/ShareX/ShareX/Enums.cs at v21.0.0 (commit d2502561f63fc3ff502cacd91514e3f7f2948c74).
    /// Every value below was read from that file member-by-member, not assumed.
    /// </summary>
    public class UpstreamEnumsTests
    {
        [Fact]
        public void ShareXBuild_Values_MatchUpstream()
        {
            Assert.Equal(0, (int)ShareXBuild.Debug);
            Assert.Equal(1, (int)ShareXBuild.Release);
            Assert.Equal(2, (int)ShareXBuild.Steam);
            Assert.Equal(3, (int)ShareXBuild.MicrosoftStore);
            Assert.Equal(4, (int)ShareXBuild.Unknown);
        }

        [Fact]
        public void ScreenTearingTestMode_Values_MatchUpstream()
        {
            Assert.Equal(0, (int)ScreenTearingTestMode.VerticalLines);
            Assert.Equal(1, (int)ScreenTearingTestMode.HorizontalLines);
        }

        [Fact]
        public void TaskJob_Values_MatchUpstream()
        {
            Assert.Equal(0, (int)TaskJob.Job);
            Assert.Equal(1, (int)TaskJob.DataUpload);
            Assert.Equal(2, (int)TaskJob.FileUpload);
            Assert.Equal(3, (int)TaskJob.TextUpload);
            Assert.Equal(4, (int)TaskJob.ShortenURL);
            Assert.Equal(5, (int)TaskJob.ShareURL);
            Assert.Equal(6, (int)TaskJob.Download);
            Assert.Equal(7, (int)TaskJob.DownloadUpload);
        }

        [Fact]
        public void TaskStatus_Values_MatchUpstream()
        {
            Assert.Equal(0, (int)TaskStatus.InQueue);
            Assert.Equal(1, (int)TaskStatus.Preparing);
            Assert.Equal(2, (int)TaskStatus.Working);
            Assert.Equal(3, (int)TaskStatus.Stopping);
            Assert.Equal(4, (int)TaskStatus.Stopped);
            Assert.Equal(5, (int)TaskStatus.Failed);
            Assert.Equal(6, (int)TaskStatus.Completed);
            Assert.Equal(7, (int)TaskStatus.History);
        }

        [Fact]
        public void AfterCaptureTasks_Values_MatchUpstream()
        {
            // Upstream is a bit-shifted [Flags] enum; None then successive powers of two.
            Assert.Equal(0, (int)AfterCaptureTasks.None);
            Assert.Equal(1, (int)AfterCaptureTasks.ShowQuickTaskMenu);
            Assert.Equal(2, (int)AfterCaptureTasks.ShowAfterCaptureWindow);
            Assert.Equal(4, (int)AfterCaptureTasks.BeautifyImage);
            Assert.Equal(8, (int)AfterCaptureTasks.AddImageEffects);
            Assert.Equal(16, (int)AfterCaptureTasks.AnnotateImage);
            // Verified directly against reference/ShareX/ShareX/Enums.cs: CopyImageToClipboard
            // is the 6th shifted flag (1 << 5) = 32, not 8 -- AddImageEffects (1 << 3) is 8.
            Assert.Equal(32, (int)AfterCaptureTasks.CopyImageToClipboard);
            Assert.Equal(64, (int)AfterCaptureTasks.PinToScreen);
            Assert.Equal(128, (int)AfterCaptureTasks.SendImageToPrinter);
            Assert.Equal(256, (int)AfterCaptureTasks.SaveImageToFile);
            Assert.Equal(512, (int)AfterCaptureTasks.SaveImageToFileWithDialog);
            Assert.Equal(1024, (int)AfterCaptureTasks.SaveThumbnailImageToFile);
            Assert.Equal(2048, (int)AfterCaptureTasks.PerformActions);
            Assert.Equal(4096, (int)AfterCaptureTasks.CopyFileToClipboard);
            Assert.Equal(8192, (int)AfterCaptureTasks.CopyFilePathToClipboard);
            Assert.Equal(16384, (int)AfterCaptureTasks.CopyFolderPathToClipboard);
            Assert.Equal(32768, (int)AfterCaptureTasks.ShowInExplorer);
            Assert.Equal(65536, (int)AfterCaptureTasks.AnalyzeImage);
            Assert.Equal(131072, (int)AfterCaptureTasks.ScanQRCode);
            Assert.Equal(262144, (int)AfterCaptureTasks.DoOCR);
            Assert.Equal(524288, (int)AfterCaptureTasks.ShowBeforeUploadWindow);
            Assert.Equal(1048576, (int)AfterCaptureTasks.UploadImageToHost);
            Assert.Equal(2097152, (int)AfterCaptureTasks.DeleteFile);
        }

        [Fact]
        public void AfterUploadTasks_Values_MatchUpstream()
        {
            Assert.Equal(0, (int)AfterUploadTasks.None);
            Assert.Equal(1, (int)AfterUploadTasks.ShowAfterUploadWindow);
            Assert.Equal(2, (int)AfterUploadTasks.UseURLShortener);
            Assert.Equal(4, (int)AfterUploadTasks.ShareURL);
            Assert.Equal(8, (int)AfterUploadTasks.CopyURLToClipboard);
            Assert.Equal(16, (int)AfterUploadTasks.OpenURL);
            Assert.Equal(32, (int)AfterUploadTasks.ShowQRCode);
        }

        [Fact]
        public void CaptureType_Values_MatchUpstream()
        {
            Assert.Equal(0, (int)CaptureType.Fullscreen);
            Assert.Equal(1, (int)CaptureType.Monitor);
            Assert.Equal(2, (int)CaptureType.ActiveMonitor);
            Assert.Equal(3, (int)CaptureType.Window);
            Assert.Equal(4, (int)CaptureType.ActiveWindow);
            Assert.Equal(5, (int)CaptureType.Region);
            Assert.Equal(6, (int)CaptureType.CustomRegion);
            Assert.Equal(7, (int)CaptureType.LastRegion);
        }

        [Fact]
        public void ScreenRecordStartMethod_Values_MatchUpstream()
        {
            Assert.Equal(0, (int)ScreenRecordStartMethod.Region);
            Assert.Equal(1, (int)ScreenRecordStartMethod.ActiveWindow);
            Assert.Equal(2, (int)ScreenRecordStartMethod.CustomRegion);
            Assert.Equal(3, (int)ScreenRecordStartMethod.LastRegion);
        }

        [Fact]
        public void HotkeyType_BoundaryValues_MatchUpstream()
        {
            // First and last declared members, plus one from the middle of each upstream
            // comment-delimited group, counted by hand against Enums.cs.
            Assert.Equal(0, (int)HotkeyType.None);
            Assert.Equal(1, (int)HotkeyType.FileUpload);
            Assert.Equal(9, (int)HotkeyType.StopUploads);
            Assert.Equal(10, (int)HotkeyType.PrintScreen);
            Assert.Equal(22, (int)HotkeyType.StopAutoCapture);
            Assert.Equal(23, (int)HotkeyType.ScreenRecorder);
            Assert.Equal(33, (int)HotkeyType.AbortScreenRecording);
            Assert.Equal(34, (int)HotkeyType.ColorPicker);
            Assert.Equal(67, (int)HotkeyType.MonitorTest);
            Assert.Equal(68, (int)HotkeyType.DisableHotkeys);
            Assert.Equal(75, (int)HotkeyType.ExitShareX);
        }

        [Fact]
        public void ToastClickAction_Values_MatchUpstream()
        {
            Assert.Equal(0, (int)ToastClickAction.CloseNotification);
            Assert.Equal(11, (int)ToastClickAction.DeleteFile);
        }

        [Fact]
        public void FileExistAction_Values_MatchUpstream()
        {
            Assert.Equal(0, (int)FileExistAction.Ask);
            Assert.Equal(1, (int)FileExistAction.Overwrite);
            Assert.Equal(2, (int)FileExistAction.UniqueName);
            Assert.Equal(3, (int)FileExistAction.Cancel);
        }

        [Fact]
        public void ImagePreviewVisibility_Values_MatchUpstream()
        {
            Assert.Equal(0, (int)ImagePreviewVisibility.Show);
            Assert.Equal(1, (int)ImagePreviewVisibility.Hide);
            Assert.Equal(2, (int)ImagePreviewVisibility.Automatic);
        }

        [Fact]
        public void ImagePreviewLocation_Values_MatchUpstream()
        {
            Assert.Equal(0, (int)ImagePreviewLocation.Side);
            Assert.Equal(1, (int)ImagePreviewLocation.Bottom);
        }

        [Fact]
        public void ThumbnailTitleLocation_Values_MatchUpstream()
        {
            Assert.Equal(0, (int)ThumbnailTitleLocation.Top);
            Assert.Equal(1, (int)ThumbnailTitleLocation.Bottom);
        }

        [Fact]
        public void RegionCaptureType_Values_MatchUpstream()
        {
            Assert.Equal(0, (int)RegionCaptureType.Default);
            Assert.Equal(1, (int)RegionCaptureType.Light);
            Assert.Equal(2, (int)RegionCaptureType.Transparent);
        }

        [Fact]
        public void StartupState_NonMicrosoftStoreVariant_Values_MatchUpstream()
        {
            Assert.Equal(0, (int)StartupState.Disabled);
            Assert.Equal(1, (int)StartupState.DisabledByUser);
            Assert.Equal(2, (int)StartupState.Enabled);
            Assert.Equal(3, (int)StartupState.DisabledByPolicy);
            Assert.Equal(4, (int)StartupState.EnabledByPolicy);
        }

        [Fact]
        public void TaskViewMode_Values_MatchUpstream()
        {
            Assert.Equal(0, (int)TaskViewMode.ListView);
            Assert.Equal(1, (int)TaskViewMode.ThumbnailView);
        }

        [Fact]
        public void NativeMessagingAction_Values_MatchUpstream()
        {
            Assert.Equal(0, (int)NativeMessagingAction.None);
            Assert.Equal(1, (int)NativeMessagingAction.UploadImage);
            Assert.Equal(2, (int)NativeMessagingAction.UploadVideo);
            Assert.Equal(3, (int)NativeMessagingAction.UploadAudio);
            Assert.Equal(4, (int)NativeMessagingAction.UploadText);
            Assert.Equal(5, (int)NativeMessagingAction.ShortenURL);
        }

        [Fact]
        public void NotificationSound_Values_MatchUpstream()
        {
            Assert.Equal(0, (int)NotificationSound.Capture);
            Assert.Equal(1, (int)NotificationSound.TaskCompleted);
            Assert.Equal(2, (int)NotificationSound.ActionCompleted);
            Assert.Equal(3, (int)NotificationSound.Error);
        }

        [Fact]
        public void UpdateChannel_Values_MatchUpstream()
        {
            Assert.Equal(0, (int)UpdateChannel.Release);
            Assert.Equal(1, (int)UpdateChannel.PreRelease);
            Assert.Equal(2, (int)UpdateChannel.Dev);
        }

        [Fact]
        public void SupportedLanguage_Values_MatchUpstream()
        {
            Assert.Equal(0, (int)SupportedLanguage.Automatic);
            Assert.Equal(1, (int)SupportedLanguage.Arabic);
            Assert.Equal(2, (int)SupportedLanguage.Dutch);
            Assert.Equal(3, (int)SupportedLanguage.English);
            Assert.Equal(24, (int)SupportedLanguage.Vietnamese);
        }

        [Fact]
        public void SupportedLanguageDescriptions_CarryExactUpstreamText()
        {
            Assert.Equal("Nederlands (Dutch)", UpstreamEnumDescriptions.SupportedLanguageDescriptions[SupportedLanguage.Dutch]);
            Assert.Equal("English", UpstreamEnumDescriptions.SupportedLanguageDescriptions[SupportedLanguage.English]);
            Assert.Equal("繁體中文 (Traditional Chinese)", UpstreamEnumDescriptions.SupportedLanguageDescriptions[SupportedLanguage.TraditionalChinese]);
            // Automatic has no literal Description upstream (it is "// Localized" instead).
            Assert.False(UpstreamEnumDescriptions.SupportedLanguageDescriptions.ContainsKey(SupportedLanguage.Automatic));
        }
    }
}
