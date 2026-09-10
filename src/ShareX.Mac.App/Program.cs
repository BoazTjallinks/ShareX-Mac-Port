using Avalonia;

namespace ShareX.Mac.App;

internal static class Program
{
    // Avalonia owns the process's main event loop (PROJECT-SPEC.md section 4).
    // The native bridge attaches to it and dispatches AppKit work to the existing
    // main queue; it never starts a second NSApplication.
    [STAThread]
    public static int Main(string[] args)
    {
        // Integration check from inside the real bundle. The stage-0 gate needs
        // evidence that `dotnet run` cannot produce, because macOS binds the
        // Screen Recording grant to a bundle identity.
        //
        // It runs inside the normal Avalonia lifetime rather than standalone: a
        // process with no NSApplication has no window-server connection, and macOS
        // will not display a TCC permission prompt for such a process. Running it
        // headless was why the app could never ask for Screen Recording.
        if (args.Contains("--selftest", StringComparer.Ordinal))
        {
            App.SelfTestOnStart = true;
            App.SelfTestAttemptsCapture = !args.Contains("--no-capture", StringComparer.Ordinal);
        }

        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
