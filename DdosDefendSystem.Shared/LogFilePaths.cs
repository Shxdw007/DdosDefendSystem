using System.Runtime.InteropServices;

namespace DdosDefendSystem.Shared;

public static class LogFilePaths
{
    public static string AgentAccessLog
    {
        get
        {
            var envPath = Environment.GetEnvironmentVariable("AGENT_LOG_PATH");
            if (!string.IsNullOrWhiteSpace(envPath))
                return envPath;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return "/var/log/nginx/ddos_access.log";

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DdosDefendSystem",
                "test_access.log");
        }
    }

    public static bool IsSystemManagedLog(string path) =>
        path.StartsWith("/var/log/", StringComparison.Ordinal) ||
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AGENT_LOG_PATH"));
}
