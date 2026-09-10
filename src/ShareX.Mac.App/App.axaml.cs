using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ShareX.Mac.App.ViewModels;
using ShareX.Mac.App.Views;

namespace ShareX.Mac.App;

public partial class App : Application
{
    /// <summary>Set from the command line by <c>--selftest</c>.</summary>
    public static bool SelfTestOnStart { get; set; }

    public static bool SelfTestAttemptsCapture { get; set; } = true;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var viewModel = new MainWindowViewModel();
            desktop.MainWindow = new MainWindow { DataContext = viewModel };

            // Start the native probes only once the loop is running, so a slow or
            // failing bridge call can never block window creation.
            desktop.MainWindow.Opened += async (_, _) =>
            {
                if (!SelfTestOnStart)
                {
                    viewModel.InitializeAsync();
                    return;
                }

                // Inside a real GUI process, so a TCC prompt can actually appear.
                int exitCode = await Support.SelfTest
                    .RunAsync(SelfTestAttemptsCapture)
                    .ConfigureAwait(true);

                desktop.Shutdown(exitCode);
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
