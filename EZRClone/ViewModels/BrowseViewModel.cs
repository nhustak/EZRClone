using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EZRClone.Models;
using EZRClone.Services;

namespace EZRClone.ViewModels;

public partial class BrowseViewModel : ObservableObject
{
    private readonly IRCloneProcessService _processService;
    private readonly IRCloneConfigService _configService;
    private readonly IAppSettingsService _settingsService;
    private readonly Dictionary<string, (List<RemoteItem> items, string? status)> _cache = new();

    [ObservableProperty]
    private ObservableCollection<string> _availableRemotes = new();

    [ObservableProperty]
    private string? _selectedRemote;

    [ObservableProperty]
    private ObservableCollection<RemoteItem> _items = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isLoadingInfo;

    [ObservableProperty]
    private string _currentPath = "";

    [ObservableProperty]
    private ObservableCollection<BreadcrumbSegment> _breadcrumbs = new();

    public BrowseViewModel(
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

    partial void OnSelectedRemoteChanged(string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            _cache.Clear();
            CurrentPath = "";
            _ = LoadDirectoryAsync("");
        }
    }

    [RelayCommand]
    private async Task NavigateToAsync(RemoteItem item)
    {
        if (!item.IsDirectory) return;
        CurrentPath = item.Path;
        await LoadDirectoryAsync(item.Path);
    }

    [RelayCommand]
    private async Task NavigateToBreadcrumbAsync(BreadcrumbSegment segment)
    {
        CurrentPath = segment.Path;
        await LoadDirectoryAsync(segment.Path);
    }

    [RelayCommand]
    private async Task GetDirectoryInfoAsync()
    {
        if (SelectedRemote == null) return;

        IsLoadingInfo = true;
        var dirs = Items.Where(i => i.IsDirectory).ToList();
        StatusMessage = dirs.Count > 0
            ? $"Getting size info for {dirs.Count} director{(dirs.Count != 1 ? "ies" : "y")}..."
            : "Getting size info...";

        try
        {
            long totalSize = 0;
            int totalFiles = 0;

            // Mark all dirs as loading
            foreach (var dir in dirs) dir.IsLoadingInfo = true;

            var completed = 0;
            var tasks = dirs.Select(async dir =>
            {
                try
                {
                    var remotePath = $"{SelectedRemote}:{dir.Path}";
                    var args = new List<string> { "size", "--json", remotePath };
                    var (exitCode, output, _) = await _processService.ExecuteAsync(args);

                    if (exitCode == 0 && !string.IsNullOrWhiteSpace(output))
                    {
                        try
                        {
                            var doc = System.Text.Json.JsonDocument.Parse(output);
                            var count = doc.RootElement.GetProperty("count").GetInt32();
                            var bytes = doc.RootElement.GetProperty("bytes").GetInt64();
                            dir.Size = bytes;
                            dir.FileCount = count;
                        }
                        catch { }
                    }
                    dir.HasLoadedInfo = true;
                }
                finally
                {
                    dir.IsLoadingInfo = false;
                    var done = Interlocked.Increment(ref completed);
                    StatusMessage = $"Getting size info... {done}/{dirs.Count}";
                }
            });

            await Task.WhenAll(tasks);

            // Also get sizes for files already in the list
            foreach (var item in Items)
            {
                if (item.Size > 0) totalSize += item.Size;
                if (item.IsDirectory) totalFiles += item.FileCount;
                else totalFiles++;
            }

            var status = $"Total: {totalFiles:N0} files, {RemoteItem.FormatSize(totalSize)}";
            StatusMessage = status;

            // Update cache with enriched data
            var cacheKey = $"{SelectedRemote}:{CurrentPath}";
            _cache[cacheKey] = (Items.ToList(), status);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoadingInfo = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (SelectedRemote == null) return;
        var key = $"{SelectedRemote}:{CurrentPath}";
        _cache.Remove(key);
        await LoadDirectoryAsync(CurrentPath);
    }

    [RelayCommand]
    private async Task NavigateUpAsync()
    {
        if (string.IsNullOrEmpty(CurrentPath)) return;

        var trimmed = CurrentPath.TrimEnd('/');
        var lastSlash = trimmed.LastIndexOf('/');
        CurrentPath = lastSlash >= 0 ? trimmed[..lastSlash] + "/" : "";
        await LoadDirectoryAsync(CurrentPath);
    }

    private async Task LoadDirectoryAsync(string path)
    {
        if (SelectedRemote == null) return;

        var cacheKey = $"{SelectedRemote}:{path}";
        UpdateBreadcrumbs(path);

        // Return cached data if available
        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            Items = new ObservableCollection<RemoteItem>(cached.items);
            StatusMessage = cached.status;
            return;
        }

        IsLoading = true;
        StatusMessage = null;
        Items.Clear();

        try
        {
            var remotePath = $"{SelectedRemote}:{path}";
            var args = new List<string>
            {
                "lsf", "--format", "pstm", "--separator", "\t", "--max-depth", "1", remotePath
            };
            var (exitCode, output, error) = await _processService.ExecuteAsync(args);

            if (exitCode != 0 && !string.IsNullOrWhiteSpace(error))
                throw new InvalidOperationException(error);

            var items = ParseLsfOutput(output, path);
            Items = new ObservableCollection<RemoteItem>(items);

            var status = items.Count == 0 ? "Empty directory" : $"{items.Count} item{(items.Count != 1 ? "s" : "")}";
            StatusMessage = status;
            _cache[cacheKey] = (items, status);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void UpdateBreadcrumbs(string path)
    {
        var segments = new ObservableCollection<BreadcrumbSegment>();
        segments.Add(new BreadcrumbSegment { Name = SelectedRemote + ":", Path = "" });

        if (!string.IsNullOrEmpty(path))
        {
            var parts = path.TrimEnd('/').Split('/');
            var accumulated = "";
            foreach (var part in parts)
            {
                accumulated += part + "/";
                segments.Add(new BreadcrumbSegment { Name = part, Path = accumulated });
            }
        }

        Breadcrumbs = segments;
    }

    private static List<RemoteItem> ParseLsfOutput(string output, string parentPath)
    {
        var items = new List<RemoteItem>();
        if (string.IsNullOrWhiteSpace(output)) return items;

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('\t');
            if (parts.Length < 1) continue;

            var name = parts[0].Trim();
            if (string.IsNullOrEmpty(name)) continue;

            var isDir = name.EndsWith('/');
            var displayName = isDir ? name.TrimEnd('/') : name;

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
                Name = displayName,
                Path = parentPath + name,
                IsDirectory = isDir,
                Size = isDir ? -1 : size,
                ModTime = modTime
            });
        }

        // Sort: directories first, then by name
        return items
            .OrderByDescending(i => i.IsDirectory)
            .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

public class BreadcrumbSegment
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
}
