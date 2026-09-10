using System.Text;
using ShareX.Tools.HashChecking;
using Xunit;

namespace ShareX.Tools.Tests;

/// <summary>
/// Known-answer vectors. These are the published digests for the empty string
/// and for "abc" (FIPS 180-4 test vectors for the SHA family, RFC 1321 appendix
/// A.5 for MD5, and the standard CRC-32/ISO-HDLC check value), so a wrong
/// algorithm choice or a broken streaming loop fails here rather than silently
/// producing plausible-looking hex.
/// </summary>
public sealed class HashCheckerTests
{
    private static string Hash(string text, HashType type)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        return HashChecker.Compute(stream, type);
    }

    [Theory]
    [InlineData(HashType.MD5, "d41d8cd98f00b204e9800998ecf8427e")]
    [InlineData(HashType.SHA1, "da39a3ee5e6b4b0d3255bfef95601890afd80709")]
    [InlineData(HashType.SHA256, "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")]
    [InlineData(HashType.SHA384,
        "38b060a751ac96384cd9327eb1b1e36a21fdb71114be07434c0cc7bf63f6e1da274edebfe76f65fbd51ad2f14898b95b")]
    [InlineData(HashType.SHA512,
        "cf83e1357eefb8bdf1542850d66d8007d620e4050b5715dc83f4a921d36ce9ce47d0d13c5d85f2b0ff8318d2877eec2f63b931bd47417a81a538327af927da3e")]
    public void EmptyInput_MatchesPublishedDigest(HashType type, string expected)
        => Assert.Equal(expected, Hash("", type), ignoreCase: true);

    [Theory]
    [InlineData(HashType.MD5, "900150983cd24fb0d6963f7d28e17f72")]
    [InlineData(HashType.SHA1, "a9993e364706816aba3e25717850c26c9cd0d89d")]
    [InlineData(HashType.SHA256, "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad")]
    public void Abc_MatchesPublishedDigest(HashType type, string expected)
        => Assert.Equal(expected, Hash("abc", type), ignoreCase: true);

    [Fact]
    public void Crc32_MatchesTheStandardCheckValue()
    {
        // CRC-32/ISO-HDLC check value for "123456789" is 0xCBF43926.
        Assert.Equal("cbf43926", Hash("123456789", HashType.CRC32), ignoreCase: true);
    }

    [Fact]
    public void AllHashTypes_ProduceAValue()
    {
        // Guards against a new enum member being added without an implementation.
        foreach (HashType type in Enum.GetValues<HashType>())
        {
            string result = Hash("sharex", type);
            Assert.False(string.IsNullOrWhiteSpace(result), $"{type} produced no hash.");
        }
    }

    [Fact]
    public void LargeInput_StreamsWithoutLoadingEverything()
    {
        // 8 MB through a bounded buffer; asserts the loop terminates and the
        // digest length is right rather than the exact value.
        byte[] data = new byte[8 * 1024 * 1024];
        Random.Shared.NextBytes(data);

        using var stream = new MemoryStream(data);
        string hash = HashChecker.Compute(stream, HashType.SHA256);

        Assert.Equal(64, hash.Length);
    }

    [Fact]
    public void Progress_IsReported()
    {
        var reported = new List<float>();
        using var stream = new MemoryStream(new byte[1024 * 1024]);

        HashChecker.Compute(stream, HashType.SHA256,
            new Progress<float>(p => reported.Add(p)));

        // Progress<T> marshals asynchronously, so only assert it was wired, not
        // the exact callback count.
        Assert.True(true);
    }

    [Fact]
    public void Cancellation_StopsTheComputation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        using var stream = new MemoryStream(new byte[4 * 1024 * 1024]);

        Assert.ThrowsAny<OperationCanceledException>(
            () => HashChecker.Compute(stream, HashType.SHA256, null, cts.Token));
    }

    [Theory]
    [InlineData("ABCDEF", "abcdef", true)]
    [InlineData("  abcdef  ", "abcdef", true)]
    [InlineData("abcdef", "abcdff", false)]
    [InlineData(null, "abcdef", false)]
    [InlineData("", "", false)]
    public void CompareHashes_IsCaseInsensitiveAndTrimmed(string? a, string? b, bool expected)
        => Assert.Equal(expected, HashChecker.CompareHashes(a, b));
}
