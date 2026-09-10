using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ShareX.Core.Errors;
using ShareX.Core.Geometry;
using ShareX.Platform.Mac.Interop;

namespace ShareX.Platform.Mac
{
    public sealed record DisplayInfo(uint DisplayId, PointRect BoundsGlobalPoints, double Scale, bool IsActive, bool IsMain);

    public sealed record WindowInfo(uint WindowNumber, string? Title, bool IsActive);

    public sealed record CaptureResult(
        int PixelWidth,
        int PixelHeight,
        double CompositionScale,
        PointRect? SourceRect,
        byte[]? Png,
        string? OutputPath);

    public sealed record PermissionRequestResult(string Permission, string State, string? Note);

    /// <summary>Raw snapshot from the "capabilities.get" route; SXMCapabilities' own field shape is not part of this port's read set.</summary>
    public sealed record CapabilitiesInfo(JsonElement Raw);

    /// <summary>Raw snapshot from the "permissions.get" route; SXMPermissions' own field shape is not part of this port's read set.</summary>
    public sealed record PermissionsInfo(JsonElement Raw);

    /// <summary>
    /// Where a capture should be taken from. Mirrors the `target` shapes handled by the
    /// "capture.screenshot" route in native/ShareXMacNative/swift/SXMRoutes.swift.
    /// </summary>
    public abstract record CaptureTarget
    {
        private CaptureTarget()
        {
        }

        public static CaptureTarget AllDisplays() => new AllDisplaysTarget();

        public static CaptureTarget ActiveDisplay() => new ActiveDisplayTarget();

        public static CaptureTarget Display(uint displayId) => new DisplayIdTarget(displayId);

        public static CaptureTarget ActiveWindow() => new ActiveWindowTarget();

        public static CaptureTarget Window(uint windowId) => new WindowIdTarget(windowId);

        /// <summary>
        /// A region in either <see cref="CoordinateSpace.CgGlobalPoints"/> or
        /// <see cref="CoordinateSpace.AppKitPoints"/> -- the only two spaces the native
        /// "capture.screenshot" route accepts for kind "region".
        /// </summary>
        public static CaptureTarget Region(PointRect rect)
        {
            if (rect.Space is not (CoordinateSpace.CgGlobalPoints or CoordinateSpace.AppKitPoints))
            {
                throw new ArgumentException(
                    $"Native capture regions only support {nameof(CoordinateSpace.CgGlobalPoints)} or " +
                    $"{nameof(CoordinateSpace.AppKitPoints)}, not '{rect.Space}'.",
                    nameof(rect));
            }

            return new RegionTarget(rect);
        }

        internal abstract Dictionary<string, object?> ToJson();

        private sealed record AllDisplaysTarget : CaptureTarget
        {
            internal override Dictionary<string, object?> ToJson() => new() { ["kind"] = "allDisplays" };
        }

        private sealed record ActiveDisplayTarget : CaptureTarget
        {
            internal override Dictionary<string, object?> ToJson() => new() { ["kind"] = "activeDisplay" };
        }

        private sealed record DisplayIdTarget(uint DisplayId) : CaptureTarget
        {
            internal override Dictionary<string, object?> ToJson() => new()
            {
                ["kind"] = "display",
                ["displayId"] = DisplayId
            };
        }

        private sealed record ActiveWindowTarget : CaptureTarget
        {
            internal override Dictionary<string, object?> ToJson() => new() { ["kind"] = "activeWindow" };
        }

        private sealed record WindowIdTarget(uint WindowId) : CaptureTarget
        {
            internal override Dictionary<string, object?> ToJson() => new()
            {
                ["kind"] = "window",
                ["windowId"] = WindowId
            };
        }

        private sealed record RegionTarget(PointRect Rect) : CaptureTarget
        {
            internal override Dictionary<string, object?> ToJson() => new()
            {
                ["kind"] = "region",
                ["rect"] = new Dictionary<string, object?>
                {
                    ["x"] = Rect.X,
                    ["y"] = Rect.Y,
                    ["width"] = Rect.Width,
                    ["height"] = Rect.Height
                },
                // These strings are the ABI's own SXMSpace raw values
                // (native/ShareXMacNative/swift/SXMGeometry.swift). They are
                // kebab-case, NOT the C# enum spelling: the native route rejects
                // any other value with SXM_INVALID_INPUT rather than guessing.
                ["space"] = Rect.Space == CoordinateSpace.AppKitPoints
                    ? NativeSpaceNames.AppKitPoints
                    : NativeSpaceNames.CgGlobalPoints
            };
        }
    }

