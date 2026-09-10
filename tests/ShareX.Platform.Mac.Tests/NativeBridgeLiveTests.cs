using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
using ShareX.Platform.Mac.Interop;
using Xunit;

namespace ShareX.Platform.Mac.Tests
{
    /// <summary>
    /// These tests require the compiled ShareXMacNative dylib to be resolvable on the process
    /// loader path (e.g. next to the test host, or via DYLD_LIBRARY_PATH) -- it is not built or
    /// present in this stage-0 skeleton environment, so both facts are skipped rather than
    /// failing the suite. Run manually once the native target is built and evidence-gathered
    /// per CLAUDE.md's "macOS native/GUI/permission claims require Mac evidence from the real
    /// app bundle" rule.
    /// </summary>
    public class NativeBridgeLiveTests
    {
        private const string SkipReason =
            "Requires the built ShareXMacNative dylib on the loader path; not available in this environment.";

        [Fact(Skip = SkipReason)]
        public void Construction_VerifiesAbiVersionAgainstRealLibrary()
        {
            using var bridge = new NativeBridge();
        }

        [Fact(Skip = SkipReason)]
        public async Task InvokeAsync_CapabilitiesGet_ReturnsOkAgainstRealLibrary()
        {
            using var bridge = new NativeBridge();
            NativeResponse response = await bridge.InvokeAsync("capabilities.get", null, CancellationToken.None);
            Assert.Equal(NativeResultCode.Ok, response.Code);
        }

        [Fact(Skip = SkipReason)]
        public async Task EventReceived_SubscribingInstallsSink_AndDeliversRealNativeEvents()
        {
            // Exercises the sxm_set_event_sink path added for the recording pipeline: once the
            // native ShareXMacNative/swift target actually publishes "recording.state" /
            // "recording.progress" events, this should observe at least one NativeEvent within
            // the timeout below without this test asserting anything about their exact schema.
            using var bridge = new NativeBridge();
            var channel = Channel.CreateUnbounded<NativeEvent>();
            bridge.EventReceived += (_, evt) => channel.Writer.TryWrite(evt);

            using var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(5));
            NativeEvent received = await channel.Reader.ReadAsync(cts.Token);

            Assert.True(received.Payload.ValueKind is System.Text.Json.JsonValueKind.Object);
        }
    }
}
