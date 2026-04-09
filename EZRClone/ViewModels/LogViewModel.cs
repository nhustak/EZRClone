using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EZRClone.Models;
using EZRClone.Services;

namespace EZRClone.ViewModels;

public partial class LogViewModel : ObservableObject
{
    private readonly IAppLogService _appLogService;

    public ObservableCollection<AppLogEntry> SessionEntries { get; } = new();
    public ObservableCollection<AppLogEntry> HistoryEntries { get; } = new();

    [ObservableProperty]
    private AppLogEntry? _selectedSessionEntry;

    [ObservableProperty]
    private AppLogEntry? _selectedHistoryEntry;

    [ObservableProperty]
    private string _historyFilter = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "No events captured yet.";

    public LogViewModel(IAppLogService appLogService)
    {
        _appLogService = appLogService;
        _appLogService.SessionEntryAdded += OnSessionEntryAdded;
        _appLogService.HistoryChanged += OnHistoryChanged;

        ReloadSessionEntries();
        ReloadHistoryEntries();
    }

    public void FocusJobHistory(string? jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            return;

        HistoryFilter = jobId;
        ApplyHistoryFilter();
    }

    [RelayCommand]
    private void ClearSession()
    {
        _appLogService.ClearSession();
        ReloadSessionEntries();
        StatusMessage = "Session log cleared.";
    }

    [RelayCommand]
    private void ClearHistoryFilter()
    {
        HistoryFilter = string.Empty;
        ApplyHistoryFilter();
    }

    partial void OnHistoryFilterChanged(string value)
    {
        ApplyHistoryFilter();
    }

    private void OnSessionEntryAdded(object? sender, AppLogEntry entry)
    {
        SessionEntries.Insert(0, entry);
        StatusMessage = $"Session events: {SessionEntries.Count} • Run history: {HistoryEntries.Count}";
    }

    private void OnHistoryChanged(object? sender, EventArgs e)
    {
        ReloadHistoryEntries();
    }

    private void ReloadSessionEntries()
    {
        SessionEntries.Clear();
        foreach (var entry in _appLogService.GetSessionEntries())
            SessionEntries.Add(entry);

        StatusMessage = $"Session events: {SessionEntries.Count} • Run history: {HistoryEntries.Count}";
    }

    private void ReloadHistoryEntries()
    {
        ApplyHistoryFilter();
        StatusMessage = $"Session events: {SessionEntries.Count} • Run history: {HistoryEntries.Count}";
    }

    private void ApplyHistoryFilter()
    {
        var entries = _appLogService.GetRunHistory();
        var filtered = string.IsNullOrWhiteSpace(HistoryFilter)
            ? entries
            : entries.Where(e =>
                (e.JobId?.Contains(HistoryFilter, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (e.JobName?.Contains(HistoryFilter, StringComparison.OrdinalIgnoreCase) ?? false) ||
                e.Message.Contains(HistoryFilter, StringComparison.OrdinalIgnoreCase));

        HistoryEntries.Clear();
        foreach (var entry in filtered)
            HistoryEntries.Add(entry);
    }
}
