using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DdosDefendSystem.AdminPanel.Models;
using DdosDefendSystem.AdminPanel.Services;
using DdosDefendSystem.Shared.Models;

namespace DdosDefendSystem.AdminPanel.ViewModels;

public partial class BlacklistViewModel : ViewModelBase, IDisposable
{
    private readonly CoordinatorApiService _api;
    private readonly SessionService _session;
    private readonly DispatcherTimer _timer = new();

    public ObservableCollection<BannedIpUiModel> BannedIps { get; } = new();

    [ObservableProperty]
    private BannedIpUiModel? _selectedBan;

    public BlacklistViewModel(CoordinatorApiService api, SessionService session)
    {
        _api = api;
        _session = session;
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += async (_, _) =>
        {
            await RefreshAsync();
            foreach (var ban in BannedIps)
                ban.UpdateCountdown();
        };
    }

    public void Start() => _timer.Start();

    public void Stop() => _timer.Stop();

    private async Task RefreshAsync()
    {
        try
        {
            var serverBans = await _api.GetBlacklistAsync();
            var currentIps = serverBans.Select(b => b.IpAddress).ToList();

            for (int i = BannedIps.Count - 1; i >= 0; i--)
            {
                if (!currentIps.Contains(BannedIps[i].IpAddress))
                    BannedIps.RemoveAt(i);
            }

            foreach (var sb in serverBans)
            {
                var existing = BannedIps.FirstOrDefault(b => b.IpAddress == sb.IpAddress);
                if (existing == null)
                    BannedIps.Add(new BannedIpUiModel(sb));
                else
                    existing.ExpiresAt = sb.ExpiresAt;
            }
        }
        catch
        {
            // ignore network errors
        }
    }

    [RelayCommand]
    private async Task PermanentBanAsync()
    {
        if (SelectedBan == null)
            return;

        try
        {
            SelectedBan.ExpiresAt = DateTime.UtcNow.AddYears(99);

            var response = await _api.UpdateBlacklistAsync(new BannedIpInfo
            {
                IpAddress = SelectedBan.IpAddress,
                Reason = "Ручной перманентный бан администратора",
                BlockedAt = SelectedBan.BlockedAt,
                ExpiresAt = SelectedBan.ExpiresAt
            });

            if (response.IsSuccessStatusCode)
            {
                SelectedBan.UpdateCountdown();
                MessageBox.Show($"IP {SelectedBan.IpAddress} переведён в режим вечной блокировки.", "Брандмауэр");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Ошибка");
        }
    }

    [RelayCommand]
    private async Task UnbanAsync()
    {
        if (SelectedBan == null || string.IsNullOrWhiteSpace(_session.Username))
            return;

        try
        {
            var response = await _api.UnbanAsync(SelectedBan.IpAddress, _session.Username);
            if (response.IsSuccessStatusCode)
                BannedIps.Remove(SelectedBan);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Ошибка");
        }
    }

    public void Dispose() => _timer.Stop();
}
