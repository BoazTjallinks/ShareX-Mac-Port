using System;

namespace ShareX.Core.Geometry
{
    /// <summary>
    /// The distinct coordinate systems that appear across the native macOS capture ABI and the
    /// managed UI layer. Deliberately not implicitly convertible into one another: a rect from
    /// AppKit (origin bottom-left, "points") is not numerically the same as a rect in CoreGraphics
    /// global space (origin top-left, "points") or a rect in raw display pixels. Call sites must
    /// convert explicitly and are expected to track which space a value is in.
    /// </summary>
    public enum CoordinateSpace
    {
        /// <summary>CoreGraphics global display space: origin top-left, points (not pixels).</summary>
        CgGlobalPoints,

        /// <summary>AppKit screen space: origin bottom-left, points (not pixels).</summary>
        AppKitPoints,

        /// <summary>Backing-store pixels of a specific display/window capture, no DPI scaling applied.</summary>
        DisplayPixels,

        /// <summary>Points local to a specific display, independent of global desktop layout.</summary>
        DisplayPoints
    }

    public readonly record struct PixelSize(int Width, int Height);

    /// <summary>
    /// A rectangle tagged with the coordinate space it was measured in. This project forbids
    /// implicitly mixing coordinate spaces: every geometric operation below verifies both
    /// operands share a space via <see cref="EnsureSpace"/> and throws
    /// <see cref="InvalidOperationException"/> otherwise, rather than silently producing a
    /// numerically-wrong rectangle.
    /// </summary>
    public readonly record struct PointRect(double X, double Y, double Width, double Height, CoordinateSpace Space)
    {
        public double Left => X;
        public double Top => Y;
        public double Right => X + Width;
        public double Bottom => Y + Height;

        public bool IsEmpty => Width <= 0 || Height <= 0;

        /// <summary>
        /// Returns this rect unchanged if its <see cref="Space"/> matches <paramref name="expected"/>;
        /// otherwise throws. Use this at the boundary of any function that assumes a particular space.
        /// </summary>
        public PointRect EnsureSpace(CoordinateSpace expected)
        {
            if (Space != expected)
            {
                throw new InvalidOperationException(
                    $"Coordinate space mismatch: expected '{expected}' but rect is in '{Space}'. " +
                    "Mixing coordinate spaces implicitly is not allowed; convert explicitly first.");
            }

            return this;
        }

        public bool Intersects(PointRect other)
        {
            EnsureSpace(other.Space);

            if (IsEmpty || other.IsEmpty)
            {
                return false;
            }

            return Left < other.Right && Right > other.Left && Top < other.Bottom && Bottom > other.Top;
        }

        public PointRect Intersect(PointRect other)
        {
            EnsureSpace(other.Space);

            double left = Math.Max(Left, other.Left);
            double top = Math.Max(Top, other.Top);
            double right = Math.Min(Right, other.Right);
            double bottom = Math.Min(Bottom, other.Bottom);

            if (right <= left || bottom <= top)
            {
                return new PointRect(0, 0, 0, 0, Space);
            }

            return new PointRect(left, top, right - left, bottom - top, Space);
        }

        public PointRect Union(PointRect other)
        {
            EnsureSpace(other.Space);

            if (IsEmpty)
            {
                return other;
            }

            if (other.IsEmpty)
            {
                return this;
            }

            double left = Math.Min(Left, other.Left);
            double top = Math.Min(Top, other.Top);
            double right = Math.Max(Right, other.Right);
            double bottom = Math.Max(Bottom, other.Bottom);

            return new PointRect(left, top, right - left, bottom - top, Space);
        }
    }
}
