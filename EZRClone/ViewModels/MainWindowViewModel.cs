using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EZRClone.Models;

namespace EZRClone.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private object? _currentView;

    [ObservableProperty]
    private string _selectedNav = "Config";

    [ObservableProperty]
    private string? _appWarningMessage;

    [ObservableProperty]
    private bool _isAppWarningVisible;

    [ObservableProperty]
    private AppLogSeverity _appWarningSeverity = AppLogSeverity.Warning;

    public string VersionText { get; } =
        $"v{Assembly.GetExecutingAssembly().GetName().Version}";

    private readonly ConfigViewModel _configViewModel;
    private readonly SettingsViewModel _settingsViewModel;
    private readonly JobsViewModel _jobsViewModel;
    private readonly LogViewModel _logViewModel;
    private readonly BrowseViewModel _browseViewModel;
    private readonly SearchViewModel _searchViewModel;

    public MainWindowViewModel(
        ConfigViewModel configViewModel,
        SettingsViewModel settingsViewModel,
        JobsViewModel jobsViewModel,
        LogViewModel logViewModel,
        BrowseViewModel browseViewModel,
        SearchViewModel searchViewModel)
    {
        _configViewModel = configViewModel;
        _settingsViewModel = settingsViewModel;
        _jobsViewModel = jobsViewModel;
        _logViewModel = logViewModel;
        _browseViewModel = browseViewModel;
        _searchViewModel = searchViewModel;

        CurrentView = _configViewModel;
    }

    public void ShowAppWarning(string message, AppLogSeverity severity = AppLogSeverity.Warning)
    {
        AppWarningMessage = message;
        AppWarningSeverity = severity;
        IsAppWarningVisible = !string.IsNullOrWhiteSpace(message);
    }

    public void ClearAppWarning()
    {
        AppWarningMessage = null;
        IsAppWarningVisible = false;
    }

    public void NavigateToSettings()
    {
        Navigate("Settings");
    }

    public void NavigateToLog()
    {
        Navigate("Log");
    }

    [RelayCommand]
    private void Navigate(string destination)
    {
        SelectedNav = destination;
        CurrentView = destination switch
        {
            "Config" => _configViewModel,
            "Settings" => _settingsViewModel,
            "Jobs" => _jobsViewModel,
            "Browse" => _browseViewModel,
            "Search" => _searchViewModel,
            "Log" => _logViewModel,
            _ => _configViewModel
        };
    }

    [RelayCommand]
    private void OpenAppWarningTarget()
    {
        NavigateToSettings();
    }
}
