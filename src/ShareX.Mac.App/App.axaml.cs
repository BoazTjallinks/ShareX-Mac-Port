using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ShareX.Mac.App.ViewModels;
using ShareX.Mac.App.Views;

namespace ShareX.Mac.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var viewModel = new MainWindowViewModel();
            desktop.MainWindow = new MainWindow { DataContext = viewModel };

            // Start the native probes only once the loop is running, so a slow or
            // failing bridge call can never block window creation.
            desktop.MainWindow.Opened += (_, _) => viewModel.InitializeAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
