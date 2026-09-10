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

// New portable implementation -- see MetadataEntry.cs for why there is no upstream file to port here.
// Implements the well-specified JPEG marker walk (ITU-T T.81 Annex B) to locate the first APP1 "Exif\0\0"
// segment, then a standard TIFF/Exif IFD walk (CIPA Exif 2.32) over that segment's payload.

using System.Text;

namespace ShareX.Tools.Metadata
{
    internal static class JpegExifReader
    {
        private static readonly byte[] ExifHeader = { (byte)'E', (byte)'x', (byte)'i', (byte)'f', 0, 0 };

        /// <summary>
        /// Scans JPEG markers from the current stream position (which must be at the SOI marker) and returns the
        /// raw TIFF bytes of the first APP1 "Exif\0\0" segment found, or null if there isn't one. Stops at SOS
        /// (start of scan) since Exif APP1 always precedes compressed image data.
        /// </summary>
        public static byte[]? FindExifPayload(Stream stream)
        {
            using BinaryReader reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

            if (ReadUInt16BigEndian(reader) != 0xFFD8)
            {
                return null; // not a JPEG (caller already checked, but stay defensive)
            }

            while (true)
            {
                byte marker = ReadMarker(reader);
                if (marker == 0)
                {
                    return null; // ran out of markers without finding APP1/Exif
                }

                if (marker == 0xD8 || marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7))
                {
                    continue; // standalone markers with no payload (SOI, TEM, RSTn)
                }

                if (marker == 0xD9 || marker == 0xDA)
                {
                    return null; // EOI or start-of-scan: no more APP segments can follow
                }

                ushort segmentLength = ReadUInt16BigEndian(reader);
                if (segmentLength < 2)
                {
                    return null; // malformed length
                }

                int payloadLength = segmentLength - 2;
                byte[] payload = reader.ReadBytes(payloadLength);
                if (payload.Length != payloadLength)
                {
                    return null; // truncated
                }

                if (marker == 0xE1 && payload.Length > ExifHeader.Length && payload.AsSpan(0, ExifHeader.Length).SequenceEqual(ExifHeader))
                {
                    return payload[ExifHeader.Length..];
                }
            }
        }

        /// <summary>Parses a TIFF/Exif structure (the bytes immediately following "Exif\0\0") into metadata entries.</summary>
        public static List<MetadataEntry> ParseTiff(byte[] tiff)
        {
            List<MetadataEntry> entries = new List<MetadataEntry>();

            if (tiff.Length < 8)
            {
                return entries;
            }

            bool littleEndian;
            if (tiff[0] == 'I' && tiff[1] == 'I')
            {
                littleEndian = true;
            }
            else if (tiff[0] == 'M' && tiff[1] == 'M')
            {
                littleEndian = false;
            }
            else
            {
                return entries; // not a valid TIFF byte-order marker
            }

            if (ReadU16(tiff, 2, littleEndian) != 42)
            {
                return entries; // TIFF magic number mismatch
            }

            uint ifd0Offset = ReadU32(tiff, 4, littleEndian);

            uint? exifIfdOffset = null;
            uint? gpsIfdOffset = null;

            ReadIfd(tiff, ifd0Offset, littleEndian, "IFD0", ExifTagNames.Ifd0, entries, ref exifIfdOffset, ref gpsIfdOffset);

            if (exifIfdOffset.HasValue)
            {
                uint? unused1 = null, unused2 = null;
                ReadIfd(tiff, exifIfdOffset.Value, littleEndian, "EXIF", ExifTagNames.ExifIfd, entries, ref unused1, ref unused2);
            }

            if (gpsIfdOffset.HasValue)
            {
                uint? unused1 = null, unused2 = null;
                ReadIfd(tiff, gpsIfdOffset.Value, littleEndian, "GPS", ExifTagNames.GpsIfd, entries, ref unused1, ref unused2);
            }

            return entries;
        }

        private static void ReadIfd(byte[] tiff, uint ifdOffset, bool littleEndian, string group,
            IReadOnlyDictionary<ushort, string> tagNames, List<MetadataEntry> entries, ref uint? exifIfdOffset, ref uint? gpsIfdOffset)
        {
            if (ifdOffset == 0 || ifdOffset + 2 > tiff.Length)
            {
                return;
            }

            ushort entryCount = ReadU16(tiff, (int)ifdOffset, littleEndian);
            int cursor = (int)ifdOffset + 2;

            for (int i = 0; i < entryCount; i++)
            {
                if (cursor + 12 > tiff.Length)
                {
                    break; // truncated IFD
                }

                ushort tag = ReadU16(tiff, cursor, littleEndian);
                ushort type = ReadU16(tiff, cursor + 2, littleEndian);
                uint count = ReadU32(tiff, cursor + 4, littleEndian);
                int valueFieldOffset = cursor + 8;

                cursor += 12;

                if (tag == ExifTagNames.ExifIfdPointerTag)
                {
                    exifIfdOffset = ReadU32(tiff, valueFieldOffset, littleEndian);
                    continue;
                }

                if (tag == ExifTagNames.GpsIfdPointerTag)
                {
                    gpsIfdOffset = ReadU32(tiff, valueFieldOffset, littleEndian);
                    continue;
                }

                if (tag == ExifTagNames.InteropIfdPointerTag)
                {
                    continue; // interoperability IFD: structural pointer, not a displayable value
                }

                string? value = TryFormatValue(tiff, type, count, valueFieldOffset, littleEndian);
                if (value != null)
                {
                    entries.Add(new MetadataEntry(group, ExifTagNames.Resolve(tagNames, tag), value));
                }
            }
        }

