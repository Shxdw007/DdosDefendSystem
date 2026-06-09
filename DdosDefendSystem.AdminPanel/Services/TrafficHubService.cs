using DdosDefendSystem.Shared.Models;
using Microsoft.AspNetCore.SignalR.Client;

namespace DdosDefendSystem.AdminPanel.Services;

public class TrafficHubService : IAsyncDisposable
{
    private HubConnection? _hubConnection;

    public event Action<IReadOnlyList<ActiveIpTraffic>>? TrafficReceived;

    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

    public async Task StartAsync()
    {
        if (_hubConnection != null)
            return;

        _hubConnection = new HubConnectionBuilder()
            .WithUrl($"{AppConfig.CoordinatorBaseUrl}/hubs/traffic")
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<List<ActiveIpTraffic>>("ReceiveTraffic", traffic =>
        {
            TrafficReceived?.Invoke(traffic);
        });

        await _hubConnection.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
            _hubConnection = null;
        }
    }
}
