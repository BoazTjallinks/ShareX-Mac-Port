using System.Diagnostics;

namespace ShareX.Mac.App.Support;

/// <summary>
/// Whether this process is running from a real .app bundle. It matters because
/// macOS attaches the Screen Recording (TCC) grant to a bundle identity: a
/// `dotnet run` process cannot hold that permission, so a capture failure there
/// is expected rather than a bug (PROJECT-SPEC.md section 12).
/// </summary>
public static class BundleInfo
{
    public static bool IsBundled { get; } = DetectBundle(out string? path) && path is not null;

    public static string? BundlePath { get; } = DetectBundle(out string? path) ? path : null;

    private static bool DetectBundle(out string? bundlePath)
    {
        bundlePath = null;

        string? executable = Environment.ProcessPath;
        if (string.IsNullOrEmpty(executable))
        {
            return false;
        }

        // .../ShareX-Mac.app/Contents/MacOS/<executable>
        DirectoryInfo? macOsDir = new FileInfo(executable).Directory;
        if (macOsDir?.Name != "MacOS")
        {
            return false;
        }

        DirectoryInfo? contents = macOsDir.Parent;
        if (contents?.Name != "Contents")
        {
            return false;
        }

        DirectoryInfo? app = contents.Parent;
        if (app is null || !app.Name.EndsWith(".app", StringComparison.Ordinal))
        {
            return false;
        }

        bundlePath = app.FullName;
        return true;
    }

    /// <summary>
    /// Opens a System Settings pane. Used to send the user to the Screen Recording
    /// privacy pane when macOS has already stored a denial; the app never modifies
    /// TCC itself.
    /// </summary>
    public static void OpenSystemSettings(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "/usr/bin/open",
                ArgumentList = { url },
                UseShellExecute = false
            });
        }
        catch (Exception)
        {
            // Opening a settings pane is a convenience; failing to do so must not
            // take down the window. The caller already shows the manual path.
        }
    }
}
