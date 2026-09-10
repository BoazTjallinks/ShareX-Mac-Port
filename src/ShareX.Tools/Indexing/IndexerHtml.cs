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
// files ShareX.IndexerLib/IndexerHtml.cs and HtmlHelper.cs. The default
// stylesheet is upstream's Resources/IndexerDefault.css, embedded verbatim.

using System.Net;
using System.Reflection;
using System.Text;

namespace ShareX.Tools.Indexing;

/// <summary>Upstream HtmlHelper, inlined: three small tag builders.</summary>
internal static class HtmlHelper
{
    public static string StartTag(string tag, string style = "", string otherFields = "")
    {
        string css = string.IsNullOrEmpty(style) ? "" : $" style=\"{style}\"";
        string fields = string.IsNullOrEmpty(otherFields) ? "" : $" {otherFields}";
        return $"<{tag}{css}{fields}>";
    }

    public static string EndTag(string tag) => $"</{tag}>";

    public static string Tag(string tag, string content, string style = "", string otherFields = "")
        => StartTag(tag, style, otherFields) + HtmlEncode(content) + EndTag(tag);

    /// <summary>
    /// Upstream's URLHelpers.HtmlEncode, ported exactly.
    ///
    /// It is NOT the same as <see cref="WebUtility.HtmlEncode"/>: upstream first
    /// HTML-encodes, then replaces EVERY character above 127 with a numeric
    /// character reference, so the generated document is pure ASCII.
    /// WebUtility leaves 128-159 alone and only escapes from 160 upward, which
    /// would silently change the output for any non-ASCII filename. Using it here
    /// was a real divergence, caught by a unicode-filename test.
    /// </summary>
    public static string HtmlEncode(string text)
    {
        string encoded = WebUtility.HtmlEncode(text);
        var result = new StringBuilder(encoded.Length + (encoded.Length / 10));

        foreach (char c in encoded)
        {
            int value = c;

            if (value > 127)
            {
                result.Append("&#").Append(value).Append(';');
            }
            else
            {
                result.Append(c);
            }
        }

        return result.ToString();
    }
}

public class IndexerHtml : Indexer
{
    protected StringBuilder sbContent = new();
    protected int prePathTrim;

    public IndexerHtml(IndexerSettings indexerSettings) : base(indexerSettings)
    {
    }

    public override string Index(string folderPath)
    {
        StringBuilder sbHtmlIndex = new();
        sbHtmlIndex.AppendLine("<!DOCTYPE html>");
        sbHtmlIndex.AppendLine(HtmlHelper.StartTag("html"));
        sbHtmlIndex.AppendLine(HtmlHelper.StartTag("head"));
        sbHtmlIndex.AppendLine("<meta charset=\"UTF-8\">");
        sbHtmlIndex.AppendLine("<meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\">");
        sbHtmlIndex.AppendLine(HtmlHelper.Tag("title", "Index for " + Path.GetFileName(folderPath)));
        sbHtmlIndex.AppendLine(GetCssStyle());
        sbHtmlIndex.AppendLine(HtmlHelper.EndTag("head"));
        sbHtmlIndex.AppendLine(HtmlHelper.StartTag("body"));

        // Upstream trims a trailing backslash and searches for the last
        // backslash to compute the "limited path" prefix. On macOS the separator
        // is '/', so Path.DirectorySeparatorChar is used instead; trimming a
        // literal '\' here would corrupt a legitimate macOS filename.
        folderPath = Path.GetFullPath(folderPath).TrimEnd(Path.DirectorySeparatorChar);
        prePathTrim = folderPath.LastIndexOf(Path.DirectorySeparatorChar) + 1;

        FolderInfo folderInfo = GetFolderInfo(folderPath);
        folderInfo.Update();

        IndexFolder(folderInfo);
        sbHtmlIndex.AppendLine(sbContent.ToString().Trim());

        if (settings.AddFooter)
        {
            sbHtmlIndex.AppendLine(HtmlHelper.StartTag("div") + GetFooter() + HtmlHelper.EndTag("div"));
        }

        sbHtmlIndex.AppendLine(HtmlHelper.EndTag("body"));
        sbHtmlIndex.AppendLine(HtmlHelper.EndTag("html"));

        return sbHtmlIndex.ToString().Trim();
    }

