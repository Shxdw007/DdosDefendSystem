using DdosDefendSystem.Coordinator.Data;
using DdosDefendSystem.Shared.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace DdosDefendSystem.Coordinator.Services;

public class DdosAnalyzer
{
    // Rule 1: частые запросы к /api/login и /api/search
    private readonly ConcurrentDictionary<string, List<DateTime>> _requestTracker = new();

    // Rule 2: медленные запросы (Slowloris)
    private readonly ConcurrentDictionary<string, List<DateTime>> _slowRequestTracker = new();

    // Rule 3: распределённая атака на /payment из одной подсети
    private readonly ConcurrentDictionary<string, HashSet<string>> _subnetTracker = new();

    // Rule 4: универсальный HTTP-флуд по любому URI
    private readonly ConcurrentDictionary<string, List<DateTime>> _globalRateTracker = new();

    public ConcurrentDictionary<string, BannedIpInfo> BannedIps { get; } = new();

    private readonly ILogger<DdosAnalyzer> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ConcurrentDictionary<string, byte> _whitelist = new();

    public DdosAnalyzer(ILogger<DdosAnalyzer> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    // ──────────────────────────────────────────────────────────────
    //  Whitelist
    // ──────────────────────────────────────────────────────────────

    public bool IsWhitelisted(string ip) => _whitelist.ContainsKey(ip);
    public void AddToWhitelist(string ip) => _whitelist[ip] = 0;
    public void RemoveFromWhitelist(string ip) => _whitelist.TryRemove(ip, out _);

    // ──────────────────────────────────────────────────────────────
    //  Загрузка состояния из БД при старте
    // ──────────────────────────────────────────────────────────────

    public async Task LoadStateFromDatabaseAsync(AppDbContext db)
    {
        var activeBans = await db.BannedIps
            .AsNoTracking()
            .Where(b => b.UnblockedAt == null && b.ExpiresAt > DateTime.UtcNow)
            .ToListAsync();

        BannedIps.Clear();
        foreach (var ban in activeBans)
            BannedIps[ban.IpAddress] = ban;

        var whitelist = await db.WhitelistIps.AsNoTracking().ToListAsync();
        _whitelist.Clear();
        foreach (var entry in whitelist)
            _whitelist[entry.IpAddress] = 0;

        _logger.LogInformation("[Analyzer] Загружено: {Bans} активных банов, {Wl} IP в белом списке",
            activeBans.Count, whitelist.Count);
    }

    // ──────────────────────────────────────────────────────────────
    //  Точка входа
    // ──────────────────────────────────────────────────────────────

    public void Analyze(List<RequestLog> logs)
    {
        var now = DateTime.UtcNow;

        foreach (var log in logs)
        {
            ApplyRule1(log, now);
            ApplyRule2(log, now);
            ApplyRule3(log);
            ApplyRule4(log, now);
        }
    }

    // ──────────────────────────────────────────────────────────────
    //  Правило 1: брутфорс /api/login или /api/search
    //  > 10 запросов за 5 секунд → бан на 5 минут
    // ──────────────────────────────────────────────────────────────

    private void ApplyRule1(RequestLog log, DateTime now)
    {
        if (log.Uri != "/api/login" && log.Uri != "/api/search")
            return;

        if (BannedIps.ContainsKey(log.IpAddress))
            return;

        var times = _requestTracker.GetOrAdd(log.IpAddress, _ => new List<DateTime>());

        lock (times)
        {
            times.Add(log.Timestamp);
            times.RemoveAll(t => (now - t).TotalSeconds > 5);

            if (times.Count > 10)
            {
                TryBan(
                    log.IpAddress,
                    $"Правило 1: {times.Count} запросов за 5 сек к {log.Uri}",
                    TimeSpan.FromMinutes(5));
            }
        }
    }

    // ──────────────────────────────────────────────────────────────
    //  Правило 2: DDoS на истощение (Slowloris)
    //  > 30 запросов с ResponseTime > 2.0s за 1 минуту → бан на 15 минут
    // ──────────────────────────────────────────────────────────────

    private void ApplyRule2(RequestLog log, DateTime now)
    {
        if (log.ResponseTime <= 2.0)
            return;

        if (BannedIps.ContainsKey(log.IpAddress))
            return;

        var slowTimes = _slowRequestTracker.GetOrAdd(log.IpAddress, _ => new List<DateTime>());

        lock (slowTimes)
        {
            slowTimes.Add(log.Timestamp);
            slowTimes.RemoveAll(t => (now - t).TotalMinutes > 1);

            if (slowTimes.Count > 30)
            {
                TryBan(
                    log.IpAddress,
                    $"Правило 2: DDoS на истощение (Slowloris) — {slowTimes.Count} медленных запросов за 1 мин",
                    TimeSpan.FromMinutes(15));
            }
        }
    }

    // ──────────────────────────────────────────────────────────────
    //  Правило 3: распределённая атака из одной /24 подсети
    //  >= 5 уникальных IP делают POST /payment → бан подсети на 10 минут
    // ──────────────────────────────────────────────────────────────

    private void ApplyRule3(RequestLog log)
    {
        if (log.HttpMethod != "POST" || log.Uri != "/payment")
            return;

        var subnet = GetSubnet24(log.IpAddress);

        if (BannedIps.ContainsKey(subnet))
            return;

        var uniqueIps = _subnetTracker.GetOrAdd(subnet, _ => new HashSet<string>());

        lock (uniqueIps)
        {
            uniqueIps.Add(log.IpAddress);

            if (uniqueIps.Count >= 5)
            {
                TryBan(
                    subnet,
                    $"Правило 3: {uniqueIps.Count} уникальных IP из подсети {subnet} атакуют /payment",
                    TimeSpan.FromMinutes(10));
            }
        }
    }

    // ──────────────────────────────────────────────────────────────
    //  Правило 4: универсальный HTTP Rate-Limit (L7 Flood)
    //  > 100 запросов за 10 секунд с одного IP, любой URI → бан на 10 минут
    // ──────────────────────────────────────────────────────────────

    private void ApplyRule4(RequestLog log, DateTime now)
    {
        if (BannedIps.ContainsKey(log.IpAddress))
            return;

        var times = _globalRateTracker.GetOrAdd(log.IpAddress, _ => new List<DateTime>());

        lock (times)
        {
            times.Add(log.Timestamp);
            times.RemoveAll(t => (now - t).TotalSeconds > 10);

            if (times.Count > 100)
            {
                TryBan(
                    log.IpAddress,
                    $"Правило 4: HTTP-флуд — {times.Count} запросов за 10 сек к {log.Uri}",
                    TimeSpan.FromMinutes(10));
            }
        }
    }

    // ──────────────────────────────────────────────────────────────
    //  Внутренние методы
    // ──────────────────────────────────────────────────────────────

    private void TryBan(string target, string reason, TimeSpan duration)
    {
        if (!target.Contains('/') && IsWhitelisted(target))
        {
            _logger.LogInformation("[WHITELIST] IP {Target} пропущен, бан отменён: {Reason}", target, reason);
            return;
        }

        var banInfo = new BannedIpInfo
        {
            IpAddress = target,
            Reason = reason,
            BlockedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(duration)
        };

        // TryAdd — атомарный gate-keeper: только первый поток заходит, остальные игнорируются
        if (BannedIps.TryAdd(target, banInfo))
        {
            _logger.LogWarning(
                "[DDoS DETECTED] {Target} → BLACKLIST на {Minutes} мин. Причина: {Reason}",
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
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // IpAddress — первичный ключ (настроен в AppDbContext.OnModelCreating)
            var existing = await db.BannedIps.FindAsync(banInfo.IpAddress);

            if (existing == null)
            {
                db.BannedIps.Add(banInfo);
            }
            else
            {
                existing.Reason = banInfo.Reason;
                existing.BlockedAt = banInfo.BlockedAt;
                existing.ExpiresAt = banInfo.ExpiresAt;
                existing.UnblockedAt = null;
                existing.UnblockedBy = null;
            }

            await db.SaveChangesAsync();
            _logger.LogInformation("[DB] Бан IP {Ip} сохранён в PostgreSQL до {Expires:u}",
                banInfo.IpAddress, banInfo.ExpiresAt);
        }
        catch (Exception ex)
        {
            _logger.LogError("[DB] Ошибка сохранения бана: {Message}",
                ex.InnerException?.Message ?? ex.Message);
        }
    }
}
