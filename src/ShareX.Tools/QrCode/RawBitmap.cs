#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

// New portable type: upstream (ShareX/Forms/QRCodeForm.cs) passed System.Drawing.Bitmap between
// TaskHelpers.GenerateQRCode/BarcodeScan and the UI PictureBox. There is no System.Drawing on this port, so a
// minimal pixel-data carrier is used instead everywhere a Bitmap would have appeared.

namespace ShareX.Tools.QrCode
{
    /// <summary>
    /// A minimal, portable raw-pixel bitmap: top-down rows, 4 bytes per pixel in B, G, R, A order
    /// (i.e. <see cref="Bgra8888"/>.Length == Width * Height * 4).
    /// </summary>
    /// <remarks>
    /// Written with an explicit constructor rather than a positional record: C#
    /// records have no primary-constructor body, so the invariant below cannot be
    /// enforced on a positional declaration.
    /// </remarks>
    public sealed record RawBitmap
    {
        public RawBitmap(int width, int height, byte[] bgra8888)
        {
            ArgumentNullException.ThrowIfNull(bgra8888);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

            long expected = (long)width * height * 4;
            if (bgra8888.LongLength != expected)
            {
                throw new ArgumentException(
                    $"Bgra8888 length {bgra8888.LongLength} does not match Width*Height*4 ({expected}).",
                    nameof(bgra8888));
            }

            Width = width;
            Height = height;
            Bgra8888 = bgra8888;
        }

        public int Width { get; }

        public int Height { get; }

        public byte[] Bgra8888 { get; }

        /// <summary>Byte offset of a pixel's blue channel. Rows are top-down.</summary>
        public int OffsetOf(int x, int y) => ((y * Width) + x) * 4;
    }
}
