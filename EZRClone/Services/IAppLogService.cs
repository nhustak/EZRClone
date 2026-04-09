using EZRClone.Models;

namespace EZRClone.Services;

public interface IAppLogService
{
    event EventHandler<AppLogEntry>? SessionEntryAdded;
    event EventHandler? HistoryChanged;

    IReadOnlyList<AppLogEntry> GetSessionEntries();
    IReadOnlyList<AppLogEntry> GetRunHistory();
    void AddSessionEntry(AppLogEntry entry);
    void ClearSession();
    Task AddRunHistoryEntryAsync(AppLogEntry entry);
    Task LoadRunHistoryAsync();
}
