using DdosDefendSystem.Agent;
using DdosDefendSystem.Agent.Services;
using DdosDefendSystem.Shared;
using System.Runtime.InteropServices;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<NginxLogParser>();

if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    builder.Services.AddSingleton<IBlocker, RealLinuxBlocker>();
else
    builder.Services.AddSingleton<IBlocker, MockLinuxBlocker>();

builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<BlacklistSyncWorker>();

var coordinatorUrl = ServiceEndpoints.ResolveCoordinatorUrl(
    builder.Configuration["Coordinator:BaseUrl"]);

builder.Services.AddHttpClient("CoordinatorClient", client =>
{
    client.BaseAddress = new Uri(coordinatorUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

var host = builder.Build();

var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
logger.LogInformation("Agent подключён к Coordinator: {Url}", coordinatorUrl);
logger.LogInformation("Файл лога: {Path}", LogFilePaths.AgentAccessLog);

host.Run();
