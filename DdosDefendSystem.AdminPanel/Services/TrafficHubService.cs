using DdosDefendSystem.Shared.Models;
using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DdosDefendSystem.AdminPanel.Services;

public class TrafficHubService : IAsyncDisposable
{
    private HubConnection? _hubConnection;
    private bool _isDisposed;

    public event Action<IReadOnlyList<ActiveIpTraffic>>? TrafficReceived;

    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

    public async Task StartAsync()
    {
        if (_hubConnection != null || _isDisposed)
            return;

        _hubConnection = new HubConnectionBuilder()
            .WithUrl($"{AppConfig.CoordinatorBaseUrl}/hubs/traffic")
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<List<ActiveIpTraffic>>("ReceiveTraffic", traffic =>
        {
            TrafficReceived?.Invoke(traffic);
        });

        // Бесконечный цикл повторного подключения при запуске
        while (!_isDisposed)
        {
            try
            {
                await _hubConnection.StartAsync();
                Console.WriteLine("SignalR Hub подключен.");
                return; // Успешно подключились, выходим из цикла
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Не удалось подключиться к SignalR Hub: {ex.Message}. Повторная попытка через 5 секунд.");
                await Task.Delay(5000);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _isDisposed = true;
        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
            _hubConnection = null;
        }
    }
}
