using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using DdosDefendSystem.AdminPanel.Services;
using DdosDefendSystem.Shared.Models;

namespace DdosDefendSystem.AdminPanel.ViewModels;

public partial class L4TrafficViewModel : ViewModelBase
{
    private readonly CoordinatorApiService _api;
    private readonly SessionService _session;
    private readonly TrafficHubService _hubService;

    public ObservableCollection<ActiveIpTraffic> TrafficItems { get; } = new();

    public L4TrafficViewModel(CoordinatorApiService api, SessionService session, TrafficHubService hubService)
    {
        _api = api;
        _session = session;
        _hubService = hubService;
        _hubService.TrafficReceived += OnTrafficReceived;
    }

    private void OnTrafficReceived(IReadOnlyList<ActiveIpTraffic> traffic)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            TrafficItems.Clear();
            foreach (var item in traffic)
                TrafficItems.Add(item);
        });
    }

    [RelayCommand]
    private async Task ManualBanAsync(ActiveIpTraffic? item)
    {
        if (item == null || string.IsNullOrWhiteSpace(_session.Username))
            return;

        try
        {
            var response = await _api.ManualBanAsync(
                item.IpAddress,
                _session.Username,
                "Manual ban from AdminPanel",
                60);

            if (response.IsSuccessStatusCode)
                MessageBox.Show($"IP {item.IpAddress} заблокирован.", "Брандмауэр");
            else
                MessageBox.Show($"Ошибка бана: {response.StatusCode}", "Брандмауэр");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Ошибка");
        }
    }
}
