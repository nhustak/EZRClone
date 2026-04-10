using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;
using EZRClone.Models;
using EZRClone.Services;
using HotCoreUtility.RClone;

namespace EZRClone.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IAppSettingsService _settingsService;
    private readonly IRCloneProcessService _processService;

    [ObservableProperty]
    private string _rCloneExePath = string.Empty;

    [ObservableProperty]
    private string _rCloneConfigPath = string.Empty;

    [ObservableProperty]
    private string _defaultDownloadPath = string.Empty;

    [ObservableProperty]
    private int _defaultTransfers = 4;

    [ObservableProperty]
    private int _defaultCheckers = 8;

    [ObservableProperty]
    private string _defaultDownloadExtraFlags = string.Empty;

    [ObservableProperty]
    private ObservableCollection<OperationOptionsEditor> _operationEditors = new();

    [ObservableProperty]
    private ObservableCollection<ConnectionPresetOption> _connectionPresets = new();

    [ObservableProperty]
    private ConnectionPresetOption? _selectedConnectionPreset;

    [ObservableProperty]
    private bool _alwaysGetDirectoryInfo;

    [ObservableProperty]
    private bool _defaultDeleteDryRun = true;

    [ObservableProperty]
    private string _validationMessage = string.Empty;

    [ObservableProperty]
    private bool _isValid;

    [ObservableProperty]
    private string _executableValidationMessage = "Executable has not been validated yet.";

    [ObservableProperty]
    private string _configValidationMessage = "Config path has not been validated yet.";

    [ObservableProperty]
    private string _remoteValidationMessage = "Remote sanity check has not been run yet.";

    public SettingsViewModel(IAppSettingsService settingsService, IRCloneProcessService processService)
    {
        _settingsService = settingsService;
        _processService = processService;
    }

    [RelayCommand]
    private void Load()
    {
        var settings = _settingsService.Load();
        RCloneExePath = settings.RCloneExePath;
        RCloneConfigPath = settings.RCloneConfigPath;
        DefaultDownloadPath = settings.DefaultDownloadPath;
        DefaultTransfers = settings.DefaultTransfers <= 0 ? 4 : settings.DefaultTransfers;
        DefaultCheckers = settings.DefaultCheckers <= 0 ? 8 : settings.DefaultCheckers;
        DefaultDownloadExtraFlags = settings.DefaultDownloadExtraFlags;
        AlwaysGetDirectoryInfo = settings.AlwaysGetDirectoryInfo;
        DefaultDeleteDryRun = settings.DefaultDeleteDryRun;
        OperationEditors = BuildOperationEditors(settings.OperationProfiles);
        DefaultTransfers = settings.OperationProfiles.Download.Transfers ?? DefaultTransfers;
        DefaultCheckers = settings.OperationProfiles.Download.Checkers ?? DefaultCheckers;
        DefaultDownloadExtraFlags = settings.OperationProfiles.Download.ExtraFlagsText;
        ConnectionPresets = BuildConnectionPresets();
        SelectedConnectionPreset = ConnectionPresets.FirstOrDefault();
        _processService.OperationProfiles = settings.OperationProfiles;
    }

    [RelayCommand]
    private void ApplyConnectionPreset()
    {
        if (SelectedConnectionPreset?.Profiles is null)
            return;

        OperationEditors = BuildOperationEditors(CloneProfiles(SelectedConnectionPreset.Profiles));
        DefaultTransfers = SelectedConnectionPreset.Profiles.Download.Transfers ?? DefaultTransfers;
        DefaultCheckers = SelectedConnectionPreset.Profiles.Download.Checkers ?? DefaultCheckers;
        DefaultDownloadExtraFlags = SelectedConnectionPreset.Profiles.Download.ExtraFlagsText;
        ValidationMessage = $"Applied recommended {SelectedConnectionPreset.DisplayName} connection profile. Save Settings to keep it.";
    }

    [RelayCommand]
    private Task Validate()
    {
        return ValidateInternalAsync(includeRemoteCheck: false);
    }

    [RelayCommand]
    private Task TestSettings()
    {
        return ValidateInternalAsync(includeRemoteCheck: true);
    }

    [RelayCommand]
    private async Task Save()
    {
        var resolvedExePath = RCloneExePath.Trim();
        var resolvedConfigPath = RCloneConfigPath.Trim();
        var profiles = BuildOperationProfiles();
        var downloadProfile = profiles.Download;

        var settings = new AppSettings
        {
            RCloneExePath = resolvedExePath,
            RCloneConfigPath = string.Empty,
            DefaultDownloadPath = DefaultDownloadPath.Trim(),
            DefaultTransfers = downloadProfile.Transfers ?? 4,
            DefaultCheckers = downloadProfile.Checkers ?? 8,
            DefaultDownloadExtraFlags = downloadProfile.ExtraFlagsText,
            OperationProfiles = profiles,
            AlwaysGetDirectoryInfo = AlwaysGetDirectoryInfo,
            StartupValidationEnabled = false,
            DefaultDeleteDryRun = DefaultDeleteDryRun
        };

        _settingsService.Save(settings);
        _processService.RCloneExePath = resolvedExePath;
        _processService.OperationProfiles = settings.OperationProfiles;

        if (string.IsNullOrWhiteSpace(resolvedExePath))
        {
            ExecutableValidationMessage = "Executable path is empty.";
            ConfigValidationMessage = "Config path could not be resolved because the executable path is empty.";
            RemoteValidationMessage = "Remote sanity check skipped because the executable path is invalid.";
            ValidationMessage = "Settings were saved, but rclone.exe is still required before config auto-detect can work.";
            _processService.RCloneConfigPath = string.Empty;
            IsValid = false;
            return;
        }

        if (!File.Exists(resolvedExePath))
        {
            ExecutableValidationMessage = "Executable file was not found.";
            ConfigValidationMessage = "Config path could not be resolved because the executable path is invalid.";
            RemoteValidationMessage = "Remote sanity check skipped because the executable path is invalid.";
            ValidationMessage = "Settings were saved, but the rclone.exe path is invalid.";
            _processService.RCloneConfigPath = string.Empty;
            IsValid = false;
            return;
        }

        try
        {
            var version = await _processService.GetVersionAsync();
            var firstLine = version.Split('\n').FirstOrDefault() ?? version;
            ExecutableValidationMessage = $"Valid — {firstLine}";
        }
        catch (Exception ex)
        {
            ExecutableValidationMessage = $"Invalid — {ex.Message}";
            ConfigValidationMessage = "Config path could not be resolved because the executable path is invalid.";
            RemoteValidationMessage = "Remote sanity check skipped because the executable path is invalid.";
            ValidationMessage = "Settings were saved, but rclone.exe could not be executed.";
            _processService.RCloneConfigPath = string.Empty;
            IsValid = false;
            return;
        }

        if (string.IsNullOrWhiteSpace(resolvedConfigPath))
        {
            try
            {
                resolvedConfigPath = await ResolveConfigPathAsync();
                RCloneConfigPath = resolvedConfigPath;
                ConfigValidationMessage = $"Auto-detected and saved — {resolvedConfigPath}";
            }
            catch (Exception ex)
            {
                ConfigValidationMessage = $"Auto-detect failed — {ex.Message}";
                RemoteValidationMessage = "Remote sanity check skipped because the config path could not be resolved.";
                ValidationMessage = "Settings were saved, but Remotes and Jobs will not work until the config path can be resolved.";
                _processService.RCloneConfigPath = string.Empty;
                IsValid = false;
                return;
            }
        }

        if (!File.Exists(resolvedConfigPath))
        {
            ConfigValidationMessage = $"Config file was not found — {resolvedConfigPath}";
            RemoteValidationMessage = "Remote sanity check skipped because the saved config path is invalid.";
            ValidationMessage = "Settings were saved, but the config path is invalid.";
            _processService.RCloneConfigPath = string.Empty;
            IsValid = false;
            return;
        }

        settings.RCloneConfigPath = resolvedConfigPath;
        _processService.RCloneConfigPath = resolvedConfigPath;
        _settingsService.Save(settings);

        await ValidateInternalAsync(includeRemoteCheck: true);
        ValidationMessage = $"Settings were saved. Using config: {resolvedConfigPath}";
    }

    private async Task ValidateInternalAsync(bool includeRemoteCheck)
    {
        IsValid = false;

        if (string.IsNullOrWhiteSpace(RCloneExePath))
        {
            ExecutableValidationMessage = "Executable path is empty.";
            ConfigValidationMessage = "Config path cannot be checked until the executable path is valid.";
            RemoteValidationMessage = includeRemoteCheck
                ? "Remote sanity check skipped because the executable path is invalid."
                : RemoteValidationMessage;
            ValidationMessage = "Settings are incomplete.";
            return;
        }

        if (!File.Exists(RCloneExePath))
        {
            ExecutableValidationMessage = "Executable file was not found.";
            ConfigValidationMessage = "Config path cannot be checked until the executable path is valid.";
            RemoteValidationMessage = includeRemoteCheck
                ? "Remote sanity check skipped because the executable path is invalid."
                : RemoteValidationMessage;
            ValidationMessage = "Settings are invalid.";
            return;
        }

        try
        {
            _processService.RCloneExePath = RCloneExePath;
            _processService.RCloneConfigPath = RCloneConfigPath;
            _processService.OperationProfiles = BuildOperationProfiles();
            var version = await _processService.GetVersionAsync();
            var firstLine = version.Split('\n').FirstOrDefault() ?? version;
            ExecutableValidationMessage = $"Valid — {firstLine}";
        }
        catch (Exception ex)
        {
            ExecutableValidationMessage = $"Invalid — {ex.Message}";
            ValidationMessage = "Settings are invalid.";
            RemoteValidationMessage = includeRemoteCheck
                ? "Remote sanity check skipped because the executable path is invalid."
                : RemoteValidationMessage;
            return;
        }

        if (string.IsNullOrWhiteSpace(RCloneConfigPath))
        {
            try
            {
                RCloneConfigPath = await ResolveConfigPathAsync();
                _processService.RCloneConfigPath = RCloneConfigPath;
            }
            catch (Exception ex)
            {
                ConfigValidationMessage = $"Unable to auto-detect config path — {ex.Message}";
                ValidationMessage = "Settings are invalid.";
                RemoteValidationMessage = includeRemoteCheck
                    ? "Remote sanity check skipped because the config path is invalid."
                    : RemoteValidationMessage;
                return;
            }
        }

        if (!File.Exists(RCloneConfigPath))
        {
            ConfigValidationMessage = "Config file was not found.";
            ValidationMessage = "Settings are invalid.";
            RemoteValidationMessage = includeRemoteCheck
                ? "Remote sanity check skipped because the config path is invalid."
                : RemoteValidationMessage;
            return;
        }

        ConfigValidationMessage = $"Valid — {RCloneConfigPath}";
        _processService.RCloneConfigPath = RCloneConfigPath;

        if (includeRemoteCheck)
        {
            try
            {
                var result = await _processService.ExecuteDetailedAsync(new EZRClone.Models.RCloneCommandRequest
                {
                    Arguments = ["listremotes"],
                    Operation = "listremotes",
                    Category = "Validation",
                    OperationProfile = RCloneOperationProfile.RemoteValidation
                });
                var remoteCount = result.Output
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Length;
                RemoteValidationMessage = remoteCount == 0
                    ? "Valid — config loaded, but no remotes are defined."
                    : $"Valid — {remoteCount} remote(s) detected.";
            }
            catch (Exception ex)
            {
                RemoteValidationMessage = $"Remote sanity check failed — {ex.Message}";
                ValidationMessage = "Settings are only partially valid.";
                return;
            }
        }
        else
        {
            RemoteValidationMessage = "Remote sanity check skipped. Use Test Settings for an end-to-end check.";
        }

        IsValid = true;
        ValidationMessage = includeRemoteCheck
            ? "Settings are valid and ready to use."
            : "Executable and config path are valid.";
    }

    private async Task<string> ResolveConfigPathAsync()
    {
        var configPathOutput = await _processService.GetConfigFilePathAsync();
        var lines = configPathOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var resolvedPath = lines.LastOrDefault()?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(resolvedPath))
            throw new InvalidOperationException("rclone did not return a config file path.");

        return resolvedPath;
    }

    private ObservableCollection<OperationOptionsEditor> BuildOperationEditors(RCloneOperationProfileSet profiles)
    {
        return new ObservableCollection<OperationOptionsEditor>(new[]
        {
            CreateEditor(RCloneOperationProfile.Download, "Download", "Interactive downloads from Browse and Search.", profiles.Download),
            CreateEditor(RCloneOperationProfile.Browse, "Browse", "Remote directory listing requests.", profiles.Browse),
            CreateEditor(RCloneOperationProfile.Search, "Search", "Recursive search/list requests.", profiles.Search),
            CreateEditor(RCloneOperationProfile.DirectoryInfo, "Directory Info", "Recursive size and file count lookups.", profiles.DirectoryInfo),
            CreateEditor(RCloneOperationProfile.Delete, "Delete", "Interactive deletes from Browse and Search.", profiles.Delete),
            CreateEditor(RCloneOperationProfile.JobCopy, "Job Copy", "Saved jobs that run copy.", profiles.JobCopy),
            CreateEditor(RCloneOperationProfile.JobSync, "Job Sync", "Saved jobs that run sync.", profiles.JobSync),
            CreateEditor(RCloneOperationProfile.JobMove, "Job Move", "Saved jobs that run move.", profiles.JobMove),
            CreateEditor(RCloneOperationProfile.JobDelete, "Job Delete", "Saved jobs that run delete.", profiles.JobDelete),
            CreateEditor(RCloneOperationProfile.RemoteValidation, "Remote Validation", "Connection checks like listremotes and remote tests.", profiles.RemoteValidation)
        });
    }

    private static OperationOptionsEditor CreateEditor(
        RCloneOperationProfile profile,
        string displayName,
        string description,
        RCloneOperationOptions options)
    {
        return new OperationOptionsEditor
        {
            Profile = profile,
            DisplayName = displayName,
            Description = description,
            Transfers = options.Transfers?.ToString() ?? string.Empty,
            Checkers = options.Checkers?.ToString() ?? string.Empty,
            Retries = options.Retries?.ToString() ?? string.Empty,
            LowLevelRetries = options.LowLevelRetries?.ToString() ?? string.Empty,
            TimeoutMilliseconds = options.TimeoutMilliseconds?.ToString() ?? string.Empty,
            MultiThreadStreams = options.MultiThreadStreams?.ToString() ?? string.Empty,
            BandwidthLimit = options.BandwidthLimit,
            UseProgress = options.UseProgress == true,
            ExtraFlagsText = options.ExtraFlagsText
        };
    }

    private RCloneOperationProfileSet BuildOperationProfiles()
    {
        var profiles = new RCloneOperationProfileSet();
        foreach (var editor in OperationEditors)
        {
            profiles.Set(editor.Profile, editor.ToOptions());
        }
        return profiles;
    }

    private static ObservableCollection<ConnectionPresetOption> BuildConnectionPresets()
    {
        return new ObservableCollection<ConnectionPresetOption>(new[]
        {
            new ConnectionPresetOption
            {
                DisplayName = "200 Mbps",
                Summary = "Balanced defaults for solid home broadband.",
                Profiles = CreatePreset(downloadTransfers: 6, downloadCheckers: 12, multiThreadStreams: 4, browseTransfers: 2, browseCheckers: 4, deleteCheckers: 4)
            },
            new ConnectionPresetOption
            {
                DisplayName = "500 Mbps",
                Summary = "Recommended starting point for roughly 486/98 Mbps service.",
                Profiles = CreatePreset(downloadTransfers: 10, downloadCheckers: 20, multiThreadStreams: 6, browseTransfers: 3, browseCheckers: 6, deleteCheckers: 6)
            },
            new ConnectionPresetOption
            {
                DisplayName = "750 Mbps",
                Summary = "Higher-throughput defaults while keeping browse/search conservative.",
                Profiles = CreatePreset(downloadTransfers: 12, downloadCheckers: 24, multiThreadStreams: 8, browseTransfers: 4, browseCheckers: 8, deleteCheckers: 8)
            },
            new ConnectionPresetOption
            {
                DisplayName = "1000 Mbps",
                Summary = "Aggressive transfer defaults for gigabit-class service.",
                Profiles = CreatePreset(downloadTransfers: 16, downloadCheckers: 32, multiThreadStreams: 8, browseTransfers: 4, browseCheckers: 8, deleteCheckers: 8)
            }
        });
    }

    private static RCloneOperationProfileSet CreatePreset(
        int downloadTransfers,
        int downloadCheckers,
        int multiThreadStreams,
        int browseTransfers,
        int browseCheckers,
        int deleteCheckers)
    {
        return new RCloneOperationProfileSet
        {
            Download = new RCloneOperationOptions
            {
                Transfers = downloadTransfers,
                Checkers = downloadCheckers,
                Retries = 3,
                LowLevelRetries = 10,
                MultiThreadStreams = multiThreadStreams,
                UseProgress = false
            },
            Browse = new RCloneOperationOptions
            {
                Transfers = browseTransfers,
                Checkers = browseCheckers,
                Retries = 2,
                LowLevelRetries = 4,
                UseProgress = false
            },
            Search = new RCloneOperationOptions
            {
                Transfers = browseTransfers,
                Checkers = browseCheckers,
                Retries = 2,
                LowLevelRetries = 4,
                UseProgress = false
            },
            DirectoryInfo = new RCloneOperationOptions
            {
                Transfers = 1,
                Checkers = browseCheckers,
                Retries = 2,
                LowLevelRetries = 4,
                UseProgress = false
            },
            Delete = new RCloneOperationOptions
            {
                Checkers = deleteCheckers,
                Retries = 3,
                LowLevelRetries = 10,
                UseProgress = false
            },
            JobCopy = new RCloneOperationOptions
            {
                Transfers = downloadTransfers,
                Checkers = downloadCheckers,
                Retries = 3,
                LowLevelRetries = 10,
                MultiThreadStreams = multiThreadStreams,
                UseProgress = false
            },
            JobSync = new RCloneOperationOptions
            {
                Transfers = downloadTransfers,
                Checkers = downloadCheckers,
                Retries = 3,
                LowLevelRetries = 10,
                MultiThreadStreams = multiThreadStreams,
                UseProgress = false
            },
            JobMove = new RCloneOperationOptions
            {
                Transfers = downloadTransfers,
                Checkers = downloadCheckers,
                Retries = 3,
                LowLevelRetries = 10,
                MultiThreadStreams = multiThreadStreams,
                UseProgress = false
            },
            JobDelete = new RCloneOperationOptions
            {
                Checkers = deleteCheckers,
                Retries = 3,
                LowLevelRetries = 10,
                UseProgress = false
            },
            RemoteValidation = new RCloneOperationOptions
            {
                Retries = 1,
                LowLevelRetries = 1,
                UseProgress = false
            }
        };
    }

    private static RCloneOperationProfileSet CloneProfiles(RCloneOperationProfileSet source)
    {
        return new RCloneOperationProfileSet
        {
            Download = source.Download.Clone(),
            Browse = source.Browse.Clone(),
            Search = source.Search.Clone(),
            DirectoryInfo = source.DirectoryInfo.Clone(),
            Delete = source.Delete.Clone(),
            JobCopy = source.JobCopy.Clone(),
            JobSync = source.JobSync.Clone(),
            JobMove = source.JobMove.Clone(),
            JobDelete = source.JobDelete.Clone(),
            RemoteValidation = source.RemoteValidation.Clone()
        };
    }
}

