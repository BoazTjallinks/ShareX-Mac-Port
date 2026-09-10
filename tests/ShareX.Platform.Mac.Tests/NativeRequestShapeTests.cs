using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ShareX.Platform.Mac.Interop;
using Xunit;

namespace ShareX.Platform.Mac.Tests
{
    /// <summary>
    /// Verifies the "{op, abi, args}" request envelope shape without touching the native
    /// dylib -- NativeRequestBuilder is pure JSON shaping, made visible to this assembly via
    /// [assembly: InternalsVisibleTo] on ShareX.Platform.Mac.
    /// </summary>
    public class NativeRequestShapeTests
    {
        [Fact]
        public void BuildRequestJson_WithArgs_ProducesExpectedEnvelope()
        {
            var args = new Dictionary<string, object?> { ["permission"] = "screenRecording" };

            byte[] bytes = NativeRequestBuilder.BuildRequestJson("permissions.request", args, abiVersion: 1);

            using JsonDocument doc = JsonDocument.Parse(bytes);
            JsonElement root = doc.RootElement;

            Assert.Equal("permissions.request", root.GetProperty("op").GetString());
            Assert.Equal(1, root.GetProperty("abi").GetInt32());
            Assert.Equal("screenRecording", root.GetProperty("args").GetProperty("permission").GetString());
        }

        [Fact]
        public void BuildRequestJson_WithNullArgs_ProducesEmptyArgsObject()
        {
            byte[] bytes = NativeRequestBuilder.BuildRequestJson("capabilities.get", null, abiVersion: 1);

            using JsonDocument doc = JsonDocument.Parse(bytes);
            JsonElement root = doc.RootElement;

            Assert.Equal("capabilities.get", root.GetProperty("op").GetString());
            Assert.Equal(JsonValueKind.Object, root.GetProperty("args").ValueKind);
            Assert.Empty(root.GetProperty("args").EnumerateObject().ToArray());
        }

        [Fact]
        public void BuildRequestJson_EmptyOp_Throws()
        {
            Assert.Throws<System.ArgumentException>(() => NativeRequestBuilder.BuildRequestJson("", null, 1));
        }
    }
}
