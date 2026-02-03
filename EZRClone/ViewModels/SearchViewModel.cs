using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EZRClone.Models;
using EZRClone.Services;
using Microsoft.Win32;

namespace EZRClone.ViewModels;

public partial class SearchViewModel : ObservableObject
{
    private readonly IRCloneProcessService _processService;
    private readonly IRCloneConfigService _configService;
    private readonly IAppSettingsService _settingsService;

    [ObservableProperty]
    private ObservableCollection<string> _availableRemotes = new();

    [ObservableProperty]
    private string? _selectedRemote;

    [ObservableProperty]
    private string _searchPattern = "";

    [ObservableProperty]
    private ObservableCollection<RemoteItem> _results = new();

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private string? _statusMessage;

    public SearchViewModel(
        IRCloneProcessService processService,
        IRCloneConfigService configService,
        IAppSettingsService settingsService)
    {
        _processService = processService;
        _configService = configService;
        _settingsService = settingsService;

        _ = LoadRemotesAsync();
    }

    private async Task LoadRemotesAsync()
    {
        try
        {
            var settings = _settingsService.Load();
            var remotes = _configService.ReadConfig(settings.RCloneConfigPath);
            AvailableRemotes = new ObservableCollection<string>(remotes.Select(r => r.Name));
        }
        catch
        {
            AvailableRemotes = new ObservableCollection<string>();
        }
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (SelectedRemote == null || string.IsNullOrWhiteSpace(SearchPattern)) return;

        IsSearching = true;
        StatusMessage = "Searching...";
        Results.Clear();

        try
        {
            var remotePath = $"{SelectedRemote}:";
            var args = new List<string>
            {
                "lsf", "--format", "pst", "--separator", "\t",
                "-R", "--include", SearchPattern, remotePath
            };
            var (exitCode, output, error) = await _processService.ExecuteAsync(args);

            if (exitCode != 0 && !string.IsNullOrWhiteSpace(error))
                throw new InvalidOperationException(error);

            var items = ParseSearchOutput(output);
            Results = new ObservableCollection<RemoteItem>(items);

            StatusMessage = items.Count == 0
                ? "No results found"
                : $"{items.Count} result{(items.Count != 1 ? "s" : "")}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsSearching = false;
        }
    }

    public async Task DownloadItemsAsync(IList<RemoteItem> items)
    {
        if (SelectedRemote == null || items.Count == 0) return;

        var settings = _settingsService.Load();
        var downloadPath = settings.DefaultDownloadPath;

        if (string.IsNullOrWhiteSpace(downloadPath))
        {
            var dialog = new OpenFolderDialog { Title = "Select download folder" };
            if (dialog.ShowDialog() != true) return;
            downloadPath = dialog.FolderName;
        }

        await DownloadMultipleAsync(items, downloadPath);
    }

    public async Task DownloadItemsToAsync(IList<RemoteItem> items)
    {
        if (SelectedRemote == null || items.Count == 0) return;

        var settings = _settingsService.Load();
        var dialog = new OpenFolderDialog
        {
            Title = "Select download folder",
            InitialDirectory = string.IsNullOrWhiteSpace(settings.DefaultDownloadPath)
                ? null : settings.DefaultDownloadPath
        };
        if (dialog.ShowDialog() != true) return;

        await DownloadMultipleAsync(items, dialog.FolderName);
    }

    private async Task DownloadMultipleAsync(IList<RemoteItem> items, string localPath)
    {
        var completed = 0;
        var failed = 0;

        foreach (var item in items)
        {
            var remotePath = $"{SelectedRemote}:{item.Path}";
            var localTarget = System.IO.Path.Combine(localPath, item.Name);

            var args = item.IsDirectory
                ? new List<string> { "copy", remotePath, localTarget }
                : new List<string> { "copyto", remotePath, localTarget };

            StatusMessage = $"Downloading {item.Name}... ({completed + 1}/{items.Count})";
            try
            {
                var (exitCode, _, _) = await _processService.ExecuteAsync(args);
                if (exitCode == 0) completed++;
                else failed++;
            }
            catch
            {
                failed++;
            }
        }

        StatusMessage = failed == 0
            ? $"Downloaded {completed} item{(completed != 1 ? "s" : "")} to {localPath}"
            : $"Downloaded {completed}, failed {failed} of {items.Count}";
    }

    public async Task DeleteItemsAsync(IList<RemoteItem> items)
    {
        if (SelectedRemote == null || items.Count == 0) return;

        var label = items.Count == 1
            ? $"{(items[0].IsDirectory ? "directory" : "file")} '{items[0].Name}'"
            : $"{items.Count} items";
        var result = MessageBox.Show(
            $"Delete {label}?\n\nThis cannot be undone.",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        var completed = 0;
        var failed = 0;

        foreach (var item in items)
        {
            var remotePath = $"{SelectedRemote}:{item.Path}";
            var args = item.IsDirectory
                ? new List<string> { "purge", remotePath }
                : new List<string> { "deletefile", remotePath };

            StatusMessage = $"Deleting {item.Name}... ({completed + failed + 1}/{items.Count})";
            try
            {
                var (exitCode, _, _) = await _processService.ExecuteAsync(args);
                if (exitCode == 0)
                {
                    Results.Remove(item);
                    completed++;
                }
                else
                {
                    failed++;
                }
            }
            catch
            {
                failed++;
            }
        }

        StatusMessage = failed == 0
            ? $"Deleted {completed} item{(completed != 1 ? "s" : "")}"
            : $"Deleted {completed}, failed {failed} of {items.Count}";
    }

    private static List<RemoteItem> ParseSearchOutput(string output)
    {
        var items = new List<RemoteItem>();
        if (string.IsNullOrWhiteSpace(output)) return items;

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('\t');
            if (parts.Length < 1) continue;

            var path = parts[0].Trim();
            if (string.IsNullOrEmpty(path)) continue;

            var isDir = path.EndsWith('/');
            var cleanPath = isDir ? path.TrimEnd('/') : path;
            var name = System.IO.Path.GetFileName(cleanPath);

            long size = -1;
            if (parts.Length >= 2 && long.TryParse(parts[1].Trim(), out var s))
                size = s;

            DateTime? modTime = null;
            if (parts.Length >= 3)
            {
                var timeStr = parts[2].Trim();
                if (DateTime.TryParse(timeStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                    modTime = dt;
            }

            items.Add(new RemoteItem
            {
                Name = name,
                Path = path,
                IsDirectory = isDir,
                Size = isDir ? -1 : size,
                ModTime = modTime
            });
        }

        return items.OrderBy(i => i.Path, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
