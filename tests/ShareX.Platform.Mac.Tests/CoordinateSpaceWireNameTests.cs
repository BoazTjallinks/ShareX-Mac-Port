using System.Text.Json;
using ShareX.Core.Geometry;
using ShareX.Platform.Mac;
using Xunit;

namespace ShareX.Platform.Mac.Tests;

/// <summary>
/// Guards the managed/native coordinate-space wire contract.
///
/// These names are the SXMSpace raw values declared in
/// native/ShareXMacNative/swift/SXMGeometry.swift. The native "capture.screenshot"
/// route compares the incoming string against them and fails the operation with
/// SXM_INVALID_INPUT on any mismatch, so a wrong spelling here is invisible to
/// every other unit test on either side of the boundary and only shows up as a
/// failed capture at runtime. A regression was found exactly this way: the
/// managed side sent the C# enum spelling ("cgGlobalPoints") instead of the ABI's
/// kebab-case value ("cg-global-points").
///
/// If these assertions ever need changing, SXMGeometry.swift must change in the
/// same commit.
/// </summary>
public sealed class CoordinateSpaceWireNameTests
{
    private static string SpaceOf(PointRect rect)
    {
        var target = CaptureTarget.Region(rect);
        byte[] json = Interop.NativeRequestBuilder.BuildRequestJson(
            "capture.screenshot",
            new System.Collections.Generic.Dictionary<string, object?> { ["target"] = ToJsonBridge(target) },
            1);

        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement
            .GetProperty("args")
            .GetProperty("target")
            .GetProperty("space")
            .GetString()!;
    }

    // CaptureTarget.ToJson is internal; InternalsVisibleTo makes it reachable here.
    private static object ToJsonBridge(CaptureTarget target)
    {
        var method = typeof(CaptureTarget).GetMethod(
            "ToJson",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        return method.Invoke(target, null)!;
    }

    [Fact]
    public void CgGlobalPointsRegion_SendsTheAbiRawValue()
    {
        var rect = new PointRect(0, 0, 100, 50, CoordinateSpace.CgGlobalPoints);
        Assert.Equal("cg-global-points", SpaceOf(rect));
    }

    [Fact]
    public void AppKitPointsRegion_SendsTheAbiRawValue()
    {
        var rect = new PointRect(0, 0, 100, 50, CoordinateSpace.AppKitPoints);
        Assert.Equal("appkit-points", SpaceOf(rect));
    }

    [Fact]
    public void SpaceNamesAreKebabCase_NotTheCSharpEnumSpelling()
    {
        // The exact failure that shipped: the enum name is not the wire name.
        Assert.NotEqual(nameof(CoordinateSpace.CgGlobalPoints), NativeSpaceNames.CgGlobalPoints);
        Assert.NotEqual(nameof(CoordinateSpace.AppKitPoints), NativeSpaceNames.AppKitPoints);

        Assert.Equal("cg-global-points", NativeSpaceNames.CgGlobalPoints);
        Assert.Equal("appkit-points", NativeSpaceNames.AppKitPoints);
        Assert.Equal("display-pixels", NativeSpaceNames.DisplayPixels);
        Assert.Equal("display-points", NativeSpaceNames.DisplayPoints);
    }
}