        private static readonly int[] TypeSizes = { 0, 1, 1, 2, 4, 8, 1, 1, 2, 4, 8, 4, 8 };

        private static string? TryFormatValue(byte[] tiff, ushort type, uint count, int valueFieldOffset, bool littleEndian)
        {
            if (type == 0 || type >= TypeSizes.Length || count == 0)
            {
                return null;
            }

            long totalSize = (long)TypeSizes[type] * count;
            if (totalSize <= 0 || totalSize > int.MaxValue)
            {
                return null;
            }

            int dataOffset;
            if (totalSize <= 4)
            {
                dataOffset = valueFieldOffset; // inline in the value/offset field
            }
            else
            {
                dataOffset = (int)ReadU32(tiff, valueFieldOffset, littleEndian);
            }

            if (dataOffset < 0 || dataOffset + totalSize > tiff.Length)
            {
                return null; // offset points outside the buffer -- malformed/truncated
            }

            switch (type)
            {
                case 2: // ASCII
                    {
                        int length = (int)count;
                        int end = dataOffset + length;
                        while (end > dataOffset && tiff[end - 1] == 0)
                        {
                            end--; // trim trailing NUL terminator(s)
                        }
                        return Encoding.ASCII.GetString(tiff, dataOffset, end - dataOffset).Trim();
                    }

                case 1: // BYTE
                case 7: // UNDEFINED
                    {
                        // Prefer a printable ASCII rendering (e.g. ExifVersion "0230"); fall back to hex.
                        byte[] raw = tiff.AsSpan(dataOffset, (int)count).ToArray();
                        if (Array.TrueForAll(raw, b => b == 0 || (b >= 0x20 && b < 0x7F)))
                        {
                            return Encoding.ASCII.GetString(raw).TrimEnd('\0');
                        }
                        return Convert.ToHexStringLower(raw);
                    }

                case 6: // SBYTE
                    return FormatIntegerArray(count, i => ((sbyte)tiff[dataOffset + i]).ToString());

                case 3: // SHORT
                    return FormatIntegerArray(count, i => ReadU16(tiff, dataOffset + i * 2, littleEndian).ToString());

                case 8: // SSHORT
                    return FormatIntegerArray(count, i => ((short)ReadU16(tiff, dataOffset + i * 2, littleEndian)).ToString());

                case 4: // LONG
                    return FormatIntegerArray(count, i => ReadU32(tiff, dataOffset + i * 4, littleEndian).ToString());

                case 9: // SLONG
                    return FormatIntegerArray(count, i => ((int)ReadU32(tiff, dataOffset + i * 4, littleEndian)).ToString());

                case 5: // RATIONAL
                    return FormatIntegerArray(count, i => FormatRational(
                        ReadU32(tiff, dataOffset + i * 8, littleEndian),
                        ReadU32(tiff, dataOffset + i * 8 + 4, littleEndian)));

                case 10: // SRATIONAL
                    return FormatIntegerArray(count, i => FormatRational(
                        (int)ReadU32(tiff, dataOffset + i * 8, littleEndian),
                        (int)ReadU32(tiff, dataOffset + i * 8 + 4, littleEndian)));

                case 11: // FLOAT
                    return FormatIntegerArray(count, i => BitConverter.ToSingle(tiff, dataOffset + i * 4).ToString(System.Globalization.CultureInfo.InvariantCulture));

                case 12: // DOUBLE
                    return FormatIntegerArray(count, i => BitConverter.ToDouble(tiff, dataOffset + i * 8).ToString(System.Globalization.CultureInfo.InvariantCulture));

                default:
                    return null;
            }
        }

        private static string FormatIntegerArray(uint count, Func<int, string> formatOne)
        {
            if (count == 1)
            {
                return formatOne(0);
            }

            string[] parts = new string[count];
            for (int i = 0; i < count; i++)
            {
                parts[i] = formatOne(i);
            }
            return string.Join(", ", parts);
        }

        private static string FormatRational(long numerator, long denominator)
        {
            if (denominator == 0)
            {
                return numerator == 0 ? "0" : $"{numerator}/0";
            }

            if (numerator % denominator == 0)
            {
                return (numerator / denominator).ToString();
            }

            return $"{numerator}/{denominator}";
        }

        private static byte ReadMarker(BinaryReader reader)
        {
            // Markers are 0xFF followed by a non-0x00/0xFF marker code; skip fill bytes (0xFF) per spec.
            int b;
            do
            {
                b = reader.BaseStream.ReadByte();
                if (b == -1) return 0;
            } while (b != 0xFF);

            int marker;
            do
            {
                marker = reader.BaseStream.ReadByte();
                if (marker == -1) return 0;
            } while (marker == 0xFF);

            return (byte)marker;
        }

        private static ushort ReadUInt16BigEndian(BinaryReader reader)
        {
            int hi = reader.BaseStream.ReadByte();
            int lo = reader.BaseStream.ReadByte();
            if (hi == -1 || lo == -1) return 0;
            return (ushort)((hi << 8) | lo);
        }

        private static ushort ReadU16(byte[] data, int offset, bool littleEndian) =>
            littleEndian
                ? (ushort)(data[offset] | (data[offset + 1] << 8))
                : (ushort)((data[offset] << 8) | data[offset + 1]);

        private static uint ReadU32(byte[] data, int offset, bool littleEndian) =>
            littleEndian
                ? (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24))
                : (uint)((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]);
    }
}
