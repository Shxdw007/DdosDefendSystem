using System.Net.Http.Json;
using DdosDefendSystem.Agent.Services;
using DdosDefendSystem.Shared.Models;

namespace DdosDefendSystem.Agent;

public class BlacklistSyncWorker : BackgroundService
{
    private readonly ILogger<BlacklistSyncWorker> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IBlocker _blocker;

    private readonly HashSet<string> _locallyBlockedIps = new();

    public BlacklistSyncWorker(ILogger<BlacklistSyncWorker> logger, IHttpClientFactory httpClientFactory, IBlocker blocker)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _blocker = blocker;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Синхронизатор черного списка запущен. Опрос каждые 10 секунд.");
        var httpClient = _httpClientFactory.CreateClient("CoordinatorClient");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var bannedIps = await httpClient.GetFromJsonAsync<List<BannedIpInfo>>("/api/blacklist", stoppingToken);

                if (bannedIps != null)
                {
                    var remoteBannedIps = bannedIps.Select(b => b.IpAddress).ToHashSet();
                    
                    // Удаляем правила для разбаненных IP (или у которых истек срок)
                    var ipsToUnban = _locallyBlockedIps.Except(remoteBannedIps).ToList();
                    foreach (var ip in ipsToUnban)
                    {
                        await _blocker.UnblockIpAsync(ip);
                        _locallyBlockedIps.Remove(ip);
                        _logger.LogInformation("[SYNC] IP {Ip} удален из iptables (нет в черном списке)", ip);
                    }

                    // Добавляем новые правила
                    foreach (var ban in bannedIps)
                    {
                        if (!_locallyBlockedIps.Contains(ban.IpAddress))
                        {
                            var duration = ban.ExpiresAt - DateTime.UtcNow;

                            if (duration.TotalSeconds > 0)
                            {
                                await _blocker.BlockIpAsync(ban.IpAddress, duration);
                                _locallyBlockedIps.Add(ban.IpAddress);
                                _logger.LogInformation("[SYNC] IP {Ip} добавлен в iptables", ban.IpAddress);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[SYNC] Ошибка синхронизации черного списка. Продолжение работы. Сообщение: {Message}", ex.Message);
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
}
