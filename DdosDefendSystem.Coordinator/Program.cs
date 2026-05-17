using DdosDefendSystem.Shared.Models;
using DdosDefendSystem.Coordinator.Services;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<DdosAnalyzer>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/", () => "DDoS Coordinator API is running!");

app.MapPost("/api/logs", ([FromBody] List<RequestLog> logs, DdosAnalyzer analyzer, ILogger<Program> logger) =>
{
    logger.LogInformation("Получена пачка логов. Обработка...");

    analyzer.Analyze(logs);

    return Results.Ok(new { Message = "Логи успешно обработаны" });
});
app.MapGet("/api/blacklist", (DdosAnalyzer analyzer) =>
{
    return Results.Ok(analyzer.BannedIps.Values);
});
app.Run();