public partial class OperationOptionsEditor : ObservableObject
{
    public required RCloneOperationProfile Profile { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }

    [ObservableProperty]
    private string _transfers = string.Empty;

    [ObservableProperty]
    private string _checkers = string.Empty;

    [ObservableProperty]
    private string _retries = string.Empty;

    [ObservableProperty]
    private string _lowLevelRetries = string.Empty;

    [ObservableProperty]
    private string _timeoutMilliseconds = string.Empty;

    [ObservableProperty]
    private string _multiThreadStreams = string.Empty;

    [ObservableProperty]
    private string _bandwidthLimit = string.Empty;

    [ObservableProperty]
    private bool _useProgress;

    [ObservableProperty]
    private string _extraFlagsText = string.Empty;

    public RCloneOperationOptions ToOptions()
    {
        return new RCloneOperationOptions
        {
            Transfers = ParseInt(Transfers),
            Checkers = ParseInt(Checkers),
            Retries = ParseInt(Retries),
            LowLevelRetries = ParseInt(LowLevelRetries),
            TimeoutMilliseconds = ParseInt(TimeoutMilliseconds),
            MultiThreadStreams = ParseInt(MultiThreadStreams),
            BandwidthLimit = BandwidthLimit.Trim(),
            UseProgress = UseProgress,
            ExtraFlagsText = ExtraFlagsText.Trim()
        };
    }

    private static int? ParseInt(string value)
    {
        return int.TryParse(value?.Trim(), out var parsed) && parsed > 0
            ? parsed
            : null;
    }
}

public sealed class ConnectionPresetOption
{
    public required string DisplayName { get; init; }
    public required string Summary { get; init; }
    public required RCloneOperationProfileSet Profiles { get; init; }
}
