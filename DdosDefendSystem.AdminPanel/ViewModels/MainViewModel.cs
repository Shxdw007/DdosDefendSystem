using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DdosDefendSystem.AdminPanel.Services;
using DdosDefendSystem.AdminPanel.Views;

namespace DdosDefendSystem.AdminPanel.ViewModels;

public partial class MainViewModel : ViewModelBase, IAsyncDisposable
{
    private readonly SessionService _session;
    private readonly ThemeService _themeService;
    private readonly TrafficHubService _hubService;
    private readonly L7LogsViewModel _l7LogsViewModel;
    private readonly BlacklistViewModel _blacklistViewModel;

    public ObservableCollection<TabItemViewModel> Tabs { get; } = new();

    [ObservableProperty]
    private string _currentUsername = string.Empty;

    [ObservableProperty]
    private string _themeToggleLabel = "☀";

    public L4TrafficViewModel L4Traffic { get; }
    public WhitelistViewModel Whitelist { get; }
    public AuditLogViewModel AuditLog { get; }

    public MainViewModel(
        SessionService session,
        ThemeService themeService,
        TrafficHubService hubService,
        CoordinatorApiService api,
        L4TrafficViewModel l4Traffic,
        L7LogsViewModel l7Logs,
        BlacklistViewModel blacklist,
        WhitelistViewModel whitelist,
        AuditLogViewModel auditLog)
    {
        _session = session;
        _themeService = themeService;
        _hubService = hubService;
        _l7LogsViewModel = l7Logs;
        _blacklistViewModel = blacklist;

        L4Traffic = l4Traffic;
        Whitelist = whitelist;
        AuditLog = auditLog;

        CurrentUsername = session.Username ?? "unknown";
        ThemeToggleLabel = themeService.ThemeToggleLabel;
        _themeService.ThemeChanged += OnThemeChanged;

        Tabs.Add(new TabItemViewModel { Header = "L4 Traffic", Content = new L4TrafficView { DataContext = l4Traffic } });
        Tabs.Add(new TabItemViewModel { Header = "L7 Logs", Content = new L7LogsView { DataContext = l7Logs } });
        Tabs.Add(new TabItemViewModel { Header = "Blacklist", Content = new BlacklistView { DataContext = blacklist } });
        Tabs.Add(new TabItemViewModel { Header = "Whitelist", Content = new WhitelistView { DataContext = whitelist } });
        Tabs.Add(new TabItemViewModel { Header = "Audit Log", Content = new AuditLogView { DataContext = auditLog } });
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        _themeService.ToggleTheme();
    }

    private void OnThemeChanged() => ThemeToggleLabel = _themeService.ThemeToggleLabel;

    [RelayCommand]
    private async Task LoadedAsync()
    {
        _l7LogsViewModel.Start();
        _blacklistViewModel.Start();

        try
        {
            await _hubService.StartAsync();
        }
        catch
        {
            // hub may be unavailable during dev
        }

        await Whitelist.LoadAsync();
        await AuditLog.LoadAsync();
    }

    [RelayCommand]
    private async Task UnloadedAsync()
    {
        _l7LogsViewModel.Stop();
        _blacklistViewModel.Stop();
        await _hubService.DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        _themeService.ThemeChanged -= OnThemeChanged;
        await _hubService.DisposeAsync();
        _l7LogsViewModel.Dispose();
        _blacklistViewModel.Dispose();
    }
}
