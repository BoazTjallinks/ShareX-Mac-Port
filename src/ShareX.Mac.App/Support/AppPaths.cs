namespace ShareX.Mac.App.Support;

/// <summary>
/// Application data locations (PROJECT-SPEC.md section 10). Application support
/// under ~/Library/Application Support/ShareX-Mac, expendable cache under
/// ~/Library/Caches/ShareX-Mac, and a user-selectable screenshots directory that
/// defaults to ~/Pictures/ShareX.
/// </summary>
public static class AppPaths
{
    private static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public static string ApplicationSupport =>
        Path.Combine(Home, "Library", "Application Support", "ShareX-Mac");

    public static string Cache =>
        Path.Combine(Home, "Library", "Caches", "ShareX-Mac");

    public static string DefaultScreenshotsFolder =>
        Path.Combine(Home, "Pictures", "ShareX");

    /// <summary>
    /// Mirrors upstream's dated screenshot folder layout
    /// (TaskHelpers.GetScreenshotsFolder): a yyyy-MM subfolder under the root.
    /// </summary>
    public static string ScreenshotsFolderFor(DateTime timestamp) =>
        Path.Combine(DefaultScreenshotsFolder, timestamp.ToString("yyyy-MM"));

    public static string EnsureDirectory(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }
}
