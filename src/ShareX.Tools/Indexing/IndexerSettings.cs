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

// Ported from ShareX v21.0.0 (commit d2502561f63fc3ff502cacd91514e3f7f2948c74), file ShareX.IndexerLib/IndexerSettings.cs.
// Property names, types and DefaultValue attributes are preserved verbatim -- this is the serialized settings
// contract (.sxie-compatible task settings embed this shape via Newtonsoft.Json, same as upstream).
// Two upstream members are intentionally dropped, both WinForms designer-only and with no effect on the
// serialized shape or indexing behavior:
//   - `using System.Drawing.Design;` and `[Editor(typeof(CssFileNameEditor), typeof(UITypeEditor))]` on
//     CustomCSSFilePath (a PropertyGrid file-picker editor; CssFileNameEditor is a WinForms UITypeEditor that
//     doesn't exist in this port and has no portable equivalent -- the property itself is preserved).
//   - `this.ApplyDefaultPropertyValues()` (a ShareX.HelpersLib reflection helper that reads the DefaultValue
//     attributes below at construction time). ShareX.Tools has no dependency on ShareX.HelpersLib/Core, so the
//     same default values are assigned directly in the constructor instead; the resulting defaults are identical.

using Newtonsoft.Json;
using System.ComponentModel;

namespace ShareX.Tools.Indexing
{
    public class IndexerSettings
    {
        [Category("Indexer"), DefaultValue(IndexerOutput.Html), Description("Indexer output type.")]
        public IndexerOutput Output { get; set; }

        [Category("Indexer"), DefaultValue(true), Description("Don't index hidden folders.")]
        public bool SkipHiddenFolders { get; set; }

        [Category("Indexer"), DefaultValue(true), Description("Don't index hidden files.")]
        public bool SkipHiddenFiles { get; set; }

        [Category("Indexer"), DefaultValue(false), Description("Index only folders and skip files.")]
        public bool SkipFiles { get; set; }

        [Category("Indexer"), DefaultValue(0), Description("Maximum folder depth level for indexing. 0 means unlimited.")]
        public int MaxDepthLevel { get; set; }

        [Category("Indexer"), DefaultValue(true), Description("Write folder and file size.")]
        public bool ShowSizeInfo { get; set; }

        [Category("Indexer"), DefaultValue(true), Description("Add footer information to show application and generated time.")]
        public bool AddFooter { get; set; }

        [Category("Indexer / Text"), DefaultValue("|___"), Description("Padding text to show indentation in the folder hierarchy.")]
        public string IndentationText { get; set; }

        [Category("Indexer / Text"), DefaultValue(false), Description("Adds empty line after folders.")]
        public bool AddEmptyLineAfterFolders { get; set; }

        [Category("Indexer / HTML"), DefaultValue(false), Description("Use custom Cascading Style Sheet file.")]
        public bool UseCustomCSSFile { get; set; }

        [Category("Indexer / HTML"), DefaultValue(false), Description("Display the path for each subfolder.")]
        public bool DisplayPath { get; set; }

        [Category("Indexer / HTML"), DefaultValue(false), Description("Limit the display path to the selected root folder. Must have DisplayPath enabled.")]
        public bool DisplayPathLimited { get; set; }

        [Category("Indexer / HTML"), DefaultValue(""), Description("Custom Cascading Style Sheet file path.")]
        public string CustomCSSFilePath { get; set; }

        [Category("Indexer / XML"), DefaultValue(true), Description("Folder/File information (name, size etc.) will be written as attribute.")]
        public bool UseAttribute { get; set; }

        [Category("Indexer / JSON"), DefaultValue(true), Description("Creates parseable but longer json output.")]
        public bool CreateParseableJson { get; set; }

        [JsonIgnore]
        public bool BinaryUnits;

        public IndexerSettings()
        {
            Output = IndexerOutput.Html;
            SkipHiddenFolders = true;
            SkipHiddenFiles = true;
            SkipFiles = false;
            MaxDepthLevel = 0;
            ShowSizeInfo = true;
            AddFooter = true;
            IndentationText = "|___";
            AddEmptyLineAfterFolders = false;
            UseCustomCSSFile = false;
            DisplayPath = false;
            DisplayPathLimited = false;
            CustomCSSFilePath = "";
            UseAttribute = true;
            CreateParseableJson = true;
        }
    }
}