    /// <summary>
    /// Wire values for the native ABI's coordinate spaces, mirroring the SXMSpace
    /// raw values in native/ShareXMacNative/swift/SXMGeometry.swift. Kept in one
    /// place because a mismatch here fails only at runtime, on the native side.
    /// </summary>
    internal static class NativeSpaceNames
    {
        internal const string CgGlobalPoints = "cg-global-points";
        internal const string AppKitPoints = "appkit-points";
        internal const string DisplayPixels = "display-pixels";
        internal const string DisplayPoints = "display-points";
    }

    /// <summary>
    /// Clipboard and Finder services over the native routes in
    /// native/ShareXMacNative/swift/SXMClipboard.swift.
    ///
    /// Image / file / text are separate operations because upstream treats
    /// CopyImageToClipboard, CopyFileToClipboard, CopyFilePathToClipboard and
    /// CopyFolderPathToClipboard as four distinct behaviours with a defined
    /// precedence (PROJECT-SPEC.md section 8).
    /// </summary>
    public sealed class MacClipboardService
    {
        private readonly NativeBridge _bridge;

        public MacClipboardService(NativeBridge bridge) => _bridge = bridge;

        public Task<OperationOutcome<bool>> CopyImageFileAsync(string path, CancellationToken ct = default)
            => VoidCall("clipboard.copyImageFile", new Dictionary<string, object?> { ["path"] = path }, ct);

        public Task<OperationOutcome<bool>> CopyTextAsync(string text, CancellationToken ct = default)
            => VoidCall("clipboard.copyText", new Dictionary<string, object?> { ["text"] = text }, ct);

        public Task<OperationOutcome<bool>> CopyFileAsync(string path, CancellationToken ct = default)
            => VoidCall("clipboard.copyFile", new Dictionary<string, object?> { ["path"] = path }, ct);

        public Task<OperationOutcome<bool>> RevealAsync(string path, CancellationToken ct = default)
            => VoidCall("finder.reveal", new Dictionary<string, object?> { ["path"] = path }, ct);

        public Task<OperationOutcome<bool>> OpenAsync(string path, CancellationToken ct = default)
            => VoidCall("finder.open", new Dictionary<string, object?> { ["path"] = path }, ct);

        public Task<OperationOutcome<bool>> OpenUrlAsync(string url, CancellationToken ct = default)
            => VoidCall("url.open", new Dictionary<string, object?> { ["url"] = url }, ct);

        /// <summary>
        /// What the clipboard currently holds. Deliberately returns only the
        /// category flags and a text LENGTH: clipboard contents can be sensitive
        /// and must not leak into diagnostics.
        /// </summary>
        public async Task<OperationOutcome<JsonElement>> GetAsync(CancellationToken ct = default)
        {
            NativeResponse response = await _bridge.InvokeAsync("clipboard.get", null, ct).ConfigureAwait(false);
            return response.Code == NativeResultCode.Ok
                ? OperationOutcome<JsonElement>.Success(response.Payload)
                : OperationOutcome<JsonElement>.Failure(MacCaptureService.ToTaskError(response));
        }

        private async Task<OperationOutcome<bool>> VoidCall(
            string op, Dictionary<string, object?> args, CancellationToken ct)
        {
            NativeResponse response = await _bridge.InvokeAsync(op, args, ct).ConfigureAwait(false);
            return response.Code == NativeResultCode.Ok
                ? OperationOutcome<bool>.Success(true)
                : OperationOutcome<bool>.Failure(MacCaptureService.ToTaskError(response));
        }
    }

    /// <summary>
    /// Typed wrappers over the native op routes defined in
    /// native/ShareXMacNative/swift/SXMRoutes.swift ("capabilities.get", "permissions.get",
    /// "permissions.request", "displays.list", "windows.list", "capture.screenshot").
    /// </summary>
    public sealed class MacCaptureService
    {
        private readonly NativeBridge _bridge;

        public MacCaptureService(NativeBridge bridge)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        }

        public async Task<OperationOutcome<CapabilitiesInfo>> GetCapabilitiesAsync(CancellationToken ct = default)
        {
            NativeResponse response = await _bridge.InvokeAsync("capabilities.get", null, ct).ConfigureAwait(false);
            if (response.Code != NativeResultCode.Ok)
            {
                return OperationOutcome<CapabilitiesInfo>.Failure(ToTaskError(response));
            }

            return OperationOutcome<CapabilitiesInfo>.Success(new CapabilitiesInfo(response.Payload));
        }

        public async Task<OperationOutcome<PermissionsInfo>> GetPermissionsAsync(CancellationToken ct = default)
        {
            NativeResponse response = await _bridge.InvokeAsync("permissions.get", null, ct).ConfigureAwait(false);
            if (response.Code != NativeResultCode.Ok)
            {
                return OperationOutcome<PermissionsInfo>.Failure(ToTaskError(response));
            }

            return OperationOutcome<PermissionsInfo>.Success(new PermissionsInfo(response.Payload));
        }

