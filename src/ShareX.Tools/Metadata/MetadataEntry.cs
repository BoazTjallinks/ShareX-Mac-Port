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

// New portable type: upstream (ShareX/Tools/MetadataForm.cs) shelled out to exiftool.exe (a Windows binary,
// not present or portable to macOS) and parsed its tab-delimited "-G -t -m -q" output into a RichTextBox.
// That external-process dependency is not reproducible portably, so this port replaces it with a self-contained
// reader: SkiaSharp's SKCodec for dimensions/format, plus hand-rolled JPEG APP1/EXIF and PNG tEXt/iTXt/zTXt chunk
// parsing (see MetadataReader.cs, JpegExifReader.cs, PngChunkReader.cs). MetadataEntry(Group, Tag, Value) mirrors
// the (group, tag, value) triple upstream extracted from each exiftool output line.

namespace ShareX.Tools.Metadata
{
    /// <summary>
    /// One metadata item: which group it belongs to (e.g. "File", "EXIF", "GPS", "PNG"), the tag name
    /// (a standard name where known, otherwise a numeric id such as "0x829A"), and its formatted value.
    /// </summary>
    public sealed record MetadataEntry(string Group, string Tag, string Value);
}
