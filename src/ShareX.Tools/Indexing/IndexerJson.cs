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
// file ShareX.IndexerLib/IndexerJson.cs.

using System.Text;
using Newtonsoft.Json;

namespace ShareX.Tools.Indexing;

public class IndexerJson : Indexer
{
    private JsonWriter jsonWriter = null!;

    public IndexerJson(IndexerSettings indexerSettings) : base(indexerSettings)
    {
    }

    public override string Index(string folderPath)
    {
        FolderInfo folderInfo = GetFolderInfo(folderPath);
        folderInfo.Update();

        StringBuilder sbContent = new();

        using (StringWriter sw = new(sbContent))
        using (jsonWriter = new JsonTextWriter(sw))
        {
            jsonWriter.Formatting = Formatting.Indented;

            jsonWriter.WriteStartObject();
            IndexFolder(folderInfo);
            jsonWriter.WriteEndObject();
        }

        return sbContent.ToString();
    }

    protected override void IndexFolder(FolderInfo dir, int level = 0)
    {
        // Two genuinely different JSON shapes, both upstream: the "simple" form
        // nests folder names as property keys (compact, but awkward to parse),
        // while CreateParseableJson emits explicit Name/Size/Folders/Files
        // objects. Both are preserved.
        if (settings.CreateParseableJson)
        {
            IndexFolderParseable(dir);
        }
        else
        {
            IndexFolderSimple(dir);
        }
    }

    private void IndexFolderSimple(FolderInfo dir)
    {
        jsonWriter.WritePropertyName(dir.FolderName);
        jsonWriter.WriteStartArray();

        foreach (FolderInfo subdir in dir.Folders)
        {
            jsonWriter.WriteStartObject();
            IndexFolder(subdir);
            jsonWriter.WriteEndObject();
        }

        foreach (FileInfo fi in dir.Files)
        {
            jsonWriter.WriteValue(fi.Name);
        }

        jsonWriter.WriteEnd();
    }

    private void IndexFolderParseable(FolderInfo dir)
    {
        jsonWriter.WritePropertyName("Name");
        jsonWriter.WriteValue(dir.FolderName);

        if (settings.ShowSizeInfo)
        {
            jsonWriter.WritePropertyName("Size");
            jsonWriter.WriteValue(dir.Size.ToSizeString(settings.BinaryUnits));
        }

        if (dir.Folders.Count > 0)
        {
            jsonWriter.WritePropertyName("Folders");
            jsonWriter.WriteStartArray();

            foreach (FolderInfo subdir in dir.Folders)
            {
                jsonWriter.WriteStartObject();
                IndexFolder(subdir);
                jsonWriter.WriteEndObject();
            }

            jsonWriter.WriteEnd();
        }

        if (dir.Files.Count > 0)
        {
            jsonWriter.WritePropertyName("Files");
            jsonWriter.WriteStartArray();

            foreach (FileInfo fi in dir.Files)
            {
                jsonWriter.WriteStartObject();

                jsonWriter.WritePropertyName("Name");
                jsonWriter.WriteValue(fi.Name);

                if (settings.ShowSizeInfo)
                {
                    jsonWriter.WritePropertyName("Size");
                    jsonWriter.WriteValue(fi.Length.ToSizeString(settings.BinaryUnits));
                }

                jsonWriter.WriteEndObject();
            }

            jsonWriter.WriteEnd();
        }
    }
}
