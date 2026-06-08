using System.Net.Http;
using System.Net.Http.Json;
using System.Windows;
using System.Windows.Input;
using DdosDefendSystem.Shared.Models;

namespace DdosDefendSystem.AdminPanel;

public partial class LoginWindow : Window
{
    private readonly HttpClient _httpClient = new();

    public LoginWindow() 
    {
        InitializeComponent();
        _httpClient = AppConfig.CreateCoordinatorClient();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        LoginButton.Content = "[ CONNECTING... ]";
        LoginButton.IsEnabled = false;
        ErrorText.Visibility = Visibility.Hidden;

        try
        {
            var loginData = new LoginRequest
            {
                Username = UsernameBox.Text,
                Password = PasswordBox.Password
            };

            var response = await _httpClient.PostAsJsonAsync("/api/auth/login", loginData);

            if (response.IsSuccessStatusCode)
            {
                MainWindow mainWindow = new MainWindow();
                mainWindow.Show();
                this.Close();
            }
        }
        catch (Exception ex)
        {
            ErrorText.Text = $"CONNECTION_ERROR: {ex.Message.ToUpper()}_";
            ErrorText.Visibility = Visibility.Visible;
        }
        finally
        {
            LoginButton.Content = "[ LOGIN ]";
            LoginButton.IsEnabled = true;
        }
    }
}