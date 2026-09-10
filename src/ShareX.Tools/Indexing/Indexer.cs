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

// Ported from ShareX v21.0.0 (commit d2502561f63fc3ff502cacd91514e3f7f2948c74), file ShareX.IndexerLib/Indexer.cs.
// The public Index(string, IndexerSettings) dispatch, the abstract Index/IndexFolder contract, and the
// GetFolderInfo folder walk (including SkipHiddenFolders/SkipHiddenFiles, SkipFiles, MaxDepthLevel and the
// UnauthorizedAccessException swallow) are ported verbatim.
//
// Deviation (required by the ShareX-Mac task spec, not present upstream): upstream's GetFolderInfo recurses into
// every subdirectory .NET's DirectoryInfo.EnumerateDirectories() yields with no cycle protection. On Windows,
// directory symlinks/junctions are rare and NTFS reparse points are typically not traversed transparently by the
// enumerator; on macOS, a symlink loop (a directory symlinked into one of its own descendants) is trivial to
// create and IS traversed, which would recurse until a stack overflow. This port adds two independent guards
// that change no observable output for a cycle-free tree:
//   1. A per-branch "ancestors" set of resolved real paths (symlinks resolved via FileSystemInfo.ResolveLinkTarget)
//      -- if a directory's real path is already an ancestor on the current recursion path, it is skipped instead
//      of recursed into (this is the actual cycle break).
//   2. A hard recursion depth ceiling (HardMaxDepth), independent of the user-configurable MaxDepthLevel, so that
//      even a non-cyclic but pathologically deep tree cannot exhaust the stack when MaxDepthLevel is 0 (unlimited).

using System;
using System.IO;

namespace ShareX.Tools.Indexing
{
    public abstract class Indexer
    {
        /// <summary>
        /// Safety ceiling on recursion depth, independent of <see cref="IndexerSettings.MaxDepthLevel"/>.
        /// Not part of the upstream serialized contract -- purely an internal guard against stack exhaustion.
        /// </summary>
        private const int HardMaxDepth = 1000;

        protected IndexerSettings settings;

        protected Indexer(IndexerSettings indexerSettings)
        {
            settings = indexerSettings;
        }

        public static string Index(string folderPath, IndexerSettings settings)
        {
            Indexer? indexer = settings.Output switch
            {
                IndexerOutput.Html => new IndexerHtml(settings),
                IndexerOutput.Txt => new IndexerText(settings),
                IndexerOutput.Xml => new IndexerXml(settings),
                IndexerOutput.Json => new IndexerJson(settings),
                _ => null
            };

            if (indexer == null)
            {
                throw new ArgumentOutOfRangeException(nameof(settings), settings.Output, "Unsupported indexer output type.");
            }

            return indexer.Index(folderPath);
        }

        public abstract string Index(string folderPath);

        protected abstract void IndexFolder(FolderInfo dir, int level = 0);

        protected FolderInfo GetFolderInfo(string folderPath, int level = 0)
        {
            return GetFolderInfo(folderPath, level, new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal));
        }

        private FolderInfo GetFolderInfo(string folderPath, int level, System.Collections.Generic.HashSet<string> ancestors)
        {
            FolderInfo folderInfo = new FolderInfo(folderPath);

            if ((settings.MaxDepthLevel == 0 || level < settings.MaxDepthLevel) && level < HardMaxDepth)
            {
                string realPath = ResolveRealPath(folderPath);

                // If this directory's real (symlink-resolved) path is already an ancestor on the current
                // recursion branch, recursing into it would loop forever: stop here instead.
                if (ancestors.Add(realPath))
                {
                    try
                    {
                        DirectoryInfo currentDirectoryInfo = new DirectoryInfo(folderPath);

                        foreach (DirectoryInfo directoryInfo in currentDirectoryInfo.EnumerateDirectories())
                        {
                            if (settings.SkipHiddenFolders && directoryInfo.Attributes.HasFlag(FileAttributes.Hidden))
                            {
                                continue;
                            }

                            FolderInfo subFolderInfo = GetFolderInfo(directoryInfo.FullName, level + 1, ancestors);
                            folderInfo.Folders.Add(subFolderInfo);
                            subFolderInfo.Parent = folderInfo;
                        }

                        if (!settings.SkipFiles)
                        {
                            foreach (FileInfo fileInfo in currentDirectoryInfo.EnumerateFiles())
                            {
                                if (settings.SkipHiddenFiles && fileInfo.Attributes.HasFlag(FileAttributes.Hidden))
                                {
                                    continue;
                                }

                                folderInfo.Files.Add(fileInfo);
                            }

                            folderInfo.Files.Sort((x, y) => string.CompareOrdinal(x.Name, y.Name));
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                    finally
                    {
                        ancestors.Remove(realPath);
                    }
                }
            }

            return folderInfo;
        }

        /// <summary>
        /// Resolves a directory path to a canonical form suitable for cycle detection: symlinks are followed to
        /// their final target. Any failure (broken link, "too many levels of symbolic links", permission error)
        /// falls back to the plain absolute path, which is still safe -- it just means that particular broken
        /// link won't be recognized as a duplicate ancestor (it also can't be recursed into successfully anyway).
        /// </summary>
        private static string ResolveRealPath(string path)
        {
            try
            {
                DirectoryInfo info = new DirectoryInfo(path);
                FileSystemInfo? finalTarget = info.ResolveLinkTarget(returnFinalTarget: true);
                return finalTarget?.FullName ?? info.FullName;
            }
            catch (IOException)
            {
                return Path.GetFullPath(path);
            }
            catch (UnauthorizedAccessException)
            {
                return Path.GetFullPath(path);
            }
        }
    }
}
