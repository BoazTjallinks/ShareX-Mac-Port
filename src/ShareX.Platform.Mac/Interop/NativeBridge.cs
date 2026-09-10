using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ShareX.Platform.Mac.Interop
{
    /// <summary>The terminal result of one sxm_begin operation.</summary>
    public sealed record NativeResponse(NativeResultCode Code, JsonElement Payload, byte[]? Blob);

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
    /// Managed side of the ShareX-Mac native bridge (native/ShareXMacNative/include/sxm_abi.h).
    /// One <see cref="NativeBridge"/> owns the process-wide event sink and issues one-shot
    /// request/response operations against sxm_begin/sxm_cancel/sxm_release/sxm_copy_blob.
    /// </summary>
    public sealed class NativeBridge : IDisposable
    {
        private const uint ExpectedAbiVersion = 1;

        // Guards the one-time sxm_abi_version() check across all NativeBridge instances in
        // this process -- the native library itself is process-wide.
        private static int s_abiVerified;

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

        private static void EnsureAbiVersion()
        {
            if (Interlocked.CompareExchange(ref s_abiVerified, 1, 0) != 0)
            {
                return;
            }

            uint reported = NativeMethods.sxm_abi_version();
            if (reported != ExpectedAbiVersion)
            {
                throw new InvalidOperationException(
                    $"ShareXMacNative ABI version mismatch: managed layer targets {ExpectedAbiVersion} " +
                    $"but the native library reports {reported}. Rebuild both sides against the same sxm_abi.h.");
            }
        }

        /// <summary>
        /// Issues one native operation and awaits its terminal completion.
        /// </summary>
        /// <remarks>
        /// Cancelling <paramref name="ct"/> calls sxm_cancel but is NOT the terminal
        /// acknowledgement per the ABI contract: this method still waits for the real
        /// completion callback (which will typically, but not necessarily, report
        /// <see cref="NativeResultCode.UserCancelled"/>) before returning. sxm_release is
        /// called exactly once, after that terminal callback, whichever way it resolves.
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
                // arrive for this attempt, so nothing will free the handle for us.
                handle.Free();
                return new NativeResponse((NativeResultCode)rc, default, null);
            }

            pending.OperationId = operationId;

            using CancellationTokenRegistration registration = ct.Register(static state =>
            {
                var op = (PendingOperation)state!;
                NativeMethods.sxm_cancel(op.OperationId);
            }, pending);

            NativeResponse response = await pending.Completion.Task.ConfigureAwait(false);

            try
            {
                if (TryGetBlobLength(response.Payload, out int blobLength) && blobLength > 0)
                {
                    byte[] blob = CopyBlob(operationId);
                    response = response with { Blob = blob };
                }
            }
            finally
            {
                // Legal only after the terminal event; idempotent, but we still only ever call
                // it once per operation.
                NativeMethods.sxm_release(operationId);
            }

            return response;
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
                JsonElement payload;
                if (utf8ResultJson != null && resultLength > 0)
                {
                    string json = Encoding.UTF8.GetString(utf8ResultJson, checked((int)resultLength));
                    using JsonDocument doc = JsonDocument.Parse(json);
                    payload = doc.RootElement.Clone();
                }
                else
                {
                    using JsonDocument doc = JsonDocument.Parse("{}");
                    payload = doc.RootElement.Clone();
                }

                pending.OperationId = operationId;
                pending.Completion.TrySetResult(new NativeResponse((NativeResultCode)resultCode, payload, null));
            }
            catch (Exception ex)
            {
                pending.Completion.TrySetException(ex);
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
            unsafe
            {
                NativeMethods.sxm_set_event_sink(null, null);
            }
        }
    }
}
