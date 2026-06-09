using DdosDefendSystem.AdminPanel.ViewModels;

namespace DdosDefendSystem.AdminPanel.Services;

public static class AppServices
{
    public static SessionService Session { get; } = new();
    public static ThemeService Theme { get; } = new();
    public static CoordinatorApiService Api { get; } = new();
    public static TrafficHubService TrafficHub { get; } = new();

    public static LoginViewModel CreateLoginViewModel() =>
        new(Api, Session);

    public static MainViewModel CreateMainViewModel()
    {
        var l4 = new L4TrafficViewModel(Api, Session, TrafficHub);
        var l7 = new L7LogsViewModel(Api);
        var blacklist = new BlacklistViewModel(Api, Session);
        var whitelist = new WhitelistViewModel(Api, Session);
        var audit = new AuditLogViewModel(Api);

        return new MainViewModel(Session, Theme, TrafficHub, Api, l4, l7, blacklist, whitelist, audit);
    }
}
