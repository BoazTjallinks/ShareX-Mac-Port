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

// New portable implementation -- see MetadataEntry.cs for why there is no upstream file to port here.

namespace ShareX.Tools.Metadata
{
    /// <summary>
    /// Result of a metadata read. When <see cref="IsSupported"/> is false, the input was not a recognized image
    /// format at all (<see cref="UnsupportedReason"/> explains why) rather than being reported as an empty success.
    /// </summary>
    public sealed record MetadataResult(bool IsSupported, string? UnsupportedReason, IReadOnlyList<MetadataEntry> Entries)
    {
        public static MetadataResult Unsupported(string reason) => new(false, reason, Array.Empty<MetadataEntry>());

        public static MetadataResult Supported(IReadOnlyList<MetadataEntry> entries) => new(true, null, entries);
    }
}
