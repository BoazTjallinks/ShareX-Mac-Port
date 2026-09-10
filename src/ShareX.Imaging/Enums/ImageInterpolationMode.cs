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
//   - Member names and order preserved verbatim (this enum backs a user-facing settings combo box
//     in upstream; ordinal values are not known to be serialized anywhere, but order is kept
//     identical regardless).
//   - There is no 1:1 equivalent of System.Drawing.Drawing2D.InterpolationMode in SkiaSharp: GDI+
//     exposes five named quality tiers across two families (bicubic, bilinear) plus nearest
//     neighbor; Skia's SKSamplingOptions exposes a filter (Nearest/Linear), an optional mipmap mode,
//     and an optional cubic resampler (with continuous B/C parameters, not named tiers). The mapping
//     in ToSamplingOptions() below is a deliberate, documented approximation - see PORTING-NOTES.md.

namespace ShareX.Imaging;

/// <summary>
/// Ported verbatim from ShareX.HelpersLib.ImageInterpolationMode (backs
/// System.Drawing.Drawing2D.InterpolationMode choices in the upstream settings UI).
/// </summary>
public enum ImageInterpolationMode
{
    HighQualityBicubic,
    Bicubic,
    HighQualityBilinear,
    Bilinear,
    NearestNeighbor
}

public static class ImageInterpolationModeExtensions
{
    /// <summary>
    /// Maps upstream's GDI+ interpolation tier to an explicit SkiaSharp SKSamplingOptions.
    /// See PORTING-NOTES.md ("Interpolation mode mapping") for the rationale behind each mapping.
    /// </summary>
    public static SkiaSharp.SKSamplingOptions ToSamplingOptions(this ImageInterpolationMode mode) => mode switch
    {
        // GDI+'s highest-quality bicubic tier -> Skia's Mitchell-Netravali cubic resampler, Skia's
        // own recommended "high quality" default cubic filter.
        ImageInterpolationMode.HighQualityBicubic => new SkiaSharp.SKSamplingOptions(SkiaSharp.SKCubicResampler.Mitchell),
        // GDI+'s standard-quality bicubic tier -> Skia's Catmull-Rom cubic resampler (sharper,
        // cheaper than Mitchell; the closest "plain bicubic" analog Skia exposes).
        ImageInterpolationMode.Bicubic => new SkiaSharp.SKSamplingOptions(SkiaSharp.SKCubicResampler.CatmullRom),
        // GDI+'s high-quality bilinear tier -> linear filtering with linear mipmapping, which is
        // what makes downscaling by large factors look correct instead of aliased.
        ImageInterpolationMode.HighQualityBilinear => new SkiaSharp.SKSamplingOptions(SkiaSharp.SKFilterMode.Linear, SkiaSharp.SKMipmapMode.Linear),
        // GDI+'s plain bilinear tier -> linear filtering, no mipmap chain.
        ImageInterpolationMode.Bilinear => new SkiaSharp.SKSamplingOptions(SkiaSharp.SKFilterMode.Linear, SkiaSharp.SKMipmapMode.None),
        ImageInterpolationMode.NearestNeighbor => new SkiaSharp.SKSamplingOptions(SkiaSharp.SKFilterMode.Nearest, SkiaSharp.SKMipmapMode.None),
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
    };
}
