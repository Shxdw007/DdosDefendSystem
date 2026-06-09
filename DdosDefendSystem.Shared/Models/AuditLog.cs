namespace DdosDefendSystem.Shared.Models;

public class AuditLog
{
    public int Id { get; set; }

    public DateTime Timestamp { get; set; }

    public string Username { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string Details { get; set; } = string.Empty;
}
