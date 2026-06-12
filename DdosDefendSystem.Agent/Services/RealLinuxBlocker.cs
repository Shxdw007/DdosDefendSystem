using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace DdosDefendSystem.Agent.Services;

public class RealLinuxBlocker : IBlocker
{
    private readonly ILogger<RealLinuxBlocker> _logger;

    public RealLinuxBlocker(ILogger<RealLinuxBlocker> logger)
    {
        _logger = logger;
    }

    public void BlockTarget(string target)
    {
        RunIptables($"-I INPUT 1 -s {target} -j DROP", target, "блокировка");
    }

    public Task BlockIpAsync(string ipAddress, TimeSpan duration)
    {
        BlockTarget(ipAddress);
        _logger.LogInformation("[FIREWALL] Срок блокировки {Target}: {Minutes} мин.", ipAddress, duration.TotalMinutes);
        return Task.CompletedTask;
    }

    public Task UnblockIpAsync(string ipAddress)
    {
        RunIptables($"-D INPUT -s {ipAddress} -j DROP", ipAddress, "разблокировка");
        return Task.CompletedTask;
    }

    private void RunIptables(string arguments, string target, string action)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "iptables",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                _logger.LogError("[FIREWALL] Не удалось запустить iptables для {Action} {Target}", action, target);
                return;
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode == 0)
            {
                _logger.LogWarning("[FIREWALL] {Action} {Target}. Команда: iptables {Args}", action, target, arguments);

                if (!string.IsNullOrWhiteSpace(output))
                    _logger.LogInformation("[FIREWALL] stdout: {Output}", output.Trim());
            }
            else
            {
                _logger.LogError(
                    "[FIREWALL] iptables завершился с кодом {ExitCode} при {Action} {Target}. stderr: {Error}",
                    process.ExitCode, action, target, error.Trim());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FIREWALL] Ошибка при {Action} {Target}", action, target);
        }
    }
}
