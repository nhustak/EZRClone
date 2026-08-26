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

public partial class SearchViewModel : ObservableObject
{
    private readonly IRCloneProcessService _processService;
    private readonly IRCloneConfigService _configService;
    private readonly IAppSettingsService _settingsService;
    private bool _isInitialized;

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
            var remotes = await _configService.ReadConfigAsync(settings.RCloneConfigPath);
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
            var result = await _processService.ExecuteDetailedAsync(new AppRCloneCommandRequest
            {
                Arguments = args,
                Operation = "lsf",
                Category = "Search",
                OperationProfile = RCloneOperationProfile.Search
            });
            var exitCode = result.ExitCode;
            var output = result.Output;
            var error = result.Error;

            if (exitCode != 0 && !string.IsNullOrWhiteSpace(error))
                throw new InvalidOperationException(error);

            var items = ParseSearchOutput(output);
            Results = new ObservableCollection<RemoteItem>(items);

            StatusMessage = items.Count == 0
                ? $"No results found in {SelectedRemote} for '{SearchPattern}'"
                : $"{items.Count} result{(items.Count != 1 ? "s" : "")} in {SelectedRemote} for '{SearchPattern}'";
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
        var options = ShowDownloadOptions(items, settings.DefaultDownloadPath, defaultPreserveStructure: true);
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

    internal static List<RemoteItem> ParseSearchOutput(string output)
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
