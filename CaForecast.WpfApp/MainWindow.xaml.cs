using System.Windows;

namespace CaForecast.WpfApp;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var viewModel = new MainViewModel();
        DataContext = viewModel;
        Closing += (_, _) => viewModel.CancelBackgroundOperations();
    }
}
