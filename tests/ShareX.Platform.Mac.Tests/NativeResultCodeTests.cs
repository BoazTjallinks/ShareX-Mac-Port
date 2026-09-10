using System;
using ShareX.Core.Errors;
using ShareX.Platform.Mac.Interop;
using Xunit;

namespace ShareX.Platform.Mac.Tests
{
    public class NativeResultCodeTests
    {
        [Fact]
        public void Values_MatchSxmResultCodeFromAbiHeader()
        {
            Assert.Equal(0, (int)NativeResultCode.Ok);
            Assert.Equal(1, (int)NativeResultCode.UserCancelled);
            Assert.Equal(2, (int)NativeResultCode.PermissionRequired);
            Assert.Equal(3, (int)NativeResultCode.PermissionDenied);
            Assert.Equal(4, (int)NativeResultCode.UnsupportedCapability);
            Assert.Equal(5, (int)NativeResultCode.TargetDisappeared);
            Assert.Equal(6, (int)NativeResultCode.InvalidConfiguration);
            Assert.Equal(7, (int)NativeResultCode.InvalidInput);
            Assert.Equal(8, (int)NativeResultCode.CredentialRequired);
            Assert.Equal(9, (int)NativeResultCode.RemoteRejected);
            Assert.Equal(10, (int)NativeResultCode.RemoteOutcomeUnknown);
            Assert.Equal(11, (int)NativeResultCode.LocalIOFailure);
            Assert.Equal(12, (int)NativeResultCode.EncoderFailure);
            Assert.Equal(13, (int)NativeResultCode.InternalFailure);
            Assert.Equal(14, (int)NativeResultCode.UnknownOperation);
            Assert.Equal(15, (int)NativeResultCode.AbiMismatch);
        }

        [Theory]
        [InlineData(NativeResultCode.UserCancelled, TaskErrorKind.UserCancelled)]
        [InlineData(NativeResultCode.PermissionRequired, TaskErrorKind.PermissionRequired)]
        [InlineData(NativeResultCode.PermissionDenied, TaskErrorKind.PermissionDenied)]
        [InlineData(NativeResultCode.UnsupportedCapability, TaskErrorKind.UnsupportedCapability)]
        [InlineData(NativeResultCode.TargetDisappeared, TaskErrorKind.TargetDisappeared)]
        [InlineData(NativeResultCode.InvalidConfiguration, TaskErrorKind.InvalidConfiguration)]
        [InlineData(NativeResultCode.InvalidInput, TaskErrorKind.InvalidInput)]
        [InlineData(NativeResultCode.CredentialRequired, TaskErrorKind.CredentialRequired)]
        [InlineData(NativeResultCode.RemoteRejected, TaskErrorKind.RemoteRejected)]
        [InlineData(NativeResultCode.RemoteOutcomeUnknown, TaskErrorKind.RemoteOutcomeUnknown)]
        [InlineData(NativeResultCode.LocalIOFailure, TaskErrorKind.LocalIOFailure)]
        [InlineData(NativeResultCode.EncoderFailure, TaskErrorKind.EncoderFailure)]
        [InlineData(NativeResultCode.InternalFailure, TaskErrorKind.InternalFailure)]
        public void ToTaskErrorKind_MapsDirectly(NativeResultCode code, TaskErrorKind expected)
        {
            Assert.Equal(expected, code.ToTaskErrorKind());
        }

        [Theory]
        [InlineData(NativeResultCode.UnknownOperation)]
        [InlineData(NativeResultCode.AbiMismatch)]
        public void ToTaskErrorKind_UnknownOperationAndAbiMismatch_MapToInternalFailure(NativeResultCode code)
        {
            Assert.Equal(TaskErrorKind.InternalFailure, code.ToTaskErrorKind());
        }

        [Fact]
        public void ToTaskErrorKind_Ok_Throws()
        {
            Assert.Throws<ArgumentException>(() => NativeResultCode.Ok.ToTaskErrorKind());
        }
    }
}
