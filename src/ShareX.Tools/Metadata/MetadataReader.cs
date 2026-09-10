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
// Format detection is by magic bytes (JPEG: FF D8 FF, PNG: the 8-byte PNG signature). Dimensions/format come
// from SkiaSharp's SKCodec (portable, no System.Drawing); EXIF and PNG textual chunks are hand-rolled
// (JpegExifReader / PngChunkReader) per the task brief.

using SkiaSharp;

namespace ShareX.Tools.Metadata
{
    public static class MetadataReader
    {
        private static readonly byte[] JpegSignature = { 0xFF, 0xD8, 0xFF };

        public static MetadataResult ReadFile(string filePath)
        {
            using FileStream stream = File.OpenRead(filePath);
            return Read(stream);
        }

        public static MetadataResult Read(Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);

            if (!stream.CanSeek)
            {
                throw new ArgumentException("Metadata extraction requires a seekable stream.", nameof(stream));
            }

            long startPosition = stream.Position;

            byte[] header = new byte[8];
            int headerRead = ReadFully(stream, header);
            stream.Position = startPosition;

            bool isJpeg = headerRead >= JpegSignature.Length && header.AsSpan(0, JpegSignature.Length).SequenceEqual(JpegSignature);
            bool isPng = headerRead >= PngChunkReader.Signature.Length && header.AsSpan(0, PngChunkReader.Signature.Length).SequenceEqual(PngChunkReader.Signature);

            if (!isJpeg && !isPng)
            {
                return MetadataResult.Unsupported(
                    "Unrecognized image format: expected a JPEG (FF D8 FF) or PNG signature; only JPEG and PNG are supported.");
            }

            List<MetadataEntry> entries = new List<MetadataEntry>();

            if (isJpeg)
            {
                entries.Add(new MetadataEntry("File", "Format", "JPEG"));

                stream.Position = startPosition;
                byte[]? exif = JpegExifReader.FindExifPayload(stream);
                if (exif != null)
                {
                    entries.AddRange(JpegExifReader.ParseTiff(exif));
                }
            }
            else
            {
                entries.Add(new MetadataEntry("File", "Format", "PNG"));

                stream.Position = startPosition;
                entries.AddRange(PngChunkReader.ReadTextChunks(stream));
            }

            stream.Position = startPosition;
            TryAddDimensions(stream, entries);

            stream.Position = startPosition;
            return MetadataResult.Supported(entries);
        }

        private static void TryAddDimensions(Stream stream, List<MetadataEntry> entries)
        {
            try
            {
                using SKCodec? codec = SKCodec.Create(stream);
                if (codec != null)
                {
                    entries.Add(new MetadataEntry("File", "ImageWidth", codec.Info.Width.ToString()));
                    entries.Add(new MetadataEntry("File", "ImageHeight", codec.Info.Height.ToString()));
                    entries.Add(new MetadataEntry("File", "EncodedFormat", codec.EncodedFormat.ToString()));
                }
            }
            catch
            {
                // Best-effort: a synthetic/truncated fixture may not be a fully decodable image; EXIF/PNG-chunk
                // extraction above is independent of this and still succeeds.
            }
        }

        private static int ReadFully(Stream stream, byte[] buffer)
        {
            int total = 0;
            while (total < buffer.Length)
            {
                int read = stream.Read(buffer, total, buffer.Length - total);
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
