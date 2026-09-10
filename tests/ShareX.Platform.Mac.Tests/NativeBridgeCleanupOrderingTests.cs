using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using ShareX.Platform.Mac.Interop;
using Xunit;

namespace ShareX.Platform.Mac.Tests
{
    /// <summary>
    /// Proves the "sxm_release exactly once, after registration.Dispose(), on every exit path"
    /// contract of NativeBridge.InvokeAsync without touching the dylib, by exercising the
    /// extracted ExecutePostBeginAsync seam directly with fake delegates. This is the exact
    /// sequence InvokeAsync wires up against pending.Completion.Task / the cancellation
    /// registration / CopyBlob / sxm_release.
    /// </summary>
    public class NativeBridgeCleanupOrderingTests
    {
        [Fact]
        public async Task HappyPath_RunsDisposeThenRelease_AfterBlobCopy_ExactlyOnce()
        {
            var order = new List<string>();
            var response = new NativeResponse(NativeResultCode.Ok, default, null);
            int releaseCount = 0;

            NativeResponse result = await NativeBridge.ExecutePostBeginAsync(
                awaitCompletion: () =>
                {
                    order.Add("await");
                    return Task.FromResult(response);
                },
                disposeRegistration: () => order.Add("dispose"),
                copyBlobIfPresent: r =>
                {
                    order.Add("copyBlob");
                    return r;
                },
                release: () =>
                {
                    order.Add("release");
                    releaseCount++;
                });

            Assert.Equal(response, result);
            Assert.Equal(new[] { "await", "copyBlob", "dispose", "release" }, order);
            Assert.Equal(1, releaseCount);
        }

        [Fact]
        public async Task WhenCompletionFaults_StillDisposesAndReleasesExactlyOnce()
        {
            // This is the exact bug report: pending.Completion.Task can fault (the completion
            // trampoline calls TrySetException when it can't parse the native JSON payload).
            // Before the fix, that exception left InvokeAsync before its try/finally ran, so
            // sxm_release (and the cancellation-registration Dispose) were never called --
            // leaking the native operation and its blob. This proves both still run, in order,
            // exactly once, even though awaitCompletion's task is faulted.
            var order = new List<string>();
            int releaseCount = 0;
            int disposeCount = 0;
            bool copyBlobCalled = false;

            var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                NativeBridge.ExecutePostBeginAsync(
                    awaitCompletion: () =>
                    {
                        order.Add("await");
                        return Task.FromException<NativeResponse>(new InvalidOperationException("malformed payload"));
                    },
                    disposeRegistration: () =>
                    {
                        order.Add("dispose");
                        disposeCount++;
                    },
                    copyBlobIfPresent: r =>
                    {
                        copyBlobCalled = true;
                        return r;
                    },
                    release: () =>
                    {
                        order.Add("release");
                        releaseCount++;
                    }));

            Assert.Equal("malformed payload", thrown.Message);
            Assert.False(copyBlobCalled);
            Assert.Equal(1, disposeCount);
            Assert.Equal(1, releaseCount);
            Assert.Equal(new[] { "await", "dispose", "release" }, order);
        }

        [Fact]
        public async Task WhenBlobCopyThrows_StillDisposesAndReleasesExactlyOnce()
        {
            var order = new List<string>();
            int releaseCount = 0;
            var response = new NativeResponse(NativeResultCode.Ok, default, null);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                NativeBridge.ExecutePostBeginAsync(
                    awaitCompletion: () => Task.FromResult(response),
                    disposeRegistration: () => order.Add("dispose"),
                    copyBlobIfPresent: _ => throw new InvalidOperationException("sxm_copy_blob failed"),
                    release: () =>
                    {
                        order.Add("release");
                        releaseCount++;
                    }));

            Assert.Equal(1, releaseCount);
            Assert.Equal(new[] { "dispose", "release" }, order);
        }
    }
}
