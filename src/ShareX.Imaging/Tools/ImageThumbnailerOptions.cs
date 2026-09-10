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

// Ported from ShareX v21.0.0 (commit d2502561f63fc3ff502cacd91514e3f7f2948c74), file ShareX/TaskSettings.cs
// (TaskSettingsImage's "Image / Thumbnail" region: ThumbnailWidth, ThumbnailHeight, ThumbnailName,
// ThumbnailCheckSize).
//
// Porting notes:
//   - Property names, types and defaults are preserved verbatim (this mirrors the same serialized
//     field set already carried by src/ShareX.Core/Workflow/TaskSettingsSnapshot.cs, which this
//     library does not reference - ShareX.Imaging has no dependency on ShareX.Core).
//   - Quality is NOT an upstream TaskSettings field: TaskHelpers.CreateThumbnail hardcodes
//     ImageHelpers.SaveJPEG(..., 90) for the automatic post-capture thumbnail. The manual
//     ShareX.MediaLib.ImageThumbnailerForm tool *does* expose an equivalent user-facing quality
//     control (nudQuality, default value 90 - see ImageThumbnailerForm.Designer.cs). This options
//     type exposes Quality so both upstream code paths are representable through one API, defaulting
//     to 90 so the automatic path's behavior is unchanged unless a caller opts in. See
//     PORTING-NOTES.md ("ImageThumbnailer: two upstream algorithms, one options type").

namespace ShareX.Imaging;

public class ImageThumbnailerOptions
{
    public int ThumbnailWidth { get; set; } = 200;
    public int ThumbnailHeight { get; set; } = 0;
    public string ThumbnailName { get; set; } = "-thumbnail";
    public bool ThumbnailCheckSize { get; set; } = false;
    public int Quality { get; set; } = 90;
}
