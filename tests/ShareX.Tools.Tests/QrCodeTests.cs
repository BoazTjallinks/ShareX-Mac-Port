using ShareX.Tools.QrCode;
using Xunit;

namespace ShareX.Tools.Tests;

public sealed class QrCodeTests
{
    [Theory]
    [InlineData("https://example.com/a/b?c=d")]
    [InlineData("plain text")]
    [InlineData("unicode: naïve café — ✓ 日本語")]
    [InlineData("1234567890")]
    public void EncodeThenDecode_RoundTrips(string payload)
    {
        RawBitmap bitmap = QrCodeService.Encode(payload, 256, 256);
        IReadOnlyList<BarcodeResult> decoded = QrCodeService.Decode(bitmap);

        BarcodeResult result = Assert.Single(decoded);
        Assert.Equal(payload, result.Text);
    }

    [Fact]
    public void Encode_HonoursTheRequestedSize()
    {
        RawBitmap bitmap = QrCodeService.Encode("size test", 320, 320);

        Assert.Equal(320, bitmap.Width);
        Assert.Equal(320, bitmap.Height);
        // The pixel buffer must actually match the declared dimensions.
        Assert.Equal(320 * 320 * 4, bitmap.Bgra8888.Length);
    }

    [Fact]
    public void Decode_OfNoise_FindsNothing()
    {
        // A decoder that "finds" a barcode in random noise would make the tool
        // untrustworthy, so the negative case is asserted explicitly.
        var noise = new byte[64 * 64 * 4];
        Random.Shared.NextBytes(noise);

        IReadOnlyList<BarcodeResult> decoded =
            QrCodeService.Decode(new RawBitmap(64, 64, noise));

        Assert.Empty(decoded);
    }

    [Fact]
    public void RawBitmap_RejectsAMismatchedBufferLength()
    {
        // The invariant that made an explicit constructor necessary.
        Assert.Throws<ArgumentException>(() => new RawBitmap(10, 10, new byte[10]));
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 0)]
    [InlineData(-1, 10)]
    public void RawBitmap_RejectsNonPositiveDimensions(int width, int height)
        => Assert.Throws<ArgumentOutOfRangeException>(
            () => new RawBitmap(width, height, new byte[Math.Max(1, width * height * 4)]));

    [Fact]
    public void EncodedQr_IsNotBlank()
    {
        // A blank bitmap would still "round trip" if the decoder were lenient, so
        // assert the image actually contains both dark and light pixels.
        RawBitmap bitmap = QrCodeService.Encode("contrast", 128, 128);

        bool hasDark = false;
        bool hasLight = false;
        for (int i = 0; i < bitmap.Bgra8888.Length; i += 4)
        {
            if (bitmap.Bgra8888[i] < 64) hasDark = true;
            if (bitmap.Bgra8888[i] > 192) hasLight = true;
            if (hasDark && hasLight) break;
        }

        Assert.True(hasDark, "encoded QR has no dark modules");
        Assert.True(hasLight, "encoded QR has no light modules");
    }
}
