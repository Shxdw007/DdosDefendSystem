using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Json;
using System.Windows;
using System.Windows.Threading;
using DdosDefendSystem.Shared.Models;

namespace DdosDefendSystem.AdminPanel;

public partial class MainWindow : Window
{
    private readonly HttpClient _httpClient = new();
    private readonly DispatcherTimer _updateTimer = new();

    public ObservableCollection<BannedIpUiModel> BannedIpsUi { get; set; } = new();
    public ObservableCollection<RequestLog> RecentTraffic { get; set; } = new();

    public MainWindow()
    {
        InitializeComponent();

        _httpClient = AppConfig.CreateCoordinatorClient();
        BlacklistGrid.ItemsSource = BannedIpsUi;
        LiveTrafficLog.ItemsSource = RecentTraffic;

        _updateTimer.Interval = TimeSpan.FromSeconds(1);
        _updateTimer.Tick += UpdateTimer_Tick;
        _updateTimer.Start();
    }

    private async void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        await RefreshBlacklistAsync();

        await RefreshTrafficAsync();

        foreach (var ban in BannedIpsUi)
        {
            ban.UpdateCountdown();
        }
    }

    private async Task RefreshTrafficAsync()
    {
        try
        {
            var logs = await _httpClient.GetFromJsonAsync<List<RequestLog>>("/api/logs/recent");
            if (logs != null)
            {
                RecentTraffic.Clear();
                foreach (var log in logs) RecentTraffic.Add(log);
            }
        }
        catch { /* Игнорируем сетевые сбои */ }
    }

    private async Task RefreshBlacklistAsync()
    {
        try
        {
            var serverBans = await _httpClient.GetFromJsonAsync<List<BannedIpInfo>>("/api/blacklist");
            if (serverBans != null)
            {
                var currentIps = serverBans.Select(b => b.IpAddress).ToList();

                for (int i = BannedIpsUi.Count - 1; i >= 0; i--)
                {
                    if (!currentIps.Contains(BannedIpsUi[i].IpAddress))
                        BannedIpsUi.RemoveAt(i);
                }

                foreach (var sb in serverBans)
                {
                    var existing = BannedIpsUi.FirstOrDefault(b => b.IpAddress == sb.IpAddress);
                    if (existing == null)
                    {
                        BannedIpsUi.Add(new BannedIpUiModel(sb));
                    }
                    else
                    {
                        existing.ExpiresAt = sb.ExpiresAt;
                    }
                }
            }
        }
        catch {  }
    }

    private async void PermanentBan_Click(object sender, RoutedEventArgs e)
    {
        if (BlacklistGrid.SelectedItem is BannedIpUiModel selectedBan)
        {
            try
            {
                selectedBan.ExpiresAt = DateTime.UtcNow.AddYears(99);

                var response = await _httpClient.PostAsJsonAsync("/api/blacklist/update", new BannedIpInfo
                {
                    IpAddress = selectedBan.IpAddress,
                    Reason = "Ручной перманентный бан администратора",
                    BlockedAt = selectedBan.BlockedAt,
                    ExpiresAt = selectedBan.ExpiresAt
                });

                if (response.IsSuccessStatusCode)
                {
                    selectedBan.UpdateCountdown();
                    MessageBox.Show($"IP {selectedBan.IpAddress} успешно переведен в режим вечной блокировки.", "Брандмауэр");
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }
    }

    private async void Unban_Click(object sender, RoutedEventArgs e)
    {
        if (BlacklistGrid.SelectedItem is BannedIpUiModel selectedBan)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"/api/blacklist/unban?ip={selectedBan.IpAddress}");
                if (response.IsSuccessStatusCode)
                {
                    BannedIpsUi.Remove(selectedBan);
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }
    }

    private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e) => DragMove();
    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void CloseButton_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
}

public class BannedIpUiModel : DependencyObject
{
    public string IpAddress { get; set; }
    public string Reason { get; set; }
    public DateTime BlockedAt { get; set; }
    public DateTime ExpiresAt { get; set; }

    public static readonly DependencyProperty TimeLeftStringProperty =
        DependencyProperty.Register(nameof(TimeLeftString), typeof(string), typeof(BannedIpUiModel), new PropertyMetadata(string.Empty));

    public string TimeLeftString
    {
        get => (string)GetValue(TimeLeftStringProperty);
        set => SetValue(TimeLeftStringProperty, value);
    }

    public BannedIpUiModel(BannedIpInfo info)
    {
        IpAddress = info.IpAddress;
        Reason = info.Reason;
        BlockedAt = info.BlockedAt;
        ExpiresAt = info.ExpiresAt;
        UpdateCountdown();
    }

    public void UpdateCountdown()
    {
        var remaining = ExpiresAt - DateTime.UtcNow;

        if (remaining.TotalDays > 365)
        {
            TimeLeftString = "ПЕРМАНЕНТНО";
        }
        else if (remaining.TotalSeconds <= 0)
        {
            TimeLeftString = "ИСТЕК";
        }
        else
        {
            TimeLeftString = $"{remaining.Minutes:D2}:{remaining.Seconds:D2}";
        }
    }
}