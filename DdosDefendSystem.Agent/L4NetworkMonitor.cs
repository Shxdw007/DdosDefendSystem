using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using DdosDefendSystem.Agent.Services;
using DdosDefendSystem.Shared.Models;

namespace DdosDefendSystem.Agent;

public class L4NetworkMonitor : BackgroundService
{
    private const int FloodThreshold = 50;
    private const int BanDurationMinutes = 30;
    private const int WhitelistRefreshSeconds = 30;

    private readonly ILogger<L4NetworkMonitor> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IBlocker _blocker;
    private readonly IConnectionSnapshotProvider _connectionProvider;

    private readonly ConcurrentDictionary<string, byte> _l4BlockedIps = new();
    private readonly HashSet<string> _whitelist = new();
    private DateTime _lastWhitelistSync = DateTime.MinValue;
    private readonly object _whitelistLock = new();

    public L4NetworkMonitor(
        ILogger<L4NetworkMonitor> logger,
        IHttpClientFactory httpClientFactory,
        IBlocker blocker,
        IConnectionSnapshotProvider connectionProvider)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _blocker = blocker;
        _connectionProvider = connectionProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            _logger.LogInformation("L4NetworkMonitor отключён: требуется Ubuntu/Linux.");
            return;
        }

        _logger.LogInformation("L4NetworkMonitor запущен. Порог: {Threshold} соединений, опрос каждую секунду.", FloodThreshold);

        var httpClient = _httpClientFactory.CreateClient("CoordinatorClient");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshWhitelistIfNeededAsync(httpClient, stoppingToken);

                var connectionCounts = await _connectionProvider.GetConnectionCountsAsync(stoppingToken);
                await SendTrafficSummaryAsync(httpClient, connectionCounts, stoppingToken);
                await EnforceFloodProtectionAsync(httpClient, connectionCounts, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[L4] Ошибка цикла мониторинга");
            }

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }

    private async Task RefreshWhitelistIfNeededAsync(HttpClient httpClient, CancellationToken stoppingToken)
    {
        if ((DateTime.UtcNow - _lastWhitelistSync).TotalSeconds < WhitelistRefreshSeconds)
            return;

        try
        {
            var whitelist = await httpClient.GetFromJsonAsync<List<WhitelistIp>>("/api/whitelist", stoppingToken);
            if (whitelist == null)
                return;

            lock (_whitelistLock)
            {
                _whitelist.Clear();
                foreach (var entry in whitelist)
                    _whitelist.Add(entry.IpAddress);
            }

            _lastWhitelistSync = DateTime.UtcNow;
            _logger.LogDebug("[L4] Whitelist обновлён: {Count} IP", whitelist.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[L4] Не удалось обновить whitelist");
        }
    }

    private bool IsWhitelisted(string ip)
    {
        lock (_whitelistLock)
            return _whitelist.Contains(ip);
    }

    private async Task SendTrafficSummaryAsync(
        HttpClient httpClient,
        IReadOnlyDictionary<string, int> connectionCounts,
        CancellationToken stoppingToken)
    {
        if (connectionCounts.Count == 0)
            return;

        var traffic = connectionCounts
            .OrderByDescending(kv => kv.Value)
            .Take(50)
            .Select(kv => new ActiveIpTraffic
            {
                IpAddress = kv.Key,
                ConnectionCount = kv.Value
            })
            .ToList();

        try
        {
            var response = await httpClient.PostAsJsonAsync("/api/traffic/update", traffic, stoppingToken);
            if (!response.IsSuccessStatusCode)
                _logger.LogWarning("[L4] Coordinator отклонил traffic/update: {Status}", response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[L4] Не удалось отправить сводку трафика");
        }
    }

    private async Task EnforceFloodProtectionAsync(
        HttpClient httpClient,
        IReadOnlyDictionary<string, int> connectionCounts,
        CancellationToken stoppingToken)
    {
        foreach (var (ip, count) in connectionCounts)
        {
            if (count < FloodThreshold)
                continue;

            if (IsWhitelisted(ip))
            {
                _logger.LogDebug("[L4] IP {Ip} пропущен (whitelist), соединений: {Count}", ip, count);
                continue;
            }

            if (!_l4BlockedIps.TryAdd(ip, 0))
                continue;

            _logger.LogWarning("[L4] FLOOD DETECTED: {Ip} — {Count} соединений. Блокировка iptables...", ip, count);

            await _blocker.BlockIpAsync(ip, TimeSpan.FromMinutes(BanDurationMinutes));
            await ReportBanToCoordinatorAsync(httpClient, ip, stoppingToken);
        }
    }

    private async Task ReportBanToCoordinatorAsync(HttpClient httpClient, string ip, CancellationToken stoppingToken)
    {
        var request = new ManualBanRequest
        {
            IpAddress = ip,
            Reason = "Auto L4 Flood TCP/UDP",
            Username = "Agent",
            DurationMinutes = BanDurationMinutes
        };

        try
        {
            var response = await httpClient.PostAsJsonAsync("/api/bans/manual", request, stoppingToken);
            if (response.IsSuccessStatusCode)
                _logger.LogInformation("[L4] Бан IP {Ip} записан в Coordinator.", ip);
            else
                _logger.LogWarning("[L4] Coordinator отклонил запись бана {Ip}: {Status}", ip, response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[L4] Не удалось записать бан IP {Ip} в Coordinator", ip);
        }
    }
}
