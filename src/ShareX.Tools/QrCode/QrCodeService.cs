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

// Ported from ShareX v21.0.0 (commit d2502561f63fc3ff502cacd91514e3f7f2948c74), file ShareX/TaskHelpers.cs
// (GenerateQRCode, BarcodeScan). Upstream builds a ZXing.Net `BarcodeWriter`/`BarcodeReader` from the
// ZXing.Windows.Compatibility-style bindings, which render straight to/from a System.Drawing Bitmap. Those
// bindings are not available portably (they require System.Drawing), so this port uses ZXing.Net's own
// image-library-agnostic core classes instead:
//   - Encoding: BarcodeWriterGeneric produces a ZXing.Common.BitMatrix (module on/off grid); this class then
//     rasterizes that matrix into a RawBitmap itself -- this is the "small portable writer adapter" the task
//     calls for, replacing BitmapRenderer.
//   - Decoding: BarcodeReaderGeneric.DecodeMultiple(byte[], width, height, BitmapFormat) reads BGRA8888 bytes
//     directly (ZXing.Net's RGBLuminanceSource supports BGRA32 natively) -- no adapter class is even needed here,
//     which is why there is no separate LuminanceSource subclass in this file.
// Encoding option defaults (Width/Height = requested size, CharacterSet = "UTF-8", PureBarcode = true,
// NoPadding = false, Margin = 1) are copied verbatim from TaskHelpers.GenerateQRCode. Upstream never set
// EncodeHintType.ERROR_CORRECTION, which leaves ZXing's QR encoder at its own built-in default of
// ErrorCorrectionLevel.L; that default is preserved explicitly here since this port exposes it as a parameter.
// Decode options (AutoRotate = true, TryHarder = true, TryInverted = true, scanning every barcode format unless
// QR-only is requested) are copied verbatim from TaskHelpers.BarcodeScan.

using ZXing;
using ZXing.Common;
using ZXing.QrCode;
using ZXing.QrCode.Internal;

namespace ShareX.Tools.QrCode
{
    /// <summary>
    /// One decoded barcode: its payload text and the barcode format ZXing recognized (e.g. "QR_CODE").
    /// </summary>
    public sealed record BarcodeResult(string Text, string Format);

    /// <summary>
    /// Portable QR/barcode encode and decode service backed by ZXing.Net's core (image-library-agnostic) API.
    /// </summary>
    public static class QrCodeService
    {
        /// <summary>Upstream QRCodeForm.GenerateQRCode enforces a 64px floor: size = Math.Max(size, 64).</summary>
        public const int MinimumSize = 64;

        /// <summary>Upstream TaskHelpers.GenerateQRCode fixed margin.</summary>
        public const int DefaultMargin = 1;

        /// <summary>ZXing's own built-in default when EncodeHintType.ERROR_CORRECTION is left unset (upstream never sets it).</summary>
        public static ErrorCorrectionLevel DefaultErrorCorrectionLevel => ErrorCorrectionLevel.L;

        /// <summary>
        /// Encodes <paramref name="text"/> as a QR code, returning raw BGRA8888 pixel data.
        /// Mirrors upstream TaskHelpers.GenerateQRCode.
        /// </summary>
        public static RawBitmap Encode(string text, int width, int height, int margin = DefaultMargin,
            ErrorCorrectionLevel? errorCorrectionLevel = null, string characterSet = "UTF-8")
        {
            ArgumentNullException.ThrowIfNull(text);
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));

            BarcodeWriterGeneric writer = new BarcodeWriterGeneric
            {
                Format = BarcodeFormat.QR_CODE,
                Options = new QrCodeEncodingOptions
                {
                    Width = width,
                    Height = height,
                    CharacterSet = characterSet,
                    PureBarcode = true,
                    NoPadding = false,
                    Margin = margin,
                    ErrorCorrection = errorCorrectionLevel ?? DefaultErrorCorrectionLevel
                }
            };

            BitMatrix matrix = writer.Encode(text);
            return RasterizeToBgra(matrix);
        }

        /// <summary>
        /// Decodes every barcode found in <paramref name="rawBitmap"/>. Mirrors upstream TaskHelpers.BarcodeScan.
        /// </summary>
        public static IReadOnlyList<BarcodeResult> Decode(RawBitmap rawBitmap, bool qrCodeOnly = false)
        {
            ArgumentNullException.ThrowIfNull(rawBitmap);

            BarcodeReaderGeneric reader = new BarcodeReaderGeneric
            {
                AutoRotate = true,
                Options = new DecodingOptions
                {
                    TryHarder = true,
                    TryInverted = true
                }
            };

            if (qrCodeOnly)
            {
                reader.Options.PossibleFormats = new List<BarcodeFormat> { BarcodeFormat.QR_CODE };
            }

            Result[] results = reader.DecodeMultiple(rawBitmap.Bgra8888, rawBitmap.Width, rawBitmap.Height,
                RGBLuminanceSource.BitmapFormat.BGRA32) ?? Array.Empty<Result>();

            List<BarcodeResult> output = new List<BarcodeResult>();

            foreach (Result? result in results)
            {
                if (result != null && !string.IsNullOrEmpty(result.Text))
                {
                    output.Add(new BarcodeResult(result.Text, result.BarcodeFormat.ToString()));
                }
            }

            return output;
        }

        private static RawBitmap RasterizeToBgra(BitMatrix matrix)
        {
            int width = matrix.Width;
            int height = matrix.Height;
            byte[] pixels = new byte[width * height * 4];

            int offset = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    byte value = matrix[x, y] ? (byte)0 : (byte)255; // black module vs. white background
                    pixels[offset] = value;     // B
                    pixels[offset + 1] = value; // G
                    pixels[offset + 2] = value; // R
                    pixels[offset + 3] = 255;   // A
                    offset += 4;
                }
            }

            return new RawBitmap(width, height, pixels);
        }
    }
}
