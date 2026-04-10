using EZRClone.Models;
using EZRClone.Services;
using EZRClone.ViewModels;
using HotCoreUtility.RClone;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AppRCloneCommandRequest = EZRClone.Models.RCloneCommandRequest;
using AppRCloneCommandResult = EZRClone.Models.RCloneCommandResult;

namespace EZRClone.Tests;

[TestClass]
public class ConfigViewModelTests
{
    [TestMethod]
    public void LoadRemotes_BlankSavedConfigPath_ShowsInvalidStatus()
    {
        var tempExePath = typeof(ConfigViewModelTests).Assembly.Location;
        var tempConfigPath = Path.GetTempFileName();
        File.WriteAllText(tempConfigPath, """
            [alpha]
            type = s3
            region = us-east-1
            """);

        try
        {
            var settingsService = new FakeSettingsService(new AppSettings
            {
                RCloneExePath = tempExePath,
                RCloneConfigPath = string.Empty
            });
            var processService = new FakeProcessService
            {
                ConfigPathText = tempConfigPath
            };
            var configService = new RCloneConfigService();

            var viewModel = new ConfigViewModel(configService, processService, settingsService);

            viewModel.LoadRemotesCommand.Execute(null);

            Assert.AreEqual(string.Empty, settingsService.Current.RCloneConfigPath);
            Assert.AreEqual(string.Empty, viewModel.ConfigFilePath);
            Assert.AreEqual(string.Empty, processService.RCloneConfigPath);
            Assert.IsFalse(viewModel.IsConfigPathReady);
            Assert.AreEqual(0, viewModel.Remotes.Count);
            StringAssert.Contains(viewModel.StatusMessage, "Config file path is missing or invalid");
        }
        finally
        {
            File.Delete(tempConfigPath);
        }
    }

    private sealed class FakeSettingsService : IAppSettingsService
    {
        public FakeSettingsService(AppSettings initial)
        {
            Current = initial;
        }

        public AppSettings Current { get; private set; }

        public AppSettings Load() => Current;

        public void Save(AppSettings settings)
        {
            Current = settings;
        }
    }

    private sealed class FakeProcessService : IRCloneProcessService
    {
        public string RCloneExePath { get; set; } = string.Empty;
        public string RCloneConfigPath { get; set; } = string.Empty;
        public RCloneOperationProfileSet OperationProfiles { get; set; } = new();
        public string ConfigPathText { get; set; } = string.Empty;

        public Task<string> RunAsync(string arguments) => Task.FromResult(string.Empty);

        public Task<(int exitCode, string output, string error)> ExecuteAsync(List<string> args) =>
            Task.FromResult((0, string.Empty, string.Empty));

        public Task<string> GetVersionAsync() => Task.FromResult("rclone v1.61.1");

        public Task<string> GetConfigFilePathAsync() => Task.FromResult(ConfigPathText);

        public Task<AppRCloneCommandResult> ExecuteDetailedAsync(AppRCloneCommandRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AppRCloneCommandResult
            {
                ExitCode = 0,
                Output = string.Empty,
                Error = string.Empty,
                StartedAt = DateTime.UtcNow,
                FinishedAt = DateTime.UtcNow
            });
    }
}
