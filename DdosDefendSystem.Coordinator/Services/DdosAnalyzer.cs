using System.Collections.Concurrent;
using DdosDefendSystem.Shared.Models;
using DdosDefendSystem.Coordinator.Data;

namespace DdosDefendSystem.Coordinator.Services;

public class DdosAnalyzer
{
    private readonly ConcurrentDictionary<string, List<DateTime>> _requestTracker = new();
    private readonly ConcurrentDictionary<string, List<DateTime>> _slowRequestTracker = new();
    private readonly ConcurrentDictionary<string, HashSet<string>> _subnetTracker = new();
    public ConcurrentDictionary<string, BannedIpInfo> BannedIps { get; } = new();

    private readonly ILogger<DdosAnalyzer> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public DdosAnalyzer(ILogger<DdosAnalyzer> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public void Analyze(List<RequestLog> logs)
    {
        var now = DateTime.UtcNow;

        foreach (var log in logs)
        {
            ApplyRule1(log, now);
            ApplyRule2(log, now);
            ApplyRule3(log);
        }
    }

    private void ApplyRule1(RequestLog log, DateTime now)
    {
        if (log.Uri != "/api/login" && log.Uri != "/api/search")
            return;

        var times = _requestTracker.GetOrAdd(log.IpAddress, _ => new List<DateTime>());

        lock (times)
        {
            times.Add(now);
            times.RemoveAll(t => (now - t).TotalSeconds > 5);

            if (times.Count > 10 && !BannedIps.ContainsKey(log.IpAddress))
            {
                TryBan(
                    log.IpAddress,
                    $"Правило 1: {times.Count} запросов за 5 сек к {log.Uri}",
                    TimeSpan.FromMinutes(5));
            }
        }
    }

    private void ApplyRule2(RequestLog log, DateTime now)
    {
        if (log.ResponseTime <= 2.0)
            return;

        var slowTimes = _slowRequestTracker.GetOrAdd(log.IpAddress, _ => new List<DateTime>());

        lock (slowTimes)
        {
            slowTimes.Add(now);
            slowTimes.RemoveAll(t => (now - t).TotalMinutes > 1);

            if (slowTimes.Count > 30 && !BannedIps.ContainsKey(log.IpAddress))
            {
                TryBan(
                    log.IpAddress,
                    "Правило 2: DDoS на истощение (Slowloris)",
                    TimeSpan.FromMinutes(15));
            }
        }
    }

    private void ApplyRule3(RequestLog log)
    {
        if (log.HttpMethod != "POST" || log.Uri != "/payment")
            return;

        var subnet = GetSubnet24(log.IpAddress);
        var uniqueIps = _subnetTracker.GetOrAdd(subnet, _ => new HashSet<string>());

        lock (uniqueIps)
        {
            uniqueIps.Add(log.IpAddress);

            if (uniqueIps.Count >= 5 && !BannedIps.ContainsKey(subnet))
            {
                TryBan(
                    subnet,
                    $"Правило 3: {uniqueIps.Count} уникальных IP из подсети {subnet}",
                    TimeSpan.FromMinutes(10));
            }
        }
    }

    private void TryBan(string target, string reason, TimeSpan duration)
    {
        var banInfo = new BannedIpInfo
        {
            IpAddress = target,
            Reason = reason,
            BlockedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(duration)
        };

        if (BannedIps.TryAdd(target, banInfo))
        {
            _logger.LogWarning("[DDoS DETECTED] {Target} отправлен в ЧЕРНЫЙ СПИСОК на {Minutes} минут! Причина: {Reason}",
                target, duration.TotalMinutes, reason);

            Task.Run(() => SaveBanToDatabase(banInfo));
        }
    }

    private static string GetSubnet24(string ipAddress)
    {
        var parts = ipAddress.Split('.');
        if (parts.Length != 4)
            return ipAddress;

        return $"{parts[0]}.{parts[1]}.{parts[2]}.0/24";
    }

    private async Task SaveBanToDatabase(BannedIpInfo banInfo)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var existingBan = await dbContext.BannedIps.FindAsync(banInfo.IpAddress);

            if (existingBan == null)
            {
                dbContext.BannedIps.Add(banInfo);
            }
            else
            {
                existingBan.ExpiresAt = banInfo.ExpiresAt;
                existingBan.Reason = banInfo.Reason;
                existingBan.BlockedAt = banInfo.BlockedAt;

                dbContext.BannedIps.Update(existingBan);
            }

            await dbContext.SaveChangesAsync();
            _logger.LogInformation("Запись о бане IP {Ip} успешно забетонирована в PostgreSQL.", banInfo.IpAddress);
        }
        catch (Exception ex)
        {
            _logger.LogError("Ошибка при сохранении в БД: {Message}", ex.InnerException?.Message ?? ex.Message);
        }
    }
}
