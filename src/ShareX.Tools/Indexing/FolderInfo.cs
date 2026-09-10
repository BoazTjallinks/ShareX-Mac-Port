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

// Ported from ShareX v21.0.0 (commit d2502561f63fc3ff502cacd91514e3f7f2948c74), file ShareX.IndexerLib/FolderInfo.cs.
// Ported verbatim: this file was already portable upstream (plain System.IO.FileInfo/Path, no WinForms/GDI+).

using System.IO;

namespace ShareX.Tools.Indexing
{
    public class FolderInfo
    {
        public string FolderPath { get; set; }
        public List<FileInfo> Files { get; set; }
        public List<FolderInfo> Folders { get; set; }
        public long Size { get; private set; }
        public int TotalFileCount { get; private set; }
        public int TotalFolderCount { get; private set; }
        public FolderInfo? Parent { get; set; }

        public string FolderName => Path.GetFileName(FolderPath);

        public bool IsEmpty => TotalFileCount == 0 && TotalFolderCount == 0;

        public FolderInfo(string folderPath)
        {
            FolderPath = folderPath;
            Files = new List<FileInfo>();
            Folders = new List<FolderInfo>();
        }

        public void Update()
        {
            Folders.ForEach(x => x.Update());
            Folders.Sort((x, y) => x.FolderName.CompareTo(y.FolderName));
            Size = Folders.Sum(x => x.Size) + Files.Sum(x => x.Length);
            TotalFileCount = Files.Count + Folders.Sum(x => x.TotalFileCount);
            TotalFolderCount = Folders.Count + Folders.Sum(x => x.TotalFolderCount);
        }
    }
}
