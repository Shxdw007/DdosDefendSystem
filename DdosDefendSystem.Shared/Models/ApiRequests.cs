namespace DdosDefendSystem.Shared.Models;

public class ManualBanRequest
{
    public string IpAddress { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public int DurationMinutes { get; set; } = 60;
}

public class UnbanRequest
{
    public string IpAddress { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;
}

public class AuditLoginRequest
{
    public string Username { get; set; } = string.Empty;
}

public class WhitelistIpRequest
{
    public string IpAddress { get; set; } = string.Empty;

    public string AddedBy { get; set; } = string.Empty;
}

public class ActiveIpTraffic
{
    public string IpAddress { get; set; } = string.Empty;

    public int ConnectionCount { get; set; }
}