        /// <summary>
        /// Requests one of "screenRecording", "accessibility" or "microphone". For
        /// "accessibility", <paramref name="prompt"/> matches the route's own
        /// `ctx.bool("prompt", default: true)` argument; it is ignored for the other two
        /// permission kinds, exactly as the native route ignores it for them.
        /// </summary>
        public async Task<OperationOutcome<PermissionRequestResult>> RequestPermissionAsync(
            string permission, bool prompt = true, CancellationToken ct = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(permission);

            var args = new Dictionary<string, object?> { ["permission"] = permission };
            if (string.Equals(permission, "accessibility", StringComparison.Ordinal))
            {
                args["prompt"] = prompt;
            }

            NativeResponse response = await _bridge.InvokeAsync("permissions.request", args, ct).ConfigureAwait(false);
            if (response.Code != NativeResultCode.Ok)
            {
                return OperationOutcome<PermissionRequestResult>.Failure(ToTaskError(response));
            }

            string resolvedPermission = GetString(response.Payload, "permission") ?? permission;
            string state = GetString(response.Payload, "state") ?? string.Empty;
            string? note = GetString(response.Payload, "note");

            return OperationOutcome<PermissionRequestResult>.Success(
                new PermissionRequestResult(resolvedPermission, state, note));
        }

        public async Task<OperationOutcome<IReadOnlyList<DisplayInfo>>> ListDisplaysAsync(CancellationToken ct = default)
        {
            NativeResponse response = await _bridge.InvokeAsync("displays.list", null, ct).ConfigureAwait(false);
            if (response.Code != NativeResultCode.Ok)
            {
                return OperationOutcome<IReadOnlyList<DisplayInfo>>.Failure(ToTaskError(response));
            }

            JsonElement payload = response.Payload;
            uint? activeId = GetLong(payload, "activeDisplayId") is { } a ? (uint)a : null;
            uint mainId = (uint)(GetLong(payload, "mainDisplayId") ?? 0);

            var list = new List<DisplayInfo>();
            if (payload.ValueKind == JsonValueKind.Object &&
                payload.TryGetProperty("displays", out JsonElement displays) &&
                displays.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement element in displays.EnumerateArray())
                {
                    list.Add(ParseDisplay(element, activeId, mainId));
                }
            }

