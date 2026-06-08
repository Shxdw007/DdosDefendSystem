namespace DdosDefendSystem.Shared;

public static class ServiceEndpoints
{
    public const string DefaultCoordinatorUrl = "http://localhost:5132";

    public static string ResolveCoordinatorUrl(string? configuredUrl = null)
    {
        var envUrl = Environment.GetEnvironmentVariable("COORDINATOR_URL");
        if (!string.IsNullOrWhiteSpace(envUrl))
            return envUrl.TrimEnd('/');

        if (!string.IsNullOrWhiteSpace(configuredUrl))
            return configuredUrl.TrimEnd('/');

        return DefaultCoordinatorUrl;
    }
}
