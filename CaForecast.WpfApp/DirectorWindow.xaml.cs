using System.Windows;
using CaForecast.Data.Services;
using CaForecast.WpfApp.ViewModels;

namespace CaForecast.WpfApp;

public partial class DirectorWindow : Window
{
    public DirectorWindow(AuthenticatedUser user)
    {
        InitializeComponent();
        DataContext = new RoleWorkspaceViewModel(user);
    }
}
