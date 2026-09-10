using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ShareX.Platform.Mac.Interop
{
    /// <summary>The terminal result of one sxm_begin operation.</summary>
    public sealed record NativeResponse(NativeResultCode Code, JsonElement Payload, byte[]? Blob);

    /// <summary>
    /// One low-rate event delivered through sxm_set_event_sink (recording state, hotkey
    /// presses, display reconfiguration, ...). Never carries media data.
    /// </summary>
    public sealed record NativeEvent(ulong SubscriptionId, JsonElement Payload);

    /// <summary>
    /// Builds the `{"op":..,"abi":1,"args":{...}}` request envelope. Split out from
    /// <see cref="NativeBridge"/> so tests can verify the JSON shape without touching the
    /// native library (see ShareX.Platform.Mac.Tests).
    /// </summary>
    internal static class NativeRequestBuilder
    {
        internal static readonly JsonSerializerOptions Options = new(JsonSerializerOptions.Default);

        internal static byte[] BuildRequestJson(string op, object? args, uint abiVersion)
        {
            ArgumentException.ThrowIfNullOrEmpty(op);

            var request = new Dictionary<string, object?>
            {
                ["op"] = op,
                ["abi"] = abiVersion,
                ["args"] = args ?? new Dictionary<string, object?>()
            };

            return JsonSerializer.SerializeToUtf8Bytes(request, Options);
        }
    }

    /// <summary>
    /// Decides whether the one-time ABI version check has already succeeded, without itself
    /// calling into the native library -- kept separate from <see cref="NativeBridge"/> so the
    /// "only latch success, keep re-checking after a failure" rule is unit-testable without a
    /// dylib. A mismatch must never latch: every call before a successful check re-verifies
    /// (and rethrows) rather than silently trusting a version that was never confirmed.
    /// </summary>
    internal static class AbiVersionGate
    {
        internal static bool CheckAndLatch(uint reportedVersion, uint expectedVersion, ref bool verified)
        {
            if (verified)
            {
                return true;
            }

            if (reportedVersion != expectedVersion)
            {
                return false;
            }

            verified = true;
            return true;
        }
    }

    /// <summary>
    /// Managed side of the ShareX-Mac native bridge (native/ShareXMacNative/include/sxm_abi.h).
    /// One <see cref="NativeBridge"/> owns the process-wide event sink and issues one-shot
    /// request/response operations against sxm_begin/sxm_cancel/sxm_release/sxm_copy_blob.
    /// </summary>
    public sealed class NativeBridge : IDisposable
    {
        private const uint ExpectedAbiVersion = 1;

        // Guards the one-time sxm_abi_version() check across all NativeBridge instances in
        // this process -- the native library itself is process-wide. Only ever latched to
        // true by AbiVersionGate.CheckAndLatch on a *successful* check; a mismatch leaves it
        // false so every subsequent call keeps re-verifying and keeps throwing.
        private static bool s_abiVerified;

        private readonly object _eventGate = new();
        private readonly ConcurrentDictionary<ulong, EventHandler<NativeEvent>> _subscriptionHandlers = new();
        private event EventHandler<NativeEvent>? _eventReceived;

        private GCHandle _selfHandle;
        private bool _eventSinkInstalled;
        private volatile bool _disposed;

        /// <summary>Per-operation state, kept alive via a GCHandle stashed as the native user_context.</summary>
        private sealed class PendingOperation
        {
            public readonly TaskCompletionSource<NativeResponse> Completion =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            public ulong OperationId;
        }

        public NativeBridge()
        {
            EnsureAbiVersion();
        }

        /// <summary>
        /// Raised for every event delivered through the process-wide native event sink.
        /// Subscribing installs the sink (idempotent, lazy, thread-safe) on first use.
        /// </summary>
        public event EventHandler<NativeEvent>? EventReceived
        {
            add
            {
                EnsureEventSinkInstalled();
                _eventReceived += value;
            }
            remove => _eventReceived -= value;
        }

        /// <summary>
        /// Routes events for one native subscription id to <paramref name="handler"/> in
        /// addition to <see cref="EventReceived"/>. Dispose the returned token to stop.
        /// </summary>
        public IDisposable SubscribeToEvents(ulong subscriptionId, EventHandler<NativeEvent> handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            EnsureEventSinkInstalled();

            _subscriptionHandlers.AddOrUpdate(
                subscriptionId,
                handler,
                (_, existing) => (EventHandler<NativeEvent>)Delegate.Combine(existing, handler));

            return new SubscriptionToken(this, subscriptionId, handler);
        }

        private void RemoveSubscription(ulong subscriptionId, EventHandler<NativeEvent> handler)
        {
            while (_subscriptionHandlers.TryGetValue(subscriptionId, out EventHandler<NativeEvent>? current))
            {
                var updated = (EventHandler<NativeEvent>?)Delegate.Remove(current, handler);
                if (updated is null)
                {
                    if (_subscriptionHandlers.TryRemove(subscriptionId, out _))
                    {
                        return;
                    }
                }
                else if (_subscriptionHandlers.TryUpdate(subscriptionId, updated, current))
                {
                    return;
                }
                // Lost a race with a concurrent subscriber change; retry.
            }
        }

        private sealed class SubscriptionToken(NativeBridge owner, ulong subscriptionId, EventHandler<NativeEvent> handler) : IDisposable
        {
            private int _disposed;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 0)
                {
                    owner.RemoveSubscription(subscriptionId, handler);
                }
            }
        }

        private static void EnsureAbiVersion()
        {
            if (Volatile.Read(ref s_abiVerified))
            {
                return;
            }

            uint reported = NativeMethods.sxm_abi_version();
            bool ok = AbiVersionGate.CheckAndLatch(reported, ExpectedAbiVersion, ref s_abiVerified);
            if (!ok)
            {
                throw new InvalidOperationException(
                    $"ShareXMacNative ABI version mismatch: managed layer targets {ExpectedAbiVersion} " +
                    $"but the native library reports {reported}. Rebuild both sides against the same sxm_abi.h.");
            }
        }

        private void EnsureEventSinkInstalled()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            EnsureAbiVersion();

            if (Volatile.Read(ref _eventSinkInstalled))
            {
                return;
            }

            lock (_eventGate)
            {
                if (_eventSinkInstalled)
                {
                    return;
                }

                if (!_selfHandle.IsAllocated)
                {
                    _selfHandle = GCHandle.Alloc(this, GCHandleType.Normal);
                }

                int rc = InstallEventSink(_selfHandle);
                if (rc != 0)
                {
                    throw new InvalidOperationException(
                        $"sxm_set_event_sink failed with result code {(NativeResultCode)rc}.");
                }

                _eventSinkInstalled = true;
            }
        }

        private static unsafe int InstallEventSink(GCHandle handle) =>
            NativeMethods.sxm_set_event_sink(&EventTrampoline, (void*)GCHandle.ToIntPtr(handle));

        /// <summary>
        /// Issues one native operation and awaits its terminal completion.
        /// </summary>
        /// <remarks>
        /// Cancelling <paramref name="ct"/> calls sxm_cancel but is NOT the terminal
        /// acknowledgement per the ABI contract: this method still waits for the real
        /// completion callback (which will typically, but not necessarily, report
        /// <see cref="NativeResultCode.UserCancelled"/>) before returning. From the moment
        /// sxm_begin returns SXM_OK, sxm_release is guaranteed exactly once on every exit path
        /// -- success, blob-copy failure, or the completion task itself faulting -- via the
        /// single try/finally in <see cref="ExecutePostBeginAsync"/>.
        /// </remarks>
        public async Task<NativeResponse> InvokeAsync(string op, object? args, CancellationToken ct)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            EnsureAbiVersion();

            byte[] requestBytes = NativeRequestBuilder.BuildRequestJson(op, args, ExpectedAbiVersion);

            var pending = new PendingOperation();
            GCHandle handle = GCHandle.Alloc(pending, GCHandleType.Normal);

            int rc = BeginOperation(requestBytes, handle, out ulong operationId);

            if (rc != 0)
            {
                // Per sxm_abi.h: a non-zero return from sxm_begin means no callback will ever
                // arrive for this attempt, so nothing will free the handle for us, and there is
                // no valid operation id to release.
                handle.Free();
                return new NativeResponse((NativeResultCode)rc, default, null);
            }

            pending.OperationId = operationId;

            CancellationTokenRegistration registration = ct.Register(static state =>
            {
                var op = (PendingOperation)state!;
                NativeMethods.sxm_cancel(op.OperationId);
            }, pending);

            return await ExecutePostBeginAsync(
                awaitCompletion: () => pending.Completion.Task,
                disposeRegistration: registration.Dispose,
                copyBlobIfPresent: response =>
                    TryGetBlobLength(response.Payload, out int blobLength) && blobLength > 0
                        ? response with { Blob = CopyBlob(operationId) }
                        : response,
                release: () => NativeMethods.sxm_release(operationId))
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Everything that must happen after sxm_begin has returned SXM_OK, expressed against
        /// delegates instead of NativeMethods directly so ShareX.Platform.Mac.Tests can prove
        /// the cleanup ordering (registration disposed, then release called exactly once, even
        /// when <paramref name="awaitCompletion"/> or <paramref name="copyBlobIfPresent"/>
        /// throws) without a dylib.
        /// </summary>
        internal static async Task<NativeResponse> ExecutePostBeginAsync(
            Func<Task<NativeResponse>> awaitCompletion,
            Action disposeRegistration,
            Func<NativeResponse, NativeResponse> copyBlobIfPresent,
            Action release)
        {
            try
            {
                NativeResponse response = await awaitCompletion().ConfigureAwait(false);
                return copyBlobIfPresent(response);
            }
            finally
            {
                // Cancellation must stop being able to call sxm_cancel on this operation id
                // before we tell the native side the id is no longer ours.
                disposeRegistration();
                release();
            }
        }

        // Kept out of InvokeAsync (a non-unsafe async method) on purpose: taking the address of
        // a local with & inside an async method is flagged by the compiler (CS9123) because
        // async locals can be relocated into the state machine. Doing the addressof work here,
        // in an ordinary synchronous method that returns before InvokeAsync ever awaits,
        // sidesteps that entirely.
        private static unsafe int BeginOperation(byte[] requestBytes, GCHandle handle, out ulong operationId)
        {
            fixed (byte* requestPtr = requestBytes)
            {
                ulong outId;
                int rc = NativeMethods.sxm_begin(
                    requestPtr,
                    (ulong)requestBytes.Length,
                    &CompletionTrampoline,
                    (void*)GCHandle.ToIntPtr(handle),
                    &outId);
                operationId = outId;
                return rc;
            }
        }

        private static bool TryGetBlobLength(JsonElement payload, out int length)
        {
            length = 0;
            if (payload.ValueKind == JsonValueKind.Object &&
                payload.TryGetProperty("blobLength", out JsonElement element) &&
                element.ValueKind == JsonValueKind.Number &&
                element.TryGetInt32(out int value))
            {
                length = value;
                return true;
            }

            return false;
        }

        private static unsafe byte[] CopyBlob(ulong operationId)
        {
            long required = NativeMethods.sxm_copy_blob(operationId, null, 0);
            if (required < 0)
            {
                throw new InvalidOperationException(
                    $"sxm_copy_blob failed while querying length for operation {operationId}: " +
                    $"result code {(NativeResultCode)(-required)}.");
            }

            if (required == 0)
            {
                return Array.Empty<byte>();
            }

            byte[] buffer = new byte[required];
            long written;
            fixed (byte* p = buffer)
            {
                written = NativeMethods.sxm_copy_blob(operationId, p, (ulong)buffer.Length);
            }

            if (written < 0)
            {
                throw new InvalidOperationException(
                    $"sxm_copy_blob failed while copying for operation {operationId}: " +
                    $"result code {(NativeResultCode)(-written)}.");
            }

            if (written != buffer.Length)
            {
                Array.Resize(ref buffer, checked((int)written));
            }

            return buffer;
        }

        /// <summary>
        /// Parses a native UTF-8 JSON payload into a standalone <see cref="JsonElement"/>,
        /// falling back to an empty object on null/empty/malformed input. Shared by the
        /// completion and event trampolines so a payload's borrowed lifetime never has to
        /// outlive the callback: callers must copy the bytes first, then call this off the
        /// native thread.
        /// </summary>
        internal static JsonElement ParseJsonOrEmptyObject(byte[]? utf8Json)
        {
            if (utf8Json is { Length: > 0 })
            {
                try
                {
                    using JsonDocument doc = JsonDocument.Parse(utf8Json);
                    return doc.RootElement.Clone();
                }
                catch (JsonException)
                {
                    // A malformed native payload must never crash op completion or event
                    // dispatch; fall through to the empty-object default below.
                }
            }

            using JsonDocument empty = JsonDocument.Parse("{}"u8.ToArray());
            return empty.RootElement.Clone();
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static unsafe void CompletionTrampoline(ulong operationId, int resultCode, byte* utf8ResultJson, ulong resultLength, void* userContext)
        {
            GCHandle handle = default;
            PendingOperation? pending = null;
            try
            {
                handle = GCHandle.FromIntPtr((IntPtr)userContext);
                pending = handle.Target as PendingOperation;
            }
            catch
            {
                // A user_context we can't resolve back to a handle means we have no
                // TaskCompletionSource to complete and nothing safe to do -- an exception must
                // never unwind across this native boundary (no managed object may cross it).
                return;
            }
            finally
            {
                if (handle.IsAllocated)
                {
                    handle.Free();
                }
            }

            if (pending is null)
            {
                return;
            }

            try
            {
                // utf8_result_json is borrowed for the duration of this call only: copy it
                // before doing anything else.
                byte[]? copy = utf8ResultJson != null && resultLength > 0
                    ? new ReadOnlySpan<byte>(utf8ResultJson, checked((int)resultLength)).ToArray()
                    : null;

                JsonElement payload = ParseJsonOrEmptyObject(copy);
                pending.OperationId = operationId;
                pending.Completion.TrySetResult(new NativeResponse((NativeResultCode)resultCode, payload, null));
            }
            catch (Exception ex)
            {
                pending.Completion.TrySetException(ex);
            }
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static unsafe void EventTrampoline(ulong subscriptionId, byte* utf8EventJson, ulong eventLength, void* userContext)
        {
            NativeBridge? bridge;
            try
            {
                GCHandle handle = GCHandle.FromIntPtr((IntPtr)userContext);
                bridge = handle.Target as NativeBridge;
            }
            catch
            {
                // The handle may already be gone if Dispose raced this callback; nothing safe
                // to do, and an exception must never unwind across this native boundary.
                return;
            }

            if (bridge is null)
            {
                return;
            }

            // utf8_event_json is borrowed for the duration of this call only: copy it before
            // returning. Parsing and dispatch happen off this thread so we never block
            // whatever native queue is delivering events.
            byte[]? copy = utf8EventJson != null && eventLength > 0
                ? new ReadOnlySpan<byte>(utf8EventJson, checked((int)eventLength)).ToArray()
                : null;

            _ = Task.Run(() => bridge.DispatchEvent(subscriptionId, copy));
        }

        private void DispatchEvent(ulong subscriptionId, byte[]? utf8Json)
        {
            try
            {
                JsonElement payload = ParseJsonOrEmptyObject(utf8Json);
                var evt = new NativeEvent(subscriptionId, payload);

                if (_subscriptionHandlers.TryGetValue(subscriptionId, out EventHandler<NativeEvent>? handler))
                {
                    handler.Invoke(this, evt);
                }

                _eventReceived?.Invoke(this, evt);
            }
            catch
            {
                // No caller is waiting to observe this: a malformed event or a misbehaving
                // subscriber must not take down a thread-pool worker or the process.
            }
        }

        /// <summary>Detaches the process-wide event sink and prevents new invocations.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            // Ask the native side to stop calling EventTrampoline before freeing the handle it
            // was using as user_context -- sxm_set_event_sink is documented idempotent, and the
            // trampoline itself tolerates a handle that turns out to already be freed.
            unsafe
            {
                NativeMethods.sxm_set_event_sink(null, null);
            }

            lock (_eventGate)
            {
                if (_selfHandle.IsAllocated)
                {
                    _selfHandle.Free();
                }
            }
        }
    }
}
