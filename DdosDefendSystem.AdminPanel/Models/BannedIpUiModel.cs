using CommunityToolkit.Mvvm.ComponentModel;
using DdosDefendSystem.Shared.Models;

namespace DdosDefendSystem.AdminPanel.Models;

public partial class BannedIpUiModel : ObservableObject
{
    public string IpAddress { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime BlockedAt { get; set; }

    [ObservableProperty]
    private DateTime _expiresAt;

    [ObservableProperty]
    private string _timeLeftString = string.Empty;

    public BannedIpUiModel(BannedIpInfo info)
    {
        IpAddress = info.IpAddress;
        Reason = info.Reason;
        BlockedAt = info.BlockedAt;
        ExpiresAt = info.ExpiresAt;
        UpdateCountdown();
    }

    public void UpdateCountdown()
    {
        var remaining = ExpiresAt - DateTime.UtcNow;

        if (remaining.TotalDays > 365)
            TimeLeftString = "ПЕРМАНЕНТНО";
        else if (remaining.TotalSeconds <= 0)
            TimeLeftString = "ИСТЕК";
        else
            TimeLeftString = $"{remaining.Minutes:D2}:{remaining.Seconds:D2}";
    }
}
