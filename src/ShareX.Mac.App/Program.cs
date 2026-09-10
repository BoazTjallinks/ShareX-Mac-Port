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
        // Headless integration check from inside the real bundle. The stage-0 gate
        // needs evidence that a `dotnet run` cannot produce, because macOS binds
        // the Screen Recording grant to a bundle identity.
        if (args.Contains("--selftest", StringComparer.Ordinal))
        {
            bool attemptCapture = !args.Contains("--no-capture", StringComparer.Ordinal);
            return Support.SelfTest.RunAsync(attemptCapture).GetAwaiter().GetResult();
        }

        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
