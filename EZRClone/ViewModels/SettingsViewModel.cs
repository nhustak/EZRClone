using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    private string _validationMessage = string.Empty;

    [ObservableProperty]
    private bool _isValid;

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
    }

    [RelayCommand]
    private async Task Validate()
    {
        if (string.IsNullOrWhiteSpace(RCloneExePath))
        {
            ValidationMessage = "Path is empty.";
            IsValid = false;
            return;
        }

        if (!System.IO.File.Exists(RCloneExePath))
        {
            ValidationMessage = "File not found.";
            IsValid = false;
            return;
        }

        try
        {
            _processService.RCloneExePath = RCloneExePath;
            var version = await _processService.GetVersionAsync();
            var firstLine = version.Split('\n').FirstOrDefault() ?? version;
            ValidationMessage = $"Valid — {firstLine}";
            IsValid = true;

            // Auto-detect config path if not set
            if (string.IsNullOrWhiteSpace(RCloneConfigPath))
            {
                var configPath = await _processService.GetConfigFilePathAsync();
                // rclone config file outputs a line like: "Configuration file is stored at:\nC:\Users\..."
                var lines = configPath.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                RCloneConfigPath = lines.LastOrDefault()?.Trim() ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            ValidationMessage = $"Invalid — {ex.Message}";
            IsValid = false;
        }
    }

    [RelayCommand]
    private async Task Save()
    {
        var settings = new AppSettings
        {
            RCloneExePath = RCloneExePath.Trim(),
            RCloneConfigPath = RCloneConfigPath.Trim(),
            DefaultDownloadPath = DefaultDownloadPath.Trim(),
            AlwaysGetDirectoryInfo = AlwaysGetDirectoryInfo
        };

        _processService.RCloneExePath = settings.RCloneExePath;
        _settingsService.Save(settings);
        await Validate();
    }
}
