namespace DdosDefendSystem.Shared;

public static class LogFilePaths
{
    public static string AgentAccessLog
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var agentDir = Path.Combine(dir.FullName, "DdosDefendSystem.Agent");
                if (Directory.Exists(agentDir))
                    return Path.Combine(agentDir, "test_access.log");

                dir = dir.Parent;
            }

            return Path.Combine(AppContext.BaseDirectory, "test_access.log");
        }
    }
}
