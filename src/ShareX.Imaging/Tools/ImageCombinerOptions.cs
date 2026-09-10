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

// Ported from ShareX v21.0.0 (commit d2502561f63fc3ff502cacd91514e3f7f2948c74), file ShareX.MediaLib/ImageCombinerOptions.cs.
//
// Porting notes:
//   - Property names, types and defaults are preserved verbatim (this is a serialized settings
//     contract per the task brief), with one necessary substitution: upstream's
//     System.Windows.Forms.Orientation becomes ShareX.Imaging.Orientation (see
//     Enums/ImageCombinerAlignment.cs) since WinForms is banned on this platform. The replacement
//     enum has identical member names and ordinal values.

namespace ShareX.Imaging;

public class ImageCombinerOptions
{
    public Orientation Orientation { get; set; } = Orientation.Vertical;
    public ImageCombinerAlignment Alignment { get; set; } = ImageCombinerAlignment.LeftOrTop;
    public int Space { get; set; } = 0;
    public int WrapAfter { get; set; } = 0;
    public bool AutoFillBackground { get; set; } = true;
}
