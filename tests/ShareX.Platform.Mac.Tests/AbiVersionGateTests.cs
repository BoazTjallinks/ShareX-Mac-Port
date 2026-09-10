using ShareX.Platform.Mac.Interop;
using Xunit;

namespace ShareX.Platform.Mac.Tests
{
    /// <summary>
    /// Proves the latch-only-on-success rule without a dylib: a mismatched version must never
    /// set the "verified" flag, so every subsequent call keeps re-checking (and keeps
    /// throwing, from NativeBridge.EnsureAbiVersion's point of view) instead of silently
    /// tolerating the mismatch forever.
    /// </summary>
    public class AbiVersionGateTests
    {
        [Fact]
        public void Mismatch_DoesNotLatch_AndKeepsFailingOnRepeatedCalls()
        {
            bool verified = false;

            bool first = AbiVersionGate.CheckAndLatch(reportedVersion: 2, expectedVersion: 1, ref verified);
            Assert.False(first);
            Assert.False(verified);

            bool second = AbiVersionGate.CheckAndLatch(reportedVersion: 2, expectedVersion: 1, ref verified);
            Assert.False(second);
            Assert.False(verified);
        }

        [Fact]
        public void Match_Latches_AndStaysLatchedEvenIfCalledAgain()
        {
            bool verified = false;

            bool first = AbiVersionGate.CheckAndLatch(reportedVersion: 1, expectedVersion: 1, ref verified);
            Assert.True(first);
            Assert.True(verified);

            // Once latched, a later call is trusted even with a (hypothetical) different
            // reported value -- matching NativeBridge's "only check once, up front" contract.
            bool second = AbiVersionGate.CheckAndLatch(reportedVersion: 99, expectedVersion: 1, ref verified);
            Assert.True(second);
            Assert.True(verified);
        }

        [Fact]
        public void MismatchThenMatch_LatchesOnTheSuccessfulCall()
        {
            bool verified = false;

            Assert.False(AbiVersionGate.CheckAndLatch(2, 1, ref verified));
            Assert.False(verified);

            Assert.True(AbiVersionGate.CheckAndLatch(1, 1, ref verified));
            Assert.True(verified);
        }
    }
}
