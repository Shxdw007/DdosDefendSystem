using System.Collections.ObjectModel;
using System.Windows.Threading;
using DdosDefendSystem.AdminPanel.Services;
using DdosDefendSystem.Shared.Models;

namespace DdosDefendSystem.AdminPanel.ViewModels;

public class L7LogsViewModel : ViewModelBase, IDisposable
{
    private readonly CoordinatorApiService _api;
    private readonly DispatcherTimer _timer = new();

    public ObservableCollection<RequestLog> Logs { get; } = new();

    public L7LogsViewModel(CoordinatorApiService api)
    {
        _api = api;
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += async (_, _) => await RefreshAsync();
    }

    public void Start() => _timer.Start();

    public void Stop() => _timer.Stop();

    private async Task RefreshAsync()
    {
        try
        {
            var logs = await _api.GetRecentLogsAsync();
            Logs.Clear();
            foreach (var log in logs)
                Logs.Add(log);
        }
        catch
        {
            // ignore network errors
        }
    }

    public void Dispose() => _timer.Stop();
}
