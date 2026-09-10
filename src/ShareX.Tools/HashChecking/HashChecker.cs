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

// Ported from ShareX v21.0.0 (commit d2502561f63fc3ff502cacd91514e3f7f2948c74), file ShareX.HelpersLib/Cryptographic/HashChecker.cs.
// The file-based Start/Stop/IsWorking API and GetHashAlgorithm switch are ported verbatim (including the 8192-byte
// bounded read buffer and the >200ms progress-report throttle from HashCheckThread). A stream-based ComputeAsync
// overload and a CompareHashes helper were added so the tool can hash arbitrary portable Stream sources (upstream
// only ever hashed local files opened by HashCheckerForm) -- these are new surface area, not present upstream.

using System.Diagnostics;
using System.Security.Cryptography;

namespace ShareX.Tools.HashChecking
{
    /// <summary>
    /// Portable, headless hash checker. Hashes a stream or file in bounded 8192-byte reads (never buffering the
    /// whole input in memory), reporting progress and honouring cancellation, matching upstream HashChecker.
    /// </summary>
    public class HashChecker
    {
        public bool IsWorking { get; private set; }

        public delegate void ProgressChanged(float progress);
        public event ProgressChanged? FileCheckProgressChanged;

        private CancellationTokenSource? cts;

        private void OnProgressChanged(float percentage)
        {
            FileCheckProgressChanged?.Invoke(percentage);
        }

        /// <summary>
        /// Ported entry point: hashes a file on disk, exactly matching upstream HashChecker.Start.
        /// Returns null if another check is already in progress, the path is empty, the file doesn't exist,
        /// or the operation was cancelled.
        /// </summary>
        public async Task<string?> Start(string filePath, HashType hashType)
        {
            string? result = null;

            if (!IsWorking && !string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                IsWorking = true;

                Progress<float> progress = new Progress<float>(OnProgressChanged);

                using (cts = new CancellationTokenSource())
                {
                    result = await Task.Run(() =>
                    {
                        try
                        {
                            using FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                            return HashCheckThread(stream, hashType, progress, cts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                        }

                        return null;
                    }, cts.Token);
                }

                IsWorking = false;
            }

            return result;
        }

        public void Stop()
        {
            cts?.Cancel();
        }

        /// <summary>
        /// Portable stream-based hashing helper (new surface area; not part of the upstream API). Hashes any
        /// seekable or non-seekable stream using the same bounded 8192-byte buffer and cancellation semantics as
        /// upstream's HashCheckThread. Progress is only reported when the stream length is known (CanSeek).
        /// </summary>
        public static async Task<string> ComputeAsync(Stream stream, HashType hashType, IProgress<float>? progress = null,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() => ComputeCore(stream, hashType, progress, cancellationToken), cancellationToken);
        }

        /// <summary>
        /// Synchronous portable stream-based hashing helper, used internally and available for callers that are
        /// already off the UI/async context.
        /// </summary>
        public static string Compute(Stream stream, HashType hashType, IProgress<float>? progress = null,
            CancellationToken cancellationToken = default)
        {
            return ComputeCore(stream, hashType, progress, cancellationToken);
        }

        private static string HashCheckThread(Stream stream, HashType hashType, IProgress<float> progress, CancellationToken ct)
        {
            return ComputeCore(stream, hashType, progress, ct);
        }

        private static string ComputeCore(Stream stream, HashType hashType, IProgress<float>? progress, CancellationToken ct)
        {
            using HashAlgorithm hash = GetHashAlgorithm(hashType) ?? throw new ArgumentOutOfRangeException(nameof(hashType), hashType, "Unsupported hash type.");
            using CryptoStream cs = new CryptoStream(stream, hash, CryptoStreamMode.Read, leaveOpen: true);

            long totalRead = 0;
            long? length = stream.CanSeek ? stream.Length : null;
            byte[] buffer = new byte[8192];
            Stopwatch timer = Stopwatch.StartNew();

            int bytesRead;
            while ((bytesRead = cs.Read(buffer, 0, buffer.Length)) > 0 && !ct.IsCancellationRequested)
            {
                totalRead += bytesRead;

                if (progress != null && length.HasValue && length.Value > 0 && timer.ElapsedMilliseconds > 200)
                {
                    float percentage = (float)totalRead / length.Value * 100;
                    progress.Report(percentage);

                    timer.Reset();
                    timer.Start();
                }
            }

            if (ct.IsCancellationRequested)
            {
                progress?.Report(0);

                ct.ThrowIfCancellationRequested();
            }

            progress?.Report(100);

            byte[] digest = hash.Hash ?? throw new InvalidOperationException("Hash algorithm did not produce a digest.");
            return Convert.ToHexStringLower(digest);
        }

        /// <summary>
        /// Ported verbatim from upstream HashChecker.GetHashAlgorithm.
        /// </summary>
        public static HashAlgorithm? GetHashAlgorithm(HashType hashType)
        {
            switch (hashType)
            {
                case HashType.CRC32:
                    return new Crc32();
                case HashType.MD5:
                    return MD5.Create();
                case HashType.SHA1:
                    return SHA1.Create();
                case HashType.SHA256:
                    return SHA256.Create();
                case HashType.SHA384:
                    return SHA384.Create();
                case HashType.SHA512:
                    return SHA512.Create();
            }

            return null;
        }

        /// <summary>
        /// Compares two hash strings the way upstream's HashCheckerForm.UpdateResult does: case-insensitive.
        /// Additionally tolerant of surrounding/interior whitespace, matching how users paste hashes to compare
        /// (upstream only compared exact textbox contents; this generalizes that comparison for headless use).
        /// </summary>
        public static bool CompareHashes(string? a, string? b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
            {
                return false;
            }

            string normalizedA = RemoveWhitespace(a);
            string normalizedB = RemoveWhitespace(b);

            return normalizedA.Equals(normalizedB, StringComparison.OrdinalIgnoreCase);
        }

        private static string RemoveWhitespace(string value)
        {
            Span<char> buffer = value.Length <= 256 ? stackalloc char[value.Length] : new char[value.Length];
            int count = 0;

            foreach (char c in value)
            {
                if (!char.IsWhiteSpace(c))
                {
                    buffer[count++] = c;
                }
            }

            return new string(buffer[..count]);
        }
    }
}