    protected override void IndexFolder(FolderInfo dir, int level = 0)
    {
        sbContent.AppendLine(GetFolderNameRow(dir, level));

        string divClass = level > 0 ? "FolderBorder" : "MainFolderBorder";
        sbContent.AppendLine(HtmlHelper.StartTag("div", "", $"class=\"{divClass}\""));

        if (dir.Files.Count > 0)
        {
            sbContent.AppendLine(HtmlHelper.StartTag("ul"));

            foreach (FileInfo fi in dir.Files)
            {
                sbContent.AppendLine(GetFileNameRow(fi));
            }

            sbContent.AppendLine(HtmlHelper.EndTag("ul"));
        }

        // Note the order difference from the text indexer: HTML emits files
        // before subfolders. Preserved as upstream has it.
        foreach (FolderInfo subdir in dir.Folders)
        {
            IndexFolder(subdir, level + 1);
        }

        sbContent.AppendLine(HtmlHelper.EndTag("div"));
    }

    private string GetFolderNameRow(FolderInfo dir, int level)
    {
        string folderNameRow = "";

        if (!dir.IsEmpty)
        {
            if (settings.ShowSizeInfo)
            {
                folderNameRow += dir.Size.ToSizeString(settings.BinaryUnits) + " ";
            }

            folderNameRow += "(";

            if (dir.TotalFileCount > 0)
            {
                folderNameRow += dir.TotalFileCount.ToString("n0")
                                 + " file" + (dir.TotalFileCount > 1 ? "s" : "");
            }

            if (dir.TotalFolderCount > 0)
            {
                if (dir.TotalFileCount > 0)
                {
                    folderNameRow += ", ";
                }

                folderNameRow += dir.TotalFolderCount.ToString("n0")
                                 + " folder" + (dir.TotalFolderCount > 1 ? "s" : "");
            }

            folderNameRow += ")";
            folderNameRow = " " + HtmlHelper.Tag("span", folderNameRow, "", "class=\"FolderInfo\"");
        }

        string pathTitle;

        if (settings.DisplayPath)
        {
            pathTitle = settings.DisplayPathLimited && dir.FolderPath.Length >= prePathTrim
                ? dir.FolderPath[prePathTrim..]
                : dir.FolderPath;
        }
        else
        {
            pathTitle = dir.FolderName;
        }

        int heading = Math.Clamp(level + 1, 1, 6);

        return HtmlHelper.StartTag("h" + heading)
               + HtmlHelper.HtmlEncode(pathTitle)
               + folderNameRow
               + HtmlHelper.EndTag("h" + heading);
    }

    private string GetFileNameRow(FileInfo fi)
    {
        string fileNameRow = HtmlHelper.StartTag("li") + HtmlHelper.HtmlEncode(fi.Name);

        if (settings.ShowSizeInfo)
        {
            fileNameRow += " " + HtmlHelper.Tag(
                "span", fi.Length.ToSizeString(settings.BinaryUnits), "", "class=\"FileSize\"");
        }

        return fileNameRow + HtmlHelper.EndTag("li");
    }

    private static string GetFooter() =>
        "Generated by <a href=\"https://getsharex.com\">ShareX Directory Indexer</a> on "
        + DateTime.UtcNow.ToString(
            "yyyy-MM-dd 'at' HH:mm:ss 'UTC'", System.Globalization.CultureInfo.InvariantCulture);

    private string GetCssStyle()
    {
        string css;

        if (settings.UseCustomCSSFile
            && !string.IsNullOrEmpty(settings.CustomCSSFilePath)
            && File.Exists(settings.CustomCSSFilePath))
        {
            css = File.ReadAllText(settings.CustomCSSFilePath, Encoding.UTF8);
        }
        else
        {
            css = DefaultCss.Value;
        }

        return $"<style type=\"text/css\">\r\n{css}\r\n</style>";
    }

    /// <summary>
    /// Upstream's Resources.IndexerDefault, embedded as a resource so the
    /// generated document looks the same without a WinForms resource designer.
    /// </summary>
    private static readonly Lazy<string> DefaultCss = new(() =>
    {
        Assembly assembly = typeof(IndexerHtml).Assembly;
        const string name = "ShareX.Tools.Indexing.Resources.IndexerDefault.css";

        using Stream? stream = assembly.GetManifestResourceStream(name);
        if (stream is null)
        {
            // Fail visibly rather than silently emitting an unstyled document.
            throw new InvalidOperationException($"Embedded resource '{name}' is missing.");
        }

        using StreamReader reader = new(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    });
}
