using DdosDefendSystem.Coordinator.Data;
using DdosDefendSystem.Shared.Models;

namespace DdosDefendSystem.Coordinator.Services;

public class AuditService
{
    private readonly AppDbContext _db;

    public AuditService(AppDbContext db)
    {
        _db = db;
    }

    public async Task LogAsync(string username, string action, string details)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            Timestamp = DateTime.UtcNow,
            Username = username,
            Action = action,
            Details = details
        });

        await _db.SaveChangesAsync();
    }
}
