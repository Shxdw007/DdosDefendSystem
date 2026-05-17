using Microsoft.Extensions.Logging;

namespace DdosDefendSystem.Agent.Services;

public class MockLinuxBlocker : IBlocker
{
    private readonly ILogger<MockLinuxBlocker> _logger;

    public MockLinuxBlocker(ILogger<MockLinuxBlocker> logger)
    {
        _logger = logger;
    }

    public Task BlockIpAsync(string ipAddress, TimeSpan duration)
    {
        _logger.LogWarning("[MOCK FIREWALL] БЛОКИРОВКА IP: {Ip}. Команда ОС: iptables -A INPUT -s {Ip} -j DROP. Срок: {Minutes} мин.",
            ipAddress, ipAddress, duration.TotalMinutes);

        return Task.CompletedTask;
    }

    public Task UnblockIpAsync(string ipAddress)
    {
        _logger.LogInformation("[MOCK FIREWALL] РАЗБЛОКИРОВКА IP: {Ip}. Команда ОС: iptables -D INPUT -s {Ip} -j DROP.", ipAddress, ipAddress);

        return Task.CompletedTask;
    }
}