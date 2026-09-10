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

// Ported from ShareX v21.0.0 (commit d2502561f63fc3ff502cacd91514e3f7f2948c74), file ShareX.HelpersLib/Enums.cs.
//
// Porting notes:
//   - Member names, declaration order and implicit numeric values (PNG=0, JPEG=1, GIF=2, BMP=3, TIFF=4)
//     are preserved verbatim. This enum is a serialized contract: it appears in .sxcu files, task
//     settings and upload history, so it must not be reordered or renamed.
//   - The upstream [Description("png")]/[Description("jpg")]/... attributes are preserved as an
//     Extension() lookup below instead of a System.ComponentModel.DescriptionAttribute dependency,
//     since ShareX.Imaging intentionally has no dependency on ShareX.HelpersLib/System.Windows.Forms.
//   - GIF and TIFF are part of the verbatim enum (for settings/history compatibility) but this
//     library's codec layer (see Codecs/ImageCodec.cs) does not implement encoders for them; see
//     PORTING-NOTES.md for why.

namespace ShareX.Imaging;

/// <summary>
/// Upstream's image container format enum (ShareX.HelpersLib.EImageFormat), ported verbatim so
/// that serialized task settings, .sxcu files and upload history that reference these ordinal
/// values continue to round-trip correctly.
/// </summary>
public enum EImageFormat
{
    PNG,
    JPEG,
    GIF,
    BMP,
    TIFF
}

/// <summary>
/// The lower-case file extension upstream associates with each <see cref="EImageFormat"/> member
/// (originally a <c>[Description]</c> attribute on the enum, see porting notes above).
/// </summary>
public static class EImageFormatExtensions
{
    public static string GetExtension(this EImageFormat format) => format switch
    {
        EImageFormat.PNG => "png",
        EImageFormat.JPEG => "jpg",
        EImageFormat.GIF => "gif",
        EImageFormat.BMP => "bmp",
        EImageFormat.TIFF => "tif",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
    };
}
