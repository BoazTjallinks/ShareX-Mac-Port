using System;
using ShareX.Core.Geometry;
using Xunit;

namespace ShareX.Core.Tests
{
    public class PointRectTests
    {
        [Fact]
        public void EnsureSpace_SameSpace_ReturnsSelf()
        {
            var rect = new PointRect(0, 0, 10, 10, CoordinateSpace.CgGlobalPoints);

            PointRect result = rect.EnsureSpace(CoordinateSpace.CgGlobalPoints);

            Assert.Equal(rect, result);
        }

        [Fact]
        public void EnsureSpace_DifferentSpace_Throws()
        {
            var rect = new PointRect(0, 0, 10, 10, CoordinateSpace.CgGlobalPoints);

            Assert.Throws<InvalidOperationException>(() => rect.EnsureSpace(CoordinateSpace.AppKitPoints));
        }

        [Fact]
        public void Intersects_MismatchedSpaces_Throws()
        {
            var a = new PointRect(0, 0, 10, 10, CoordinateSpace.CgGlobalPoints);
            var b = new PointRect(5, 5, 10, 10, CoordinateSpace.DisplayPixels);

            Assert.Throws<InvalidOperationException>(() => a.Intersects(b));
        }

        [Fact]
        public void Intersect_MismatchedSpaces_Throws()
        {
            var a = new PointRect(0, 0, 10, 10, CoordinateSpace.CgGlobalPoints);
            var b = new PointRect(5, 5, 10, 10, CoordinateSpace.AppKitPoints);

            Assert.Throws<InvalidOperationException>(() => a.Intersect(b));
        }

        [Fact]
        public void Union_MismatchedSpaces_Throws()
        {
            var a = new PointRect(0, 0, 10, 10, CoordinateSpace.CgGlobalPoints);
            var b = new PointRect(5, 5, 10, 10, CoordinateSpace.DisplayPoints);

            Assert.Throws<InvalidOperationException>(() => a.Union(b));
        }

        [Fact]
        public void Intersects_OverlappingRects_SameSpace_ReturnsTrue()
        {
            var a = new PointRect(0, 0, 10, 10, CoordinateSpace.CgGlobalPoints);
            var b = new PointRect(5, 5, 10, 10, CoordinateSpace.CgGlobalPoints);

            Assert.True(a.Intersects(b));
        }

        [Fact]
        public void Intersects_NonOverlappingRects_SameSpace_ReturnsFalse()
        {
            var a = new PointRect(0, 0, 10, 10, CoordinateSpace.CgGlobalPoints);
            var b = new PointRect(20, 20, 10, 10, CoordinateSpace.CgGlobalPoints);

            Assert.False(a.Intersects(b));
        }

        [Fact]
        public void Intersect_OverlappingRects_ReturnsOverlapRegion()
        {
            var a = new PointRect(0, 0, 10, 10, CoordinateSpace.CgGlobalPoints);
            var b = new PointRect(5, 5, 10, 10, CoordinateSpace.CgGlobalPoints);

            PointRect result = a.Intersect(b);

            Assert.Equal(5, result.X);
            Assert.Equal(5, result.Y);
            Assert.Equal(5, result.Width);
            Assert.Equal(5, result.Height);
            Assert.Equal(CoordinateSpace.CgGlobalPoints, result.Space);
        }

        [Fact]
        public void Intersect_NonOverlappingRects_ReturnsEmptyRect()
        {
            var a = new PointRect(0, 0, 10, 10, CoordinateSpace.CgGlobalPoints);
            var b = new PointRect(20, 20, 10, 10, CoordinateSpace.CgGlobalPoints);

            PointRect result = a.Intersect(b);

            Assert.True(result.IsEmpty);
        }

        [Fact]
        public void Union_TwoRects_ReturnsBoundingBox()
        {
            var a = new PointRect(0, 0, 10, 10, CoordinateSpace.CgGlobalPoints);
            var b = new PointRect(20, 20, 10, 10, CoordinateSpace.CgGlobalPoints);

            PointRect result = a.Union(b);

            Assert.Equal(0, result.X);
            Assert.Equal(0, result.Y);
            Assert.Equal(30, result.Width);
            Assert.Equal(30, result.Height);
        }

        [Theory]
        [InlineData(0, 10)]
        [InlineData(10, 0)]
        [InlineData(-1, 10)]
        public void IsEmpty_NonPositiveDimensions_ReturnsTrue(double width, double height)
        {
            var rect = new PointRect(0, 0, width, height, CoordinateSpace.CgGlobalPoints);

            Assert.True(rect.IsEmpty);
        }

        [Fact]
        public void IsEmpty_PositiveDimensions_ReturnsFalse()
        {
            var rect = new PointRect(0, 0, 1, 1, CoordinateSpace.CgGlobalPoints);

            Assert.False(rect.IsEmpty);
        }
    }
}
