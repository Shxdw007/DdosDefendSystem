using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DdosDefendSystem.AdminPanel.Services;
using DdosDefendSystem.Shared.Models;

namespace DdosDefendSystem.AdminPanel.ViewModels;

public partial class WhitelistViewModel : ViewModelBase
{
    private readonly CoordinatorApiService _api;
    private readonly SessionService _session;

    public ObservableCollection<WhitelistIp> Items { get; } = new();

    [ObservableProperty]
    private string _newIpAddress = string.Empty;

    [ObservableProperty]
    private WhitelistIp? _selectedItem;

    public WhitelistViewModel(CoordinatorApiService api, SessionService session)
    {
        _api = api;
        _session = session;
    }

    public async Task LoadAsync() => await RefreshAsync();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            var items = await _api.GetWhitelistAsync();
            Items.Clear();
            foreach (var item in items)
                Items.Add(item);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Ошибка загрузки whitelist");
        }
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        if (string.IsNullOrWhiteSpace(NewIpAddress) || string.IsNullOrWhiteSpace(_session.Username))
            return;

        try
        {
            var response = await _api.AddWhitelistAsync(NewIpAddress.Trim(), _session.Username);
            if (response.IsSuccessStatusCode)
            {
                NewIpAddress = string.Empty;
                await RefreshAsync();
            }
            else
                MessageBox.Show($"Ошибка: {response.StatusCode}", "Whitelist");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Ошибка");
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedItem == null)
            return;

        try
        {
            var response = await _api.DeleteWhitelistAsync(SelectedItem.IpAddress);
            if (response.IsSuccessStatusCode)
                await RefreshAsync();
            else
                MessageBox.Show($"Ошибка: {response.StatusCode}", "Whitelist");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Ошибка");
        }
    }
}
