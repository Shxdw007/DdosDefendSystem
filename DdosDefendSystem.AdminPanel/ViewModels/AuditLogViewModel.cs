using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using DdosDefendSystem.AdminPanel.Services;
using DdosDefendSystem.Shared.Models;

namespace DdosDefendSystem.AdminPanel.ViewModels;

public partial class AuditLogViewModel : ViewModelBase
{
    private readonly CoordinatorApiService _api;

    public ObservableCollection<AuditLog> Entries { get; } = new();

    public AuditLogViewModel(CoordinatorApiService api)
    {
        _api = api;
    }

    public async Task LoadAsync() => await RefreshAsync();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            var logs = await _api.GetAuditLogsAsync();
            Entries.Clear();
            foreach (var log in logs)
                Entries.Add(log);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Ошибка загрузки журнала");
        }
    }
}