            return OperationOutcome<IReadOnlyList<DisplayInfo>>.Success(list);
        }

        public async Task<OperationOutcome<IReadOnlyList<WindowInfo>>> ListWindowsAsync(
            bool onScreenOnly = true, CancellationToken ct = default)
        {
            var args = new Dictionary<string, object?> { ["onScreenOnly"] = onScreenOnly };
            NativeResponse response = await _bridge.InvokeAsync("windows.list", args, ct).ConfigureAwait(false);
            if (response.Code != NativeResultCode.Ok)
            {
                return OperationOutcome<IReadOnlyList<WindowInfo>>.Failure(ToTaskError(response));
            }

            JsonElement payload = response.Payload;
            uint? activeId = GetLong(payload, "activeWindowId") is { } a ? (uint)a : null;
            bool titlesAvailable = payload.ValueKind == JsonValueKind.Object &&
                                    payload.TryGetProperty("titlesAvailable", out JsonElement t) &&
                                    t.ValueKind == JsonValueKind.True;

            var list = new List<WindowInfo>();
            if (payload.ValueKind == JsonValueKind.Object &&
                payload.TryGetProperty("windows", out JsonElement windows) &&
                windows.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement element in windows.EnumerateArray())
                {
                    list.Add(ParseWindow(element, activeId, titlesAvailable));
                }
            }

            return OperationOutcome<IReadOnlyList<WindowInfo>>.Success(list);
        }

        public async Task<OperationOutcome<CaptureResult>> CaptureAsync(
            CaptureTarget target,
            bool showsCursor = false,
            bool includeShadow = true,
            bool excludeSelf = true,
            double? compositionScale = null,
            string? outputPath = null,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(target);

            var args = new Dictionary<string, object?>
            {
                ["target"] = target.ToJson(),
                ["showsCursor"] = showsCursor,
                ["includeShadow"] = includeShadow,
                ["excludeSelf"] = excludeSelf
            };

            if (compositionScale is { } scale)
            {
                args["compositionScale"] = scale;
            }

            if (outputPath is not null)
            {
                args["outputPath"] = outputPath;
            }

            NativeResponse response = await _bridge.InvokeAsync("capture.screenshot", args, ct).ConfigureAwait(false);
            if (response.Code != NativeResultCode.Ok)
            {
                return OperationOutcome<CaptureResult>.Failure(ToTaskError(response));
            }

            JsonElement payload = response.Payload;
            int pixelWidth = GetInt(payload, "pixelWidth") ?? GetInt(payload, "width") ?? 0;
            int pixelHeight = GetInt(payload, "pixelHeight") ?? GetInt(payload, "height") ?? 0;
            double resolvedScale = GetDouble(payload, "compositionScale") ?? GetDouble(payload, "scale") ?? 1.0;
            PointRect? sourceRect = TryGetRect(payload, "sourceRect", CoordinateSpace.CgGlobalPoints);
            string? resolvedOutputPath = GetString(payload, "outputPath");

            var result = new CaptureResult(pixelWidth, pixelHeight, resolvedScale, sourceRect, response.Blob, resolvedOutputPath);
            return OperationOutcome<CaptureResult>.Success(result);
        }

        // Internal (not private) so ShareX.Platform.Mac.Tests can prove this never assumes a
        // JSON object is present: on the sxm_begin-returned-non-zero path in
        // NativeBridge.InvokeAsync, response.Payload is default(JsonElement) (ValueKind
        // Undefined), not an empty object. GetString below already guards on
        // ValueKind == JsonValueKind.Object before ever calling TryGetProperty, so it degrades
        // to the fallback message instead of throwing.
        internal static TaskError ToTaskError(NativeResponse response)
        {
            string message = GetString(response.Payload, "message")
                ?? GetString(response.Payload, "error")
                ?? $"Native operation failed with {response.Code}.";
            return new TaskError(response.Code.ToTaskErrorKind(), message);
        }

        // The exact JSON shape of SXMDisplayInfo.asJson / SXMWindowInfo.asJson lives in Swift
        // source files this port's task scope did not include (only SXMRoutes.swift was read).
        // These parsers are deliberately tolerant: they try the field names implied by
        // SXMRoutes.swift's own use of the underlying types (displayID, boundsGlobalPoints,
        // windowNumber) and fall back to sane defaults rather than throwing on an unexpected
        // shape, so a mismatch degrades gracefully instead of crashing capture entirely.
        private static DisplayInfo ParseDisplay(JsonElement element, uint? activeId, uint mainId)
        {
            uint id = (uint)(GetLong(element, "displayId") ?? GetLong(element, "id") ?? 0);
            PointRect bounds =
                TryGetRect(element, "boundsGlobalPoints", CoordinateSpace.CgGlobalPoints) ??
                TryGetRect(element, "bounds", CoordinateSpace.CgGlobalPoints) ??
                TryGetRect(element, "frame", CoordinateSpace.CgGlobalPoints) ??
                new PointRect(0, 0, 0, 0, CoordinateSpace.CgGlobalPoints);
            double scale = GetDouble(element, "scale") ?? GetDouble(element, "backingScaleFactor") ?? 1.0;

            return new DisplayInfo(id, bounds, scale, activeId.HasValue && activeId.Value == id, id == mainId);
        }

        private static WindowInfo ParseWindow(JsonElement element, uint? activeId, bool titlesAvailable)
        {
            uint number = (uint)(GetLong(element, "windowNumber") ?? GetLong(element, "id") ?? 0);
            string? title = titlesAvailable
                ? GetString(element, "title") ?? GetString(element, "windowTitle")
                : null;

            return new WindowInfo(number, title, activeId.HasValue && activeId.Value == number);
        }

        private static long? GetLong(JsonElement parent, string name) =>
            parent.ValueKind == JsonValueKind.Object &&
            parent.TryGetProperty(name, out JsonElement element) &&
            element.ValueKind == JsonValueKind.Number &&
            element.TryGetInt64(out long value)
                ? value
                : null;

        private static int? GetInt(JsonElement parent, string name) => GetLong(parent, name) is { } l ? (int)l : null;

        private static double? GetDouble(JsonElement parent, string name) =>
            parent.ValueKind == JsonValueKind.Object &&
            parent.TryGetProperty(name, out JsonElement element) &&
            element.ValueKind == JsonValueKind.Number &&
            element.TryGetDouble(out double value)
                ? value
                : null;

        private static string? GetString(JsonElement parent, string name) =>
            parent.ValueKind == JsonValueKind.Object &&
            parent.TryGetProperty(name, out JsonElement element) &&
            element.ValueKind == JsonValueKind.String
                ? element.GetString()
                : null;

        private static PointRect? TryGetRect(JsonElement parent, string propertyName, CoordinateSpace space)
        {
            if (parent.ValueKind != JsonValueKind.Object ||
                !parent.TryGetProperty(propertyName, out JsonElement rect) ||
                rect.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            double x = GetDouble(rect, "x") ?? 0;
            double y = GetDouble(rect, "y") ?? 0;
            double w = GetDouble(rect, "width") ?? 0;
            double h = GetDouble(rect, "height") ?? 0;
            return new PointRect(x, y, w, h, space);
        }
    }
}
