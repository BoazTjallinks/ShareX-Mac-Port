using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShareX.Core.Enums;
using ShareX.Core.Errors;
using ShareX.Core.Workflow;
using ShareX.Mac.App.Models;
using ShareX.Mac.App.Services;
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

/// <summary>One after-capture stage from the most recent task, for the diagnostics view.</summary>
public sealed class WorkflowStageViewModel
{
    public WorkflowStageViewModel(WorkflowStage stage)
    {
        Name = stage.Name;
        Detail = stage.Detail;
        // A stage that failed is visually distinct from one that was simply not
        // requested: the trace must never make a failure look like a skip.
        Marker = stage.Error is not null ? "!" : stage.Ran ? "\u2713" : "\u00b7";
    }

    public string Name { get; }
    public string Detail { get; }
    public string Marker { get; }
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

    private readonly CaptureCommandService _commands;
    private readonly MacClipboardService _clipboard;
    private readonly MacWorkflowServices _workflowServices;

    /// <summary>Set by the view so the region overlay can be owned by the main window.</summary>
    public Func<Avalonia.Controls.Window?> OwnerWindowAccessor { get; set; } = () => null;

    public MainWindowViewModel()
    {
        _capture = new MacCaptureService(_bridge);
        _clipboard = new MacClipboardService(_bridge);
        _workflowServices = new MacWorkflowServices(_clipboard);
        _commands = new CaptureCommandService(_capture, _workflowServices, () => OwnerWindowAccessor());

        foreach (AfterCaptureTaskViewModel task in
                 AfterCaptureCatalog.Build(_commands.Settings.AfterCaptureJob))
        {
            task.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(AfterCaptureTaskViewModel.IsChecked))
                {
                    ApplyAfterCaptureSelection();
                }
            };
            AfterCaptureTasksList.Add(task);
        }

        _commands.Settings = _commands.Settings with
        {
            ScreenshotsFolder = AppPaths.DefaultScreenshotsFolder
        };
    }

    /// <summary>
    /// Runs a ShareX command by its upstream identity. Every trigger - rail
    /// button, menu item, later a hotkey or the CLI - goes through here, so
    /// behaviour cannot drift between entry points.
    /// </summary>
    [RelayCommand]
    private async Task RunCommandAsync(CommandEntry? entry)
    {
        if (entry is null || entry.IsSeparator)
        {
            return;
        }

        if (!entry.IsExecutable)
        {
            // Honest feedback instead of a dead control.
            StatusText = entry.HasChildren
                ? $"{entry.Label}: choose an item from the submenu."
                : $"{entry.Label} is not implemented yet in this build.";
            AppendLog($"command {entry.Label}: no upstream HotkeyType / not implemented");
            return;
        }

        if (IsBusy)
        {
            StatusText = "Another capture is already running.";
            return;
        }

        IsBusy = true;
        var row = new TaskRowViewModel
        {
            Status = "Working",
            FileName = entry.Label,
            Detail = entry.Hotkey.ToString()
        };
        Tasks.Insert(0, row);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            CommandRun run = await _commands.ExecuteAsync(entry.Hotkey).ConfigureAwait(true);
            stopwatch.Stop();

            if (!run.Handled)
            {
                row.Status = "Unavailable";
                row.Detail = run.Message;
                StatusText = run.Message;
            }
            else if (run.Cancelled)
            {
                // Cancellation must leave no artifact and no false success.
                row.Status = "Cancelled";
                row.Detail = run.Message;
                StatusText = run.Message;
            }
            else if (run.FilePath is { } file)
            {
                row.Status = "Completed";
                row.FileName = Path.GetFileName(file);
                row.FilePath = file;
                row.Detail = $"{run.PixelWidth}x{run.PixelHeight} px @{run.Scale:0.##}x";
                StatusText = run.Message;

                PreviewCaption =
                    $"{entry.Label} - {run.PixelWidth}x{run.PixelHeight} px, scale {run.Scale:0.##} - {file}";
                LoadPreview(file);
            }
            else
            {
                row.Status = "Failed";
                row.Detail = run.Message;
                StatusText = run.Message;
                PreviewCaption = $"{entry.Label} failed - {run.Message}";
            }

            LastWorkflowStages.Clear();
            if (run.Stages is { } stages)
            {
                foreach (WorkflowStage stage in stages)
                {
                    LastWorkflowStages.Add(new WorkflowStageViewModel(stage));
                }
            }

            AppendLog($"command {entry.Hotkey} -> {row.Status} in {stopwatch.ElapsedMilliseconds} ms: {run.Message}");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            row.Status = "Failed";
            row.Detail = ex.Message;
            StatusText = $"{entry.Label} failed: {ex.Message}";
            AppendLog($"command {entry.Hotkey} threw: {ex}");
        }
        finally
        {
            IsBusy = false;
            // Permission state can change as a result of a capture attempt (the
            // first attempt is what triggers the macOS prompt), so re-read it.
            await RefreshPermissionsAsync().ConfigureAwait(true);
        }
    }

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

    /// <summary>Upstream's "After capture tasks" set, all 22 non-zero flags.</summary>
    public ObservableCollection<AfterCaptureTaskViewModel> AfterCaptureTasksList { get; } = new();

    /// <summary>Stage trace of the most recent task, shown in Diagnostics.</summary>
    public ObservableCollection<WorkflowStageViewModel> LastWorkflowStages { get; } = new();

    [ObservableProperty] private string _environmentSummary = "Querying the native bridge…";
    [ObservableProperty] private string _bundleSummary = "";
    [ObservableProperty] private Bitmap? _previewImage;
    [ObservableProperty] private string _previewCaption = "No capture yet.";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusText = "Ready";

    /// <summary>
    /// True when a permission was just granted or is still pending: macOS only
    /// applies a new Screen Recording grant to a freshly launched process.
    /// </summary>
    [ObservableProperty] private bool _needsRelaunch;

    public bool IsBundled => BundleInfo.IsBundled;

    public void InitializeAsync() => _ = RefreshAllAsync();

    /// <summary>
    /// Rebuilds the flag set from the checkboxes. The result is a new immutable
    /// snapshot, so a job already running keeps the settings it started with.
    /// </summary>
    private void ApplyAfterCaptureSelection()
    {
        AfterCaptureTasks flags = AfterCaptureTasks.None;
        foreach (AfterCaptureTaskViewModel task in AfterCaptureTasksList)
        {
            if (task.IsChecked)
            {
                flags |= task.Flag;
            }
        }

        _commands.Settings = _commands.Settings with { AfterCaptureJob = flags };
        AppendLog($"after-capture tasks set to: {flags}");
    }

    // ---------------------------------------------------------------- commands

    [RelayCommand]
    private async Task RefreshAllAsync()
    {
        await RefreshCapabilitiesAsync().ConfigureAwait(true);
        await RefreshPermissionsAsync().ConfigureAwait(true);
        await EnsureScreenRecordingAsync().ConfigureAwait(true);
        await RefreshDisplaysAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// Asks for Screen Recording on startup when it is missing, because every
    /// capture command needs it and ShareX's whole purpose depends on it.
    ///
    /// Two macOS behaviours make this necessary rather than optional:
    /// <list type="bullet">
    /// <item>Preflight checks never prompt, so an app that only preflights can
    /// report "permission required" forever without macOS ever asking, and may
    /// not even appear in the Privacy list.</item>
    /// <item>The prompt is asynchronous: the request call returns the current
    /// (still-denied) state immediately. The app must therefore stay running for
    /// the dialog to be usable, and macOS requires a relaunch before a new grant
    /// takes effect for screen capture.</item>
    /// </list>
    /// </summary>
    private async Task EnsureScreenRecordingAsync()
    {
        PermissionRowViewModel? row =
            Permissions.FirstOrDefault(p => p.Key == "screenRecording");

        if (row is null || row.IsGranted)
        {
            return;
        }

        AppendLog("screen recording not granted — asking macOS to prompt");
        OperationOutcome<PermissionRequestResult> outcome =
            await _capture.RequestPermissionAsync("screenRecording").ConfigureAwait(true);

        if (outcome.IsSuccess)
        {
            row.State = outcome.Value.State;
        }

        if (row.IsGranted)
        {
            StatusText = "Screen Recording granted.";
            NeedsRelaunch = false;
            return;
        }

        // Do not pretend this is usable yet, and do not silently retry.
        NeedsRelaunch = true;
        StatusText = "Screen Recording is required. Approve ShareX-Mac in the macOS "
                     + "dialog (or in System Settings), then quit and reopen ShareX-Mac.";
        AppendLog($"screen recording still '{row.State}' — relaunch needed after granting");
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

    // Toolbar shortcuts. They route through the same dispatcher as the menu, so
    // there is exactly one implementation per command.
    [RelayCommand]
    private Task CaptureFullscreenAsync() =>
        RunCommandAsync(new CommandEntry("Fullscreen", HotkeyType.PrintScreen));

    [RelayCommand]
    private Task CaptureActiveMonitorAsync() =>
        RunCommandAsync(new CommandEntry("Monitor", HotkeyType.ActiveMonitor));

    [RelayCommand]
    private Task CaptureActiveWindowAsync() =>
        RunCommandAsync(new CommandEntry("Window", HotkeyType.ActiveWindow));

    [RelayCommand]
    private Task CaptureRegionAsync() =>
        RunCommandAsync(new CommandEntry("Region", HotkeyType.RectangleRegion));

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

    /// <summary>
    /// Quits and reopens the app, which is what macOS requires before a new
    /// Screen Recording grant applies. Uses `open -n` on the bundle so the new
    /// process is its own LaunchServices instance rather than a child of this one
    /// (a child can inherit the parent's TCC attribution and mask the real state).
    /// </summary>
    [RelayCommand]
    private void RelaunchApp()
    {
        string? bundle = BundleInfo.BundlePath;
        if (bundle is null)
        {
            StatusText = "Relaunch is only available when running from the .app bundle.";
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "/bin/sh",
                // Small delay so this process is gone before the new one starts.
                ArgumentList = { "-c", $"sleep 1; open -n \"{bundle}\"" },
                UseShellExecute = false
            });
        }
        catch (Exception ex)
        {
            StatusText = $"Could not relaunch: {ex.Message}";
            return;
        }

        if (Avalonia.Application.Current?.ApplicationLifetime
            is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    /// <summary>
    /// Opens the diagnostics window. Permissions, display geometry, the native
    /// capability report and the operation log live there rather than on the main
    /// window, which stays focused on capturing.
    /// </summary>
    [RelayCommand]
    private void OpenDiagnostics()
    {
        if (_diagnosticsWindow is { } existing)
        {
            existing.Activate();
            return;
        }

        var window = new Views.DiagnosticsWindow { DataContext = this };
        window.Closed += (_, _) => _diagnosticsWindow = null;
        _diagnosticsWindow = window;

        Avalonia.Controls.Window? owner = OwnerWindowAccessor();
        if (owner is not null)
        {
            window.Show(owner);
        }
        else
        {
            window.Show();
        }
    }

    private Views.DiagnosticsWindow? _diagnosticsWindow;

    [RelayCommand]
    private async Task OpenScreenshotsFolderAsync()
    {
        string folder = AppPaths.EnsureDirectory(AppPaths.DefaultScreenshotsFolder);
        OperationOutcome<bool> opened = await _clipboard.OpenAsync(folder).ConfigureAwait(true);
        StatusText = opened.IsSuccess ? $"Opened {folder}" : opened.Error!.Message;
        AppendLog($"open screenshots folder -> {(opened.IsSuccess ? "ok" : opened.Error!.Kind.ToString())}");
    }

    /// <summary>Reveals the currently previewed capture in Finder.</summary>
    [RelayCommand]
    private async Task RevealLastCaptureAsync()
    {
        TaskRowViewModel? last = Tasks.FirstOrDefault(t => !string.IsNullOrEmpty(t.FilePath));
        if (last is null)
        {
            StatusText = "No capture to reveal yet.";
            return;
        }

        OperationOutcome<bool> revealed = await _clipboard.RevealAsync(last.FilePath).ConfigureAwait(true);
        StatusText = revealed.IsSuccess ? $"Revealed {last.FileName}" : revealed.Error!.Message;
    }

    /// <summary>Copies the currently previewed capture to the clipboard on demand.</summary>
    [RelayCommand]
    private async Task CopyLastCaptureAsync()
    {
        TaskRowViewModel? last = Tasks.FirstOrDefault(t => !string.IsNullOrEmpty(t.FilePath));
        if (last is null)
        {
            StatusText = "No capture to copy yet.";
            return;
        }

        OperationOutcome<bool> copied = await _clipboard.CopyImageFileAsync(last.FilePath).ConfigureAwait(true);
        StatusText = copied.IsSuccess ? $"Copied {last.FileName} to the clipboard" : copied.Error!.Message;
        AppendLog($"copy image -> {(copied.IsSuccess ? "ok" : copied.Error!.Kind.ToString())}");
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
