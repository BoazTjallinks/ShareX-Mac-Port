using System.Threading;
using System.Threading.Tasks;
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
    }
}
