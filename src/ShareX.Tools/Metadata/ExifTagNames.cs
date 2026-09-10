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

// New portable implementation. Tag id -> standard name mappings taken from the public CIPA Exif 2.32 tag tables
// (the same tag ids/names exiftool, upstream's external metadata engine, reports). This is not an exhaustive
// dump of every registered Exif tag -- only the commonly populated ones are named; anything else is reported by
// its numeric id (see JpegExifReader) rather than a made-up name, per the task brief.

namespace ShareX.Tools.Metadata
{
    internal static class ExifTagNames
    {
        // 0th IFD (main image) / TIFF tags.
        internal static readonly IReadOnlyDictionary<ushort, string> Ifd0 = new Dictionary<ushort, string>
        {
            [0x0100] = "ImageWidth",
            [0x0101] = "ImageLength",
            [0x0102] = "BitsPerSample",
            [0x0103] = "Compression",
            [0x0106] = "PhotometricInterpretation",
            [0x010E] = "ImageDescription",
            [0x010F] = "Make",
            [0x0110] = "Model",
            [0x0111] = "StripOffsets",
            [0x0112] = "Orientation",
            [0x0115] = "SamplesPerPixel",
            [0x0116] = "RowsPerStrip",
            [0x0117] = "StripByteCounts",
            [0x011A] = "XResolution",
            [0x011B] = "YResolution",
            [0x011C] = "PlanarConfiguration",
            [0x0128] = "ResolutionUnit",
            [0x0131] = "Software",
            [0x0132] = "DateTime",
            [0x013B] = "Artist",
            [0x8298] = "Copyright",
        };

        // Exif SubIFD tags (reached via IFD0 tag 0x8769).
        internal static readonly IReadOnlyDictionary<ushort, string> ExifIfd = new Dictionary<ushort, string>
        {
            [0x829A] = "ExposureTime",
            [0x829D] = "FNumber",
            [0x8822] = "ExposureProgram",
            [0x8827] = "ISOSpeedRatings",
            [0x9000] = "ExifVersion",
            [0x9003] = "DateTimeOriginal",
            [0x9004] = "DateTimeDigitized",
            [0x9101] = "ComponentsConfiguration",
            [0x9201] = "ShutterSpeedValue",
            [0x9202] = "ApertureValue",
            [0x9204] = "ExposureBiasValue",
            [0x9205] = "MaxApertureValue",
            [0x9206] = "SubjectDistance",
            [0x9207] = "MeteringMode",
            [0x9208] = "LightSource",
            [0x9209] = "Flash",
            [0x920A] = "FocalLength",
            [0x9286] = "UserComment",
            [0xA000] = "FlashpixVersion",
            [0xA001] = "ColorSpace",
            [0xA002] = "PixelXDimension",
            [0xA003] = "PixelYDimension",
            [0xA20E] = "FocalPlaneXResolution",
            [0xA20F] = "FocalPlaneYResolution",
            [0xA210] = "FocalPlaneResolutionUnit",
            [0xA215] = "ExposureIndex",
            [0xA217] = "SensingMethod",
            [0xA300] = "FileSource",
            [0xA301] = "SceneType",
            [0xA402] = "ExposureMode",
            [0xA403] = "WhiteBalance",
            [0xA404] = "DigitalZoomRatio",
            [0xA405] = "FocalLengthIn35mmFilm",
            [0xA406] = "SceneCaptureType",
            [0xA407] = "GainControl",
            [0xA408] = "Contrast",
            [0xA409] = "Saturation",
            [0xA40A] = "Sharpness",
            [0xA420] = "ImageUniqueID",
            [0xA430] = "CameraOwnerName",
            [0xA431] = "BodySerialNumber",
            [0xA432] = "LensSpecification",
            [0xA433] = "LensMake",
            [0xA434] = "LensModel",
        };

        // GPS IFD tags (reached via IFD0 tag 0x8825).
        internal static readonly IReadOnlyDictionary<ushort, string> GpsIfd = new Dictionary<ushort, string>
        {
            [0x0000] = "GPSVersionID",
            [0x0001] = "GPSLatitudeRef",
            [0x0002] = "GPSLatitude",
            [0x0003] = "GPSLongitudeRef",
            [0x0004] = "GPSLongitude",
            [0x0005] = "GPSAltitudeRef",
            [0x0006] = "GPSAltitude",
            [0x0007] = "GPSTimeStamp",
            [0x0012] = "GPSMapDatum",
            [0x001D] = "GPSDateStamp",
        };

        /// <summary>IFD0 pointer tags that are followed rather than emitted as plain entries.</summary>
        internal const ushort ExifIfdPointerTag = 0x8769;
        internal const ushort GpsIfdPointerTag = 0x8825;
        internal const ushort InteropIfdPointerTag = 0xA005;

        internal static string Resolve(IReadOnlyDictionary<ushort, string> table, ushort tag) =>
            table.TryGetValue(tag, out string? name) ? name : $"0x{tag:X4}";
    }
}
