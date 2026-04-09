using System.Text.Json;
using System.IO;
using EZRClone.Models;

namespace EZRClone.Services;

public class AppLogService : IAppLogService
{
    private static readonly string AppDataDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EZRClone");

    private static readonly string HistoryPath =
        Path.Combine(AppDataDir, "run-history.json");

    private readonly List<AppLogEntry> _sessionEntries = new();
    private readonly List<AppLogEntry> _historyEntries = new();
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public event EventHandler<AppLogEntry>? SessionEntryAdded;
    public event EventHandler? HistoryChanged;

    public IReadOnlyList<AppLogEntry> GetSessionEntries() => _sessionEntries.ToList();

    public IReadOnlyList<AppLogEntry> GetRunHistory() => _historyEntries.ToList();

    public void AddSessionEntry(AppLogEntry entry)
    {
        _sessionEntries.Insert(0, entry);
        SessionEntryAdded?.Invoke(this, entry);
    }

    public void ClearSession()
    {
        _sessionEntries.Clear();
    }

    public async Task AddRunHistoryEntryAsync(AppLogEntry entry)
    {
        entry.IsHistory = true;
        _historyEntries.Insert(0, entry);
        await SaveHistoryAsync();
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task LoadRunHistoryAsync()
    {
        _historyEntries.Clear();

        if (!File.Exists(HistoryPath))
            return;

        try
        {
            var json = await File.ReadAllTextAsync(HistoryPath);
            var entries = JsonSerializer.Deserialize<List<AppLogEntry>>(json, _jsonOptions) ?? new List<AppLogEntry>();
            _historyEntries.AddRange(entries.OrderByDescending(e => e.Timestamp));
        }
        catch
        {
            AddSessionEntry(new AppLogEntry
            {
                Severity = AppLogSeverity.Warning,
                Category = "Log",
                Message = "Run history could not be loaded. The history file appears to be invalid."
            });
        }

        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task SaveHistoryAsync()
    {
        Directory.CreateDirectory(AppDataDir);
        var json = JsonSerializer.Serialize(_historyEntries, _jsonOptions);
        await File.WriteAllTextAsync(HistoryPath, json);
    }
}
