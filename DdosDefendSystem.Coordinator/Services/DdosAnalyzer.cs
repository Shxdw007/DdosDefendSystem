using System.Collections.Concurrent;
using DdosDefendSystem.Shared.Models;
using DdosDefendSystem.Coordinator.Data; 

namespace DdosDefendSystem.Coordinator.Services;

public class DdosAnalyzer
{
    private readonly ConcurrentDictionary<string, List<DateTime>> _requestTracker = new();
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
            if (log.Uri == "/api/login" || log.Uri == "/api/search")
            {
                var times = _requestTracker.GetOrAdd(log.IpAddress, _ => new List<DateTime>());

                lock (times)
                {
                    times.Add(now);
                    times.RemoveAll(t => (now - t).TotalSeconds > 5);

                    if (times.Count > 10 && !BannedIps.ContainsKey(log.IpAddress))
                    {
                        var banInfo = new BannedIpInfo
                        {
                            IpAddress = log.IpAddress,
                            Reason = $"Правило 1: {times.Count} запросов за 5 сек к {log.Uri}",
                            BlockedAt = DateTime.UtcNow,
                            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
                        };

                        if (BannedIps.TryAdd(log.IpAddress, banInfo))
                        {
                            _logger.LogWarning("[DDoS DETECTED] IP {Ip} отправлен в ЧЕРНЫЙ СПИСОК на 5 минут!", log.IpAddress);

                            Task.Run(() => SaveBanToDatabase(banInfo));
                        }
                    }
                }
            }
        }
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