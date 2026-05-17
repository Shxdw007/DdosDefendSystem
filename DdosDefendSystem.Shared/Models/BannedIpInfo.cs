namespace DdosDefendSystem.Shared.Models;

public class BannedIpInfo
{
    public string IpAddress { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public DateTime BlockedAt { get; set; }

    public DateTime ExpiresAt { get; set; }
}