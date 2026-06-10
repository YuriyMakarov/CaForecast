using CaForecast.Data.Services;

namespace CaForecast.WpfApp.ViewModels;

public sealed class RoleWorkspaceViewModel(AuthenticatedUser user) : ViewModelBase
{
    public AuthenticatedUser User { get; } = user;

    public string FullName => User.FullName;

    public string RoleName => User.RoleName;
}
