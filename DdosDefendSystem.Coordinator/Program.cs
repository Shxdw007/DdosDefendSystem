using DdosDefendSystem.Shared.Models;
using DdosDefendSystem.Coordinator.Data;
using DdosDefendSystem.Coordinator.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddSingleton<DdosAnalyzer>();
var recentLogs = new System.Collections.Concurrent.ConcurrentQueue<RequestLog>();
builder.Services.AddSingleton(recentLogs);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate(); 

    if (!db.Users.Any())
    {
        string username = "admin";
        db.Users.Add(new User
        {
            Username = username,
            PasswordHash = PasswordHasher.HashPassword("1234", username),
            Role = "Admin"
        });
        db.SaveChanges();
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/", () => "DDoS Coordinator API is running!");

app.MapPost("/api/auth/login", async ([FromBody] LoginRequest request, AppDbContext db) =>
{
    var user = await db.Users.FirstOrDefaultAsync(u => u.Username == request.Username);

    if (user == null)
        return Results.Json(new { Message = "INVALID_USER" }, statusCode: 401);

    var isPasswordValid = PasswordHasher.VerifyPassword(request.Password, request.Username, user.PasswordHash);

    if (!isPasswordValid)
        return Results.Json(new { Message = "INVALID_PASSWORD" }, statusCode: 401);

    return Results.Ok(new { Message = "ACCESS_GRANTED", Role = user.Role });
});

app.MapPost("/api/logs", ([FromBody] List<RequestLog>? logs, DdosAnalyzer analyzer, System.Collections.Concurrent.ConcurrentQueue<RequestLog> recentLogs) =>
{
    if (logs == null || logs.Count == 0)
        return Results.BadRequest();

    foreach (var log in logs)
    {
        recentLogs.Enqueue(log);
        while (recentLogs.Count > 50)
            recentLogs.TryDequeue(out _);
    }

    analyzer.Analyze(logs);

    return Results.Ok();
});

app.MapGet("/api/logs/recent", (System.Collections.Concurrent.ConcurrentQueue<RequestLog> recentLogs) =>
    Results.Ok(recentLogs.Reverse().ToList()));

app.MapGet("/api/blacklist", (DdosAnalyzer analyzer) => Results.Ok(analyzer.BannedIps.Values));

app.MapPost("/api/blacklist/update", async ([FromBody] BannedIpInfo updatedBan, AppDbContext db, DdosAnalyzer analyzer) =>
{
    var existBan = await db.BannedIps.FindAsync(updatedBan.IpAddress);
    if (existBan != null)
    {
        existBan.ExpiresAt = updatedBan.ExpiresAt;
        existBan.Reason = updatedBan.Reason;
        await db.SaveChangesAsync();

        analyzer.BannedIps[updatedBan.IpAddress] = updatedBan;
        return Results.Ok();
    }
    return Results.NotFound();
});

app.MapDelete("/api/blacklist/unban", async ([FromQuery] string ip, AppDbContext db, DdosAnalyzer analyzer) =>
{
    var existBan = await db.BannedIps.FindAsync(ip);
    if (existBan != null)
    {
        db.BannedIps.Remove(existBan);
        await db.SaveChangesAsync();
    }

    analyzer.BannedIps.TryRemove(ip, out _);
    return Results.Ok();
});

app.Run();