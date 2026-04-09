using System.Windows;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using EZRClone.Services;
using EZRClone.ViewModels;

namespace EZRClone;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();

        // Services
        services.AddSingleton<IAppSettingsService, AppSettingsService>();
        services.AddSingleton<IRCloneConfigService, RCloneConfigService>();
        services.AddSingleton<IRCloneProcessService, RCloneProcessService>();
        services.AddSingleton<IJobStorageService, JobStorageService>();
        services.AddSingleton<IBatchImportService, BatchImportService>();
        services.AddSingleton<IAppLogService, AppLogService>();

        // ViewModels
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<ConfigViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<JobsViewModel>();
        services.AddSingleton<BrowseViewModel>();
        services.AddSingleton<SearchViewModel>();
        services.AddSingleton<LogViewModel>();

        // Window
        services.AddSingleton<MainWindow>();

        _serviceProvider = services.BuildServiceProvider();

        // Load settings and configure process service
        var settingsService = _serviceProvider.GetRequiredService<IAppSettingsService>();
        var processService = _serviceProvider.GetRequiredService<IRCloneProcessService>();
        var logService = _serviceProvider.GetRequiredService<IAppLogService>();
        var settings = settingsService.Load();
        processService.RCloneExePath = settings.RCloneExePath;
        logService.LoadRunHistoryAsync().GetAwaiter().GetResult();

        // Wire up Config → Browse navigation
        var configVm = _serviceProvider.GetRequiredService<ConfigViewModel>();
        var browseVm = _serviceProvider.GetRequiredService<BrowseViewModel>();
        var jobsVm = _serviceProvider.GetRequiredService<JobsViewModel>();
        var logVm = _serviceProvider.GetRequiredService<LogViewModel>();
        var mainVm = _serviceProvider.GetRequiredService<MainWindowViewModel>();
        configVm.NavigateToRemoteBrowse = remoteName =>
        {
            browseVm.SelectedRemote = remoteName;
            mainVm.NavigateCommand.Execute("Browse");
        };
        configVm.NavigateToSettings = mainVm.NavigateToSettings;
        jobsVm.OpenJobHistoryRequested = jobId =>
        {
            logVm.FocusJobHistory(jobId);
            mainVm.NavigateToLog();
        };

        if (settings.StartupValidationEnabled)
        {
            var warnings = new List<string>();
            if (string.IsNullOrWhiteSpace(settings.RCloneExePath) || !File.Exists(settings.RCloneExePath))
                warnings.Add("rclone.exe path is missing or invalid");
            if (string.IsNullOrWhiteSpace(settings.RCloneConfigPath) || !File.Exists(settings.RCloneConfigPath))
                warnings.Add("rclone.conf path is missing or invalid");

            if (warnings.Count > 0)
            {
                var message = $"Startup validation found issues: {string.Join("; ", warnings)}.";
                mainVm.ShowAppWarning(message);
                mainVm.NavigateToSettings();
                logService.AddSessionEntry(new Models.AppLogEntry
                {
                    Severity = Models.AppLogSeverity.Warning,
                    Category = "Startup",
                    Message = message
                });
            }
            else
            {
                logService.AddSessionEntry(new Models.AppLogEntry
                {
                    Category = "Startup",
                    Message = "Startup validation passed."
                });
            }
        }

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.DataContext = _serviceProvider.GetRequiredService<MainWindowViewModel>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
