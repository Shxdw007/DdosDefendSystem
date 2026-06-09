using System.Windows;
using DdosDefendSystem.AdminPanel.Services;

namespace DdosDefendSystem.AdminPanel;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        AppServices.Theme.ApplyTheme(AppTheme.Dark);

        var loginWindow = new LoginWindow
        {
            DataContext = AppServices.CreateLoginViewModel()
        };
        loginWindow.Show();
    }
}
