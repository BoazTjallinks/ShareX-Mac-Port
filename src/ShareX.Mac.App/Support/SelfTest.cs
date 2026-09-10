using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ShareX.Core.Errors;
using ShareX.Core.Geometry;
using ShareX.Platform.Mac;
using ShareX.Platform.Mac.Interop;

namespace ShareX.Mac.App.Support;

/// <summary>
/// Headless integration check, run with <c>ShareX-Mac --selftest</c>.
///
/// This exists because the stage-0 exit gate cannot be satisfied by a unit test
/// or by <c>dotnet run</c>: macOS binds the Screen Recording (TCC) grant to a
/// bundle identity, so the only meaningful evidence is the real app bundle
/// exercising the real native bridge. Output is plain text plus a machine-readable
/// JSON tail, so it can be pasted into planning/PROGRESS.md as evidence.
///
/// It never fabricates a pass: a permission that is not granted is reported as
/// such, and a failed capture is reported with its structured error.
/// </summary>
public static class SelfTest
{
    public static async Task<int> RunAsync(bool attemptCapture)
    {
        var report = new StringBuilder();
        var results = new Dictionary<string, object?>();
        bool allRequiredPassed = true;

        void Line(string text)
        {
            report.AppendLine(text);
            Console.WriteLine(text);
        }

        Line("ShareX-Mac self-test");
        Line("====================");
        Line($"time                : {DateTimeOffset.Now:O}");
        Line($"process path        : {Environment.ProcessPath}");
        Line($"running from bundle : {BundleInfo.IsBundled}");
        Line($"bundle path         : {BundleInfo.BundlePath ?? "(none)"}");
        Line($"runtime             : .NET {Environment.Version}");
        Line("");

        results["bundled"] = BundleInfo.IsBundled;
        results["bundlePath"] = BundleInfo.BundlePath;

        if (!BundleInfo.IsBundled)
        {
            Line("WARNING: not running from an .app bundle. macOS cannot grant this");
            Line("         process Screen Recording permission, so any capture below");
            Line("         is expected to fail with PermissionRequired.");
            Line("");
        }

        NativeBridge bridge;
        try
        {
            bridge = new NativeBridge();
        }
        catch (Exception ex)
        {
            Line($"FAIL  native bridge   : {ex.Message}");
            results["nativeBridge"] = "failed";
            results["nativeBridgeError"] = ex.Message;
            Emit(results, false);
            return 1;
        }

        using (bridge)
        {
            var capture = new MacCaptureService(bridge);

            // ---- capabilities -------------------------------------------------
            var stopwatch = Stopwatch.StartNew();
            OperationOutcome<CapabilitiesInfo> capabilities =
                await capture.GetCapabilitiesAsync().ConfigureAwait(false);
            stopwatch.Stop();

            if (capabilities.IsSuccess)
            {
                JsonElement raw = capabilities.Value.Raw;
                Line($"PASS  capabilities.get: {stopwatch.ElapsedMilliseconds} ms");
                Line($"      os            : {Str(raw, "osVersion")}");
                Line($"      arch          : {Str(raw, "architecture")}");
                Line($"      abi           : {Num(raw, "abiVersion")}");
                Line($"      bundle id     : {Str(raw, "bundleIdentifier")}");
                if (raw.TryGetProperty("features", out JsonElement features))
                {
                    Line($"      features      : {features}");
                }

                results["capabilities"] = "pass";
                results["osVersion"] = Str(raw, "osVersion");
                results["architecture"] = Str(raw, "architecture");
                results["abiVersion"] = Num(raw, "abiVersion");
                results["nativeBundleId"] = Str(raw, "bundleIdentifier");
            }
            else
            {
                Line($"FAIL  capabilities.get: {capabilities.Error.Kind} — {capabilities.Error.Message}");
                results["capabilities"] = $"fail:{capabilities.Error.Kind}";
                allRequiredPassed = false;
            }

            Line("");

            // ---- permissions --------------------------------------------------
            OperationOutcome<PermissionsInfo> permissions =
                await capture.GetPermissionsAsync().ConfigureAwait(false);

            string screenRecording = "unknown";
            if (permissions.IsSuccess)
            {
                JsonElement raw = permissions.Value.Raw;
                screenRecording = Str(raw, "screenRecording");
                Line("PASS  permissions.get");
                Line($"      screenRecording: {screenRecording}");
                Line($"      accessibility  : {Str(raw, "accessibility")}");
                Line($"      microphone     : {Str(raw, "microphone")}");

                results["permissions"] = "pass";
                results["screenRecording"] = screenRecording;
                results["accessibility"] = Str(raw, "accessibility");
                results["microphone"] = Str(raw, "microphone");
            }
            else
            {
                Line($"FAIL  permissions.get : {permissions.Error.Kind} — {permissions.Error.Message}");
                results["permissions"] = $"fail:{permissions.Error.Kind}";
                allRequiredPassed = false;
            }

            Line("");

            // ---- displays -----------------------------------------------------
            OperationOutcome<IReadOnlyList<DisplayInfo>> displays =
                await capture.ListDisplaysAsync().ConfigureAwait(false);

            if (displays.IsSuccess)
            {
                Line($"PASS  displays.list   : {displays.Value.Count} display(s)");
                foreach (DisplayInfo display in displays.Value)
                {
                    PointRect b = display.BoundsGlobalPoints;
                    Line($"      #{display.DisplayId} {b.Width:0}x{b.Height:0} pt @{display.Scale:0.##}x "
                         + $"origin ({b.X:0},{b.Y:0}) space={b.Space} "
                         + $"{(display.IsMain ? "main " : "")}{(display.IsActive ? "active" : "")}");
                }

                results["displays"] = displays.Value.Count;
            }
            else
            {
                Line($"FAIL  displays.list   : {displays.Error.Kind} — {displays.Error.Message}");
                results["displays"] = $"fail:{displays.Error.Kind}";
                allRequiredPassed = false;
            }

            Line("");

            // ---- windows ------------------------------------------------------
            OperationOutcome<IReadOnlyList<WindowInfo>> windows =
                await capture.ListWindowsAsync().ConfigureAwait(false);

            if (windows.IsSuccess)
            {
                Line($"PASS  windows.list    : {windows.Value.Count} window(s)");
                results["windows"] = windows.Value.Count;
            }
            else
            {
                Line($"FAIL  windows.list    : {windows.Error.Kind} — {windows.Error.Message}");
                results["windows"] = $"fail:{windows.Error.Kind}";
                allRequiredPassed = false;
            }

            Line("");

            // ---- encoder discovery --------------------------------------------
            NativeResponse encoders = await bridge
                .InvokeAsync("media.encoders", null, CancellationToken.None)
                .ConfigureAwait(false);

            if (encoders.Code == NativeResultCode.Ok)
            {
                Line("PASS  media.encoders  : (discovered, not assumed)");
                Line($"      {encoders.Payload}");
                results["mediaEncoders"] = "pass";
            }
            else
            {
                Line($"FAIL  media.encoders  : {encoders.Code}");
                results["mediaEncoders"] = $"fail:{encoders.Code}";
                allRequiredPassed = false;
            }

            Line("");

            // ---- real capture --------------------------------------------------
            if (!attemptCapture)
            {
                Line("SKIP  capture         : --no-capture requested");
                results["capture"] = "skipped";
            }
            else
            {
                DateTime now = DateTime.Now;
                string folder = AppPaths.EnsureDirectory(AppPaths.ScreenshotsFolderFor(now));
                string path = Path.Combine(folder, $"selftest_{now:yyyy-MM-dd_HH-mm-ss}.png");

                stopwatch.Restart();
                OperationOutcome<CaptureResult> shot = await capture
                    .CaptureAsync(CaptureTarget.ActiveDisplay(), showsCursor: false, outputPath: path)
                    .ConfigureAwait(false);
                stopwatch.Stop();

                if (shot.IsSuccess)
                {
                    CaptureResult result = shot.Value;
                    string written = result.OutputPath ?? path;
                    long size = File.Exists(written) ? new FileInfo(written).Length : 0;

                    Line($"PASS  capture         : {stopwatch.ElapsedMilliseconds} ms");
                    Line($"      pixels        : {result.PixelWidth}x{result.PixelHeight}");
                    Line($"      scale         : {result.CompositionScale:0.##}");
                    Line($"      source rect   : {(result.SourceRect is { } r
                        ? $"({r.X:0},{r.Y:0}) {r.Width:0}x{r.Height:0} {r.Space}"
                        : "unspecified")}");
                    Line($"      file          : {written} ({size:N0} bytes)");

                    results["capture"] = "pass";
                    results["capturePixels"] = $"{result.PixelWidth}x{result.PixelHeight}";
                    results["captureScale"] = result.CompositionScale;
                    results["captureFile"] = written;
                    results["captureBytes"] = size;

                    // A zero-byte or absent file is a failure even if the call succeeded.
                    if (size <= 0)
                    {
                        Line("FAIL  capture         : the call succeeded but no bytes were written.");
                        results["capture"] = "fail:empty-output";
                        allRequiredPassed = false;
                    }
                }
                else
                {
                    TaskError error = shot.Error;
                    Line($"FAIL  capture         : {error.Kind} — {error.Message}");
                    results["capture"] = $"fail:{error.Kind}";

                    if (error.Kind is TaskErrorKind.PermissionRequired or TaskErrorKind.PermissionDenied)
                    {
                        Line("");
                        Line("      This is the expected result until Screen Recording is granted to");
                        Line("      this bundle. Grant it in System Settings → Privacy & Security →");
                        Line("      Screen & System Audio Recording, then run the self-test again.");
                    }

                    allRequiredPassed = false;
                }
            }

            Line("");
            Line(allRequiredPassed
                ? "RESULT: all checks passed."
                : "RESULT: one or more checks did not pass (see above).");
        }

        Emit(results, allRequiredPassed);
        return allRequiredPassed ? 0 : 1;
    }

    private static void Emit(Dictionary<string, object?> results, bool passed)
    {
        results["passed"] = passed;
        Console.WriteLine();
        Console.WriteLine("--- json ---");
        Console.WriteLine(JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string Str(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object
           && element.TryGetProperty(name, out JsonElement value)
           && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? "" : "(absent)";

    private static string Num(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object
           && element.TryGetProperty(name, out JsonElement value)
           && value.ValueKind == JsonValueKind.Number
            ? value.ToString() : "(absent)";
}
