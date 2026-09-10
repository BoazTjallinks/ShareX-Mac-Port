using System;
using System.Collections.Generic;
using ShareX.Core.Geometry;
using ShareX.Platform.Mac;
using Xunit;

namespace ShareX.Platform.Mac.Tests
{
    /// <summary>
    /// Verifies CaptureTarget.ToJson() (internal, visible here via InternalsVisibleTo) matches
    /// the `target` shapes the "capture.screenshot" route in SXMRoutes.swift switches on. No
    /// native call is made.
    /// </summary>
    public class CaptureTargetTests
    {
        [Fact]
        public void AllDisplays_Shape()
        {
            var json = CaptureTarget.AllDisplays().ToJson();
            Assert.Equal("allDisplays", json["kind"]);
        }

        [Fact]
        public void ActiveDisplay_Shape()
        {
            var json = CaptureTarget.ActiveDisplay().ToJson();
            Assert.Equal("activeDisplay", json["kind"]);
        }

        [Fact]
        public void Display_Shape_IncludesDisplayId()
        {
            var json = CaptureTarget.Display(42).ToJson();
            Assert.Equal("display", json["kind"]);
            Assert.Equal(42u, json["displayId"]);
        }

        [Fact]
        public void ActiveWindow_Shape()
        {
            var json = CaptureTarget.ActiveWindow().ToJson();
            Assert.Equal("activeWindow", json["kind"]);
        }

        [Fact]
        public void Window_Shape_IncludesWindowId()
        {
            var json = CaptureTarget.Window(7).ToJson();
            Assert.Equal("window", json["kind"]);
            Assert.Equal(7u, json["windowId"]);
        }

        [Fact]
        public void Region_CgGlobalPoints_Shape()
        {
            var rect = new PointRect(1, 2, 3, 4, CoordinateSpace.CgGlobalPoints);
            var json = CaptureTarget.Region(rect).ToJson();

            Assert.Equal("region", json["kind"]);
            // The ABI's SXMSpace raw value, not the C# enum spelling. See
            // CoordinateSpaceWireNameTests: this originally asserted
            // "cgGlobalPoints", which the native route rejects.
            Assert.Equal("cg-global-points", json["space"]);
            var rectJson = Assert.IsType<Dictionary<string, object?>>(json["rect"]);
            Assert.Equal(1d, rectJson["x"]);
            Assert.Equal(2d, rectJson["y"]);
            Assert.Equal(3d, rectJson["width"]);
            Assert.Equal(4d, rectJson["height"]);
        }

        [Fact]
        public void Region_AppKitPoints_Shape()
        {
            var rect = new PointRect(0, 0, 1, 1, CoordinateSpace.AppKitPoints);
            var json = CaptureTarget.Region(rect).ToJson();

            Assert.Equal("appkit-points", json["space"]);
        }

        [Theory]
        [InlineData(CoordinateSpace.DisplayPixels)]
        [InlineData(CoordinateSpace.DisplayPoints)]
        public void Region_UnsupportedSpace_Throws(CoordinateSpace space)
        {
            var rect = new PointRect(0, 0, 1, 1, space);
            Assert.Throws<ArgumentException>(() => CaptureTarget.Region(rect));
        }
    }
}
