using System.Net.Http;
using System.Net.Http.Json;
using DdosDefendSystem.Shared.Models;

namespace DdosDefendSystem.AdminPanel.Services;

public class CoordinatorApiService
{
    private readonly HttpClient _httpClient;

    public CoordinatorApiService()
    {
        _httpClient = AppConfig.CreateCoordinatorClient();
    }

    public async Task<(bool Success, string? Role, string? Error)> LoginAsync(string username, string password)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = username,
            Password = password
        });

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
            return (true, result?.Role, null);
        }

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            return (false, null, "ACCESS DENIED");

        return (false, null, $"ERROR: {response.StatusCode}");
    }

    public Task AuditLoginAsync(string username) =>
        _httpClient.PostAsJsonAsync("/api/audit/login", new AuditLoginRequest { Username = username });

    public async Task<List<RequestLog>> GetRecentLogsAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<RequestLog>>("/api/logs/recent") ?? [];
    }

    public async Task<List<BannedIpInfo>> GetBlacklistAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<BannedIpInfo>>("/api/blacklist") ?? [];
    }

    public Task<HttpResponseMessage> UpdateBlacklistAsync(BannedIpInfo ban) =>
        _httpClient.PostAsJsonAsync("/api/blacklist/update", ban);

    public Task<HttpResponseMessage> UnbanAsync(string ipAddress, string username) =>
        _httpClient.PostAsJsonAsync("/api/bans/unban", new UnbanRequest
        {
            IpAddress = ipAddress,
            Username = username
        });

    public Task<HttpResponseMessage> ManualBanAsync(string ipAddress, string username, string reason, int durationMinutes) =>
        _httpClient.PostAsJsonAsync("/api/bans/manual", new ManualBanRequest
        {
            IpAddress = ipAddress,
            Username = username,
            Reason = reason,
            DurationMinutes = durationMinutes
        });

    public async Task<List<WhitelistIp>> GetWhitelistAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<WhitelistIp>>("/api/whitelist") ?? [];
    }

    public Task<HttpResponseMessage> AddWhitelistAsync(string ipAddress, string addedBy) =>
        _httpClient.PostAsJsonAsync("/api/whitelist", new WhitelistIpRequest
        {
            IpAddress = ipAddress,
            AddedBy = addedBy
        });

    public Task<HttpResponseMessage> DeleteWhitelistAsync(string ipAddress) =>
        _httpClient.DeleteAsync($"/api/whitelist/{Uri.EscapeDataString(ipAddress)}");

    public async Task<List<AuditLog>> GetAuditLogsAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<AuditLog>>("/api/audit") ?? [];
    }

    private sealed class LoginResponse
    {
        public string Message { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }
}
