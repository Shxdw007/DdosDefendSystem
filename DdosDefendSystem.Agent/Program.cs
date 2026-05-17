using DdosDefendSystem.Agent;
using DdosDefendSystem.Agent.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<NginxLogParser>();

builder.Services.AddSingleton<IBlocker, MockLinuxBlocker>();

builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<BlacklistSyncWorker>();

builder.Services.AddHttpClient("CoordinatorClient", client =>
{
    client.BaseAddress = new Uri("https://localhost:7105");
});

var host = builder.Build();
host.Run();