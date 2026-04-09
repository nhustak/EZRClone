using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;
using EZRClone.Models;
using EZRClone.Services;

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
        AlwaysGetDirectoryInfo = settings.AlwaysGetDirectoryInfo;
        DefaultDeleteDryRun = settings.DefaultDeleteDryRun;
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

        var settings = new AppSettings
        {
            RCloneExePath = resolvedExePath,
            RCloneConfigPath = string.Empty,
            DefaultDownloadPath = DefaultDownloadPath.Trim(),
            AlwaysGetDirectoryInfo = AlwaysGetDirectoryInfo,
            StartupValidationEnabled = false,
            DefaultDeleteDryRun = DefaultDeleteDryRun
        };

        _settingsService.Save(settings);
        _processService.RCloneExePath = resolvedExePath;

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
                var result = await _processService.ExecuteAsync(new List<string> { "listremotes" });
                var remoteCount = result.output
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
}
