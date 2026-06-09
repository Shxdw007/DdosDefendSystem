namespace DdosDefendSystem.AdminPanel.Services;

public class SessionService
{
    public string? Username { get; private set; }
    public string? Role { get; private set; }

    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(Username);

    public void SetSession(string username, string role)
    {
        Username = username;
        Role = role;
    }

    public void Clear()
    {
        Username = null;
        Role = null;
    }
}
