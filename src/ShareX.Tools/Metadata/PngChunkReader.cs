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
// Implements the PNG chunk structure and the tEXt/zTXt/iTXt textual-data chunks per the PNG 1.2 specification
// (W3C REC-PNG). zTXt/iTXt decompression uses System.IO.Compression.ZLibStream (zlib-wrapped deflate), the same
// container PNG specifies.

using System.IO.Compression;
using System.Text;

namespace ShareX.Tools.Metadata
{
    internal static class PngChunkReader
    {
        internal static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };

        public static List<MetadataEntry> ReadTextChunks(Stream stream)
        {
            List<MetadataEntry> entries = new List<MetadataEntry>();

            byte[] signature = new byte[8];
            if (ReadFully(stream, signature) != 8 || !signature.AsSpan().SequenceEqual(Signature))
            {
                return entries;
            }

            Span<byte> lengthBuffer = stackalloc byte[4];
            byte[] typeBuffer = new byte[4];

            while (true)
            {
                if (ReadFully(stream, lengthBuffer) != 4)
                {
                    break; // EOF
                }

                uint length = (uint)((lengthBuffer[0] << 24) | (lengthBuffer[1] << 16) | (lengthBuffer[2] << 8) | lengthBuffer[3]);

                if (ReadFully(stream, typeBuffer) != 4)
                {
                    break;
                }

                string chunkType = Encoding.ASCII.GetString(typeBuffer);

                if (length > int.MaxValue - 8)
                {
                    break; // absurd/corrupt length
                }

                byte[] data = new byte[length];
                if (length > 0 && ReadFully(stream, data) != length)
                {
                    break; // truncated
                }

                // Skip the trailing 4-byte CRC; we don't validate it (matches exiftool's tolerant reading).
                if (stream.CanSeek)
                {
                    stream.Seek(4, SeekOrigin.Current);
                }
                else
                {
                    byte[] crc = new byte[4];
                    ReadFully(stream, crc);
                }

                switch (chunkType)
                {
                    case "tEXt":
                        TryAddText(entries, ParseTEXt(data));
                        break;
                    case "zTXt":
                        TryAddText(entries, ParseZTXt(data));
                        break;
                    case "iTXt":
                        TryAddText(entries, ParseITXt(data));
                        break;
                    case "IEND":
                        return entries;
                }
            }

            return entries;
        }

        private static void TryAddText(List<MetadataEntry> entries, (string Keyword, string Text)? parsed)
        {
            if (parsed.HasValue)
            {
                entries.Add(new MetadataEntry("PNG", parsed.Value.Keyword, parsed.Value.Text));
            }
        }

        private static (string Keyword, string Text)? ParseTEXt(byte[] data)
        {
            int nul = Array.IndexOf(data, (byte)0);
            if (nul < 0)
            {
                return null;
            }

            string keyword = Latin1(data, 0, nul);
            string text = Latin1(data, nul + 1, data.Length - nul - 1);
            return (keyword, text);
        }

        private static (string Keyword, string Text)? ParseZTXt(byte[] data)
        {
            int nul = Array.IndexOf(data, (byte)0);
            if (nul < 0 || nul + 2 > data.Length)
            {
                return null;
            }

            string keyword = Latin1(data, 0, nul);
            byte compressionMethod = data[nul + 1];
            if (compressionMethod != 0)
            {
                return null; // only zlib/deflate (method 0) is defined by the spec
            }

            byte[] compressed = data[(nul + 2)..];
            string? text = InflateZlibLatin1(compressed);
            return text != null ? (keyword, text) : null;
        }

        private static (string Keyword, string Text)? ParseITXt(byte[] data)
        {
            int p = Array.IndexOf(data, (byte)0);
            if (p < 0 || p + 2 > data.Length)
            {
                return null;
            }

            string keyword = Latin1(data, 0, p);
            byte compressionFlag = data[p + 1];
            byte compressionMethod = data[p + 2];
            int cursor = p + 3;

            int langEnd = Array.IndexOf(data, (byte)0, cursor);
            if (langEnd < 0)
            {
                return null;
            }
            cursor = langEnd + 1;

            int translatedKeywordEnd = Array.IndexOf(data, (byte)0, cursor);
            if (translatedKeywordEnd < 0)
            {
                return null;
            }
            cursor = translatedKeywordEnd + 1;

            byte[] textBytes = data[cursor..];

            string? text;
            if (compressionFlag == 0)
            {
                text = Encoding.UTF8.GetString(textBytes);
            }
            else if (compressionMethod == 0)
            {
                text = InflateZlibUtf8(textBytes);
            }
            else
            {
                return null;
            }

            return text != null ? (keyword, text) : null;
        }

        private static string? InflateZlibLatin1(byte[] compressed) => Inflate(compressed, Latin1Encoding);

        private static string? InflateZlibUtf8(byte[] compressed) => Inflate(compressed, Encoding.UTF8);

        private static string? Inflate(byte[] compressed, Encoding encoding)
        {
            try
            {
                using MemoryStream input = new MemoryStream(compressed);
                using ZLibStream zlib = new ZLibStream(input, CompressionMode.Decompress);
                using MemoryStream output = new MemoryStream();
                zlib.CopyTo(output);
                return encoding.GetString(output.ToArray());
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException)
            {
                return null;
            }
        }

        private static readonly Encoding Latin1Encoding = Encoding.Latin1;

        private static string Latin1(byte[] data, int offset, int count) => Latin1Encoding.GetString(data, offset, count);

        private static int ReadFully(Stream stream, byte[] buffer) => ReadFully(stream, buffer.AsSpan());

        private static int ReadFully(Stream stream, Span<byte> buffer)
        {
            int total = 0;
            while (total < buffer.Length)
            {
                int read = stream.Read(buffer[total..]);
                if (read == 0)
                {
                    break;
                }
                total += read;
            }
            return total;
        }
    }
}
