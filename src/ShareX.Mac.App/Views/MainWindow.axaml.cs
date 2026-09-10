using Avalonia.Controls;
using ShareX.Mac.App.ViewModels;

namespace ShareX.Mac.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // The region overlay needs an owner window, and the view model must not
        // hold a hard reference to a view type's instance beyond this accessor.
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.OwnerWindowAccessor = () => this;
            }
        };
    }
}
