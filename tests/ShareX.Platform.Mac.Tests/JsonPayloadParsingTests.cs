using System.Text;
using System.Text.Json;
using ShareX.Platform.Mac.Interop;
using Xunit;

namespace ShareX.Platform.Mac.Tests
{
    /// <summary>
    /// NativeBridge.ParseJsonOrEmptyObject is the shared, off-native-thread parser used by both
    /// the completion trampoline and the event trampoline. It must never throw regardless of
    /// what a native payload contains -- these are exactly the "defensive, don't assume a
    /// schema you can't verify" cases called out for the recording event payloads.
    /// </summary>
    public class JsonPayloadParsingTests
    {
        [Fact]
        public void Null_ReturnsEmptyObject()
        {
            JsonElement result = NativeBridge.ParseJsonOrEmptyObject(null);

            Assert.Equal(JsonValueKind.Object, result.ValueKind);
            Assert.Empty(result.EnumerateObject());
        }

        [Fact]
        public void Empty_ReturnsEmptyObject()
        {
            JsonElement result = NativeBridge.ParseJsonOrEmptyObject(System.Array.Empty<byte>());

            Assert.Equal(JsonValueKind.Object, result.ValueKind);
        }

        [Fact]
        public void Malformed_FallsBackToEmptyObject_WithoutThrowing()
        {
            byte[] garbage = Encoding.UTF8.GetBytes("{not valid json");

            JsonElement result = NativeBridge.ParseJsonOrEmptyObject(garbage);

            Assert.Equal(JsonValueKind.Object, result.ValueKind);
        }

        [Fact]
        public void ValidRecordingStateEvent_ParsesFieldsDefensively()
        {
            // Shape sketched by the coordinator for the in-progress recording pipeline:
            // {"kind":"recording.state","sessionId":N,...}. Not hard-coded as a contract here
            // -- just used to prove ordinary well-formed JSON round-trips through the parser.
            byte[] json = Encoding.UTF8.GetBytes("""{"kind":"recording.state","sessionId":7}""");

            JsonElement result = NativeBridge.ParseJsonOrEmptyObject(json);

            Assert.Equal(JsonValueKind.Object, result.ValueKind);
            Assert.Equal("recording.state", result.GetProperty("kind").GetString());
            Assert.Equal(7, result.GetProperty("sessionId").GetInt32());
        }

        [Fact]
        public void ValidButNonObjectJson_IsReturnedAsIs()
        {
            // Not a shape any current route produces, but the parser itself should not force
            // a schema -- valid JSON that happens not to be an object still parses.
            byte[] json = Encoding.UTF8.GetBytes("42");

            JsonElement result = NativeBridge.ParseJsonOrEmptyObject(json);

            Assert.Equal(JsonValueKind.Number, result.ValueKind);
            Assert.Equal(42, result.GetInt32());
        }
    }
}
