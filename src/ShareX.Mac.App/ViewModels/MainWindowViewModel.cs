using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShareX.Core.Errors;
using ShareX.Mac.App.Models;
using ShareX.Mac.App.Support;
using ShareX.Platform.Mac;
using ShareX.Platform.Mac.Interop;

namespace ShareX.Mac.App.ViewModels;

/// <summary>One line in the task list, mirroring upstream's lvUploads rows.</summary>
public sealed partial class TaskRowViewModel : ObservableObject
{
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private string _fileName = "";
    [ObservableProperty] private string _detail = "";
    [ObservableProperty] private string _filePath = "";
    public DateTime Started { get; } = DateTime.Now;
    public string StartedText => Started.ToString("HH:mm:ss");
}

public sealed partial class PermissionRowViewModel : ObservableObject
{
    public PermissionRowViewModel(string key, string title, string why)
    {
        Key = key;
        Title = title;
        Why = why;
    }

    public string Key { get; }
    public string Title { get; }
    public string Why { get; }

    [ObservableProperty] private string _state = "unknown";

    public bool IsGranted => State == "granted";

    /// <summary>
    /// macOS stores a denial permanently; the app cannot re-prompt and must not
    /// try to change TCC. The only honest next step is to send the user to the
    /// settings pane.
    /// </summary>
    public bool NeedsSystemSettings => State is "denied";

    partial void OnStateChanged(string value)
    {
        OnPropertyChanged(nameof(IsGranted));
        OnPropertyChanged(nameof(NeedsSystemSettings));
    }
}

