namespace DdosDefendSystem.Shared.Models;

public class WhitelistIp
{
    public string IpAddress { get; set; } = string.Empty;

    public DateTime AddedAt { get; set; }

    public string AddedBy { get; set; } = string.Empty;
}
