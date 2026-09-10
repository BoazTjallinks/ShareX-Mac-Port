namespace ShareX.Imaging;

/// <summary>
/// NOT an upstream type. Upstream's <see cref="EImageFormat"/> (PNG/JPEG/GIF/BMP/TIFF) is a
/// serialized settings/history contract and is ported verbatim elsewhere in this namespace.
/// This separate enum is the encode target list this library's codec layer actually implements:
/// PNG, JPEG, WebP and BMP, per the ShareX.Imaging task brief. GIF and TIFF are intentionally not
/// members here; see PORTING-NOTES.md for why they are not implemented as encode targets.
/// </summary>
public enum ImageCodecFormat
{
    Png,
    Jpeg,
    WebP,
    Bmp
}
