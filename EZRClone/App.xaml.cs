using System.Windows;
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

        // ViewModels
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<ConfigViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<JobsViewModel>();
        services.AddSingleton<LogViewModel>();

        // Window
        services.AddSingleton<MainWindow>();

        _serviceProvider = services.BuildServiceProvider();

        // Load settings and configure process service
        var settingsService = _serviceProvider.GetRequiredService<IAppSettingsService>();
        var processService = _serviceProvider.GetRequiredService<IRCloneProcessService>();
        var settings = settingsService.Load();
        processService.RCloneExePath = settings.RCloneExePath;

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
