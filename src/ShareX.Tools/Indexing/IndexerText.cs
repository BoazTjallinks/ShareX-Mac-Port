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

// Ported from ShareX v21.0.0 (commit d2502561f63fc3ff502cacd91514e3f7f2948c74),
// file ShareX.IndexerLib/IndexerText.cs.

using System.Text;

namespace ShareX.Tools.Indexing;

public class IndexerText : Indexer
{
    protected StringBuilder sbContent = new();

    public IndexerText(IndexerSettings indexerSettings) : base(indexerSettings)
    {
    }

    public override string Index(string folderPath)
    {
        StringBuilder sbTxtIndex = new();

        FolderInfo folderInfo = GetFolderInfo(folderPath);
        folderInfo.Update();

        IndexFolder(folderInfo);
        string index = sbContent.ToString().Trim();

        sbTxtIndex.AppendLine(index);

        if (settings.AddFooter)
        {
            string footer = IndexerFormatting.GetFooter();
            sbTxtIndex.AppendLine("_".Repeat(footer.Length));
            sbTxtIndex.AppendLine(footer);
        }

        return sbTxtIndex.ToString().Trim();
    }

    protected override void IndexFolder(FolderInfo dir, int level = 0)
    {
        sbContent.AppendLine(GetFolderNameRow(dir, level));

        foreach (FolderInfo subdir in dir.Folders)
        {
            if (settings.AddEmptyLineAfterFolders)
            {
                sbContent.AppendLine();
            }

            IndexFolder(subdir, level + 1);
        }

        if (dir.Files.Count > 0)
        {
            if (settings.AddEmptyLineAfterFolders)
            {
                sbContent.AppendLine();
            }

            foreach (FileInfo fi in dir.Files)
            {
                sbContent.AppendLine(GetFileNameRow(fi, level + 1));
            }
        }
    }

    private string GetFolderNameRow(FolderInfo dir, int level)
    {
        string folderNameRow = settings.IndentationText.Repeat(level) + dir.FolderName;

        // Upstream gates the folder size on Size > 0, but the file size on the
        // ShowSizeInfo flag alone. Preserved rather than made consistent.
        if (settings.ShowSizeInfo && dir.Size > 0)
        {
            folderNameRow += $" [{dir.Size.ToSizeString(settings.BinaryUnits)}]";
        }

        return folderNameRow;
    }

    private string GetFileNameRow(FileInfo fi, int level)
    {
        string fileNameRow = settings.IndentationText.Repeat(level) + fi.Name;

        if (settings.ShowSizeInfo)
        {
            fileNameRow += $" [{fi.Length.ToSizeString(settings.BinaryUnits)}]";
        }

        return fileNameRow;
    }
}
