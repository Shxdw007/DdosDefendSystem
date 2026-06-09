using DdosDefendSystem.Coordinator.Hubs;
using DdosDefendSystem.Shared.Models;
using Microsoft.AspNetCore.SignalR;

namespace DdosDefendSystem.Coordinator.Services;

public class TrafficBroadcaster
{
    private readonly IHubContext<TrafficHub> _hubContext;

    public TrafficBroadcaster(IHubContext<TrafficHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task BroadcastAsync(IReadOnlyList<ActiveIpTraffic> traffic) =>
        _hubContext.Clients.All.SendAsync("ReceiveTraffic", traffic);
}
