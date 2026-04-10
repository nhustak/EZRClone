using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EZRClone.Models;
using EZRClone.Services;
using EZRClone.Views;
using Microsoft.Win32;
using RCloneOperationOptions = HotCoreUtility.RClone.RCloneOperationOptions;
using RCloneOperationProfile = HotCoreUtility.RClone.RCloneOperationProfile;
using AppRCloneCommandRequest = EZRClone.Models.RCloneCommandRequest;

namespace EZRClone.ViewModels;

public partial class BrowseViewModel : ObservableObject
{
    private readonly IRCloneProcessService _processService;
    private readonly IRCloneConfigService _configService;
    private readonly IAppSettingsService _settingsService;
    private readonly Dictionary<string, (List<RemoteItem> items, string? status)> _cache = new();
    private bool _isInitialized;
    private const int DirectoryListTimeoutMs = 30000;
    private const int DirectoryInfoTimeoutMs = 30000;

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
    }

    public async Task EnsureInitializedAsync()
    {
        if (_isInitialized)
            return;

        _isInitialized = true;
        await LoadRemotesAsync();
    }

    private async Task LoadRemotesAsync()
    {
        try
        {
            var settings = _settingsService.Load();
            var remotes = await Task.Run(() => _configService.ReadConfig(settings.RCloneConfigPath));
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
                    var result = await _processService.ExecuteDetailedAsync(new AppRCloneCommandRequest
                    {
                        Arguments = ["size", "--json", remotePath],
                        Operation = "size",
                        Category = "Browse",
                        OperationProfile = RCloneOperationProfile.DirectoryInfo,
                        TimeoutMilliseconds = DirectoryInfoTimeoutMs
                    });
                    var exitCode = result.ExitCode;
                    var output = result.Output;

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

    public async Task DownloadItemsAsync(IList<RemoteItem> items)
    {
        if (SelectedRemote == null || items.Count == 0) return;

        var settings = _settingsService.Load();
        var options = ShowDownloadOptions(items, settings.DefaultDownloadPath, defaultPreserveStructure: items.Count > 1);
        if (options is null) return;

        await DownloadMultipleAsync(items, options);
    }

    public async Task DownloadItemsToAsync(IList<RemoteItem> items)
    {
        if (SelectedRemote == null || items.Count == 0) return;

        var settings = _settingsService.Load();
        var options = ShowDownloadOptions(items, settings.DefaultDownloadPath, defaultPreserveStructure: true);
        if (options is null) return;

        await DownloadMultipleAsync(items, options);
    }

    private async Task DownloadMultipleAsync(IList<RemoteItem> items, DownloadRequestOptions options)
    {
        var completed = 0;
        var failed = 0;
        var progressWindow = new DownloadProgressWindow(items.Count, options.DestinationPath)
        {
            Owner = Application.Current?.MainWindow
        };

        progressWindow.Show();
        progressWindow.ReportOutput("Download queued.", isError: false);

        try
        {
            foreach (var (item, index) in items.Select((item, index) => (item, index)))
            {
                var remotePath = $"{SelectedRemote}:{item.Path}";
                var localTarget = BuildLocalTargetPath(options.DestinationPath, item, options.PreserveFolderStructure);
                EnsureParentDirectory(localTarget, item.IsDirectory);

                var args = item.IsDirectory
                    ? new List<string> { "copy", remotePath, localTarget }
                    : new List<string> { "copyto", remotePath, localTarget };

                if (options.CreateLogFile && !string.IsNullOrWhiteSpace(options.LogFilePath))
                {
                    args.Add("--log-file");
                    args.Add(options.LogFilePath);
                }

                StatusMessage = $"Downloading {item.Name}... ({index + 1}/{items.Count})";
                progressWindow.SetCurrentItem(index + 1, items.Count, item.Name, localTarget);
                progressWindow.ReportOutput($"Starting {item.Name}", isError: false);

                try
                {
                    var result = await _processService.ExecuteDetailedAsync(new AppRCloneCommandRequest
                    {
                        Arguments = args,
                        Operation = item.IsDirectory ? "copy" : "copyto",
                        Category = "Download",
                        OperationProfile = RCloneOperationProfile.Download,
                        ExecutionOptions = new RCloneOperationOptions
                        {
                            Transfers = options.Transfers,
                            Checkers = options.Checkers,
                            DryRun = options.DryRun,
                            ExtraFlagsText = options.ExtraFlagsText,
                            UseProgress = true
                        },
                        RequiredArguments = ["--stats=1s", "--stats-one-line"],
                        OnOutput = output => progressWindow.ReportOutput(output.Text, output.IsError)
                    });

                    if (result.IsSuccess)
                    {
                        completed++;
                    }
                    else
                    {
                        failed++;
                        progressWindow.ReportOutput(
                            string.IsNullOrWhiteSpace(result.Error)
                                ? $"{item.Name} failed with exit code {result.ExitCode}."
                                : result.Error,
                            isError: true);
                    }
                }
                catch (Exception ex)
                {
                    failed++;
                    progressWindow.ReportOutput($"{item.Name} failed: {ex.Message}", isError: true);
                }
            }
        }
        finally
        {
            var summary = failed == 0
                ? $"Downloaded {completed} item{(completed != 1 ? "s" : "")} to {options.DestinationPath}"
                : $"Downloaded {completed}, failed {failed} of {items.Count}";

            StatusMessage = summary;
            progressWindow.Complete(summary, failed > 0);
        }
    }

    public async Task DeleteItemsAsync(IList<RemoteItem> items)
    {
        if (SelectedRemote == null || items.Count == 0) return;

        var label = BuildDeleteLabel(items);
        var result = MessageBox.Show(
            $"Delete {label} from remote '{SelectedRemote}'?\n\nDirectories will be purged recursively.\n\nThis cannot be undone.",
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
                var deleteResult = await _processService.ExecuteDetailedAsync(new AppRCloneCommandRequest
                {
                    Arguments = args,
                    Operation = item.IsDirectory ? "purge" : "deletefile",
                    Category = "Delete",
                    OperationProfile = RCloneOperationProfile.Delete
                });
                if (deleteResult.IsSuccess)
                {
                    Items.Remove(item);
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

        var cacheKey = $"{SelectedRemote}:{CurrentPath}";
        _cache.Remove(cacheKey);

        StatusMessage = failed == 0
            ? $"Deleted {completed} item{(completed != 1 ? "s" : "")}"
            : $"Deleted {completed}, failed {failed} of {items.Count}";
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
            var result = await _processService.ExecuteDetailedAsync(new AppRCloneCommandRequest
            {
                Arguments = args,
                Operation = "lsf",
                Category = "Browse",
                OperationProfile = RCloneOperationProfile.Browse,
                TimeoutMilliseconds = DirectoryListTimeoutMs
            });
            var exitCode = result.ExitCode;
            var output = result.Output;
            var error = result.Error;

            if (result.TimedOut)
                throw new TimeoutException("Browse request timed out.");

            if (exitCode != 0 && !string.IsNullOrWhiteSpace(error))
                throw new InvalidOperationException(error);

            var items = ParseLsfOutput(output, path);
            Items = new ObservableCollection<RemoteItem>(items);

            var location = string.IsNullOrEmpty(path) ? $"{SelectedRemote}:/" : $"{SelectedRemote}:{path}";
            var status = items.Count == 0
                ? $"Empty directory • {location}"
                : $"{items.Count} item{(items.Count != 1 ? "s" : "")} • {location}";
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

    internal static List<RemoteItem> ParseLsfOutput(string output, string parentPath)
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

    private static string BuildDeleteLabel(IList<RemoteItem> items)
    {
        if (items.Count == 1)
            return $"{(items[0].IsDirectory ? "directory" : "file")} '{items[0].Name}'";

        var preview = string.Join(", ", items.Take(3).Select(item => item.Name));
        if (items.Count > 3)
            preview += ", ...";

        return $"{items.Count} items ({preview})";
    }

    private DownloadRequestOptions? ShowDownloadOptions(IList<RemoteItem> items, string initialPath, bool defaultPreserveStructure)
    {
        if (SelectedRemote is null)
            return null;

        var settings = _settingsService.Load();
        var downloadProfile = settings.OperationProfiles.Download;
        var window = new DownloadOptionsWindow(
            SelectedRemote,
            items.ToList(),
            initialPath,
            defaultPreserveStructure,
            downloadProfile.Transfers ?? settings.DefaultTransfers,
            downloadProfile.Checkers ?? settings.DefaultCheckers,
            downloadProfile.ExtraFlagsText)
        {
            Owner = Application.Current?.MainWindow
        };

        return window.ShowDialog() == true ? window.Result : null;
    }

    private static string BuildLocalTargetPath(string basePath, RemoteItem item, bool preserveStructure)
    {
        if (!preserveStructure)
            return Path.Combine(basePath, item.Name);

        var relativePath = item.Path.TrimEnd('/').Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(basePath, relativePath);
    }

    private static void EnsureParentDirectory(string localTarget, bool isDirectory)
    {
        var directory = isDirectory
            ? localTarget
            : Path.GetDirectoryName(localTarget);

        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
    }
}

public class BreadcrumbSegment
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
}
