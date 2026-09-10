using System.Collections.Generic;

namespace ShareX.Core.Errors
{
    /// <summary>
    /// The macOS-port error taxonomy. Every native ABI result code (sxm_result_code) and
    /// every user-facing task failure maps into exactly one of these kinds; see
    /// ShareX.Platform.Mac.Interop.NativeResultCode for the mapping from the C ABI.
    /// </summary>
    public enum TaskErrorKind
    {
        UserCancelled,
        PermissionRequired,
        PermissionDenied,
        UnsupportedCapability,
        TargetDisappeared,
        InvalidConfiguration,
        InvalidInput,
        CredentialRequired,
        RemoteRejected,
        RemoteOutcomeUnknown,
        LocalIOFailure,
        EncoderFailure,
        InternalFailure
    }

    public sealed record TaskError(TaskErrorKind Kind, string Message, IReadOnlyDictionary<string, string>? Detail = null);
}
