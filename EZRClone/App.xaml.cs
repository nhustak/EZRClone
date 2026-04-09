using System.Windows;
using System.IO;
using System.Windows.Threading;
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

        try
        {
            var services = new ServiceCollection();

            services.AddSingleton<IAppSettingsService, AppSettingsService>();
            services.AddSingleton<IRCloneConfigService, RCloneConfigService>();
            services.AddSingleton<IRCloneProcessService, RCloneProcessService>();
            services.AddSingleton<IJobStorageService, JobStorageService>();
            services.AddSingleton<IBatchImportService, BatchImportService>();
            services.AddSingleton<IAppLogService, AppLogService>();

            services.AddSingleton<MainWindowViewModel>();
            services.AddSingleton<ConfigViewModel>();
            services.AddSingleton<SettingsViewModel>();
            services.AddSingleton<JobsViewModel>();
            services.AddSingleton<BrowseViewModel>();
            services.AddSingleton<SearchViewModel>();
            services.AddSingleton<LogViewModel>();

            services.AddSingleton<MainWindow>();

            _serviceProvider = services.BuildServiceProvider();

            var settingsService = _serviceProvider.GetRequiredService<IAppSettingsService>();
            var processService = _serviceProvider.GetRequiredService<IRCloneProcessService>();
            var logService = _serviceProvider.GetRequiredService<IAppLogService>();
            var settings = settingsService.Load();
            processService.RCloneExePath = settings.RCloneExePath;
            processService.RCloneConfigPath = settings.RCloneConfigPath;

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
            jobsVm.OpenJobHistoryRequested = jobId =>
            {
                logVm.FocusJobHistory(jobId);
                mainVm.NavigateToLog();
            };

            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.DataContext = _serviceProvider.GetRequiredService<MainWindowViewModel>();
            mainWindow.Show();

            mainWindow.Dispatcher.BeginInvoke(
                new Action(() => configVm.LoadRemotesCommand.Execute(null)),
                DispatcherPriority.ContextIdle);

            _ = logService.LoadRunHistoryAsync();
        }
        catch (Exception ex)
        {
            WriteStartupFailure(ex);
            MessageBox.Show(
                $"EZRClone failed to start.{Environment.NewLine}{Environment.NewLine}{ex.Message}{Environment.NewLine}{Environment.NewLine}A startup error log was written beside the app.",
                "EZRClone Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }

    private static void WriteStartupFailure(Exception ex)
    {
        try
        {
            var logPath = Path.Combine(AppContext.BaseDirectory, "EZRClone-startup-error.txt");
            var content = $"{DateTime.Now:u}{Environment.NewLine}{ex}{Environment.NewLine}";
            File.WriteAllText(logPath, content);
        }
        catch
        {
            // Best effort only.
        }
    }
}
