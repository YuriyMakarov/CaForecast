using System.Windows;
using CaForecast.Data.Services;
using CaForecast.WpfApp.ViewModels;

namespace CaForecast.WpfApp;

public partial class ManagerWindow : Window
{
    public ManagerWindow(AuthenticatedUser user)
    {
        InitializeComponent();
        DataContext = new RoleWorkspaceViewModel(user);
    }
}
