using System;
using ShareX.Core.Errors;

namespace ShareX.Platform.Mac.Interop
{
    /// <summary>
    /// Mirrors the native `sxm_result_code` enum in sxm_abi.h exactly: same members (in
    /// PascalCase), same underlying values.
    /// </summary>
    public enum NativeResultCode
    {
        Ok = 0,
        UserCancelled = 1,
        PermissionRequired = 2,
        PermissionDenied = 3,
        UnsupportedCapability = 4,
        TargetDisappeared = 5,
        InvalidConfiguration = 6,
        InvalidInput = 7,
        CredentialRequired = 8,
        RemoteRejected = 9,
        RemoteOutcomeUnknown = 10,
        LocalIOFailure = 11,
        EncoderFailure = 12,
        InternalFailure = 13,
        UnknownOperation = 14,
        AbiMismatch = 15
    }

    public static class NativeResultCodeExtensions
    {
        /// <summary>
        /// Maps a native result code onto the portable <see cref="TaskErrorKind"/> taxonomy.
        /// <see cref="NativeResultCode.UnknownOperation"/> and <see cref="NativeResultCode.AbiMismatch"/>
        /// both collapse to <see cref="TaskErrorKind.InternalFailure"/> since neither represents a
        /// condition a caller of the public API can act on distinctly.
        /// </summary>
        public static TaskErrorKind ToTaskErrorKind(this NativeResultCode code) => code switch
        {
            NativeResultCode.UserCancelled => TaskErrorKind.UserCancelled,
            NativeResultCode.PermissionRequired => TaskErrorKind.PermissionRequired,
            NativeResultCode.PermissionDenied => TaskErrorKind.PermissionDenied,
            NativeResultCode.UnsupportedCapability => TaskErrorKind.UnsupportedCapability,
            NativeResultCode.TargetDisappeared => TaskErrorKind.TargetDisappeared,
            NativeResultCode.InvalidConfiguration => TaskErrorKind.InvalidConfiguration,
            NativeResultCode.InvalidInput => TaskErrorKind.InvalidInput,
            NativeResultCode.CredentialRequired => TaskErrorKind.CredentialRequired,
            NativeResultCode.RemoteRejected => TaskErrorKind.RemoteRejected,
            NativeResultCode.RemoteOutcomeUnknown => TaskErrorKind.RemoteOutcomeUnknown,
            NativeResultCode.LocalIOFailure => TaskErrorKind.LocalIOFailure,
            NativeResultCode.EncoderFailure => TaskErrorKind.EncoderFailure,
            NativeResultCode.InternalFailure => TaskErrorKind.InternalFailure,
            NativeResultCode.UnknownOperation => TaskErrorKind.InternalFailure,
            NativeResultCode.AbiMismatch => TaskErrorKind.InternalFailure,
            NativeResultCode.Ok => throw new ArgumentException("SXM_OK is not an error result code.", nameof(code)),
            _ => TaskErrorKind.InternalFailure
        };
    }
}
