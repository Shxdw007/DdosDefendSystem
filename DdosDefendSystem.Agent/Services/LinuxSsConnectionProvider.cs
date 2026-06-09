using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;

namespace DdosDefendSystem.Agent.Services;

public partial class LinuxSsConnectionProvider : IConnectionSnapshotProvider
{
    private readonly ILogger<LinuxSsConnectionProvider> _logger;

    public LinuxSsConnectionProvider(ILogger<LinuxSsConnectionProvider> logger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyDictionary<string, int>> GetConnectionCountsAsync(CancellationToken cancellationToken = default)
    {
        var output = await RunSsPipelineAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(output))
            return new Dictionary<string, int>();

        var counts = new Dictionary<string, int>();

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var match = ConnectionCountLine().Match(line);
            if (!match.Success)
                continue;

            if (!int.TryParse(match.Groups[1].Value, out var count) || count <= 0)
                continue;

            var ip = NormalizeIp(match.Groups[2].Value);
            if (ip == null)
                continue;

            counts[ip] = count;
        }

        return counts;
    }

    private async Task<string> RunSsPipelineAsync(CancellationToken cancellationToken)
    {
        var command =
            "ss -ntau | awk '{print $5}' | cut -d: -f1 | sed 's/[][]//g' | grep -E '^[0-9]+\\.' | sort | uniq -c | sort -nr";

        var startInfo = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = $"-c \"{command}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            _logger.LogError("[L4] Не удалось запустить ss pipeline");
            return string.Empty;
        }

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var error = await errorTask;
        if (process.ExitCode != 0)
        {
            _logger.LogWarning("[L4] ss pipeline завершился с кодом {ExitCode}: {Error}", process.ExitCode, error.Trim());
            return string.Empty;
        }

        return await outputTask;
    }

    private static string? NormalizeIp(string rawIp)
    {
        if (string.IsNullOrWhiteSpace(rawIp) || rawIp == "*")
            return null;

        if (!IPAddress.TryParse(rawIp, out var address))
            return null;

        if (IPAddress.IsLoopback(address))
            return null;

        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
            return null;

        return address.ToString();
    }

    [GeneratedRegex(@"^\s*(\d+)\s+(\S+)\s*$", RegexOptions.Compiled)]
    private static partial Regex ConnectionCountLine();
}
