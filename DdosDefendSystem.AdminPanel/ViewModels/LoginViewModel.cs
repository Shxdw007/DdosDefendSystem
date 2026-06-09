using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DdosDefendSystem.AdminPanel.Services;
using System.Windows;

namespace DdosDefendSystem.AdminPanel.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    private readonly CoordinatorApiService _api;
    private readonly SessionService _session;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private bool _isBusy;

    public LoginViewModel(CoordinatorApiService api, SessionService session)
    {
        _api = api;
        _session = session;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsBusy)
            return;

        HasError = false;
        ErrorMessage = string.Empty;
        IsBusy = true;

        try
        {
            var (success, role, error) = await _api.LoginAsync(Username, Password);

            if (!success)
            {
                ErrorMessage = error ?? "ACCESS DENIED";
                HasError = true;
                return;
            }

            _session.SetSession(Username, role ?? "Admin");

            try
            {
                await _api.AuditLoginAsync(Username);
            }
            catch
            {
                // audit failure should not block login
            }

            var mainWindow = new MainWindow();
            mainWindow.Show();

            foreach (Window window in Application.Current.Windows)
            {
                if (window is LoginWindow)
                {
                    window.Close();
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"CONNECTION_ERROR: {ex.Message.ToUpperInvariant()}_";
            HasError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
