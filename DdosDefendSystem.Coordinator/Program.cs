using DdosDefendSystem.Shared.Models;
using DdosDefendSystem.Coordinator.Data;
using DdosDefendSystem.Coordinator.Hubs;
using DdosDefendSystem.Coordinator.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<AuditService>();
builder.Services.AddSingleton<TrafficBroadcaster>();
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

app.MapGet("/api/whitelist", async (AppDbContext db) =>
    Results.Ok(await db.WhitelistIps.OrderBy(w => w.IpAddress).ToListAsync()));

app.MapPost("/api/whitelist", async ([FromBody] WhitelistIpRequest request, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.IpAddress))
        return Results.BadRequest("IpAddress is required.");

    if (await db.WhitelistIps.FindAsync(request.IpAddress) != null)
        return Results.Conflict("IP already whitelisted.");

    var entry = new WhitelistIp
    {
        IpAddress = request.IpAddress,
        AddedAt = DateTime.UtcNow,
        AddedBy = request.AddedBy
    };

    db.WhitelistIps.Add(entry);
    await db.SaveChangesAsync();
    return Results.Created($"/api/whitelist/{entry.IpAddress}", entry);
});

app.MapDelete("/api/whitelist/{ip}", async (string ip, AppDbContext db) =>
{
    var entry = await db.WhitelistIps.FindAsync(ip);
    if (entry == null)
        return Results.NotFound();

    db.WhitelistIps.Remove(entry);
    await db.SaveChangesAsync();
    return Results.Ok();
});

app.MapPost("/api/bans/manual", async (
    [FromBody] ManualBanRequest request,
    AppDbContext db,
    DdosAnalyzer analyzer,
    AuditService audit) =>
{
    if (string.IsNullOrWhiteSpace(request.IpAddress))
        return Results.BadRequest("IpAddress is required.");

    var now = DateTime.UtcNow;
    var banInfo = new BannedIpInfo
    {
        IpAddress = request.IpAddress,
        Reason = string.IsNullOrWhiteSpace(request.Reason) ? "Manual ban" : request.Reason,
        BlockedAt = now,
        ExpiresAt = now.AddMinutes(request.DurationMinutes > 0 ? request.DurationMinutes : 60)
    };

    var existingBan = await db.BannedIps.FindAsync(request.IpAddress);
    if (existingBan == null)
    {
        db.BannedIps.Add(banInfo);
    }
    else
    {
        existingBan.Reason = banInfo.Reason;
        existingBan.BlockedAt = banInfo.BlockedAt;
        existingBan.ExpiresAt = banInfo.ExpiresAt;
        existingBan.UnblockedAt = null;
        existingBan.UnblockedBy = null;
        banInfo = existingBan;
    }

    await db.SaveChangesAsync();
    analyzer.BannedIps[request.IpAddress] = banInfo;

    await audit.LogAsync(
        request.Username,
        "Manual Ban IP",
        $"IP {request.IpAddress} banned until {banInfo.ExpiresAt:u}. Reason: {banInfo.Reason}");

    return Results.Ok(banInfo);
});

app.MapPost("/api/bans/unban", async (
    [FromBody] UnbanRequest request,
    AppDbContext db,
    DdosAnalyzer analyzer,
    AuditService audit) =>
{
    if (string.IsNullOrWhiteSpace(request.IpAddress))
        return Results.BadRequest("IpAddress is required.");

    var existBan = await db.BannedIps.FindAsync(request.IpAddress);
    if (existBan == null)
        return Results.NotFound();

    existBan.UnblockedAt = DateTime.UtcNow;
    existBan.UnblockedBy = request.Username;
    await db.SaveChangesAsync();

    analyzer.BannedIps.TryRemove(request.IpAddress, out _);

    await audit.LogAsync(
        request.Username,
        "Unban IP",
        $"IP {request.IpAddress} unblocked.");

    return Results.Ok(existBan);
});

app.MapPost("/api/audit/login", async ([FromBody] AuditLoginRequest request, AuditService audit) =>
{
    if (string.IsNullOrWhiteSpace(request.Username))
        return Results.BadRequest("Username is required.");

    await audit.LogAsync(request.Username, "Login", $"User {request.Username} logged in.");
    return Results.Ok();
});

app.MapGet("/api/audit", async (AppDbContext db) =>
    Results.Ok(await db.AuditLogs
        .OrderByDescending(a => a.Timestamp)
        .Take(500)
        .ToListAsync()));

app.MapPost("/api/traffic/update", async (
    [FromBody] List<ActiveIpTraffic>? traffic,
    TrafficBroadcaster broadcaster) =>
{
    if (traffic == null)
        return Results.BadRequest();

    await broadcaster.BroadcastAsync(traffic);
    return Results.Ok();
});

app.MapHub<TrafficHub>("/hubs/traffic");

app.Run();