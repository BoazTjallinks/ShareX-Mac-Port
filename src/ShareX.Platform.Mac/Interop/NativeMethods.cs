using System.Runtime.InteropServices;

namespace ShareX.Platform.Mac.Interop
{
    /// <summary>
    /// Source-generated P/Invoke bindings for the ShareX-Mac native bridge described by
    /// native/ShareXMacNative/include/sxm_abi.h. This is the single place in the managed
    /// codebase allowed to declare these entry points; everything else goes through
    /// <see cref="NativeBridge"/>.
    /// </summary>
    internal static partial class NativeMethods
    {
        private const string LibraryName = "ShareXMacNative";

        /// <summary>uint32_t sxm_abi_version(void);</summary>
        [LibraryImport(LibraryName)]
        internal static partial uint sxm_abi_version();

        /// <summary>
        /// int32_t sxm_set_event_sink(sxm_event_fn sink, void *user_context);
        /// Pass a null function pointer to detach. Idempotent.
        /// </summary>
        [LibraryImport(LibraryName)]
        internal static unsafe partial int sxm_set_event_sink(
            delegate* unmanaged[Cdecl]<ulong, byte*, ulong, void*, void> sink,
            void* userContext);

        /// <summary>
        /// int32_t sxm_begin(const uint8_t *utf8_request_json, uint64_t request_length,
        ///                    sxm_completion_fn completion, void *user_context,
        ///                    uint64_t *out_operation_id);
        /// Copies the request bytes before returning. Either fails immediately (no callback
        /// will follow) or returns SXM_OK with an operation id that completes exactly once.
        /// </summary>
        [LibraryImport(LibraryName)]
        internal static unsafe partial int sxm_begin(
            byte* utf8RequestJson,
            ulong requestLength,
            delegate* unmanaged[Cdecl]<ulong, int, byte*, ulong, void*, void> completion,
            void* userContext,
            ulong* outOperationId);

        /// <summary>int32_t sxm_cancel(uint64_t operation_id); Idempotent, not terminal.</summary>
        [LibraryImport(LibraryName)]
        internal static partial int sxm_cancel(ulong operationId);

        /// <summary>int32_t sxm_release(uint64_t operation_id); Legal only after the terminal event, idempotent.</summary>
        [LibraryImport(LibraryName)]
        internal static partial int sxm_release(ulong operationId);

        /// <summary>
        /// int64_t sxm_copy_blob(uint64_t operation_id, uint8_t *out_buffer, uint64_t buffer_capacity);
        /// Call with a null buffer to query the required length first (two-call pattern).
        /// </summary>
        [LibraryImport(LibraryName)]
        internal static unsafe partial long sxm_copy_blob(ulong operationId, byte* outBuffer, ulong bufferCapacity);
    }
}
