using System.Net.Http;
using Microsoft.Extensions.Configuration;
using DdosDefendSystem.Shared;

namespace DdosDefendSystem.AdminPanel;

public static class AppConfig
{
    public static IConfiguration Configuration { get; } = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddEnvironmentVariables()
        .Build();

    public static string CoordinatorBaseUrl =>
        ServiceEndpoints.ResolveCoordinatorUrl(Configuration["Coordinator:BaseUrl"]);

    public static HttpClient CreateCoordinatorClient() => new()
    {
        BaseAddress = new Uri(CoordinatorBaseUrl),
        Timeout = TimeSpan.FromSeconds(30)
    };
}
