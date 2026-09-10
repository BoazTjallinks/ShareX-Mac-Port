using System.Text.Json;
using ShareX.Core.Errors;
using ShareX.Platform.Mac;
using ShareX.Platform.Mac.Interop;
using Xunit;

namespace ShareX.Platform.Mac.Tests
{
    /// <summary>
    /// When sxm_begin itself returns non-zero, NativeBridge.InvokeAsync returns a
    /// NativeResponse whose Payload is default(JsonElement) (ValueKind.Undefined), not an
    /// empty JSON object -- there was never a native completion to parse. MacCaptureService's
    /// error mapping must not assume an object is present on that path.
    /// </summary>
    public class MacCaptureServiceErrorMappingTests
    {
        [Fact]
        public void ToTaskError_UndefinedPayload_DoesNotThrow_AndFallsBackToCodeMessage()
        {
            var response = new NativeResponse(NativeResultCode.InvalidInput, default, null);
            Assert.Equal(JsonValueKind.Undefined, response.Payload.ValueKind);

            TaskError error = MacCaptureService.ToTaskError(response);

            Assert.Equal(TaskErrorKind.InvalidInput, error.Kind);
            Assert.Contains("InvalidInput", error.Message);
        }

        [Fact]
        public void ToTaskError_ObjectPayloadWithMessage_UsesThatMessage()
        {
            using JsonDocument doc = JsonDocument.Parse("""{"message":"target display is gone"}""");
            var response = new NativeResponse(NativeResultCode.TargetDisappeared, doc.RootElement, null);

            TaskError error = MacCaptureService.ToTaskError(response);

            Assert.Equal(TaskErrorKind.TargetDisappeared, error.Kind);
            Assert.Equal("target display is gone", error.Message);
        }

        [Fact]
        public void ToTaskError_NonObjectPayload_DoesNotThrow_AndFallsBackToCodeMessage()
        {
            using JsonDocument doc = JsonDocument.Parse("42");
            var response = new NativeResponse(NativeResultCode.InternalFailure, doc.RootElement, null);

            TaskError error = MacCaptureService.ToTaskError(response);

            Assert.Equal(TaskErrorKind.InternalFailure, error.Kind);
            Assert.Contains("InternalFailure", error.Message);
        }
    }
}