public sealed partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private const string ScreenCapturePrivacyPane =
        "x-apple.systempreferences:com.apple.preference.security?Privacy_ScreenCapture";
    private const string MicrophonePrivacyPane =
        "x-apple.systempreferences:com.apple.preference.security?Privacy_Microphone";

    // One bridge per app: it owns the process-wide native event sink.
    private readonly NativeBridge _bridge = new();
    private readonly MacCaptureService _capture;
    private bool _disposed;

    public MainWindowViewModel() => _capture = new MacCaptureService(_bridge);

    public IReadOnlyList<CommandEntry> MainRail => CommandTree.MainRail;

    public ObservableCollection<TaskRowViewModel> Tasks { get; } = new();
    public ObservableCollection<PermissionRowViewModel> Permissions { get; } = new()
    {
        new("screenRecording", "Screen & System Audio Recording",
            "Required for every capture and recording command."),
        new("accessibility", "Accessibility",
            "Used by scrolling capture and window inspection."),
        new("microphone", "Microphone",
            "Only used when you record narration into a screen recording.")
    };

    public ObservableCollection<string> Displays { get; } = new();
    public ObservableCollection<string> Log { get; } = new();

    [ObservableProperty] private string _environmentSummary = "Querying the native bridge…";
    [ObservableProperty] private string _bundleSummary = "";
    [ObservableProperty] private Bitmap? _previewImage;
    [ObservableProperty] private string _previewCaption = "No capture yet.";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusText = "Ready";

    public bool IsBundled => BundleInfo.IsBundled;

    public void InitializeAsync() => _ = RefreshAllAsync();

    // ---------------------------------------------------------------- commands

    [RelayCommand]
    private async Task RefreshAllAsync()
    {
        await RefreshCapabilitiesAsync().ConfigureAwait(true);
        await RefreshPermissionsAsync().ConfigureAwait(true);
        await RefreshDisplaysAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RefreshCapabilitiesAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        OperationOutcome<CapabilitiesInfo> outcome =
            await _capture.GetCapabilitiesAsync().ConfigureAwait(true);
        stopwatch.Stop();

        outcome.Match(
            info =>
            {
                JsonElement raw = info.Raw;
                string os = Text(raw, "osVersion");
                string arch = Text(raw, "architecture");
                string bundleId = Text(raw, "bundleIdentifier");
                int abi = raw.TryGetProperty("abiVersion", out JsonElement a) && a.TryGetInt32(out int v) ? v : 0;

                EnvironmentSummary = $"macOS {os} · {arch} · native ABI {abi}";
                BundleSummary = BundleInfo.IsBundled
                    ? $"Bundled: {bundleId} at {BundleInfo.BundlePath}"
                    : "Not running from an .app bundle — macOS cannot grant this process "
                      + "Screen Recording permission, so captures will fail until you launch "
                      + "the built ShareX-Mac.app.";

                AppendLog($"capabilities.get ok in {stopwatch.ElapsedMilliseconds} ms");
                return true;
            },
            error =>
            {
                EnvironmentSummary = $"Native bridge unavailable: {error.Kind} — {error.Message}";
                BundleSummary = "";
                AppendLog($"capabilities.get FAILED ({error.Kind}): {error.Message}");
                return false;
            });
    }

    [RelayCommand]
    private async Task RefreshPermissionsAsync()
    {
        OperationOutcome<PermissionsInfo> outcome =
            await _capture.GetPermissionsAsync().ConfigureAwait(true);

        outcome.Match(
            info =>
            {
                foreach (PermissionRowViewModel row in Permissions)
                {
                    row.State = Text(info.Raw, row.Key, "unknown");
                }

                AppendLog("permissions.get ok");
                return true;
            },
            error =>
            {
                foreach (PermissionRowViewModel row in Permissions)
                {
                    row.State = "unknown";
                }

                AppendLog($"permissions.get FAILED ({error.Kind}): {error.Message}");
                return false;
            });
    }

    [RelayCommand]
    private async Task RequestPermissionAsync(PermissionRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        OperationOutcome<PermissionRequestResult> outcome =
            await _capture.RequestPermissionAsync(row.Key).ConfigureAwait(true);

        outcome.Match(
            result =>
            {
                row.State = result.State;
                AppendLog($"permissions.request({row.Key}) → {result.State}");

                if (result.State != "granted")
                {
                    // Distinguish "not asked yet" from "macOS already stored a
                    // decision" — the second cannot be fixed from inside the app.
                    StatusText = result.Note
                        ?? "macOS did not grant the permission. If no dialog appeared, the "
                           + "decision is already stored and must be changed in System Settings.";
                }

                return true;
            },
            error =>
            {
                AppendLog($"permissions.request({row.Key}) FAILED ({error.Kind}): {error.Message}");
                StatusText = $"{error.Kind}: {error.Message}";
                return false;
            });
    }

    [RelayCommand]
    private void OpenPrivacySettings(PermissionRowViewModel? row)
    {
        string pane = row?.Key switch
        {
            "microphone" => MicrophonePrivacyPane,
            _ => ScreenCapturePrivacyPane
        };

        BundleInfo.OpenSystemSettings(pane);
        AppendLog($"opened System Settings pane for {row?.Key ?? "screenRecording"}");
    }

    [RelayCommand]
    private async Task RefreshDisplaysAsync()
    {
        OperationOutcome<IReadOnlyList<DisplayInfo>> outcome =
            await _capture.ListDisplaysAsync().ConfigureAwait(true);

        Displays.Clear();
        outcome.Match(
            displays =>
            {
                foreach (DisplayInfo display in displays)
                {
                    string flags = string.Join(" ", new[]
                    {
                        display.IsMain ? "main" : null,
                        display.IsActive ? "active" : null
                    }.Where(f => f is not null));

                    Displays.Add(
                        $"#{display.DisplayId}  {display.BoundsGlobalPoints.Width:0}×{display.BoundsGlobalPoints.Height:0} pt "
                        + $"@{display.Scale:0.##}x  origin ({display.BoundsGlobalPoints.X:0},{display.BoundsGlobalPoints.Y:0}) "
                        + $"{display.BoundsGlobalPoints.Space}  {flags}");
                }

                AppendLog($"displays.list ok — {displays.Count} display(s)");
                return true;
            },
            error =>
            {
                Displays.Add($"Unavailable: {error.Kind} — {error.Message}");
                AppendLog($"displays.list FAILED ({error.Kind}): {error.Message}");
                return false;
            });
    }

    [RelayCommand]
    private Task CaptureFullscreenAsync() => CaptureAsync(CaptureTarget.AllDisplays(), "Fullscreen");

    [RelayCommand]
    private Task CaptureActiveMonitorAsync() => CaptureAsync(CaptureTarget.ActiveDisplay(), "Monitor");

    [RelayCommand]
    private Task CaptureActiveWindowAsync() => CaptureAsync(CaptureTarget.ActiveWindow(), "Window");

    /// <summary>
    /// Runs a real capture, writes it to the screenshots folder and shows it.
    /// This is the vertical slice: command → native capture → saved artifact →
    /// visible result, with failures surfaced structurally.
    /// </summary>
    private async Task CaptureAsync(CaptureTarget target, string label)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusText = $"Capturing ({label})…";

        var row = new TaskRowViewModel { Status = "Working", FileName = label, Detail = "capture.screenshot" };
        Tasks.Insert(0, row);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            DateTime now = DateTime.Now;
            string folder = AppPaths.EnsureDirectory(AppPaths.ScreenshotsFolderFor(now));
            // Upstream's default filename pattern is %y-%mo-%d_%h-%mi-%s.
            string fileName = $"{now:yyyy-MM-dd_HH-mm-ss}.png";
            string outputPath = Path.Combine(folder, fileName);

            OperationOutcome<CaptureResult> outcome = await _capture
                .CaptureAsync(target, showsCursor: false, outputPath: outputPath)
                .ConfigureAwait(true);

            stopwatch.Stop();

            outcome.Match(
                result =>
                {
                    row.Status = "Completed";
                    row.FileName = fileName;
                    row.FilePath = result.OutputPath ?? outputPath;
                    row.Detail =
                        $"{result.PixelWidth}×{result.PixelHeight} px @{result.CompositionScale:0.##}x";

                    PreviewCaption =
                        $"{label} — {result.PixelWidth}×{result.PixelHeight} px, composition scale "
                        + $"{result.CompositionScale:0.##}, source {DescribeRect(result)} — {row.FilePath}";

                    LoadPreview(row.FilePath);
                    StatusText = $"Saved {fileName}";
                    AppendLog($"capture.screenshot({label}) ok in {stopwatch.ElapsedMilliseconds} ms → {row.FilePath}");
                    return true;
                },
                error =>
                {
                    row.Status = "Failed";
                    row.Detail = $"{error.Kind}: {error.Message}";

                    StatusText = error.Kind switch
                    {
                        TaskErrorKind.PermissionRequired =>
                            "Screen Recording permission is required. Grant it in the Permissions panel.",
                        TaskErrorKind.PermissionDenied =>
                            "Screen Recording is denied. Change it in System Settings → Privacy & Security.",
                        TaskErrorKind.UserCancelled => "Capture cancelled.",
                        _ => $"{error.Kind}: {error.Message}"
                    };

                    // A failed capture must never look like a successful empty one.
                    PreviewCaption = $"{label} failed — {error.Kind}: {error.Message}";
                    AppendLog($"capture.screenshot({label}) FAILED ({error.Kind}): {error.Message}");
                    return false;
                });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            row.Status = "Failed";
            row.Detail = ex.Message;
            StatusText = $"Capture failed: {ex.Message}";
            AppendLog($"capture.screenshot({label}) threw: {ex}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenScreenshotsFolder()
    {
        string folder = AppPaths.EnsureDirectory(AppPaths.DefaultScreenshotsFolder);
        BundleInfo.OpenSystemSettings(folder);
        AppendLog($"revealed {folder}");
    }

    // ---------------------------------------------------------------- helpers

    private static string DescribeRect(CaptureResult result) =>
        result.SourceRect is { } rect
            ? $"({rect.X:0},{rect.Y:0}) {rect.Width:0}×{rect.Height:0} {rect.Space}"
            : "unspecified";

    private void LoadPreview(string path)
    {
        try
        {
            using FileStream stream = File.OpenRead(path);
            PreviewImage?.Dispose();
            PreviewImage = new Bitmap(stream);
        }
        catch (Exception ex)
        {
            PreviewCaption += $"  (preview unavailable: {ex.Message})";
        }
    }

    private static string Text(JsonElement element, string property, string fallback = "")
        => element.ValueKind == JsonValueKind.Object
           && element.TryGetProperty(property, out JsonElement value)
           && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? fallback
            : fallback;

    private void AppendLog(string message)
    {
        void Add()
        {
            Log.Insert(0, $"{DateTime.Now:HH:mm:ss.fff}  {message}");
            while (Log.Count > 300)
            {
                Log.RemoveAt(Log.Count - 1);
            }
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            Add();
        }
        else
        {
            Dispatcher.UIThread.Post(Add);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        PreviewImage?.Dispose();
        _bridge.Dispose();
    }
}
