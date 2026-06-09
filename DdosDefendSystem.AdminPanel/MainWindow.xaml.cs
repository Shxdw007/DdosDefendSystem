using System.Windows;
using System.Windows.Input;
using DdosDefendSystem.AdminPanel.Services;
using DdosDefendSystem.AdminPanel.ViewModels;

namespace DdosDefendSystem.AdminPanel;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = AppServices.CreateMainViewModel();
        DataContext = _viewModel;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e) =>
        await _viewModel.LoadedCommand.ExecuteAsync(null);

    private async void MainWindow_Unloaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.UnloadedCommand.ExecuteAsync(null);
        await _viewModel.DisposeAsync();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();
    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void CloseButton_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
}
